# GlimpseMac - macOS 版产品需求文档 (PRD)

> **项目名称**: GlimpseMac  
> **版本**: v1.0  
> **更新日期**: 2026-01-01  
> **技术栈**: Swift 5.9+ / SwiftUI / Accessibility API  
> **目标平台**: macOS 13.0 (Ventura) 及以上

---

## 一、产品概述

### 1.1 核心理念

GlimpseMac 是 GlimpseMe 的 macOS 原生版本，实现与 Windows 版本**功能对等**的剪贴板监控与上下文溯源系统。

> "复制操作是一种标注行为。我们不仅要记录**复制了什么**，更要记录**为什么复制**——这段内容来自哪个页面、哪个对话、哪个代码文件。"

### 1.2 与 Windows 版本的对比

| 维度 | Windows 版 | macOS 版 | 对等性 |
|------|-----------|---------|--------|
| 剪贴板监控 | `AddClipboardFormatListener` | `NSPasteboard.changeCount` 轮询 | ✅ 功能对等 |
| 上下文获取 | UI Automation API | Accessibility API (AXUIElement) | ✅ 功能对等 |
| 系统托盘 | `Shell_NotifyIconW` | `NSStatusBar` | ✅ 功能对等 |
| 数据存储 | JSON 文件 (`%APPDATA%`) | JSON 文件 (`~/Library/Application Support/`) | ✅ 功能对等 |
| 浏览器扩展 | Native Messaging (Windows) | Native Messaging (macOS) | ✅ 功能对等 |
| 权限模型 | 无需特殊权限 | ⚠️ 需要 Accessibility 权限 | ⚠️ 差异点 |
| 开发语言 | C++ / Win32 API | Swift / Cocoa | ⚠️ 技术栈不同 |

### 1.3 产品目标

**功能对等性**：
- ✅ 100% 实现 Windows 版本的所有核心功能
- ✅ 支持相同的应用适配器（浏览器、微信、VSCode、Notion）
- ✅ 使用相同的 JSON 数据格式

**平台优势**：
- macOS Accessibility API 提供更统一的 UI 元素访问方式
- 更简洁的权限模型（一次授权，全局生效）
- 更现代的开发体验（Swift/SwiftUI）

---

## 二、技术栈选择与论证

### 2.1 为什么选择 Swift 而非 Rust？

| 技术需求 | Swift | Rust | 选择理由 |
|---------|-------|------|---------|
| **Accessibility API** | ✅ 原生 `ApplicationServices` 框架 | ❌ 需要 FFI 绑定（复杂且文档少） | Swift 无需绑定层 |
| **Cocoa 框架** | ✅ 原生 `NSPasteboard`, `NSWorkspace` | ❌ 需要 `objc` crate（维护成本高） | Swift 一等公民支持 |
| **开发效率** | ✅ Xcode + 编译速度快 | ⚠️ 编译慢 + 调试困难 | Swift 开发速度快 3-5 倍 |
| **错误处理** | ✅ Optional + Result | ✅ Result + Option | 相当 |
| **内存安全** | ✅ ARC（自动引用计数） | ✅ 所有权系统 | 相当 |
| **社区资源** | ✅ 大量 macOS 开发文档 | ⚠️ macOS 相关资源少 | Swift 更适合 macOS |

**结论**: Swift 是 macOS 平台的最佳选择，能快速实现与 Windows 版本功能对等的系统。

### 2.2 为什么不用 Objective-C++？

| 维度 | Swift | Objective-C++ |
|------|-------|---------------|
| 类型安全 | ✅ 强类型 + Optional | ⚠️ 指针易出错 |
| 现代语法 | ✅ 闭包、协议、泛型 | ❌ 语法繁琐 |
| 异步编程 | ✅ async/await | ⚠️ 需要 GCD |
| 社区活跃度 | ✅ 持续更新 | ❌ 逐渐过时 |

### 2.3 技术栈确定

```
编程语言: Swift 5.9+
UI 框架: SwiftUI (菜单栏应用)
系统框架:
  - ApplicationServices (Accessibility API)
  - AppKit (NSPasteboard, NSStatusBar)
  - Foundation (FileManager, JSON 编码)
构建工具: Xcode 15+ / Swift Package Manager
```

---

## 三、系统架构设计

### 3.1 模块划分

```
GlimpseMac/
├── App/
│   ├── GlimpseMacApp.swift           # App 入口
│   ├── AppDelegate.swift             # 应用生命周期
│   └── MenuBarController.swift       # 菜单栏控制
│
├── Core/
│   ├── ClipboardMonitor.swift        # 剪贴板监控
│   ├── AccessibilityManager.swift    # 权限管理
│   ├── ContextTracer.swift           # 上下文溯源核心
│   └── ConfigManager.swift           # 配置管理
│
├── Adapters/
│   ├── AdapterProtocol.swift         # 适配器接口
│   ├── BrowserAdapter.swift          # 浏览器 (Chrome/Safari/Edge)
│   ├── WeChatAdapter.swift           # 微信
│   ├── VSCodeAdapter.swift           # VSCode/Cursor
│   ├── NotionAdapter.swift           # Notion
│   └── FinderAdapter.swift           # Finder (文件管理器)
│
├── Storage/
│   ├── ClipboardEntry.swift          # 数据模型
│   ├── JSONStorage.swift             # JSON 存储
│   └── DataMigration.swift           # 数据迁移 (可选)
│
├── Extensions/
│   └── BrowserExtension/             # 浏览器扩展 (可选)
│       ├── manifest.json
│       ├── content.js
│       └── native_host/
│
└── Utils/
    ├── SourceInfo.swift              # 活动窗口信息
    ├── Logger.swift                  # 日志系统
    └── AXElementExtensions.swift     # Accessibility 扩展
```

