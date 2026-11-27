# Capy

[![Release](https://img.shields.io/github/v/release/kxianghui/capy-toolbox)](https://github.com/kxianghui/capy-toolbox/releases/latest)

一个离线可用的 Windows 桌面工具箱,单个 `.exe` 提供三个互不依赖的工具:

- **PDF 转 Word** —— 将 PDF 转换为 DOCX,输出到源文件同目录。
- **图片压缩** —— 对 JPG / PNG / BMP 按画质或最大宽度压缩,支持批量。
- **图片格式转换** —— JPG / PNG / BMP 互转,支持批量。

三个工具共享同一套交互:拖入或选择文件 → 设置少量选项 → 批量处理 → 每行独立状态 → 完成后打开结果或所在文件夹。

## 特性

- **完全离线**:不联网、不需要账号,不上传任何文件。
- **零依赖**:GUI 只需系统自带的 .NET Framework 4.x;PDF 引擎已打包进 exe,无需安装 Python 或 Office。
- **单文件发布**:GUI 与 PDF 引擎合并在一个 `.exe` 中,拷贝即用。
- **自绘界面**:自绘标题栏与控件,统一的主题样式。
- **托盘驻留**:最小化或关闭时可驻留系统托盘。

## 运行前提

- Windows(建议 Win10 / Win11)。
- .NET Framework 4.x(Windows 系统自带,无需额外安装)。

## 使用

直接双击 `Capy.exe` 启动图形界面。

### 命令行

带文件参数启动时,程序不进入 GUI,而是附着到父控制台做批量 PDF 转换:

```
Capy.exe <文件1.pdf> [文件2.pdf ...]
```

- 每行输出 `OK\t输入绝对路径\t输出路径` 或 `FAIL\t输入\t错误信息`。
- 结束时输出 `DONE ok=<成功数> fail=<失败数>`。
- 任一失败则退出码为 `1`,全部成功为 `0`。
- 相同内容同时写入临时目录下的 `pdf2word_cli.log`。

> 命令行仅覆盖 PDF 转换;图片压缩与格式转换只在 GUI 中提供。

## 从源码构建

### 依赖

- PowerShell
- Python 3(构建时用于打包 PDF 引擎)
- .NET Framework 4.0 自带的 `csc.exe`(位于 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\`)

### 完整构建(含 Python 引擎)

```powershell
.\build.ps1 -Ver 1.0.0
```

构建流程:

1. 创建虚拟环境并安装 `src/requirements-engine.txt` 中的引擎依赖。
2. 用 PyInstaller 打包 `pdf2docx_engine`。
3. 用 `csc` 编译单文件 GUI 并将引擎作为内嵌资源打入。
4. 注入文件版本信息。

产物输出到 `dist\Capy-v<版本>.exe`。

### 快速构建(复用已有引擎)

引擎已构建过时,可只重新编译 GUI:

```powershell
.\build-dev.ps1 -Ver 1.0.0
```

### 构建参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `-Type` | 版本类型,如 `release` / `alpha` / `beta`;非 `release` 会在文件名追加后缀 | `release` |
| `-Ver`  | 版本号 | `0.0.1` |
| `-Num`  | 版本序号,与 `-Type` 组合形成后缀 | 空 |

## 项目结构

```
.
├── src/                        C# 源码与 Python 引擎
│   ├── Program.cs              入口(GUI / 命令行分流、单实例)
│   ├── MainForm.cs             主窗口、标签切换、托盘
│   ├── ToolPage.cs             工具页基类(文件列表、拖放、进度、快捷键)
│   ├── PdfToWordPage.cs        PDF 转 Word 页
│   ├── ImageCompressPage.cs    图片压缩页
│   ├── ImageConvertPage.cs     图片格式转换页
│   ├── Engine.cs               PDF 引擎封装与命令行执行
│   ├── ImageOps.cs             图像处理
│   ├── ImageWriters.cs         图像编码写出
│   ├── UiControls.cs           自绘控件、主题、菜单渲染
│   ├── AppSettings.cs          设置持久化
│   ├── pdf2docx_engine.py      Python PDF 转换引擎
│   ├── pdf2docx_engine.spec    PyInstaller 打包配置
│   └── requirements-engine.txt 引擎依赖(版本固定)
├── icon/                       应用图标
├── build.ps1                   完整构建(含 Python 引擎)
├── build-dev.ps1               快速构建(复用已有引擎)
├── embed_version.py            注入文件版本信息
├── design-tokens.json          设计 token(机器可读)
├── DESIGN_TOKENS.md            设计 token 规范
└── IMPLEMENTATION.md           技术实现与设计说明
```

## 技术栈

| 层 | 选型 |
|----|------|
| GUI | C# + WinForms,.NET Framework 4.x |
| 绘制 | GDI+ 自绘控件 |
| PDF 引擎 | Python 3 + pdf2docx 0.5.8,经 PyInstaller 打包为单文件 |
| 图像处理 | .NET `System.Drawing` |

## 许可

本项目基于 [MIT License](LICENSE) 开源。
