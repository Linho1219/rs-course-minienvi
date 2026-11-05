using System.Buffers.Binary;
using System.Globalization;
using MiniEnvi.Models;

namespace MiniEnvi.IO;

public static class EnviWriter
{
    public static async Task WriteCropAsync(EnviDataset source, string headerPath,
        int x, int y, int width, int height, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        headerPath = Path.ChangeExtension(headerPath, ".hdr");
        var dataPath = Path.ChangeExtension(headerPath, ".dat");
        var header = CreateCropHeader(source.Header, Path.GetFileName(dataPath), x, y, width, height);
        var pixelBytes = EnviDataType.SizeOf(header.DataType);
        var buffer = new byte[checked(width * height * pixelBytes)];

        await using (var stream = new FileStream(dataPath, FileMode.Create, FileAccess.Write, FileShare.None,
                         1024 * 1024, useAsync: true))
        {
            for (var band = 0; band < header.Bands; band++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = source.ReadWindow(band, x, y, width, height);
                for (var i = 0; i < values.Length; i++)
                    Encode(values[i], buffer.AsSpan(i * pixelBytes, pixelBytes), header.DataType, header.ByteOrder);
                await stream.WriteAsync(buffer, cancellationToken);
                progress?.Report((band + 1.0) / header.Bands);
            }
        }

        EnviHeaderParser.Write(headerPath, header);
    }

    private static EnviHeader CreateCropHeader(EnviHeader original, string dataFileName,
        int x, int y, int width, int height)
    {
        var header = original.Clone();
        header.Set("description", $"{{Cropped from source: x={x}, y={y}, width={width}, height={height}}}");
        header.Set("samples", width.ToString(CultureInfo.InvariantCulture));
        header.Set("lines", height.ToString(CultureInfo.InvariantCulture));
        header.Set("header offset", "0");
        header.Set("interleave", "bsq");
        header.Set("data file", "{" + dataFileName + "}");

        var map = original.GetList("map info").ToArray();
        if (map.Length >= 3 && double.TryParse(map[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var refX) &&
            double.TryParse(map[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var refY))
        {
            map[1] = (refX - x).ToString("G17", CultureInfo.InvariantCulture);
            map[2] = (refY - y).ToString("G17", CultureInfo.InvariantCulture);
            header.Set("map info", "{" + string.Join(", ", map) + "}");
        }
        return header;
    }

    private static void Encode(double value, Span<byte> bytes, int type, int byteOrder)
    {
        var little = byteOrder == 0;
        switch (type)
        {
            case 1: bytes[0] = (byte)Math.Clamp(Math.Round(value), byte.MinValue, byte.MaxValue); break;
            case 2: WriteInt16(bytes, (short)Math.Clamp(Math.Round(value), short.MinValue, short.MaxValue), little); break;
            case 3: WriteInt32(bytes, (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue), little); break;
            case 4: WriteInt32(bytes, BitConverter.SingleToInt32Bits((float)value), little); break;
            case 5: WriteInt64(bytes, BitConverter.DoubleToInt64Bits(value), little); break;
            case 12: WriteUInt16(bytes, (ushort)Math.Clamp(Math.Round(value), ushort.MinValue, ushort.MaxValue), little); break;
            case 13: WriteUInt32(bytes, (uint)Math.Clamp(Math.Round(value), uint.MinValue, uint.MaxValue), little); break;
            case 14: WriteInt64(bytes, (long)Math.Clamp(Math.Round(value), long.MinValue, long.MaxValue), little); break;
            case 15: WriteUInt64(bytes, (ulong)Math.Clamp(Math.Round(value), 0, ulong.MaxValue), little); break;
            default: throw new NotSupportedException($"暂不支持 ENVI data type {type}。");
        }
    }

    private static void WriteInt16(Span<byte> bytes, short value, bool little)
    { if (little) BinaryPrimitives.WriteInt16LittleEndian(bytes, value); else BinaryPrimitives.WriteInt16BigEndian(bytes, value); }
    private static void WriteUInt16(Span<byte> bytes, ushort value, bool little)
    { if (little) BinaryPrimitives.WriteUInt16LittleEndian(bytes, value); else BinaryPrimitives.WriteUInt16BigEndian(bytes, value); }
    private static void WriteInt32(Span<byte> bytes, int value, bool little)
    { if (little) BinaryPrimitives.WriteInt32LittleEndian(bytes, value); else BinaryPrimitives.WriteInt32BigEndian(bytes, value); }
    private static void WriteUInt32(Span<byte> bytes, uint value, bool little)
    { if (little) BinaryPrimitives.WriteUInt32LittleEndian(bytes, value); else BinaryPrimitives.WriteUInt32BigEndian(bytes, value); }
    private static void WriteInt64(Span<byte> bytes, long value, bool little)
    { if (little) BinaryPrimitives.WriteInt64LittleEndian(bytes, value); else BinaryPrimitives.WriteInt64BigEndian(bytes, value); }
    private static void WriteUInt64(Span<byte> bytes, ulong value, bool little)
    { if (little) BinaryPrimitives.WriteUInt64LittleEndian(bytes, value); else BinaryPrimitives.WriteUInt64BigEndian(bytes, value); }
}
