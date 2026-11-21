import {test,expect} from '@playwright/test';
async function login(page,user='ogrenci1') {
  // Wait for the InteractiveServer render acknowledgement, not just prerendered inputs.
  const ready=page.waitForEvent('websocket',{predicate:ws=>ws.url().includes('/_blazor')})
    .then(ws=>ws.waitForEvent('framesent',{predicate:e=>Buffer.from(e.payload).includes(Buffer.from('OnRenderCompleted'))}));
  await page.goto('/login');
  await ready;
  await page.getByLabel('Kullanıcı adı',{exact:true}).fill(user);
  await page.getByLabel('Parola',{exact:true}).fill(user); // shared synthetic fixture only
  await page.getByRole('button',{name:'Giriş yap',exact:true}).click();
  await expect(page).toHaveURL(/\/$/);
  const response=await page.request.get('/api/v1/antiforgery');
  expect(response.ok()).toBeTruthy();
  return {'X-CSRF-TOKEN':(await response.json()).token};
}
async function draft(page,headers) {
  const r=await page.request.post('/api/v1/file-drafts/',{headers});expect(r.status()).toBe(201);return (await r.json()).draftId;
}
async function upload(page,headers,id,name,buffer) {
  return page.request.post('/api/v1/file-drafts/'+id+'/files',{headers,multipart:{file:{name,mimeType:'application/octet-stream',buffer}}});
}
async function course(page) {
  const r=await page.request.get('/api/v1/courses');expect(r.ok()).toBeTruthy();
  const rows=await r.json();const item=rows.find(c=>c.shortName==='SENG101');expect(item).toBeTruthy();return item.id;
}
test('Gerçek AUTH girişi ve gerçek ders bağlamında ASG listesi',async({page})=>{
  await login(page);const id=await course(page);
  await page.goto('/courses/'+id+'/assignments');
  await expect(page.getByRole('heading',{name:'Ders ödevleri'})).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});
test('CRS bölüm modeli olmadan ödev oluşturma güvenli reddedilir',async({page})=>{
  test.skip(!!process.env.E2E_SECTION_ID,'Gerçek bölüm adaptörü bağlandığında tam senaryolar çalışır.');
  const headers=await login(page,'ogretmen1');const id=await course(page);
  const r=await page.request.post('/api/v1/courses/'+id+'/assignments',{headers,data:{sectionId:1,title:'Blocked fixture',requiresConfirmation:true,enabledSubmissionTypes:['text']}});
  expect(r.status()).toBe(422);expect(await r.text()).toContain('invalid_section');
});
test('Gerçek FILE ve ClamAV: temiz dosya, benzersiz ad, geçersiz tür ve EICAR',async({page})=>{
  const headers=await login(page);const id=await draft(page,headers);
  for(const name of ['work.txt','work (1).txt']) {
    const r=await upload(page,headers,id,'work.txt',Buffer.from('synthetic work'));
    expect(r.status()).toBe(201);expect((await r.json()).name).toBe(name);
  }
  expect((await upload(page,headers,id,'wrong.pdf',Buffer.from('plain text'))).status()).toBe(415);
  const eicar=Buffer.from('X5O!P%@AP[4'+'\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*');
  const rejected=await upload(page,headers,id,'eicar.txt',eicar);expect(rejected.status()).toBe(422);expect(await rejected.text()).toContain('virus_detected');
  const files=await (await page.request.get('/api/v1/file-drafts/'+id+'/files')).json();expect(files.files).toHaveLength(2);
});
test('Başka öğrencinin taslağına erişim reddedilir',async({page,browser})=>{
  const headers=await login(page);const id=await draft(page,headers);
  const context=await browser.newContext({baseURL:process.env.E2E_BASE_URL || 'http://localhost:8080'});const other=await context.newPage();
  try {const h=await login(other,'ogrenci2');expect((await upload(other,h,id,'private.txt',Buffer.from('data'))).status()).toBe(404);}
  finally {await context.close();}
});
for(const type of ['text','file']) test('Tam senaryo '+(type==='text'?'A':'B')+': AUTH/CRS/ASG/FILE/GRD',async({page,browser})=>{
  test.skip(!process.env.E2E_SECTION_ID,'ENGEL: gerçek CRS bölüm modeli ve ICourseSectionQuery gerekli; sahte bölüm kullanılmaz.');
  const headers=await login(page,'ogretmen1');const courseId=await course(page);
  const created=await page.request.post('/api/v1/courses/'+courseId+'/assignments',{headers,data:{sectionId:Number(process.env.E2E_SECTION_ID),title:'E2E '+type+' '+crypto.randomUUID(),requiresConfirmation:true,enabledSubmissionTypes:[type]}});
  expect(created.status()).toBe(201);const assignment=await created.json();
  const context=await browser.newContext({baseURL:process.env.E2E_BASE_URL || 'http://localhost:8080'});const learner=await context.newPage();
  try {
    await login(learner);await learner.goto('/assignments/'+assignment.id+'/submit');
    if(type==='text') await learner.getByLabel('Teslim metni').fill('E2E synthetic text');
    else {
      await learner.locator('[data-file-input]').setInputFiles({name:'work.txt',mimeType:'text/plain',buffer:Buffer.from('E2E synthetic file')});
      await learner.getByRole('button',{name:'Dosyaları yükle ve tara'}).click();
      await expect(learner.locator('[data-draft-files]')).toContainText('work.txt');
    }
    await learner.getByRole('button',{name:'Kaydet',exact:true}).click();
    await expect(learner.getByText('Teslim kaydedildi.')).toBeVisible();
    await learner.getByRole('checkbox').check();await learner.getByRole('button',{name:'Teslimi gönder',exact:true}).click();
    await expect(learner.getByText('Teslim gönderildi.')).toBeVisible();
    const mine=await (await learner.request.get('/api/v1/assignments/'+assignment.id+'/my-submission')).json();
    await page.goto('/assignments/'+assignment.id+'/submissions/'+mine.submission.id);
    await expect(page.getByRole('alert')).toHaveCount(0);
    if(type==='file') {
      const files=await (await page.request.get('/api/v1/file-areas/'+assignment.id+'/mod_assign/submission/'+mine.submission.id+'/files')).json();
      expect((await page.request.get('/api/v1/files/'+files.files[0].fileId+'/content')).status()).toBe(200);
    }
    const grade=await page.request.put('/api/v1/assignments/'+assignment.id+'/learners/'+mine.submission.learnerId+'/attempts/0/grade',{data:{score:85,feedback:'E2E',version:0}});
    expect(grade.ok()).toBeTruthy();
    const report=await learner.request.get('/api/v1/courses/'+courseId+'/my-grades');expect(report.ok()).toBeTruthy();expect(await report.text()).toContain('85');
  } finally {await context.close();}
});

