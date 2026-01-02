# GlimpseMe 配置系统设计

> **目标**：实现可配置的适配器系统和用户偏好设置

## config.json 结构

```json
{
  "version": "1.0",
  "general": {
    "enabled": true,
    "save_location": "%APPDATA%\\ClipboardMonitor",
    "max_entries": 1000,
    "log_level": "info"
  },
  "adapters": {
    "browser": {
      "enabled": true,
      "timeout_ms": 5000,
      "capture_url": true,
      "capture_title": true
    },
    "wechat": {
      "enabled": true,
      "timeout_ms": 5000,
      "recent_messages_count": 5
    },
    "vscode": {
      "enabled": true,
      "timeout_ms": 5000,
      "capture_line_number": true,
      "capture_language": true
    },
    "notion": {
      "enabled": true,
      "timeout_ms": 5000,
      "capture_breadcrumbs": true
    }
  },
  "output": {
    "include_pid": false,
    "include_process_path": false,
    "content_preview_max_length": 200
  },
  "browser_extension": {
    "enabled": true,
    "native_messaging": true
  }
}
```

## 配置文件位置

- **Windows**: `%APPDATA%\ClipboardMonitor\config.json`
- **首次运行**: 自动创建默认配置

## 实现方案

### config.h
```cpp
struct AdapterConfig {
    bool enabled = true;
    int timeout_ms = 5000;
    std::map<std::string, std::string> options;
};

struct Config {
    bool enabled = true;
    int max_entries = 1000;
    std::map<std::string, AdapterConfig> adapters;
    
    // Output options
    bool include_pid = false;
    bool include_process_path = false;
    int content_preview_max_length = 200;
    
    static Config Load(const std::wstring& path);
    void Save(const std::wstring& path) const;
};
```

### 集成点

1. **main.cpp**: 启动时加载配置
2. **ContextManager**: 根据配置启用/禁用适配器
3. **storage.cpp**: 根据配置决定输出字段

## 实现优先级

1. ✅ 设计完成（本文档）
2. ⏳ config.h/cpp 实现（需 Windows 编译）
3. ⏳ 集成到 main.cpp
4. ⏳ 修改 storage.cpp 遵循配置
