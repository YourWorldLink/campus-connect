using System;
using System.Drawing;
using System.Windows.Forms;
using CampusConnect;

static class RenderingTests
{
    sealed class ProbeButton : ActionButton
    {
        public void State(string state)
        {
            Enabled = state != "disabled";
            Selected = state == "selected";
            OnMouseLeave(EventArgs.Empty);
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
            if (state == "hover" || state == "pressed") OnMouseEnter(EventArgs.Empty);
            if (state == "pressed") OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
        }
        public Bitmap PaintOverStalePixels()
        {
            var bitmap = new Bitmap(Width, Height);
            using (var graphics = Graphics.FromImage(bitmap))
            using (var args = new PaintEventArgs(graphics, ClientRectangle))
            {
                graphics.Clear(Color.Magenta);
                OnPaintBackground(args);
                OnPaint(args);
            }
            return bitmap;
        }
    }
    [STAThread]
    static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            int checks = 0;
            using (var host = new Form { Size = new Size(440, 300), BackColor = Theme.Canvas, ShowInTaskbar = false, Opacity = 0 })
            using (var parent = new HeroCard { Bounds = new Rectangle(10, 10, 400, 230) })
            using (var button = new ProbeButton { Text = "连接", IconName = "wifi", Location = new Point(30, 40) })
            {
                host.Controls.Add(parent);
                // An asymmetric parent drawing detects incorrect background offsets.
                parent.Paint += (s, e) => { using (var brush = new SolidBrush(Color.LightBlue)) e.Graphics.FillRectangle(brush, 25, 35, 65, 35); };
                host.Show(); Application.DoEvents();
                using (var background = new Bitmap(parent.Width, parent.Height))
                {
                    parent.DrawToBitmap(background, parent.ClientRectangle);
                    parent.Controls.Add(button); button.CreateControl();
                    foreach (bool pill in new[] { false, true })
                    foreach (bool primary in new[] { false, true })
                    foreach (var size in new[] { new Size(60, 77), new Size(148, 43), new Size(210, 55) })
                    foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "selected" })
                    {
                        button.Pill = pill; button.Primary = primary; button.Size = size; button.State(state);
                        using (var painted = button.PaintOverStalePixels())
                        using (var native = new Bitmap(button.Width, button.Height))
                        {
                            button.DrawToBitmap(native, button.ClientRectangle);
                            foreach (var point in new[] { new Point(0, 0), new Point(size.Width - 1, 0), new Point(0, size.Height - 1), new Point(size.Width - 1, size.Height - 1), new Point(1, 1) })
                            {
                                var expected = background.GetPixel(button.Left + point.X, button.Top + point.Y);
                                if (painted.GetPixel(point.X, point.Y).ToArgb() != expected.ToArgb() || native.GetPixel(point.X, point.Y).ToArgb() != expected.ToArgb())
                                    throw new Exception("Rounded corner retained stale pixels: " + state + ", pill=" + pill + ", primary=" + primary + ", size=" + size + ", point=" + point + ", expected=" + expected + ", paint=" + painted.GetPixel(point.X, point.Y) + ", native=" + native.GetPixel(point.X, point.Y));
                            }
                            // The button must remain visibly painted inside its shape.
                            if (painted.GetPixel(size.Width / 2, size.Height - 5).ToArgb() == Color.Magenta.ToArgb()) throw new Exception("Button interior was not repainted.");
                            checks++;
                        }
                    }
                }
            }
            Console.WriteLine("PASS " + checks + " rendering states: rounded/ pill, primary/ secondary, hover/ press/ disabled/ selected, resized, custom parent artwork and WM_PRINT");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
