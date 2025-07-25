using GameAssetArchive.Core;
using GameAssetArchive.Core.Enums;
using System.IO.Compression;

namespace GameAssetArchive;

public class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: GameAssetArchiver assets_dir=<path_to_assets_to_archive> output=<path_to_archive_output_file>");
            Console.WriteLine("OR");
            Console.WriteLine("Usage: GameAssetArchiver command_file=<path_to_archive_command_file>");
            Console.WriteLine("OR");
            Console.WriteLine("Usage: GameAssetArchiver dump_toc=<path_to_archive_file>");
            return;
        }

        var parameters = args
            .Select(arg => arg.Split('='))
            .ToDictionary(pair => pair[0].Trim().ToLowerInvariant(), pair => pair.Length > 1 ? pair[1].Trim() : string.Empty);

        if (parameters.TryGetValue("assets_dir", out var assetsDir) && !string.IsNullOrEmpty(assetsDir)
            && parameters.TryGetValue("output", out var output) && !string.IsNullOrEmpty(output))
        {
            if (!Directory.Exists(assetsDir))
            {
                Console.WriteLine($"The specified path does not exist: {assetsDir}");
                return;
            }

            if(File.Exists(output))
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
        else if(parameters.TryGetValue("command_file", out var commandFilePath) && !string.IsNullOrEmpty(commandFilePath))
        {

        }
        else if(parameters.TryGetValue("dump_toc", out var dumpTocPath) && !string.IsNullOrEmpty(dumpTocPath))
        {
            using var reader = new GameAssetArchiveReader();
            await reader.ReadFromAsync(dumpTocPath);
            foreach (var entry in reader.TableOfContents)
            {
                Console.WriteLine($"File: {entry.Key}, Size: {entry.Value.size}bytes");
            }
        }
        else
        {
            Console.WriteLine("Invalid parameters. Please specify either 'assets_dir' and 'output' or 'command_file'.");
        }
    }
}
