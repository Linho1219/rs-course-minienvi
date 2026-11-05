namespace MiniEnvi.Processing;

public sealed record ImageStatistics(
    long Count,
    double Minimum,
    double Maximum,
    double Mean,
    double StandardDeviation,
    long[] Histogram)
{
    public static ImageStatistics Calculate(double[] values)
    {
        var finite = values.Where(double.IsFinite).ToArray();
        if (finite.Length == 0) return new ImageStatistics(0, double.NaN, double.NaN, double.NaN, double.NaN, new long[256]);
        var min = finite.Min();
        var max = finite.Max();
        var mean = finite.Average();
        var variance = finite.Sum(x => (x - mean) * (x - mean)) / finite.Length;
        var histogram = new long[256];
        var span = max - min;
        foreach (var value in finite)
        {
            var bin = span <= 0 ? 0 : (int)Math.Clamp((value - min) / span * 255, 0, 255);
            histogram[bin]++;
        }
        return new ImageStatistics(finite.Length, min, max, mean, Math.Sqrt(variance), histogram);
    }
}
