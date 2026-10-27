// RPF Studio — OpenIV + ArchiveFix в одном окне.
// Сборка: build.bat (использует встроенный в Windows csc.exe из .NET Framework 4).
// Проверка вёрстки без показа окна: RpfStudio.exe --snap <папка> — сохраняет PNG каждой страницы.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RpfStudio
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new MainForm();
            if (args.Length >= 2 && args[0] == "--snap") form.SnapshotTo(args[1]);
            Application.Run(form);
        }
    }

    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(18, 20, 25);
        public static readonly Color Surface = Color.FromArgb(28, 31, 38);
        public static readonly Color SurfaceHi = Color.FromArgb(38, 42, 51);
        public static readonly Color Border = Color.FromArgb(48, 53, 64);
        public static readonly Color Fg = Color.FromArgb(234, 236, 240);
        public static readonly Color Muted = Color.FromArgb(140, 147, 160);
        public static readonly Color Blue = Color.FromArgb(64, 132, 245);
        public static readonly Color Green = Color.FromArgb(38, 170, 90);
        public static readonly Color Red = Color.FromArgb(235, 87, 87);
        public static readonly Color Yellow = Color.FromArgb(240, 180, 60);

        public static readonly Font Body = new Font("Segoe UI", 9.75f);
        public static readonly Font BodyBold = new Font("Segoe UI Semibold", 9.75f);
        public static readonly Font Small = new Font("Segoe UI", 8.5f);
        public static readonly Font H1 = new Font("Segoe UI Semibold", 20f);
        public static readonly Font H2 = new Font("Segoe UI Semibold", 15f);
        public static readonly Font CardTitle = new Font("Segoe UI Semibold", 17f);
        public static readonly Font Mono = new Font("Consolas", 9.5f);
        public static readonly Font Icon = new Font("Segoe MDL2 Assets", 11f);
        public static readonly Font IconBig = new Font("Segoe MDL2 Assets", 24f);

        // Глифы Segoe MDL2 Assets
        public const string GPlay = "\uE768", GRepair = "\uE90F", GSettings = "\uE713", GBack = "\uE72B",
            GAdd = "\uE710", GFolder = "\uE8B7", GDelete = "\uE74D", GClear = "\uE894", GSync = "\uE895",
            GPackage = "\uE7B8", GSearch = "\uE721", GArrow = "\uE76C";

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }
    }

    // Скруглённая кнопка с иконкой и подсветкой при наведении
    class FlatBtn : Control
    {
        public Color BaseColor = Theme.SurfaceHi;
        public string Glyph = "";
        public int Radius = 8;
        bool hover, down;

        public FlatBtn(string text, string glyph, Color color)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Text = text; Glyph = glyph; BaseColor = color;
            ForeColor = Color.White; Font = Theme.Body; Cursor = Cursors.Hand;
            Height = 38; Margin = new Padding(0, 0, 8, 8);
            Width = TextRenderer.MeasureText(text, Font).Width + (glyph != "" ? 30 : 0) + 32;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Color c = BaseColor;
            if (!Enabled) c = Theme.Mix(BaseColor, Theme.Bg, 0.55f);
            else if (down) c = Theme.Mix(BaseColor, Color.Black, 0.18f);
            else if (hover) c = Theme.Mix(BaseColor, Color.White, 0.12f);
            using (var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Radius))
            using (var br = new SolidBrush(c))
                g.FillPath(br, path);

            Color fg = Enabled ? ForeColor : Theme.Mix(ForeColor, Theme.Bg, 0.5f);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            int tw = Text == "" ? 0 : TextRenderer.MeasureText(g, Text, Font, Size.Empty, flags).Width;
            int iw = Glyph == "" ? 0 : TextRenderer.MeasureText(g, Glyph, Theme.Icon, Size.Empty, flags).Width;
            int gap = (tw > 0 && iw > 0) ? 8 : 0;
            int x = (Width - tw - iw - gap) / 2;
            if (iw > 0) TextRenderer.DrawText(g, Glyph, Theme.Icon, new Rectangle(x, 0, iw, Height), fg, flags);
            if (tw > 0) TextRenderer.DrawText(g, Text, Font, new Rectangle(x + iw + gap, 0, tw + 2, Height), fg, flags);
        }
    }

    // Карточка выбора: большая (главный экран) или компактная строка (действия на странице OpenIV)
    class Card : Control
    {
        public string Title = "", Desc = "", Glyph = "", Hint = "";
        public Color Accent = Theme.Blue;
        public bool Compact;
        bool hover;

        public Card()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Margin = new Padding(0, 0, 12, 12);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            bool hot = hover && Enabled;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.Round(rect, 14))
            {
                using (var br = new SolidBrush(hot ? Theme.SurfaceHi : Theme.Surface)) g.FillPath(br, path);
                using (var pen = new Pen(hot ? Accent : Theme.Border, hot ? 2f : 1f)) g.DrawPath(pen, path);
            }
            float dim = Enabled ? 0f : 0.55f;
            Color fg = Theme.Mix(Theme.Fg, Theme.Surface, dim), muted = Theme.Mix(Theme.Muted, Theme.Surface, dim);
            Color acc = Theme.Mix(Accent, Theme.Surface, dim);

            if (Compact)
            {
                int d = 44, cy = Height / 2;
                var circle = new Rectangle(18, cy - d / 2, d, d);
                using (var br = new SolidBrush(Theme.Mix(acc, Theme.Surface, 0.75f))) g.FillEllipse(br, circle);
                TextRenderer.DrawText(g, Glyph, Theme.Icon, circle, acc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                int tx = circle.Right + 16, tw = Width - tx - 48;
                TextRenderer.DrawText(g, Title, Theme.BodyBold, new Rectangle(tx, cy - 22, tw, 22), fg, TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, Desc, Theme.Small, new Rectangle(tx, cy + 1, tw, 22), muted, TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, Theme.GArrow, Theme.Icon, new Rectangle(Width - 44, 0, 30, Height), hot ? acc : muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            // Большая карточка: иконка, заголовок, описание, подсказка — по центру
            int circleD = 84;
            int blockH = circleD + 18 + 34 + 8 + 44 + 18 + 22;
            int y = Math.Max(16, (Height - blockH) / 2);
            var c2 = new Rectangle((Width - circleD) / 2, y, circleD, circleD);
            using (var br = new SolidBrush(Theme.Mix(acc, Theme.Surface, hot ? 0.6f : 0.75f))) g.FillEllipse(br, c2);
            TextRenderer.DrawText(g, Glyph, Theme.IconBig, c2, acc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            y += circleD + 18;
            TextRenderer.DrawText(g, Title, Theme.CardTitle, new Rectangle(0, y, Width, 34), fg, TextFormatFlags.HorizontalCenter);
            y += 34 + 8;
            TextRenderer.DrawText(g, Desc, Theme.Body, new Rectangle(20, y, Width - 40, 44), muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
            y += 44 + 18;
            // подсказка: текст обычным шрифтом + стрелка иконочным
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            int hw = TextRenderer.MeasureText(g, Hint, Theme.BodyBold, Size.Empty, flags).Width;
            int aw = TextRenderer.MeasureText(g, Theme.GArrow, Theme.Icon, Size.Empty, flags).Width;
            int hx = (Width - hw - aw - 8) / 2 + (hot ? 3 : 0);
            TextRenderer.DrawText(g, Hint, Theme.BodyBold, new Rectangle(hx, y, hw + 2, 22), acc, flags | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, Theme.GArrow, Theme.Icon, new Rectangle(hx + hw + 8 + (hot ? 3 : 0), y, aw + 2, 22), acc, flags | TextFormatFlags.VerticalCenter);
        }
    }

    // Тонкая полоса прогресса
    class ProgressLine : Control
    {
        float value;
        public float Value { get { return value; } set { this.value = Math.Max(0, Math.Min(1, value)); Invalidate(); } }
        public ProgressLine()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 6;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            using (var p = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), (Height - 1) / 2f))
            using (var br = new SolidBrush(Theme.SurfaceHi)) g.FillPath(br, p);
            float w = (Width - 1) * value;
            if (w >= Height)
                using (var p = Theme.Round(new RectangleF(0, 0, w, Height - 1), (Height - 1) / 2f))
                using (var br = new SolidBrush(Theme.Green)) g.FillPath(br, p);
        }
    }

    class Settings
    {
        public string OpenIVPath = "";
        public string ArchiveFixDir = "";
        public string GtaDir = "";
        public bool Backup = true;

        static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini"); }
        }

        public static Settings Load()
        {
            var s = new Settings();
            if (File.Exists(FilePath))
            {
                foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                    if (k == "OpenIV") s.OpenIVPath = v;
                    else if (k == "ArchiveFix") s.ArchiveFixDir = v;
                    else if (k == "GTA") s.GtaDir = v;
                    else if (k == "Backup") s.Backup = v == "1";
                }
            }
            if (s.OpenIVPath == "") s.OpenIVPath = DetectOpenIV();
            if (s.ArchiveFixDir == "") s.ArchiveFixDir = DetectArchiveFix();
            if (s.GtaDir == "") s.GtaDir = DetectGta();
            return s;
        }

        public void Save()
        {
            try
            {
                // Truncate вместо перезаписи: так можно сохранять и в скрытый settings.ini
                // (File.WriteAllLines на скрытом файле падает с «доступ запрещён»)
                var mode = File.Exists(FilePath) ? FileMode.Truncate : FileMode.CreateNew;
                using (var w = new StreamWriter(new FileStream(FilePath, mode, FileAccess.Write), Encoding.UTF8))
                {
                    w.WriteLine("OpenIV=" + OpenIVPath);
                    w.WriteLine("ArchiveFix=" + ArchiveFixDir);
                    w.WriteLine("GTA=" + GtaDir);
                    w.WriteLine("Backup=" + (Backup ? "1" : "0"));
                }
            }
            catch { }
        }

        public static string DetectOpenIV()
        {
            string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"New Technology Studio\Apps\OpenIV\OpenIV.exe");
            return File.Exists(p) ? p : "";
        }

        public static string DetectArchiveFix()
        {
            string[] candidates = {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ArchiveFix"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ArchiveFix")
            };
            foreach (var c in candidates)
                if (File.Exists(Path.Combine(c, "ArchiveFix.exe"))) return c.TrimEnd('\\');
            return "";
        }

        public static string DetectGta()
        {
            string[] keys = { @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", @"SOFTWARE\WOW6432Node\Rockstar Games\GTAV" };
            string[] values = { "InstallFolder", "InstallFolderSteam", "InstallFolderEpic" };
            foreach (var key in keys)
            {
                try
                {
                    using (var k = Registry.LocalMachine.OpenSubKey(key))
                    {
                        if (k == null) continue;
                        foreach (var v in values)
                        {
                            var p = k.GetValue(v) as string;
                            if (!string.IsNullOrEmpty(p))
                            {
                                p = p.TrimEnd('\\');
                                if (Directory.Exists(p)) return p;
                            }
                        }
                    }
                }
                catch { }
            }
            return "";
        }
    }

    class MainForm : Form
    {
        enum LogKind { Info, Ok, Error, Step }
        // Состояние архива в списке
        const int StPending = 0, StRunning = 1, StOk = 2, StFail = 3, StSkip = 4;
        // Флаг шифрования в заголовке RPF7: так OpenIV помечает изменённые им архивы
        const uint EncOpen = 0x4E45504F;

        Settings settings;
        TextBox txtOpenIV, txtFix, txtGta;
        Label stOpenIV, stFix, stGta;
        RichTextBox txtLog;
        ListBox lstFiles, lstMods;
        Label lblEmpty, lblCount, lblModsEmpty;
        FlatBtn btnOpenInOiv;
        CheckBox chkBackup;
        FlatBtn btnFix;
        Card cardLaunch, cardAuto;
        ProgressLine progress;
        Label sbOpenIV, sbFix, sbGta;
        Panel pageHost;
        Control pageHome, pageOpenIV, pageFix, pageSettings;
        readonly Dictionary<string, int> fileState = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> fileSize = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        volatile bool busy;
        bool snapMode;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public MainForm()
        {
            settings = Settings.Load();
            Text = "RPF Studio — OpenIV + ArchiveFix";
            Size = new Size(960, 720);
            MinimumSize = new Size(800, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Fg;
            Font = Theme.Body;
            DoubleBuffered = true;
            BuildUi();
            FormClosing += (s, e) =>
            {
                if (busy && !snapMode && MessageBox.Show(this,
                        "Сейчас идёт работа (исправление архивов или ожидание OpenIV). Закрыть всё равно?",
                        "RPF Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                if (!snapMode) SaveSettings();
            };
            Log("Выберите, что открыть: OpenIV или ArchiveFix.", LogKind.Info);
            CheckPaths();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Тёмный заголовок окна (Windows 10 1809+/11)
            try { int on = 1; DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { }
        }

        // Отрисовывает все страницы в PNG и закрывается — для проверки вёрстки
        public void SnapshotTo(string dir)
        {
            Directory.CreateDirectory(dir);
            Opacity = 0;
            snapMode = true; // не сохранять тестовые данные; пути считаются найденными
            // Примерные пути вместо настоящих, чтобы на скриншотах не было личных данных
            txtOpenIV.Text = @"C:\Users\Player\AppData\Local\New Technology Studio\Apps\OpenIV\OpenIV.exe";
            txtFix.Text = @"C:\Users\Player\Desktop\ArchiveFix";
            txtGta.Text = @"D:\Games\GTAV";
            ShowInTaskbar = false;
            AddFile(@"C:\Games\GTAV\mods\x64e.rpf");
            AddFile(@"C:\Games\GTAV\mods\update\update.rpf");
            AddFile(@"C:\Games\GTAV\mods\x64\levels\gta5\vehicles.rpf");
            AddModFile(@"C:\Users\Player\Desktop\BMW M5\bmw_m5.oiv");
            AddModFile(@"C:\Users\Player\Desktop\BMW M5\m5.yft");
            AddModFile(@"C:\Users\Player\Desktop\BMW M5\m5.ytd");
            AddModFile(@"C:\Users\Player\Desktop\BMW M5\handling.meta");
            fileState[@"C:\Games\GTAV\mods\x64e.rpf"] = StOk;
            fileState[@"C:\Games\GTAV\mods\update\update.rpf"] = StRunning;
            progress.Value = 0.4f;
            Log("ArchiveFix → C:\\Games\\GTAV\\mods\\x64e.rpf", LogKind.Step);
            Log("Готово. Успешно: 1, ошибок: 0.", LogKind.Ok);
            Log("Нет файла: C:\\Games\\GTAV\\mods\\nope.rpf", LogKind.Error);
            Shown += delegate
            {
                var pages = new[] { pageHome, pageOpenIV, pageFix, pageSettings };
                string[] names = { "home", "openiv", "archivefix", "settings" };
                for (int i = 0; i < pages.Length; i++)
                {
                    ShowPage(pages[i]);
                    Application.DoEvents();
                    using (var bmp = new Bitmap(Width, Height))
                    {
                        DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                        bmp.Save(Path.Combine(dir, names[i] + ".png"));
                    }
                }
                Close();
            };
        }

        void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 10), ColumnCount = 1, RowCount = 3, BackColor = Theme.Bg };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            Controls.Add(root);

            pageHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            root.Controls.Add(pageHost);

            pageSettings = BuildSettingsPage();
            pageHome = BuildHomePage();
            pageOpenIV = BuildOpenIVPage();
            pageFix = BuildFixPage();
            foreach (var p in new[] { pageHome, pageOpenIV, pageFix, pageSettings })
            {
                p.Dock = DockStyle.Fill;
                p.Visible = false;
                pageHost.Controls.Add(p);
            }
            ShowPage(pageHome);

            // --- Журнал (общий для всех страниц) ---
            var logBox = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 10, 0, 0) };
            logBox.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            logBox.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            logBox.Controls.Add(new Label { Text = "ЖУРНАЛ", AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Small, Margin = new Padding(2, 0, 0, 4) });
            var logFrame = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(10, 8, 4, 8), Margin = new Padding(0) };
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill, ReadOnly = true, ScrollBars = RichTextBoxScrollBars.Vertical,
                BackColor = Theme.Surface, ForeColor = Theme.Muted, BorderStyle = BorderStyle.None, Font = Theme.Mono,
                DetectUrls = false
            };
            logFrame.Controls.Add(txtLog);
            logBox.Controls.Add(logFrame);
            root.Controls.Add(logBox);

            // --- Статусная строка ---
            var status = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0), WrapContents = false };
            sbOpenIV = StatusItem(status); sbFix = StatusItem(status); sbGta = StatusItem(status);
            root.Controls.Add(status);
        }

        Label StatusItem(Control parent)
        {
            var l = new Label { AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Margin = new Padding(0, 0, 22, 0) };
            parent.Controls.Add(l);
            return l;
        }

        void ShowPage(Control page)
        {
            pageHost.SuspendLayout();
            foreach (Control c in pageHost.Controls) c.Visible = c == page;
            pageHost.ResumeLayout();
        }

        // Страница: шапка (кнопка «Назад», заголовок, подзаголовок) + содержимое
        TableLayoutPanel Page(string title, string subtitle, Control body)
        {
            var t = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var head = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 0, 0, 14) };
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var back = new FlatBtn("", Theme.GBack, Theme.SurfaceHi) { Width = 40, Height = 40, Margin = new Padding(0, 4, 0, 0) };
            back.Click += delegate { ShowPage(pageHome); };
            head.Controls.Add(back, 0, 0);
            head.SetRowSpan(back, 2);
            head.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Theme.Fg, Font = Theme.H2, Margin = new Padding(0) }, 1, 0);
            head.Controls.Add(new Label { Text = subtitle, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(2, 0, 0, 0) }, 1, 1);
            t.Controls.Add(head, 0, 0);

            body.Dock = DockStyle.Fill;
            body.Margin = new Padding(0);
            t.Controls.Add(body, 0, 1);
            return t;
        }

        Control BuildHomePage()
        {
            var t = new TableLayoutPanel { ColumnCount = 2, RowCount = 3, Margin = new Padding(0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var head = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0, 0, 0, 16), WrapContents = false };
            head.Controls.Add(new Label { Text = "RPF Studio", AutoSize = true, ForeColor = Theme.Fg, Font = Theme.H1, Margin = new Padding(0) });
            head.Controls.Add(new Label { Text = "Что открыть? OpenIV для правки модов или ArchiveFix для починки архивов.", AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(3, 2, 0, 0) });
            t.Controls.Add(head, 0, 0);
            t.SetColumnSpan(head, 2);

            var cOiv = new Card { Title = "OpenIV", Glyph = Theme.GPackage, Accent = Theme.Blue, Hint = "Открыть", Dock = DockStyle.Fill,
                Desc = "Редактирование архивов .rpf, замена моделей, текстур и звуков" };
            cOiv.Click += delegate { ShowPage(pageOpenIV); };
            var cFix = new Card { Title = "ArchiveFix", Glyph = Theme.GRepair, Accent = Theme.Green, Hint = "Открыть", Dock = DockStyle.Fill,
                Desc = "Исправление .rpf после OpenIV, чтобы моды работали на RP-сервере" };
            cFix.Margin = new Padding(6, 0, 0, 12);
            cOiv.Margin = new Padding(0, 0, 6, 12);
            cFix.Click += delegate { ShowPage(pageFix); };
            t.Controls.Add(cOiv, 0, 1);
            t.Controls.Add(cFix, 1, 1);

            var settingsBtn = new FlatBtn("Настройки путей", Theme.GSettings, Theme.SurfaceHi);
            settingsBtn.Click += delegate { ShowPage(pageSettings); };
            t.Controls.Add(settingsBtn, 0, 2);
            return t;
        }

        // Типы файлов мода, которые принимает список на странице OpenIV
        static readonly Dictionary<string, string> ModTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".oiv", "пакет мода — установится через OpenIV" },
            { ".rpf", "архив — откроется в OpenIV" },
            { ".ytd", "текстуры" },
            { ".yft", "модель машины / объекта" },
            { ".ydr", "модель предмета / оружия" },
            { ".ydd", "модель одежды / персонажа" },
            { ".ycd", "анимации" },
            { ".awc", "звуки" },
            { ".meta", "настройки (.meta)" },
            { ".xml", "настройки (.xml)" },
            { ".dat", "настройки графики / данных" },
            { ".gfx", "интерфейс: HUD, меню, карта" },
            { ".ymt", "данные персонажей / одежды" },
            { ".ytyp", "описания объектов карты" },
            { ".ymap", "объекты на карте" },
            { ".ybn", "коллизии" }
        };

        static bool OpensInOpenIV(string f)
        {
            string ext = Path.GetExtension(f);
            return ext.Equals(".oiv", StringComparison.OrdinalIgnoreCase) || ext.Equals(".rpf", StringComparison.OrdinalIgnoreCase);
        }

        Control BuildOpenIVPage()
        {
            var body = new TableLayoutPanel { ColumnCount = 3, RowCount = 3 };
            for (int i = 0; i < 3; i++) body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            cardLaunch = ActionCard("Запустить OpenIV", "Просто открыть", Theme.GPlay, Theme.Blue, delegate { LaunchOpenIV(false); });
            cardAuto = ActionCard("OpenIV → ArchiveFix", "Потом чинит .rpf", Theme.GSync, Theme.Green, delegate { LaunchOpenIV(true); });
            var cardMods = ActionCard("Папка mods", "Открыть в проводнике", Theme.GFolder, Theme.Yellow, delegate { OpenModsFolder(); });
            cardLaunch.Margin = new Padding(0, 0, 6, 10);
            cardAuto.Margin = new Padding(6, 0, 6, 10);
            cardMods.Margin = new Padding(6, 0, 0, 10);
            body.Controls.Add(cardLaunch, 0, 0);
            body.Controls.Add(cardAuto, 1, 0);
            body.Controls.Add(cardMods, 2, 0);

            // Панель инструментов списка файлов мода
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 4, 0, 4), WrapContents = true };
            tools.Controls.Add(Tool("Файлы", Theme.GAdd, delegate { AddModFilesDialog(); }));
            tools.Controls.Add(Tool("Папка", Theme.GFolder, delegate { AddModFolderDialog(); }));
            btnOpenInOiv = new FlatBtn("Открыть в OpenIV", Theme.GPlay, Theme.Blue) { Height = 34 };
            btnOpenInOiv.Click += delegate { OpenModInOpenIV(); };
            tools.Controls.Add(btnOpenInOiv);
            tools.Controls.Add(Tool("Показать в папке", Theme.GSearch, delegate { ShowModInFolder(); }));
            tools.Controls.Add(Tool("Убрать", Theme.GDelete, delegate { RemoveSelectedMods(); }));
            tools.Controls.Add(Tool("Очистить", Theme.GClear, delegate { lstMods.Items.Clear(); UpdateModsEmpty(); }));
            body.Controls.Add(tools, 0, 1);
            body.SetColumnSpan(tools, 3);

            // Список файлов мода
            var listFrame = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(6), Margin = new Padding(0) };
            lstMods = new ListBox
            {
                Dock = DockStyle.Fill, BackColor = Theme.Surface, ForeColor = Theme.Fg, BorderStyle = BorderStyle.None,
                SelectionMode = SelectionMode.MultiExtended, AllowDrop = true, IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 46
            };
            lstMods.DrawItem += DrawModItem;
            lstMods.DoubleClick += delegate { OpenModInOpenIV(); };
            lstMods.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) RemoveSelectedMods(); };
            lblModsEmpty = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Muted, BackColor = Theme.Surface, AllowDrop = true,
                Text = "Перетащите сюда файлы мода с рабочего стола\n(.oiv, .ytd, .yft, .ydr, .awc, .meta, .rpf …)\nили нажмите «Файлы»"
            };
            foreach (Control c in new Control[] { lstMods, lblModsEmpty })
            {
                c.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
                c.DragDrop += (s, e) => AddModPaths((string[])e.Data.GetData(DataFormats.FileDrop));
            }
            // Перетаскивание из списка наружу — прямо в окно OpenIV
            Point dragStart = Point.Empty;
            lstMods.MouseDown += (s, e) =>
            {
                dragStart = e.Button == MouseButtons.Left && lstMods.IndexFromPoint(e.Location) >= 0 ? e.Location : Point.Empty;
            };
            lstMods.MouseMove += (s, e) =>
            {
                if (dragStart == Point.Empty || e.Button != MouseButtons.Left) return;
                var ds = SystemInformation.DragSize;
                if (new Rectangle(dragStart.X - ds.Width / 2, dragStart.Y - ds.Height / 2, ds.Width, ds.Height).Contains(e.Location)) return;
                dragStart = Point.Empty;
                var files = lstMods.SelectedItems.Cast<string>().Where(File.Exists).ToArray();
                if (files.Length > 0) lstMods.DoDragDrop(new DataObject(DataFormats.FileDrop, files), DragDropEffects.Copy);
            };
            var hint = new Label
            {
                Dock = DockStyle.Bottom, Height = 24, ForeColor = Theme.Muted, Font = Theme.Small, TextAlign = ContentAlignment.MiddleLeft,
                Text = "Совет: перетащите файлы из списка прямо в окно OpenIV. Двойной клик по .oiv или .rpf — открыть в OpenIV."
            };
            listFrame.Controls.Add(lstMods);
            listFrame.Controls.Add(lblModsEmpty);
            listFrame.Controls.Add(hint);
            lblModsEmpty.BringToFront();
            body.Controls.Add(listFrame, 0, 2);
            body.SetColumnSpan(listFrame, 3);

            UpdateModsEmpty();
            return Page("OpenIV", "Редактор архивов GTA V", body);
        }

        void DrawModItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var g = e.Graphics;
            Theme.Smooth(g);
            string path = (string)lstMods.Items[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            var r = e.Bounds;
            using (var bg = new SolidBrush(Theme.Surface)) g.FillRectangle(bg, r);
            if (sel)
                using (var p = Theme.Round(new RectangleF(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4), 8))
                using (var br = new SolidBrush(Theme.Mix(Theme.Blue, Theme.Surface, 0.75f))) g.FillPath(br, p);

            // Плашка с расширением: зелёная — пакет .oiv, синяя — архив .rpf, серая — остальные
            string ext = Path.GetExtension(path).ToLowerInvariant();
            Color badge = ext == ".oiv" ? Theme.Green : ext == ".rpf" ? Theme.Blue : Theme.Border;
            var badgeRect = new RectangleF(r.X + 10, r.Y + (r.Height - 24) / 2f, 46, 24);
            using (var p = Theme.Round(badgeRect, 6))
            using (var br = new SolidBrush(Theme.Mix(badge, Theme.Surface, 0.35f))) g.FillPath(br, p);
            TextRenderer.DrawText(g, ext.TrimStart('.').ToUpperInvariant(), Theme.Small, Rectangle.Round(badgeRect), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            string size;
            if (!fileSize.TryGetValue(path, out size)) size = "—";
            int right = r.Right - 14;
            TextRenderer.DrawText(g, size, Theme.Small, new Rectangle(right - 90, r.Y, 90, r.Height), Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            string desc;
            if (!ModTypes.TryGetValue(ext, out desc)) desc = "файл мода";
            if (!OpensInOpenIV(path)) desc += " · перетащите в OpenIV";
            int tx = r.X + 68, tw = right - 100 - tx;
            var lf = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(g, Path.GetFileName(path), Theme.BodyBold, new Rectangle(tx, r.Y + 4, tw, 20), Theme.Fg, lf);
            TextRenderer.DrawText(g, desc, Theme.Small, new Rectangle(tx, r.Y + 22, tw, 20), Theme.Muted, lf);
        }

        Card ActionCard(string title, string desc, string glyph, Color accent, EventHandler onClick)
        {
            var c = new Card { Title = title, Desc = desc, Glyph = glyph, Accent = accent, Compact = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) };
            c.Click += onClick;
            return c;
        }

        Control BuildFixPage()
        {
            var body = new TableLayoutPanel { ColumnCount = 1, RowCount = 3 };
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Панель инструментов
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 0, 0, 4), WrapContents = true };
            tools.Controls.Add(Tool("Файлы", Theme.GAdd, delegate { AddFilesDialog(); }));
            tools.Controls.Add(Tool("Папка", Theme.GFolder, delegate { AddFolderDialog(); }));
            tools.Controls.Add(Tool("Всё из mods", Theme.GSearch, delegate { AddModsFolder(); }));
            tools.Controls.Add(Tool("Убрать", Theme.GDelete, delegate { RemoveSelected(); }));
            tools.Controls.Add(Tool("Очистить", Theme.GClear, delegate { ClearFiles(); }));
            body.Controls.Add(tools, 0, 0);

            // Список архивов
            var listFrame = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(6), Margin = new Padding(0) };
            lstFiles = new ListBox
            {
                Dock = DockStyle.Fill, BackColor = Theme.Surface, ForeColor = Theme.Fg, BorderStyle = BorderStyle.None,
                SelectionMode = SelectionMode.MultiExtended, AllowDrop = true, IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 46
            };
            lstFiles.DrawItem += DrawFileItem;
            lstFiles.SelectedIndexChanged += delegate { UpdateEmpty(); };
            lblEmpty = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Muted, BackColor = Theme.Surface,
                Text = "Перетащите сюда .rpf файлы или папку\nили нажмите «Файлы» / «Всё из mods»", AllowDrop = true
            };
            foreach (Control c in new Control[] { lstFiles, lblEmpty })
            {
                c.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
                c.DragDrop += (s, e) => AddPaths((string[])e.Data.GetData(DataFormats.FileDrop));
            }
            lstFiles.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };
            listFrame.Controls.Add(lstFiles);
            listFrame.Controls.Add(lblEmpty);
            lblEmpty.BringToFront();
            body.Controls.Add(listFrame, 0, 1);

            // Нижняя панель: бэкап, прогресс, кнопка
            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 2, Margin = new Padding(0, 10, 0, 0) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            chkBackup = new CheckBox { Text = "Делать бэкап (.bak) перед исправлением", Checked = settings.Backup, AutoSize = true, ForeColor = Theme.Fg, Anchor = AnchorStyles.Left, Margin = new Padding(2, 0, 16, 0) };
            lblCount = new Label { AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Small, Anchor = AnchorStyles.Left, Margin = new Padding(2, 4, 0, 0) };
            progress = new ProgressLine { Dock = DockStyle.Fill, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 16, 0) };
            btnFix = new FlatBtn("Исправить архивы", Theme.GRepair, Theme.Green) { Height = 44, Font = Theme.BodyBold, Margin = new Padding(0) };
            btnFix.Width = Math.Max(btnFix.Width, TextRenderer.MeasureText("Идёт исправление…", Theme.BodyBold).Width + 30 + 32) + 30;
            btnFix.Click += delegate { FixListed(); };
            bottom.Controls.Add(chkBackup, 0, 0);
            bottom.Controls.Add(progress, 1, 0);
            bottom.Controls.Add(btnFix, 2, 0);
            bottom.SetRowSpan(btnFix, 2);
            bottom.Controls.Add(lblCount, 0, 1);
            body.Controls.Add(bottom, 0, 2);

            UpdateEmpty();
            return Page("ArchiveFix", "Перешифровка .rpf после правки в OpenIV", body);
        }

        FlatBtn Tool(string text, string glyph, EventHandler onClick)
        {
            var b = new FlatBtn(text, glyph, Theme.SurfaceHi) { Height = 34 };
            b.Click += onClick;
            return b;
        }

        void DrawFileItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var g = e.Graphics;
            Theme.Smooth(g);
            string path = (string)lstFiles.Items[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            var r = e.Bounds;
            using (var bg = new SolidBrush(Theme.Surface)) g.FillRectangle(bg, r);
            var inner = new RectangleF(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
            if (sel)
                using (var p = Theme.Round(inner, 8))
                using (var br = new SolidBrush(Theme.Mix(Theme.Blue, Theme.Surface, 0.75f))) g.FillPath(br, p);

            int st;
            if (!fileState.TryGetValue(path, out st)) st = StPending;
            Color dot = st == StOk ? Theme.Green : st == StFail ? Theme.Red : st == StRunning ? Theme.Yellow : st == StSkip ? Theme.Blue : Theme.Border;
            using (var br = new SolidBrush(dot)) g.FillEllipse(br, r.X + 14, r.Y + r.Height / 2 - 5, 10, 10);

            string size;
            if (!fileSize.TryGetValue(path, out size)) size = "—";
            string state = st == StOk ? "исправлен" : st == StFail ? "ошибка" : st == StRunning ? "в работе…" : st == StSkip ? "не нужно" : "";
            int right = r.Right - 14;
            var rf = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, size, Theme.Small, new Rectangle(right - 90, r.Y + 4, 90, 20), Theme.Muted, rf);
            TextRenderer.DrawText(g, state, Theme.Small, new Rectangle(right - 90, r.Y + 22, 90, 20), dot == Theme.Border ? Theme.Muted : dot, rf);

            int tx = r.X + 36, tw = right - 100 - tx;
            var lf = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, Path.GetFileName(path), Theme.BodyBold, new Rectangle(tx, r.Y + 4, tw, 20), Theme.Fg, lf | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, Path.GetDirectoryName(path), Theme.Small, new Rectangle(tx, r.Y + 22, tw, 20), Theme.Muted, lf | TextFormatFlags.PathEllipsis);
        }

        static string FormatSize(long b)
        {
            if (b >= 1L << 30) return (b / (double)(1L << 30)).ToString("0.0") + " ГБ";
            if (b >= 1L << 20) return (b / (double)(1L << 20)).ToString("0.0") + " МБ";
            return (b / 1024.0).ToString("0") + " КБ";
        }

        Control BuildSettingsPage()
        {
            var body = new TableLayoutPanel { ColumnCount = 1, RowCount = 2 };
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var frame = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(18, 14, 18, 10), AutoSize = true, Margin = new Padding(0) };
            var pt = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, AutoSize = true, BackColor = Theme.Surface };
            pt.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));
            pt.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            pt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pt.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            txtOpenIV = PathRow(pt, "OpenIV.exe", settings.OpenIVPath, true, out stOpenIV);
            txtFix = PathRow(pt, "Папка ArchiveFix", settings.ArchiveFixDir, false, out stFix);
            txtGta = PathRow(pt, "Папка GTA V", settings.GtaDir, false, out stGta);
            frame.Controls.Add(pt);
            body.Controls.Add(frame, 0, 0);

            var detect = new FlatBtn("Найти автоматически", Theme.GSearch, Theme.SurfaceHi) { Margin = new Padding(0, 12, 0, 0) };
            detect.Click += delegate
            {
                string a = Settings.DetectOpenIV(), b = Settings.DetectArchiveFix(), c = Settings.DetectGta();
                if (a != "") txtOpenIV.Text = a;
                if (b != "") txtFix.Text = b;
                if (c != "") txtGta.Text = c;
                Log("Автопоиск: OpenIV " + (a != "" ? "найден" : "не найден") + ", ArchiveFix " + (b != "" ? "найден" : "не найден") + ", GTA V " + (c != "" ? "найдена" : "не найдена") + ".", LogKind.Info);
            };
            body.Controls.Add(detect, 0, 1);
            return Page("Настройки", "Где лежат OpenIV, ArchiveFix и игра", body);
        }

        TextBox PathRow(TableLayoutPanel t, string label, string value, bool isFile, out Label state)
        {
            var st = new Label { AutoSize = true, Font = Theme.Icon, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 0, 0) };
            var l = new Label { Text = label, AutoSize = true, ForeColor = Theme.Fg, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 8, 0) };
            var tb = new TextBox { Text = value, Dock = DockStyle.Fill, BackColor = Theme.SurfaceHi, ForeColor = Theme.Fg, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 9, 8, 9) };
            tb.TextChanged += delegate { CheckPaths(); };
            var b = new FlatBtn("Обзор", "", Theme.SurfaceHi) { Height = 32, Margin = new Padding(0, 4, 0, 4) };
            b.BaseColor = Theme.Border;
            b.Click += delegate
            {
                if (isFile)
                {
                    using (var d = new OpenFileDialog { Filter = "OpenIV|OpenIV.exe|Программы|*.exe" })
                        if (d.ShowDialog(this) == DialogResult.OK) tb.Text = d.FileName;
                }
                else
                {
                    using (var d = new FolderBrowserDialog { SelectedPath = tb.Text })
                        if (d.ShowDialog(this) == DialogResult.OK) tb.Text = d.SelectedPath;
                }
            };
            int row = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(st, 0, row);
            t.Controls.Add(l, 1, row);
            t.Controls.Add(tb, 2, row);
            t.Controls.Add(b, 3, row);
            state = st;
            return tb;
        }

        // ---------- Логика ----------

        string ArchiveFixExe { get { return Path.Combine(txtFix.Text.Trim(), "ArchiveFix.exe"); } }
        string ModsDir { get { return Path.Combine(txtGta.Text.Trim(), "mods"); } }

        void CheckPaths()
        {
            if (sbOpenIV == null || txtOpenIV == null || txtFix == null || txtGta == null) return;
            SetState(sbOpenIV, stOpenIV, "OpenIV", snapMode || File.Exists(txtOpenIV.Text.Trim()));
            SetState(sbFix, stFix, "ArchiveFix", snapMode || File.Exists(ArchiveFixExe));
            SetState(sbGta, stGta, "GTA V", snapMode || Directory.Exists(txtGta.Text.Trim()));
        }

        static void SetState(Label bar, Label icon, string name, bool ok)
        {
            bar.Text = "● " + name + (ok ? "" : " — не найден");
            bar.ForeColor = ok ? Theme.Green : Theme.Red;
            if (icon != null)
            {
                icon.Text = ok ? "\uE73E" : "\uE711"; // галочка / крестик
                icon.ForeColor = ok ? Theme.Green : Theme.Red;
            }
        }

        void SaveSettings()
        {
            settings.OpenIVPath = txtOpenIV.Text.Trim();
            settings.ArchiveFixDir = txtFix.Text.Trim();
            settings.GtaDir = txtGta.Text.Trim();
            settings.Backup = chkBackup.Checked;
            settings.Save();
        }

        // Если вызвано из фонового потока — передаёт вызов в поток окна и возвращает true.
        // После закрытия окна вызов молча отбрасывается, чтобы фоновый поток не уронил программу.
        bool OffUi(Delegate d, params object[] args)
        {
            if (!InvokeRequired) return false;
            try { if (!IsDisposed) BeginInvoke(d, args); }
            catch (InvalidOperationException) { }
            return true;
        }

        void Log(string msg, LogKind kind)
        {
            if (OffUi(new Action<string, LogKind>(Log), msg, kind)) return;
            Color c = kind == LogKind.Ok ? Theme.Green : kind == LogKind.Error ? Theme.Red : kind == LogKind.Step ? Theme.Fg : Theme.Muted;
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionColor = Theme.Mix(Theme.Muted, Theme.Surface, 0.3f);
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  ");
            txtLog.SelectionColor = c;
            txtLog.AppendText(msg + "\n");
            txtLog.ScrollToCaret();
        }

        void SetBusy(bool b)
        {
            if (OffUi(new Action<bool>(SetBusy), b)) return;
            busy = b;
            btnFix.Enabled = cardLaunch.Enabled = cardAuto.Enabled = !b;
            btnFix.Text = b ? "Идёт исправление…" : "Исправить архивы";
        }

        void SetFileState(string f, int st)
        {
            if (OffUi(new Action<string, int>(SetFileState), f, st)) return;
            fileState[f] = st;
            UpdateSize(f);
            lstFiles.Invalidate();
        }

        void SetProgress(float v)
        {
            if (OffUi(new Action<float>(SetProgress), v)) return;
            progress.Value = v;
        }

        void UpdateEmpty()
        {
            lblEmpty.Visible = lstFiles.Items.Count == 0;
            int n = lstFiles.Items.Count;
            lblCount.Text = n == 0 ? "Список пуст" : "Архивов в списке: " + n + (lstFiles.SelectedItems.Count > 0 ? ", выбрано: " + lstFiles.SelectedItems.Count : "");
        }

        void AddPaths(IEnumerable<string> paths)
        {
            int added = 0;
            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    foreach (var f in SafeFindRpf(p)) if (AddFile(f)) added++;
                }
                else if (File.Exists(p) && p.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase))
                {
                    if (AddFile(p)) added++;
                }
            }
            Log("Добавлено архивов: " + added, LogKind.Info);
        }

        bool AddFile(string f)
        {
            foreach (string existing in lstFiles.Items)
                if (string.Equals(existing, f, StringComparison.OrdinalIgnoreCase)) return false;
            lstFiles.Items.Add(f);
            UpdateSize(f);
            UpdateEmpty();
            return true;
        }

        // Размер считается при добавлении и после обработки, а не при каждой перерисовке списка
        void UpdateSize(string f)
        {
            string size = "—";
            try { if (File.Exists(f)) size = FormatSize(new FileInfo(f).Length); } catch { }
            fileSize[f] = size;
        }

        // Флаг шифрования из заголовка RPF7 или null, если файл не архив RPF7
        static uint? ReadRpfEncryption(string f)
        {
            using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var b = new byte[16];
                if (fs.Read(b, 0, 16) < 16 || BitConverter.ToUInt32(b, 0) != 0x52504637) return null;
                return BitConverter.ToUInt32(b, 12);
            }
        }

        static List<string> SafeFindRpf(string dir)
        {
            return SafeFindFiles(dir, "*.rpf");
        }

        // Рекурсивный поиск, который пропускает папки без доступа вместо падения
        static List<string> SafeFindFiles(string dir, string pattern)
        {
            var result = new List<string>();
            var stack = new Stack<string>();
            stack.Push(dir);
            while (stack.Count > 0)
            {
                string d = stack.Pop();
                try
                {
                    result.AddRange(Directory.GetFiles(d, pattern));
                    foreach (var sub in Directory.GetDirectories(d)) stack.Push(sub);
                }
                catch { }
            }
            return result;
        }

        void AddFilesDialog()
        {
            using (var d = new OpenFileDialog { Filter = "RPF архивы|*.rpf", Multiselect = true })
            {
                if (Directory.Exists(ModsDir)) d.InitialDirectory = ModsDir;
                if (d.ShowDialog(this) == DialogResult.OK) AddPaths(d.FileNames);
            }
        }

        void AddFolderDialog()
        {
            using (var d = new FolderBrowserDialog())
                if (d.ShowDialog(this) == DialogResult.OK) AddPaths(new[] { d.SelectedPath });
        }

        void AddModsFolder()
        {
            if (!Directory.Exists(ModsDir)) { Log("Папка mods не найдена: " + ModsDir, LogKind.Error); return; }
            AddPaths(new[] { ModsDir });
        }

        void RemoveSelected()
        {
            if (busy) return;
            var sel = lstFiles.SelectedItems.Cast<object>().ToList();
            foreach (var s in sel) lstFiles.Items.Remove(s);
            UpdateEmpty();
        }

        void ClearFiles()
        {
            if (busy) return;
            lstFiles.Items.Clear();
            fileState.Clear();
            fileSize.Clear();
            progress.Value = 0;
            UpdateEmpty();
        }

        void OpenModsFolder()
        {
            string dir = Directory.Exists(ModsDir) ? ModsDir : txtGta.Text.Trim();
            if (Directory.Exists(dir)) Process.Start("explorer.exe", "\"" + dir + "\"");
            else Log("Папка GTA V не указана — откройте «Настройки путей».", LogKind.Error);
        }

        // ---------- Файлы мода (страница OpenIV) ----------

        void UpdateModsEmpty()
        {
            lblModsEmpty.Visible = lstMods.Items.Count == 0;
        }

        void AddModPaths(IEnumerable<string> paths)
        {
            int added = 0, skipped = 0;
            foreach (var p in paths)
            {
                var files = Directory.Exists(p) ? SafeFindFiles(p, "*") : new List<string> { p };
                foreach (var f in files)
                {
                    if (!File.Exists(f)) continue;
                    if (!ModTypes.ContainsKey(Path.GetExtension(f))) { skipped++; continue; }
                    if (AddModFile(f)) added++;
                }
            }
            Log("Файлов мода добавлено: " + added + (skipped > 0 ? ", пропущено (не файлы мода): " + skipped : ""), LogKind.Info);
        }

        bool AddModFile(string f)
        {
            foreach (string existing in lstMods.Items)
                if (string.Equals(existing, f, StringComparison.OrdinalIgnoreCase)) return false;
            lstMods.Items.Add(f);
            UpdateSize(f);
            UpdateModsEmpty();
            return true;
        }

        void AddModFilesDialog()
        {
            var exts = string.Join(";", ModTypes.Keys.Select(k => "*" + k).ToArray());
            using (var d = new OpenFileDialog { Filter = "Файлы мода|" + exts + "|Все файлы|*.*", Multiselect = true })
            {
                d.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (d.ShowDialog(this) == DialogResult.OK) AddModPaths(d.FileNames);
            }
        }

        void AddModFolderDialog()
        {
            using (var d = new FolderBrowserDialog { Description = "Папка со скачанным модом" })
                if (d.ShowDialog(this) == DialogResult.OK) AddModPaths(new[] { d.SelectedPath });
        }

        void RemoveSelectedMods()
        {
            foreach (var s in lstMods.SelectedItems.Cast<object>().ToList()) lstMods.Items.Remove(s);
            UpdateModsEmpty();
        }

        string SelectedMod()
        {
            if (lstMods.SelectedItems.Count > 0) return (string)lstMods.SelectedItems[0];
            if (lstMods.Items.Count == 1) return (string)lstMods.Items[0];
            return null;
        }

        void OpenModInOpenIV()
        {
            string f = SelectedMod();
            if (f == null) { Log("Выберите файл в списке.", LogKind.Error); return; }
            if (!File.Exists(f)) { Log("Нет файла: " + f, LogKind.Error); return; }
            bool canAutoFix = File.Exists(ArchiveFixExe) && Directory.Exists(txtGta.Text.Trim());
            if (OpensInOpenIV(f))
            {
                LaunchOpenIV(canAutoFix, f);
                return;
            }
            // .ytd, .yft и т.п. OpenIV сам не открывает — их нужно перетащить в нужную папку архива
            if (Process.GetProcessesByName("OpenIV").Length == 0) LaunchOpenIV(canAutoFix);
            Log(Path.GetFileName(f) + ": в OpenIV включите Edit mode, откройте нужную папку архива и перетащите файл из списка в окно OpenIV.", LogKind.Info);
        }

        void ShowModInFolder()
        {
            string f = SelectedMod();
            if (f == null) { Log("Выберите файл в списке.", LogKind.Error); return; }
            if (File.Exists(f)) Process.Start("explorer.exe", "/select,\"" + f + "\"");
            else Log("Нет файла: " + f, LogKind.Error);
        }

        void LaunchOpenIV(bool autoFix)
        {
            LaunchOpenIV(autoFix, null);
        }

        // file — необязательный файл (.oiv, .rpf), который OpenIV откроет при запуске
        void LaunchOpenIV(bool autoFix, string file)
        {
            if (busy) { Log("Подождите — идёт ожидание OpenIV или исправление архивов.", LogKind.Info); return; }
            string exe = txtOpenIV.Text.Trim();
            if (!File.Exists(exe)) { Log("OpenIV.exe не найден. Укажите путь в «Настройках путей».", LogKind.Error); return; }
            if (autoFix && !File.Exists(ArchiveFixExe)) { Log("ArchiveFix.exe не найден — автофикс невозможен.", LogKind.Error); return; }
            if (autoFix && !Directory.Exists(txtGta.Text.Trim())) { Log("Укажите папку GTA V — в ней будут искаться изменённые архивы.", LogKind.Error); return; }
            SaveSettings();

            DateTime start = DateTime.Now.AddSeconds(-2);
            try
            {
                Process.Start(new ProcessStartInfo(exe, file == null ? "" : "\"" + file + "\"") { WorkingDirectory = Path.GetDirectoryName(exe) });
                Log(file == null ? "OpenIV запущен." : "OpenIV запущен с файлом " + Path.GetFileName(file) + ".", LogKind.Step);
            }
            catch (Exception ex) { Log("Не удалось запустить OpenIV: " + ex.Message, LogKind.Error); return; }

            if (!autoFix) return;

            Log("Жду закрытия OpenIV, затем исправлю изменённые .rpf…", LogKind.Info);
            SetBusy(true);
            string gta = txtGta.Text.Trim();
            bool backup = chkBackup.Checked;
            string fixExe = ArchiveFixExe;
            new Thread(() =>
            {
                Thread.Sleep(3000);
                while (Process.GetProcessesByName("OpenIV").Length > 0) Thread.Sleep(1500);
                Log("OpenIV закрыт. Ищу изменённые архивы в " + gta + " …", LogKind.Step);
                var changed = SafeFindRpf(gta).Where(f =>
                {
                    try { return File.GetLastWriteTime(f) >= start; } catch { return false; }
                }).ToList();
                if (changed.Count == 0)
                {
                    Log("Изменённых архивов не найдено.", LogKind.Info);
                    SetBusy(false);
                    return;
                }
                try { Invoke(new Action(() => { foreach (var f in changed) AddFile(f); ShowPage(pageFix); })); }
                catch (InvalidOperationException) { return; } // окно уже закрыто
                RunFix(changed, backup, fixExe);
            }) { IsBackground = true }.Start();
        }

        void FixListed()
        {
            if (busy) return;
            if (!File.Exists(ArchiveFixExe)) { Log("ArchiveFix.exe не найден. Укажите папку в «Настройках путей».", LogKind.Error); return; }
            var files = lstFiles.SelectedItems.Count > 0
                ? lstFiles.SelectedItems.Cast<string>().ToList()
                : lstFiles.Items.Cast<string>().ToList();
            if (files.Count == 0) { Log("Список пуст — добавьте .rpf файлы.", LogKind.Error); return; }
            SaveSettings();
            SetBusy(true);
            bool backup = chkBackup.Checked;
            string fixExe = ArchiveFixExe;
            new Thread(() => RunFix(files, backup, fixExe)) { IsBackground = true }.Start();
        }

        // Выполняется в фоновом потоке. ArchiveFix всегда завершается с кодом 0,
        // поэтому успех определяется по его выводу: "Done." — исправлен, иначе ошибка.
        void RunFix(List<string> files, bool backup, string fixExe)
        {
            int ok = 0, fail = 0, skipped = 0;
            SetProgress(0);
            for (int i = 0; i < files.Count; i++)
            {
                string f = files[i], name = Path.GetFileName(f);
                SetFileState(f, StRunning);
                int result = StFail;
                try
                {
                    uint? enc = File.Exists(f) ? ReadRpfEncryption(f) : null;
                    if (!File.Exists(f)) Log("Нет файла: " + f, LogKind.Error);
                    else if (enc == null) Log(name + ": это не архив RPF7.", LogKind.Error);
                    else if (enc.Value != EncOpen)
                    {
                        // Оригинальный или уже исправленный архив: ArchiveFix на нём выдаёт ошибку
                        Log(name + ": не изменён OpenIV или уже исправлен — пропущен.", LogKind.Info);
                        result = StSkip;
                    }
                    else
                    {
                        if (backup)
                        {
                            File.Copy(f, f + ".bak", true);
                            Log("Бэкап: " + name + ".bak", LogKind.Info);
                        }
                        Log("ArchiveFix → " + f, LogKind.Step);
                        var psi = new ProcessStartInfo(fixExe, "fix \"" + f + "\"")
                        {
                            WorkingDirectory = Path.GetDirectoryName(fixExe),
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            // Ввод перенаправляется и сразу закрывается: без этого ArchiveFix
                            // после работы ждёт нажатия клавиши в невидимой консоли и не завершается
                            RedirectStandardInput = true,
                            // ArchiveFix пишет в консоль в UTF-16
                            StandardOutputEncoding = Encoding.Unicode,
                            StandardErrorEncoding = Encoding.Unicode
                        };
                        string output, error;
                        var outBuf = new StringBuilder();
                        var errBuf = new StringBuilder();
                        using (var p = Process.Start(psi))
                        {
                            p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (outBuf) outBuf.AppendLine(e.Data); };
                            p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (errBuf) errBuf.AppendLine(e.Data); };
                            p.BeginOutputReadLine();
                            p.BeginErrorReadLine();
                            try { p.StandardInput.Close(); } catch { }

                            // Ждём завершения. Если ArchiveFix уже ответил ("Done." / ошибка), но не закрылся
                            // за 3 секунды — завершаем его сами. Общий предел — 30 минут.
                            var startedAt = DateTime.Now;
                            DateTime? answeredAt = null;
                            while (!p.WaitForExit(500))
                            {
                                string so;
                                lock (outBuf) so = outBuf.ToString();
                                if (answeredAt == null && (so.Contains("Done.") || so.Contains("Failed") || so.Contains("Oops")))
                                    answeredAt = DateTime.Now;
                                bool hung = answeredAt != null && (DateTime.Now - answeredAt.Value).TotalSeconds > 3;
                                bool timeout = (DateTime.Now - startedAt).TotalMinutes > 30;
                                if (hung || timeout)
                                {
                                    if (timeout) Log(name + ": ArchiveFix работает больше 30 минут — остановлен.", LogKind.Error);
                                    try { p.Kill(); } catch { }
                                    break;
                                }
                            }
                            p.WaitForExit(); // дочитать вывод
                        }
                        lock (outBuf) output = outBuf.ToString();
                        lock (errBuf) error = errBuf.ToString();
                        if (output.TrimStart().StartsWith("Done.", StringComparison.Ordinal))
                        {
                            Log(name + ": исправлен.", LogKind.Ok);
                            result = StOk;
                        }
                        else
                        {
                            string first = (output + "\n" + error).Trim().Split('\n')[0].Trim();
                            Log(name + ": ArchiveFix сообщил об ошибке — " + (first == "" ? "пустой ответ" : first), LogKind.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log(name + ": " + ex.Message, LogKind.Error);
                }
                if (result == StOk) ok++; else if (result == StSkip) skipped++; else fail++;
                SetFileState(f, result);
                SetProgress((i + 1) / (float)files.Count);
            }
            Log("Готово. Исправлено: " + ok + ", пропущено: " + skipped + ", ошибок: " + fail + ".", fail == 0 ? LogKind.Ok : LogKind.Error);
            SetBusy(false);
        }
    }
}
