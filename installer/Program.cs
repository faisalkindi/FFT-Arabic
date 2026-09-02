using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FftArabic
{
    static class Program
    {
        const string AppId = "1004640";                 // FINAL FANTASY TACTICS - The Ivalice Chronicles
        const string BackupSuffix = ".arabic_backup";
        static readonly string[] GameProcesses = { "FFT_enhanced", "FFT_classic" };

        // payload-relative path (forward slashes, as stored in the zip)  ->  same relative path in the game folder
        static readonly string[] PayloadFiles =
        {
            "dinput8.dll",
            "data/enhanced/0004.en.pac",
            "data/enhanced/0007.pac",
            "data/enhanced/0008.pac",
        };

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(true);
            Application.Run(new MainForm());
        }

        // ---- Steam game-folder detection -------------------------------------

        public static string DetectGamePath()
        {
            try
            {
                string steam = GetSteamPath();
                if (steam == null) return null;
                string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                var libs = new System.Collections.Generic.List<string> { steam };
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                        libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));

                foreach (string lib in libs)
                {
                    string acf = Path.Combine(lib, "steamapps", "appmanifest_" + AppId + ".acf");
                    if (!File.Exists(acf)) continue;
                    var im = Regex.Match(File.ReadAllText(acf), "\"installdir\"\\s*\"([^\"]+)\"");
                    if (!im.Success) continue;
                    string game = Path.Combine(lib, "steamapps", "common", im.Groups[1].Value);
                    if (IsValidGameFolder(game)) return game;
                }
            }
            catch { }
            return null;
        }

        static string GetSteamPath()
        {
            try
            {
                object p = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
                if (p is string s1 && Directory.Exists(s1)) return s1.Replace('/', '\\');
            }
            catch { }
            try
            {
                object p = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null);
                if (p is string s2 && Directory.Exists(s2)) return s2;
            }
            catch { }
            return null;
        }

        // ---- path helpers -----------------------------------------------------

        public static string MarkerPac(string root) => Path.Combine(root, "data", "enhanced", "0008.pac");
        public static bool IsValidGameFolder(string root) =>
            !string.IsNullOrEmpty(root) && File.Exists(MarkerPac(root));
        public static bool IsInstalled(string root) =>
            !string.IsNullOrEmpty(root) && File.Exists(MarkerPac(root) + BackupSuffix);

        // ---- install / uninstall ---------------------------------------------

        public static void Install(string root, Action<string> progress)
        {
            EnsureGameClosed();
            progress("جارٍ تحضير ملفات التعريب…");
            var asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream("payload.zip"))
            {
                if (s == null) throw new Exception("ملفات التعريب المضمّنة غير موجودة داخل المثبّت.");
                using (var z = new ZipArchive(s, ZipArchiveMode.Read))
                {
                    foreach (string rel in PayloadFiles)
                    {
                        var entry = z.GetEntry(rel);
                        if (entry == null) throw new Exception("ملف مفقود من الحزمة: " + rel);
                        string dest = Path.Combine(root, rel.Replace('/', '\\'));
                        string bak = dest + BackupSuffix;

                        progress("جارٍ تثبيت " + Path.GetFileName(dest) +
                                 (Path.GetFileName(dest) == "0008.pac" ? "…  (قد يستغرق لحظات)" : "…"));

                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        if (File.Exists(dest) && !File.Exists(bak))
                            File.Copy(dest, bak);          // back up the user's original once
                        entry.ExtractToFile(dest, true);   // stream straight into place
                    }
                }
            }
            progress("تم التثبيت بنجاح ✔");
        }

        public static void Uninstall(string root, Action<string> progress)
        {
            EnsureGameClosed();
            progress("جارٍ استعادة الملفات الأصلية…");
            foreach (string rel in PayloadFiles)
            {
                string dest = Path.Combine(root, rel.Replace('/', '\\'));
                string bak = dest + BackupSuffix;
                if (File.Exists(bak))
                {
                    File.Copy(bak, dest, true);
                    File.Delete(bak);
                }
                else if (File.Exists(dest))
                {
                    // no backup means we added this file (e.g. dinput8.dll on a clean game)
                    File.Delete(dest);
                }
            }
            progress("تمت الإزالة ✔");
        }

        static void EnsureGameClosed()
        {
            foreach (string n in GameProcesses)
                try { if (Process.GetProcessesByName(n).Length > 0)
                        throw new Exception("اللعبة قيد التشغيل. الرجاء إغلاقها تمامًا ثم إعادة المحاولة."); }
                catch (Exception ex) when (!(ex.Message.Contains("قيد التشغيل"))) { }
        }
    }

    // ===================== modern UI =====================

    static class Ui
    {
        public static readonly Color Bg = Color.FromArgb(30, 26, 23);
        public static readonly Color Card = Color.FromArgb(44, 38, 33);
        public static readonly Color Gold = Color.FromArgb(206, 167, 92);
        public static readonly Color GoldHover = Color.FromArgb(224, 187, 112);
        public static readonly Color Red = Color.FromArgb(168, 70, 58);
        public static readonly Color RedHover = Color.FromArgb(192, 86, 72);
        public static readonly Color Ink = Color.FromArgb(34, 28, 23);
        public static readonly Color Text = Color.FromArgb(236, 228, 214);
        public static readonly Color Muted = Color.FromArgb(150, 140, 128);

        static PrivateFontCollection _pfc;
        public static FontFamily Family;

        public static void LoadFont()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("ui_font.ttf"))
                {
                    byte[] data = new byte[s.Length];
                    s.Read(data, 0, data.Length);
                    IntPtr ptr = Marshal.AllocCoTaskMem(data.Length);
                    Marshal.Copy(data, 0, ptr, data.Length);
                    _pfc = new PrivateFontCollection();
                    _pfc.AddMemoryFont(ptr, data.Length);
                    Marshal.FreeCoTaskMem(ptr);
                    Family = _pfc.Families[0];
                }
            }
            catch { Family = new FontFamily("Tahoma"); }
        }

        public static Font F(float size, FontStyle style = FontStyle.Regular)
            => new Font(Family, size, style, GraphicsUnit.Point);

        public static Image LoadLogo()
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ui_logo.png"))
                using (var img = Image.FromStream(s))
                    return new Bitmap(img);
            }
            catch { return null; }
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    public class RoundButton : Button
    {
        public Color Base = Ui.Gold;
        public Color Hover = Ui.GoldHover;
        public Color Fg = Ui.Ink;
        public int Radius = 14;
        bool _hover;

        public RoundButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = false; Invalidate(); };
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = !Enabled ? Color.FromArgb(70, 64, 58) : (_hover ? Hover : Base);
            using (var path = Ui.Round(rect, Radius))
            using (var b = new SolidBrush(fill))
                g.FillPath(b, path);
            var sf = new StringFormat(StringFormatFlags.DirectionRightToLeft)
            { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var tb = new SolidBrush(Enabled ? Fg : Color.FromArgb(140, 130, 120)))
                g.DrawString(Text, Font, tb, rect, sf);
        }
    }

    public class MainForm : Form
    {
        string gamePath;
        Label lblStatus, lblPath;
        RoundButton btnInstall, btnUninstall;
        LinkLabel btnBrowse;
        bool busy;

        public MainForm()
        {
            Ui.LoadFont();

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 716);
            BackColor = Ui.Bg;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Font = Ui.F(11f);
            Region = new Region(Ui.Round(new Rectangle(0, 0, Width, Height), 18));
            Text = "تعريب فاينل فانتازي تاكتيكس";

            MouseDown += DragStart;

            var close = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Ui.Muted,
                AutoSize = false,
                Size = new Size(34, 30),
                Location = new Point(12, 12),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            close.Click += (s, e) => Close();
            close.MouseEnter += (s, e) => { close.ForeColor = Ui.Red; };
            close.MouseLeave += (s, e) => { close.ForeColor = Ui.Muted; };
            Controls.Add(close);

            var logo = new PictureBox
            {
                Image = Ui.LoadLogo(),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Size = new Size(440, 220),
                Location = new Point((ClientSize.Width - 440) / 2, 44)
            };
            logo.MouseDown += DragStart;
            Controls.Add(logo);

            var subtitle = new Label
            {
                Text = "التعريب العربي الكامل",
                Font = Ui.F(15f, FontStyle.Bold),
                ForeColor = Ui.Gold,
                AutoSize = false,
                UseCompatibleTextRendering = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width, 34),
                Location = new Point(0, 270)
            };
            subtitle.MouseDown += DragStart;
            Controls.Add(subtitle);

            var card = new RoundPanel
            {
                Size = new Size(440, 78),
                Location = new Point((ClientSize.Width - 440) / 2, 322),
                Fill = Ui.Card
            };
            lblPath = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 8, 14, 8),
                ForeColor = Ui.Text,
                Font = Ui.F(9.5f),
                UseCompatibleTextRendering = true,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            card.Controls.Add(lblPath);
            Controls.Add(card);

            btnInstall = new RoundButton
            {
                Text = "تثبيت اللغة العربية",
                Font = Ui.F(14f, FontStyle.Bold),
                Size = new Size(440, 62),
                Location = new Point((ClientSize.Width - 440) / 2, 420),
                Base = Ui.Gold, Hover = Ui.GoldHover, Fg = Ui.Ink
            };
            btnInstall.Click += OnInstall;
            Controls.Add(btnInstall);

            btnUninstall = new RoundButton
            {
                Text = "إزالة اللغة العربية",
                Font = Ui.F(12.5f, FontStyle.Bold),
                Size = new Size(440, 50),
                Location = new Point((ClientSize.Width - 440) / 2, 494),
                Base = Ui.Red, Hover = Ui.RedHover, Fg = Ui.Text
            };
            btnUninstall.Click += OnUninstall;
            Controls.Add(btnUninstall);

            lblStatus = new Label
            {
                AutoSize = false,
                Font = Ui.F(10f),
                UseCompatibleTextRendering = true,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Ui.Muted,
                Size = new Size(ClientSize.Width, 30),
                Location = new Point(0, 602)
            };
            lblStatus.MouseDown += DragStart;
            Controls.Add(lblStatus);

            btnBrowse = new LinkLabel
            {
                Text = "تحديد مجلد اللعبة يدويًا",
                AutoSize = false,
                Font = Ui.F(9f),
                LinkColor = Ui.Muted,
                ActiveLinkColor = Ui.Gold,
                LinkBehavior = LinkBehavior.HoverUnderline,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width, 24),
                Location = new Point(0, 642),
                BackColor = Color.Transparent
            };
            btnBrowse.Click += OnBrowse;
            Controls.Add(btnBrowse);

            var footer = new Label
            {
                Text = "تعريب وإعداد:  Kindiboy",
                Font = Ui.F(9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(170, 140, 92),
                AutoSize = false,
                UseCompatibleTextRendering = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width, 22),
                Location = new Point(0, 682)
            };
            footer.MouseDown += DragStart;
            Controls.Add(footer);

            gamePath = Program.DetectGamePath();
            RefreshState();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(60, Ui.Gold), 1))
            using (var path = Ui.Round(new Rectangle(0, 0, Width - 1, Height - 1), 18))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            }
        }

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int wp, int lp);
        void DragStart(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); }
        }

        void RefreshState()
        {
            if (Program.IsValidGameFolder(gamePath))
            {
                bool installed = Program.IsInstalled(gamePath);
                lblPath.ForeColor = Ui.Text;
                lblPath.Text = "تم العثور على اللعبة" + Environment.NewLine + Trim(gamePath);
                btnInstall.Enabled = !busy;
                btnUninstall.Enabled = !busy && installed;
                if (installed) SetStatus("✔ اللغة العربية مُثبّتة حاليًا", Ui.Gold);
                else SetStatus("اللغة العربية غير مُثبّتة", Ui.Muted);
            }
            else
            {
                lblPath.ForeColor = Ui.Red;
                lblPath.Text = "لم يتم العثور على اللعبة" + Environment.NewLine + "الرجاء تحديد المجلد يدويًا";
                btnInstall.Enabled = false;
                btnUninstall.Enabled = false;
                SetStatus("في انتظار تحديد مجلد اللعبة", Ui.Muted);
            }
            btnInstall.Invalidate();
            btnUninstall.Invalidate();
        }

        static string Trim(string p)
        {
            if (p != null && p.Length > 52) return "…" + p.Substring(p.Length - 50);
            return p;
        }

        void SetStatus(string text, Color color) { lblStatus.Text = text; lblStatus.ForeColor = color; }

        void Progress(string text)
        {
            if (InvokeRequired) BeginInvoke(new Action(() => SetStatus(text, Color.FromArgb(120, 170, 220))));
            else SetStatus(text, Color.FromArgb(120, 170, 220));
        }

        void SetBusy(bool b) { busy = b; Cursor = b ? Cursors.WaitCursor : Cursors.Default; RefreshState(); }

        void OnBrowse(object sender, EventArgs e)
        {
            if (busy) return;
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "اختر مجلد اللعبة (الذي يحتوي على FFT_enhanced.exe)";
                dlg.UseDescriptionForTitle = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string chosen = dlg.SelectedPath;
                    if (!Program.IsValidGameFolder(chosen))
                    {
                        string sub = Path.Combine(chosen, "FINAL FANTASY TACTICS - The Ivalice Chronicles");
                        if (Program.IsValidGameFolder(sub)) chosen = sub;
                    }
                    if (Program.IsValidGameFolder(chosen)) gamePath = chosen;
                    else MessageBox.Show(this, "هذا المجلد لا يحتوي على ملفات اللعبة (data\\enhanced\\0008.pac).",
                        "مجلد غير صالح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RefreshState();
                }
            }
        }

        void OnInstall(object sender, EventArgs e)
        {
            if (busy) return;
            if (MessageBox.Show(this,
                    "سيتم تثبيت التعريب العربي (استبدال ملفات اللغة الإنجليزية مع الاحتفاظ بنسخة احتياطية).\n" +
                    "تأكد من إغلاق اللعبة وتوفّر ~500 ميجابايت مساحة فارغة.\n\nالمتابعة؟",
                    "تثبيت", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;

            SetBusy(true);
            string root = gamePath;
            var t = new Thread(() =>
            {
                try
                {
                    Program.Install(root, Progress);
                    BeginInvoke(new Action(() =>
                    {
                        SetBusy(false);
                        MessageBox.Show(this,
                            "تم تثبيت التعريب بنجاح!\n\n" +
                            "شغّل «FINAL FANTASY TACTICS – Enhanced».\n" +
                            "يظهر التعريب على اللغة الإنجليزية (English) — اخترها من إعدادات اللغة إن لزم.",
                            "تم التثبيت", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() =>
                    {
                        SetBusy(false);
                        MessageBox.Show(this, ex.Message, "خطأ في التثبيت", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            }) { IsBackground = true };
            t.Start();
        }

        void OnUninstall(object sender, EventArgs e)
        {
            if (busy) return;
            SetBusy(true);
            string root = gamePath;
            var t = new Thread(() =>
            {
                try
                {
                    Program.Uninstall(root, Progress);
                    BeginInvoke(new Action(() =>
                    {
                        SetBusy(false);
                        MessageBox.Show(this, "تمت إزالة التعريب. عادت اللعبة إلى ملفاتها الأصلية.",
                            "تمت الإزالة", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() =>
                    {
                        SetBusy(false);
                        MessageBox.Show(this, ex.Message, "خطأ في الإزالة", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            }) { IsBackground = true };
            t.Start();
        }
    }

    public class RoundPanel : Panel
    {
        public Color Fill = Ui.Card;
        public int Radius = 12;
        public RoundPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.Round(r, Radius))
            using (var b = new SolidBrush(Fill))
                g.FillPath(b, path);
        }
    }
}
