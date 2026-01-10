using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace FloatingTool
{
    public class DashboardForm : Form
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        // 温暖明亮主题色彩
        static readonly Color BgWarmIvory = Color.FromArgb(255, 248, 240);
        static readonly Color CardBg = Color.FromArgb(255, 252, 248);
        static readonly Color CardHover = Color.FromArgb(255, 255, 252);
        static readonly Color BtnLikeColor = Color.FromArgb(255, 177, 153);
        static readonly Color BtnMehColor = Color.FromArgb(232, 228, 255);
        static readonly Color BtnDislikeColor = Color.FromArgb(232, 245, 227);
        static readonly Color AccentColor = Color.FromArgb(255, 169, 119);
        static readonly Color TextPrimary = Color.FromArgb(58, 54, 50);
        static readonly Color TextSecondary = Color.FromArgb(107, 101, 96);
        static readonly Color BorderColor = Color.FromArgb(255, 228, 214);
        static readonly Color InputBorder = Color.FromArgb(240, 220, 210);
        static readonly Color InputBorderFocus = Color.FromArgb(255, 169, 119);

        private ModernSearchBox searchBox;
        private FlowLayoutPanel filterPanel;
        private Panel listPanel;
        private List<ClipboardEntry> entries = new List<ClipboardEntry>();
        private string currentFilter = "all";

        public DashboardForm()
        {
            InitializeForm();
            InitializeControls();
            LoadHistory();
        }

        private void InitializeForm()
        {
            this.Text = "GlimpseMe";
            this.Size = new Size(600, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = BgWarmIvory;
            this.Font = new Font("Microsoft YaHei", 10);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Windows 11 圆角
            try {
                int value = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
            } catch { }
        }

        private void InitializeControls()
        {
            // 顶部标题栏
            var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = BgWarmIvory };
            var titleLabel = new Label
            {
                Text = "\U0001F4CB GlimpseMe",
                Font = new Font("Microsoft YaHei", 16, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(20, 18),
                AutoSize = true
            };
            header.Controls.Add(titleLabel);

            // 现代搜索框
            searchBox = new ModernSearchBox
            {
                Location = new Point(200, 14),
                Size = new Size(220, 32),
                PlaceholderText = "搜索..."
            };
            searchBox.TextChanged += (s, e) => RefreshList();
            header.Controls.Add(searchBox);

            // 现代设置按钮
            var settingsBtn = new ModernIconButton("\u2699")
            {
                Location = new Point(540, 14),
                Size = new Size(36, 32)
            };
            settingsBtn.Click += (s, e) => new SettingsForm().ShowDialog();
            header.Controls.Add(settingsBtn);

            this.Controls.Add(header);

            // 筛选芯片
            filterPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(15, 10, 15, 10),
                BackColor = BgWarmIvory
            };
            AddFilterChip("\u5168\u90E8", "all", true);
            AddFilterChip("\U0001F44D \u559C\u6B22", "like", false);
            AddFilterChip("\U0001F610 \u4E00\u822C", "meh", false);
            AddFilterChip("\U0001F44E \u4E0D\u559C\u6B22", "dislike", false);
            this.Controls.Add(filterPanel);

            // 列表区域
            listPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BgWarmIvory,
                Padding = new Padding(15)
            };
            this.Controls.Add(listPanel);
        }

        private void AddFilterChip(string text, string filter, bool selected)
        {
            var chip = new ModernChip(text, filter, selected);
            chip.ChipClick += (s, e) => {
                currentFilter = filter;
                foreach (ModernChip c in filterPanel.Controls)
                    c.SetSelected(c.Filter == filter);
                RefreshList();
            };
            filterPanel.Controls.Add(chip);
        }

        private void LoadHistory()
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClipboardMonitor", "clipboard_history.json");

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("entries", out var entriesArray))
                    {
                        foreach (var entry in entriesArray.EnumerateArray())
                        {
                            entries.Add(ClipboardEntry.FromJson(entry));
                        }
                    }
                }
                catch { }
            }
            RefreshList();
        }

        private void RefreshList()
        {
            listPanel.Controls.Clear();
            int y = 10;
            string search = searchBox.Text.ToLower();
            string lastDate = "";

            var filtered = entries.FindAll(e =>
                (currentFilter == "all" || e.Reaction == currentFilter) &&
                (string.IsNullOrEmpty(search) || e.Content.ToLower().Contains(search)));

            filtered.Reverse();

            foreach (var entry in filtered)
            {
                string date = entry.Timestamp.ToString("yyyy-MM-dd");
                if (date != lastDate)
                {
                    var dateLabel = new Label
                    {
                        Text = GetDateLabel(entry.Timestamp),
                        Font = new Font("Microsoft YaHei", 11, FontStyle.Bold),
                        ForeColor = TextSecondary,
                        Location = new Point(5, y),
                        AutoSize = true
                    };
                    listPanel.Controls.Add(dateLabel);
                    y += 35;
                    lastDate = date;
                }

                var card = CreateCard(entry, y);
                listPanel.Controls.Add(card);
                y += card.Height + 12;
            }
        }

        private string GetDateLabel(DateTime dt)
        {
            if (dt.Date == DateTime.Today) return "今天";
            if (dt.Date == DateTime.Today.AddDays(-1)) return "昨天";
            return dt.ToString("M月d日");
        }

        private Panel CreateCard(ClipboardEntry entry, int y)
        {
            var card = new ModernCard
            {
                Location = new Point(5, y),
                Size = new Size(listPanel.Width - 50, 80)
            };

            // 反应图标
            string emoji = entry.Reaction switch
            {
                "like" => "\U0001F44D",
                "meh" => "\U0001F610",
                "dislike" => "\U0001F44E",
                _ => "\U0001F4DD"
            };
            Color emojiColor = entry.Reaction switch
            {
                "like" => BtnLikeColor,
                "meh" => BtnMehColor,
                "dislike" => BtnDislikeColor,
                _ => CardBg
            };

            var emojiLabel = new Label { Size = new Size(36, 36), Location = new Point(12, 12) };
            emojiLabel.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(emojiColor))
                    e.Graphics.FillEllipse(brush, 0, 0, 35, 35);
                TextRenderer.DrawText(e.Graphics, emoji, new Font("Segoe UI Emoji", 14),
                    new Rectangle(0, 0, 36, 36), TextPrimary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            card.Controls.Add(emojiLabel);

            // 内容预览
            var contentLabel = new Label
            {
                Text = $"\"{TruncateText(entry.Content, 55)}\"",
                Font = new Font("Microsoft YaHei", 10),
                ForeColor = TextPrimary,
                Location = new Point(56, 10),
                Size = new Size(card.Width - 80, 22),
                AutoEllipsis = true
            };
            card.Controls.Add(contentLabel);

            // 评论
            if (!string.IsNullOrEmpty(entry.Comment))
            {
                var commentLabel = new Label
                {
                    Text = $"\U0001F4AC \"{entry.Comment}\"",
                    Font = new Font("Microsoft YaHei", 9, FontStyle.Italic),
                    ForeColor = TextSecondary,
                    Location = new Point(56, 32),
                    Size = new Size(card.Width - 80, 18),
                    AutoEllipsis = true
                };
                card.Controls.Add(commentLabel);
            }

            // 来源信息
            var sourceLabel = new Label
            {
                Text = $"{entry.SourceApp} \u00B7 {entry.SourceDetail} \u00B7 {GetRelativeTime(entry.Timestamp)}",
                Font = new Font("Microsoft YaHei", 8),
                ForeColor = TextSecondary,
                Location = new Point(56, 54),
                Size = new Size(card.Width - 80, 16),
                AutoEllipsis = true
            };
            card.Controls.Add(sourceLabel);

            return card;
        }

        private string TruncateText(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...";
        }

        private string GetRelativeTime(DateTime dt)
        {
            var span = DateTime.Now - dt;
            if (span.TotalMinutes < 1) return "刚刚";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}分钟前";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}小时前";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}天前";
            return dt.ToString("M月d日");
        }

        private GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
            path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class ClipboardEntry
    {
        public DateTime Timestamp { get; set; }
        public string Content { get; set; } = "";
        public string Reaction { get; set; } = "";
        public string Comment { get; set; } = "";
        public string SourceApp { get; set; } = "";
        public string SourceDetail { get; set; } = "";

        public static ClipboardEntry FromJson(JsonElement el)
        {
            var entry = new ClipboardEntry();
            if (el.TryGetProperty("timestamp", out var ts) && DateTime.TryParse(ts.GetString(), out var dt))
                entry.Timestamp = dt;
            if (el.TryGetProperty("content", out var content))
                entry.Content = content.GetString() ?? "";
            if (el.TryGetProperty("content_preview", out var preview))
                entry.Content = preview.GetString() ?? entry.Content;
            if (el.TryGetProperty("annotation", out var ann))
            {
                if (ann.TryGetProperty("reaction", out var r)) entry.Reaction = r.GetString() ?? "";
                if (ann.TryGetProperty("comment", out var c)) entry.Comment = c.GetString() ?? "";
            }
            if (el.TryGetProperty("source", out var src))
            {
                if (src.TryGetProperty("process_name", out var p)) entry.SourceApp = p.GetString() ?? "";
            }
            if (el.TryGetProperty("context", out var ctx))
            {
                if (ctx.TryGetProperty("source_url", out var url)) entry.SourceDetail = url.GetString() ?? "";
                else if (ctx.TryGetProperty("page_title", out var title)) entry.SourceDetail = title.GetString() ?? "";
                else if (ctx.TryGetProperty("contactName", out var contact)) entry.SourceDetail = contact.GetString() ?? "";
            }
            return entry;
        }
    }

    public class SettingsForm : Form
    {
        static readonly Color BgWarmIvory = Color.FromArgb(255, 248, 240);
        static readonly Color CardBg = Color.FromArgb(255, 252, 248);
        static readonly Color TextPrimary = Color.FromArgb(58, 54, 50);
        static readonly Color BorderColor = Color.FromArgb(255, 228, 214);

        public SettingsForm()
        {
            this.Text = "\u8BBE\u7F6E";
            this.Size = new Size(400, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = BgWarmIvory;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int y = 20;
            AddSettingRow("\u89E6\u53D1\u6D6E\u7A97\u5FEB\u6377\u952E", "Alt + Q", ref y);
            AddSettingRow("\u4E3B\u9898", "\u8DDF\u968F\u7CFB\u7EDF", ref y);
            AddToggleRow("\u5F00\u673A\u81EA\u542F\u52A8", true, ref y);
            AddSettingRow("\u5B58\u50A8\u4F4D\u7F6E", "%APPDATA%\\ClipboardMonitor", ref y);

            var exportBtn = new ModernIconButton("\u5BFC\u51FA") { Location = new Point(20, y + 20), Size = new Size(100, 32) };
            this.Controls.Add(exportBtn);
        }

        private void AddSettingRow(string label, string value, ref int y)
        {
            this.Controls.Add(new Label { Text = label, Location = new Point(20, y), Size = new Size(150, 24), ForeColor = TextPrimary, Font = new Font("Microsoft YaHei", 10) });
            this.Controls.Add(new Label { Text = value, Location = new Point(180, y), Size = new Size(180, 24), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei", 10), TextAlign = ContentAlignment.MiddleRight });
            y += 45;
        }

        private void AddToggleRow(string label, bool isOn, ref int y)
        {
            this.Controls.Add(new Label { Text = label, Location = new Point(20, y), Size = new Size(150, 24), ForeColor = TextPrimary, Font = new Font("Microsoft YaHei", 10) });
            this.Controls.Add(new CheckBox { Checked = isOn, Location = new Point(340, y), Size = new Size(20, 24) });
            y += 45;
        }
    }

    // 现代搜索框
    public class ModernSearchBox : Control
    {
        private TextBox innerBox;
        private bool isFocused;
        public string PlaceholderText { get; set; } = "";
        public new string Text { get => innerBox.Text; set => innerBox.Text = value; }
        public new event EventHandler TextChanged { add => innerBox.TextChanged += value; remove => innerBox.TextChanged -= value; }

        static readonly Color InputBg = Color.FromArgb(255, 252, 250);
        static readonly Color InputBorder = Color.FromArgb(240, 220, 210);
        static readonly Color InputBorderFocus = Color.FromArgb(255, 169, 119);

        public ModernSearchBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            innerBox = new TextBox { BorderStyle = BorderStyle.None, BackColor = InputBg, Font = new Font("Microsoft YaHei", 10), Location = new Point(10, 7) };
            innerBox.GotFocus += (s, e) => { isFocused = true; Invalidate(); };
            innerBox.LostFocus += (s, e) => { isFocused = false; Invalidate(); };
            Controls.Add(innerBox);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); innerBox.Size = new Size(Width - 20, Height - 14); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = GetRoundedPath(rect, 8))
            {
                using (var brush = new SolidBrush(InputBg)) e.Graphics.FillPath(brush, path);
                using (var pen = new Pen(isFocused ? InputBorderFocus : InputBorder, isFocused ? 2 : 1)) e.Graphics.DrawPath(pen, path);
            }
            if (string.IsNullOrEmpty(innerBox.Text) && !isFocused)
                TextRenderer.DrawText(e.Graphics, PlaceholderText, innerBox.Font, new Rectangle(10, 7, Width - 20, Height - 14), Color.FromArgb(180, 175, 170), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // 现代图标按钮
    public class ModernIconButton : Control
    {
        private string text;
        private bool isHovered;
        static readonly Color CardBg = Color.FromArgb(255, 252, 248);
        static readonly Color CardHover = Color.FromArgb(255, 248, 242);
        static readonly Color BorderColor = Color.FromArgb(255, 228, 214);

        public ModernIconButton(string text) { this.text = text; Cursor = Cursors.Hand; SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true); }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = GetRoundedPath(rect, 8))
            {
                using (var brush = new SolidBrush(isHovered ? CardHover : CardBg)) e.Graphics.FillPath(brush, path);
                using (var pen = new Pen(BorderColor)) e.Graphics.DrawPath(pen, path);
            }
            TextRenderer.DrawText(e.Graphics, text, new Font("Segoe UI Emoji", 12), rect, Color.FromArgb(58, 54, 50), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // 现代筛选芯片
    public class ModernChip : Control
    {
        public string Filter { get; }
        private string text;
        private bool isSelected, isHovered;
        public event EventHandler ChipClick;

        static readonly Color CardBg = Color.FromArgb(255, 252, 248);
        static readonly Color CardHover = Color.FromArgb(255, 248, 242);
        static readonly Color AccentColor = Color.FromArgb(255, 169, 119);
        static readonly Color BorderColor = Color.FromArgb(255, 228, 214);

        public ModernChip(string text, string filter, bool selected)
        {
            this.text = text; Filter = filter; isSelected = selected;
            Size = new Size(85, 30); Margin = new Padding(4); Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        public void SetSelected(bool sel) { isSelected = sel; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); ChipClick?.Invoke(this, e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var bgColor = isSelected ? AccentColor : (isHovered ? CardHover : CardBg);
            var fgColor = isSelected ? Color.White : Color.FromArgb(58, 54, 50);
            using (var path = GetRoundedPath(rect, Height / 2))
            {
                using (var brush = new SolidBrush(bgColor)) e.Graphics.FillPath(brush, path);
                if (!isSelected) using (var pen = new Pen(BorderColor)) e.Graphics.DrawPath(pen, path);
            }
            TextRenderer.DrawText(e.Graphics, text, new Font("Microsoft YaHei", 9), rect, fgColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // 现代卡片（带悬停效果）
    public class ModernCard : Panel
    {
        private bool isHovered;
        private int shadowOffset = 2;
        static readonly Color CardBg = Color.FromArgb(255, 252, 248);
        static readonly Color CardHover = Color.FromArgb(255, 255, 252);
        static readonly Color BorderColor = Color.FromArgb(255, 228, 214);

        public ModernCard()
        {
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; shadowOffset = 4; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; shadowOffset = 2; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            // 阴影
            var shadowRect = new Rectangle(shadowOffset, shadowOffset, Width - shadowOffset - 1, Height - shadowOffset - 1);
            using (var shadowPath = GetRoundedPath(shadowRect, 12))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(isHovered ? 25 : 15, 120, 80, 60)))
                e.Graphics.FillPath(shadowBrush, shadowPath);
            // 卡片
            var rect = new Rectangle(0, 0, Width - shadowOffset - 1, Height - shadowOffset - 1);
            using (var path = GetRoundedPath(rect, 12))
            {
                using (var brush = new SolidBrush(isHovered ? CardHover : CardBg)) e.Graphics.FillPath(brush, path);
                using (var pen = new Pen(BorderColor)) e.Graphics.DrawPath(pen, path);
            }
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