test('Ortak FILE yöneticisi: öğretmen yükler, kaydeder, öğrenci indirir, öğretmen siler', async({page,browser})=>{
  await login(page,'ogretmen1');
  const courseId=await course(page);
  const name='shared-'+crypto.randomUUID()+'.txt';
  const bytes=Buffer.from('Shared FILE integration '+crypto.randomUUID());
  await page.goto('/courses/'+courseId+'/files');
  const manager=page.getByTestId('file-manager');
  await expect(manager.locator('input[type=file]')).toBeEnabled({timeout:15000});
  await manager.locator('input[type=file]').setInputFiles({name,mimeType:'application/octet-stream',buffer:bytes});
  await manager.getByTestId('upload').click();
  await expect(manager.getByTestId('draft')).toContainText(name);
  await expect(manager.getByTestId('upload-unavailable')).toHaveCount(0);
  await manager.getByTestId('commit').click();
  const row=manager.locator('tr').filter({hasText:name});
  await expect(row).toBeVisible();
  const fileId=Number(await row.getAttribute('data-file-id'));
  const context=await browser.newContext({baseURL:process.env.E2E_BASE_URL||'http://localhost:8080'});
  try {
    const student=await context.newPage(); await login(student);
    await student.goto('/courses/'+courseId+'/files');
    const link=student.getByTestId('download').filter({hasText:name});
    await expect(link).toBeVisible();
    const content=await student.request.get('/api/v1/files/'+fileId+'/content?download=true');
    expect(content.status()).toBe(200); expect(content.headers()['content-type']).toContain('text/plain');
    expect(await content.body()).toEqual(bytes);
    const downloadEvent=student.waitForEvent('download');
    await link.click(); const download=await downloadEvent;
    expect(download.suggestedFilename()).toBe(name);
    expect(await download.failure()).toBeNull();
    expect((await student.request.delete('/api/v1/files/'+fileId)).status()).toBe(404);
    await row.getByTestId('delete').click();
    await page.getByRole('button',{name:'Evet, sil',exact:true}).click();
    await expect(row).toHaveCount(0);
    expect((await student.request.get('/api/v1/files/'+fileId+'/content')).status()).toBe(404);
  } finally {await context.close();}
});

test('Gerçek öğrenci dosya yönetici ayarlarını okuyamaz veya değiştiremez',async({page})=>{
  await login(page);
  await page.goto('/admin/files');
  await expect(page.getByRole('alert')).toBeVisible();
  expect((await page.request.get('/api/v1/admin/file-settings')).status()).toBe(403);
  expect((await page.request.put('/api/v1/admin/file-settings',{data:{maxFileBytes:1,userQuotaBytes:1,draftTtlHours:1,trashRetentionHours:1}})).status()).toBe(403);
  expect((await page.request.post('/api/v1/admin/file-maintenance/run')).status()).toBe(403);
});
