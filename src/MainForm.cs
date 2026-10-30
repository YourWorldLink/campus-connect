using System;
using System.Drawing;
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
        static readonly Color Teal = Theme.Green;
        static readonly Color Ink = Theme.Ink;
        static readonly Color Muted = Theme.Muted;
        readonly TextBox ssid = new TextBox(), url = new TextBox(), username = new TextBox(), password = new TextBox();
        readonly CheckBox auto = new CheckBox(), startWithWindows = new CheckBox();
        readonly Label status = new Label(), detail = new Label(), stepWifi = new Label(), stepPortal = new Label(), stepOnline = new Label();
        readonly TimelineLog log = new TimelineLog();
        readonly ActionButton connect = new ActionButton(), cancel = new ActionButton(), save = new ActionButton();
        readonly PageDeck tabs = new PageDeck();
        readonly Panel browserTab = new Panel { Text = "认证页面" };
        readonly Panel fields = new Panel();
        Settings settings;
        WebView2 browser;
        Task browserInitialization;
        string browserDirectory;
        CancellationTokenSource operation;
        bool busy, closing;
        bool portalAuthenticated;
        bool statusHasError;
        readonly bool preview;
        readonly bool autostart;
        readonly string screenshot;

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
                Show();
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                BringToFront();
                Activate();
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

        public MainForm(bool preview, string screenshot, bool autostart = false)
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
            try { settings = SettingsStore.Load(); }
            catch (Exception ex)
            {
                settings = new Settings();
                SetStatus("请重新配置", "原配置无法读取或密码无法解密。原文件会保留，保存后才会更新。", false);
                WriteLog("配置读取失败（" + ex.GetType().Name + "），请重新输入账号和密码。");
            }
            ApplySettings();
            Shown += OnShown;
            FormClosing += (sender, e) =>
            {
                closing = true;
                if (operation != null) operation.Cancel();
                if (browser != null) browser.Dispose();
            };
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
            AddRow(panel, LabelText(caption, 9.5F, true), 30);
            var holder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 7, 10, 5), BackColor = Color.FromArgb(246, 249, 250), Margin = new Padding(0, 0, 0, 2) };
            holder.Paint += (sender, e) => { using (var pen = new Pen(Color.FromArgb(221, 230, 232))) e.Graphics.DrawRectangle(pen, 0, 0, holder.Width - 1, holder.Height - 1); };
            box.BorderStyle = BorderStyle.None;
            box.BackColor = holder.BackColor;
            box.Dock = DockStyle.Fill;
            box.Font = new Font(Font.FontFamily, 10);
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
            try { startWithWindows.Checked = Startup.IsEnabled(); }
            catch (Exception ex) { WriteLog("无法读取开机自启动设置（" + ex.GetType().Name + "）。"); }
            accountValue.Text = settings.HasCredentials ? "已填写" : "未认证";
            recentValue.Text = String.IsNullOrEmpty(settings.LastConnected) ? "--" : settings.LastConnected;
            networkValue.Text = String.IsNullOrEmpty(settings.Ssid) ? "当前网络" : "校园 Wi-Fi";
            if (status.Text == "等待连接") SetStatus(settings.HasCredentials ? "已配置完成" : "等待配置", settings.HasCredentials ? "点击“连接网络”即可连接校园网。" : "填写左侧配置，即可一键连接校园网。", false);
        }
        void SaveCurrent(bool connecting)
        {
            var updated = new Settings { Ssid = ssid.Text.Trim(), PortalUrl = url.Text.Trim(), Username = username.Text.Trim(), Password = password.Text,
                AutoConnect = auto.Checked, LastConnected = settings.LastConnected, ProfileName = settings.ProfileName, UsernameSelector = settings.UsernameSelector,
                PasswordSelector = settings.PasswordSelector, LoginSelector = settings.LoginSelector };
            Validation.Check(updated, connecting);
            SettingsStore.Save(updated);
            settings = updated;
            accountValue.Text = settings.HasCredentials ? "已填写" : "未认证";
            networkValue.Text = String.IsNullOrEmpty(settings.Ssid) ? "当前网络" : "校园 Wi-Fi";
        }
        async void OnShown(object sender, EventArgs e)
        {
            WriteLog(settings.HasCredentials ? "工具已就绪。配置已载入，可以连接校园网。" : "工具已就绪。首次使用请填写 SSID、账号和密码。");
            if (!String.IsNullOrEmpty(screenshot))
            {
                // Previews must not expose a user's saved account in shareable QA images.
                username.Text = settings.HasCredentials ? "（账号已保存）" : "";
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
            status.ForeColor = error ? Color.FromArgb(181, 78, 49) : Ink;
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
            log.Add(DateTime.Now.ToString("HH:mm:ss"), Redact(message));
        }
        void ShowFailure(Exception exception)
        {
            if (closing) return;
            string message = exception is ArgumentException || exception is InvalidOperationException || exception is TimeoutException
                ? exception.Message : "操作失败（" + exception.GetType().Name + "）。请检查配置、网络或本地文件权限后重试。";
            message = Redact(message);
            SetStatus("连接未完成", message, true);
            WriteLog(message);
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

        async Task ConnectAsync(bool retryOnStartup = false)
        {
            if (busy || closing) return;
            try { SaveCurrent(true); }
            catch (Exception ex) { ShowFailure(ex); return; }
            busy = true;
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
                if (retryOnStartup) await ConnectUntilOnlineAsync(token);
                else await ConnectOnceAsync(token);
            }
            catch (OperationCanceledException)
            {
                if (!closing && browser != null && browser.CoreWebView2 != null) browser.CoreWebView2.Stop();
                SetStatus("已取消", "连接流程已停止。已连接的 Wi-Fi 会保留。", false);
                WriteLog("用户已取消连接。");
            }
            catch (Exception ex) { ShowFailure(ex); }
            finally
            {
                operation.Dispose();
                operation = null;
                busy = false;
                if (!closing) { fields.Enabled = true; connect.Enabled = save.Enabled = navSettings.Enabled = true; cancel.Visible = false; connect.Text = "连接网络"; badgeLabel.Text = statusHasError ? "●  请检查" : "●  已就绪"; }
            }
        }
        // Wi-Fi 就绪通常在一分钟内，给足余量但不要永远空转：不在校园网时（例如在家）
        // 无限重试只会让无线网卡每隔一分钟白白拨一次。超过上限就交给用户手动重试。
        async Task ConnectUntilOnlineAsync(CancellationToken token)
        {
            var started = DateTime.UtcNow;
            int attempt = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                attempt++;
                try
                {
                    var result = await ConnectOnceAsync(token);
                    if (result == ConnectResult.Online) return;
                    if (result == ConnectResult.NeedsManual)
                    {
                        WriteLog("认证页面需要人工操作，已停止自动重试。");
                        return;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (PortalRejectedException) { throw; }
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
                if (await Internet.CheckAsync(token)) { MarkOnline("网络已在后台连通。"); return; }
                int delay = RetryPolicy.DelaySeconds(attempt);
                SetStatus("等待重试", "第 " + (attempt + 1) + " 次尝试将在 " + delay + " 秒后开始，已持续 " + (int)(DateTime.UtcNow - started).TotalMinutes + " 分钟…", false);
                WriteLog("第 " + (attempt + 1) + " 次尝试将在 " + delay + " 秒后开始。");
                await Task.Delay(TimeSpan.FromSeconds(delay), token);
            }
        }
        async Task<ConnectResult> ConnectOnceAsync(CancellationToken token)
        {
            SetStatus("正在连接", "正在连接校园 Wi-Fi，请稍候…", false);
            WriteLog(String.IsNullOrEmpty(settings.Ssid) ? "使用当前网络。" : "正在连接指定 Wi-Fi。");
            stepWifi.ForeColor = Teal;
            await Wifi.ConnectAsync(settings.Ssid, settings.ProfileName, token);
            token.ThrowIfCancellationRequested();
            WriteLog("Wi-Fi 连接步骤完成，检查当前网络。");
            if (await Internet.CheckAsync(token)) { MarkOnline("当前网络已可访问互联网，无需重复认证。"); return ConnectResult.Online; }
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
            recentValue.Text = DateTime.Now.ToString("MM-dd HH:mm");
            settings.LastConnected = recentValue.Text;
            try { SettingsStore.Save(settings); } catch (Exception) { WriteLog("网络已连接，但最近连接时间未能保存。"); }
            stepWifi.ForeColor = stepPortal.ForeColor = stepOnline.ForeColor = Teal;
            WriteLog(description);
            tabs.SelectedIndex = 0;
        }
        void ShowAdvanced()
        {
            using (var dialog = new Form { Text = "高级设置", Size = new Size(520, 470), FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, Font = Font, BackColor = Color.White })
            {
                var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 0 };
                dialog.Controls.Add(panel);
                var note = LabelText("通常保持留空即可。仅在 Wi-Fi 配置名不同，\n或认证页面无法自动识别时修改。", 9);
                note.ForeColor = Muted;
                AddRow(panel, note, 48);
                var profile = new TextBox { Text = settings.ProfileName };
                var accountSelector = new TextBox { Text = settings.UsernameSelector };
                var secretSelector = new TextBox { Text = settings.PasswordSelector };
                var loginSelector = new TextBox { Text = settings.LoginSelector };
                AddField(panel, "Windows Wi-Fi 配置名称", profile);
                AddField(panel, "账号输入框 CSS 选择器（如 #username）", accountSelector);
                AddField(panel, "密码输入框 CSS 选择器（如 #password）", secretSelector);
                AddField(panel, "登录按钮 CSS 选择器（如 #login）", loginSelector);
                var done = new Button();
                StyleButton(done, "应用设置", true);
                done.Anchor = AnchorStyles.Right;
                done.Margin = new Padding(0, 8, 0, 0);
                done.Click += (sender, e) =>
                {
                    settings.ProfileName = profile.Text.Trim();
                    settings.UsernameSelector = accountSelector.Text.Trim();
                    settings.PasswordSelector = secretSelector.Text.Trim();
                    settings.LoginSelector = loginSelector.Text.Trim();
                    dialog.DialogResult = DialogResult.OK;
                };
                AddRow(panel, done, 52);
                dialog.AcceptButton = done;
                dialog.ShowDialog(this);
            }
        }
    }
}