### 3.2 核心类关系

```swift
// 适配器协议
protocol ContextAdapter {
    var supportedBundleIDs: [String] { get }
    func canHandle(_ appInfo: SourceInfo) -> Bool
    func extractContext(from element: AXUIElement) -> ContextInfo
}

// 上下文溯源器
class ContextTracer {
    private var adapters: [ContextAdapter] = []
    
    func registerAdapter(_ adapter: ContextAdapter)
    func getContext(for appInfo: SourceInfo) -> ContextInfo?
}

// 剪贴板监控器
class ClipboardMonitor {
    private let pasteboard = NSPasteboard.general
    private var lastChangeCount: Int = 0
    private var timer: Timer?
    
    func startMonitoring()
    func stopMonitoring()
    private func handleClipboardChange()
}

// 存储管理器
class JSONStorage {
    func save(_ entry: ClipboardEntry) throws
    func loadHistory() throws -> [ClipboardEntry]
    func clearHistory() throws
}
```

---

## 四、核心功能实现方案

### 4.1 剪贴板监控

#### 4.1.1 监控机制

macOS 不支持 Windows 的 `AddClipboardFormatListener`，需要使用**轮询方式**：

```swift
class ClipboardMonitor {
    private let pasteboard = NSPasteboard.general
    private var lastChangeCount: Int
    private var timer: Timer?
    
    func startMonitoring() {
        // 初始化
        lastChangeCount = pasteboard.changeCount
        
        // 每 500ms 检查一次 (性能影响可忽略)
        timer = Timer.scheduledTimer(withTimeInterval: 0.5, repeats: true) { [weak self] _ in
            self?.checkForChanges()
        }
    }
    
    private func checkForChanges() {
        let currentChangeCount = pasteboard.changeCount
        
        if currentChangeCount != lastChangeCount {
            lastChangeCount = currentChangeCount
            handleClipboardChange()
        }
    }
}
```

#### 4.1.2 内容获取

```swift
func getClipboardContent() -> ClipboardContent? {
    // 文本
    if let text = pasteboard.string(forType: .string) {
        return .text(text)
    }
    
    // 图片
    if let image = pasteboard.readObjects(forClasses: [NSImage.self])?.first as? NSImage {
        return .image(image)
    }
    
    // 文件
    if let urls = pasteboard.readObjects(forClasses: [NSURL.self]) as? [URL] {
        return .files(urls)
    }
    
    return nil
}
```

### 4.2 来源应用识别

macOS 提供比 Windows 更简洁的 API：

```swift
import AppKit

struct SourceInfo {
    let processName: String      // "Google Chrome"
    let bundleID: String          // "com.google.Chrome"
    let windowTitle: String       // "GlimpseMe - GitHub"
    let pid: pid_t                // 12345
    let executablePath: String    // "/Applications/Google Chrome.app/..."
}

func getActiveApplicationInfo() -> SourceInfo? {
    let workspace = NSWorkspace.shared
    
    guard let app = workspace.frontmostApplication else { return nil }
    
    // 获取窗口标题 (通过 Accessibility API)
    let windowTitle = getActiveWindowTitle(for: app.processIdentifier)
    
    return SourceInfo(
        processName: app.localizedName ?? "Unknown",
        bundleID: app.bundleIdentifier ?? "",
        windowTitle: windowTitle ?? "",
        pid: app.processIdentifier,
        executablePath: app.bundleURL?.path ?? ""
    )
}

func getActiveWindowTitle(for pid: pid_t) -> String? {
    let app = AXUIElementCreateApplication(pid)
    var value: CFTypeRef?
    
    // 获取焦点窗口
    guard AXUIElementCopyAttributeValue(app, kAXFocusedWindowAttribute as CFString, &value) == .success,
          let window = value as! AXUIElement? else {
        return nil
    }
    
    // 获取窗口标题
    guard AXUIElementCopyAttributeValue(window, kAXTitleAttribute as CFString, &value) == .success else {
        return nil
    }
    
    return value as? String
}
```

### 4.3 上下文溯源 - Accessibility API

这是 macOS 版本的**核心差异化实现**。

#### 4.3.1 基础框架

```swift
import ApplicationServices

class ContextTracer {
    private var adapters: [ContextAdapter] = []
    
    init() {
        // 注册所有适配器
        registerAdapter(BrowserAdapter())
        registerAdapter(WeChatAdapter())
        registerAdapter(VSCodeAdapter())
        registerAdapter(NotionAdapter())
        registerAdapter(FinderAdapter())
    }
    
    func getContext(for appInfo: SourceInfo) -> ContextInfo? {
        // 检查权限
        guard AccessibilityManager.shared.isAuthorized else {
            return nil
        }
        
        // 选择适配器
        guard let adapter = adapters.first(where: { $0.canHandle(appInfo) }) else {
            return .basic(appInfo)  // 基础信息
        }
        
        // 获取焦点元素
        let app = AXUIElementCreateApplication(appInfo.pid)
        var focusedElement: CFTypeRef?
        
        guard AXUIElementCopyAttributeValue(app, 
                kAXFocusedUIElementAttribute as CFString, 
                &focusedElement) == .success else {
            return .basic(appInfo)
        }
        
        // 调用适配器
        return adapter.extractContext(from: focusedElement as! AXUIElement)
    }
}
```

