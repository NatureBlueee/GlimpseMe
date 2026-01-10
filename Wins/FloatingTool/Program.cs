using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace FloatingTool
{
    public class FloatingToolForm : Form
    {
        // Win32 API
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        [DllImport("user32.dll")]
        private static extern uint RegisterWindowMessage(string lpString);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        private const int HOTKEY_ID = 1;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_Q = 0x51;
        private const int WM_HOTKEY = 0x0312;

        // IPC message from C++ ClipboardMonitor
        private static uint WM_GLIMPSEME_SHOW_FLOATING = 0;

        // DWM 常量
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        // 温暖明亮主题色彩
        static readonly Color BgWarmIvory = Color.FromArgb(255, 248, 240);
        static readonly Color InputBg = Color.FromArgb(255, 252, 250);
        static readonly Color InputBorder = Color.FromArgb(240, 220, 210);
        static readonly Color InputBorderFocus = Color.FromArgb(255, 169, 119);
        static readonly Color BtnLikeColor = Color.FromArgb(255, 177, 153);
        static readonly Color BtnMehColor = Color.FromArgb(232, 228, 255);
        static readonly Color BtnDislikeColor = Color.FromArgb(232, 245, 227);
        static readonly Color AccentColor = Color.FromArgb(255, 169, 119);
        static readonly Color TextPrimary = Color.FromArgb(58, 54, 50);
        static readonly Color TextSecondary = Color.FromArgb(140, 135, 130);
        static readonly Color TextPlaceholder = Color.FromArgb(180, 175, 170);
        static readonly Color BorderColor = Color.FromArgb(255, 235, 225);
        static readonly Color ShadowColor = Color.FromArgb(30, 120, 80, 60);

        private ModernTextBox inputField;
        private ReactionButton btnLike, btnMeh, btnDislike;
        private ModernCheckBox chkSelectAll;
        private string selectedReaction = null;
        private Timer fadeTimer;
        private float currentOpacity = 0f;
        private JsonElement? currentEntry = null;

        // 数据目录路径
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClipboardMonitor");

        public FloatingToolForm()
        {
            // 窗口基础设置
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(440, 64);
            this.BackColor = BgWarmIvory;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.Opacity = 0;

            // 启用 Windows 11 圆角
            EnableRoundedCorners();

            // 淡入动画计时器
            fadeTimer = new Timer { Interval = 16 };
            fadeTimer.Tick += FadeTimer_Tick;

            // 反应按钮
            btnLike = new ReactionButton("\U0001F44D", BtnLikeColor);
            btnLike.Location = new Point(16, 16);
            btnLike.Click += (s, e) => SelectReaction(btnLike, "like");
            this.Controls.Add(btnLike);

            btnMeh = new ReactionButton("\U0001F610", BtnMehColor);
            btnMeh.Location = new Point(56, 16);
            btnMeh.Click += (s, e) => SelectReaction(btnMeh, "meh");
            this.Controls.Add(btnMeh);

            btnDislike = new ReactionButton("\U0001F44E", BtnDislikeColor);
            btnDislike.Location = new Point(96, 16);
            btnDislike.Click += (s, e) => SelectReaction(btnDislike, "dislike");
            this.Controls.Add(btnDislike);

            // 现代输入框
            inputField = new ModernTextBox();
            inputField.Location = new Point(144, 14);
            inputField.Size = new Size(200, 36);
            inputField.PlaceholderText = "Write something...";
            inputField.KeyDown += InputField_KeyDown;
            this.Controls.Add(inputField);

            // 现代复选框
            chkSelectAll = new ModernCheckBox();
            chkSelectAll.Text = "All";
            chkSelectAll.Location = new Point(356, 20);
            chkSelectAll.Size = new Size(70, 24);
            this.Controls.Add(chkSelectAll);

            // 绘制窗口
            this.Paint += Form_Paint;

            // 注册 IPC 消息（与 C++ ClipboardMonitor 通信）
            WM_GLIMPSEME_SHOW_FLOATING = RegisterWindowMessage("WM_GLIMPSEME_SHOW_FLOATING");

            // 注册快捷键（备用）
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT, VK_Q);
        }

        private void EnableRoundedCorners()
        {
            try
            {
                int value = DWMWCP_ROUND;
                DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
            }
            catch { }
        }

        private void FadeTimer_Tick(object sender, EventArgs e)
        {
            currentOpacity += 0.12f;
            if (currentOpacity >= 1f)
            {
                currentOpacity = 1f;
                fadeTimer.Stop();
            }
            this.Opacity = EaseOutQuad(currentOpacity);
        }

        private float EaseOutQuad(float t) => t * (2 - t);

        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void Form_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // 绘制柔和边框
            using (var path = GetRoundedPath(rect, 20))
            using (var pen = new Pen(BorderColor, 1.5f))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        private void SelectReaction(ReactionButton selected, string reaction)
        {
            selectedReaction = reaction;
            btnLike.IsSelected = (selected == btnLike);
            btnMeh.IsSelected = (selected == btnMeh);
            btnDislike.IsSelected = (selected == btnDislike);
        }

        private void InputField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SaveAndClose();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                HideWindow();
                e.Handled = true;
            }
        }

        private void SaveAndClose()
        {
            string note = inputField.Text;
            bool selectAll = chkSelectAll.Checked;

            // 保存标注到 clipboard_history.json
            if (currentEntry.HasValue && (!string.IsNullOrEmpty(selectedReaction) || !string.IsNullOrEmpty(note)))
            {
                SaveAnnotation(selectedReaction, note, selectAll);
            }
            HideWindow();
        }

        private void SaveAnnotation(string reaction, string note, bool selectAll)
        {
            try
            {
                string historyPath = Path.Combine(AppDataPath, "clipboard_history.json");
                string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");

                // 构建带标注的条目
                var entry = currentEntry.Value;
                string entryJson = entry.GetRawText();

                // 在 JSON 末尾的 } 前插入 annotation
                int lastBrace = entryJson.LastIndexOf('}');
                string annotationJson = $",\n    \"annotation\": {{\n" +
                    $"      \"reaction\": \"{reaction ?? ""}\",\n" +
                    $"      \"note\": \"{EscapeJson(note)}\",\n" +
                    $"      \"is_highlight\": true,\n" +
                    $"      \"triggered_by_hotkey\": true\n" +
                    $"    }}";
                if (selectAll)
                    annotationJson += $",\n    \"full_context\": \"select_all\"";

                string newEntry = entryJson.Substring(0, lastBrace) + annotationJson + "\n  }";

                // 追加到历史文件
                AppendToHistory(historyPath, newEntry, timestamp);
            }
            catch { }
        }

        private void AppendToHistory(string path, string entry, string timestamp)
        {
            // 简单追加：读取现有文件，在 entries 数组末尾添加新条目
            if (File.Exists(path))
            {
                string content = File.ReadAllText(path);
                int entriesEnd = content.LastIndexOf(']');
                if (entriesEnd > 0)
                {
                    // 检查是否有现有条目
                    string before = content.Substring(0, entriesEnd).TrimEnd();
                    string comma = before.EndsWith("}") ? ",\n" : "\n";
                    string newContent = before + comma + entry + "\n]\n}\n";
                    File.WriteAllText(path, newContent);
                    return;
                }
            }
            // 创建新文件
            string json = $"{{\n\"version\": \"1.0\",\n\"generated\": \"{timestamp}\",\n\"entries\": [\n{entry}\n]\n}}\n";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
        }

        private string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private void ShowAtCursor()
        {
            // 读取 C++ 写入的临时文件
            LoadCurrentEntry();

            if (GetCursorPos(out POINT p))
            {
                Rectangle screen = Screen.FromPoint(new Point(p.X, p.Y)).WorkingArea;
                int x = Math.Min(p.X, screen.Right - this.Width);
                int y = Math.Min(p.Y, screen.Bottom - this.Height);
                this.Location = new Point(x, y);

                // 重置状态
                selectedReaction = null;
                btnLike.IsSelected = false;
                btnMeh.IsSelected = false;
                btnDislike.IsSelected = false;
                inputField.Text = "";
                chkSelectAll.Checked = false;

                // 淡入动画
                currentOpacity = 0f;
                this.Opacity = 0;
                this.Show();
                SetForegroundWindow(this.Handle);
                inputField.Focus();
                fadeTimer.Start();
            }
        }

        private void LoadCurrentEntry()
        {
            try
            {
                string tempPath = Path.Combine(AppDataPath, "current_entry.json");
                if (File.Exists(tempPath))
                {
                    string json = File.ReadAllText(tempPath);
                    currentEntry = JsonDocument.Parse(json).RootElement;
                }
            }
            catch { currentEntry = null; }
        }

        private void HideWindow()
        {
            fadeTimer.Stop();
            this.Hide();
        }

        protected override void WndProc(ref Message m)
        {
            // 处理 Alt+Q 快捷键（备用）
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
                ShowAtCursor();
            // 处理来自 C++ ClipboardMonitor 的 IPC 消息
            else if (WM_GLIMPSEME_SHOW_FLOATING != 0 && m.Msg == WM_GLIMPSEME_SHOW_FLOATING)
                ShowAtCursor();
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            fadeTimer?.Dispose();
            base.OnFormClosing(e);
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!this.IsHandleCreated)
            {
                CreateHandle();
                value = false;
            }
            base.SetVisibleCore(value);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            HideWindow();
        }
    }

    // 现代圆形反应按钮（带动画效果）
    public class ReactionButton : Control
    {
        private string emoji;
        private Color baseColor;
        private bool isHovered = false;
        private bool isSelected = false;
        private bool isPressed = false;
        private float scale = 1f;
        private Timer animTimer;

        public bool IsSelected
        {
            get => isSelected;
            set { isSelected = value; Invalidate(); }
        }

        public ReactionButton(string emoji, Color color)
        {
            this.emoji = emoji;
            this.baseColor = color;
            this.Size = new Size(32, 32);
            this.Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;

            animTimer = new Timer { Interval = 16 };
            animTimer.Tick += (s, e) => {
                float target = isPressed ? 0.9f : (isHovered ? 1.08f : 1f);
                scale += (target - scale) * 0.3f;
                if (Math.Abs(scale - target) < 0.01f) { scale = target; animTimer.Stop(); }
                Invalidate();
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int size = (int)(30 * scale);
            int offset = (32 - size) / 2;

            // 背景色
            Color fillColor = baseColor;
            if (isSelected) fillColor = ControlPaint.Light(baseColor, 0.15f);
            else if (isHovered) fillColor = ControlPaint.Light(baseColor, 0.25f);

            // 绘制阴影（悬停时）
            if (isHovered && !isPressed)
            {
                using (var shadowBrush = new SolidBrush(Color.FromArgb(20, 0, 0, 0)))
                    e.Graphics.FillEllipse(shadowBrush, offset + 1, offset + 2, size, size);
            }

            // 绘制圆形背景
            using (var brush = new SolidBrush(fillColor))
                e.Graphics.FillEllipse(brush, offset, offset, size - 1, size - 1);

            // 选中状态边框
            if (isSelected)
            {
                using (var pen = new Pen(Color.FromArgb(255, 169, 119), 2.5f))
                    e.Graphics.DrawEllipse(pen, offset + 1, offset + 1, size - 3, size - 3);
            }

            // 绘制 emoji
            using (var font = new Font("Segoe UI Emoji", 13 * scale))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(emoji, font, Brushes.Black, new RectangleF(0, 0, 32, 32), sf);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; animTimer.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; isPressed = false; animTimer.Start(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { isPressed = true; animTimer.Start(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { isPressed = false; animTimer.Start(); base.OnMouseUp(e); }

        protected override void Dispose(bool disposing) { if (disposing) animTimer?.Dispose(); base.Dispose(disposing); }
    }

    // 现代输入框（圆角、焦点效果）
    public class ModernTextBox : Control
    {
        private TextBox innerBox;
        private bool isFocused = false;
        private string placeholderText = "";

        public string Text { get => innerBox.Text; set => innerBox.Text = value; }
        public string PlaceholderText { get => placeholderText; set { placeholderText = value; Invalidate(); } }
        public bool Checked => false;

        public new event KeyEventHandler KeyDown { add => innerBox.KeyDown += value; remove => innerBox.KeyDown -= value; }

        public ModernTextBox()
        {
            this.Size = new Size(200, 36);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            innerBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(255, 252, 250),
                ForeColor = Color.FromArgb(58, 54, 50),
                Location = new Point(12, 9),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            innerBox.GotFocus += (s, e) => { isFocused = true; Invalidate(); };
            innerBox.LostFocus += (s, e) => { isFocused = false; Invalidate(); };
            innerBox.TextChanged += (s, e) => Invalidate();
            this.Controls.Add(innerBox);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            innerBox.Width = Width - 24;
        }

        public new bool Focus() => innerBox.Focus();

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // 背景
            using (var path = GetRoundedPath(rect, 10))
            using (var brush = new SolidBrush(Color.FromArgb(255, 252, 250)))
                e.Graphics.FillPath(brush, path);

            // 边框
            Color borderColor = isFocused ? Color.FromArgb(255, 169, 119) : Color.FromArgb(240, 220, 210);
            float borderWidth = isFocused ? 2f : 1.5f;
            using (var path = GetRoundedPath(rect, 10))
            using (var pen = new Pen(borderColor, borderWidth))
                e.Graphics.DrawPath(pen, path);

            // Placeholder
            if (string.IsNullOrEmpty(innerBox.Text) && !isFocused && !string.IsNullOrEmpty(placeholderText))
            {
                using (var brush = new SolidBrush(Color.FromArgb(180, 175, 170)))
                using (var font = new Font("Segoe UI", 10))
                    e.Graphics.DrawString(placeholderText, font, brush, 12, 9);
            }
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // 现代复选框
    public class ModernCheckBox : Control
    {
        private bool isChecked = false;
        private bool isHovered = false;

        public bool Checked { get => isChecked; set { isChecked = value; Invalidate(); } }

        public ModernCheckBox()
        {
            this.Size = new Size(70, 24);
            this.Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int boxSize = 18;
            var boxRect = new Rectangle(0, (Height - boxSize) / 2, boxSize, boxSize);

            // 复选框背景
            Color boxBg = isChecked ? Color.FromArgb(255, 169, 119) : (isHovered ? Color.FromArgb(255, 245, 240) : Color.White);
            using (var path = GetRoundedPath(boxRect, 5))
            using (var brush = new SolidBrush(boxBg))
                e.Graphics.FillPath(brush, path);

            // 边框
            Color borderColor = isChecked ? Color.FromArgb(255, 140, 90) : Color.FromArgb(220, 210, 200);
            using (var path = GetRoundedPath(boxRect, 5))
            using (var pen = new Pen(borderColor, 1.5f))
                e.Graphics.DrawPath(pen, path);

            // 勾选标记
            if (isChecked)
            {
                using (var pen = new Pen(Color.White, 2.5f))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    e.Graphics.DrawLine(pen, boxRect.X + 4, boxRect.Y + boxSize / 2, boxRect.X + 7, boxRect.Bottom - 5);
                    e.Graphics.DrawLine(pen, boxRect.X + 7, boxRect.Bottom - 5, boxRect.Right - 4, boxRect.Y + 5);
                }
            }

            // 文字
            using (var font = new Font("Segoe UI", 9.5f))
            using (var brush = new SolidBrush(Color.FromArgb(100, 95, 90)))
            {
                var textRect = new Rectangle(boxSize + 6, 0, Width - boxSize - 6, Height);
                var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(Text, font, brush, textRect, sf);
            }
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnClick(EventArgs e) { isChecked = !isChecked; Invalidate(); base.OnClick(e); }
        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); base.OnMouseLeave(e); }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GlimpseMeContext());
        }
    }

    public class GlimpseMeContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private DashboardForm dashboardForm;
        private FloatingToolForm floatingForm;

        public GlimpseMeContext()
        {
            // 创建托盘图标
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Text = "GlimpseMe - Alt+Q 唤起标注",
                Visible = true
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("打开主界面", null, OpenDashboard);
            menu.Items.Add("-");
            menu.Items.Add("退出", null, ExitApp);
            trayIcon.ContextMenuStrip = menu;
            trayIcon.Click += (s, e) => {
                if (((MouseEventArgs)e).Button == MouseButtons.Left)
                    OpenDashboard(s, e);
            };

            // 启动浮窗（后台监听快捷键）
            floatingForm = new FloatingToolForm();
        }

        private void OpenDashboard(object sender, EventArgs e)
        {
            if (dashboardForm == null || dashboardForm.IsDisposed)
            {
                dashboardForm = new DashboardForm();
            }
            dashboardForm.Show();
            dashboardForm.BringToFront();
            dashboardForm.Activate();
        }

        private void ExitApp(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            floatingForm?.Close();
            Application.Exit();
        }
    }
}
