using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
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

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        private const int HOTKEY_ID = 1;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_Q = 0x51;
        private const int WM_HOTKEY = 0x0312;

        // 温暖明亮主题色彩
        static readonly Color BgWarmIvory = Color.FromArgb(255, 248, 240);
        static readonly Color InputBg = Color.FromArgb(255, 250, 245);
        static readonly Color BtnLikeColor = Color.FromArgb(255, 177, 153);      // Soft Apricot
        static readonly Color BtnMehColor = Color.FromArgb(232, 228, 255);       // Pale Periwinkle
        static readonly Color BtnDislikeColor = Color.FromArgb(232, 245, 227);   // Sage Green
        static readonly Color AccentColor = Color.FromArgb(255, 169, 119);       // Soft Tangerine
        static readonly Color TextPrimary = Color.FromArgb(58, 54, 50);
        static readonly Color TextSecondary = Color.FromArgb(107, 101, 96);
        static readonly Color BorderColor = Color.FromArgb(255, 228, 214);

        private TextBox inputField;
        private ReactionButton btnLike, btnMeh, btnDislike;
        private CheckBox chkSelectAll;
        private string selectedReaction = null;

        public FloatingToolForm()
        {
            // 窗口基础设置
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(420, 56);
            this.BackColor = BgWarmIvory;
            this.ShowInTaskbar = false;
            this.TopMost = true;

            // 圆角窗口
            this.Region = CreateRoundedRegion(420, 56, 28);

            // 反应按钮
            btnLike = new ReactionButton("👍", BtnLikeColor);
            btnLike.Location = new Point(12, 12);
            btnLike.Click += (s, e) => SelectReaction(btnLike, "喜欢");
            this.Controls.Add(btnLike);

            btnMeh = new ReactionButton("😐", BtnMehColor);
            btnMeh.Location = new Point(52, 12);
            btnMeh.Click += (s, e) => SelectReaction(btnMeh, "一般");
            this.Controls.Add(btnMeh);

            btnDislike = new ReactionButton("👎", BtnDislikeColor);
            btnDislike.Location = new Point(92, 12);
            btnDislike.Click += (s, e) => SelectReaction(btnDislike, "不喜欢");
            this.Controls.Add(btnDislike);

            // 输入框
            inputField = new TextBox();
            inputField.Location = new Point(140, 14);
            inputField.Size = new Size(200, 28);
            inputField.Font = new Font("Microsoft YaHei", 10);
            inputField.BackColor = InputBg;
            inputField.ForeColor = TextPrimary;
            inputField.BorderStyle = BorderStyle.FixedSingle;
            inputField.Text = "";
            inputField.GotFocus += (s, e) => { if (inputField.Text == "") inputField.ForeColor = TextPrimary; };
            inputField.KeyDown += InputField_KeyDown;
            this.Controls.Add(inputField);

            // 全选复选框
            chkSelectAll = new CheckBox();
            chkSelectAll.Text = "全选";
            chkSelectAll.Location = new Point(350, 17);
            chkSelectAll.Size = new Size(60, 22);
            chkSelectAll.Font = new Font("Microsoft YaHei", 9);
            chkSelectAll.ForeColor = TextSecondary;
            chkSelectAll.FlatStyle = FlatStyle.Flat;
            this.Controls.Add(chkSelectAll);

            // 绘制边框
            this.Paint += Form_Paint;

            // 注册快捷键
            if (!RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT, VK_Q))
            {
                MessageBox.Show("无法注册快捷键 Alt+Q", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Region CreateRoundedRegion(int width, int height, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(0, 0, radius, radius, 180, 90);
            path.AddArc(width - radius - 1, 0, radius, radius, 270, 90);
            path.AddArc(width - radius - 1, height - radius - 1, radius, radius, 0, 90);
            path.AddArc(0, height - radius - 1, radius, radius, 90, 90);
            path.CloseFigure();
            return new Region(path);
        }

        private void Form_Paint(object sender, PaintEventArgs e)
        {
            // 绘制柔和边框
            using (Pen pen = new Pen(BorderColor, 1))
            {
                GraphicsPath path = new GraphicsPath();
                int r = 28;
                path.AddArc(0, 0, r, r, 180, 90);
                path.AddArc(this.Width - r - 1, 0, r, r, 270, 90);
                path.AddArc(this.Width - r - 1, this.Height - r - 1, r, r, 0, 90);
                path.AddArc(0, this.Height - r - 1, r, r, 90, 90);
                path.CloseFigure();
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
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
            string comment = inputField.Text;
            bool selectAll = chkSelectAll.Checked;

            // TODO: 保存到 ClipboardMonitor
            Console.WriteLine($"反馈: {selectedReaction ?? "无"}, 评论: {comment}, 全选: {selectAll}");

            HideWindow();
        }

        private void ShowAtCursor()
        {
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

                this.Show();
                SetForegroundWindow(this.Handle);
                inputField.Focus();
            }
        }

        private void HideWindow() => this.Hide();

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
                ShowAtCursor();
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
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

    // 自定义圆形反应按钮
    public class ReactionButton : Control
    {
        private string emoji;
        private Color baseColor;
        private bool isHovered = false;
        private bool isSelected = false;

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
            this.DoubleBuffered = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color fillColor = baseColor;
            if (isSelected)
                fillColor = ControlPaint.Light(baseColor, 0.1f);
            else if (isHovered)
                fillColor = ControlPaint.Light(baseColor, 0.3f);

            // 绘制圆形背景
            using (SolidBrush brush = new SolidBrush(fillColor))
            {
                e.Graphics.FillEllipse(brush, 0, 0, 31, 31);
            }

            // 选中状态边框
            if (isSelected)
            {
                using (Pen pen = new Pen(Color.FromArgb(255, 169, 119), 2))
                {
                    e.Graphics.DrawEllipse(pen, 1, 1, 29, 29);
                }
            }

            // 绘制 emoji
            using (Font font = new Font("Segoe UI Emoji", 14))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(emoji, font, Brushes.Black, new RectangleF(0, 0, 32, 32), sf);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            isHovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }
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