#### 4.3.2 UI 元素遍历

```swift
extension AXUIElement {
    // 获取子元素
    func children() -> [AXUIElement]? {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(self, kAXChildrenAttribute as CFString, &value) == .success,
              let children = value as? [AXUIElement] else {
            return nil
        }
        return children
    }
    
    // 获取父元素
    func parent() -> AXUIElement? {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(self, kAXParentAttribute as CFString, &value) == .success else {
            return nil
        }
        return value as! AXUIElement?
    }
    
    // 获取属性值
    func getValue<T>(for attribute: String) -> T? {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(self, attribute as CFString, &value) == .success else {
            return nil
        }
        return value as? T
    }
    
    // 查找特定角色的元素
    func findElement(withRole role: String, maxDepth: Int = 10) -> AXUIElement? {
        if let elementRole: String = getValue(for: kAXRoleAttribute as String),
           elementRole == role {
            return self
        }
        
        guard maxDepth > 0, let children = children() else { return nil }
        
        for child in children {
            if let found = child.findElement(withRole: role, maxDepth: maxDepth - 1) {
                return found
            }
        }
        
        return nil
    }
}
```

---

## 五、应用适配器实现

### 5.1 浏览器适配器 (BrowserAdapter)

#### 5.1.1 支持的浏览器

| 浏览器 | Bundle ID | 难度 | 特殊处理 |
|-------|-----------|------|---------|
| Chrome | `com.google.Chrome` | ⭐⭐ | 地址栏在工具栏中 |
| Safari | `com.apple.Safari` | ⭐⭐⭐ | UI 结构更复杂 |
| Edge | `com.microsoft.edgemac` | ⭐⭐ | 类似 Chrome |
| Arc | `company.thebrowser.Browser` | ⭐⭐ | Chromium 内核 |
| Firefox | `org.mozilla.firefox` | ⭐⭐⭐ | 独立 UI 树 |

#### 5.1.2 URL 获取实现

```swift
class BrowserAdapter: ContextAdapter {
    var supportedBundleIDs: [String] {
        ["com.google.Chrome", "com.apple.Safari", "com.microsoft.edgemac", 
         "company.thebrowser.Browser", "org.mozilla.firefox"]
    }
    
    func extractContext(from element: AXUIElement) -> ContextInfo {
        let url = extractURL(from: element)
        let pageTitle = extractPageTitle(from: element)
        let selectionContext = extractSelectionContext(from: element)
        
        return ContextInfo(
            type: .browser,
            data: [
                "url": url ?? "",
                "page_title": pageTitle ?? "",
                "selection_context": selectionContext ?? ""
            ]
        )
    }
    
    private func extractURL(from element: AXUIElement) -> String? {
        // 获取应用级元素
        var app: CFTypeRef?
        AXUIElementCopyAttributeValue(element, kAXParentApplicationAttribute as CFString, &app)
        
        guard let application = app as! AXUIElement? else { return nil }
        
        // 查找地址栏 (Chrome: AXToolbar -> AXTextField)
        guard let toolbar = application.findElement(withRole: kAXToolbarRole as String),
              let addressBar = toolbar.findElement(withRole: kAXTextFieldRole as String) else {
            return nil
        }
        
        // 获取 URL
        return addressBar.getValue(for: kAXValueAttribute as String)
    }
    
    private func extractPageTitle(from element: AXUIElement) -> String? {
        // 从焦点窗口获取标题
        var window: CFTypeRef?
        AXUIElementCopyAttributeValue(element, kAXWindowAttribute as CFString, &window)
        
        guard let win = window as! AXUIElement? else { return nil }
        return win.getValue(for: kAXTitleAttribute as String)
    }
    
    private func extractSelectionContext(from element: AXUIElement) -> String? {
        // 获取选中的文本
        let selectedText: String? = element.getValue(for: kAXSelectedTextAttribute as String)
        
        // 获取周围文本 (如果需要)
        let fullText: String? = element.getValue(for: kAXValueAttribute as String)
        
        return selectedText
    }
}
```

#### 5.1.3 浏览器扩展增强 (可选)

对于需要完整页面内容的场景，可以开发浏览器扩展：

```javascript
// content.js - 监听复制事件
document.addEventListener('copy', (event) => {
    const selection = window.getSelection();
    const selectedText = selection.toString();
    
    // 获取选中内容的 DOM 上下文
    const range = selection.getRangeAt(0);
    const container = range.commonAncestorContainer;
    
    // 获取页面元数据
    const metadata = {
        url: window.location.href,
        title: document.title,
        selectedText: selectedText,
        surroundingHTML: container.parentElement?.innerHTML.substring(0, 500),
        timestamp: new Date().toISOString()
    };
    
    // 发送到 native host
    chrome.runtime.sendNativeMessage('com.glimpseme.native', metadata);
});
```

### 5.2 微信适配器 (WeChatAdapter)

#### 5.2.1 UI 结构分析

macOS 微信的 Accessibility 树结构：

