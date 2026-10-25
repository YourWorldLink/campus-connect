using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CampusConnect
{
    public static class Theme
    {
        public static readonly Color Green = Color.FromArgb(15, 139, 116);
        public static readonly Color Ink = Color.FromArgb(30, 43, 65);
        public static readonly Color Muted = Color.FromArgb(126, 143, 167);
        public static readonly Color Line = Color.FromArgb(224, 233, 243);
        public static readonly Color Canvas = Color.FromArgb(244, 248, 251);
        public static Font Font(float size = 14, bool bold = false) { return new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
        public static GraphicsPath Rounded(RectangleF rectangle, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
            path.AddArc(rectangle.X, rectangle.Y, d, d, 180, 90);
            path.AddArc(rectangle.Right - d, rectangle.Y, d, d, 270, 90);
            path.AddArc(rectangle.Right - d, rectangle.Bottom - d, d, d, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
    public class Card : Panel
    {
        public Color Fill = Color.White;
        public Color Border = Theme.Line;
        public int Radius = 12;
        public Card() { DoubleBuffered = true; BackColor = Color.Transparent; SetStyle(ControlStyles.SupportsTransparentBackColor, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.Rounded(new RectangleF(0.5F, 0.5F, Width - 1, Height - 1), Radius))
            using (var brush = new SolidBrush(Fill))
            using (var pen = new Pen(Border)) { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
            base.OnPaint(e);
        }
    }
    public class ActionButton : Button
    {
        public string IconName = "";
        public bool Primary, Pill, Navigation, Selected;
        bool hover, pressed;
        public ActionButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent; Font = Theme.Font(14, true); Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) { pressed = false; Invalidate(); } }
        // Compose the background and shape together: WM_PRINT and Button's flat
        // state repaint paths do not consistently request a separate background pass.
        protected override void OnPaintBackground(PaintEventArgs e) { }
        void PaintParentBackground(PaintEventArgs e)
        {
            // Button's flat renderer can leave a rectangular state background in the
            // rounded corners. Repaint the parent surface, including custom artwork.
            if (Parent == null) { e.Graphics.Clear(Theme.Canvas); return; }
            var state = e.Graphics.Save();
            try
            {
                e.Graphics.TranslateTransform(-Left, -Top);
                using (var background = new PaintEventArgs(e.Graphics, new Rectangle(Left, Top, Width, Height)))
                {
                    InvokePaintBackground(Parent, background);
                    InvokePaint(Parent, background);
                }
            }
            finally { e.Graphics.Restore(state); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            PaintParentBackground(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rectangle = new RectangleF(0.5F, 0.5F, Width - 1, Height - 1);
            using (var path = Theme.Rounded(rectangle, Pill ? Height / 2 : 10))
            {
                if (Primary)
                {
                    using (var gradient = new LinearGradientBrush(ClientRectangle, Enabled ? Color.FromArgb(78, 184, 154) : Color.FromArgb(177, 199, 192), Enabled ? Color.FromArgb(5, 120, 105) : Color.FromArgb(142, 164, 158), 28)) e.Graphics.FillPath(gradient, path);
                }
                else
                {
                    Color fill = Selected ? Color.FromArgb(229, 247, 243) : hover ? Color.FromArgb(239, 248, 246) : Navigation ? Color.White : Color.FromArgb(253, 254, 255);
                    using (var brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
                    if (!Navigation) using (var pen = new Pen(Selected ? Color.FromArgb(202, 237, 230) : Theme.Line)) e.Graphics.DrawPath(pen, path);
                }
                if (Enabled && (pressed || (Primary && hover)))
                    using (var shade = new SolidBrush(Color.FromArgb(pressed ? 26 : 12, Color.Black))) e.Graphics.FillPath(shade, path);
            }
            Color color = !Enabled ? Color.FromArgb(147, 163, 174) : Primary ? Color.White : Selected ? Theme.Green : Theme.Ink;
            if (Navigation)
            {
                Icons.Draw(e.Graphics, IconName, new RectangleF((Width - 24) / 2, 15, 24, 24), color);
                TextRenderer.DrawText(e.Graphics, Text, Theme.Font(13, Selected), new Rectangle(0, 47, Width, 22), color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                int textWidth = TextRenderer.MeasureText(Text, Font).Width;
                int iconSpace = String.IsNullOrEmpty(IconName) ? 0 : 28;
                int start = (Width - textWidth - iconSpace) / 2;
                if (iconSpace > 0) Icons.Draw(e.Graphics, IconName, new RectangleF(start, (Height - 20) / 2, 20, 20), color);
                TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(start + iconSpace, 0, textWidth + 2, Height), color, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), color, BackColor);
        }
    }
    public sealed class IconView : Control
    {
        public string IconName = "wifi";
        public Color IconColor = Theme.Green;
        public bool Disc;
        public IconView() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Color.Transparent; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Disc)
            {
                using (var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(111, 204, 175), Color.FromArgb(4, 127, 106), 65)) e.Graphics.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
                Icons.Draw(e.Graphics, IconName, new RectangleF(Width * .24F, Height * .24F, Width * .52F, Height * .52F), Color.White);
            }
            else Icons.Draw(e.Graphics, IconName, new RectangleF(0, 0, Width, Height), IconColor);
        }
    }
    // Caption buttons draw their glyphs directly, so the hit area and the glyph
    // size stay independent of font metrics.
    public sealed class WindowButton : Control
    {
        public enum Glyph { Minimize, Maximize, Restore, Close }
        public Glyph Kind = Glyph.Minimize;
        bool hover, pressed;
        public WindowButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = false;
            AccessibleRole = AccessibleRole.PushButton;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) { pressed = false; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool closing = Kind == Glyph.Close;
            Color color = Theme.Ink;
            if (hover || pressed)
            {
                Color fill = closing
                    ? (pressed ? Color.FromArgb(196, 20, 34) : Color.FromArgb(226, 34, 51))
                    : (pressed ? Color.FromArgb(222, 231, 238) : Color.FromArgb(237, 243, 248));
                using (var path = Theme.Rounded(new RectangleF(2.5F, 2.5F, Width - 5F, Height - 5F), 6F))
                using (var brush = new SolidBrush(fill)) graphics.FillPath(brush, path);
                if (closing) color = Color.White;
            }
            float size = Math.Min(15F, Math.Min(Width - 16F, Height - 12F));
            float half = size / 2F;
            float cx = Width / 2F, cy = Height / 2F;
            using (var pen = new Pen(color, 1.7F) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
            {
                switch (Kind)
                {
                    case Glyph.Minimize:
                        graphics.DrawLine(pen, cx - half, cy + 4F, cx + half, cy + 4F);
                        break;
                    case Glyph.Maximize:
                        graphics.DrawRectangle(pen, cx - half, cy - half, size, size);
                        break;
                    case Glyph.Restore:
                        float offset = size * 0.26F;
                        graphics.DrawRectangle(pen, cx - half + offset, cy - half, size - offset, size - offset);
                        graphics.DrawLines(pen, new[]
                        {
                            new PointF(cx - half + offset, cy - half + offset),
                            new PointF(cx - half, cy - half + offset),
                            new PointF(cx - half, cy + half),
                            new PointF(cx + half - offset, cy + half)
                        });
                        break;
                    case Glyph.Close:
                        graphics.DrawLine(pen, cx - half, cy - half, cx + half, cy + half);
                        graphics.DrawLine(pen, cx + half, cy - half, cx - half, cy + half);
                        break;
                }
            }
        }
    }
    public sealed class HeroCard : Card
    {
        public bool ShowLaptop = true;
        public HeroCard() { Fill = Color.FromArgb(243, 251, 249); Border = Color.FromArgb(234, 245, 242); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var state = g.Save();
            using (var clip = Theme.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), Radius)) g.SetClip(clip);
            // Draw the reference's soft landscape and laptop directly as vector geometry.
            int x = Width - 245;
            using (var wave = new GraphicsPath())
            {
                wave.AddBezier(x - 145, Height, x - 30, Height - 175, x + 10, Height + 30, x + 100, Height - 118);
                wave.AddBezier(x + 100, Height - 118, x + 160, Height - 220, x + 180, Height + 5, x + 275, 30);
                wave.AddLine(x + 275, 30, Width, Height); wave.CloseFigure();
                using (var brush = new SolidBrush(Color.FromArgb(30, 103, 195, 170))) g.FillPath(brush, wave);
            }
            using (var haze = new SolidBrush(Color.FromArgb(55, 204, 239, 229))) g.FillEllipse(haze, x - 40, Height - 44, 290, 70);
            if (ShowLaptop && Width > 650)
            {
                using (var screen = new GraphicsPath())
                {
                    screen.AddPolygon(new PointF[] { new PointF(x + 30, 65), new PointF(x + 184, 65), new PointF(x + 171, Height - 31), new PointF(x + 16, Height - 31) });
                    using (var brush = new SolidBrush(Color.FromArgb(250, 254, 253))) g.FillPath(brush, screen);
                    using (var pen = new Pen(Color.FromArgb(102, 165, 155), 5)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, screen); }
                }
                using (var pen = new Pen(Color.FromArgb(150, 211, 198), 8)) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLine(pen, x - 5, Height - 21, x + 187, Height - 21); }
                Icons.Draw(g, "wifi", new RectangleF(x + 75, 93, 46, 46), Color.FromArgb(86, 183, 157));
                using (var leaf = new SolidBrush(Color.FromArgb(132, 207, 187)))
                {
                    g.FillEllipse(leaf, x + 212, Height - 112, 17, 44);
                    g.FillEllipse(leaf, x + 225, Height - 94, 27, 16);
                    g.FillEllipse(leaf, x + 198, Height - 80, 26, 14);
                }
                using (var pen = new Pen(Color.FromArgb(146, 204, 189), 3)) g.DrawLine(pen, x + 223, Height - 95, x + 220, Height - 30);
                using (var pot = new SolidBrush(Color.FromArgb(245, 252, 249))) g.FillPolygon(pot, new Point[] { new Point(x + 204, Height - 39), new Point(x + 239, Height - 39), new Point(x + 234, Height), new Point(x + 209, Height) });
            }
            g.Restore(state);
        }
    }
    public sealed class TimelineLog : ScrollableControl
    {
        readonly List<Tuple<string,string>> entries = new List<Tuple<string,string>>();
        public TimelineLog() { DoubleBuffered = true; AutoScroll = true; BackColor = Color.White; }
        public void Add(string time, string message)
        {
            entries.Add(Tuple.Create(time, message));
            if (entries.Count > 150) entries.RemoveAt(0);
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            int y = 13 + AutoScrollPosition.Y;
            using (var font = Theme.Font(13))
            foreach (var entry in entries)
            {
                int width = Math.Max(80, ClientSize.Width - 120);
                int height = Math.Max(42, TextRenderer.MeasureText(entry.Item2, font, new Size(width, 1000), TextFormatFlags.WordBreak).Height + 18);
                using (var pen = new Pen(Theme.Line, 1)) g.DrawLine(pen, 14, y, 14, y + height);
                using (var brush = new SolidBrush(Theme.Green)) g.FillEllipse(brush, 11, y + 9, 6, 6);
                TextRenderer.DrawText(g, entry.Item1, font, new Rectangle(30, y + 2, 67, height), Theme.Muted);
                TextRenderer.DrawText(g, entry.Item2, font, new Rectangle(104, y + 2, width, height), Color.FromArgb(103, 121, 148), TextFormatFlags.WordBreak);
                y += height;
            }
            AutoScrollMinSize = new Size(0, y - AutoScrollPosition.Y + 8);
        }
    }

    public sealed class PageDeck : Panel
    {
        readonly Panel header = new Panel(), content = new Panel();
        readonly List<Panel> pages = new List<Panel>();
        readonly List<DeckTab> buttons = new List<DeckTab>();
        int selected;
        public PageDeck()
        {
            BackColor = Color.White;
            content.Dock = DockStyle.Fill; content.Padding = new Padding(8, 12, 8, 6);
            header.Dock = DockStyle.Top; header.Height = 43;
            header.Paint += (sender, e) => { using(var pen = new Pen(Theme.Line)) e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1); };
            Controls.Add(content); Controls.Add(header);
        }
        public void AddPage(Panel page)
        {
            int index = pages.Count;
            page.Dock = DockStyle.Fill; page.Visible = false;
            pages.Add(page); content.Controls.Add(page);
            var button = new DeckTab { Text = page.Text, IconName = index == 0 ? "clock" : "globe", Bounds = new Rectangle(index * 132, 0, 128, 43) };
            button.Click += (sender, e) => SelectedIndex = index;
            header.Controls.Add(button); buttons.Add(button);
            SelectedIndex = selected;
        }
        public Panel SelectedTab { get { return pages.Count == 0 ? null : pages[selected]; } set { SelectedIndex = pages.IndexOf(value); } }
        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if(value < 0 || value >= pages.Count) return;
                selected = value;
                for(int i=0;i<pages.Count;i++) { pages[i].Visible=i==selected; buttons[i].Active=i==selected; buttons[i].Invalidate(); }
                pages[selected].BringToFront();
            }
        }
        sealed class DeckTab : Button
        {
            public string IconName;
            public bool Active;
            public DeckTab() { FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.PageTab; }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Color.White); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                Icons.Draw(e.Graphics,IconName,new RectangleF(9,11,21,21),Active?Theme.Green:Theme.Muted);
                TextRenderer.DrawText(e.Graphics,Text,Theme.Font(15,Active),new Rectangle(39,0,89,41),Active?Theme.Ink:Theme.Muted,TextFormatFlags.VerticalCenter);
                if(Active) using(var pen=new Pen(Theme.Green,3)) {pen.StartCap=pen.EndCap=LineCap.Round;e.Graphics.DrawLine(pen,40,40,99,40);}
                if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,Width-6,Height-9));
            }
        }
    }

    // Icon paths are compiled as resources and rendered offline.
    // 来源：Ant Design Icons（MIT）。它的绘图网格是 viewBox="64 64 896 896"，
    // 即 1024 网格四周各留 64 边距，所以缩放时要把原点减掉 64。
    public static class Icons
    {
        const float ViewBoxSize = 896F;
        const float ViewBoxOrigin = 64F;
        static readonly Dictionary<string, GraphicsPath> cache = new Dictionary<string, GraphicsPath>();
        public static void Draw(Graphics graphics, string name, RectangleF bounds, Color color)
        {
            if (String.IsNullOrEmpty(name)) return;
            GraphicsPath path;
            if (!cache.TryGetValue(name, out path))
            {
                using (var stream = typeof(Icons).Assembly.GetManifestResourceStream("CampusConnect.icons." + name + ".path"))
                {
                    if (stream == null) return;
                    using (var reader = new StreamReader(stream)) path = SvgPath.Parse(reader.ReadToEnd());
                }
                cache.Add(name, path);
            }
            float sx = bounds.Width / ViewBoxSize, sy = bounds.Height / ViewBoxSize;
            var state = graphics.Save();
            graphics.TranslateTransform(bounds.X - ViewBoxOrigin * sx, bounds.Y - ViewBoxOrigin * sy);
            graphics.ScaleTransform(sx, sy);
            using (var brush = new SolidBrush(color)) graphics.FillPath(brush, path);
            graphics.Restore(state);
        }
    }
    static class SvgPath
    {
        // SVG 允许圆弧的两个标志位不写分隔符（如 "a8 8 0 00-11.3 0"），而路径里的负数
        // 同样不带分隔，正则预分词无法区分。这里按字符扫描：标志位固定是单个 '0'/'1'。
        sealed class Scan
        {
            readonly string text;
            int at;
            public Scan(string text) { this.text = text; }
            public bool End { get { return at >= text.Length; } }
            void Skip() { while (at < text.Length && (Char.IsWhiteSpace(text[at]) || text[at] == ',')) at++; }
            public char PeekCommand()
            {
                Skip();
                return at < text.Length && Char.IsLetter(text[at]) ? text[at] : '\0';
            }
            public char Command() { return text[at++]; }
            public double Number()
            {
                Skip();
                int start = at;
                if (at < text.Length && (text[at] == '-' || text[at] == '+')) at++;
                while (at < text.Length && Char.IsDigit(text[at])) at++;
                if (at < text.Length && text[at] == '.') { at++; while (at < text.Length && Char.IsDigit(text[at])) at++; }
                if (at < text.Length && (text[at] == 'e' || text[at] == 'E'))
                {
                    int save = at;
                    at++;
                    if (at < text.Length && (text[at] == '-' || text[at] == '+')) at++;
                    if (at < text.Length && Char.IsDigit(text[at])) { while (at < text.Length && Char.IsDigit(text[at])) at++; }
                    else at = save;
                }
                if (at == start) throw new InvalidDataException("Unexpected SVG path data at offset " + start + ".");
                return Double.Parse(text.Substring(start, at - start), CultureInfo.InvariantCulture);
            }
            public bool Flag()
            {
                Skip();
                if (at >= text.Length) throw new InvalidDataException("Truncated SVG arc flags.");
                char flag = text[at++];
                if (flag != '0' && flag != '1') throw new InvalidDataException("Invalid SVG arc flag: " + flag);
                return flag == '1';
            }
        }
        public static GraphicsPath Parse(string data)
        {
            var scan = new Scan(data);
            var path = new GraphicsPath(FillMode.Winding);
            char command = 'M', previous = ' ';
            double x = 0, y = 0, sx = 0, sy = 0, cx = 0, cy = 0;
            while (!scan.End)
            {
                char next = scan.PeekCommand();
                if (next != '\0') command = scan.Command();
                bool relative = Char.IsLower(command);
                char op = Char.ToUpperInvariant(command);
                double ox = relative ? x : 0, oy = relative ? y : 0;
                double nx, ny, a, b, c, d;
                switch (op)
                {
                    case 'M': x = scan.Number()+ox; y = scan.Number()+oy; sx=x; sy=y; path.StartFigure(); command=relative?'l':'L'; break;
                    case 'L': nx=scan.Number()+ox; ny=scan.Number()+oy; path.AddLine((float)x,(float)y,(float)nx,(float)ny); x=nx;y=ny; break;
                    case 'H': nx=scan.Number()+ox; path.AddLine((float)x,(float)y,(float)nx,(float)y);x=nx;break;
                    case 'V': ny=scan.Number()+oy; path.AddLine((float)x,(float)y,(float)x,(float)ny);y=ny;break;
                    case 'C': a=scan.Number()+ox;b=scan.Number()+oy;c=scan.Number()+ox;d=scan.Number()+oy;nx=scan.Number()+ox;ny=scan.Number()+oy;path.AddBezier((float)x,(float)y,(float)a,(float)b,(float)c,(float)d,(float)nx,(float)ny);cx=c;cy=d;x=nx;y=ny;break;
                    case 'S': a=(previous=='C'||previous=='S')?2*x-cx:x;b=(previous=='C'||previous=='S')?2*y-cy:y;c=scan.Number()+ox;d=scan.Number()+oy;nx=scan.Number()+ox;ny=scan.Number()+oy;path.AddBezier((float)x,(float)y,(float)a,(float)b,(float)c,(float)d,(float)nx,(float)ny);cx=c;cy=d;x=nx;y=ny;break;
                    case 'Q': case 'T':
                        a=op=='Q'?scan.Number()+ox:(previous=='Q'||previous=='T'?2*x-cx:x);b=op=='Q'?scan.Number()+oy:(previous=='Q'||previous=='T'?2*y-cy:y);nx=scan.Number()+ox;ny=scan.Number()+oy;
                        path.AddBezier((float)x,(float)y,(float)(x+2*(a-x)/3),(float)(y+2*(b-y)/3),(float)(nx+2*(a-nx)/3),(float)(ny+2*(b-ny)/3),(float)nx,(float)ny);cx=a;cy=b;x=nx;y=ny;break;
                    case 'A':
                        a=scan.Number();b=scan.Number();c=scan.Number();bool large=scan.Flag(),sweep=scan.Flag();nx=scan.Number()+ox;ny=scan.Number()+oy;Arc(path,x,y,a,b,c,large,sweep,nx,ny);x=nx;y=ny;break;
                    case 'Z': path.CloseFigure();x=sx;y=sy;command='M';break;
                    default: throw new InvalidDataException("Unsupported SVG path command: "+op);
                }
                previous=op;
            }
            return path;
        }
        static void Arc(GraphicsPath path,double x,double y,double rx,double ry,double rotation,bool large,bool sweep,double nx,double ny)
        {
            rx=Math.Abs(rx);ry=Math.Abs(ry);
            if (rx==0||ry==0) {path.AddLine((float)x,(float)y,(float)nx,(float)ny);return;}
            if (Math.Abs(x-nx)+Math.Abs(y-ny)<.000001) return;
            double phi=rotation*Math.PI/180,cos=Math.Cos(phi),sin=Math.Sin(phi);
            double px=cos*(x-nx)/2+sin*(y-ny)/2,py=-sin*(x-nx)/2+cos*(y-ny)/2;
            double scale=px*px/(rx*rx)+py*py/(ry*ry);
            if(scale>1) {rx*=Math.Sqrt(scale);ry*=Math.Sqrt(scale);}
            double factor=(large==sweep?-1:1)*Math.Sqrt(Math.Max(0,(rx*rx*ry*ry-rx*rx*py*py-ry*ry*px*px)/(rx*rx*py*py+ry*ry*px*px)));
            double centerX=factor*rx*py/ry,centerY=-factor*ry*px/rx;
            double cx=cos*centerX-sin*centerY+(x+nx)/2,cy=sin*centerX+cos*centerY+(y+ny)/2;
            double start=Math.Atan2((py-centerY)/ry,(px-centerX)/rx),end=Math.Atan2((-py-centerY)/ry,(-px-centerX)/rx),delta=end-start;
            if(sweep&&delta<0)delta+=2*Math.PI;if(!sweep&&delta>0)delta-=2*Math.PI;
            int segments=(int)Math.Ceiling(Math.Abs(delta)/(Math.PI/2));
            Func<double,double,PointF> point=(u,v)=>new PointF((float)(cx+rx*cos*u-ry*sin*v),(float)(cy+rx*sin*u+ry*cos*v));
            for(int i=0;i<segments;i++)
            {
                double t=start+delta*i/segments,t2=start+delta*(i+1)/segments,k=4.0/3*Math.Tan((t2-t)/4);
                path.AddBezier(point(Math.Cos(t),Math.Sin(t)),point(Math.Cos(t)-k*Math.Sin(t),Math.Sin(t)+k*Math.Cos(t)),point(Math.Cos(t2)+k*Math.Sin(t2),Math.Sin(t2)-k*Math.Cos(t2)),point(Math.Cos(t2),Math.Sin(t2)));
            }
        }
    }
}
