using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using LibraryEditor;

// Explicit input/output converter: excluded from the *-tests working-directory bootstrap.
if (args.Length != 2) throw new ArgumentException("Pass ten-year Data directory and a NEW output directory.");
string source = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
if (Directory.Exists(output)) throw new IOException("Output already exists; refusing to replace assets.");
string gatePath = Path.Combine(source, "ChrSel.wil"), stonePath = Path.Combine(source, "Prguse.wil");
string buttonsPath = Path.Combine(source, "Prguse3.wil");
var gates = new WeMadeLibrary(gatePath);
var interfaceArt = new WeMadeLibrary(stonePath);
var buttonsArt = new WeMadeLibrary(buttonsPath);
if (gates.Images is not { Length: > 32 } || gates.Images[22].Image is not { Width: 800, Height: 600 } ||
    interfaceArt.Images is not { Length: > 1 } || interfaceArt.Images[1].Image is not { Width: 800, Height: 251 })
    throw new InvalidDataException("Source must match the inspected ten-year WIL mapping (ChrSel 22 and Prguse 1).");
for (int index=23; index<=32; index++)
    if (gates.Images[index].Image is not { Width: 496, Height: 361 })
        throw new InvalidDataException($"Gate patch {index} must be 496x361.");
Directory.CreateDirectory(output);
using var login = new Bitmap(gates.Images[22].Image);
using var select = CompositeGate(32);
using var stone = interfaceArt.Images[1].Image.Clone(new Rectangle(666, 109, 43, 26), PixelFormat.Format32bppArgb);
var bitmaps = new List<Bitmap> { login, select, stone };
for(int index=23; index<=32; index++) bitmaps.Add(CompositeGate(index));
using var cave = new Bitmap(interfaceArt.Images[65].Image);
using (var graphics = Graphics.FromImage(cave))
    for(int y=449;y<586;y+=35) for(int x=250;x<570;x+=70)
    {
        var patch = new Rectangle(x,y,Math.Min(70,570-x),Math.Min(35,586-y));
        graphics.DrawImage(interfaceArt.Images[65].Image,patch,new Rectangle(260,480,patch.Width,patch.Height),GraphicsUnit.Pixel);
    }
bitmaps.Add(cave); // 13: native stone cave, central baked captions cleared for real controls.
bitmaps.Add(new Bitmap(interfaceArt.Images[73].Image)); // 14: native creation frame.
for(int index=40; index<=272; index++)
{
    Bitmap frame = gates.Images[index].Image is { } image ? new Bitmap(image) : new Bitmap(1,1,PixelFormat.Format32bppArgb);
    if(index is >=220 and <=232 || index is >=260 and <=272)
    {
        var canvas = new Bitmap(index < 240 ? 176 : 196, index < 240 ? 311 : 314, PixelFormat.Format32bppArgb);
        using(var graphics = Graphics.FromImage(canvas)) graphics.DrawImageUnscaled(frame, index < 240 ? 30 : 23, index < 240 ? 14 : 20);
        frame.Dispose(); frame=canvas;
    }
    bitmaps.Add(frame); // Native ChrSel index - 25.
}
foreach(int index in new[]{66,67,79,80,74,55,75,56,76,57,77,58,78,59})
    bitmaps.Add(new Bitmap(interfaceArt.Images[index].Image)); // 248..261.
bitmaps.Add(interfaceArt.Images[65].Image.Clone(new Rectangle(0,0,38,600),PixelFormat.Format32bppArgb)); //262 stone outside the baked metadata panel.
bitmaps.Add(new Bitmap(buttonsArt.Images[2].Image)); //263 neutral button.
bitmaps.Add(new Bitmap(buttonsArt.Images[3].Image)); //264 highlighted button.
var library = new MLibraryV2(Path.Combine(output, "Classic176FrontEnd.Lib"));
foreach (var bitmap in bitmaps) library.Images.Add(new MLibraryV2.MImage(bitmap) { X = 0, Y = 0 });
library.Count = library.Images.Count;
library.Save();
var check = new MLibraryV2(library.FileName);
if (check.Count != 265) throw new InvalidDataException("Atlas must contain 265 entries.");
for (int i = 0; i < bitmaps.Count; i++)
{
    using var empty = new Bitmap(1,1,PixelFormat.Format32bppArgb);
    var decoded = check.Images[i].Image ?? empty;
    byte[] before = Pixels(bitmaps[i]), after = Pixels(decoded);
    for (int p = 0; p < before.Length; p += 4)
        if (before[p] == 0 && before[p + 1] == 0 && before[p + 2] == 0) before[p + 3] = 0;
    if (!before.AsSpan().SequenceEqual(after)) throw new InvalidDataException($"Pixel round-trip mismatch at {i}.");
    decoded.Save(Path.Combine(output, $"frontend-{i}.png"), ImageFormat.Png);
}
File.WriteAllText(Path.Combine(output, "assets.json"), JsonSerializer.Serialize(new
{
    Kind = "native two-slot cave and stone creation frame, paged to retain four Crystal roles",
    SourceGate = new { Path = gatePath, SHA256 = Hash(gatePath), Index = 22, Width = 800, Height = 600 },
    SourceStone = new { Path = stonePath, SHA256 = Hash(stonePath), Index = 1, Rect = new[] { 666, 109, 43, 26 } },
    SourceDoor = new { Indices = Enumerable.Range(23,10).ToArray(), Position=new[]{153,96}, PatchSize=new[]{496,361}, PreserveOpaqueNearBlack=true },
    SourceRoles = new { Background=65,Creation=73,SpriteIndices="ChrSel40..272 -> atlas15..247",FemaleRevealPadding="220..232 +(30,14) in176x311; 260..272 +(23,20) in196x314",ButtonsPath=buttonsPath,ButtonsSHA256=Hash(buttonsPath) },
    Entries = new[] { "0: closed login gate", "1: opened select gate (22 + patch32)", "2: unlabelled stone", "3..12: full 800x600 gate composites", "13: native cave, cleared central captions", "14: native creation", "15..247: native ChrSel40..272", "248..261: native selects/arrows/class/gender", "262: side stone", "263..264: unlabelled stone buttons" },
    Playback = new { FrameDelayMs=120, PlaybackSteps=12, DurationMs=1440, FinalFrameHoldMs=360, Loop=false },
    Offset = "all destination offsets reset to zero; source WIL (7,-44) is not carried into the background",
    License = "user-provided local client; redistribution rights not established by this tool",
    PixelRoundTripVerified = true, AtlasSHA256 = Hash(library.FileName)
}, new JsonSerializerOptions { WriteIndented = true }));
foreach(var frame in bitmaps.Skip(3)) frame.Dispose();
Console.WriteLine($"Verified 265 independent atlas entries: {library.FileName}");

Bitmap CompositeGate(int patch)
{
    var result = new Bitmap(gates.Images[22].Image);
    using var graphics=Graphics.FromImage(result);
    // Exact native placement from invariant border pixel matching. RGB(8,0,0) in the opening is opaque.
    graphics.DrawImageUnscaled(gates.Images[patch].Image,153,96);
    return result;
}

static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
static byte[] Pixels(Bitmap bitmap)
{
    var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    try
    {
        byte[] result = new byte[bitmap.Width * bitmap.Height * 4];
        for (int row = 0; row < bitmap.Height; row++) Marshal.Copy(IntPtr.Add(data.Scan0, row * data.Stride), result, row * bitmap.Width * 4, bitmap.Width * 4);
        return result;
    }
    finally { bitmap.UnlockBits(data); }
}
