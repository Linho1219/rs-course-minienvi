namespace MiniEnvi.IO;

public static class EnviDataType
{
    public static int SizeOf(int type) => type switch
    {
        1 => 1,
        2 or 12 => 2,
        3 or 4 or 13 => 4,
        5 or 14 or 15 => 8,
        _ => throw new NotSupportedException($"暂不支持 ENVI data type {type}。")
    };

    public static string NameOf(int type) => type switch
    {
        1 => "8-bit byte",
        2 => "16-bit signed integer",
        3 => "32-bit signed integer",
        4 => "32-bit floating point",
        5 => "64-bit floating point",
        12 => "16-bit unsigned integer",
        13 => "32-bit unsigned integer",
        14 => "64-bit signed integer",
        15 => "64-bit unsigned integer",
        _ => $"ENVI type {type}"
    };
}
