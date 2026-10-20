# MdReader

自用的本地 Markdown 阅读器：Chromium 扩展负责渲染和编辑，Windows 启动器负责接管 `.md` 文件关联、起一个只监听 `127.0.0.1` 的临时 HTTP 服务。

这是 [Markdown_brave_reader](https://github.com/Henu312/Markdown_brave_reader) 的重构版：启动器从「自包含 146 MB」改为「框架依赖约 250 KB」，代码按职责拆分成多个小文件。

> **不想安装 .NET？** 旧版把运行时一起打包进 exe，双击即用、不需要任何环境：
> <https://github.com/Henu312/Markdown_brave_reader>

## 目录结构

```
MdReader/
├─ extension/                 浏览器扩展（原样复制，未改动）
├─ src/MdReader.Launcher/     启动器源码
│  ├─ Program.cs              入口：参数 → 单实例 → 起服务 → 开浏览器
│  ├─ App/
│  │  ├─ MarkdownFile.cs      参数解析、目录越界判断、MIME
│  │  ├─ LauncherSettings.cs  browser.txt / extension-id.txt 解析
│  │  ├─ BrowserLauncher.cs   定位 brave/chrome 并打开地址
│  │  ├─ SingleInstance.cs    命名互斥体 + 命名管道
│  │  └─ Ui.cs                唯一的提示出口
│  ├─ Server/
│  │  ├─ DocumentServer.cs    HttpListener 与四个路由
│  │  └─ DocumentStore.cs     doc 令牌 → 真实路径
│  ├─ SelfTest.cs             --selftest 用的无界面自检
│  ├─ viewer.html             没有扩展时的后备阅读页
│  └─ browser.txt / extension-id.txt
├─ 发布.ps1                   一键发布
└─ dist/                      发布产物（脚本生成，已被 .gitignore 忽略）
```

## 为什么只有 250 KB

启动器用**框架依赖 + 单文件**发布：exe 里只有自己的代码，`System.*`、WinForms、WPF 全部用本机已安装的 .NET 运行时。

旧版是自包含单文件，把整个 `Microsoft.NETCore.App` + `Microsoft.WindowsDesktop.App`（含用不到的 47 MB WPF）都打了进去，所以是 146 MB。

代价：本机必须有 **.NET 6 Desktop Runtime 或更高**（`Microsoft.WindowsDesktop.App`）。`RollForward=LatestMajor` 让它能跑在 6/8/9/10 任意一个版本上。

## 构建

```powershell
git clone https://github.com/Henu312/MdReader.git
cd MdReader
pwsh -File .\发布.ps1
```

或者手动：

```powershell
dotnet publish .\src\MdReader.Launcher\MdReader.Launcher.csproj -c Release -o .\dist
```

发布属性（`SelfContained=false`、`PublishSingleFile=true`、`RollForward=LatestMajor`）都写在 csproj 里，不需要每次在命令行上敲。

> 换到别的机器后如果双击提示缺少 .NET，见下面「无法运行？缺的是这个依赖」。

## 配置（改完不用重启）

`dist\browser.txt`：

- `brave` / `chrome`：指定浏览器
- `auto` 或其他值：先找 Brave，再找 Chrome

`dist\extension-id.txt`：

- `brave=<ID>`、`chrome=<ID>` 分行写
- 或只写一行裸 ID
- 没填有效 ID 时走内置后备阅读页

每次打开 `.md` 都会重新读取这两个文件。

`extension-id.txt` 含个人扩展 ID，**不提交到仓库**；仓库里只有 `extension-id.example.txt` 模板。

## 从自带运行时的旧版迁移

1. 打开 `brave://extensions`，移除旧的“本地 Markdown 阅读器”。
2. 点“加载已解压的扩展程序”，选择本项目的 `extension` 目录。
   **扩展 ID 会变** —— 非打包扩展的 ID 是由安装目录路径算出来的，换了目录就换了 ID。
3. 把新 ID 写进 `src\MdReader.Launcher\extension-id.txt`（仓库里只提供 `extension-id.example.txt` 模板），发布时会自动复制到 `dist\`。
4. 右键任意 `.md` → “打开方式” → 选择 `dist\MdReader.exe`，勾选“始终使用”。
5. 旧版 `MdLauncher.exe` 和旧扩展目录都可以留着，随时切回去。

## 与旧版的行为差异

| 项目 | 旧版 | 现在 |
|---|---|---|
| 可执行文件名 | `MdLauncher.exe` | `MdReader.exe` |
| 发布方式 | 自包含单文件，146 MB | 框架依赖单文件，约 250 KB |
| 目标框架 | net6.0-windows | net6.0-windows（装了 SDK 10 可一行改成 net10） |
| 后备阅读页 | 请求正文时丢了 doc 令牌，页面空白 | 已修复，正文和图片都能加载 |
| 单实例 / 管道 / 端口 / 路由 / 保存逻辑 | — | 行为保持一致 |

## 无法运行？缺的是这个依赖

启动器是**框架依赖**发布：exe 里只有自己的代码，.NET 运行时用的是本机安装的版本。如果双击后弹出下面这类提示，就是缺少运行时：

> To run this application, you must install .NET.
> You must install .NET Desktop Runtime 6.0.x

需要安装的东西：

| 项目 | 要求 |
|---|---|
| 组件 | **.NET Desktop Runtime**（`Microsoft.WindowsDesktop.App`） |
| 版本 | 最低 6.0.x；装了 8 / 9 / 10 也可以（csproj 里 `RollForward=LatestMajor` 会自动用最高的） |
| 架构 | **x64** |
| 不需要 | .NET Framework 4.8、ASP.NET Core Runtime、.NET SDK |

下载方式（任选其一）：

- 官方下载页：<https://dotnet.microsoft.com/zh-cn/download/dotnet> —— 找最新的 **Desktop Runtime / 桌面运行时 x64** 安装包
- 指定 6.0：<https://dotnet.microsoft.com/zh-cn/download/dotnet/6.0> —— 下载 “.NET Desktop Runtime 6.0.x” 的 Windows x64 版
- 命令行（Windows 包管理器）：

  ```powershell
  winget search Microsoft.DotNet.DesktopRuntime
  winget install Microsoft.DotNet.DesktopRuntime.6
  ```

装完不用重启，直接再双击 `.md` 就行。

确认是否装好：

```powershell
dotnet --list-runtimes | Select-String 'WindowsDesktop'
```

能列出 `Microsoft.WindowsDesktop.App 6.x`（或 8/9/10）就说明可以运行了。

> 两个常见误区：
> 1. 只装 `Microsoft.NETCore.App`（.NET Runtime）不够 —— WinForms 需要的是 **Desktop** Runtime。
> 2. 只有改代码重新编译时才需要 .NET **SDK**；日常使用只需要上面的运行时。
## 已知限制

- 进程最多运行 2 小时后自动退出，之后已打开的标签页无法再保存或加载相对路径图片。
- 写回原文件固定使用 UTF-8（无 BOM），原本是 GBK/UTF-16 的 Markdown 保存后会被转码。
- 内置后备页只支持标题、段落、列表、引用、代码块和图片，复杂语法请用扩展。
- 相对路径资源接口只允许访问当前 Markdown 所在目录及其子目录。

## 自检

```powershell
.\dist\MdReader.exe --selftest
```

- 第一部分：参数解析、配置解析、路径越界判断，共 11 项。
- 第二部分：在本机起一次真实 HTTP 服务，跑一遍 `/content`、`/asset`、`/save`、`OPTIONS` 和后备页，共 9 项。

全部通过退出码为 0。如果当前环境不允许绑定 `127.0.0.1`（例如受限沙箱），HTTP 部分会显示 `SKIP`，不算失败。

注意：`--selftest` 是 GUI 程序，在 pwsh 里直接 `&` 调用不会等待，建议用：

```powershell
Start-Process .\dist\MdReader.exe -ArgumentList '--selftest' -Wait -NoNewWindow
```



