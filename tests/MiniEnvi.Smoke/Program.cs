using MiniEnvi.IO;
using MiniEnvi.Processing;
using System.Buffers.Binary;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: MiniEnvi.Smoke <input.hdr> <output-directory>");
    return 2;
}

Directory.CreateDirectory(args[1]);
foreach (var interleave in new[] { "bsq", "bil", "bip" })
foreach (var byteOrder in new[] { 0, 1 })
    TestSyntheticLayout(args[1], interleave, byteOrder);

using var dataset = EnviDataset.Open(args[0]);
Require(dataset.Header.Samples == 598, "samples");
Require(dataset.Header.Lines == 563, "lines");
Require(dataset.Header.Bands == 191, "bands");
Require(dataset.Header.DataType == 4, "data type");
Require(dataset.Header.Interleave == "bsq", "interleave");

var band0 = dataset.ReadWindow(0, 10, 20, 8, 6);
Require(band0.Length == 48 && band0.All(double.IsFinite), "window read");
var stats = ImageStatistics.Calculate(dataset.ReadBand(11));
Require(stats.Count == 598L * 563 && stats.Maximum > stats.Minimum, "statistics");

var rendered = StretchRenderer.Render(dataset, DisplayMode.Rgb, [28, 19, 11], StretchMode.Percent2);
Require(rendered.Width == 598 && rendered.Height == 563, "render dimensions");
Require(rendered.Pixels.Length == 598 * 563 * 4, "render buffer");
Require(rendered.Pixels.Where((_, i) => i % 4 != 3).Distinct().Count() > 20, "render content");

var cropHeader = Path.Combine(args[1], "smoke_crop.hdr");
await EnviWriter.WriteCropAsync(dataset, cropHeader, 10, 20, 8, 6);
using var crop = EnviDataset.Open(cropHeader);
Require(crop.Header.Samples == 8 && crop.Header.Lines == 6 && crop.Header.Bands == 191, "crop header");
var croppedBand0 = crop.ReadBand(0);
Require(band0.SequenceEqual(croppedBand0), "crop values");

Console.WriteLine($"PASS: {dataset.Name}, {dataset.Header.Samples}x{dataset.Header.Lines}x{dataset.Header.Bands}");
Console.WriteLine($"Band 12 mean={stats.Mean:G8}, std={stats.StandardDeviation:G8}");
Console.WriteLine($"Crop reopened: {crop.DataPath}");
return 0;

static void Require(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("Smoke test failed: " + name);
}

static void TestSyntheticLayout(string directory, string interleave, int byteOrder)
{
    const int samples = 3, lines = 2, bands = 2;
    var stem = Path.Combine(directory, $"synthetic_{interleave}_{byteOrder}");
    File.WriteAllText(stem + ".hdr", $$"""
        ENVI
        description = {synthetic
          multiline header}
        samples = {{samples}}
        lines = {{lines}}
        bands = {{bands}}
        header offset = 0
        file type = ENVI Standard
        data type = 4
        interleave = {{interleave}}
        byte order = {{byteOrder}}
        band names = {First, Second}
        """);

    var order = new List<(int Band, int Row, int Col)>();
    if (interleave == "bsq")
        for (var b = 0; b < bands; b++) for (var r = 0; r < lines; r++) for (var c = 0; c < samples; c++) order.Add((b, r, c));
    else if (interleave == "bil")
        for (var r = 0; r < lines; r++) for (var b = 0; b < bands; b++) for (var c = 0; c < samples; c++) order.Add((b, r, c));
    else
        for (var r = 0; r < lines; r++) for (var c = 0; c < samples; c++) for (var b = 0; b < bands; b++) order.Add((b, r, c));

    var bytes = new byte[order.Count * 4];
    for (var i = 0; i < order.Count; i++)
    {
        var (band, row, col) = order[i];
        var bits = BitConverter.SingleToInt32Bits(100 * band + 10 * row + col + 0.5f);
        if (byteOrder == 0) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4, 4), bits);
        else BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(i * 4, 4), bits);
    }
    File.WriteAllBytes(stem + ".dat", bytes);

    using var image = EnviDataset.Open(stem + ".hdr");
    for (var band = 0; band < bands; band++)
    {
        var actual = image.ReadBand(band);
        var expected = Enumerable.Range(0, samples * lines)
            .Select(i => 100 * band + 10 * (i / samples) + i % samples + 0.5).ToArray();
        Require(actual.SequenceEqual(expected), $"{interleave} byteOrder={byteOrder}");
    }
}
