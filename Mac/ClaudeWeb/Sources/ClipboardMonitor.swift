import Cocoa

class ClipboardMonitor {
    private let pasteboard = NSPasteboard.general
    private var lastChangeCount: Int = 0
    private var timer: Timer?
    private let contextTracer: ContextTracer
    var isRunning: Bool = false
    
    init(contextTracer: ContextTracer) {
        self.contextTracer = contextTracer
        self.lastChangeCount = pasteboard.changeCount
    }
    
    func start() {
        isRunning = true
        // 每 0.5 秒检查一次剪贴板变化
        timer = Timer.scheduledTimer(withTimeInterval: 0.5, repeats: true) { [weak self] _ in
            self?.checkClipboard()
        }
        print("Clipboard monitoring started")
    }
    
    func stop() {
        isRunning = false
        timer?.invalidate()
        timer = nil
        print("Clipboard monitoring stopped")
    }
    
    func toggle() {
        if isRunning {
            stop()
        } else {
            start()
        }
    }
    
    private func checkClipboard() {
        let currentChangeCount = pasteboard.changeCount
        
        // 检查是否有变化
        guard currentChangeCount != lastChangeCount else { return }
        lastChangeCount = currentChangeCount
        
        // 获取剪贴板内容
        guard let content = getClipboardContent() else { return }
        
        // 获取来源应用信息
        let sourceInfo = getActiveApplicationInfo()
        
        // 获取上下文（核心功能）
        let context = contextTracer.getContext(for: sourceInfo)
        
        // 创建记录
        let record = ClipboardRecord(
            content: content,
            source: sourceInfo,
            context: context
        )
        
        // 保存到 JSON
        Storage.shared.save(record: record)
        
        print("📋 Captured from \(sourceInfo.processName): \(content.preview)")
    }
    
    private func getClipboardContent() -> ClipboardContent? {
        // 优先获取文本
        if let string = pasteboard.string(forType: .string) {
            return ClipboardContent(
                type: "text",
                text: string,
                preview: String(string.prefix(100))
            )
        }
        
        // 获取文件路径
        if let urls = pasteboard.readObjects(forClasses: [NSURL.self]) as? [URL] {
            let paths = urls.map { $0.path }.joined(separator: ", ")
            return ClipboardContent(
                type: "file",
                text: paths,
                preview: paths
            )
        }
        
        // 获取图片
        if let image = pasteboard.data(forType: .tiff) {
            return ClipboardContent(
                type: "image",
                text: "[Image \(image.count) bytes]",
                preview: "[Image]"
            )
        }
        
        return nil
    }
    
    private func getActiveApplicationInfo() -> SourceInfo {
        guard let app = NSWorkspace.shared.frontmostApplication else {
            return SourceInfo.unknown()
        }
        
        return SourceInfo(
            processName: app.localizedName ?? "Unknown",
            bundleID: app.bundleIdentifier ?? "",
            pid: Int(app.processIdentifier),
            executablePath: app.executableURL?.path ?? ""
        )
    }
}

// MARK: - 数据模型

struct ClipboardContent {
    let type: String
    let text: String
    let preview: String
}

struct SourceInfo {
    let processName: String
    let bundleID: String
    let pid: Int
    let executablePath: String
    
    static func unknown() -> SourceInfo {
        return SourceInfo(
            processName: "Unknown",
            bundleID: "",
            pid: 0,
            executablePath: ""
        )
    }
}

struct ClipboardRecord: Codable {
    let timestamp: String
    let content: Content
    let source: Source
    let context: Context
    
    struct Content: Codable {
        let type: String
        let text: String
        let preview: String
    }
    
    struct Source: Codable {
        let processName: String
        let bundleID: String
    }
    
    struct Context: Codable {
        let browserURL: String?
        let browserTitle: String?
        let wechatContact: String?
        let wechatMessages: [String]?
        let vscodePath: String?
        let vscodeLineNumber: Int?
        let notionPage: String?
        let rawElements: [String]?
    }
    
    init(content: ClipboardContent, source: SourceInfo, context: Context) {
        // ISO 8601 格式时间戳
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        self.timestamp = formatter.string(from: Date())
        
        self.content = Content(
            type: content.type,
            text: content.text,
            preview: content.preview
        )
        
        self.source = Source(
            processName: source.processName,
            bundleID: source.bundleID
        )
        
        self.context = context
    }
}
