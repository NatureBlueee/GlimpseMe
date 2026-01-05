# GlimpseMe

> **Select, Annotate, Share Your Thoughts — Let AI Truly Understand You.**

---

## 🎯 Core Vision

**Copying is not just copying — it's annotation.**

Select any content in any application, press a hotkey, and a floating panel appears instantly. You can:

- 👍 **Like** — Agree, worth saving, resonates with you
- 👎 **Dislike** — Disagree, has issues
- 💬 **Comment** — Add your thoughts, insights, connections

The system automatically captures full context: which webpage, which chat contact, which code file. What you annotate today, AI will remember tomorrow, connecting it with thoughts you marked three months ago, forming a true understanding of who you are.

**This data is built for AI to read.**

---

## ✨ What You Can Do

| Scenario | What GlimpseMe Does |
|----------|---------------------|
| See a brilliant insight on a webpage | Select → Like or comment → Auto-captures URL and page title |
| Someone says something touching in WeChat | Select → Add your reaction → Auto-captures group name and chat context |
| Discover clever code patterns | Select → Add a note → Auto-captures file path and project name |
| Read important info in Notion | Select → Give it a like → Auto-captures page path |

---

## 🔧 How It Works

```
You select content
     ↓
Press hotkey (e.g., Alt+Q)
     ↓
┌─────────────────────────────┐
│   Floating panel at cursor  │
│   ┌─────┬─────┬─────┐       │
│   │ 👍  │ 👎  │ 😐  │       │
│   └─────┴─────┴─────┘       │
│   ┌─────────────────────┐   │
│   │ Write your thoughts...│  │
│   └─────────────────────┘   │
└─────────────────────────────┘
     ↓
Auto-capture source context (URL/contact/file path...)
     ↓
Save as structured data → AI-readable and understandable
```

---

## 🚀 Quick Start

### Windows

**Requirements**: Windows 10+, Visual Studio 2022 Build Tools

```powershell
# Build ClipboardMonitor (context capture)
cd Wins/ClipboardMonitor
.\build.bat

# Build FloatingTool (annotation UI)
cd Wins/FloatingTool
dotnet build -c Release

# Run
.\bin\ClipboardMonitor.exe
# Then press Alt+Q to trigger annotation panel
```

### macOS

**Requirements**: macOS 11.0+, Xcode Command Line Tools

```bash
# Install dev tools (if not installed)
xcode-select --install

# Build
cd Mac/ClaudeWeb
chmod +x build.sh
./build.sh

# Run
./build/GlimpseMac
```

> ⚠️ **First run on macOS** requires Accessibility permission: System Settings → Privacy & Security → Accessibility

---

## 📍 Smart Context Capture

| Application | Auto-Captured Context |
|-------------|----------------------|
| 🌐 **Browsers** | Full URL + Page title |
| 💬 **WeChat** | Contact/Group name + Last 10 messages |
| 💻 **VSCode** | File path + Project name + Line number |
| 📝 **Notion** | Page path + Document structure |
| 📁 **Other Apps** | Window title + Process info |

---

## 📂 Data Storage

### Storage Location

- **Windows**: `%APPDATA%\ClipboardMonitor\clipboard_history.json`
- **macOS**: `~/Library/Application Support/GlimpseMac/clipboard_history.json`

### Data Example

```json
{
  "timestamp": "2026-01-03T15:30:45.123+08:00",
  "content": {
    "type": "text",
    "text": "Your selected content"
  },
  "annotation": {
    "reaction": "like",
    "comment": "This insight is inspiring"
  },
  "source": {
    "processName": "Google Chrome"
  },
  "context": {
    "browserURL": "https://example.com/article",
    "browserTitle": "A Great Article"
  }
}
```

---

## 🔒 Privacy

- ✅ **Fully Local** — Data never leaves your computer
- ✅ **No Network** — No servers, no tracking
- ✅ **Open Source** — Fully auditable

---

## 📋 Roadmap

- [x] Context capture (Browsers, WeChat, VSCode, Notion)
- [x] Floating annotation UI (Like/Dislike/Comment)
- [ ] More app support (Lark, DingTalk, Slack)
- [ ] Browser extension (more precise selection context)
- [ ] History viewer
- [ ] AI integration API

---

## 🤝 Contributing

Issues and Pull Requests are welcome!

---

## 📄 License

MIT © 2025

---

**GlimpseMe** — Every selection you make tells AI who you are.
