using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using MiniEnvi.Models;

namespace MiniEnvi.IO;

public sealed class EnviDataset : IDisposable
{
    private readonly MemoryMappedFile _map;
    private readonly MemoryMappedViewAccessor _accessor;

    private EnviDataset(string headerPath, string dataPath, EnviHeader header)
    {
        HeaderPath = headerPath;
        DataPath = dataPath;
        Header = header;
        _map = MemoryMappedFile.CreateFromFile(dataPath, FileMode.Open, null, 0,
            MemoryMappedFileAccess.Read);
        _accessor = _map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        ValidateFileLength();
    }

    public string HeaderPath { get; }
    public string DataPath { get; }
    public string Name => Path.GetFileNameWithoutExtension(HeaderPath);
    public EnviHeader Header { get; }

    public static EnviDataset Open(string path)
    {
        var headerPath = ResolveHeaderPath(path);
        var header = EnviHeaderParser.Read(headerPath);
        var dataPath = ResolveDataPath(headerPath, header);
        return new EnviDataset(headerPath, dataPath, header);
    }

    public double[] ReadBand(int band) => ReadWindow(band, 0, 0, Header.Samples, Header.Lines);

    public double[] ReadWindow(int band, int x, int y, int width, int height)
    {
        if (band < 0 || band >= Header.Bands) throw new ArgumentOutOfRangeException(nameof(band));
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > Header.Samples ||
            y + height > Header.Lines) throw new ArgumentOutOfRangeException(nameof(width));

        var result = new double[checked(width * height)];
        var pixelBytes = EnviDataType.SizeOf(Header.DataType);
        var index = 0;
        if (Header.Interleave is "bsq" or "bil")
        {
            var rowBytes = new byte[checked(width * pixelBytes)];
            for (var row = y; row < y + height; row++)
            {
                _accessor.ReadArray(GetOffset(band, row, x), rowBytes, 0, rowBytes.Length);
                for (var col = 0; col < width; col++)
                    result[index++] = Decode(rowBytes.AsSpan(col * pixelBytes, pixelBytes));
            }
        }
        else
        {
            var rowBytes = new byte[checked(width * Header.Bands * pixelBytes)];
            for (var row = y; row < y + height; row++)
            {
                _accessor.ReadArray(GetOffset(0, row, x), rowBytes, 0, rowBytes.Length);
                for (var col = 0; col < width; col++)
                {
                    var offset = (col * Header.Bands + band) * pixelBytes;
                    result[index++] = Decode(rowBytes.AsSpan(offset, pixelBytes));
                }
            }
        }
        return result;
    }

    public string BandLabel(int band)
    {
        var names = Header.GetList("band names");
        var wavelengths = Header.GetList("wavelength");
        var name = band < names.Count ? names[band] : $"Band {band + 1}";
        if (band >= wavelengths.Count) return name;
        var unit = EnviHeader.TrimBraces(Header.Get("wavelength units", ""));
        return $"{name}  ({wavelengths[band]}{(unit.Length > 0 ? " " + unit : "")})";
    }

    public void Dispose()
    {
        _accessor.Dispose();
        _map.Dispose();
    }

    private long GetOffset(int band, int row, int col)
    {
        long sampleIndex = Header.Interleave switch
        {
            "bsq" => ((long)band * Header.Lines + row) * Header.Samples + col,
            "bil" => ((long)row * Header.Bands + band) * Header.Samples + col,
            "bip" => ((long)row * Header.Samples + col) * Header.Bands + band,
            _ => throw new NotSupportedException()
        };
        return Header.HeaderOffset + sampleIndex * EnviDataType.SizeOf(Header.DataType);
    }

    private double Decode(Span<byte> bytes)
    {
        var little = Header.ByteOrder == 0;
        return Header.DataType switch
        {
            1 => bytes[0],
            2 => ReadInt16(bytes, little),
            3 => ReadInt32(bytes, little),
            4 => BitConverter.Int32BitsToSingle(ReadInt32(bytes, little)),
            5 => BitConverter.Int64BitsToDouble(ReadInt64(bytes, little)),
            12 => ReadUInt16(bytes, little),
            13 => ReadUInt32(bytes, little),
            14 => ReadInt64(bytes, little),
            15 => ReadUInt64(bytes, little),
            _ => throw new NotSupportedException()
        };
    }

    private static short ReadInt16(ReadOnlySpan<byte> bytes, bool little) => little
        ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : BinaryPrimitives.ReadInt16BigEndian(bytes);
    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, bool little) => little
        ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : BinaryPrimitives.ReadUInt16BigEndian(bytes);
    private static int ReadInt32(ReadOnlySpan<byte> bytes, bool little) => little
        ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : BinaryPrimitives.ReadInt32BigEndian(bytes);
    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, bool little) => little
        ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes);
    private static long ReadInt64(ReadOnlySpan<byte> bytes, bool little) => little
        ? BinaryPrimitives.ReadInt64LittleEndian(bytes) : BinaryPrimitives.ReadInt64BigEndian(bytes);
    private static ulong ReadUInt64(ReadOnlySpan<byte> bytes, bool little) => little
        ? BinaryPrimitives.ReadUInt64LittleEndian(bytes) : BinaryPrimitives.ReadUInt64BigEndian(bytes);

    private void ValidateFileLength()
    {
        var expected = (long)Header.HeaderOffset + (long)Header.Samples * Header.Lines * Header.Bands *
            EnviDataType.SizeOf(Header.DataType);
        var actual = new FileInfo(DataPath).Length;
        if (actual < expected)
            throw new InvalidDataException($"数据文件过短：需要至少 {expected:N0} 字节，实际 {actual:N0} 字节。");
    }

    private static string ResolveHeaderPath(string path)
    {
        if (Path.GetExtension(path).Equals(".hdr", StringComparison.OrdinalIgnoreCase)) return Path.GetFullPath(path);
        var candidate = Path.ChangeExtension(path, ".hdr");
        if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        throw new FileNotFoundException("找不到与数据文件对应的 .hdr 文件。", candidate);
    }

    private static string ResolveDataPath(string headerPath, EnviHeader header)
    {
        var directory = Path.GetDirectoryName(headerPath)!;
        if (!string.IsNullOrWhiteSpace(header.DataFile))
        {
            var declared = Path.Combine(directory, header.DataFile);
            if (File.Exists(declared)) return declared;
        }

        var basePath = Path.Combine(directory, Path.GetFileNameWithoutExtension(headerPath));
        foreach (var candidate in new[] { basePath + ".dat", basePath + ".img", basePath })
            if (File.Exists(candidate)) return candidate;
        throw new FileNotFoundException("找不到 HDR 对应的 .dat 或 .img 数据文件。", basePath);
    }
}
