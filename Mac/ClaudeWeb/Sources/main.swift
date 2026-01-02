import Cocoa
import ApplicationServices

// MARK: - 主程序入口

class AppDelegate: NSObject, NSApplicationDelegate {
    var statusItem: NSStatusItem?
    var clipboardMonitor: ClipboardMonitor?
    var contextTracer: ContextTracer?
    
    func applicationDidFinishLaunching(_ notification: Notification) {
        // 1. 检查 Accessibility 权限
        checkAccessibilityPermission()
        
        // 2. 创建系统托盘图标
        setupStatusBar()
        
        // 3. 启动剪贴板监控
        contextTracer = ContextTracer()
        clipboardMonitor = ClipboardMonitor(contextTracer: contextTracer!)
        clipboardMonitor?.start()
        
        print("GlimpseMe for macOS started")
        print("Data will be saved to: \(Storage.shared.dataPath)")
    }
    
    private func checkAccessibilityPermission() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true]
        let trusted = AXIsProcessTrustedWithOptions(options as CFDictionary)
        
        if !trusted {
            let alert = NSAlert()
            alert.messageText = "需要辅助功能权限"
            alert.informativeText = "GlimpseMe 需要辅助功能权限来获取复制内容的上下文信息（如浏览器 URL、微信聊天对象等）。\n\n请在系统设置中授权。"
            alert.alertStyle = .warning
            alert.addButton(withTitle: "打开系统设置")
            alert.addButton(withTitle: "稍后")
            
            if alert.runModal() == .alertFirstButtonReturn {
                NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!)
            }
        }
    }
    
    private func setupStatusBar() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        
        if let button = statusItem?.button {
            button.title = "📋"
            button.toolTip = "GlimpseMe - 运行中"
        }
        
        let menu = NSMenu()
        
        menu.addItem(NSMenuItem(title: "已捕获 \(Storage.shared.getCount()) 条记录", action: nil, keyEquivalent: ""))
        menu.addItem(NSMenuItem.separator())
        menu.addItem(NSMenuItem(title: "打开数据文件夹", action: #selector(openDataFolder), keyEquivalent: ""))
        menu.addItem(NSMenuItem(title: "查看最近记录", action: #selector(showRecentRecords), keyEquivalent: ""))
        menu.addItem(NSMenuItem.separator())
        menu.addItem(NSMenuItem(title: "暂停监控", action: #selector(toggleMonitoring), keyEquivalent: "p"))
        menu.addItem(NSMenuItem.separator())
        menu.addItem(NSMenuItem(title: "退出", action: #selector(quit), keyEquivalent: "q"))
        
        statusItem?.menu = menu
    }
    
    @objc func openDataFolder() {
        NSWorkspace.shared.selectFile(nil, inFileViewerRootedAtPath: Storage.shared.dataDirectory)
    }
    
    @objc func showRecentRecords() {
        if let records = Storage.shared.getRecentRecords(count: 5) {
            let alert = NSAlert()
            alert.messageText = "最近 5 条记录"
            alert.informativeText = records
            alert.runModal()
        }
    }
    
    @objc func toggleMonitoring() {
        clipboardMonitor?.toggle()
        statusItem?.button?.title = clipboardMonitor?.isRunning == true ? "📋" : "⏸"
    }
    
    @objc func quit() {
        NSApplication.shared.terminate(nil)
    }
}

// 启动应用
let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.run()
