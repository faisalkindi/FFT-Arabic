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
        public const string Version = "1.0.0";
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

    // ===================== modern UI (same visual language as the other Kindiboy installers) =====================

    static class Ui
    {
        public static readonly Color Bg = Color.FromArgb(22, 17, 13);
        public static readonly Color Card = Color.FromArgb(44, 36, 29);
        public static readonly Color Cyan = Color.FromArgb(214, 176, 98);   // Ivalice gold
        public static readonly Color CyanHover = Color.FromArgb(232, 196, 122);
        public static readonly Color Red = Color.FromArgb(190, 54, 44);
        public static readonly Color RedHover = Color.FromArgb(214, 76, 62);
        public static readonly Color Ink = Color.FromArgb(30, 22, 14);
        public static readonly Color Text = Color.FromArgb(238, 230, 214);
        public static readonly Color Muted = Color.FromArgb(158, 144, 122);

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

        public static Image LoadBackground()
        {
            try
            {
                using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("ui_bg.jpg"))
                using (var img = Image.FromStream(st))
                    return new Bitmap(img);
            }
            catch { return null; }
        }

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
        public Color Base = Ui.Cyan;
        public Color Hover = Ui.CyanHover;
        public Color Fg = Ui.Ink;
        public int Radius = 14;
        public Color Outline = Color.Empty;
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
            Color fill = !Enabled ? Color.FromArgb(64, 54, 44) : (_hover ? Hover : Base);
            using (var path = Ui.Round(rect, Radius))
            using (var b = new SolidBrush(fill))
            {
                g.FillPath(b, path);
                if (Outline != Color.Empty) using (var pen = new Pen(Outline, 1f)) g.DrawPath(pen, path);
            }
            var sf = new StringFormat(StringFormatFlags.DirectionRightToLeft)
            { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var tb = new SolidBrush(Enabled ? Fg : Color.FromArgb(140, 128, 110)))
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

            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 780);
            BackColor = Ui.Bg;
            BackgroundImage = Ui.LoadBackground();
            BackgroundImageLayout = ImageLayout.Stretch;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Font = Ui.F(11f);
            Text = "تعريب فاينل فانتازي تاكتيكس v" + Program.Version;
            MouseDown += DragStart;

            var close = new Label
            {
                Text = "✕", Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Ui.Muted,
                AutoSize = false, Size = new Size(34, 30), Location = new Point(14, 14),
                TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand, BackColor = Color.Transparent
            };
            close.Click += (s, e) => Close();
            close.MouseEnter += (s, e) => { close.ForeColor = Ui.Red; };
            close.MouseLeave += (s, e) => { close.ForeColor = Ui.Muted; };
            Controls.Add(close);

            var ver = new Label
            {
                Text = "v" + Program.Version, Font = new Font("Segoe UI", 9f), ForeColor = Ui.Muted,
                AutoSize = false, Size = new Size(80, 30), Location = new Point(ClientSize.Width - 92, 14),
                TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No, BackColor = Color.Transparent
            };
            ver.MouseDown += DragStart;
            Controls.Add(ver);

            var logo = new PictureBox
            {
                Image = Ui.LoadLogo(), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent,
                Size = new Size(440, 176), Location = new Point((ClientSize.Width - 440) / 2, 52)
            };
            logo.MouseDown += DragStart;
            Controls.Add(logo);

            var subtitle = new Label
            {
                Text = "التعريب الكامل", Font = Ui.F(18f, FontStyle.Bold), ForeColor = Ui.Cyan,
                AutoSize = false, UseCompatibleTextRendering = true, TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width, 58), Location = new Point(0, 236), BackColor = Color.Transparent
            };
            subtitle.MouseDown += DragStart; Controls.Add(subtitle);

            var tagline = new Label
            {
                Text = "ترجمة كاملة للحوارات والقوائم والموسوعة · خط عربي",
                Font = Ui.F(9f), ForeColor = Ui.Muted,
                AutoSize = false, UseCompatibleTextRendering = true, TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width, 30), Location = new Point(0, 294), BackColor = Color.Transparent
            };
            tagline.MouseDown += DragStart; Controls.Add(tagline);

            var card = new RoundPanel
            {
                Size = new Size(480, 86), Location = new Point((ClientSize.Width - 480) / 2, 334),
                Fill = Color.FromArgb(205, Ui.Card), Border = Color.FromArgb(60, Ui.Cyan)
            };
            lblPath = new Label
            {
                AutoSize = false, Dock = DockStyle.Fill, Padding = new Padding(6, 4, 6, 4), ForeColor = Ui.Text,
                Font = Ui.F(8.5f), UseCompatibleTextRendering = true, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent
            };
            card.Controls.Add(lblPath); Controls.Add(card);

            btnInstall = new RoundButton
            {
                Text = "تثبيت اللغة العربية", Font = Ui.F(15f, FontStyle.Bold), Size = new Size(480, 64),
                Location = new Point((ClientSize.Width - 480) / 2, 440), Base = Ui.Cyan, Hover = Ui.CyanHover, Fg = Ui.Ink, Radius = 14
            };
            btnInstall.Click += OnInstall; Controls.Add(btnInstall);

            btnUninstall = new RoundButton
            {
                Text = "إزالة اللغة العربية", Font = Ui.F(12f, FontStyle.Bold), Size = new Size(480, 52),
                Location = new Point((ClientSize.Width - 480) / 2, 514), Base = Color.FromArgb(46, 38, 30), Hover = Color.FromArgb(66, 54, 42),
                Fg = Ui.Cyan, Outline = Color.FromArgb(150, Ui.Cyan), Radius = 14
            };
            btnUninstall.Click += OnUninstall; Controls.Add(btnUninstall);

            lblStatus = new Label
            {
                AutoSize = false, Font = Ui.F(10f), UseCompatibleTextRendering = true, TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Ui.Muted, Size = new Size(ClientSize.Width, 32), Location = new Point(0, 578), BackColor = Color.Transparent
            };
            lblStatus.MouseDown += DragStart; Controls.Add(lblStatus);

            btnBrowse = new LinkLabel
            {
                Text = "تحديد مجلد اللعبة يدويًا", AutoSize = false, Font = Ui.F(9f), LinkColor = Ui.Muted,
                ActiveLinkColor = Ui.Cyan, LinkBehavior = LinkBehavior.HoverUnderline, TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width, 28), Location = new Point(0, 610), BackColor = Color.Transparent
            };
            btnBrowse.Click += OnBrowse; Controls.Add(btnBrowse);

            var kofi = new RoundButton
            {
                Text = "أعجبك التعريب؟ ادعمني على Ko-fi", Font = Ui.F(10.5f, FontStyle.Bold), Size = new Size(440, 46),
                Location = new Point((ClientSize.Width - 440) / 2, 676), Base = Color.FromArgb(40, 33, 26), Hover = Color.FromArgb(58, 48, 38),
                Fg = Ui.Text, Outline = Color.FromArgb(120, Ui.Cyan), Radius = 14
            };
            kofi.Click += (s, e) => { try { Process.Start(new ProcessStartInfo("https://ko-fi.com/kindiboy") { UseShellExecute = true }); } catch { } };
            Controls.Add(kofi);

            var footer = new Label
            {
                Text = "تعريب وإعداد:  Kindiboy", Font = Ui.F(9.5f, FontStyle.Bold), ForeColor = Ui.Cyan,
                AutoSize = false, UseCompatibleTextRendering = true, TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width, 28), Location = new Point(0, 736), BackColor = Color.Transparent
            };
            footer.MouseDown += DragStart;
            Controls.Add(footer);

            gamePath = Program.DetectGamePath();
            RefreshState();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            // recompute after DPI auto-scaling so the rounded corners follow the real size
            Region = new Region(Ui.Round(new Rectangle(0, 0, Width, Height), (int)(20 * DeviceDpi / 96f)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // subtle gold border
            using (var pen = new Pen(Color.FromArgb(70, Ui.Cyan), 1))
            using (var path = Ui.Round(new Rectangle(0, 0, Width - 1, Height - 1), (int)(20 * DeviceDpi / 96f)))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            }
        }

        // ---- drag-to-move (borderless) ----
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int wp, int lp);
        void DragStart(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
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
                if (installed) SetStatus("✔ اللغة العربية مُثبّتة حاليًا", Ui.Cyan);
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
            if (p != null && p.Length > 30) p = "…" + p.Substring(p.Length - 28);
            return p == null ? null : "\u202A" + p + "\u202C";
        }

        void SetStatus(string text, Color color)
        {
            lblStatus.Text = text;
            lblStatus.ForeColor = color;
        }

        void Progress(string text)
        {
            if (InvokeRequired) BeginInvoke(new Action(() => SetStatus(text, Ui.Cyan)));
            else SetStatus(text, Ui.Cyan);
        }

        void SetBusy(bool b)
        {
            busy = b;
            Cursor = b ? Cursors.WaitCursor : Cursors.Default;
            RefreshState();
        }

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
                    "سيتم تثبيت التعريب (استبدال ملفات اللغة الإنجليزية مع الاحتفاظ بنسخة احتياطية).\n" +
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
        public Color Border = Color.Empty;
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
            {
                g.FillPath(b, path);
                if (Border != Color.Empty) using (var pen = new Pen(Border, 1f)) g.DrawPath(pen, path);
            }
        }
    }
}
