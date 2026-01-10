using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace FloatingTool
{
    public class DashboardForm : Form
    {
        // 温暖明亮主题色彩
        static readonly Color BgWarmIvory = Color.FromArgb(255, 248, 240);
        static readonly Color CardBg = Color.FromArgb(255, 252, 248);
        static readonly Color BtnLikeColor = Color.FromArgb(255, 177, 153);
        static readonly Color BtnMehColor = Color.FromArgb(232, 228, 255);
        static readonly Color BtnDislikeColor = Color.FromArgb(232, 245, 227);
        static readonly Color AccentColor = Color.FromArgb(255, 169, 119);
        static readonly Color TextPrimary = Color.FromArgb(58, 54, 50);
        static readonly Color TextSecondary = Color.FromArgb(107, 101, 96);
        static readonly Color BorderColor = Color.FromArgb(255, 228, 214);

        private TextBox searchBox;
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
        }

        private void InitializeControls()
        {
            // 顶部标题栏
            var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = BgWarmIvory };
            var titleLabel = new Label
            {
                Text = "📋 GlimpseMe",
                Font = new Font("Microsoft YaHei", 16, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(20, 18),
                AutoSize = true
            };
            header.Controls.Add(titleLabel);

            // 搜索框
            searchBox = new TextBox
            {
                Location = new Point(200, 18),
                Size = new Size(200, 28),
                Font = new Font("Microsoft YaHei", 10),
                BackColor = CardBg,
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            searchBox.TextChanged += (s, e) => RefreshList();
            header.Controls.Add(searchBox);

            // 设置按钮
            var settingsBtn = new Button
            {
                Text = "⚙️",
                Location = new Point(540, 15),
                Size = new Size(36, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = CardBg,
                ForeColor = TextPrimary,
                Cursor = Cursors.Hand
            };
            settingsBtn.FlatAppearance.BorderColor = BorderColor;
            settingsBtn.Click += (s, e) => new SettingsForm().ShowDialog();
            header.Controls.Add(settingsBtn);

            this.Controls.Add(header);

            // 筛选芯片
            filterPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 45,
                Padding = new Padding(15, 8, 15, 8),
                BackColor = BgWarmIvory
            };
            AddFilterChip("全部", "all", true);
            AddFilterChip("👍 喜欢", "like", false);
            AddFilterChip("😐 一般", "meh", false);
            AddFilterChip("👎 不喜欢", "dislike", false);
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
            var chip = new Label
            {
                Text = text,
                AutoSize = false,
                Size = new Size(80, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei", 9),
                BackColor = selected ? AccentColor : CardBg,
                ForeColor = selected ? Color.White : TextPrimary,
                Cursor = Cursors.Hand,
                Margin = new Padding(4),
                Tag = filter
            };
            chip.Paint += (s, e) => {
                var rect = new Rectangle(0, 0, chip.Width - 1, chip.Height - 1);
                using (var path = CreateRoundedRect(rect, 14))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var brush = new SolidBrush(chip.BackColor))
                        e.Graphics.FillPath(brush, path);
                    using (var pen = new Pen(BorderColor))
                        e.Graphics.DrawPath(pen, path);
                }
                TextRenderer.DrawText(e.Graphics, chip.Text, chip.Font, rect, chip.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            chip.Click += (s, e) => {
                currentFilter = filter;
                foreach (Label c in filterPanel.Controls)
                {
                    c.BackColor = c.Tag.ToString() == filter ? AccentColor : CardBg;
                    c.ForeColor = c.Tag.ToString() == filter ? Color.White : TextPrimary;
                    c.Invalidate();
                }
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
            var card = new Panel
            {
                Location = new Point(5, y),
                Size = new Size(listPanel.Width - 50, 80),
                BackColor = CardBg,
                Cursor = Cursors.Hand
            };
            card.Paint += (s, e) => {
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using (var path = CreateRoundedRect(rect, 12))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var brush = new SolidBrush(CardBg))
                        e.Graphics.FillPath(brush, path);
                    using (var pen = new Pen(BorderColor))
                        e.Graphics.DrawPath(pen, path);
                }
            };

            // 反应图标
            string emoji = entry.Reaction switch
            {
                "like" => "👍",
                "meh" => "😐",
                "dislike" => "👎",
                _ => "📝"
            };
            Color emojiColor = entry.Reaction switch
            {
                "like" => BtnLikeColor,
                "meh" => BtnMehColor,
                "dislike" => BtnDislikeColor,
                _ => CardBg
            };

            var emojiLabel = new Label
            {
                Text = emoji,
                Font = new Font("Segoe UI Emoji", 16),
                Location = new Point(12, 12),
                Size = new Size(36, 36),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = emojiColor
            };
            emojiLabel.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(emojiColor))
                    e.Graphics.FillEllipse(brush, 0, 0, 35, 35);
                TextRenderer.DrawText(e.Graphics, emoji, emojiLabel.Font,
                    new Rectangle(0, 0, 36, 36), Color.Black,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            card.Controls.Add(emojiLabel);

            // 内容预览
            var contentLabel = new Label
            {
                Text = $"\"{TruncateText(entry.Content, 60)}\"",
                Font = new Font("Microsoft YaHei", 10),
                ForeColor = TextPrimary,
                Location = new Point(56, 10),
                Size = new Size(card.Width - 80, 22),
                AutoEllipsis = true
            };
            card.Controls.Add(contentLabel);

            // 评论（如果有）
            if (!string.IsNullOrEmpty(entry.Comment))
            {
                var commentLabel = new Label
                {
                    Text = $"💬 \"{entry.Comment}\"",
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
                Text = $"{entry.SourceApp} · {entry.SourceDetail} · {GetRelativeTime(entry.Timestamp)}",
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
            this.Text = "设置";
            this.Size = new Size(400, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = BgWarmIvory;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int y = 20;

            // 快捷键设置
            AddSettingRow("触发浮窗快捷键", "Alt + Q", ref y);

            // 主题设置
            AddSettingRow("主题", "跟随系统", ref y);

            // 开机自启
            AddToggleRow("开机自启动", true, ref y);

            // 存储位置
            AddSettingRow("存储位置", "%APPDATA%\\ClipboardMonitor", ref y);

            // 导出按钮
            var exportBtn = new Button
            {
                Text = "导出数据",
                Location = new Point(20, y + 20),
                Size = new Size(100, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = CardBg,
                ForeColor = TextPrimary
            };
            exportBtn.FlatAppearance.BorderColor = BorderColor;
            this.Controls.Add(exportBtn);
        }

        private void AddSettingRow(string label, string value, ref int y)
        {
            var lbl = new Label
            {
                Text = label,
                Location = new Point(20, y),
                Size = new Size(150, 24),
                ForeColor = TextPrimary,
                Font = new Font("Microsoft YaHei", 10)
            };
            var val = new Label
            {
                Text = value,
                Location = new Point(180, y),
                Size = new Size(180, 24),
                ForeColor = Color.Gray,
                Font = new Font("Microsoft YaHei", 10),
                TextAlign = ContentAlignment.MiddleRight
            };
            this.Controls.Add(lbl);
            this.Controls.Add(val);
            y += 45;
        }

        private void AddToggleRow(string label, bool isOn, ref int y)
        {
            var lbl = new Label
            {
                Text = label,
                Location = new Point(20, y),
                Size = new Size(150, 24),
                ForeColor = TextPrimary,
                Font = new Font("Microsoft YaHei", 10)
            };
            var toggle = new CheckBox
            {
                Checked = isOn,
                Location = new Point(340, y),
                Size = new Size(20, 24)
            };
            this.Controls.Add(lbl);
            this.Controls.Add(toggle);
            y += 45;
        }
    }
}
