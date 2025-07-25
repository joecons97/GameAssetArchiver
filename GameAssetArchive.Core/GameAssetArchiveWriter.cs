using Force.Crc32;
using GameAssetArchive.Core.Models;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace GameAssetArchive.Core;

public class GameAssetArchiveWriter
{
    /*
        LAYOUT:
        5 bytes: "GAARC" magic string
        4 bytes: version number (currently 1)
        4 bytes: compression type (enum value)
        4 bytes: compression level (enum value)
        4 bytes: Table of Contents compressed size
        4 bytes: CRC32 of the Table of Contents
        N bytes: Table of Contents (compressed)
            - 4 bytes: number of entries
            - For each entry:
                - null-terminated string: relative file path
                - 8 bytes: offset in the archive where the file data starts
        4 bytes: asset data compressed size
        4 bytes: CRC32 of the asset data
        N bytes: asset data (compressed)
            - For each file:
                - compressed file data
    */

    private readonly CompressionOptions compressionOptions;

    public GameAssetArchiveWriter(
        CompressionOptions compressionOptions)
    {
        this.compressionOptions = compressionOptions;
    }

    public async Task ArchiveGameAssetsAsync(string gameAssetsPath, string archiveName)
    {
        var assetPaths = Directory.GetFiles(gameAssetsPath, "*.*", SearchOption.AllDirectories);
        await ArchiveGameAssetsAsync(gameAssetsPath, assetPaths, archiveName);
    }

    public async Task ArchiveGameAssetsAsync(string workingDirectory, string[] assetPaths, string archiveName)
    {
        Console.WriteLine($"Archiving game assets in: {workingDirectory} to: {archiveName} ({compressionOptions.Type}, {compressionOptions.Level})");

        using var storeResult = await BuildStoreAsync(assetPaths, workingDirectory);
        var tocResult = await BuildTableOfContentsAsync(storeResult.FileOffsets);

        var archivePath = Path.ChangeExtension(Path.Combine(workingDirectory, archiveName), ".gaarc");
        using var fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(fileStream);

        writer.Write("GAARC"u8.ToArray()); //Write Magic Num
        writer.Write(1); //Write Version Number
        writer.Write((int)compressionOptions.Type); //Write Compression Type
        writer.Write((int)compressionOptions.Level); //Write Compression Level

        tocResult.stream.Position = 0;
        var tocData = tocResult.stream.ToArray();

        writer.Write(tocResult.crc); // Write CRC32 of the Table of Contents
        writer.Write(tocData.Length); // Write Table of Contents compressed size
        writer.Write(tocData); // Write Table of Contents data

        storeResult.Stream.Position = 0;
        var assetData = storeResult.Stream.ToArray();
        uint assetCrc = Crc32Algorithm.Compute(assetData);

        writer.Write(assetCrc);
        writer.Write(assetData.Length);
        writer.Write(assetData);

        foreach (var fileOffset in storeResult.FileOffsets)
        {
            Console.WriteLine($"File: {fileOffset.Key}, Offset: {fileOffset.Value}");
        }

        tocResult.stream.Dispose();
    }

    private async Task<BuildStoreResult> BuildStoreAsync(string[] assetPaths, string workingPath)
    {
        var fileOffsets = new List<KeyValuePair<string, BuildStoreFile>>();

        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var assetPath in assetPaths)
        {
            if (!File.Exists(assetPath))
            {
                throw new FileNotFoundException($"Asset file not found: {assetPath}");
            }

            var startPosition = stream.Length;
            var relativePath = Path.GetRelativePath(workingPath, assetPath).Replace('\\', '/');

            using var fileStream = File.Open(assetPath, FileMode.Open);
            using var compressedStream = new MemoryStream();
            using var compressor = GetCompressionStream(compressedStream, compressionOptions, true);
            await fileStream.CopyToAsync(compressor);

            await compressor.FlushAsync();

            var data = compressedStream.ToArray();
            writer.Write(data);

            fileOffsets.Add(new KeyValuePair<string, BuildStoreFile>(relativePath, new(startPosition, compressedStream.Length)));
        }
        return new BuildStoreResult(stream, fileOffsets);
    }

    private async Task<(uint crc, MemoryStream stream)> BuildTableOfContentsAsync(List<KeyValuePair<string, BuildStoreFile>> fileOffsets)
    {
        using var fullStream = new MemoryStream();
        using var writer = new BinaryWriter(fullStream);
        writer.Write(fileOffsets.Count);

        foreach (var item in fileOffsets)
        {
            writer.Write(item.Key);
            writer.Write(item.Value.offset);
            writer.Write(item.Value.size);
        }

        fullStream.Seek(0, SeekOrigin.Begin);

        var data = fullStream.ToArray();
        var crc = Crc32Algorithm.Compute(data);

        fullStream.Seek(0, SeekOrigin.Begin);

        using var compressed = new MemoryStream();
        using var compressor = GetCompressionStream(compressed, compressionOptions, true);
        await fullStream.CopyToAsync(compressor);

        await compressor.FlushAsync();

        return new (crc, new MemoryStream(compressed.ToArray()));
    }

    private static Stream GetCompressionStream(Stream stream, CompressionOptions options, bool leaveOpen)
    {
        return options.Type switch
        {
            Enums.CompressionType.GZip => new GZipStream(stream, options.Level, leaveOpen),
            Enums.CompressionType.Deflate => new DeflateStream(stream, options.Level, leaveOpen),
            _ => throw new NotSupportedException($"Compression type {options.Type} is not supported."),
        };
    }
}
