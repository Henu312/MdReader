const input = document.querySelector('#file-input');
const status = document.querySelector('#status');

input.addEventListener('change', async () => {
  const file = input.files[0];
  if (!file) return;

  status.textContent = '正在读取文件...';
  const text = await file.text();
  await chrome.storage.local.set({ activeDocument: { name: file.name, text, updatedAt: Date.now() } });
  await chrome.tabs.create({ url: chrome.runtime.getURL('viewer.html') });
  window.close();
});
