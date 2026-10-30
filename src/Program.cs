using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CampusConnect
{
    static class Program
    {
        internal static readonly uint ActivateMessage = RegisterWindowMessage("CampusConnect.Activate.V1");
        delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder text, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string message);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr window);
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetProp(IntPtr window, string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SetProp(IntPtr window, string name, IntPtr value);
        // Tagged per version so a newer build never hijacks an older one's window.
        internal static void MarkMainWindow(IntPtr handle) { SetProp(handle, "CampusConnect.MainWindow." + BuildInfo.Version, new IntPtr(1)); }

        static bool ActivateExistingInstance()
        {
            using (var current = Process.GetCurrentProcess())
            {
                foreach (var process in Process.GetProcessesByName(current.ProcessName))
                using (process)
                {
                    if (process.Id == current.Id || process.SessionId != current.SessionId) continue;
                    IntPtr found = IntPtr.Zero;
                    EnumWindows((window, parameter) =>
                    {
                        uint owner;
                        GetWindowThreadProcessId(window, out owner);
                        if (owner != process.Id) return true;
                        var title = new StringBuilder(256);
                        var windowClass = new StringBuilder(256);
                        GetWindowText(window, title, title.Capacity);
                        GetClassName(window, windowClass, windowClass.Capacity);
                        // EnumWindows includes hidden windows; MainWindowHandle does not.
                        if (title.ToString() == "校园网一键连接" && windowClass.ToString().StartsWith("WindowsForms10.Window") && GetProp(window, "CampusConnect.MainWindow." + BuildInfo.Version) != IntPtr.Zero)
                        { found = window; return false; }
                        return true;
                    }, IntPtr.Zero);
                    if (found == IntPtr.Zero) continue;
                    AllowSetForegroundWindow((uint)process.Id);
                    PostMessage(found, ActivateMessage, IntPtr.Zero, IntPtr.Zero);
                    ShowWindow(found, 9); // SW_RESTORE also recovers a hidden older version.
                    BringWindowToTop(found);
                    SetForegroundWindow(found);
                    return true;
                }
            }
            return false;
        }
        [STAThread]
        static void Main(string[] args)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) => MessageBox.Show("程序遇到问题，请重新打开。\n" + e.Exception.GetType().Name, "校园网一键连接", MessageBoxButtons.OK, MessageBoxIcon.Error);
            string screenshot = null;
            bool preview = false;
            bool compact = false;
            bool maximized = false;
            bool darkPreview = false;
            bool autostart = false;
            foreach (var arg in args)
            {
                if (arg == "--install-shortcut") { CreateShortcut(); return; }
                if (arg == "--make-icon") { MakeIcon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "campus.ico")); return; }
                if (arg == "--preview" || arg == "--settings") preview = true;
                if (arg == "--compact") compact = true;
                if (arg == "--maximized") maximized = true;
                if (arg == "--dark-preview") darkPreview = true;
                if (arg == "--autostart") autostart = true;
                if (arg.StartsWith("--screenshot=")) { screenshot = arg.Substring(13); preview = true; }
            }
            if (!String.IsNullOrEmpty(screenshot))
            {
                // Visual QA must be independent from a user's running instance.
                var capture = new MainForm(true, screenshot, false, darkPreview ? (bool?)true : null);
                if (compact) capture.Size = new Size(1060, 740);
                if (maximized) capture.WindowState = FormWindowState.Maximized;
                Application.Run(capture);
                return;
            }
            bool created;
            using (var mutex = new Mutex(true, "Local\\CampusConnect.App." + BuildInfo.Version, out created))
            {
                if (!created)
                {
                    // A second launch can arrive while the first is constructing its form.
                    for (int attempt = 0; attempt < 25; attempt++)
                    {
                        if (ActivateExistingInstance()) return;
                        Thread.Sleep(100);
                    }
                    MessageBox.Show("已有程序正在启动，请稍后再点击桌面图标。", "校园网一键连接");
                    return;
                }
                Application.Run(new MainForm(preview, screenshot, autostart));
            }
        }

        public static string CreateShortcut()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktop, "校园网一键连接.lnk");
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = null;
            try
            {
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                string executable = Application.ExecutablePath;
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { executable });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(executable) });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "连接校园 Wi-Fi 并自动完成深澜校园网认证" });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { executable + ",0" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
            return shortcutPath;
        }

        public static void MakeIcon(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };
            byte[][] frames = new byte[sizes.Length][];
            using (var resource = typeof(Program).Assembly.GetManifestResourceStream("CampusConnect.app-icon.png"))
            using (var source = Image.FromStream(resource))
            {
                for (int i = 0; i < sizes.Length; i++)
                using (var bitmap = new Bitmap(sizes[i], sizes[i], System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (var graphics = Graphics.FromImage(bitmap))
                using (var png = new MemoryStream())
                {
                    graphics.Clear(Color.Transparent);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    float scale = Math.Min((float)sizes[i] / source.Width, (float)sizes[i] / source.Height);
                    float width = source.Width * scale, height = source.Height * scale;
                    using (var attributes = new System.Drawing.Imaging.ImageAttributes())
                    {
                        attributes.SetWrapMode(WrapMode.TileFlipXY);
                        graphics.DrawImage(source, new Rectangle((int)Math.Round((sizes[i] - width) / 2), (int)Math.Round((sizes[i] - height) / 2), (int)Math.Round(width), (int)Math.Round(height)), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                    }
                    // The supplied picture has opaque white corners. Apply an alpha
                    // mask at each output size so the desktop shows through them.
                    float radius = sizes[i] * 0.26F;
                    for (int y = 0; y < sizes[i]; y++)
                    for (int x = 0; x < sizes[i]; x++)
                    {
                        float dx = Math.Max(0, Math.Max(radius - (x + .5F), x + .5F - (sizes[i] - radius)));
                        float dy = Math.Max(0, Math.Max(radius - (y + .5F), y + .5F - (sizes[i] - radius)));
                        if (dx == 0 || dy == 0) continue;
                        float coverage = Math.Max(0, Math.Min(1, radius + .5F - (float)Math.Sqrt(dx * dx + dy * dy)));
                        Color pixel = bitmap.GetPixel(x, y);
                        bitmap.SetPixel(x, y, Color.FromArgb((int)Math.Round(pixel.A * coverage), pixel.R, pixel.G, pixel.B));
                    }
                    bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                    frames[i] = png.ToArray();
                }
            }
            // Windows Vista and newer accept lossless PNG frames in an ICO container.
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
                uint offset = (uint)(6 + 16 * sizes.Length);
                for (int i = 0; i < sizes.Length; i++)
                {
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((byte)0); writer.Write((byte)0);
                    writer.Write((ushort)1); writer.Write((ushort)32);
                    writer.Write((uint)frames[i].Length); writer.Write(offset);
                    offset += (uint)frames[i].Length;
                }
                foreach (var frame in frames) writer.Write(frame);
            }
        }
    }
}