```
Application (微信)
└── Window (AXWindow)
    ├── Group (左侧对话列表)
    │   └── List (AXList)
    │       └── Group[] (对话项)
    ├── Group (中间消息区域)
    │   ├── StaticText (对话对象名称)
    │   └── ScrollArea
    │       └── List (AXList)  ← 消息列表
    │           └── Group[] (单条消息)
    │               ├── StaticText (发送者)
    │               ├── StaticText (时间)
    │               └── StaticText (内容)
    └── Group (右侧详情面板)
```

#### 5.2.2 实现代码

```swift
class WeChatAdapter: ContextAdapter {
    var supportedBundleIDs: [String] {
        ["com.tencent.xinWeChat"]
    }
    
    func extractContext(from element: AXUIElement) -> ContextInfo {
        let chatTarget = extractChatTarget(from: element)
        let recentMessages = extractRecentMessages(from: element, count: 5)
        
        return ContextInfo(
            type: .wechat,
            data: [
                "chat_target": chatTarget ?? "Unknown",
                "recent_messages": recentMessages
            ]
        )
    }
    
    private func extractChatTarget(from element: AXUIElement) -> String? {
        // 查找主窗口
        var window: CFTypeRef?
        AXUIElementCopyAttributeValue(element, kAXWindowAttribute as CFString, &window)
        
        guard let win = window as! AXUIElement?,
              let groups = win.children() else {
            return nil
        }
        
        // 遍历查找消息区域的标题
        for group in groups {
            if let staticTexts = group.findElements(withRole: kAXStaticTextRole as String, maxDepth: 2),
               !staticTexts.isEmpty {
                // 第一个 StaticText 通常是对话对象名称
                return staticTexts[0].getValue(for: kAXValueAttribute as String)
            }
        }
        
        return nil
    }
    
    private func extractRecentMessages(from element: AXUIElement, count: Int) -> [[String: String]] {
        guard let window: AXUIElement = element.getValue(for: kAXWindowAttribute as String),
              let groups = window.children() else {
            return []
        }
        
        var messages: [[String: String]] = []
        
        // 查找消息列表 (通常在第二个 Group 中)
        for group in groups {
            if let scrollArea = group.findElement(withRole: kAXScrollAreaRole as String),
               let messageList = scrollArea.findElement(withRole: kAXListRole as String),
               let messageGroups = messageList.children() {
                
                // 提取最近 N 条消息
                for messageGroup in messageGroups.suffix(count) {
                    if let texts = messageGroup.findElements(withRole: kAXStaticTextRole as String, maxDepth: 1) {
                        messages.append([
                            "sender": texts.first?.getValue(for: kAXValueAttribute as String) ?? "",
                            "content": texts.last?.getValue(for: kAXValueAttribute as String) ?? ""
                        ])
                    }
                }
            }
        }
        
        return messages
    }
}

extension AXUIElement {
    func findElements(withRole role: String, maxDepth: Int) -> [AXUIElement]? {
        var results: [AXUIElement] = []
        
        func traverse(_ element: AXUIElement, depth: Int) {
            if depth > maxDepth { return }
            
            if let elementRole: String = element.getValue(for: kAXRoleAttribute as String),
               elementRole == role {
                results.append(element)
            }
            
            if let children = element.children() {
                for child in children {
                    traverse(child, depth: depth + 1)
                }
            }
        }
        
        traverse(self, depth: 0)
        return results.isEmpty ? nil : results
    }
}
```

### 5.3 VSCode 适配器 (VSCodeAdapter)

#### 5.3.1 实现要点

```swift
class VSCodeAdapter: ContextAdapter {
    var supportedBundleIDs: [String] {
        ["com.microsoft.VSCode", "com.todesktop.230313mzl4w4u92"]  // Cursor
    }
    
    func extractContext(from element: AXUIElement) -> ContextInfo {
        let filePath = extractFilePath(from: element)
        let lineNumber = extractLineNumber(from: element)
        let selectedCode = extractSelectedCode(from: element)
        
        return ContextInfo(
            type: .vscode,
            data: [
                "file_path": filePath ?? "",
                "line_number": lineNumber ?? 0,
                "selected_code": selectedCode ?? ""
            ]
        )
    }
    
    private func extractFilePath(from element: AXUIElement) -> String? {
        // 从窗口标题解析 (格式: "main.swift — GlimpseMac")
        guard let window: AXUIElement = element.getValue(for: kAXWindowAttribute as String),
              let title: String = window.getValue(for: kAXTitleAttribute as String) else {
            return nil
        }
        
        let parts = title.split(separator: "—").map { $0.trimmingCharacters(in: .whitespaces) }
        return parts.first
    }
    
    private func extractLineNumber(from element: AXUIElement) -> Int? {
        // VSCode 的状态栏包含行号信息
        // 需要遍历查找包含 "Ln" 的 StaticText
        guard let app: AXUIElement = element.getValue(for: kAXParentApplicationAttribute as String),
              let statusBar = app.findElement(withRole: "AXGroup", matching: { group in
                  // 状态栏通常在窗口底部
                  return true
              }) else {
            return nil
        }
        
        // 查找 "Ln 123, Col 45" 格式的文本
        if let staticTexts = statusBar.findElements(withRole: kAXStaticTextRole as String, maxDepth: 2) {
            for text in staticTexts {
                if let value: String = text.getValue(for: kAXValueAttribute as String),
                   value.contains("Ln") {
                    // 解析行号
                    let lineStr = value.split(separator: ",").first?
                        .replacingOccurrences(of: "Ln", with: "")
                        .trimmingCharacters(in: .whitespaces)
                    return Int(lineStr ?? "")
                }
            }
        }
        
        return nil
    }
}
```

