using MiniEnvi.IO;

namespace MiniEnvi.Processing;

public static class StretchRenderer
{
    public static RenderedImage Render(EnviDataset dataset, DisplayMode mode, int[] bands, StretchMode stretch)
    {
        if (bands.Length == 0) throw new ArgumentException("没有选择波段。", nameof(bands));
        var width = dataset.Header.Samples;
        var height = dataset.Header.Lines;
        var needed = mode == DisplayMode.Rgb ? 3 : 1;
        if (bands.Length < needed) throw new ArgumentException("RGB 显示需要三个波段。", nameof(bands));

        var source = new double[needed][];
        var ranges = new (double Low, double High)[needed];
        for (var i = 0; i < needed; i++)
        {
            source[i] = dataset.ReadBand(bands[i]);
            ranges[i] = FindRange(source[i], stretch);
        }

        var pixels = new byte[checked(width * height * 4)];
        for (var i = 0; i < width * height; i++)
        {
            var p = i * 4;
            if (mode == DisplayMode.Rgb)
            {
                pixels[p] = Scale(source[2][i], ranges[2], stretch);
                pixels[p + 1] = Scale(source[1][i], ranges[1], stretch);
                pixels[p + 2] = Scale(source[0][i], ranges[0], stretch);
            }
            else
            {
                var value = Scale(source[0][i], ranges[0], stretch);
                if (mode == DisplayMode.Grayscale)
                    pixels[p] = pixels[p + 1] = pixels[p + 2] = value;
                else
                {
                    var (r, g, b) = Pseudocolor(value);
                    pixels[p] = b;
                    pixels[p + 1] = g;
                    pixels[p + 2] = r;
                }
            }
            pixels[p + 3] = 255;
        }
        return new RenderedImage(width, height, pixels);
    }

    private static (double Low, double High) FindRange(double[] values, StretchMode mode)
    {
        if (mode == StretchMode.None) return (0, 255);
        var step = Math.Max(1, values.Length / 500_000);
        var sample = new List<double>(Math.Min(values.Length, 500_000));
        for (var i = 0; i < values.Length; i += step)
            if (double.IsFinite(values[i])) sample.Add(values[i]);
        if (sample.Count == 0) return (0, 1);

        if (mode == StretchMode.StandardDeviation)
        {
            var mean = sample.Average();
            var variance = sample.Sum(x => (x - mean) * (x - mean)) / sample.Count;
            var sigma = Math.Sqrt(variance);
            return NormalizeRange(mean - 2 * sigma, mean + 2 * sigma);
        }

        sample.Sort();
        if (mode == StretchMode.Percent2)
        {
            var low = sample[(int)Math.Floor((sample.Count - 1) * 0.02)];
            var high = sample[(int)Math.Ceiling((sample.Count - 1) * 0.98)];
            return NormalizeRange(low, high);
        }
        return NormalizeRange(sample[0], sample[^1]);
    }

    private static (double Low, double High) NormalizeRange(double low, double high) =>
        high > low ? (low, high) : (low, low + 1);

    private static byte Scale(double value, (double Low, double High) range, StretchMode mode)
    {
        if (!double.IsFinite(value)) return 0;
        if (mode == StretchMode.None) return (byte)Math.Clamp(Math.Round(value), 0, 255);
        return (byte)Math.Clamp(Math.Round((value - range.Low) * 255 / (range.High - range.Low)), 0, 255);
    }

    private static (byte R, byte G, byte B) Pseudocolor(byte value)
    {
        var t = value / 255.0;
        var r = Math.Clamp(1.5 - Math.Abs(4 * t - 3), 0, 1);
        var g = Math.Clamp(1.5 - Math.Abs(4 * t - 2), 0, 1);
        var b = Math.Clamp(1.5 - Math.Abs(4 * t - 1), 0, 1);
        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
