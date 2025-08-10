using GameAssetArchive.Core;
using GameAssetArchive.Core.Dtos;
using GameAssetArchive.Core.Enums;
using System.IO.Compression;
using System.Text.Json;

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
            await Commands.BuildFromAssetDirectoryAsync(assetsDir, output);
        }
        else if(parameters.TryGetValue("command_file", out var commandFilePath) && !string.IsNullOrEmpty(commandFilePath))
        {
            await Commands.BuildFromCommandFile(commandFilePath);
        }
        else if(parameters.TryGetValue("dump_toc", out var dumpTocPath) && !string.IsNullOrEmpty(dumpTocPath))
        {
            await Commands.DumpTableOfContents(dumpTocPath);
        }
        else
        {
            Console.WriteLine("Invalid parameters. Please specify either 'assets_dir' and 'output' or 'command_file'.");
        }
    }
}
