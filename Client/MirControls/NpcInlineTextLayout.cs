using Client.MirGraphics;

namespace Client.MirControls;

/// <summary>Keep NPC text metadata intact, but draw each normal or styled run only once.</summary>
internal static class NpcInlineTextLayout
{
    internal readonly record struct Span(int Start, int Length, MirLabel Label);
    private const TextFormatFlags Format = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

    internal static void Apply(MirLabel line, string text, List<Span> spans)
    {
        line.Text = text; // NPC service detection still reads the complete resolved line.
        if (spans.Count == 0) return;
        line.DrawControlTexture = false;
        int cursor = 0;
        foreach (var span in spans.OrderBy(s => s.Start))
        {
            if (span.Start > cursor) AddPlain(cursor, span.Start - cursor);
            Place(span.Label, span.Start, span.Length, false);
            cursor = span.Start + span.Length;
        }
        if (cursor < text.Length) AddPlain(cursor, text.Length - cursor);

        int Advance(int length) => length == 0 ? 0 : TextRenderer.MeasureText(CMain.Graphics,
            text[..length], line.Font, new Size(int.MaxValue, int.MaxValue), Format).Width;

        void Place(MirLabel label, int start, int length, bool child)
        {
            int x = Advance(start);
            label.AutoSize = false;
            label.DrawFormat = Format;
            label.Location = (child ? Point.Empty : line.Location).Add(new Point(x, 0));
            label.Visible = x < line.Size.Width;
            label.Size = new Size(Math.Max(1, Math.Min(Advance(start + length) - x + 2, line.Size.Width - x)), line.Size.Height);
        }

        void AddPlain(int start, int length)
        {
            // Font setters scale from points to logical pixels; undo that scaling before cloning.
            using var font = new Font(line.Font.Name, line.Font.Size * MirControl.FontDpiX / 96f, line.Font.Style);
            var label = new MirLabel
            {
                Parent = line, Text = text.Substring(start, length), Font = font, NotControl = true,
                ForeColour = line.ForeColour, BackColour = line.BackColour,
                OutLine = line.OutLine, OutLineColour = line.OutLineColour
            };
            Place(label, start, length, true);
        }
    }
}
