# 快速开始

## 1️⃣ 下载项目

你已经有了所有源代码文件：

```
GlimpseMac/
├── Sources/
│   ├── main.swift              # 主程序
│   ├── ClipboardMonitor.swift  # 剪贴板监控
│   ├── ContextTracer.swift     # 上下文溯源（核心）
│   └── Storage.swift           # 数据存储
├── build.sh                    # 编译脚本
├── README.md                   # 完整文档
├── COMPARISON.md               # 与 Rust 版本对比
└── QUICKSTART.md               # 本文件
```

## 2️⃣ 编译

```bash
cd GlimpseMac
./build.sh
```

**输出**:
```
🔨 Building GlimpseMe for macOS...
✅ Build successful!
📦 Executable: build/GlimpseMac
```

## 3️⃣ 运行

```bash
./build/GlimpseMac
```

**首次运行会看到**:

![权限请求](permission.png)

点击「打开系统设置」→ 在「辅助功能」中勾选 GlimpseMac

## 4️⃣ 测试

### 测试 1: 浏览器 URL

1. 打开 Chrome/Safari
2. 在地址栏随便打开一个网页
3. 复制网页上的任意文字
4. 查看数据文件

```bash
cat ~/Library/Application\ Support/GlimpseMac/clipboard_history.json
```

**你会看到**:
```json
{
  "context": {
    "browserURL": "https://...",  // ← URL 成功获取！
    "browserTitle": "..."
  }
}
```

### 测试 2: 微信聊天

1. 打开微信
2. 进入任意聊天窗口
3. 复制一条消息
4. 查看数据文件

**你会看到**:
```json
{
  "context": {
    "wechatContact": "朋友 A",     // ← 联系人
    "wechatMessages": [            // ← 上下文消息
      "消息1",
      "消息2"
    ]
  }
}
```

### 测试 3: VSCode

1. 打开 VSCode
2. 打开任意文件
3. 复制代码
4. 查看数据文件

**你会看到**:
```json
{
  "context": {
    "vscodePath": "/path/to/file.swift"  // ← 文件路径
  }
}
```

## 5️⃣ 查看数据

### 方法 1: 命令行

```bash
# 查看最后一条记录
tail -n 50 ~/Library/Application\ Support/GlimpseMac/clipboard_history.json

# 实时监控（新记录会自动显示）
tail -f ~/Library/Application\ Support/GlimpseMac/clipboard_history.json
```

### 方法 2: 系统托盘菜单

点击菜单栏的 📋 图标 → 「查看最近记录」

### 方法 3: 直接打开文件夹

点击菜单栏的 📋 图标 → 「打开数据文件夹」

## 6️⃣ 对比验证

如果你之前装了 Rust 版本，现在可以对比一下：

| 功能 | Rust 版本 | Swift 版本（本项目）|
|------|-----------|-------------------|
| 浏览器 URL | ❌ | ✅ |
| 微信上下文 | ❌ | ✅ |
| VSCode 路径 | ❌ | ✅ |

## 🎯 下一步

- 阅读 `README.md` 了解完整功能
- 阅读 `COMPARISON.md` 了解技术细节
- 查看 `Sources/ContextTracer.swift` 学习 Accessibility API

## 🐛 常见问题

### Q: 编译失败？

```bash
# 检查 Xcode CLI Tools
xcode-select --install
```

### Q: 权限请求不弹出？

```bash
# 手动打开系统设置
open "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility"
```

### Q: 获取不到浏览器 URL？

检查：
1. 是否授权了辅助功能权限
2. 是否在浏览器中（不是隐私模式）
3. 查看控制台日志：`Console.app` → 搜索 "GlimpseMac"

### Q: 如何停止程序？

点击菜单栏 📋 → 「退出」

或按 `Ctrl+C`（如果在终端运行）

---

**5 分钟就能体验到完整的上下文溯源功能！**
