using System.Runtime.CompilerServices;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Client.MirGraphics;
using SlimDX;
using SlimDX.Direct3D9;

namespace Client.MirControls;

/// <summary>Presentation only. The independent atlas contains verified local ten-year art.</summary>
internal static class Classic176FrontEndSkin
{
    internal static readonly Color Ink = Color.FromArgb(239, 218, 168);
    internal static readonly Color Edge = Color.FromArgb(142, 110, 62);
    private static readonly ConditionalWeakTable<MirControl, object> Applied = new();
    private static bool warned;

    internal static bool Available
    {
        get
        {
            try
            {
                if (Libraries.Classic176FrontEnd.GetSize(0) == new Size(800, 600) &&
                    Libraries.Classic176FrontEnd.GetSize(1) == new Size(800, 600) &&
                    Libraries.Classic176FrontEnd.GetSize(2) == new Size(43, 26)) return true;
                Warn("missing or invalid atlas entries 0=800x600, 1=800x600, 2=43x26");
            }
            catch (Exception ex) { Warn(ex.GetType().Name); }
            return false;
        }
    }

    private static void Warn(string reason)
    {
        if (warned) return;
        warned = true;
        CMain.SaveError("Classic176 front-end fallback: " + reason);
    }

    internal static bool Begin(MirControl control)
    {
        if (Applied.TryGetValue(control, out _)) return false;
        Applied.Add(control, new object());
        return true;
    }

    internal static void Background(MirControl parent, int index)
    {
        var art = new BackgroundArt(index, parent as MirAnimatedControl) { Size = parent.Size, NotControl = true };
        parent.InsertControl(0, art);
        var title = Label(parent, "热血传奇", new Rectangle(0, 16, parent.Size.Width, 54), 30, FontStyle.Bold);
        title.ForeColour = Color.FromArgb(244, 204, 111);
        Label(parent, "玛法大陆 · 再续征程", new Rectangle(0, 70, parent.Size.Width, 24), 10);
    }

    internal static void Panel(MirImageControl panel, Size size, string title)
    {
        panel.AutoSize = false;
        panel.Library = Libraries.Classic176FrontEnd;
        panel.Index = -1;
        panel.DrawImage = panel.DrawControlTexture = false;
        panel.Size = size;
        panel.Border = false;
        panel.BorderColour = Edge;
        panel.BeforeDraw += (_, _) => Tile(panel.DisplayLocation, panel.Size);
        var surface = new MetalSurface(MetalKind.Panel, !string.IsNullOrEmpty(title)) { Size = size, NotControl = true };
        surface.BeforeDraw += (_, _) => surface.State = panel.BorderColour == Color.Gold ? 1 : 0;
        panel.InsertControl(0, surface);
        if (!string.IsNullOrEmpty(title)) Label(panel, title, new Rectangle(14, 12, size.Width - 28, 32), 17, FontStyle.Bold);
    }

    internal static void Tile(Point origin, Size size)
    {
        for (int y = 0; y < size.Height; y += 26)
            for (int x = 0; x < size.Width; x += 43)
                Libraries.Classic176FrontEnd.Draw(2,
                    new Rectangle(0, 0, Math.Min(43, size.Width - x), Math.Min(26, size.Height - y)),
                    origin.Add(new Point(x, y)), Color.FromArgb(76, 76, 76), false);
    }

    internal static MirLabel Label(MirControl parent, string text, Rectangle bounds, float fontSize = 10, FontStyle style = FontStyle.Regular)
    {
        return new MirLabel
        {
            Parent = parent, Location = bounds.Location, Size = bounds.Size, AutoSize = false,
            Text = text, NotControl = true, BackColour = Color.Transparent, ForeColour = Ink,
            Font = new System.Drawing.Font(Settings.FontName, fontSize, style),
            DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding
        };
    }

