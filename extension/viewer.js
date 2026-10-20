const content = document.querySelector('#content');
const emptyState = document.querySelector('#empty-state');
const fileName = document.querySelector('#file-name');
const status = document.querySelector('#status');
const fileInput = document.querySelector('#file-input');
const dropZone = document.querySelector('#drop-zone');
const themeToggle = document.querySelector('#theme-toggle');
const editToggle = document.querySelector('#edit-toggle');
const previewToggle = document.querySelector('#preview-toggle');
const saveFile = document.querySelector('#save-file');
const editorLayout = document.querySelector('#editor-layout');
const editor = document.querySelector('#editor');
const syntaxSuggestions = document.querySelector('#syntax-suggestions');
const previewPane = document.querySelector('#preview-pane');
const editorPreview = document.querySelector('#editor-preview');
const tocPanel = document.querySelector('#toc-panel');
const tocLinks = document.querySelector('#toc-links');
let documentBaseUrl = '';
let documentAssetBaseUrl = '';
let documentSaveUrl = '';
let currentDocument = { name: '未命名.md', text: '' };
let editing = false;
let previewVisible = localStorage.getItem('md-preview-visible') !== 'false';
let syntaxMatch = null;
let syntaxResults = [];
let activeSyntaxIndex = 0;

const syntaxTemplates = [
  { name: '图片', keywords: ['image', 'img', '图片'], code: '![图片说明](图片路径)', text: '![图片说明](图片路径)', selectStart: 2, selectLength: 4 },
  { name: 'HTML 图片', keywords: ['image', 'img', 'html-image', '图片'], code: '<img src="图片路径" alt="图片说明">', text: '<img src="图片路径" alt="图片说明">', selectStart: 10, selectLength: 4 },
  { name: '链接', keywords: ['link', 'url', '链接'], code: '[链接文字](https://example.com)', text: '[链接文字](https://example.com)', selectStart: 1, selectLength: 4 },
  { name: '一级标题', keywords: ['h1', 'heading', 'title', '标题'], code: '# 标题', text: '# 标题', selectStart: 2, selectLength: 2 },
  { name: '二级标题', keywords: ['h2', 'heading', 'subtitle', '标题'], code: '## 标题', text: '## 标题', selectStart: 3, selectLength: 2 },
  { name: '粗体', keywords: ['bold', 'strong', '粗体'], code: '**粗体文字**', text: '**粗体文字**', selectStart: 2, selectLength: 4 },
  { name: '斜体', keywords: ['italic', 'em', '斜体'], code: '*斜体文字*', text: '*斜体文字*', selectStart: 1, selectLength: 4 },
  { name: '引用', keywords: ['quote', 'blockquote', '引用'], code: '> 引用内容', text: '> 引用内容', selectStart: 2, selectLength: 4 },
  { name: '无序列表', keywords: ['list', 'ul', '列表'], code: '- 列表项', text: '- 列表项', selectStart: 2, selectLength: 3 },
  { name: '有序列表', keywords: ['list', 'ol', '列表'], code: '1. 列表项', text: '1. 列表项', selectStart: 3, selectLength: 3 },
  { name: '任务列表', keywords: ['task', 'todo', 'checkbox', '任务'], code: '- [ ] 待办事项', text: '- [ ] 待办事项', selectStart: 6, selectLength: 4 },
  { name: '行内代码', keywords: ['code', 'inline-code', '代码'], code: '`代码`', text: '`代码`', selectStart: 1, selectLength: 2 },
  { name: '代码块', keywords: ['code', 'codeblock', '代码块'], code: '```语言  代码内容  ```', text: '```语言\n代码内容\n```', selectStart: 6, selectLength: 4 },
  { name: '表格', keywords: ['table', '表格'], code: '| 标题 | 标题 |', text: '| 标题1 | 标题2 |\n| --- | --- |\n| 内容1 | 内容2 |', selectStart: 2, selectLength: 3 },
  { name: '分隔线', keywords: ['hr', 'divider', '分隔线'], code: '---', text: '---', selectStart: 3, selectLength: 0 }
];

function escapeHtml(value) {
  return value.replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]);
}

function safeUrl(value) {
  const url = value.trim();
  const embeddedImage = /^data:image\/[a-z0-9.+-]+;base64,[a-z0-9+/=\s]+$/i;
  if (/^(https?:|mailto:)/i.test(url) || embeddedImage.test(url)) return url;
  if (documentAssetBaseUrl && !/^[a-z][a-z0-9+.-]*:/i.test(url)) {
    return `${documentAssetBaseUrl}?path=${encodeURIComponent(url)}`;
  }
  if (documentBaseUrl && !/^[a-z][a-z0-9+.-]*:/i.test(url)) {
    try { return new URL(url, documentBaseUrl).href; } catch { return ''; }
  }
  return '';
}

