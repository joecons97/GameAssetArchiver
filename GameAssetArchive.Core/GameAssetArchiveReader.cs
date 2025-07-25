using Force.Crc32;
using GameAssetArchive.Core.Enums;
using GameAssetArchive.Core.Models;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GameAssetArchive.Core;

public class GameAssetArchiveReader : IDisposable
{
    private int tocStartOffset = 5 + 4 + 4 + 4 + 4 + 4;
    private int tocCompressedSize;
    private uint tocCRC32;

    private int assetDataCompressedSize;
    private uint assetDataCRC32;
    private int assetDataStartOffset;

    private CompressionType compressionType;

    private Dictionary<string, BuildStoreFile> fileOffsets = new Dictionary<string, BuildStoreFile>();

    private FileStream? stream;

    public async Task ReadFromAsync(string filePath)
        => await ReadFromAsync(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));

    public async Task ReadFromAsync(FileStream fileStream)
    {
        stream = fileStream;
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var magic = reader.ReadBytes(5);
        if (Encoding.UTF8.GetString(magic) != "GAARC")
        {
            throw new InvalidDataException("Invalid archive format. Expected 'GAARC' magic string.");
        }

        var version = reader.ReadInt32();
        compressionType = (CompressionType)reader.ReadInt32();
        var compressionLevel = (CompressionLevel)reader.ReadInt32();

        tocCRC32 = reader.ReadUInt32();
        tocCompressedSize = reader.ReadInt32();

        await ReadTableOfContentsAsync(reader, compressionType);

        assetDataCRC32 = reader.ReadUInt32();
        assetDataCompressedSize = reader.ReadInt32();

        assetDataStartOffset = (int)stream.Position;
    }

    public async Task<Stream?> TryGetFileStreamAsync(string filePath)
    {
        if(stream == null)
        {
            throw new InvalidOperationException("Archive has not been opened. Call ReadFromAsync first.");
        }

        if (fileOffsets.TryGetValue(filePath, out var file) == false)
        {
            return null;
        }

        stream.Seek(file.offset + assetDataStartOffset, SeekOrigin.Begin);
        var compressedData = new byte[file.size];
        var readBytes = await stream.ReadAsync(compressedData, 0, (int)file.size);
        if(readBytes != file.size)
        {
            throw new InvalidDataException($"Failed to read the expected number of bytes for file '{filePath}'. Expected: {file.size}, Read: {readBytes}");
        }

        using var decompressedStream = new MemoryStream();
        using (var compressedStream = new MemoryStream(compressedData))
        {
            using var decompressor = GetDecompressionStream(compressedStream, compressionType); // Assuming GZip for simplicity
            decompressor.CopyTo(decompressedStream);
            decompressor.Flush();
        }

        decompressedStream.Seek(0, SeekOrigin.Begin);
        return new MemoryStream(decompressedStream.ToArray());
    }

    private async Task ReadTableOfContentsAsync(BinaryReader reader, CompressionType compressionType)
    {
        var compressedTocData = reader.ReadBytes(tocCompressedSize);

        using var decompressedTocStream = new MemoryStream();

        using (var tocCompressedStream = new MemoryStream(compressedTocData))
        {
            using var decompressor = GetDecompressionStream(tocCompressedStream, compressionType);
            await decompressor.CopyToAsync(decompressedTocStream);
            await decompressor.FlushAsync();
        }

        decompressedTocStream.Seek(0, SeekOrigin.Begin);
        var tocCrc = Crc32Algorithm.Compute(decompressedTocStream.ToArray());
        if (tocCrc != tocCRC32)
        {
            throw new InvalidDataException("Table of contents CRC32 mismatch.");
        }

        decompressedTocStream.Seek(0, SeekOrigin.Begin);

        using var tocReader = new BinaryReader(decompressedTocStream);

        var tocEntryCount = tocReader.ReadInt32();
        for (int i = 0; i < tocEntryCount; i++)
        {
            var filePath = tocReader.ReadString();
            var offset = tocReader.ReadInt64();
            var size = tocReader.ReadInt64();
            fileOffsets.Add(filePath, new BuildStoreFile(offset, size));
        }
    }

    private static Stream GetDecompressionStream(Stream stream, CompressionType type)
    {
        return type switch
        {
            CompressionType.GZip => new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true),
            CompressionType.Deflate => new DeflateStream(stream, CompressionMode.Decompress, leaveOpen: true),
            _ => throw new NotSupportedException($"Unsupported compression type: {type}"),
        };
    }

    public void Dispose()
    {
        stream?.Dispose();
        stream = null;
    }
}
