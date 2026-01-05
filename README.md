# GlimpseMe

> **选取、标注、发表你的看法——让 AI 真正懂你。**

---

## 🎯 核心理念

**复制不只是复制，它是一种标注。**

在任何应用里选中任何内容，按下快捷键，一个浮窗会立刻出现在你眼前。你可以对这段内容：

- 👍 **点赞** — 表示喜欢、同意、值得收藏
- 👎 **点踩** — 表示不认同、有问题
- 💬 **写评论** — 添加你的想法、见解、联想

系统自动捕获完整上下文：这段话来自哪个网页、哪个聊天对象、哪个代码文件。你今天标注的一句话，明天的 AI 会记得，会和你三个月前标注的另一句话联系起来，形成对你这个人的理解。

**这份数据是给 AI 读的。**

---

## ✨ 你能做什么

| 场景 | GlimpseMe 做的事 |
|------|------------------|
| 在网页上看到一段精彩观点 | 选中 → 点赞或写评论 → 自动记录 URL 和页面标题 |
| 微信群里有人说了句触动你的话 | 选中 → 标注你的感受 → 自动记录群名和聊天上下文 |
| 代码里发现一个巧妙的写法 | 选中 → 加个备注 → 自动记录文件路径和项目名 |
| Notion 文档里读到重要信息 | 选中 → 点个赞 → 自动记录页面路径 |

---

## 🔧 工作原理

```
你选中内容
     ↓
按下快捷键（如 Alt+Q）
     ↓
┌─────────────────────────────┐
│   浮窗出现在光标位置         │
│   ┌─────┬─────┬─────┐       │
│   │ 👍  │ 👎  │ 😐  │       │
│   └─────┴─────┴─────┘       │
│   ┌─────────────────────┐   │
│   │ 写下你的想法...      │   │
│   └─────────────────────┘   │
└─────────────────────────────┘
     ↓
自动获取来源上下文（URL/群名/文件路径...）
     ↓
保存为结构化数据 → AI 可读取和理解
```

---

## 🚀 快速开始

### Windows

**环境要求**：Windows 10+，Visual Studio 2022 Build Tools

```powershell
# 编译 ClipboardMonitor（上下文捕获）
cd Wins/ClipboardMonitor
.\build.bat

# 编译 FloatingTool（标注界面）
cd Wins/FloatingTool
dotnet build -c Release

# 运行
.\bin\ClipboardMonitor.exe
# 然后按 Alt+Q 唤起标注浮窗
```

### macOS

**环境要求**：macOS 11.0+，Xcode Command Line Tools

```bash
# 安装开发工具（如未安装）
xcode-select --install

# 编译
cd Mac/ClaudeWeb
chmod +x build.sh
./build.sh

# 运行
./build/GlimpseMac
```

> ⚠️ **macOS 首次运行**需要授予辅助功能权限：系统设置 → 隐私与安全性 → 辅助功能

---

## 📍 智能上下文捕获

| 应用 | 自动获取的上下文 |
|------|------------------|
| 🌐 **浏览器** | 完整 URL + 页面标题 |
| 💬 **微信** | 联系人/群名 + 最近 10 条消息 |
| 💻 **VSCode** | 文件路径 + 项目名 + 行号 |
| 📝 **Notion** | 页面路径 + 文档结构 |
| 📁 **其他应用** | 窗口标题 + 进程信息 |

---

## 📂 数据存储

### 存储位置

- **Windows**: `%APPDATA%\ClipboardMonitor\clipboard_history.json`
- **macOS**: `~/Library/Application Support/GlimpseMac/clipboard_history.json`

### 数据示例

```json
{
  "timestamp": "2026-01-03T15:30:45.123+08:00",
  "content": {
    "type": "text",
    "text": "你选中的内容"
  },
  "annotation": {
    "reaction": "like",
    "comment": "这个观点很有启发"
  },
  "source": {
    "processName": "Google Chrome"
  },
  "context": {
    "browserURL": "https://example.com/article",
    "browserTitle": "一篇好文章"
  }
}
```

---

## 🔒 隐私

- ✅ **纯本地运行** — 数据永远不离开你的电脑
- ✅ **不联网** — 无服务器、无追踪
- ✅ **开源代码** — 完全可审计

---

## 📋 路线图

- [x] 上下文捕获（浏览器、微信、VSCode、Notion）
- [x] 浮窗标注界面（点赞/点踩/评论）
- [ ] 更多应用支持（飞书、钉钉、Slack）
- [ ] 浏览器扩展（更精确的选区上下文）
- [ ] 历史查看界面
- [ ] AI 集成接口

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

---

## 📄 许可证

MIT © 2025

---

**GlimpseMe** — 你的每一次选取，都在告诉 AI 你是谁。