### 5.4 Notion 适配器 (NotionAdapter)

```swift
class NotionAdapter: ContextAdapter {
    var supportedBundleIDs: [String] {
        ["notion.id"]
    }
    
    func extractContext(from element: AXUIElement) -> ContextInfo {
        let pagePath = extractPagePath(from: element)
        let blockType = extractBlockType(from: element)
        
        return ContextInfo(
            type: .notion,
            data: [
                "page_path": pagePath ?? "",
                "block_type": blockType ?? "text"
            ]
        )
    }
    
    private func extractPagePath(from element: AXUIElement) -> String? {
        // Notion 的页面路径在窗口标题中
        guard let window: AXUIElement = element.getValue(for: kAXWindowAttribute as String),
              let title: String = window.getValue(for: kAXTitleAttribute as String) else {
            return nil
        }
        
        // 格式: "My Page | Notion"
        return title.split(separator: "|").first?
            .trimmingCharacters(in: .whitespaces)
    }
}
```

### 5.5 Finder 适配器 (FinderAdapter)

```swift
class FinderAdapter: ContextAdapter {
    var supportedBundleIDs: [String] {
        ["com.apple.finder"]
    }
    
    func extractContext(from element: AXUIElement) -> ContextInfo {
        let selectedFiles = extractSelectedFiles(from: element)
        let currentPath = extractCurrentPath(from: element)
        
        return ContextInfo(
            type: .finder,
            data: [
                "selected_files": selectedFiles,
                "current_path": currentPath ?? ""
            ]
        )
    }
    
    private func extractSelectedFiles(from element: AXUIElement) -> [String] {
        // 通过 NSWorkspace 获取选中的文件 (更可靠)
        let selectedURLs = NSWorkspace.shared.selectedURLs(forApplication: "Finder")
        return selectedURLs?.map { $0.path } ?? []
    }
    
    private func extractCurrentPath(from element: AXUIElement) -> String? {
        // 从窗口标题或地址栏获取
        guard let window: AXUIElement = element.getValue(for: kAXWindowAttribute as String),
              let title: String = window.getValue(for: kAXTitleAttribute as String) else {
            return nil
        }
        
        return title
    }
}
```

---

## 六、权限管理

### 6.1 Accessibility 权限

macOS 要求应用获得 Accessibility 权限才能访问其他应用的 UI 元素。

#### 6.1.1 权限检查

```swift
import ApplicationServices

class AccessibilityManager {
    static let shared = AccessibilityManager()
    
    var isAuthorized: Bool {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: false]
        return AXIsProcessTrustedWithOptions(options as CFDictionary)
    }
    
    func requestAuthorization() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true]
        _ = AXIsProcessTrustedWithOptions(options as CFDictionary)
    }
}
```

#### 6.1.2 首次启动流程

```swift
@main
struct GlimpseMacApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var appDelegate
    
    var body: some Scene {
        Settings {
            EmptyView()
        }
    }
}

class AppDelegate: NSObject, NSApplicationDelegate {
    var statusBarController: MenuBarController?
    
    func applicationDidFinishLaunching(_ notification: Notification) {
        // 检查权限
        if !AccessibilityManager.shared.isAuthorized {
            showPermissionAlert()
        } else {
            startMonitoring()
        }
    }
    
    private func showPermissionAlert() {
        let alert = NSAlert()
        alert.messageText = "需要辅助功能权限"
        alert.informativeText = "GlimpseMac 需要访问辅助功能以获取复制内容的上下文信息。\n\n点击"打开系统设置"前往授权。"
        alert.addButton(withTitle: "打开系统设置")
        alert.addButton(withTitle: "稍后")
        
        if alert.runModal() == .alertFirstButtonReturn {
            AccessibilityManager.shared.requestAuthorization()
            
            // 打开系统设置
            NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!)
        }
    }
}
```

### 6.2 Info.plist 配置

```xml
<key>NSAppleEventsUsageDescription</key>
<string>GlimpseMac 需要访问其他应用以获取复制内容的上下文信息</string>

<key>LSUIElement</key>
<true/>  <!-- 不显示在 Dock 中 -->

<key>LSMinimumSystemVersion</key>
<string>13.0</string>
```

---

## 七、数据存储

### 7.1 数据模型

```swift
struct ClipboardEntry: Codable {
    let id: UUID
    let timestamp: Date
    let contentType: ContentType
    let content: String
    let contentPreview: String?
    let source: SourceInfo
    let context: ContextInfo?
    
    enum ContentType: String, Codable {
        case text, image, file
    }
}

struct SourceInfo: Codable {
    let processName: String
    let bundleID: String
    let windowTitle: String
    let pid: Int32
    let executablePath: String
}

struct ContextInfo: Codable {
    let type: ContextType
    let data: [String: AnyCodable]  // 动态数据
    
    enum ContextType: String, Codable {
        case browser, wechat, vscode, notion, finder, basic
    }
}
```

### 7.2 存储实现

