import Cocoa
import ApplicationServices

/// 上下文溯源器 - 使用 macOS Accessibility API 获取应用上下文
class ContextTracer {
    
    /// 根据应用类型获取上下文
    func getContext(for sourceInfo: SourceInfo) -> ClipboardRecord.Context {
        // 检查是否有 Accessibility 权限
        guard AXIsProcessTrusted() else {
            print("⚠️  No Accessibility permission")
            return emptyContext()
        }
        
        let bundleID = sourceInfo.bundleID.lowercased()
        
        // 根据 Bundle ID 选择适配器
        if bundleID.contains("chrome") || bundleID.contains("safari") || bundleID.contains("firefox") {
            return getBrowserContext(pid: sourceInfo.pid, bundleID: bundleID)
        } else if bundleID.contains("wechat") {
            return getWeChatContext(pid: sourceInfo.pid)
        } else if bundleID.contains("code") || bundleID.contains("vscode") {
            return getVSCodeContext(pid: sourceInfo.pid)
        } else if bundleID.contains("notion") {
            return getNotionContext(pid: sourceInfo.pid)
        } else {
            return getGenericContext(pid: sourceInfo.pid)
        }
    }
    
    // MARK: - 浏览器适配器
    
    private func getBrowserContext(pid: Int, bundleID: String) -> ClipboardRecord.Context {
        let appElement = AXUIElementCreateApplication(pid_t(pid))
        
        var url: String?
        var title: String?
        
        // 获取焦点窗口
        var focusedWindow: CFTypeRef?
        AXUIElementCopyAttributeValue(appElement, kAXFocusedWindowAttribute as CFString, &focusedWindow)
        
        if let window = focusedWindow {
            // 查找地址栏（URL）
            url = findAddressBarURL(in: window as! AXUIElement, bundleID: bundleID)
            
            // 获取窗口标题
            var titleValue: CFTypeRef?
            AXUIElementCopyAttributeValue(window as! AXUIElement, kAXTitleAttribute as CFString, &titleValue)
            title = titleValue as? String
        }
        
        return ClipboardRecord.Context(
            browserURL: url,
            browserTitle: title,
            wechatContact: nil,
            wechatMessages: nil,
            vscodePath: nil,
            vscodeLineNumber: nil,
            notionPage: nil,
            rawElements: nil
        )
    }
    
    private func findAddressBarURL(in element: AXUIElement, bundleID: String) -> String? {
        // Chrome: Toolbar -> TextField (role: AXTextField, subrole: AXTextField)
        // Safari: Group -> TextField
        
        var children: CFTypeRef?
        AXUIElementCopyAttributeValue(element, kAXChildrenAttribute as CFString, &children)
        
        guard let childArray = children as? [AXUIElement] else { return nil }
        
        // 递归查找地址栏
        for child in childArray {
            // 检查角色
            var role: CFTypeRef?
            AXUIElementCopyAttributeValue(child, kAXRoleAttribute as CFString, &role)
            
            if let roleStr = role as? String {
                // Chrome/Safari 的地址栏是 AXTextField
                if roleStr == "AXTextField" {
                    // 检查是否在顶部工具栏区域
                    var position: CFTypeRef?
                    AXUIElementCopyAttributeValue(child, kAXPositionAttribute as CFString, &position)
                    
                    if let pos = position as? AXValue {
                        var point = CGPoint.zero
                        AXValueGetValue(pos, .cgPoint, &point)
                        
                        // 地址栏通常在 Y < 100 的位置
                        if point.y < 100 {
                            var value: CFTypeRef?
                            AXUIElementCopyAttributeValue(child, kAXValueAttribute as CFString, &value)
                            
                            if let urlString = value as? String, urlString.hasPrefix("http") {
                                return urlString
                            }
                        }
                    }
                }
                
                // 递归查找（Toolbar 等容器）
                if let url = findAddressBarURL(in: child, bundleID: bundleID) {
                    return url
                }
            }
        }
        
        return nil
    }
    
    // MARK: - 微信适配器
    
    private func getWeChatContext(pid: Int) -> ClipboardRecord.Context {
        let appElement = AXUIElementCreateApplication(pid_t(pid))
        
        var contact: String?
        var messages: [String] = []
        
        // 获取主窗口
        var focusedWindow: CFTypeRef?
        AXUIElementCopyAttributeValue(appElement, kAXFocusedWindowAttribute as CFString, &focusedWindow)
        
        if let window = focusedWindow {
            // 获取窗口标题（包含联系人/群名）
            var titleValue: CFTypeRef?
            AXUIElementCopyAttributeValue(window as! AXUIElement, kAXTitleAttribute as CFString, &titleValue)
            contact = titleValue as? String
            
            // 查找消息列表
            messages = findWeChatMessages(in: window as! AXUIElement)
        }
        
        return ClipboardRecord.Context(
            browserURL: nil,
            browserTitle: nil,
            wechatContact: contact,
            wechatMessages: messages.isEmpty ? nil : messages,
            vscodePath: nil,
            vscodeLineNumber: nil,
            notionPage: nil,
            rawElements: nil
        )
    }
    
    private func findWeChatMessages(in element: AXUIElement) -> [String] {
        var messages: [String] = []
        
        // 查找 ScrollArea（消息区域）
        if let scrollAreas = findElementsByRole(element, role: "AXScrollArea") {
            for scrollArea in scrollAreas {
                // 获取 ScrollArea 的子元素
                var children: CFTypeRef?
                AXUIElementCopyAttributeValue(scrollArea, kAXChildrenAttribute as CFString, &children)
                
                if let childArray = children as? [AXUIElement] {
                    // 提取文本内容
                    for child in childArray.prefix(10) {  // 最多取 10 条消息
                        if let text = getElementText(child) {
                            messages.append(text)
                        }
                    }
                }
            }
        }
        
        return messages
    }
    
