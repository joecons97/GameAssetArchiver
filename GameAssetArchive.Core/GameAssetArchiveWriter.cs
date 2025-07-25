using GameAssetArchive.Core.Models;
using System.IO.Compression;
using System.Text;
using Force.Crc32;

namespace GameAssetArchive.Core;

public class GameAssetArchiveWriter
{
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
        using var tocResult = await BuildTableOfContentsAsync(storeResult.FileOffsets);

        var archivePath = Path.ChangeExtension(Path.Combine(workingDirectory, archiveName), ".gaarc");
        using var fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(fileStream);

        writer.Write("GAARC"u8.ToArray());
        writer.Write(1);
        writer.Write((int)compressionOptions.Type);
        writer.Write((int)compressionOptions.Level);

        var tocData = tocResult.ToArray();
        uint tocCrc = Crc32Algorithm.Compute(tocData);

        writer.Write(tocCrc);
        writer.Write(tocData);

        storeResult.Stream.Position = 0;
        var assetData = storeResult.Stream.ToArray();
        uint assetCrc = Crc32Algorithm.Compute(assetData);

        writer.Write(assetCrc);

        storeResult.Stream.Position = 0;
        storeResult.Stream.CopyTo(writer.BaseStream);

        foreach (var fileOffset in storeResult.FileOffsets)
        {
            Console.WriteLine($"File: {fileOffset.Key}, Offset: {fileOffset.Value}");
        }
    }

    private async Task<BuildStoreResult> BuildStoreAsync(string[] assetPaths, string workingPath)
    {
        var fileOffsets = new List<KeyValuePair<string, long>>();

        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var assetPath in assetPaths)
        {
            if (!File.Exists(assetPath))
            {
                throw new FileNotFoundException($"Asset file not found: {assetPath}");
            }

            var relativePath = Path.GetRelativePath(workingPath, assetPath).Replace('\\', '/');
            fileOffsets.Add(new KeyValuePair<string, long>(relativePath, stream.Length));

            using var fileStream = File.Open(assetPath, FileMode.Open);
            using var compressedStream = new MemoryStream();
            using var compressor = GetCompressionStream(compressedStream, compressionOptions, true);
            await fileStream.CopyToAsync(compressor);

            await compressor.FlushAsync();
            await compressor.DisposeAsync();

            var data = compressedStream.ToArray();
            writer.Write(data);
        }

        stream.Seek(0, SeekOrigin.Begin);
        return new BuildStoreResult(stream, fileOffsets);
    }

    private async Task<MemoryStream> BuildTableOfContentsAsync(List<KeyValuePair<string, long>> fileOffsets)
    {
        using var fullStream = new MemoryStream();
        using var writer = new BinaryWriter(fullStream);
        writer.Write(fileOffsets.Count);

        foreach (var item in fileOffsets)
        {
            writer.Write(item.Key);
            writer.Write(item.Value);
        }

        var finalStream = new MemoryStream();
        using var compressor = GetCompressionStream(finalStream, compressionOptions, true);
        await fullStream.CopyToAsync(compressor);

        await compressor.FlushAsync();
        await compressor.DisposeAsync();

        finalStream.Seek(0, SeekOrigin.Begin);
        return finalStream;
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
