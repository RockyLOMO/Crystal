using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Runtime.InteropServices;
using LibraryEditor;

// Uses Crystal's existing WIL reader and LIB writer. Never writes to the source directory.
if (args.Length != 2) throw new ArgumentException("Usage: <Prguse.wil> <new output directory>");
string source = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
if (Directory.Exists(output)) throw new IOException("Choose a new output directory.");
Directory.CreateDirectory(output);
var input = new WeMadeLibrary(source);
if (input.Images == null) throw new InvalidDataException("WIL/WIX pair not found.");
File.WriteAllText(Path.Combine(output, "images.json"), JsonSerializer.Serialize(
    input.Images.Select((im, i) => new { Index = i, im.Width, im.Height, im.X, im.Y }),
    new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
foreach (int i in new[] { 1, 4, 5, 6, 7 })
    input.Images[i].Image?.Save(Path.Combine(output, $"image-{i}.png"), ImageFormat.Png);
for (int start = 0; start < input.Images.Length; start += 100)
{
    using var sheet = new Bitmap(1200, 1000);
    using var g = Graphics.FromImage(sheet);
    g.Clear(Color.FromArgb(35, 35, 35));
    for (int i = start; i < Math.Min(start + 100, input.Images.Length); i++)
    {
        var im = input.Images[i];
        int x = (i - start) % 10 * 120, y = (i - start) / 10 * 100;
        g.DrawString($"{i}: {im.Width}x{im.Height}", SystemFonts.DefaultFont, Brushes.White, x, y);
        if (im.Image == null) continue;
        float scale = Math.Min(1f, Math.Min(116f / im.Width, 78f / im.Height));
        g.DrawImage(im.Image, x, y + 20, im.Width * scale, im.Height * scale);
    }
    sheet.Save(Path.Combine(output, $"sheet-{start:D4}.png"), ImageFormat.Png);
}
var lib = new MLibraryV2(Path.Combine(output, "Prguse176.Lib"));
foreach (var im in input.Images)
    lib.Images.Add(im.HasMask
        ? new MLibraryV2.MImage(im.Image, im.MaskImage) { X = im.X, Y = im.Y, MaskX = im.X, MaskY = im.Y }
        : new MLibraryV2.MImage(im.Image) { X = im.X, Y = im.Y });
lib.Count = lib.Images.Count;
lib.Save();
// Re-open the output to check the writer and all image payloads.
var check = new MLibraryV2(lib.FileName);
if (check.Count != input.Images.Length) throw new InvalidDataException("Converted image count differs.");
for (int i = 0; i < check.Count; i++)
{
    if (input.Images[i].Image != null &&
        (check.Images[i].Width != input.Images[i].Width || check.Images[i].Height != input.Images[i].Height))
        throw new InvalidDataException($"Converted dimensions differ at {i}.");
    if (input.Images[i].Image == null) continue;
    byte[] expected = Pixels(input.Images[i].Image), actual = Pixels(check.Images[i].Image);
    for (int p = 0; p < expected.Length; p += 4)
        if (expected[p] == 0 && expected[p + 1] == 0 && expected[p + 2] == 0) expected[p + 3] = 0;
    if (!expected.AsSpan().SequenceEqual(actual)) throw new InvalidDataException($"Converted pixels differ at {i}.");
}
Console.WriteLine($"Converted, re-read and pixel-verified {check.Count} images (black transparency applied): {lib.FileName}");

static byte[] Pixels(Bitmap bitmap)
{
    var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    try
    {
        var bytes = new byte[bitmap.Width * bitmap.Height * 4];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        return bytes;
    }
    finally { bitmap.UnlockBits(data); }
}