    // MARK: - VSCode 适配器
    
    private func getVSCodeContext(pid: Int) -> ClipboardRecord.Context {
        let appElement = AXUIElementCreateApplication(pid_t(pid))
        
        var filePath: String?
        var lineNumber: Int?
        
        // 从窗口标题提取文件路径
        var focusedWindow: CFTypeRef?
        AXUIElementCopyAttributeValue(appElement, kAXFocusedWindowAttribute as CFString, &focusedWindow)
        
        if let window = focusedWindow {
            var titleValue: CFTypeRef?
            AXUIElementCopyAttributeValue(window as! AXUIElement, kAXTitleAttribute as CFString, &titleValue)
            
            if let title = titleValue as? String {
                // VSCode 标题格式: "filename - /path/to/file - Visual Studio Code"
                let parts = title.components(separatedBy: " - ")
                if parts.count >= 2 {
                    filePath = parts[1]
                }
            }
        }
        
        return ClipboardRecord.Context(
            browserURL: nil,
            browserTitle: nil,
            wechatContact: nil,
            wechatMessages: nil,
            vscodePath: filePath,
            vscodeLineNumber: lineNumber,
            notionPage: nil,
            rawElements: nil
        )
    }
    
    // MARK: - Notion 适配器
    
    private func getNotionContext(pid: Int) -> ClipboardRecord.Context {
        let appElement = AXUIElementCreateApplication(pid_t(pid))
        
        var pagePath: String?
        
        // 从窗口标题提取页面路径
        var focusedWindow: CFTypeRef?
        AXUIElementCopyAttributeValue(appElement, kAXFocusedWindowAttribute as CFString, &focusedWindow)
        
        if let window = focusedWindow {
            var titleValue: CFTypeRef?
            AXUIElementCopyAttributeValue(window as! AXUIElement, kAXTitleAttribute as CFString, &titleValue)
            pagePath = titleValue as? String
        }
        
        return ClipboardRecord.Context(
            browserURL: nil,
            browserTitle: nil,
            wechatContact: nil,
            wechatMessages: nil,
            vscodePath: nil,
            vscodeLineNumber: nil,
            notionPage: pagePath,
            rawElements: nil
        )
    }
    
    // MARK: - 通用适配器
    
    private func getGenericContext(pid: Int) -> ClipboardRecord.Context {
        let appElement = AXUIElementCreateApplication(pid_t(pid))
        
        var elements: [String] = []
        
        // 获取焦点元素
        var focusedElement: CFTypeRef?
        AXUIElementCopyAttributeValue(appElement, kAXFocusedUIElementAttribute as CFString, &focusedElement)
        
        if let element = focusedElement {
            // 收集周围元素信息
            elements = collectSurroundingElements(element as! AXUIElement, maxDepth: 2)
        }
        
        return ClipboardRecord.Context(
            browserURL: nil,
            browserTitle: nil,
            wechatContact: nil,
            wechatMessages: nil,
            vscodePath: nil,
            vscodeLineNumber: nil,
            notionPage: nil,
            rawElements: elements.isEmpty ? nil : elements
        )
    }
    
    // MARK: - 辅助函数
    
    private func findElementsByRole(_ element: AXUIElement, role: String) -> [AXUIElement]? {
        var results: [AXUIElement] = []
        
        var currentRole: CFTypeRef?
        AXUIElementCopyAttributeValue(element, kAXRoleAttribute as CFString, &currentRole)
        
        if let roleStr = currentRole as? String, roleStr == role {
            results.append(element)
        }
        
        // 递归查找子元素
        var children: CFTypeRef?
        AXUIElementCopyAttributeValue(element, kAXChildrenAttribute as CFString, &children)
        
        if let childArray = children as? [AXUIElement] {
            for child in childArray {
                if let childResults = findElementsByRole(child, role: role) {
                    results.append(contentsOf: childResults)
                }
            }
        }
        
        return results.isEmpty ? nil : results
    }
    
    private func getElementText(_ element: AXUIElement) -> String? {
        var value: CFTypeRef?
        AXUIElementCopyAttributeValue(element, kAXValueAttribute as CFString, &value)
        return value as? String
    }
    
    private func collectSurroundingElements(_ element: AXUIElement, maxDepth: Int) -> [String] {
        guard maxDepth > 0 else { return [] }
        
        var elements: [String] = []
        
        // 获取当前元素信息
        if let text = getElementText(element) {
            elements.append(text)
        }
        
        // 获取父元素
        var parent: CFTypeRef?
        AXUIElementCopyAttributeValue(element, kAXParentAttribute as CFString, &parent)
        
        if let parentElement = parent {
            elements.append(contentsOf: collectSurroundingElements(parentElement as! AXUIElement, maxDepth: maxDepth - 1))
        }
        
        return elements
    }
    
    private func emptyContext() -> ClipboardRecord.Context {
        return ClipboardRecord.Context(
            browserURL: nil,
            browserTitle: nil,
            wechatContact: nil,
            wechatMessages: nil,
            vscodePath: nil,
            vscodeLineNumber: nil,
            notionPage: nil,
            rawElements: nil
        )
    }
}
