import Foundation

class Storage {
    static let shared = Storage()
    
    let dataDirectory: String
    let dataPath: String
    
    private init() {
        // 数据存储在 ~/Library/Application Support/GlimpseMac/
        let appSupport = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        dataDirectory = appSupport.appendingPathComponent("GlimpseMac").path
        dataPath = dataDirectory + "/clipboard_history.json"
        
        // 创建目录
        try? FileManager.default.createDirectory(atPath: dataDirectory, withIntermediateDirectories: true)
        
        // 如果文件不存在，创建空数组
        if !FileManager.default.fileExists(atPath: dataPath) {
            try? "[]".write(toFile: dataPath, atomically: true, encoding: .utf8)
        }
    }
    
    /// 保存一条新记录
    func save(record: ClipboardRecord) {
        var records = loadRecords()
        records.append(record)
        
        // 只保留最近 1000 条
        if records.count > 1000 {
            records = Array(records.suffix(1000))
        }
        
        saveRecords(records)
    }
    
    /// 获取记录数量
    func getCount() -> Int {
        return loadRecords().count
    }
    
    /// 获取最近的 N 条记录（用于显示）
    func getRecentRecords(count: Int) -> String? {
        let records = loadRecords().suffix(count)
        
        var summary = ""
        for (index, record) in records.enumerated() {
            summary += "\n[\(index + 1)] \(record.timestamp)\n"
            summary += "来源: \(record.source.processName)\n"
            summary += "内容: \(record.content.preview)\n"
            
            // 显示上下文
            if let url = record.context.browserURL {
                summary += "URL: \(url)\n"
            }
            if let contact = record.context.wechatContact {
                summary += "微信联系人: \(contact)\n"
            }
            if let path = record.context.vscodePath {
                summary += "文件: \(path)\n"
            }
            summary += "\n"
        }
        
        return summary
    }
    
    // MARK: - Private
    
    private func loadRecords() -> [ClipboardRecord] {
        guard let data = try? Data(contentsOf: URL(fileURLWithPath: dataPath)) else {
            return []
        }
        
        let decoder = JSONDecoder()
        return (try? decoder.decode([ClipboardRecord].self, from: data)) ?? []
    }
    
    private func saveRecords(_ records: [ClipboardRecord]) {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        
        guard let data = try? encoder.encode(records) else {
            print("❌ Failed to encode records")
            return
        }
        
        do {
            try data.write(to: URL(fileURLWithPath: dataPath), options: .atomic)
            print("✅ Saved to \(dataPath)")
        } catch {
            print("❌ Failed to save: \(error)")
        }
    }
}
