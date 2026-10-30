using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CampusConnect
{
    public sealed partial class MainForm : Form
    {
        static Color Teal { get { return Theme.Green; } }
        static Color Ink { get { return Theme.Ink; } }
        static Color Muted { get { return Theme.Muted; } }
        readonly TextBox ssid = new TextBox(), url = new TextBox(), username = new TextBox(), password = new TextBox();
        readonly CheckBox auto = new CheckBox(), startWithWindows = new CheckBox();
        readonly Label status = new Label(), detail = new Label(), stepWifi = new Label(), stepPortal = new Label(), stepOnline = new Label();
        readonly TimelineLog log = new TimelineLog();
        readonly ActionButton connect = new ActionButton(), cancel = new ActionButton(), save = new ActionButton();
        readonly PageDeck tabs = new PageDeck();
        readonly Panel browserTab = new Panel { Text = "认证页面" };
        readonly Panel logTab = new Panel { Text = "运行记录", BackColor = Color.White };
        readonly Panel nearbyTab = new Panel { Text = "附近 Wi-Fi", BackColor = Color.White };
        readonly ImageList nearbyImages = new ImageList { ImageSize = new Size(16, 48), ColorDepth = ColorDepth.Depth32Bit };
        ListView nearbyList;
        Label nearbyState;
        ActionButton nearbyChoose, nearbyRefresh;
        bool nearbyScanning, nearbyLoaded;
        string pendingProfile;
        readonly Panel fields = new Panel();
        Settings settings;
        WebView2 browser;
        NotifyIcon trayIcon;
        readonly System.Windows.Forms.Timer monitorTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        Task browserInitialization;
        string browserDirectory;
        CancellationTokenSource operation;
        bool busy, closing;
        bool portalAuthenticated;
        bool statusHasError;
        bool monitorArmed, monitorChecking, settingsOpen;
        int failedProbes;
        NetworkScope lastProbeScope = NetworkScope.Unknown;
        readonly bool preview;
        readonly bool autostart;
        readonly string screenshot;
        readonly Dictionary<Control, Color[]> originalColors = new Dictionary<Control, Color[]>();
        readonly Dictionary<IconView, Color> originalIconColors = new Dictionary<IconView, Color>();

        // DWMWA_WINDOW_CORNER_PREFERENCE / DWMWCP_ROUND（Windows 11 起支持）
        const int WindowCornerPreference = 33;
        const int RoundCorner = 2;

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        // Let the window manager draw the rounded frame: it antialiases the corners and
        // clips the client area itself, which a window region cannot do as smoothly.
        void ApplyRoundedCorners()
        {
            int preference = RoundCorner;
            try { DwmSetWindowAttribute(Handle, WindowCornerPreference, ref preference, 4); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Program.MarkMainWindow(Handle);
            ApplyRoundedCorners();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == Program.ActivateMessage)
            {
                RestoreFromTray();
                return;
            }
            base.WndProc(ref message);
            if (message.Msg == 0x84 && message.Result == new IntPtr(1) && WindowState != FormWindowState.Maximized)
            {
                var point = PointToClient(new Point((short)((long)message.LParam & 0xffff), (short)(((long)message.LParam >> 16) & 0xffff)));
                int border = 7;
                if (point.Y < border) message.Result = new IntPtr(point.X < border ? 13 : point.X >= Width - border ? 14 : 12);
                else if (point.Y >= Height - border) message.Result = new IntPtr(point.X < border ? 16 : point.X >= Width - border ? 17 : 15);
                else if (point.X < border) message.Result = new IntPtr(10);
                else if (point.X >= Width - border) message.Result = new IntPtr(11);
            }
        }

        public MainForm(bool preview, string screenshot, bool autostart = false, bool? appearancePreview = null)
        {
            this.preview = preview;
            this.autostart = autostart;
            this.screenshot = screenshot;
            Text = "校园网一键连接";
            var work = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(1420, work.Width - 40), Math.Min(830, work.Height - 36));
            MinimumSize = new Size(1060, 740);
            FormBorderStyle = FormBorderStyle.None;
            MaximizedBounds = work;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            Font = Theme.Font();
            ForeColor = Ink;
            BackColor = Theme.Canvas;
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "campus.ico");
            if (File.Exists(iconPath)) Icon = new Icon(iconPath);
            BuildUi();
            InitializeTray();
            try { settings = String.IsNullOrEmpty(screenshot) ? SettingsStore.Load() : Settings.CreateScreenshotExample(); }
            catch (Exception ex)
            {
                settings = new Settings();
                SetStatus("请重新配置", "原配置无法读取或密码无法解密。原文件会保留，保存后才会更新。", false);
                WriteLog("配置读取失败（" + ex.GetType().Name + "），请重新输入账号和密码。");
            }
            ApplySettings();
            ApplyAppearance(appearancePreview ?? (settings.Appearance == "dark"));
            monitorTimer.Tick += async (sender, e) => await MonitorAsync();
            if (!preview) monitorTimer.Start();
            Shown += OnShown;
            FormClosing += (sender, e) =>
            {
                closing = true;
                monitorTimer.Stop(); monitorTimer.Dispose();
                if (operation != null) operation.Cancel();
                if (browser != null) browser.Dispose();
                if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
                nearbyImages.Dispose();
            };
        }

        void InitializeTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("打开窗口", null, (sender, e) => RestoreFromTray());
            menu.Items.Add("打开认证页面", null, (sender, e) => { RestoreFromTray(); tabs.SelectedTab = browserTab; });
            menu.Items.Add("退出", null, (sender, e) => Close());
            trayIcon = new NotifyIcon { Icon = Icon ?? SystemIcons.Application, Text = "校园网一键连接", ContextMenuStrip = menu };
            trayIcon.DoubleClick += (sender, e) => RestoreFromTray();
            trayIcon.BalloonTipClicked += (sender, e) => { RestoreFromTray(); tabs.SelectedTab = browserTab; };
        }

        void MinimizeToTray()
        {
            trayIcon.Visible = true;
            Hide();
        }

        void RestoreFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            if (trayIcon != null) trayIcon.Visible = false;
            BringToFront();
            Activate();
        }

        Label LabelText(string value, float size = 10F, bool bold = false)
        {
            return new Label { Text = value, AutoSize = false, Dock = DockStyle.Fill,
                Font = Theme.Font(size * 1.4F, bold),
                TextAlign = ContentAlignment.MiddleLeft, ForeColor = Ink, Margin = Padding.Empty };
        }
        void StyleButton(Button button, string text, bool primary = false)
        {
            button.Text = text;
            button.AutoSize = false;
            button.Height = 40;
            button.Width = primary ? 162 : 112;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = primary ? Teal : Color.FromArgb(216, 225, 227);
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.BackColor = primary ? Teal : Color.White;
            button.ForeColor = primary ? Color.White : Ink;
            button.Cursor = Cursors.Hand;
            button.Margin = new Padding(8, 0, 0, 0);
        }
        static void AddRow(TableLayoutPanel panel, Control control, int height)
        {
            int row = panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            panel.Controls.Add(control, 0, row);
        }
        void AddField(TableLayoutPanel panel, string caption, TextBox box)
        {
            AddRow(panel, LabelText(caption, 11.5F, true), 34);
            var holder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 7, 10, 5), BackColor = Color.FromArgb(246, 249, 250), Margin = new Padding(0, 0, 0, 2) };
            holder.Paint += (sender, e) => { using (var pen = new Pen(Color.FromArgb(221, 230, 232))) e.Graphics.DrawRectangle(pen, 0, 0, holder.Width - 1, holder.Height - 1); };
            box.BorderStyle = BorderStyle.None;
            box.BackColor = holder.BackColor;
            box.Dock = DockStyle.Fill;
            box.Font = new Font(Font.FontFamily, 12);
            holder.Controls.Add(box);
            AddRow(panel, holder, 40);
        }
        static void InitStep(Label label, string text)
        {
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Muted;
            label.Margin = Padding.Empty;
        }
        void ApplySettings()
        {
            ssid.Text = settings.Ssid;
            url.Text = settings.PortalUrl;
            username.Text = settings.Username;
            password.Text = settings.Password;
            auto.Checked = settings.AutoConnect;
            try { startWithWindows.Checked = String.IsNullOrEmpty(screenshot) && Startup.IsEnabled(); }
            catch (Exception ex) { WriteLog("无法读取开机自启动设置（" + ex.GetType().Name + "）。"); }
            accountValue.Text = settings.HasCredentials ? "已填写" : "未认证";
            recentValue.Text = String.IsNullOrEmpty(settings.LastConnected) ? "--" : settings.LastConnected.Replace(" ", "\n");
            networkValue.Text = String.IsNullOrEmpty(settings.Ssid) ? "当前网络" : "校园 Wi-Fi";
            if (status.Text == "等待连接") SetStatus(settings.HasCredentials ? "已配置完成" : "等待配置", settings.HasCredentials ? "点击“连接网络”即可连接校园网。" : "填写左侧配置，即可一键连接校园网。", false);
        }
        void SaveCurrent(bool connecting)
        {
            var updated = new Settings { Ssid = ssid.Text.Trim(), PortalUrl = url.Text.Trim(), Username = username.Text.Trim(), Password = password.Text,
                AutoConnect = auto.Checked, LastConnected = settings.LastConnected, ProfileName = pendingProfile ?? settings.ProfileName, UsernameSelector = settings.UsernameSelector,
                PasswordSelector = settings.PasswordSelector, LoginSelector = settings.LoginSelector, Appearance = settings.Appearance,
                MonitorConnection = settings.MonitorConnection, Provider = settings.Provider };
            Validation.Check(updated, connecting);
            SettingsStore.Save(updated);
            settings = updated;
            pendingProfile = null;
            accountValue.Text = settings.HasCredentials ? "已填写" : "未认证";
            networkValue.Text = String.IsNullOrEmpty(settings.Ssid) ? "当前网络" : "校园 Wi-Fi";
        }
        async void OnShown(object sender, EventArgs e)
        {
            WriteLog(settings.HasCredentials ? "工具已就绪。配置已载入，可以连接校园网。" : "工具已就绪。首次使用请填写 SSID、账号和密码。");
            if (!String.IsNullOrEmpty(screenshot))
            {
                await Task.Delay(500);
                using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(screenshot); }
                Close();
                return;
            }
            if (LaunchPolicy.ShouldConnect(preview, autostart, settings.AutoConnect, settings.HasCredentials))
            {
                // Launched at logon, the WLAN stack is often still coming up.
                await Task.Delay(autostart ? 12000 : 700);
                if (!closing) await ConnectAsync(autostart);
            }
        }
        void SetStatus(string title, string description, bool error)
        {
            if (closing) return;
            status.Text = title;
            detail.Text = description;
            statusHasError = error;
            if (trayIcon != null)
            {
                trayIcon.Text = "校园网一键连接 - " + title;
                trayIcon.Icon = error || title == "需要页面操作" ? SystemIcons.Warning : title == "已连接，可以上网" ? SystemIcons.Information : Icon ?? SystemIcons.Application;
            }
            status.ForeColor = error ? (Theme.Dark ? Color.FromArgb(255, 155, 130) : Color.FromArgb(181, 78, 49)) : Ink;
            badgeLabel.Text = error ? "●  请检查" : busy ? "●  连接中" : "●  已就绪";
            badgeLabel.ForeColor = error ? status.ForeColor : Teal;
            if (error) connectionValue.Text = "未完成";
            else if (title == "正在连接") connectionValue.Text = "连接中";
            else if (title == "正在认证") { connectionValue.Text = "Wi-Fi 就绪"; accountValue.Text = "认证中"; }
            else if (title == "已取消") { connectionValue.Text = "已取消"; accountValue.Text = "未确认"; }
        }
        string Redact(string value)
        {
            if (settings != null && !String.IsNullOrEmpty(settings.Password)) value = value.Replace(settings.Password, "••••");
            if (settings != null && !String.IsNullOrEmpty(settings.Username)) value = value.Replace(settings.Username, "[账号]");
            return value;
        }
        void WriteLog(string message)
        {
            if (closing) return;
            string safeMessage = Redact(message);
            log.Add(DateTime.Now.ToString("HH:mm:ss"), safeMessage);
            if (!String.IsNullOrEmpty(screenshot)) return;
            try { LogStore.Append(safeMessage); }
            catch (Exception) { /* Logging must not interrupt connection. */ }
        }

        void ApplyAppearance(bool dark)
        {
            Theme.Dark = dark;
            ApplyColors(this, dark);
            Invalidate(true);
        }

        void ApplyColors(Control control, bool dark)
        {
            Color[] colors;
            var card = control as Card;
            if (!originalColors.TryGetValue(control, out colors))
            {
                colors = new[] { control.BackColor, control.ForeColor, card == null ? Color.Empty : card.Fill, card == null ? Color.Empty : card.Border };
                originalColors[control] = colors;
            }
            control.BackColor = dark ? DarkBackground(colors[0]) : colors[0];
            control.ForeColor = dark ? DarkText(colors[1]) : colors[1];
            if (card != null)
            {
                card.Fill = dark ? DarkBackground(colors[2]) : colors[2];
                card.Border = dark ? Theme.Line : colors[3];
            }
            var icon = control as IconView;
            if (icon != null && !icon.Disc)
            {
                Color original;
                if (!originalIconColors.TryGetValue(icon, out original)) { original = icon.IconColor; originalIconColors[icon] = original; }
                icon.IconColor = dark ? DarkText(original) : original;
            }
            foreach (Control child in control.Controls) ApplyColors(child, dark);
            control.Invalidate();
        }
        static Color DarkBackground(Color color)
        {
            if (color == Color.Transparent || color.IsEmpty || color.A == 0) return color;
            if (color.ToArgb() == Theme.Canvas.ToArgb() || color.ToArgb() == Theme.Surface.ToArgb() || color.ToArgb() == Theme.Elevated.ToArgb() || color.ToArgb() == Theme.Inset.ToArgb()) return color;
            if (color.ToArgb() == Color.FromArgb(244, 248, 251).ToArgb()) return Theme.Canvas;
            if (color.ToArgb() == Color.FromArgb(248, 250, 254).ToArgb() || color.ToArgb() == Color.FromArgb(246, 249, 250).ToArgb()) return Theme.Inset;
            if (color.ToArgb() == Color.FromArgb(250, 252, 255).ToArgb() || color.ToArgb() == Color.FromArgb(243, 251, 249).ToArgb()) return Theme.Elevated;
            if (color.G > color.R + 5 && color.GetBrightness() > 0.8F) return Color.FromArgb(40, 61, 56);
            return color.GetBrightness() > 0.8F ? Theme.Surface : Theme.Canvas;
        }
        static Color DarkText(Color color)
        {
            if (color == Color.Transparent || color.IsEmpty || color.A == 0) return color;
            if (color.ToArgb() == Theme.Ink.ToArgb() || color.ToArgb() == Theme.Muted.ToArgb() || color.ToArgb() == Theme.Green.ToArgb() || color.ToArgb() == Color.White.ToArgb()) return color;
            if (color.R > color.G * 1.3 && color.R > color.B * 1.3) return Color.FromArgb(255, 155, 130);
            if (color.G > color.R * 1.2 && color.G > color.B * 0.95) return Theme.Green;
            return color.GetBrightness() > 0.55F ? Theme.Muted : Theme.Ink;
        }
        void ShowFailure(Exception exception)
        {
            if (closing) return;
            string message = exception is ArgumentException || exception is InvalidOperationException || exception is TimeoutException
                ? exception.Message : "操作失败（" + exception.GetType().Name + "）。请检查配置、网络或本地文件权限后重试。";
            message = Redact(message);
            SetStatus("连接未完成", message, true);
            WriteLog(message);
            NotifyAttention("连接未完成，请打开程序查看原因。");
        }
        void NotifyAttention(string message)
        {
            if (trayIcon != null && trayIcon.Visible)
            {
                trayIcon.BalloonTipTitle = "校园网一键连接";
                trayIcon.BalloonTipText = message;
                trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
                trayIcon.ShowBalloonTip(5000);
            }
        }

        async Task MonitorAsync()
        {
            if (closing || busy || monitorChecking || settingsOpen || !monitorArmed || !settings.MonitorConnection || String.IsNullOrWhiteSpace(settings.Ssid)) return;
            monitorChecking = true;
            try
            {
                var scope = NetworkContext.GetScope(settings.Ssid);
                if (scope == NetworkScope.Other || scope == NetworkScope.Unknown) { failedProbes = 0; lastProbeScope = scope; return; }
                if (lastProbeScope != scope) failedProbes = 0;
                lastProbeScope = scope;
                bool online = scope == NetworkScope.Campus && await Internet.CheckAsync(CancellationToken.None);
                if (closing || busy || settingsOpen || !monitorArmed || !settings.MonitorConnection) return;
                var currentScope = NetworkContext.GetScope(settings.Ssid);
                if (currentScope != scope) { failedProbes = 0; lastProbeScope = currentScope; return; }
                if (online) { failedProbes = 0; return; }
                failedProbes++;
                if (!MonitorPolicy.ShouldReconnect(settings.MonitorConnection, monitorArmed, currentScope, failedProbes)) return;
                failedProbes = 0;
                monitorArmed = false;
                WriteLog("检测到网络连续不可用，开始自动重连。");
                await ConnectAsync(true, true);
            }
            catch (Exception ex) { if (!closing) WriteLog("网络监测失败（" + ex.GetType().Name + "）。"); }
            finally { monitorChecking = false; }
        }
        bool MonitorNetworkAllowed()
        {
            var scope = NetworkContext.GetScope(settings.Ssid);
            return scope == NetworkScope.Campus || scope == NetworkScope.Disconnected;
        }
        async Task EnsureBrowserAsync()
        {
            if (browserInitialization != null) { await browserInitialization; return; }
            browserInitialization = InitializeBrowserAsync();
            try { await browserInitialization; }
            catch { browserInitialization = null; throw; }
        }
        async Task InitializeBrowserAsync()
        {
            try { CoreWebView2Environment.GetAvailableBrowserVersionString(); }
            catch (WebView2RuntimeNotFoundException)
            {
                throw new InvalidOperationException("缺少 Microsoft Edge WebView2 运行时。请安装微软 WebView2 Evergreen Runtime 后重新打开程序。下载地址见使用说明。");
            }
            if (browser != null) { browser.Dispose(); browserTab.Controls.Clear(); }
            browser = new WebView2 { Dock = DockStyle.Fill };
            browserDirectory = Path.Combine(SettingsStore.DirectoryPath, "Browser");
            browserTab.Controls.Clear();
            browserTab.Controls.Add(browser);
            if (Theme.Dark) ApplyColors(browserTab, true);
            var environment = await CoreWebView2Environment.CreateAsync(null, browserDirectory);
            var options = environment.CreateCoreWebView2ControllerOptions();
            options.IsInPrivateModeEnabled = true;
            await browser.EnsureCoreWebView2Async(environment, options);
            browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            browser.CoreWebView2.PermissionRequested += (sender, e) => e.State = CoreWebView2PermissionState.Deny;
            browser.CoreWebView2.NewWindowRequested += (sender, e) =>
            {
                e.Handled = true;
                Uri target;
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out target) && (target.Scheme == "http" || target.Scheme == "https"))
                    browser.CoreWebView2.Navigate(target.AbsoluteUri);
            };
            browser.CoreWebView2.DownloadStarting += (sender, e) => e.Cancel = true;
        }
        async Task NavigateAsync(string target, CancellationToken token)
        {
            var completion = new TaskCompletionSource<bool>();
            EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = (sender, e) =>
            {
                if (e.IsSuccess) completion.TrySetResult(true);
                else completion.TrySetException(new InvalidOperationException("无法打开校园网认证页（" + e.WebErrorStatus + "）。请检查是否连接校园网络及认证网址是否正确。"));
            };
            browser.CoreWebView2.NavigationCompleted += handler;
            try
            {
                browser.CoreWebView2.Navigate(target);
                var delay = Task.Delay(25000, token);
                if (await Task.WhenAny(completion.Task, delay) != completion.Task)
                {
                    browser.CoreWebView2.Stop();
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException("认证页面打开超时。请确认已连接校园 Wi-Fi，然后重试。");
                }
                await completion.Task;
                token.ThrowIfCancellationRequested();
            }
            finally { if (!closing && browser.CoreWebView2 != null) browser.CoreWebView2.NavigationCompleted -= handler; }
        }
        async Task<PortalReport> InspectAsync(Uri origin, bool submit)
        {
            if (!Validation.SameOrigin(origin, browser.CoreWebView2.Source))
                return new PortalReport { State = "redirect", Message = "认证页跳转到了其他地址，自动填写已暂停。请在右侧确认页面，并将认证网址更新为学校实际使用的地址。" };
            string result = await browser.ExecuteScriptAsync(PortalScript.Build(settings, submit));
            if (result == "null" || result == "undefined") return new PortalReport { State = "waiting", Message = "等待认证页面加载。" };
            return Json.Read<PortalReport>(result);
        }
        // 一次连接尝试的结果：只有 Transient 值得重试。
        enum ConnectResult { Online, Transient, NeedsManual }

        // 认证页明确报错（密码错误、账号停机等），重试只会加重风控，必须停。
        sealed class PortalRejectedException : InvalidOperationException
        {
            public PortalRejectedException(string message) : base(message) { }
        }

        async Task ConnectAsync(bool retryOnStartup = false, bool monitorRetry = false)
        {
            if (busy || closing) return;
            monitorArmed = false;
            try { if (monitorRetry) Validation.Check(settings, true); else SaveCurrent(true); }
            catch (Exception ex) { ShowFailure(ex); return; }
            busy = true;
            if (nearbyChoose != null) nearbyChoose.Enabled = false;
            portalAuthenticated = false;
            fields.Enabled = false;
            connect.Enabled = save.Enabled = navSettings.Enabled = false;
            cancel.Visible = true;
            stepWifi.ForeColor = stepPortal.ForeColor = stepOnline.ForeColor = Muted;
            operation = new CancellationTokenSource();
            var token = operation.Token;
            try
            {
                // 开机自启时 Wi-Fi 往往还没就绪，一直重试到连通；手动点击只尝试一次，
                // 这样失败原因能立刻看到，也不会在用户看不到的时候反复联网。
                if (retryOnStartup) await ConnectUntilOnlineAsync(token, monitorRetry);
                else await ConnectOnceAsync(token);
            }
            catch (OperationCanceledException)
            {
                if (!closing && browser != null && browser.CoreWebView2 != null) browser.CoreWebView2.Stop();
                SetStatus("已取消", "连接流程已停止。已连接的 Wi-Fi 会保留。", false);
                WriteLog("用户已取消连接。");
            }
            catch (NetworkChangedException)
            {
                monitorArmed = settings.MonitorConnection;
                failedProbes = 0;
                SetStatus("已暂停重连", "检测到其他网络连接或网络状态不明，已停止本轮重连。", false);
                WriteLog("网络状态发生变化，已停止本轮重连。");
            }
            catch (Exception ex) { ShowFailure(ex); }
            finally
            {
                operation.Dispose();
                operation = null;
                busy = false;
                if (!closing) { fields.Enabled = true; connect.Enabled = save.Enabled = navSettings.Enabled = true; cancel.Visible = false; connect.Text = "连接网络"; badgeLabel.Text = statusHasError ? "●  请检查" : "●  已就绪"; if (nearbyChoose != null) nearbyChoose.Enabled = !nearbyScanning && nearbyList.SelectedItems.Count == 1; }
            }
        }
        // Wi-Fi 就绪通常在一分钟内，给足余量但不要永远空转：不在校园网时（例如在家）
        // 无限重试只会让无线网卡每隔一分钟白白拨一次。超过上限就交给用户手动重试。
        async Task ConnectUntilOnlineAsync(CancellationToken token, bool monitorRetry)
        {
            var started = DateTime.UtcNow;
            int attempt = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (monitorRetry && !MonitorNetworkAllowed()) throw new NetworkChangedException();
                attempt++;
                try
                {
                    var result = await ConnectOnceAsync(token, monitorRetry);
                    if (result == ConnectResult.Online) return;
                    if (result == ConnectResult.NeedsManual)
                    {
                        WriteLog("认证页面需要人工操作，已停止自动重试。");
                        return;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (PortalRejectedException) { throw; }
                catch (NetworkChangedException) { throw; }
                // Wi-Fi 配置被删掉了（忘记网络），重试不会自愈，直接停下来说清楚。
                catch (Wifi.WifiProfileMissingException) { throw; }
                catch (ArgumentException) { throw; }
                catch (Exception ex)
                {
                    WriteLog("第 " + attempt + " 次尝试未成功：" + Redact(ex.Message));
                }
                if (RetryPolicy.Expired(DateTime.UtcNow - started))
                {
                    SetStatus("已停止重试", "连续重试 30 分钟仍未连通。可能不在校园网范围内，稍后点击“连接网络”即可重新尝试。", true);
                    WriteLog("重试已达 30 分钟上限，已停止自动重试。");
                    return;
                }
                bool online = await Internet.CheckAsync(token);
                if (monitorRetry && !MonitorNetworkAllowed()) throw new NetworkChangedException();
                if (online) { MarkOnline("网络已在后台连通。"); return; }
                int delay = RetryPolicy.DelaySeconds(attempt);
                SetStatus("等待重试", "第 " + (attempt + 1) + " 次尝试将在 " + delay + " 秒后开始，已持续 " + (int)(DateTime.UtcNow - started).TotalMinutes + " 分钟…", false);
                WriteLog("第 " + (attempt + 1) + " 次尝试将在 " + delay + " 秒后开始。");
                await Task.Delay(TimeSpan.FromSeconds(delay), token);
            }
        }
        async Task<ConnectResult> ConnectOnceAsync(CancellationToken token, bool monitorRetry = false)
        {
            if (monitorRetry && !MonitorNetworkAllowed()) throw new NetworkChangedException();
            SetStatus("正在连接", "正在连接校园 Wi-Fi，请稍候…", false);
            WriteLog(String.IsNullOrEmpty(settings.Ssid) ? "使用当前网络。" : "正在连接指定 Wi-Fi。");
            stepWifi.ForeColor = Teal;
            await Wifi.ConnectAsync(settings.Ssid, settings.ProfileName, token, monitorRetry ? (Func<bool>)MonitorNetworkAllowed : null);
            token.ThrowIfCancellationRequested();
            WriteLog("Wi-Fi 连接步骤完成，检查当前网络。");
            bool online = await Internet.CheckAsync(token);
            if (monitorRetry && !MonitorNetworkAllowed()) throw new NetworkChangedException();
            if (online) { MarkOnline("当前网络已可访问互联网，无需重复认证。"); return ConnectResult.Online; }
            SetStatus("正在认证", "正在打开学校认证页面并填写账号密码…", false);
            stepPortal.ForeColor = Teal;
            await EnsureBrowserAsync();
            token.ThrowIfCancellationRequested();
            await NavigateAsync(settings.PortalUrl, token);
            WriteLog("认证页面已打开。");
            // 认证页加载完就切过去，连接过程中能直接看到真实的登录页面；
            // 成功后 MarkOnline 会切回运行记录。
            tabs.SelectedTab = browserTab;
            var origin = Validation.Portal(settings.PortalUrl);
            bool submitted = false, manual = false;
            string lastState = "";
            var deadline = DateTime.UtcNow.AddSeconds(65);
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                if (monitorRetry && !MonitorNetworkAllowed()) throw new NetworkChangedException();
                if (!String.IsNullOrWhiteSpace(settings.Ssid) && !Wifi.ConnectedSsids().Contains(settings.Ssid))
                    throw new InvalidOperationException("指定的校园 Wi-Fi 已断开。请重新连接后再试。");
                var report = await InspectAsync(origin, !submitted && !manual);
                if (report.State != lastState)
                {
                    WriteLog(report.Message ?? "等待认证结果。");
                    lastState = report.State;
                }
                if (report.State == "error") throw new PortalRejectedException(Redact(report.Message));
                if (report.State == "submitted") { submitted = true; stepOnline.ForeColor = Teal; }
                if (report.State == "manual" || report.State == "redirect" || (report.State == "missing" && DateTime.UtcNow > deadline.AddSeconds(-50)))
                {
                    if (!manual) NotifyAttention("认证页面需要操作，请打开程序查看。");
                    manual = true;
                    tabs.SelectedTab = browserTab;
                    SetStatus("需要页面操作", report.Message, false);
                }
                if (await Internet.CheckAsync(token)) { portalAuthenticated = submitted || report.State == "portal-online"; MarkOnline("网络连通检查通过，可以开始上网了。"); return ConnectResult.Online; }
                if (report.State == "portal-online")
                {
                    SetStatus("账号已在线", "认证页显示已在线，正在检查互联网访问。", false);
                    stepOnline.ForeColor = Teal;
                }
                await Task.Delay(1500, token);
            }
            tabs.SelectedTab = browserTab;
            SetStatus("尚未确认联网", "未能通过网络连通检查。请查看认证页是否需要选择运营商、填写验证码或显示错误；完成后可点击重试。", true);
            WriteLog("认证等待结束，未能确认互联网可用。网络检测服务不可达时，也可能出现此状态。");
            return manual ? ConnectResult.NeedsManual : ConnectResult.Transient;
        }
        void MarkOnline(string description)
        {
            SetStatus("已连接，可以上网", description, false);
            connectionValue.Text = "已联网";
            accountValue.Text = portalAuthenticated ? "已认证" : "无需认证";
            settings.LastConnected = DateTime.Now.ToString("MM-dd HH:mm:ss");
            recentValue.Text = settings.LastConnected.Replace(" ", "\n");
            try { SettingsStore.Save(settings); } catch (Exception) { WriteLog("网络已连接，但最近连接时间未能保存。"); }
            stepWifi.ForeColor = stepPortal.ForeColor = stepOnline.ForeColor = Teal;
            monitorArmed = settings.MonitorConnection && !String.IsNullOrWhiteSpace(settings.Ssid);
            failedProbes = 0;
            WriteLog(description);
            tabs.SelectedTab = logTab;
        }
        void BuildNearbyPage()
        {
            nearbyImages.Images.Add(new Bitmap(16, 48));
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.White };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            nearbyTab.Controls.Add(root);
            nearbyState = UiLabel("选择搜索附近网络", 14);
            nearbyState.Dock = DockStyle.Fill; nearbyState.ForeColor = Muted; root.Controls.Add(nearbyState, 0, 0);
            nearbyList = new ListView { Dock = DockStyle.Fill, View = View.Details, HeaderStyle = ColumnHeaderStyle.None, FullRowSelect = true, MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.None, Font = Theme.Font(15), OwnerDraw = true, SmallImageList = nearbyImages, BackColor = Color.White };
            nearbyList.Columns.Add("网络", 400);
            nearbyList.Resize += (sender, e) => nearbyList.Columns[0].Width = Math.Max(100, nearbyList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            nearbyList.DrawColumnHeader += (sender, e) => e.DrawDefault = true;
            nearbyList.DrawSubItem += (sender, e) =>
            {
                var network = (Wifi.NearbyNetwork)e.Item.Tag;
                var bounds = e.Bounds;
                var fill = e.Item.Selected ? Theme.Selected : Theme.Surface;
                using (var brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, bounds);
                using (var pen = new Pen(Theme.Line)) e.Graphics.DrawLine(pen, bounds.Left + 8, bounds.Bottom - 1, bounds.Right - 8, bounds.Bottom - 1);
                Icons.Draw(e.Graphics, "wifi", new RectangleF(bounds.Left + 8, bounds.Top + 13, 22, 22), Theme.Green);
                var nameBounds = new Rectangle(bounds.Left + 38, bounds.Top + 4, Math.Max(30, bounds.Width - 180), bounds.Height - 8);
                string statusText = network.Connected ? "已连接" : !String.IsNullOrEmpty(network.Profile) ? "已保存" : network.Secured ? "需要系统配置" : "未保存";
                using (var nameFont = Theme.Font(15, true))
                using (var statusFont = Theme.Font(13))
                using (var signalFont = Theme.Font(13, true))
                {
                    TextRenderer.DrawText(e.Graphics, network.Ssid, nameFont, nameBounds, Theme.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                    TextRenderer.DrawText(e.Graphics, statusText, statusFont, new Rectangle(bounds.Right - 138, bounds.Top, 90, bounds.Height), Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine);
                    TextRenderer.DrawText(e.Graphics, network.Signal + "%", signalFont, new Rectangle(bounds.Right - 47, bounds.Top, 43, bounds.Height), Theme.Green, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine);
                }
                if (e.Item.Focused && nearbyList.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -2, -2));
            };
            root.Controls.Add(nearbyList, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 5, 0, 0) };
            nearbyChoose = new ActionButton { Text = "使用此网络", Primary = true, Size = new Size(130, 38), Enabled = false, Margin = Padding.Empty };
            nearbyRefresh = new ActionButton { Text = "重新搜索", IconName = "wifi", Size = new Size(120, 38), Margin = new Padding(0, 0, 10, 0) };
            actions.Controls.Add(nearbyChoose); actions.Controls.Add(nearbyRefresh); root.Controls.Add(actions, 0, 2);
            nearbyList.SelectedIndexChanged += (sender, e) => nearbyChoose.Enabled = !busy && !nearbyScanning && nearbyList.SelectedItems.Count == 1;
            Action select = () =>
            {
                if (busy || nearbyScanning || nearbyList.SelectedItems.Count != 1) return;
                var network = (Wifi.NearbyNetwork)nearbyList.SelectedItems[0].Tag;
                ssid.Text = network.Ssid;
                pendingProfile = network.Profile ?? "";
                nearbyState.Text = "已选择 " + network.Ssid;
            };
            nearbyChoose.Click += (sender, e) => select();
            nearbyList.DoubleClick += (sender, e) => select();
            nearbyRefresh.Click += async (sender, e) => await ScanNearbyAsync();
        }

        async Task ScanNearbyAsync()
        {
            if (nearbyScanning || closing) return;
            nearbyScanning = true; nearbyLoaded = true;
            nearbyRefresh.Enabled = false; nearbyChoose.Enabled = false; nearbyList.Items.Clear(); nearbyState.Text = "正在搜索附近网络…";
            try
            {
                var networks = await Task.Run(() => Wifi.ScanNearby());
                if (closing || IsDisposed) return;
                foreach (var network in networks) nearbyList.Items.Add(new ListViewItem(network.Ssid) { Tag = network });
                nearbyState.Text = networks.Count == 0 ? "未找到附近网络。" : "找到 " + networks.Count + " 个网络";
            }
            catch (Exception ex)
            {
                if (!closing && !IsDisposed) { nearbyState.Text = "搜索未完成"; MessageBox.Show(this, ex.Message, "附近的 Wi-Fi", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            }
            finally
            {
                nearbyScanning = false;
                if (!closing && !IsDisposed) nearbyRefresh.Enabled = true;
            }
        }
        void ShowAdvanced()
        {
            using (var dialog = new Form { Text = "设置", Size = new Size(620, 650), FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, Font = Theme.Font(16), BackColor = Color.White })
            {
                var pages = new PageDeck { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 0) };
                var general = new Panel { Text = "连接", BackColor = Color.White };
                var appearancePage = new Panel { Text = "外观", BackColor = Color.White };
                var logs = new Panel { Text = "日志", BackColor = Color.White };
                pages.AddPage(general, "wifi"); pages.AddPage(appearancePage, "settings"); pages.AddPage(logs, "clock"); dialog.Controls.Add(pages);
                var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16, 8, 16, 12), ColumnCount = 1, RowCount = 0 };
                general.AutoScroll = true;
                general.Controls.Add(panel);
                var note = LabelText("通常保持留空即可。仅在 Wi-Fi 配置名不同，\n或认证页面无法自动识别时修改。", 11);
                note.ForeColor = Muted;
                AddRow(panel, note, 56);
                var profile = new TextBox { Text = pendingProfile ?? settings.ProfileName };
                var accountSelector = new TextBox { Text = settings.UsernameSelector };
                var secretSelector = new TextBox { Text = settings.PasswordSelector };
                var loginSelector = new TextBox { Text = settings.LoginSelector };
                var provider = new TextBox { Text = settings.Provider ?? "" };
                ConfigField(panel, "Windows Wi-Fi 配置名称", profile); AddRow(panel, new Panel(), 10);
                ConfigField(panel, "账号输入框 CSS 选择器（如 #username）", accountSelector); AddRow(panel, new Panel(), 10);
                ConfigField(panel, "密码输入框 CSS 选择器（如 #password）", secretSelector); AddRow(panel, new Panel(), 10);
                ConfigField(panel, "登录按钮 CSS 选择器（如 #login）", loginSelector);
                AddRow(panel, new Panel(), 10);
                ConfigField(panel, "运营商名称（可选，需与页面一致）", provider);
                AddRow(panel, new Panel(), 10);
                var monitor = new CheckBox { Text = "断线后自动重连指定校园 Wi-Fi", Checked = settings.MonitorConnection, Dock = DockStyle.Fill, Font = Theme.Font(16), Margin = Padding.Empty };
                AddRow(panel, monitor, 40);
                var appearancePanel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 12), ColumnCount = 1, RowCount = 0 };
                appearancePage.Controls.Add(appearancePanel);
                AddRow(appearancePanel, LabelText("主题外观", 11.5F, true), 34);
                bool darkSelected = settings.Appearance == "dark";
                var choices = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
                var lightChoice = new ActionButton { Text = "浅色", Pill = true, Size = new Size(100, 40), Selected = !darkSelected, Margin = new Padding(0, 0, 10, 0) };
                var darkChoice = new ActionButton { Text = "深色", Pill = true, Size = new Size(100, 40), Selected = darkSelected, Margin = Padding.Empty };
                lightChoice.Click += (sender, e) => { darkSelected = false; lightChoice.Selected = true; darkChoice.Selected = false; lightChoice.Invalidate(); darkChoice.Invalidate(); };
                darkChoice.Click += (sender, e) => { darkSelected = true; lightChoice.Selected = false; darkChoice.Selected = true; lightChoice.Invalidate(); darkChoice.Invalidate(); };
                choices.Controls.Add(lightChoice); choices.Controls.Add(darkChoice); AddRow(appearancePanel, choices, 46);
                var done = new ActionButton { Text = "应用设置", Primary = true, IconName = "save", Size = new Size(138, 40), Margin = Padding.Empty };
                var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 64, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(24, 10, 24, 10), BackColor = Color.White };
                actions.Controls.Add(done); dialog.Controls.Add(actions);
                done.Click += (sender, e) =>
                {
                    string oldProfile = settings.ProfileName, oldAccount = settings.UsernameSelector, oldSecret = settings.PasswordSelector;
                    string oldLogin = settings.LoginSelector, oldAppearance = settings.Appearance;
                    string oldProvider = settings.Provider;
                    string oldPendingProfile = pendingProfile;
                    bool oldMonitor = settings.MonitorConnection;
                    try
                    {
                        settings.ProfileName = profile.Text.Trim();
                        pendingProfile = null;
                        settings.UsernameSelector = accountSelector.Text.Trim();
                        settings.PasswordSelector = secretSelector.Text.Trim();
                        settings.LoginSelector = loginSelector.Text.Trim();
                        settings.Appearance = darkSelected ? "dark" : "light";
                        settings.Provider = provider.Text.Trim();
                        settings.MonitorConnection = monitor.Checked;
                        SaveCurrent(false);
                        monitorArmed = settings.MonitorConnection && connectionValue.Text == "已联网" && !String.IsNullOrWhiteSpace(settings.Ssid);
                        failedProbes = 0;
                        ApplyAppearance(settings.Appearance == "dark");
                        dialog.DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex)
                    {
                        settings.ProfileName = oldProfile; settings.UsernameSelector = oldAccount; settings.PasswordSelector = oldSecret;
                        settings.LoginSelector = oldLogin; settings.Appearance = oldAppearance;
                        settings.Provider = oldProvider; settings.MonitorConnection = oldMonitor;
                        pendingProfile = oldPendingProfile;
                        MessageBox.Show(dialog, "无法保存设置：" + ex.Message, "设置", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                logs.Padding = new Padding(16, 8, 16, 12);
                var logText = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(248, 250, 254), Font = Theme.Font(15) };
                var logHolder = new Card { Dock = DockStyle.Fill, Fill = Color.FromArgb(248, 250, 254), Radius = 8, Padding = new Padding(12) };
                logHolder.Controls.Add(logText);
                var logActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0), BackColor = Color.White };
                var clear = new ActionButton { Text = "清除日志", Size = new Size(110, 38), Margin = Padding.Empty };
                var refresh = new ActionButton { Text = "刷新", Size = new Size(90, 38), Margin = new Padding(0, 0, 10, 0) };
                Action loadLog = () => { try { logText.Text = LogStore.Read(); logText.SelectionStart = logText.TextLength; logText.ScrollToCaret(); } catch (Exception ex) { MessageBox.Show(dialog, "无法读取日志：" + ex.Message); } };
                refresh.Click += (sender, e) => loadLog();
                clear.Click += (sender, e) =>
                {
                    if (MessageBox.Show(dialog, "确定清除已保存的运行日志吗？", "清除日志", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    try { LogStore.Clear(); loadLog(); } catch (Exception ex) { MessageBox.Show(dialog, "无法清除日志：" + ex.Message); }
                };
                logActions.Controls.Add(clear); logActions.Controls.Add(refresh);
                logs.Controls.Add(logHolder); logs.Controls.Add(logActions);
                loadLog();
                if (Theme.Dark) ApplyColors(dialog, true);
                dialog.AcceptButton = done;
                dialog.ShowDialog(this);
                ForgetColors(dialog);
            }
        }
        void ForgetColors(Control control)
        {
            foreach (Control child in control.Controls) ForgetColors(child);
            originalColors.Remove(control);
            var icon = control as IconView;
            if (icon != null) originalIconColors.Remove(icon);
        }
    }
}
