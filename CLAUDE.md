# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

**GlimpseMe** 是一个上下文感知的剪贴板监控系统，它不仅捕获你复制的内容，还记录复制来源。它将"复制"视为"标注"——自动记录丰富的上下文信息（URL、聊天历史、文件路径、文档结构）和剪贴板内容。

项目包含两个平台实现：
- **Mac/** - macOS 版本，使用 Swift + Accessibility API
- **Wins/** - Windows 版本，使用 C++ + UI Automation

两个实现共享同一目标：创建一个 AI 可以理解的结构化个人数据层。

## 核心架构

### 上下文提取的适配器模式

两个平台都使用**适配器模式**来支持不同应用：

```
ClipboardMonitor（剪贴板监控）
    ↓
ContextManager（异步协调器）
    ↓
[BrowserAdapter | WeChatAdapter | VSCodeAdapter | NotionAdapter]
    ↓
UI Automation APIs（平台特定）
    ↓
JSON 存储
```

**核心原则**：每个适配器独立处理特定应用的上下文提取。添加新应用支持只需实现新的适配器类。

### 异步架构（仅 Windows）

Windows 版本使用 2 线程池（`AsyncExecutor`）异步获取上下文：
- 主线程永不阻塞在 UI Automation 调用上
- 每个适配器有可配置的超时时间（150-300ms）
- 上下文获取失败时仍保存剪贴板内容，标记 `success: false`

macOS 版本当前同步执行上下文提取，但由于 Accessibility API 效率高（典型 <50ms），速度足够快。

## 常用开发命令

### macOS

```bash
cd Mac/ClaudeWeb
./build.sh                     # 编译
./build/GlimpseMac             # 运行
```

**调试**：
```bash
cat ~/Library/Application\ Support/GlimpseMac/clipboard_history.json  # 查看历史
tail -f ~/Library/Application\ Support/GlimpseMac/clipboard_history.json  # 实时监控
```

**环境要求**：macOS 11.0+，Xcode Command Line Tools（`xcode-select --install`）
**首次运行**需授予辅助功能权限：系统设置 → 隐私与安全性 → 辅助功能

### Windows

**ClipboardMonitor (C++ 上下文捕获)**：
```bash
cd Wins/ClipboardMonitor
.\build.bat                    # 或 cmake .. && cmake --build . --config Release
.\bin\ClipboardMonitor.exe     # 运行
```

**FloatingTool (C# .NET 8 标注界面)**：
```bash
cd Wins/FloatingTool
dotnet build -c Release
# 按 Alt+Q 唤起标注浮窗
```

**调试**：
```powershell
Get-Content $env:APPDATA\ClipboardMonitor\clipboard_history.json  # 查看历史
Get-Content $env:APPDATA\ClipboardMonitor\debug.log -Wait -Tail 50  # 实时日志
```

**环境要求**：Windows 10+，MSVC 19.41+，.NET 8 SDK

## 关键技术模式

### 1. 适配器实现

添加新应用支持时：

**macOS (Swift)**：
- 在 `Sources/ContextTracer.swift` 中添加新函数（例如 `getSlackContext()`）
- 模式：获取焦点窗口 → 遍历 UI 层级 → 提取特定元素
- 使用 `AXUIElementCopyAttributeValue()` 获取属性，递归遍历处理复杂 UI

**Windows (C++)**：
- 创建 `context/adapters/xxx_adapter.h/cpp`，继承 `IContextAdapter`
- 实现 `CanHandle(processName, windowTitle)` 用于应用检测
- 实现 `GetContext(sourceInfo)` 用于上下文提取
- 在 `main.cpp` 中注册：`contextManager->RegisterAdapter(std::make_shared<XXXAdapter>(timeout))`
- 更新 `CMakeLists.txt` 和 `build.bat` 添加新源文件
- 在 `storage.cpp` 中添加序列化逻辑

### 2. 上下文数据结构

**macOS**：使用基于字典的 JSON 编码，带平台特定键名：
- `browserURL`, `browserTitle` - 浏览器
- `wechatContact`, `wechatMessages` - 微信
- `vscodePath` - VSCode
- `notionPath` - Notion

**Windows**：使用从 `ContextData` 基类继承的层次结构：
- `BrowserContext` - 添加 `sourceUrl`, `pageTitle`
- `WeChatContext` - 添加 `contactName`, `chatType`, `recentMessages`
- `VSCodeContext` - 添加 `fileName`, `projectName`, `lineNumber`, `language`
- `NotionContext` - 添加 `pagePath`, `breadcrumbs`, `pageType`

### 3. 字符串编码（Windows C++ 特定）

Windows 对所有 Windows API 交互使用**宽字符串**（`std::wstring`，UTF-16），但 JSON 存储使用 **UTF-8**（`std::string`）：

```cpp
// JSON 序列化前务必转换
json << "\"field\": \"" << Utils::WideToUtf8(wideString) << "\"";

// 转义 JSON 特殊字符
json << "\"text\": \"" << Utils::EscapeJson(Utils::WideToUtf8(text)) << "\"";
```

**常见陷阱**：混用 `wstring` 和 `string` 会导致编译错误。向上下文结构添加字段时，确保类型一致，并在 JSON 边界正确转换。

### 4. 超时与错误处理

**设计原则**：绝不因上下文获取失败而丢失剪贴板内容。

Windows 实现双重回调机制：
- 同时启动异步任务 + 超时计时器
- `std::atomic<bool>` 确保只有一个回调执行
- 超时返回 `{success: false, error: "Timeout"}`
- 即使失败也保存记录，便于调试

macOS 当前没有显式超时，但操作完成很快（<50ms）。

## 文件组织

### macOS 结构
```
Mac/ClaudeWeb/
├── Sources/
│   ├── main.swift              # 应用入口、系统托盘 UI
│   ├── ClipboardMonitor.swift  # 基于轮询的剪贴板监控
│   ├── ContextTracer.swift     # 核心上下文提取（Accessibility API）
│   └── Storage.swift           # JSON 持久化
├── build.sh                    # 编译脚本
└── *.md                        # 文档
```

### Windows 结构
```
Wins/
├── ClipboardMonitor/           # C++ 上下文捕获
│   ├── main.cpp, clipboard_monitor.h/cpp, storage.h/cpp, utils.h
│   ├── context/
│   │   ├── context_data.h, context_adapter.h, context_manager.h/cpp
│   │   ├── async_executor.h/cpp
│   │   ├── adapters/           # browser, wechat, vscode, notion
│   │   └── utils/              # ui_automation_helper, html_parser
│   ├── CMakeLists.txt, build.bat
└── FloatingTool/               # C# .NET 8 标注界面
    ├── Program.cs
    └── FloatingTool.csproj
```

## 各应用的上下文提取

### 浏览器上下文
- **macOS**：递归搜索角色为"地址栏"的 `AXTextField` 获取 URL
- **Windows**：解析剪贴板的 `CF_HTML` 格式，提取 `SourceURL:` 元数据
- 支持：Chrome、Edge、Safari、Firefox、Brave、Opera、Vivaldi、Arc 及 20+ 浏览器

### 微信上下文
- **macOS**：找到聊天窗口 → 从标题/UI 元素提取联系人名 → 从滚动区域获取最近消息
- **Windows**：使用 UI Automation 查找 List 控件 → 提取最后 N 条消息 → 启发式聊天类型检测（群聊 vs 私聊）
- 可配置消息数量（默认：5-10 条最近消息）

### VSCode 上下文
- **macOS**：解析窗口标题获取文件路径和项目名
- **Windows**：解析窗口标题（`● filename.ext - Project Name - Visual Studio Code`）→ 从扩展名推断语言（40+ 映射）→ 从状态栏提取光标位置
- 支持：VSCode、Cursor、VSCodium、Code Insiders
- 检测未保存修改（`●` 字符 = U+25CF）

### Notion 上下文
- **macOS**：从窗口标题提取页面路径
- **Windows**：解析窗口标题 → 构造伪 URL（`notion://Workspace/Page`）→ 通过 UI Automation 提取面包屑导航 → 从标题关键词推断页面类型（database/table/page）

## 数据存储

两个平台都使用 **JSON** 格式，带 ISO 8601 时间戳：

```json
{
  "timestamp": "2026-01-01T15:30:45.123+08:00",
  "content": {
    "type": "text",
    "text": "复制的内容",
    "preview": "复制的内容..."
  },
  "source": {
    "processName": "chrome.exe",
    "bundleID": "com.google.Chrome"
  },
  "context": {
    "browserURL": "https://example.com",
    "browserTitle": "示例页面"
  }
}
```

**存储位置**：
- macOS：`~/Library/Application Support/GlimpseMac/clipboard_history.json`
- Windows：`%APPDATA%\ClipboardMonitor\clipboard_history.json`

**调试日志**（仅 Windows）：`%APPDATA%\ClipboardMonitor\debug.log`

## 重要编码规范

### macOS Swift
- 使用 `AXUIElementCopyAttributeValue()` 进行所有 Accessibility API 查询
- 检查 `AXError` 返回码；失败时记录日志但不崩溃
- 使用带深度限制的递归 UI 树遍历（防止无限循环）
- 正确管理 Core Foundation 对象的内存释放

### Windows C++
- **禁用 `std::min/max`** - Windows.h 定义的宏会破坏它们。使用三元运算符：`(a < b ? a : b)`
- **始终转义 Unicode 字面量**：写 `L'\u25CF'` 而不是 `L'●'`（避免 C4819 编码警告）
- 对 COM 对象使用 RAII 模式（`UIAutomationHelper` 析构函数调用 `Release()` 和 `CoUninitialize()`）
- 所有调试输出通过 `DEBUG_LOG()` 宏写入文件，永不输出到控制台
- JSON 序列化是手动的（无外部库）- 记得转义引号、反斜杠、换行符

### 跨平台一致性
- 两个平台对同一应用应生成相似的 JSON 结构
- 上下文字段名应尽可能匹配（使用 `browserURL` 而非 `browser_url` vs `browserUrl`）
- 时间戳格式：带时区的 ISO 8601（`YYYY-MM-DDTHH:MM:SS.sss+TZ`）

## 已知问题与限制

### macOS
- 需要辅助功能权限；首次运行必须手动授予
- 某些应用（尤其是沙盒应用）可能 UI 可访问性有限
- 浏览器 URL 提取需要遍历可能很大的 UI 树（复杂页面可能较慢）

### Windows
- 重复记录 bug（已通过将超时从 150ms 增加到 300ms 修复）
- 微信/Notion UI 结构可能在版本间变化（基于启发式的提取）
- 聊天类型检测是启发式的（非 100% 准确）
- PowerShell 默认将 UTF-8 JSON 显示为乱码（文件内容正确；使用 `notepad` 或设置控制台编码）

## 设计哲学

1. **非侵入式**：后台运行；异步上下文获取；不向用户显示错误
2. **渐进增强**：先发布基础功能（从 CF_HTML 获取 URL），后添加深度功能（通过扩展获取文本选择上下文）
3. **启发式优于完美**：简单规则达到 80% 准确率，胜过复杂 ML 模型达到 100%
4. **数据完整性优先**：记录失败尝试（`success: false`）而不是丢失剪贴板内容
5. **模块化**：适配器之间松耦合；易于添加新应用支持

## 调试技巧

### macOS
- 检查 Console.app 中的 Accessibility API 错误（按"GlimpseMac"筛选）
- 使用 Accessibility Inspector（Xcode → 开发者工具）探索应用 UI 层级
- 在编写提取逻辑前打印 UI 元素的角色/属性以理解结构

### Windows
- 用 PowerShell `Get-Content -Wait` 实时监控 `%APPDATA%\ClipboardMonitor\debug.log`
- 使用 Spy++（Visual Studio 工具）检查窗口类名和 UI Automation 属性
- 检查 JSON 中的 `fetchTimeMs` 字段以识别慢适配器
- 使用 Process Monitor 验证文件 I/O 和注册表访问

### 常见问题
- **未捕获上下文**：检查权限（macOS）或 UI Automation 可用性（Windows）
- **上下文错误**：应用 UI 结构可能已变化；更新适配器搜索逻辑
- **性能慢**：增加特定适配器的超时或优化 UI 树遍历深度
- **编码问题**：确保 Windows 上 UTF-8 → UTF-16 转换；检查 macOS 上的文件编码