```swift
import Foundation

class JSONStorage {
    static let shared = JSONStorage()
    
    private let fileManager = FileManager.default
    private lazy var storageURL: URL = {
        let appSupport = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let appDir = appSupport.appendingPathComponent("GlimpseMac", isDirectory: true)
        
        if !fileManager.fileExists(atPath: appDir.path) {
            try? fileManager.createDirectory(at: appDir, withIntermediateDirectories: true)
        }
        
        return appDir.appendingPathComponent("clipboard_history.json")
    }()
    
    func save(_ entry: ClipboardEntry) throws {
        var history = (try? loadHistory()) ?? []
        history.append(entry)
        
        // 限制历史记录数量 (可配置)
        if history.count > 10000 {
            history = Array(history.suffix(10000))
        }
        
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        
        let data = try encoder.encode(history)
        try data.write(to: storageURL)
    }
    
    func loadHistory() throws -> [ClipboardEntry] {
        guard fileManager.fileExists(atPath: storageURL.path) else {
            return []
        }
        
        let data = try Data(contentsOf: storageURL)
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        
        return try decoder.decode([ClipboardEntry].self, from: data)
    }
    
    func clearHistory() throws {
        try fileManager.removeItem(at: storageURL)
    }
}

// 支持动态 JSON
struct AnyCodable: Codable {
    let value: Any
    
    init(_ value: Any) {
        self.value = value
    }
    
    // 实现 Codable...
}
```

### 7.3 与 Windows 版本数据格式对齐

```json
{
  "timestamp": "2026-01-01T12:00:00.000+08:00",
  "content_type": "text",
  "content": "复制的内容",
  "content_preview": "复制的内容...",
  "source": {
    "process_name": "Google Chrome",
    "bundle_id": "com.google.Chrome",
    "window_title": "GlimpseMe - GitHub",
    "pid": 12345
  },
  "context": {
    "type": "browser",
    "data": {
      "url": "https://github.com/glimpseme/glimpsemac",
      "page_title": "GlimpseMe - GitHub",
      "selection_context": "..."
    }
  }
}
```

---

## 八、系统托盘 (菜单栏)

### 8.1 菜单设计

```
┌─────────────────────────┐
│ GlimpseMac              │
├─────────────────────────┤
│ ⏸  暂停监控              │
│ 📂 打开历史记录          │
│ ⚙️  偏好设置...          │
├─────────────────────────┤
│ 关于 GlimpseMac         │
│ 退出                    │
└─────────────────────────┘
```

### 8.2 实现代码

```swift
import AppKit

class MenuBarController {
    private var statusItem: NSStatusItem?
    private var clipboardMonitor: ClipboardMonitor?
    
    func setup() {
        // 创建状态栏图标
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        
        if let button = statusItem?.button {
            button.image = NSImage(systemSymbolName: "doc.on.clipboard", accessibilityDescription: "GlimpseMac")
        }
        
        // 创建菜单
        let menu = NSMenu()
        
        menu.addItem(NSMenuItem(title: "暂停监控", action: #selector(toggleMonitoring), keyEquivalent: "p"))
        menu.addItem(NSMenuItem(title: "打开历史记录", action: #selector(openHistory), keyEquivalent: "h"))
        menu.addItem(NSMenuItem(title: "偏好设置...", action: #selector(openPreferences), keyEquivalent: ","))
        
        menu.addItem(NSMenuItem.separator())
        
        menu.addItem(NSMenuItem(title: "关于 GlimpseMac", action: #selector(showAbout), keyEquivalent: ""))
        menu.addItem(NSMenuItem(title: "退出", action: #selector(quit), keyEquivalent: "q"))
        
        statusItem?.menu = menu
        
        // 启动监控
        clipboardMonitor = ClipboardMonitor()
        clipboardMonitor?.startMonitoring()
    }
    
    @objc func toggleMonitoring() {
        // 切换监控状态
    }
    
    @objc func openHistory() {
        // 打开历史记录窗口
        NSWorkspace.shared.open(JSONStorage.shared.storageURL)
    }
    
    @objc func openPreferences() {
        // 打开设置窗口
    }
    
    @objc func showAbout() {
        NSApp.orderFrontStandardAboutPanel(nil)
    }
    
    @objc func quit() {
        NSApp.terminate(nil)
    }
}
```

---

## 九、配置系统

### 9.1 配置文件

```json
{
  "version": "1.0",
  "monitoring": {
    "enabled": true,
    "poll_interval_ms": 500
  },
  "adapters": {
    "browser": {
      "enabled": true,
      "timeout_ms": 5000,
      "capture_full_page": false
    },
    "wechat": {
      "enabled": true,
      "context_messages": 5
    },
    "vscode": {
      "enabled": true,
      "capture_file_path": true,
      "capture_line_number": true
    },
    "notion": {
      "enabled": true
    },
    "finder": {
      "enabled": true
    }
  },
  "storage": {
    "max_entries": 10000,
    "auto_cleanup": true
  },
  "output": {
    "include_pid": false,
    "include_executable_path": false,
    "content_preview_length": 200
  }
}
```

### 9.2 配置管理器

```swift
class ConfigManager {
    static let shared = ConfigManager()
    
    private var config: Config
    private let configURL: URL
    
    init() {
        let appSupport = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        configURL = appSupport.appendingPathComponent("GlimpseMac/config.json")
        
        // 加载或创建默认配置
        if let data = try? Data(contentsOf: configURL),
           let loaded = try? JSONDecoder().decode(Config.self, from: data) {
            config = loaded
        } else {
            config = Config.default
            save()
        }
    }
    
    func save() {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        
        if let data = try? encoder.encode(config) {
            try? data.write(to: configURL)
        }
    }
    
    // Getters and Setters...
}

struct Config: Codable {
    var monitoring: MonitoringConfig
    var adapters: AdaptersConfig
    var storage: StorageConfig
    var output: OutputConfig
    
    static let `default` = Config(
        monitoring: MonitoringConfig(enabled: true, pollIntervalMs: 500),
        adapters: AdaptersConfig(/* ... */),
        storage: StorageConfig(maxEntries: 10000, autoCleanup: true),
        output: OutputConfig(includePid: false, contentPreviewLength: 200)
    )
}
```

