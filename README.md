# GameAssetArchive
Install via nuget: `https://www.nuget.org/packages/GameAssetArchive`

An open-source game asset archiving system used for packages assets into a single `.gaarc` file.

For example, you might have one `.gaarc` file per level or you could split up levels into different `.gaarc` files and stream in data as the player goes through the level, etc.

Example usage
`GameAssetArchiver assets_dir=<path_to_assets_to_archive> output=<path_to_archive_output_file>` packs all files found recursively within the directory specified in the `assets_dir` param and outputs a `.gaarc` to the path specified in the `output` param.
`GameAssetArchiver assets_dir="C:/my_game/content/level1" output="C:/my_game/content/packed/level1"` will output a `.gaarc` called `level1` in the directory `C:/my_game/content/packed/`

You can also dump a list of files for a given archive file
`GameAssetArchiver dump_toc=<path_to_archive_file>`

## Command Files
Archives can also be built using `command files`. These are json files that can define multiple archives.
For example:
``
{
    "archives":[
        {
            "outputPath": "Graphics",
            "inputPaths":[
                "Art\\",
                "Shaders\\"
            ]
        },
        {
            "outputPath": "BlockData",
            "inputPaths":[
                "Blocks\\"
            ]
        }
    ]
}
``
Will create two archives in the same directory as the command file:
1. Graphics - containing all files within the art and shaders folder.
2. BlockData - containing all files within the blocks folder. 
