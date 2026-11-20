// ASG-specific adapter; replaceable by the team's future shared file manager.
async function request(path, options = {}) {
    const token = await fetch("/api/v1/antiforgery", { credentials: "same-origin" }).then(r => r.json());
    const response = await fetch(path, { ...options, credentials: "same-origin",
        headers: { ...options.headers, "X-CSRF-TOKEN": token.token } });
    const body = await response.json();
    if (!response.ok) throw new Error(body.error?.message ?? "İşlem tamamlanamadı.");
    return body;
}
document.addEventListener("click", async event => {
    const button = event.target.closest("[data-upload-files]");
    if (!button || button.disabled) return;
    const area = button.closest("[data-asg-upload]");
    const status = area.querySelector("[data-upload-status]");
    const field = area.querySelector("[data-draft-id]");
    const form = button.closest("form");
    const submitButtons = [...form.querySelectorAll('button[type="submit"]')];
    const previousDisabled = submitButtons.map(item => item.disabled);
    submitButtons.forEach(item => item.disabled = true);
    button.disabled = true;
    try {
        const selected = area.querySelector("[data-file-input]").files;
        if (!selected.length) throw new Error("Bir dosya seçin.");
        if (!field.value) field.value = (await request("/api/v1/file-drafts/", { method: "POST" })).draftId;
        for (const file of selected) {
            status.textContent = file.name + " yükleniyor ve taranıyor…";
            const form = new FormData(); form.append("file", file);
            await request("/api/v1/file-drafts/"+field.value+"/files", { method: "POST", body: form });
        }
        status.textContent = "Tarama tamamlandı. Teslimi kaydetmek için Kaydet'e basın.";
    } catch (error) { status.textContent = error.message; }
    finally {
        if (field.value) {
            try {
                const data = await request("/api/v1/file-drafts/"+field.value+"/files");
                const list = area.querySelector("[data-draft-files]"); list.replaceChildren();
                for (const file of data.files) {
                    const item = document.createElement("li");
                    item.textContent = file.name+" ("+file.sizeBytes+" bayt)"; list.append(item);
                }
            } catch { }
        }
        submitButtons.forEach((item, index) => item.disabled = previousDisabled[index]);
        button.disabled = false;
    }
});