---

## 十、实施路线图

### 10.1 分阶段开发

| 阶段 | 内容 | 预估时间 | 优先级 |
|------|------|----------|--------|
| **阶段 1** | 项目搭建 + 基础剪贴板监控 | 1 天 | 🔴 P0 |
| **阶段 2** | 来源应用识别 + JSON 存储 | 1 天 | 🔴 P0 |
| **阶段 3** | Accessibility 权限管理 | 0.5 天 | 🔴 P0 |
| **阶段 4** | 上下文溯源框架 (ContextTracer) | 1 天 | 🔴 P0 |
| **阶段 5** | 浏览器适配器 | 2 天 | 🟠 P1 |
| **阶段 6** | 微信适配器 | 2 天 | 🟠 P1 |
| **阶段 7** | VSCode 适配器 | 1.5 天 | 🟠 P1 |
| **阶段 8** | Notion/Finder 适配器 | 1.5 天 | 🟡 P2 |
| **阶段 9** | 配置系统 + 菜单栏 UI | 1 天 | 🟡 P2 |
| **阶段 10** | 测试 + 优化 | 2 天 | 🟡 P2 |
| **阶段 11** | 浏览器扩展 (可选) | 3-5 天 | 🟢 P3 |

**总计**: 12-14 天 (不含浏览器扩展)

### 10.2 里程碑

| 里程碑 | 完成标志 | 预期日期 |
|--------|---------|---------|
| **M1: MVP** | 基础剪贴板监控 + 来源应用 | Day 2 |
| **M2: 核心功能** | 浏览器 + 微信上下文溯源 | Day 7 |
| **M3: 功能对等** | 所有适配器完成 | Day 10 |
| **M4: 生产就绪** | 配置 + 测试完成 | Day 12 |

---

## 十一、关键技术挑战与解决方案

### 11.1 挑战 1: Accessibility API 学习曲线

**问题**: macOS Accessibility API 文档不如 Windows UI Automation 完善

