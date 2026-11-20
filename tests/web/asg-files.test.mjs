import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import vm from 'node:vm';
const source=await readFile(new URL('../../src/Moodle.Web/wwwroot/asg-files.js',import.meta.url),'utf8');
for(const fail of [false,true]) test('ASG upload blocks save until scan finishes; failure='+fail,async()=>{
  let click,finish;
  const waiting=new Promise(resolve=>finish=resolve);
  const save={disabled:false},alreadyDisabled={disabled:true};
  const status={textContent:''},field={value:''};const list={items:[],replaceChildren(){this.items=[];},append(item){this.items.push(item);}};
  const area={querySelector:selector=>({'[data-upload-status]':status,'[data-draft-id]':field,'[data-file-input]':{files:[{name:'work.txt'}]},'[data-draft-files]':list}[selector])};
  const form={querySelectorAll:()=>[save,alreadyDisabled]};
  const button={disabled:false,closest:selector=>selector==='form'?form:area};
  let uploads=0;
  const fetch=async(path,options={})=>{
    if(path==='/api/v1/antiforgery')return {json:async()=>({token:'test-token'})};
    assert.equal(options.credentials,'same-origin');assert.equal(options.headers['X-CSRF-TOKEN'],'test-token');
    if(path==='/api/v1/file-drafts/')return {ok:true,json:async()=>({draftId:12})};
    if(options.method==='POST') {uploads++;await waiting;return {ok:!fail,json:async()=>fail?{error:{message:'Tarama tamamlanamadı'}}:{fileId:1}};}
    return {ok:true,json:async()=>({files:fail?[]:[{name:'work.txt',sizeBytes:4}]})};
  };
  vm.runInNewContext(source,{document:{addEventListener:(name,handler)=>click=handler,createElement:()=>({textContent:''})},fetch,FormData:class {append(){}},Error});
  const pending=click({target:{closest:()=>button}});
  assert.equal(save.disabled,true);assert.equal(button.disabled,true);
  finish();await pending;
  assert.equal(save.disabled,false);assert.equal(alreadyDisabled.disabled,true);assert.equal(button.disabled,false);assert.equal(field.value,12);assert.equal(uploads,1);
  assert.equal(list.items.length,fail?0:1);
  assert.match(status.textContent,fail?/Tarama tamamlanamadı/:/Tarama tamamlandı/);
});