function inline(source) {
  let value = escapeHtml(source);
  value = value.replace(/`([^`]+)`/g, '<code>$1</code>');
  value = value.replace(/!\[([^\]]*)\]\(([^ )]+)(?:\s+&quot;[^&]*&quot;)?\)/g, (_, alt, url) => {
    const safe = safeUrl(url); return safe ? `<img src="${safe}" alt="${alt}">` : alt;
  });
  value = value.replace(/\[([^\]]+)\]\(([^ )]+)\)/g, (_, label, url) => {
    const safe = safeUrl(url); return safe ? `<a href="${safe}" target="_blank" rel="noopener noreferrer">${label}</a>` : label;
  });
  value = value.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  value = value.replace(/~~([^~]+)~~/g, '<del>$1</del>');
  value = value.replace(/(^|[^*])\*([^*]+)\*/g, '$1<em>$2</em>');
  return value;
}

function extractHeadings(markdown) {
  const entries = [];
  let inCode = false;
  let index = 0;
  const lines = markdown.replace(/\r\n?/g, '\n').split('\n');
  for (const line of lines) {
    if (/^```/.test(line)) {
      inCode = !inCode;
      continue;
    }
    if (inCode) continue;
    const heading = line.match(/^(#{1,6})\s+(.+)$/);
    if (!heading) continue;
    entries.push({
      level: heading[1].length,
      title: heading[2].replace(/`([^`]+)`/g, '$1').trim(),
      id: `heading-${index}`
    });
    index += 1;
  }
  return entries;
}

function render(markdown, includeHeadingIds = true) {
  const lines = markdown.replace(/\r\n?/g, '\n').split('\n');
  const headings = extractHeadings(markdown);
  const html = [];
  let paragraph = [];
  let list = null;
  let inCode = false;
  let code = [];
  let headingNumber = 0;

  const flushParagraph = () => {
    if (paragraph.length) html.push(`<p>${inline(paragraph.join(' '))}</p>`);
    paragraph = [];
  };
  const closeList = () => {
    if (list) html.push(`</${list}>`);
    list = null;
  };

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];
    if (/^```/.test(line)) {
      flushParagraph(); closeList();
      if (inCode) html.push(`<pre><code>${escapeHtml(code.join('\n'))}</code></pre>`);
      inCode = !inCode; code = [];
      continue;
    }
    if (inCode) { code.push(line); continue; }
    if (!line.trim()) { flushParagraph(); closeList(); continue; }
    const heading = line.match(/^(#{1,6})\s+(.+)$/);
    if (heading) {
      flushParagraph(); closeList();
      const level = heading[1].length;
      const headingId = headings[headingNumber]?.id || `heading-${headingNumber}`;
      html.push(`<h${level}${includeHeadingIds ? ` id="${headingId}"` : ''}>${inline(heading[2])}</h${level}>`);
      headingNumber += 1;
      continue;
    }
    if (/^\s{0,3}([-*_])(?:\s*\1){2,}\s*$/.test(line)) { flushParagraph(); closeList(); html.push('<hr>'); continue; }
    const quote = line.match(/^>\s?(.*)$/);
    if (quote) { flushParagraph(); closeList(); html.push(`<blockquote>${inline(quote[1])}</blockquote>`); continue; }
    const item = line.match(/^\s*([-+*]|\d+\.)\s+(.+)$/);
    if (item) {
      flushParagraph();
      const kind = /\d+\./.test(item[1]) ? 'ol' : 'ul';
      if (list !== kind) { closeList(); html.push(`<${kind}>`); list = kind; }
      const task = item[2].match(/^\[([ xX])\]\s+(.*)$/);
      html.push(task ? `<li class="task"><input type="checkbox" disabled ${task[1] !== ' ' ? 'checked' : ''}> ${inline(task[2])}</li>` : `<li>${inline(item[2])}</li>`);
      continue;
    }
    const tableDivider = /^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$/.test(lines[index + 1] || '');
    if (line.includes('|') && tableDivider) {
      flushParagraph(); closeList();
      const headers = line.replace(/^\||\|$/g, '').split('|').map(cell => `<th>${inline(cell.trim())}</th>`).join('');
      html.push(`<table><thead><tr>${headers}</tr></thead><tbody>`); index += 2;
      while (index < lines.length && lines[index].includes('|') && lines[index].trim()) {
        const cells = lines[index].replace(/^\||\|$/g, '').split('|').map(cell => `<td>${inline(cell.trim())}</td>`).join('');
        html.push(`<tr>${cells}</tr>`); index += 1;
      }
      html.push('</tbody></table>'); index -= 1; continue;
    }
    closeList(); paragraph.push(line.trim());
  }
  if (inCode) html.push(`<pre><code>${escapeHtml(code.join('\n'))}</code></pre>`);
  flushParagraph(); closeList();
  return html.join('\n');
}

function enhanceCodeBlocks(container) {
  for (const pre of container.querySelectorAll('pre')) {
    if (pre.parentElement?.classList.contains('code-block')) continue;
    const block = document.createElement('div');
    block.className = 'code-block';
    pre.replaceWith(block);
    block.appendChild(pre);
    const bar = document.createElement('div');
    bar.className = 'code-scrollbar';
    bar.setAttribute('aria-hidden', 'true');
    bar.appendChild(document.createElement('div'));
    block.appendChild(bar);
    const sync = () => {
      if (pre.scrollWidth - pre.clientWidth <= 1) { bar.hidden = true; return; }
      bar.hidden = false;
      bar.firstElementChild.style.width = `${pre.scrollWidth}px`;
      if (Math.abs(bar.scrollLeft - pre.scrollLeft) > 0.5) bar.scrollLeft = pre.scrollLeft;
    };
    pre.addEventListener('scroll', () => { if (Math.abs(bar.scrollLeft - pre.scrollLeft) > 0.5) bar.scrollLeft = pre.scrollLeft; });
    bar.addEventListener('scroll', () => { if (Math.abs(pre.scrollLeft - bar.scrollLeft) > 0.5) pre.scrollLeft = bar.scrollLeft; });
    pre.syncScrollBar = sync;
    sync();
  }
}

function syncCodeScrollbars() {
  document.querySelectorAll('pre').forEach(pre => pre.syncScrollBar?.());
}

function renderInto(element, markdown, includeHeadingIds = true) {
  element.innerHTML = render(markdown, includeHeadingIds);
  enhanceCodeBlocks(element);
}
function renderToc(markdown) {
  const entries = extractHeadings(markdown);
  if (!entries.length) {
    tocPanel.hidden = true;
    tocLinks.innerHTML = '';
    return;
  }
  const baseLevel = Math.min(...entries.map(entry => entry.level));
  tocLinks.innerHTML = entries.map(entry => `<a href="#${entry.id}" data-target="${entry.id}" style="--toc-indent:${Math.min(entry.level - baseLevel, 4)}">${escapeHtml(entry.title)}</a>`).join('');
  tocPanel.hidden = false;
}

function jumpToHeading(event) {
  const eventNode = event.target;
  const link = eventNode instanceof Element
    ? eventNode.closest('a[data-target]')
    : eventNode?.parentElement?.closest('a[data-target]');
  if (!link || !tocLinks.contains(link)) return;
  event.preventDefault();
  const target = Array.from(document.querySelectorAll(`[id="${link.dataset.target}"]`))
    .find(element => element.getClientRects().length > 0);
  if (!target) return;
  // 选择标题所在的可滚动容器，避免隐藏的编辑预览副本抢先匹配相同 ID。
  let container = target.parentElement;
  while (container && container !== document.body) {
    const style = getComputedStyle(container);
    if (/(auto|scroll|overlay)/.test(style.overflowY) && container.scrollHeight > container.clientHeight) {
      const top = target.getBoundingClientRect().top - container.getBoundingClientRect().top + container.scrollTop - 18;
      container.scrollTo({ top: Math.max(0, top), behavior: 'auto' });
      break;
    }
    container = container.parentElement;
  }
  if (!container || container === document.body) {
    const scrollingElement = document.scrollingElement || document.documentElement;
    const targetTop = target.getBoundingClientRect().top + scrollingElement.scrollTop - 76;
    scrollingElement.scrollTo({ top: Math.max(0, targetTop), behavior: 'auto' });
  }
  try { history.replaceState(null, '', `#${encodeURIComponent(link.dataset.target)}`); } catch { /* 某些受限页面不允许修改地址栏，跳转本身不受影响。 */ }
}

function hideSyntaxSuggestions() {
  syntaxSuggestions.hidden = true;
  syntaxSuggestions.innerHTML = '';
  syntaxMatch = null;
  syntaxResults = [];
  activeSyntaxIndex = 0;
  editor.setAttribute('aria-expanded', 'false');
  editor.removeAttribute('aria-activedescendant');
}

function renderSyntaxSuggestions() {
  syntaxSuggestions.innerHTML = syntaxResults.map((template, index) => `
    <button id="syntax-option-${index}" class="syntax-option${index === activeSyntaxIndex ? ' active' : ''}" type="button" role="option" aria-selected="${index === activeSyntaxIndex}" data-index="${index}">
      <span class="syntax-name">${escapeHtml(template.name)}</span>
      <span class="syntax-code">${escapeHtml(template.code)}</span>
    </button>`).join('');
  syntaxSuggestions.hidden = false;
  editor.setAttribute('aria-expanded', 'true');
  editor.setAttribute('aria-activedescendant', `syntax-option-${activeSyntaxIndex}`);
  syntaxSuggestions.querySelector('.syntax-option.active')?.scrollIntoView({ block: 'nearest' });
}

function updateSyntaxSuggestions() {
  if (!editing || editor.selectionStart !== editor.selectionEnd) {
    hideSyntaxSuggestions();
    return;
  }
  const beforeCaret = editor.value.slice(0, editor.selectionStart);
  const match = beforeCaret.match(/<([a-zA-Z\u4e00-\u9fff-]*)$/);
  if (!match) {
    hideSyntaxSuggestions();
    return;
  }
  const query = match[1].toLowerCase();
  syntaxResults = syntaxTemplates.filter(template =>
    !query || template.name.toLowerCase().includes(query) || template.keywords.some(keyword => keyword.startsWith(query))
  );
  if (!syntaxResults.length) {
    hideSyntaxSuggestions();
    return;
  }
  syntaxMatch = { start: editor.selectionStart - match[0].length, end: editor.selectionStart };
  activeSyntaxIndex = 0;
  renderSyntaxSuggestions();
}

function insertSyntaxTemplate(index) {
  const template = syntaxResults[index];
  if (!template || !syntaxMatch) return;
  const value = editor.value;
  editor.value = `${value.slice(0, syntaxMatch.start)}${template.text}${value.slice(syntaxMatch.end)}`;
  const selectionStart = syntaxMatch.start + template.selectStart;
  editor.setSelectionRange(selectionStart, selectionStart + template.selectLength);
  hideSyntaxSuggestions();
  editor.focus();
  editor.dispatchEvent(new Event('input', { bubbles: true }));
}

function handleSyntaxKeydown(event) {
  if (syntaxSuggestions.hidden || event.isComposing) return;
  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault();
    const direction = event.key === 'ArrowDown' ? 1 : -1;
    activeSyntaxIndex = (activeSyntaxIndex + direction + syntaxResults.length) % syntaxResults.length;
    renderSyntaxSuggestions();
  } else if (event.key === 'Enter' || event.key === 'Tab') {
    event.preventDefault();
    insertSyntaxTemplate(activeSyntaxIndex);
  } else if (event.key === 'Escape') {
    event.preventDefault();
    hideSyntaxSuggestions();
  }
}

async function showDocument(markdownDocument) {
  currentDocument = markdownDocument;
  editing = false;
  editorLayout.hidden = true;
  previewToggle.hidden = true;
  previewVisible = localStorage.getItem('md-preview-visible') !== 'false';
  applyPreviewVisibility();
  saveFile.hidden = true;
  saveFile.title = '保存 Markdown 副本';
  saveFile.setAttribute('aria-label', saveFile.title);
  editToggle.textContent = '\u270e';
  editToggle.title = '编辑 Markdown';
  editToggle.setAttribute('aria-label', editToggle.title);
  fileName.textContent = markdownDocument.name;
  window.document.title = `${markdownDocument.name} - Markdown 阅读器`;
  renderInto(content, markdownDocument.text);
  editor.value = markdownDocument.text;
  renderInto(editorPreview, markdownDocument.text, false);
  renderToc(markdownDocument.text);
  content.hidden = false;
  emptyState.hidden = true;
  await chrome.storage.local.set({ activeDocument: markdownDocument });
}

function setEditMode(enabled) {
  if (!enabled) {
    currentDocument.text = editor.value;
    renderInto(content, currentDocument.text);
    renderInto(editorPreview, currentDocument.text, false);
    renderToc(currentDocument.text);
  }
  editing = enabled;
  editToggle.textContent = enabled ? '\u2716' : '\u270e';
  editToggle.title = enabled ? '退出编辑' : '编辑 Markdown';
  editToggle.setAttribute('aria-label', editToggle.title);
  saveFile.hidden = !enabled;
  previewToggle.hidden = !enabled;
  editorLayout.hidden = !enabled;
  content.hidden = enabled || !currentDocument.text;
  tocPanel.hidden = enabled || !currentDocument.text.match(/^(#{1,6})\s+.+$/m);
  if (!enabled) hideSyntaxSuggestions();
  if (enabled) {
    saveFile.title = documentSaveUrl ? '写回原 Markdown 文件' : '保存 Markdown 副本';
    saveFile.setAttribute('aria-label', saveFile.title);
    renderInto(editorPreview, editor.value, true);
    applyPreviewVisibility();
    editor.focus();
    editorPreview.scrollTop = 0;
  }
}

function applyPreviewVisibility() {
  editorLayout.classList.toggle('preview-hidden', !previewVisible);
  previewPane.hidden = !previewVisible;
  previewToggle.textContent = '\u{1F441}';
  previewToggle.title = previewVisible ? '隐藏预览' : '显示预览';
  previewToggle.setAttribute('aria-label', previewToggle.title);
  requestAnimationFrame(syncCodeScrollbars);
}

function updatePreview() {
  currentDocument.text = editor.value;
  renderInto(editorPreview, editor.value, true);
  renderToc(editor.value);
}

function insertTextAtCaret(text) {
  const start = editor.selectionStart;
  const end = editor.selectionEnd;
  const value = editor.value;
  editor.value = `${value.slice(0, start)}${text}${value.slice(end)}`;
  const caret = start + text.length;
  editor.setSelectionRange(caret, caret);
  editor.dispatchEvent(new Event('input', { bubbles: true }));
}

function readImageAsDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.addEventListener('load', () => resolve(String(reader.result || '')));
    reader.addEventListener('error', () => reject(reader.error || new Error('图片读取失败')));
    reader.readAsDataURL(file);
  });
}