**解决方案**:
1. 使用 **Accessibility Inspector** 工具 (Xcode 自带) 探索 UI 树
2. 参考开源项目 (如 [Hammerspoon](https://github.com/Hammerspoon/hammerspoon))
3. 编写通用的元素遍历和查询工具函数

### 11.2 挑战 2: 不同应用的 UI 结构差异

**问题**: 每个应用的 Accessibility 树结构不同

**解决方案**:
1. 采用**适配器模式**，每个应用独立实现
2. 提供**启发式查找**方法 (基于角色、位置、属性)
3. 支持**降级策略** (无法获取上下文时返回基础信息)

```swift
// 启发式查找示例
func findAddressBar(in toolbar: AXUIElement) -> AXUIElement? {
    // 方法 1: 按角色查找
    if let textField = toolbar.findElement(withRole: kAXTextFieldRole as String) {
        return textField
    }
    
    // 方法 2: 按位置查找 (地址栏通常在中间)
    if let children = toolbar.children() {
        let middle = children[children.count / 2]
        if let role: String = middle.getValue(for: kAXRoleAttribute as String),
           role == kAXTextFieldRole as String {
            return middle
        }
    }
    
    // 方法 3: 降级
    return nil
}
```

### 11.3 挑战 3: 权限管理用户体验

**问题**: 用户可能忘记授权或授权后不重启应用

**解决方案**:
1. **持续监测权限状态**，在菜单栏显示状态
2. **友好的引导流程**，一步步指导用户
3. **自动检测授权变化**，提示用户重启

```swift
class AccessibilityManager {
    private var timer: Timer?
    
    func startMonitoring(onChange: @escaping (Bool) -> Void) {
        timer = Timer.scheduledTimer(withTimeInterval: 2.0, repeats: true) { _ in
            let authorized = self.isAuthorized
            onChange(authorized)
        }
    }
}
```

### 11.4 挑战 4: 性能优化

**问题**: 轮询剪贴板可能消耗资源

**解决方案**:
1. **动态调整轮询间隔** (活跃时 500ms，空闲时 2s)
2. **异步处理上下文获取**，不阻塞主线程
3. **缓存常用元素**，减少 Accessibility API 调用

```swift
class ClipboardMonitor {
    private var pollInterval: TimeInterval = 0.5
    
    func adjustPollInterval(basedOn activity: SystemActivity) {
        switch activity {
        case .active:
            pollInterval = 0.5
        case .idle:
            pollInterval = 2.0
        case .asleep:
            timer?.invalidate()  // 停止轮询
        }
    }
}
```

### 11.5 挑战 5: macOS 版本兼容性

**问题**: 不同 macOS 版本的 API 可能有差异

**解决方案**:
1. **最低支持 macOS 13.0** (Ventura)
2. 使用 `@available` 检查 API 可用性
3. 提供降级实现

```swift
func getWindowList() -> [AXUIElement] {
    if #available(macOS 14.0, *) {
        // 使用新 API
    } else {
        // 使用旧 API
    }
}
```

---

## 十二、测试策略

### 12.1 单元测试

```swift
import XCTest
@testable import GlimpseMac

class ClipboardMonitorTests: XCTestCase {
    func testClipboardChangeDetection() {
        let monitor = ClipboardMonitor()
        let expectation = XCTestExpectation(description: "Clipboard change detected")
        
        monitor.onClipboardChange = { _ in
            expectation.fulfill()
        }
        
        monitor.startMonitoring()
        
        // 模拟复制操作
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString("Test", forType: .string)
        
        wait(for: [expectation], timeout: 2.0)
    }
}
```

### 12.2 集成测试

| 测试场景 | 验证内容 |
|---------|---------|
| Chrome 复制 URL | 正确获取 URL 和页面标题 |
| WeChat 复制消息 | 正确获取对话对象和消息上下文 |
| VSCode 复制代码 | 正确获取文件路径和行号 |
| Notion 复制文本 | 正确获取页面路径 |
| Finder 复制文件 | 正确获取文件路径 |

### 12.3 手动测试清单

```markdown
- [ ] 首次启动权限引导流程
- [ ] 剪贴板监控启停
- [ ] 各应用适配器功能
- [ ] JSON 数据格式正确性
- [ ] 菜单栏交互
- [ ] 配置文件读写
- [ ] 性能测试 (CPU/内存占用)
```

---

## 十三、交付物

### 13.1 代码交付

```
GlimpseMac/
├── GlimpseMac.xcodeproj
├── Sources/
│   ├── App/
│   ├── Core/
│   ├── Adapters/
│   ├── Storage/
│   └── Utils/
├── Tests/
├── Resources/
│   ├── Assets.xcassets
│   └── Info.plist
├── README.md
├── LICENSE (MIT)
└── CHANGELOG.md
```

### 13.2 文档交付

1. **README.md** - 使用说明、安装指南
2. **ARCHITECTURE.md** - 架构设计文档
3. **API_REFERENCE.md** - API 参考
4. **TROUBLESHOOTING.md** - 常见问题

### 13.3 发布物

1. **App Bundle** - `GlimpseMac.app`
2. **DMG 安装包** - `GlimpseMac-v1.0.dmg`
3. **GitHub Release** - 源码 + 二进制

---

## 十四、风险评估

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|---------|
| Accessibility API 无法获取某些应用的 UI | 中 | 中 | 提供降级策略，支持浏览器扩展增强 |
| macOS 更新破坏兼容性 | 低 | 高 | 保持最低版本支持，定期测试新版本 |
| 权限被用户误操作撤销 | 中 | 中 | 持续监测权限状态，及时提醒 |
| 性能问题 (内存泄漏) | 低 | 高 | 使用 Instruments 工具分析，ARC 自动管理内存 |

---

## 十五、后续规划

### 15.1 v1.1 增强

- [ ] iCloud 同步 (跨设备共享历史)
- [ ] Spotlight 搜索集成
- [ ] Shortcuts 集成
- [ ] 更多应用适配器 (Slack, Figma, etc.)

### 15.2 v2.0 愿景

- [ ] AI 辅助分类和标签
- [ ] 可视化历史记录查看器
- [ ] 浏览器插件深度集成
- [ ] iOS/iPadOS 版本

---

## 附录 A: macOS 开发环境搭建

### A.1 安装 Xcode

```bash
# 从 Mac App Store 安装 Xcode
# 或使用命令行工具
xcode-select --install
```

### A.2 创建 Swift 项目

```bash
# 使用 Xcode 创建新项目
# File -> New -> Project -> macOS -> App
# 选择 SwiftUI + Swift
```

### A.3 必要的框架

```swift
// Package.swift (如果使用 SPM)
dependencies: [
    // 暂无外部依赖，全部使用系统框架
]
```

---

## 附录 B: Accessibility API 参考

### B.1 常用角色 (Roles)

| 角色 | 常量 | 用途 |
|------|------|------|
| 窗口 | `kAXWindowRole` | 主窗口 |
| 文本框 | `kAXTextFieldRole` | 输入框、地址栏 |
| 按钮 | `kAXButtonRole` | 按钮 |
| 列表 | `kAXListRole` | 消息列表、文件列表 |
| 静态文本 | `kAXStaticTextRole` | 标签、文本 |
| 工具栏 | `kAXToolbarRole` | 浏览器工具栏 |
| 滚动区域 | `kAXScrollAreaRole` | 可滚动区域 |

### B.2 常用属性 (Attributes)

| 属性 | 常量 | 返回类型 |
|------|------|---------|
| 标题 | `kAXTitleAttribute` | String |
| 值 | `kAXValueAttribute` | String/Any |
| 角色 | `kAXRoleAttribute` | String |
| 子元素 | `kAXChildrenAttribute` | [AXUIElement] |
| 父元素 | `kAXParentAttribute` | AXUIElement |
| 焦点元素 | `kAXFocusedUIElementAttribute` | AXUIElement |
| 选中文本 | `kAXSelectedTextAttribute` | String |

---

## 结语

本 PRD 提供了 macOS 版 GlimpseMe 的完整实现方案，确保与 Windows 版本的**功能对等性**，同时充分利用 macOS 平台的技术优势。

**核心亮点**：
1. ✅ Swift 原生实现，开发效率高
2. ✅ Accessibility API 提供强大的上下文溯源能力
3. ✅ 统一的适配器架构，易于扩展
4. ✅ 与 Windows 版本数据格式一致

**预期工期**: 12-14 天完成核心功能，达到生产就绪状态。
