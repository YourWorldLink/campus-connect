using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CampusConnect
{
    public sealed partial class MainForm
    {
        readonly Label connectionValue = new Label(), accountValue = new Label(), networkValue = new Label(), recentValue = new Label();
        readonly ActionButton navConnect = new ActionButton(), navSettings = new ActionButton(), navAbout = new ActionButton();
        readonly WindowButton minimizeButton = new WindowButton { Kind = WindowButton.Glyph.Minimize, AccessibleName = "最小化到系统托盘" };
        readonly WindowButton maximizeButton = new WindowButton { Kind = WindowButton.Glyph.Maximize, AccessibleName = "最大化" };
        readonly WindowButton closeButton = new WindowButton { Kind = WindowButton.Glyph.Close, AccessibleName = "关闭" };
        readonly IconView revealPassword = new IconView { IconName = "eye", IconColor = Muted, Width = 24, Height = 24, Cursor = Cursors.Hand, AccessibleName = "显示或隐藏密码", TabStop = false };
        bool passwordShown;
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        Label UiLabel(string text, float size, bool bold = false)
        {
            return new Label { Text = text, Font = Theme.Font(size, bold), ForeColor = Ink, BackColor = Color.Transparent, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
        }
        void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 8, 24, 22), ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);
            var header = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            root.Controls.Add(header, 0, 0);
            var logo = new IconView { Disc = true, Bounds = new Rectangle(0, 10, 42, 42) };
            var title = UiLabel("校园网一键连接", 24, true); title.Bounds = new Rectangle(54, 6, 202, 50);
            header.Controls.Add(logo); header.Controls.Add(title);
            MouseEventHandler drag = (sender, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); } };
            header.MouseDown += drag; title.MouseDown += drag; logo.MouseDown += drag;
            header.DoubleClick += (sender, e) => ToggleMaximize();
            var windowButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Size = new Size(138, 34), Location = new Point(header.Width - 138, 2) };
            header.Resize += (sender, e) => windowButtons.Left = header.Width - 138;
            foreach (var button in new[] { minimizeButton, maximizeButton, closeButton })
            {
                button.Size = new Size(46, 34);
                button.Margin = Padding.Empty;
                windowButtons.Controls.Add(button);
            }
            minimizeButton.Click += (sender, e) => MinimizeToTray();
            maximizeButton.Click += (sender, e) => ToggleMaximize();
            var windowTips = new ToolTip();
            windowTips.SetToolTip(minimizeButton, "最小化到系统托盘");
            windowTips.SetToolTip(closeButton, "关闭");
            Action syncMaximize = () => { bool maximized = WindowState == FormWindowState.Maximized; maximizeButton.Kind = maximized ? WindowButton.Glyph.Restore : WindowButton.Glyph.Maximize; maximizeButton.AccessibleName = maximized ? "还原" : "最大化"; windowTips.SetToolTip(maximizeButton, maximizeButton.AccessibleName); maximizeButton.Invalidate(); };
            Resize += (sender, e) => syncMaximize();
            FormClosed += (sender, e) => windowTips.Dispose();
            syncMaximize();
            closeButton.Click += (sender, e) => Close();
            header.Controls.Add(windowButtons);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(body, 0, 1);
            var navigation = new FlowLayoutPanel { Location = new Point(288, 12), Size = new Size(420, 40), WrapContents = false, BackColor = Color.Transparent };
            header.Controls.Add(navigation);
            Navigation(navConnect, "主页", "wifi", true); Navigation(navSettings, "设置", "settings", false); Navigation(navAbout, "关于", "info", false);
            navigation.Controls.Add(navConnect); navigation.Controls.Add(navSettings); navigation.Controls.Add(navAbout);
            navConnect.Click += (sender, e) => { tabs.SelectedTab = logTab; SelectNav(navConnect); ssid.Focus(); };
            navSettings.Click += (sender, e) => { if (!busy) { settingsOpen = true; try { SelectNav(navSettings); ShowAdvanced(); } finally { settingsOpen = false; SelectNav(navConnect); } } };
            navAbout.Click += (sender, e) => { SelectNav(navAbout); ShowAbout(); SelectNav(navConnect); };
            var shortcut = new ActionButton { AccessibleName = "创建桌面快捷方式" };
            Navigation(shortcut, "桌面图标", "link", false);
            shortcut.Width = 116;
            shortcut.Click += (sender, e) => { try { Program.CreateShortcut(); WriteLog("桌面快捷方式已创建。"); } catch (Exception ex) { ShowFailure(ex); } };
            navigation.Controls.Add(shortcut);
            Action fitWorkspace = () =>
            {
                int gutter = Math.Max(0, (root.DisplayRectangle.Width - 1520) / 2);
                body.Margin = new Padding(gutter, 0, gutter, 0);
                logo.Left = gutter;
                title.Left = gutter + 54;
                navigation.Left = gutter + 266;
                body.ColumnStyles[0].Width = Math.Max(336, Math.Min(400, root.DisplayRectangle.Width / 3));
            };
            root.SizeChanged += (sender, e) => fitWorkspace();
            fitWorkspace();

            var config = new Card { Dock = DockStyle.Top, Height = 594, Margin = new Padding(0, 0, 16, 0), Padding = new Padding(20, 16, 20, 18) };
            body.Controls.Add(config, 0, 0);
            body.SizeChanged += (sender, e) => config.Height = Math.Min(594, body.ClientSize.Height);
            var configLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White, Margin = Padding.Empty };
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            config.Controls.Add(configLayout);
            fields.Dock = DockStyle.Fill; fields.AutoScroll = true; fields.BackColor = Color.White; fields.Margin = Padding.Empty; configLayout.Controls.Add(fields, 0, 0);
            ConfigureAction(save, "保存配置", "save", false, 158);
            save.Font = Theme.Font(16, true);
            save.Dock = DockStyle.Fill; save.Margin = new Padding(0, 12, 0, 0);
            configLayout.Controls.Add(save, 0, 1);
            var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 0, BackColor = Color.White, Margin = Padding.Empty, Padding = new Padding(0, 0, 2, 0) };
            fields.Controls.Add(form);
            var configTitle = UiLabel("连接配置", 24, true); configTitle.Dock = DockStyle.Fill; AddRow(form, configTitle, 38);
            ConfigField(form, "Wi-Fi 名称（SSID）", ssid);
            var current = new ActionButton { Text = "使用当前 Wi-Fi", Width = 135, Height = 31, Font = Theme.Font(14), Anchor = AnchorStyles.Left, Margin = new Padding(0, 5, 0, 0) };
            current.Click += (sender, e) => { var networks = Wifi.ConnectedSsids(); if (networks.Count > 0) ssid.Text = networks[0]; else MessageBox.Show(this, "当前未连接 Wi-Fi，请先在 Windows 中连接并保存校园 Wi-Fi。", Text); };
            var search = new ActionButton { Text = "搜索附近", IconName = "wifi", Width = 118, Height = 31, Font = Theme.Font(14), Margin = new Padding(8, 5, 0, 0) };
            search.Click += async (sender, e) => { if (tabs.SelectedTab == nearbyTab) await ScanNearbyAsync(); else tabs.SelectedTab = nearbyTab; };
            var networkActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            networkActions.Controls.Add(current); networkActions.Controls.Add(search); AddRow(form, networkActions, 40);
            ConfigField(form, "校园网认证网址", url); AddRow(form, new Panel(), 11);
            ConfigField(form, "校园网账号", username); AddRow(form, new Panel(), 11);
            ConfigField(form, "校园网密码", password, revealPassword);
            password.UseSystemPasswordChar = true;
            revealPassword.Click += (sender, e) =>
            {
                passwordShown = !passwordShown;
                password.UseSystemPasswordChar = !passwordShown;
                revealPassword.IconName = passwordShown ? "eye-off" : "eye";
                revealPassword.IconColor = passwordShown ? Teal : Muted;
                revealPassword.Invalidate();
                password.Focus();
            };
            revealPassword.MouseEnter += (sender, e) => { if (!passwordShown) { revealPassword.IconColor = Ink; revealPassword.Invalidate(); } };
            revealPassword.MouseLeave += (sender, e) => { if (!passwordShown) { revealPassword.IconColor = Muted; revealPassword.Invalidate(); } };
            auto.Text = "下次打开程序时自动连接"; auto.Font = Theme.Font(15); auto.Dock = DockStyle.Fill; auto.Margin = Padding.Empty; AddRow(form, auto, 34);
            startWithWindows.Text = "开机时自动启动本工具"; startWithWindows.Font = Theme.Font(15); startWithWindows.Dock = DockStyle.Fill; startWithWindows.Margin = Padding.Empty; AddRow(form, startWithWindows, 34);
            startWithWindows.CheckedChanged += (sender, e) => SyncStartupEntry();
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent, Margin = Padding.Empty };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 196));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.Controls.Add(content, 1, 0);
            var hero = new HeroCard { Dock = DockStyle.Fill, Margin = Padding.Empty };
            content.Controls.Add(hero, 0, 0);
            var caption = UiLabel("校园网连接", 15, true); caption.ForeColor = Color.White; caption.Bounds = new Rectangle(24, 14, 180, 24); hero.Controls.Add(caption);
            status.Text = "等待连接"; status.Font = Theme.Font(30, true); status.ForeColor = Color.White; status.BackColor = Color.Transparent; status.Bounds = new Rectangle(24, 41, 340, 42); hero.Controls.Add(status);
            detail.Font = Theme.Font(15); detail.ForeColor = Color.White; detail.BackColor = Color.Transparent; detail.Bounds = new Rectangle(24, 85, 410, 49); hero.Controls.Add(detail);
            detail.TextAlign = ContentAlignment.TopLeft; detail.AutoEllipsis = true;
            ConfigureAction(connect, "连接网络", "wifi", true, 148);
            connect.Bounds = new Rectangle(24, 137, 150, 42);
            ConfigureAction(cancel, "取消连接", "", false, 116);
            cancel.Bounds = new Rectangle(186, 137, 116, 42); cancel.Visible = false;
            hero.Controls.Add(connect); hero.Controls.Add(cancel);
            hero.Resize += (sender, e) =>
            {
                status.Width = Math.Max(240, hero.Width - 175);
                detail.Width = status.Width;
            };

            var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 14, 0, 0), BackColor = Color.Transparent };
            metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            Metric(metrics, 0, "wifi", "连接状态", "未连接", connectionValue); Metric(metrics, 1, "user", "账号状态", "未认证", accountValue);
            Metric(metrics, 2, "globe", "网络类型", "校园网", networkValue); Metric(metrics, 3, "clock", "最近连接", "--", recentValue); content.Controls.Add(metrics, 0, 1);

            var history = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 14, 0, 0), Padding = new Padding(14, 10, 14, 8) };
            tabs.Dock = DockStyle.Fill;
            log.Dock = DockStyle.Fill; logTab.Controls.Add(log);
            browserTab.BackColor = Color.White;
            var placeholder = UiLabel("连接时将自动加载学校认证页面。\n\n如需验证码或选择运营商，可在此完成。", 14); placeholder.ForeColor = Muted; placeholder.Dock = DockStyle.Fill; placeholder.Padding = new Padding(22); browserTab.Controls.Add(placeholder);
            BuildNearbyPage();
            tabs.AddPage(nearbyTab, "wifi"); tabs.AddPage(logTab, "clock"); tabs.AddPage(browserTab, "globe"); tabs.SelectedTab = logTab;
            nearbyTab.VisibleChanged += async (sender, e) => { if (nearbyTab.Visible && !nearbyLoaded) await ScanNearbyAsync(); };
            history.Controls.Add(tabs); content.Controls.Add(history, 0, 2);

            save.Click += (sender, e) => { try { SaveCurrent(false); SetStatus("已配置完成", auto.Checked ? "下次打开程序即可自动连接校园网。" : "点击“连接网络”即可连接校园网。", false); WriteLog("配置已保存，密码已使用 Windows 加密。"); } catch (Exception ex) { ShowFailure(ex); } };
            cancel.Click += (sender, e) => { if (operation != null) operation.Cancel(); }; connect.Click += async (sender, e) => await ConnectAsync();
            AcceptButton = connect;
        }
        void Navigation(ActionButton button, string text, string icon, bool selected)
        {
            button.Text = text; button.IconName = icon; button.Navigation = true; button.Selected = selected; button.Size = new Size(88, 38); button.Margin = new Padding(0, 0, 6, 0);
        }
        void SelectNav(ActionButton selected)
        {
            foreach (var button in new[] { navConnect, navSettings, navAbout }) { button.Selected = button == selected; button.Invalidate(); }
        }
        void ConfigureAction(ActionButton button, string text, string icon, bool primary, int width)
        {
            button.Text = text; button.IconName = icon; button.Primary = primary; button.Size = new Size(width, 43); button.Margin = new Padding(10, 0, 0, 0);
        }
        void ConfigField(TableLayoutPanel form, string caption, TextBox box, Control trailing = null)
        {
            var label = UiLabel(caption, 16, true); label.Dock = DockStyle.Fill; AddRow(form, label, 34);
            var holder = new Card { Dock = DockStyle.Fill, Fill = Color.FromArgb(248, 250, 254), Radius = 8, Padding = new Padding(11, 11, trailing == null ? 9 : 42, 9), Margin = Padding.Empty };
            box.BorderStyle = BorderStyle.None; box.BackColor = holder.Fill; box.Font = Theme.Font(17); box.Dock = DockStyle.Fill; holder.Controls.Add(box);
            if (trailing != null)
            {
                // The reserved right padding keeps the inline control clear of the text.
                holder.Controls.Add(trailing);
                trailing.BringToFront();
                EventHandler place = (sender, e) => trailing.Bounds = new Rectangle(holder.Width - 10 - trailing.Width, (holder.Height - trailing.Height) / 2, trailing.Width, trailing.Height);
                holder.Resize += place;
                place(holder, EventArgs.Empty);
            }
            AddRow(form, holder, 44);
        }
        void Metric(TableLayoutPanel grid, int column, string icon, string caption, string value, Label output)
        {
            var card = new Card { Dock = DockStyle.Fill, Margin = new Padding(column == 0 ? 0 : 5, 0, column == 3 ? 0 : 5, 0) };
            var symbol = new IconView { IconName = icon, Bounds = new Rectangle(14, 13, 18, 18) };
            var label = UiLabel(caption, 14); label.ForeColor = Muted; label.Bounds = new Rectangle(38, 9, 96, 26);
            card.Controls.Add(symbol); card.Controls.Add(label);
            output.Text = value; output.Font = Theme.Font(output == recentValue ? 16 : 21, true); output.ForeColor = Ink;
            output.BackColor = Color.Transparent; output.TextAlign = ContentAlignment.MiddleLeft;
            output.Bounds = new Rectangle(12, 36, 120, 48); card.Controls.Add(output);
            card.Resize += (sender, e) =>
            {
                output.Width = Math.Max(1, card.Width - 24);
                label.Width = Math.Max(1, card.Width - label.Left - 6);
            };
            grid.Controls.Add(card, column, 0);
        }
        void ToggleMaximize() { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }
        // The registry is authoritative: only a real difference (on/off, or a stale
        // path left by an upgrade) triggers a write, so loading the state is quiet.
        void SyncStartupEntry()
        {
            if (!String.IsNullOrEmpty(screenshot) || syncingStartup) return;
            bool wanted = startWithWindows.Checked;
            syncingStartup = true;
            try
            {
                if (!(wanted ? Startup.IsCurrent() : !Startup.IsEnabled()))
                {
                    Startup.SetEnabled(wanted);
                    WriteLog(wanted ? "已设置开机自动启动。" : "已取消开机自动启动。");
                }
                if (wanted && Startup.IsDisabledByWindows())
                {
                    WriteLog("Windows 启动应用设置已禁用本工具，请在系统设置中启用。");
                    MessageBox.Show(this, "Windows 已禁用本工具的启动项。请打开 Windows 设置 → 应用 → 启动，启用“校园网一键连接”。", "开机自启动", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                ShowFailure(ex);
                try { startWithWindows.Checked = Startup.IsEnabled(); }
                catch (Exception) { }
            }
            finally { syncingStartup = false; }
        }
        void ShowAbout()
        {
            using (var dialog = new Form { Text = "关于校园网一键连接", Size = new Size(500, 310), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Color.White, Font = Theme.Font(14) })
            {
                var title = UiLabel("校园网 · 一键连接", 23, true); title.Bounds = new Rectangle(26, 24, 420, 40); dialog.Controls.Add(title);
                var body = UiLabel("版本 " + BuildInfo.Version + " · Windows 桌面版\n\n连接已保存的校园 Wi-Fi，自动完成深澜认证。\n密码由 Windows 当前用户加密保存在本机。\n\n界面图标来自 Ant Design Icons（MIT 许可）", 13); body.Bounds = new Rectangle(26, 77, 430, 150); dialog.Controls.Add(body);
                var close = new ActionButton { Text = "知道了", Primary = true, Bounds = new Rectangle(340, 226, 112, 35), DialogResult = DialogResult.OK }; dialog.Controls.Add(close); dialog.AcceptButton = close;
                if (Theme.Dark) ApplyColors(dialog, true);
                dialog.ShowDialog(this);
                ForgetColors(dialog);
            }
        }
    }
}