async function insertDroppedImages(files) {
  const images = [...files].filter(file => file.type.startsWith('image/'));
  if (!images.length) return false;
  const maxBytes = 20 * 1024 * 1024;
  const accepted = images.filter(file => file.size <= maxBytes);
  if (!accepted.length) {
    setStatus('图片超过 20 MB，未插入', 3500);
    return true;
  }
  try {
    const markdown = [];
    for (const file of accepted) {
      const dataUrl = await readImageAsDataUrl(file);
      const alt = file.name.replace(/\.[^.]+$/, '') || '图片';
      markdown.push(`![${alt}](${dataUrl})`);
    }
    insertTextAtCaret(markdown.join('\n'));
    setStatus(`已嵌入 ${accepted.length} 张图片`, 2500);
  } catch (error) {
    setStatus(error.message || '图片读取失败', 3500);
  }
  return true;
}

function setStatus(message, duration = 0) {
  status.textContent = message;
  if (duration) setTimeout(() => { if (status.textContent === message) status.textContent = ''; }, duration);
}

function saveCopy() {
  const blob = new Blob([editor.value], { type: 'text/markdown;charset=utf-8' });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = currentDocument.name.toLowerCase().endsWith('.md') ? currentDocument.name : `${currentDocument.name}.md`;
  link.click();
  URL.revokeObjectURL(link.href);
}

