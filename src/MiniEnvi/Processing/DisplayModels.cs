namespace MiniEnvi.Processing;

public enum DisplayMode
{
    Grayscale,
    Pseudocolor,
    Rgb
}

public enum StretchMode
{
    Percent2,
    MinMax,
    StandardDeviation,
    None
}

public static class DisplayNames
{
    public static string Name(this StretchMode mode) => mode switch
    {
        StretchMode.Percent2 => "2% 线性拉伸",
        StretchMode.MinMax => "最小—最大拉伸",
        StretchMode.StandardDeviation => "均值 ± 2σ",
        StretchMode.None => "无拉伸",
        _ => mode.ToString()
    };
}

public sealed record RenderedImage(int Width, int Height, byte[] Pixels);