    internal static void Button(MirButton button, string caption, Rectangle bounds)
    {
        button.AutoSize = false;
        button.Library = Libraries.Classic176FrontEnd;
        button.Index = button.HoverIndex = button.PressedIndex = -1;
        button.DrawImage = false;
        button.DrawControlTexture = false;
        button.BackColour = Color.FromArgb(255, 34, 28, 20);
        button.Border = false;
        button.BorderColour = Edge;
        button.Location = bounds.Location;
        button.Size = bounds.Size;
        var surface = new MetalSurface(MetalKind.Button) { Size = bounds.Size, NotControl = true };
        button.InsertControl(0, surface);
        // Reuse the button's own text label; no artwork-language overlay can match this atlas.
        button.Text = caption;
        button.CenterText = true;
        button.FontColour = Ink;
        var label = button.Controls.OfType<MirLabel>().First();
        label.Font = new System.Drawing.Font(Settings.FontName, 10F);
        label.DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        button.BeforeDraw += (_, _) =>
        {
            bool highlighted = MirControl.MouseControl == button || button.BorderColour == Color.Gold;
            label.ForeColour = !button.Enabled ? Color.Gray : highlighted ? Color.Gold : Ink;
            surface.State = !button.Enabled ? 3 : MirControl.ActiveControl == button ? 2 : highlighted ? 1 : 0;
            button.BackColour = Color.FromArgb(255, ActiveShade(34, 58, 83), ActiveShade(28, 43, 59), ActiveShade(20, 25, 31));
            int ActiveShade(int normal, int hover, int pressed) => !button.Enabled ? normal :
                MirControl.ActiveControl == button ? pressed : MirControl.MouseControl == button ? hover : normal;
        };
        button.MouseEnter += (_, _) => button.BackColour = Color.FromArgb(255, 58, 43, 25);
        button.MouseLeave += (_, _) => button.BackColour = Color.FromArgb(255, 34, 28, 20);
    }

    internal static void Input(MirTextBox input, Rectangle bounds)
    {
        // The native validator hides its border for empty text. Keep a permanent input well beneath it.
        var well = new MetalSurface(MetalKind.Input)
        {
            Location = new Point(bounds.X - 2, bounds.Y - 2), Size = new Size(bounds.Width + 4, bounds.Height + 4),
            NotControl = true, DrawControlTexture = true, BackColour = Color.FromArgb(255, 12, 11, 9), Border = true, BorderColour = Edge
        };
        input.Parent.InsertControl(input.Parent.Controls.IndexOf(input), well);
        input.Location = bounds.Location;
        input.Size = bounds.Size;
        input.BackColour = Color.FromArgb(255, 12, 11, 9);
        input.ForeColour = Color.FromArgb(245, 231, 202);
        input.Font = new System.Drawing.Font(Settings.FontName, 11F);
    }

    internal static Point Center(Size size) => new((Settings.ScreenWidth - size.Width) / 2, (Settings.ScreenHeight - size.Height) / 2);

    private sealed class BackgroundArt(int index, MirAnimatedControl timeline) : MirControl
    {
        protected internal override void DrawControl()
        {
            // Cover uniformly, with equal edge cropping; doors and emblems keep their proportions.
            float scale = Math.Max(Size.Width / 800f, Size.Height / 600f);
            var drawSize = new Size((int)Math.Ceiling(800 * scale), (int)Math.Ceiling(600 * scale));
            Libraries.Classic176FrontEnd.Draw(index == 0 ? Classic176GateTransition.Frame(timeline) : index,
                DisplayLocation.Add(new Point((Size.Width - drawSize.Width) / 2, (Size.Height - drawSize.Height) / 2)), drawSize, Color.White);
        }
    }

    private enum MetalKind { Panel, Button, Input }

    /// <summary>One cached texture per control; gradients and fine metal edges are not rebuilt each frame.</summary>
    private sealed class MetalSurface(MetalKind kind, bool header = false) : MirControl
    {
        private int state;
        internal int State { set { if (state == value) return; state = value; TextureValid = false; Redraw(); } }