async function saveDocument() {
  if (!documentSaveUrl) {
    saveCopy();
    setStatus('已下载副本', 2500);
    return;
  }
  saveFile.disabled = true;
  setStatus('正在保存…');
  try {
    const response = await fetch(documentSaveUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'text/markdown; charset=utf-8' },
      body: editor.value
    });
    if (!response.ok) throw new Error(`保存失败（HTTP ${response.status}）`);
    currentDocument.text = editor.value;
    setStatus('已写回原文件', 2500);
  } catch (error) {
    setStatus(error.message || '写回失败，已下载副本', 3500);
    saveCopy();
  } finally {
    saveFile.disabled = false;
  }
}

async function openFile(file) {
  if (!file) return;
  documentBaseUrl = '';
  documentAssetBaseUrl = '';
  documentSaveUrl = '';
  await showDocument({ name: file.name, text: await file.text(), updatedAt: Date.now() });
}

async function openExternalFile(fileUrl) {
  if (!/^file:\/\//i.test(fileUrl)) throw new Error('仅允许打开本地 file:// 文件。');
  const response = await fetch(fileUrl);
  if (!response.ok) throw new Error(`读取失败（HTTP ${response.status}）。`);
  const name = decodeURIComponent(new URL(fileUrl).pathname.split('/').pop() || 'Markdown 文件');
  documentBaseUrl = fileUrl;
  documentSaveUrl = '';
  await showDocument({ name, text: await response.text(), updatedAt: Date.now() });
}

async function openExternalSource(sourceUrl, assetBaseUrl) {
  if (!/^https?:\/\/127\.0\.0\.1(?::\d+)?\//i.test(sourceUrl)) throw new Error('仅允许读取本机预览服务。');
  const response = await fetch(sourceUrl);
  if (!response.ok) throw new Error(`读取失败（HTTP ${response.status}）。`);
  documentBaseUrl = sourceUrl;
  documentAssetBaseUrl = assetBaseUrl || '';
  const saveUrl = new URL(sourceUrl);
  saveUrl.pathname = '/save';
  documentSaveUrl = saveUrl.href;
  const name = new URLSearchParams(window.location.search).get('name') || 'Markdown 文件';
  await showDocument({ name, text: await response.text(), updatedAt: Date.now() });
}

fileInput.addEventListener('change', () => openFile(fileInput.files[0]));
editToggle.addEventListener('click', () => setEditMode(!editing));
previewToggle.addEventListener('click', () => {
  previewVisible = !previewVisible;
  localStorage.setItem('md-preview-visible', String(previewVisible));
  applyPreviewVisibility();
});
saveFile.addEventListener('click', saveDocument);
editor.addEventListener('input', () => {
  updatePreview();
  updateSyntaxSuggestions();
});
editor.addEventListener('keydown', handleSyntaxKeydown);
editor.addEventListener('click', updateSyntaxSuggestions);
syntaxSuggestions.addEventListener('mousedown', event => {
  const option = event.target instanceof Element ? event.target.closest('.syntax-option') : null;
  if (!option) return;
  event.preventDefault();
  insertSyntaxTemplate(Number(option.dataset.index));
});
document.addEventListener('mousedown', event => {
  if (event.target !== editor && !syntaxSuggestions.contains(event.target)) hideSyntaxSuggestions();
});
tocLinks.addEventListener('click', jumpToHeading);
window.addEventListener('resize', syncCodeScrollbars);
document.addEventListener('keydown', event => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's' && editing) {
    event.preventDefault();
    saveDocument();
  }
});
editor.addEventListener('dragover', event => {
  if (![...(event.dataTransfer?.items || [])].some(item => item.kind === 'file' && item.type.startsWith('image/'))) return;
  event.preventDefault();
  event.stopPropagation();
  editor.classList.add('image-dragging');
});
editor.addEventListener('dragleave', event => {
  if (!editor.contains(event.relatedTarget)) editor.classList.remove('image-dragging');
});
editor.addEventListener('drop', async event => {
  const files = event.dataTransfer?.files || [];
  if (![...files].some(file => file.type.startsWith('image/'))) return;
  event.preventDefault();
  event.stopPropagation();
  editor.classList.remove('image-dragging');
  await insertDroppedImages(files);
});
dropZone.addEventListener('dragover', event => { event.preventDefault(); dropZone.classList.add('dragging'); });
dropZone.addEventListener('dragleave', () => dropZone.classList.remove('dragging'));
dropZone.addEventListener('drop', event => { event.preventDefault(); dropZone.classList.remove('dragging'); openFile(event.dataTransfer.files[0]); });
themeToggle.addEventListener('click', () => { document.body.classList.toggle('dark'); chrome.storage.local.set({ dark: document.body.classList.contains('dark') }); });

(async () => {
  const params = new URLSearchParams(window.location.search);
  const externalSource = params.get('source');
  if (externalSource) {
    try { await openExternalSource(externalSource, params.get('assetBase') || ''); }
    catch (error) {
      emptyState.hidden = false;
      emptyState.querySelector('h1').textContent = '无法打开文件';
      emptyState.querySelector('p').textContent = error.message;
    }
    return;
  }
  const externalFile = params.get('file');
  if (externalFile) {
    try { await openExternalFile(externalFile); }
    catch (error) {
      emptyState.hidden = false;
      emptyState.querySelector('h1').textContent = '无法打开文件';
      emptyState.querySelector('p').textContent = error.message;
    }
    return;
  }
  const { activeDocument, dark } = await chrome.storage.local.get(['activeDocument', 'dark']);
  if (dark) document.body.classList.add('dark');
  if (activeDocument) showDocument(activeDocument);
})();
