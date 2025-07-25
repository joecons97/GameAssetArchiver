namespace GameAssetArchive.Core.Models;

internal record BuildStoreResult(MemoryStream Stream, List<KeyValuePair<string, BuildStoreFile>> FileOffsets) : IDisposable
{
    public void Dispose()
    {
        Stream.Dispose();
    }
}

public record BuildStoreFile(long offset, long size);