        protected unsafe override void CreateTexture()
        {
            if (Size.Width < 2 || Size.Height < 2) return;
            if (TextureSize != Size) DisposeTexture();
            if (ControlTexture == null || ControlTexture.Disposed)
            {
                DXManager.ControlList.Add(this);
                ControlTexture = new Texture(DXManager.Device, Size.Width, Size.Height, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
                TextureSize = Size;
            }
            DataRectangle locked = ControlTexture.LockRectangle(0, LockFlags.Discard);
            using (var bitmap = new Bitmap(Size.Width, Size.Height, Size.Width * 4, PixelFormat.Format32bppArgb, locked.Data.DataPointer))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                var rect = new Rectangle(0, 0, Size.Width - 1, Size.Height - 1);
                Color top = kind == MetalKind.Panel ? Color.FromArgb(32, 31, 28) : kind == MetalKind.Input ? Color.FromArgb(9, 10, 10) :
                    state == 2 ? Color.FromArgb(34, 28, 20) : state == 1 ? Color.FromArgb(86, 66, 38) : state == 3 ? Color.FromArgb(34, 33, 30) : Color.FromArgb(56, 47, 34);
                Color bottom = kind == MetalKind.Panel ? Color.FromArgb(14, 15, 15) : kind == MetalKind.Input ? Color.FromArgb(17, 18, 17) :
                    state == 2 ? Color.FromArgb(60, 45, 27) : state == 1 ? Color.FromArgb(39, 31, 22) : Color.FromArgb(24, 24, 22);
                using (var fill = new LinearGradientBrush(rect, top, bottom, LinearGradientMode.Vertical))
                    g.FillRectangle(fill, kind == MetalKind.Panel ? new Rectangle(4, 4, Size.Width - 8, Size.Height - 8) : rect);
                using var outer = new Pen(Color.FromArgb(10, 10, 9));
                using var edge = new Pen(state == 1 ? Color.FromArgb(193, 157, 85) : state == 3 ? Color.FromArgb(73, 69, 57) : Edge);
                using var light = new Pen(state == 2 ? Color.FromArgb(45, 38, 27) : Color.FromArgb(111, 96, 65));
                using var shade = new Pen(Color.FromArgb(15, 16, 15));
                g.DrawRectangle(outer, rect);
                g.DrawRectangle(edge, 1, 1, Size.Width - 3, Size.Height - 3);
                g.DrawLine(light, 2, 2, Size.Width - 3, 2);
                g.DrawLine(light, 2, 2, 2, Size.Height - 3);
                g.DrawLine(shade, 2, Size.Height - 3, Size.Width - 3, Size.Height - 3);
                if (kind == MetalKind.Panel)
                {
                    g.DrawRectangle(shade, 5, 5, Size.Width - 11, Size.Height - 11);
                    using var subtle = new Pen(Color.FromArgb(69, 61, 44));
                    g.DrawRectangle(subtle, 6, 6, Size.Width - 13, Size.Height - 13);
                    if (header)
                    {
                        using var band = new LinearGradientBrush(new Rectangle(8, 8, Size.Width - 16, 40), Color.FromArgb(48, 40, 28), Color.FromArgb(24, 24, 22), LinearGradientMode.Vertical);
                        g.FillRectangle(band, 8, 8, Size.Width - 16, 40);
                        g.DrawLine(subtle, 18, 48, Size.Width - 19, 48);
                    }
                    foreach (int x in new[] { 9, Size.Width - 10 }) foreach (int y in new[] { 9, Size.Height - 10 })
                    {
                        g.DrawLine(edge, x, y, x + (x < 10 ? 14 : -14), y);
                        g.DrawLine(edge, x, y, x, y + (y < 10 ? 14 : -14));
                    }
                }
            }
            ControlTexture.UnlockRectangle(0);
            DXManager.Sprite.Flush();
            TextureValid = true;
        }

        protected internal override void DrawControl()
        {
            DrawControlTexture = true;
            base.DrawControl();
        }
    }
}
