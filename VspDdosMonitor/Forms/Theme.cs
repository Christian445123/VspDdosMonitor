using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace VspDdosMonitor.Forms
{
    /// <summary>Dunkles Design passend zum Web-Dashboard (gleiche Farbpalette).</summary>
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(0x0F, 0x14, 0x20);
        public static readonly Color Panel = Color.FromArgb(0x17, 0x1D, 0x2C);
        public static readonly Color Panel2 = Color.FromArgb(0x1E, 0x25, 0x36);
        public static readonly Color Border = Color.FromArgb(0x2A, 0x32, 0x47);
        public static readonly Color Text = Color.FromArgb(0xE6, 0xE9, 0xF0);
        public static readonly Color Muted = Color.FromArgb(0x8B, 0x94, 0xAB);
        public static readonly Color Accent = Color.FromArgb(0x4F, 0x8C, 0xFF);
        public static readonly Color Danger = Color.FromArgb(0xE7, 0x4C, 0x3C);
        public static readonly Color Warn = Color.FromArgb(0xF3, 0x9C, 0x12);
        public static readonly Color Success = Color.FromArgb(0x2E, 0xCC, 0x71);
        public static readonly Color Selection = Color.FromArgb(0x26, 0x3F, 0x73);

        public static readonly Color DangerBack = Color.FromArgb(0x3A, 0x1F, 0x24);
        public static readonly Color DangerText = Color.FromArgb(0xFF, 0x8D, 0x80);
        public static readonly Color SuccessBack = Color.FromArgb(0x16, 0x35, 0x2A);
        public static readonly Color SuccessText = Color.FromArgb(0x5B, 0xE4, 0x9B);
        public static readonly Color WarnBack = Color.FromArgb(0x3A, 0x30, 0x1A);
        public static readonly Color WarnText = Color.FromArgb(0xFF, 0xCF, 0x6B);

        public static Font BaseFont => new Font("Segoe UI", 9.5f);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string? subIdList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// <summary>Markiert einen Button als Hauptaktion (Akzentfarbe).</summary>
        public static void Primary(Button b) => b.Tag = "primary";

        public static void Apply(Control root)
        {
            if (root is Form form)
            {
                form.BackColor = Bg;
                form.ForeColor = Text;
                form.Font = BaseFont;
                form.HandleCreated += (_, _) =>
                {
                    var on = 1;
                    DwmSetWindowAttribute(form.Handle, 20, ref on, sizeof(int));   // dunkle Titelleiste (Windows 10 2004+/11)
                    DwmSetWindowAttribute(form.Handle, 19, ref on, sizeof(int));
                };
            }
            Walk(root);
        }

        private static void Walk(Control c)
        {
            Style(c);
            foreach (Control child in c.Controls) Walk(child);
        }

        private static void Style(Control c)
        {
            switch (c)
            {
                case Form:
                    break;
                case MenuStrip menu:
                    menu.BackColor = Panel;
                    menu.ForeColor = Text;
                    menu.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
                    foreach (ToolStripItem item in menu.Items) StyleMenuItem(item);
                    break;
                case Button b:
                    var primary = b.Tag as string == "primary";
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = primary ? Accent : Border;
                    b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0x6A, 0x9F, 0xFF) : Border;
                    b.FlatAppearance.MouseDownBackColor = Selection;
                    b.BackColor = primary ? Accent : Panel2;
                    b.ForeColor = primary ? Color.White : Text;
                    b.Cursor = Cursors.Hand;
                    break;
                case TextBox t:
                    t.BackColor = Panel2;
                    t.ForeColor = Text;
                    t.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox cb:
                    cb.FlatStyle = FlatStyle.Flat;
                    cb.BackColor = Panel2;
                    cb.ForeColor = Text;
                    break;
                case ListView lv:
                    StyleList(lv);
                    break;
                case Chart chart:
                    StyleChart(chart);
                    break;
                case CheckBox cb:
                    cb.ForeColor = Text;
                    break;
                case Label l:
                    l.BackColor = Color.Transparent;
                    l.ForeColor = l.ForeColor == Color.DimGray ? Muted : (l.ForeColor == SystemColors.ControlText ? Text : l.ForeColor);
                    break;
                case GroupBox g:
                    g.ForeColor = Muted;
                    g.BackColor = Bg;
                    break;
                case SplitContainer sc:
                    sc.BackColor = Border;
                    sc.Panel1.BackColor = Bg;
                    sc.Panel2.BackColor = Bg;
                    break;
                case TabPage tp:
                    tp.BackColor = Bg;
                    tp.UseVisualStyleBackColor = false;
                    break;
                case ProgressBar:
                    break;
                default:
                    if (c is UserControl || c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel || c is TabControl)
                    {
                        c.BackColor = Bg;
                        c.ForeColor = Text;
                    }
                    break;
            }
        }

        private static void StyleMenuItem(ToolStripItem item)
        {
            item.ForeColor = Text;
            item.BackColor = Panel;
            if (item is ToolStripDropDownItem dd)
            {
                foreach (ToolStripItem child in dd.DropDownItems) StyleMenuItem(child);
            }
        }

        private static void StyleChart(Chart chart)
        {
            chart.BackColor = Panel;
            chart.ForeColor = Text;
            foreach (var area in chart.ChartAreas)
            {
                area.BackColor = Panel;
                foreach (var axis in new[] { area.AxisX, area.AxisY, area.AxisX2, area.AxisY2 })
                {
                    axis.LineColor = Border;
                    axis.MajorGrid.LineColor = Border;
                    axis.MajorTickMark.LineColor = Border;
                    axis.LabelStyle.ForeColor = Muted;
                    axis.TitleForeColor = Muted;
                }
            }
            foreach (var legend in chart.Legends)
            {
                legend.BackColor = Panel;
                legend.ForeColor = Text;
            }
        }

        public static void StyleList(ListView lv)
        {
            lv.OwnerDraw = true;
            lv.HandleCreated += (_, _) => SetWindowTheme(lv.Handle, "DarkMode_Explorer", null);   // dunkle Scrollbalken
            if (lv.IsHandleCreated) SetWindowTheme(lv.Handle, "DarkMode_Explorer", null);
            lv.BackColor = Panel;
            lv.ForeColor = Text;
            lv.BorderStyle = BorderStyle.None;
            lv.GridLines = false;
            typeof(ListView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(lv, true);

            lv.DrawColumnHeader += (_, e) =>
            {
                using (var back = new SolidBrush(Panel2)) e.Graphics.FillRectangle(back, e.Bounds);
                using (var pen = new Pen(Border)) e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", lv.Font, Rectangle.Inflate(e.Bounds, -6, 0), Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            lv.DrawItem += (_, e) => { /* Zellen werden in DrawSubItem gezeichnet */ };
            lv.DrawSubItem += (_, e) =>
            {
                var selected = e.Item != null && e.Item.Selected;
                var back = selected ? Selection : (e.ItemIndex % 2 == 0 ? Panel : Color.FromArgb(0x1B, 0x22, 0x33));
                using (var brush = new SolidBrush(back)) e.Graphics.FillRectangle(brush, e.Bounds);
                var fore = e.Item == null ? Text : (e.Item.ForeColor == SystemColors.WindowText ? Text : e.Item.ForeColor);
                TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", lv.Font, Rectangle.Inflate(e.Bounds, -6, 0), fore,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
        }

        private sealed class DarkColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Panel2;
            public override Color MenuItemSelectedGradientBegin => Panel2;
            public override Color MenuItemSelectedGradientEnd => Panel2;
            public override Color MenuItemBorder => Border;
            public override Color MenuBorder => Border;
            public override Color ToolStripDropDownBackground => Panel;
            public override Color ImageMarginGradientBegin => Panel;
            public override Color ImageMarginGradientMiddle => Panel;
            public override Color ImageMarginGradientEnd => Panel;
            public override Color MenuStripGradientBegin => Panel;
            public override Color MenuStripGradientEnd => Panel;
            public override Color MenuItemPressedGradientBegin => Panel2;
            public override Color MenuItemPressedGradientEnd => Panel2;
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Border;
        }
    }

    /// <summary>TabControl mit komplett eigener Darstellung (dunkel, Reiter am rechten Rand, waagerechte Beschriftung).</summary>
    internal sealed class DarkTabControl : TabControl
    {
        public DarkTabControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            Alignment = TabAlignment.Right;
            SizeMode = TabSizeMode.Fixed;
            ItemSize = new Size(46, 160);
            Multiline = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Bg);
            for (var i = 0; i < TabCount; i++)
            {
                var rect = GetTabRect(i);
                var selected = i == SelectedIndex;
                using (var back = new SolidBrush(selected ? Theme.Accent : Theme.Panel))
                {
                    e.Graphics.FillRectangle(back, rect);
                }
                if (!selected)
                {
                    using var pen = new Pen(Theme.Border);
                    e.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                }
                using var font = new Font("Segoe UI", 10f, selected ? FontStyle.Bold : FontStyle.Regular);
                TextRenderer.DrawText(e.Graphics, TabPages[i].Text, font, Rectangle.Inflate(rect, -14, 0),
                    selected ? Color.White : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }
    }
}
