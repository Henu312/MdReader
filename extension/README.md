# 本地 Markdown 阅读器

兼容 Brave 和 Google Chrome（均为 Chromium 浏览器）。

这是一个无需网络权限的 Brave/Chromium 扩展。它通过用户主动选择或拖放文件来读取 Markdown，因此不会受到浏览器将 `file://` 或 `.md` 地址视为下载的影响。

## 安装

1. 在 Brave 打开 `brave://extensions/`。
2. 打开右上角的“开发者模式”。
3. 点击“加载已解压的扩展程序”。
4. 选择本目录：`D:\data\reader\brave-markdown-reader`。
5. 将“本地 Markdown 阅读器”固定到工具栏。

## 使用

点击工具栏上的扩展图标并选择 `.md` 文件；也可在打开的阅读器页面中把 Markdown 文件拖入页面。阅读器会根据 `#` 至 `######` 标题自动生成可点击目录。内容只保存在浏览器本地扩展存储中，用于恢复最近一次阅读的文档。

阅读页工具栏的铅笔按钮可进入编辑模式。通过 `md-launcher` 打开的文件，点击保存按钮或按 `Ctrl+S` 会写回原 Markdown 文件；使用扩展内“打开文件”或拖放打开时，浏览器限制只能保存副本。

编辑区输入 `<` 可显示 Markdown 语法建议；继续输入 `<image`、`<table`、`<code` 等关键词可筛选模板。使用方向键选择后按 `Enter` 或 `Tab` 插入，也可以直接点击。

编辑模式下可直接将图片拖入编辑区，图片会转换为 Base64 Data URL 并插入 Markdown 图片语法；单张图片限制 20 MB。

## 与默认打开启动器配合

若要双击 `.md` 后由本扩展渲染：

1. 重新加载本扩展，确保“允许访问文件网址”已开启。
2. 在 `brave://extensions/` 复制本扩展的 32 位 ID。
3. 将该 ID 写入当前发布目录的 `md-launcher\extension-id.txt`。
4. 将 `.md` 默认打开方式设置为当前发布目录的 `md-launcher\MdLauncher.exe`。
