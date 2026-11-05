using System.Text;
using MiniEnvi.Models;

namespace MiniEnvi.IO;

public static class EnviHeaderParser
{
    public static EnviHeader Read(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0 || !lines[0].Trim().Equals("ENVI", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("所选文件不是有效的 ENVI 头文件（首行应为 ENVI）。");

        var header = new EnviHeader();
        var current = new StringBuilder();
        var depth = 0;

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 && current.Length == 0) continue;
            if (current.Length > 0) current.AppendLine();
            current.Append(line);
            depth += line.Count(c => c == '{') - line.Count(c => c == '}');
            if (depth > 0) continue;

            ParseEntry(current.ToString(), header);
            current.Clear();
            depth = 0;
        }

        if (current.Length > 0) ParseEntry(current.ToString(), header);
        Validate(header);
        return header;
    }

    public static void Write(string path, EnviHeader header)
    {
        var text = new StringBuilder("ENVI\r\n");
        foreach (var (key, value) in header.Entries)
            text.Append(key).Append(" = ").Append(value.Replace("\n", "\r\n")).Append("\r\n");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static void ParseEntry(string text, EnviHeader header)
    {
        var equals = text.IndexOf('=');
        if (equals <= 0) return;
        header.Set(text[..equals], text[(equals + 1)..]);
    }

    private static void Validate(EnviHeader header)
    {
        if (header.Samples <= 0 || header.Lines <= 0 || header.Bands <= 0)
            throw new InvalidDataException("HDR 中的 samples、lines 或 bands 无效。");
        if (header.DataType <= 0)
            throw new InvalidDataException("HDR 中缺少有效的 data type。");
        if (header.Interleave is not ("bsq" or "bil" or "bip"))
            throw new NotSupportedException($"暂不支持交错方式：{header.Interleave}");
        if (header.ByteOrder is not (0 or 1))
            throw new NotSupportedException($"暂不支持 byte order：{header.ByteOrder}");
    }
}
