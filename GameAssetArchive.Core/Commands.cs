using GameAssetArchive.Core.Dtos;
using GameAssetArchive.Core.Enums;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace GameAssetArchive.Core;

public static class Commands
{
    public static async Task BuildFromAssetDirectoryAsync(string assetsDir, string output)
    {
        if (!Directory.Exists(assetsDir))
        {
            Console.WriteLine($"The specified path does not exist: {assetsDir}");
            return;
        }

        if (File.Exists(output))
        {
            Console.WriteLine($"The output file already exists: {output}");
            return;
        }

        try
        {
            var writer = new GameAssetArchiveWriter(new Core.Models.CompressionOptions(CompressionType.GZip, CompressionLevel.Fastest));
            await writer.ArchiveGameAssetsAsync(assetsDir, output);
            Console.WriteLine($"Game assets archived successfully to: {output}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while archiving game assets: {ex.Message}");
        }
    }

    public static async Task BuildFromCommandFile(string commandFilePath)
    {
        var fileData = await File.ReadAllTextAsync(commandFilePath);
        var dto = JsonSerializer.Deserialize<CommandFileDTO>(fileData, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
        if (dto == null)
        {
            Console.WriteLine("An error occurred while trying to parse command file");
            return;
        }

        var workingDirectory = Path.GetDirectoryName(commandFilePath);
        if (string.IsNullOrEmpty(workingDirectory))
        {
            Console.WriteLine("An error occurred while finding the working directory");
            return;
        }

        foreach (var archive in dto.Archives)
        {
            var allDirectories = archive.InputPaths
                .SelectMany(x =>
                {
                    try
                    {
                        var combined = Path.Combine(workingDirectory, x);
                        var normalized = Path.GetFullPath(combined.Replace('\\', Path.DirectorySeparatorChar));
                        return Directory.GetFiles(normalized, "*.*", SearchOption.AllDirectories);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("An error occurred while trying to find files in path: " + x);
                        Console.WriteLine(ex);
                        return [];
                    }
                })
                .ToArray();

            var writer = new GameAssetArchiveWriter(new Core.Models.CompressionOptions(CompressionType.GZip, CompressionLevel.Fastest));
            await writer.ArchiveGameAssetsAsync(workingDirectory, allDirectories, archive.OutputPath);
        }
    }

    public static async Task DumpTableOfContents(string dumpTocPath)
    {
        using var reader = new GameAssetArchiveReader();
        await reader.ReadFromAsync(dumpTocPath);
        foreach (var entry in reader.TableOfContents)
        {
            Console.WriteLine($"File: {entry.Key}, Size: {entry.Value.size}bytes");
        }
    }
}
