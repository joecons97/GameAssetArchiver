namespace GameAssetArchive.Core.Dtos;

public class CommandFileDTO
{
    public CommandFileArchiveDTO[] Archives { get; set; } = [];
}

public class CommandFileArchiveDTO
{
    public string OutputPath { get; set; } = "";
    public string[] InputPaths { get; set; } = [];
}

//Example
/*
{
    Archives: [
        {
            InputPaths: [
                "C:\Users\joeco\source\repos\NovaBlock\NovaBlock.Client\Content\Art\",
                "C:\Users\joeco\source\repos\NovaBlock\NovaBlock.Client\Content\Shaders\",
            ],
            OutputPath: "C:\Users\joeco\source\repos\NovaBlock\NovaBlock.Client\Content\Graphics"
        }
    ]
}
 
*/