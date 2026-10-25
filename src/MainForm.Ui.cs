using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CampusConnect
{
    public sealed partial class MainForm
    {
        readonly Label connectionValue = new Label(), accountValue = new Label(), networkValue = new Label(), recentValue = new Label(), badgeLabel = new Label();
        readonly ActionButton navConnect = new ActionButton(), navSettings = new ActionButton(), navAbout = new ActionButton();
        readonly WindowButton minimizeButton = new WindowButton { Kind = WindowButton.Glyph.Minimize, AccessibleName = "最小化" };
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
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22, 16, 22, 18), ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);
            var header = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            root.Controls.Add(header, 0, 0);
            var logo = new IconView { Disc = true, Bounds = new Rectangle(15, 14, 52, 52) };
            var title = UiLabel("校园网 · 一键连接", 28, true); title.Bounds = new Rectangle(88, 10, 440, 40);
            var subtitle = UiLabel("保存一次配置。每次开机后，双击桌面图标即可连接。", 14); subtitle.ForeColor = Muted; subtitle.Bounds = new Rectangle(88, 53, 620, 23);
            header.Controls.Add(logo); header.Controls.Add(title); header.Controls.Add(subtitle);
            MouseEventHandler drag = (sender, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); } };
            header.MouseDown += drag; title.MouseDown += drag; subtitle.MouseDown += drag;
            header.DoubleClick += (sender, e) => ToggleMaximize();
            // 最小化与关闭。放在 header 内且 y 非负：子控件超出父控件会被裁剪。
            var windowButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Size = new Size(92, 34), Location = new Point(header.Width - 92, 2) };
            header.Resize += (sender, e) => windowButtons.Left = header.Width - 92;
            foreach (var button in new[] { minimizeButton, closeButton })
            {
                button.Size = new Size(46, 34);
                button.Margin = Padding.Empty;
                windowButtons.Controls.Add(button);
            }
            minimizeButton.Click += (sender, e) => WindowState = FormWindowState.Minimized;
            closeButton.Click += (sender, e) => Close();
            header.Controls.Add(windowButtons);
            var badge = new Card { Fill = Color.FromArgb(230, 248, 244), Border = Color.FromArgb(205, 237, 231), Radius = 16, Size = new Size(92, 30), Anchor = AnchorStyles.Right | AnchorStyles.Top };
            badgeLabel.Text = "●  已就绪"; badgeLabel.ForeColor = Teal; badgeLabel.Font = Theme.Font(12, true); badgeLabel.TextAlign = ContentAlignment.MiddleCenter; badgeLabel.Dock = DockStyle.Fill; badgeLabel.BackColor = Color.Transparent;
            badge.Controls.Add(badgeLabel); header.Controls.Add(badge);
            header.Resize += (sender, e) => badge.Location = new Point(header.Width - 7 - badge.Width, 46);

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 356));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(body, 0, 1);
            var rail = new Card { Dock = DockStyle.Fill, Border = Color.FromArgb(242, 247, 250), Margin = new Padding(0, 0, 12, 0), Padding = new Padding(8, 16, 8, 8) };
            body.Controls.Add(rail, 0, 0);
            var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
            var railLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
            railLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            railLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            railLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 77));
            navigation.Margin = Padding.Empty;
            rail.Controls.Add(railLayout); railLayout.Controls.Add(navigation, 0, 0);
            Navigation(navConnect, "连接", "wifi", true); Navigation(navSettings, "设置", "settings", false); Navigation(navAbout, "关于", "info", false);
            navigation.Controls.Add(navConnect); navigation.Controls.Add(navSettings); navigation.Controls.Add(navAbout);
            navConnect.Click += (sender, e) => { tabs.SelectedIndex = 0; SelectNav(navConnect); ssid.Focus(); };
            navSettings.Click += (sender, e) => { if (!busy) { SelectNav(navSettings); ShowAdvanced(); SelectNav(navConnect); } };
            navAbout.Click += (sender, e) => { SelectNav(navAbout); ShowAbout(); SelectNav(navConnect); };
            var shortcut = new ActionButton { AccessibleName = "创建桌面快捷方式" };
            Navigation(shortcut, "桌面图标", "link", false);
            shortcut.Dock = DockStyle.Fill; shortcut.Margin = Padding.Empty;
            shortcut.Click += (sender, e) => { try { Program.CreateShortcut(); WriteLog("桌面快捷方式已创建。"); } catch (Exception ex) { ShowFailure(ex); } };
            railLayout.Controls.Add(shortcut, 0, 1);

            var config = new Card { Dock = DockStyle.Fill, Border = Color.FromArgb(238, 244, 249), Margin = new Padding(0, 0, 16, 0), Padding = new Padding(21, 19, 21, 18) };
            body.Controls.Add(config, 1, 0);
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
            var current = new ActionButton { Text = "使用当前 Wi-Fi", Pill = true, Selected = true, Width = 145, Height = 31, Font = Theme.Font(14), Anchor = AnchorStyles.Left, Margin = new Padding(0, 5, 0, 0) };
            current.Click += (sender, e) => { var networks = Wifi.ConnectedSsids(); if (networks.Count > 0) ssid.Text = networks[0]; else MessageBox.Show(this, "当前未连接 Wi-Fi，请先在 Windows 中连接并保存校园 Wi-Fi。", Text); };
            AddRow(form, current, 40);
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
            // 高级设置的唯一入口是左侧栏的「设置」；这里原本还有一个同样的按钮，属于重复。

            var dashboard = new Card { Dock = DockStyle.Fill, Padding = new Padding(18), Border = Color.FromArgb(238, 244, 249), Margin = Padding.Empty };
            body.Controls.Add(dashboard, 2, 0);
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.White, Margin = Padding.Empty };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 190)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 98)); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); dashboard.Controls.Add(content);
            var hero = new HeroCard { Dock = DockStyle.Fill, Margin = Padding.Empty };
            content.Controls.Add(hero, 0, 0);
            var halo = new Card { Fill = Color.FromArgb(232, 247, 242), Border = Color.FromArgb(227, 244, 239), Radius = 68, Bounds = new Rectangle(20, 31, 128, 128) };
            halo.Controls.Add(new IconView { Disc = true, Bounds = new Rectangle(18, 18, 92, 92) }); hero.Controls.Add(halo);
            status.Text = "等待连接"; status.Font = Theme.Font(24, true); status.ForeColor = Ink; status.BackColor = Color.Transparent; status.Bounds = new Rectangle(169, 30, 330, 35); hero.Controls.Add(status);
            detail.Text = "填写左侧配置，即可一键连接校园网。"; detail.Font = Theme.Font(13); detail.ForeColor = Muted; detail.BackColor = Color.Transparent; detail.Bounds = new Rectangle(169, 72, 310, 41); hero.Controls.Add(detail);
            ConfigureAction(connect, "连接网络", "wifi", true, 148); connect.Pill = true;
            connect.Bounds = new Rectangle(169, 124, 148, 43);
            ConfigureAction(cancel, "取消连接", "", false, 116);
            cancel.Bounds = new Rectangle(329, 124, 116, 43); cancel.Visible = false;
            hero.Controls.Add(connect); hero.Controls.Add(cancel);
            hero.Resize += (sender, e) =>
            {
                status.Width = Math.Max(180, hero.Width > 650 ? hero.Width - 420 : hero.Width - 185);
                detail.Width = status.Width;
            };

            var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 14, 0, 0), BackColor = Color.White };
            metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            Metric(metrics, 0, "wifi", "连接状态", "未连接", connectionValue); Metric(metrics, 1, "user", "账号状态", "未认证", accountValue);
            Metric(metrics, 2, "globe", "网络类型", "校园网", networkValue); Metric(metrics, 3, "clock", "最近连接", "--", recentValue); content.Controls.Add(metrics, 0, 1);

            var history = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 15, 0, 0), Padding = new Padding(10, 8, 10, 8) };
            tabs.Dock = DockStyle.Fill;
            var logTab = new Panel { Text = "运行记录", BackColor = Color.White }; log.Dock = DockStyle.Fill; logTab.Controls.Add(log);
            browserTab.BackColor = Color.White;
            var placeholder = UiLabel("连接时将自动加载学校认证页面。\n\n如需验证码或选择运营商，可在此完成。", 14); placeholder.ForeColor = Muted; placeholder.Dock = DockStyle.Fill; placeholder.Padding = new Padding(22); browserTab.Controls.Add(placeholder);
            tabs.AddPage(logTab); tabs.AddPage(browserTab); history.Controls.Add(tabs); content.Controls.Add(history, 0, 2);

            save.Click += (sender, e) => { try { SaveCurrent(false); SetStatus("已配置完成", auto.Checked ? "下次打开程序即可自动连接校园网。" : "点击“连接网络”即可连接校园网。", false); WriteLog("配置已保存，密码已使用 Windows 加密。"); } catch (Exception ex) { ShowFailure(ex); } };
            cancel.Click += (sender, e) => { if (operation != null) operation.Cancel(); }; connect.Click += async (sender, e) => await ConnectAsync();
            AcceptButton = connect;
        }
        void Navigation(ActionButton button, string text, string icon, bool selected)
        {
            button.Text = text; button.IconName = icon; button.Navigation = true; button.Selected = selected; button.Size = new Size(60, 77); button.Margin = new Padding(0, 0, 0, 13);
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
            var card = new Card { Dock = DockStyle.Fill, Fill = Color.FromArgb(250, 252, 255), Radius = 9, Margin = new Padding(column == 0 ? 0 : 6, 0, column == 3 ? 0 : 6, 0) };
            var circle = new Card { Fill = Color.FromArgb(231, 247, 242), Border = Color.FromArgb(231, 247, 242), Radius = 22, Bounds = new Rectangle(12, 21, 40, 40) };
            circle.Controls.Add(new IconView { IconName = icon, Bounds = new Rectangle(8, 8, 24, 24) }); card.Controls.Add(circle);
            var label = UiLabel(caption, 11.5F); label.ForeColor = Muted; label.Bounds = new Rectangle(62, 15, 100, 23); card.Controls.Add(label);
            output.Text = value; output.Font = Theme.Font(13); output.ForeColor = Muted; output.BackColor = Color.Transparent; output.Bounds = new Rectangle(62, 40, 100, 23); card.Controls.Add(output);
            card.Resize += (sender, e) =>
            {
                bool compact = card.Width < 165;
                circle.Bounds = compact ? new Rectangle(9, 25, 28, 28) : new Rectangle(12, 21, 40, 40);
                circle.Controls[0].Bounds = compact ? new Rectangle(5, 5, 18, 18) : new Rectangle(8, 8, 24, 24);
                label.Left = output.Left = compact ? 44 : 62;
                label.Width = output.Width = Math.Max(36, card.Width - label.Left - 8);
                output.Font = Theme.Font(compact ? 11 : 13);
            };
            grid.Controls.Add(card, column, 0);
        }
        void ToggleMaximize() { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }
        // The registry is authoritative: only a real difference (on/off, or a stale
        // path left by an upgrade) triggers a write, so loading the state is quiet.
        void SyncStartupEntry()
        {
            bool wanted = startWithWindows.Checked;
            try
            {
                if (wanted ? Startup.IsCurrent() : !Startup.IsEnabled()) return;
                Startup.SetEnabled(wanted);
                WriteLog(wanted ? "已设置开机自动启动。" : "已取消开机自动启动。");
            }
            catch (Exception ex)
            {
                ShowFailure(ex);
                try { startWithWindows.Checked = Startup.IsEnabled(); }
                catch (Exception) { }
            }
        }
        void ShowAbout()
        {
            using (var dialog = new Form { Text = "关于校园网一键连接", Size = new Size(500, 310), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Color.White, Font = Theme.Font(14) })
            {
                var title = UiLabel("校园网 · 一键连接", 23, true); title.Bounds = new Rectangle(26, 24, 420, 40); dialog.Controls.Add(title);
                var body = UiLabel("版本 " + BuildInfo.Version + " · Windows 桌面版\n\n连接已保存的校园 Wi-Fi，自动完成深澜认证。\n密码由 Windows 当前用户加密保存在本机。\n\n界面图标来自 Ant Design Icons（MIT 许可）", 13); body.Bounds = new Rectangle(26, 77, 430, 150); dialog.Controls.Add(body);
                var close = new ActionButton { Text = "知道了", Primary = true, Bounds = new Rectangle(340, 226, 112, 35), DialogResult = DialogResult.OK }; dialog.Controls.Add(close); dialog.AcceptButton = close; dialog.ShowDialog(this);
            }
        }
    }
}
