namespace GameAssetArchive.Core.Models;

internal record BuildStoreResult(MemoryStream Stream, List<KeyValuePair<string, long>> FileOffsets) : IDisposable
{
    public void Dispose()
    {
        Stream.Dispose();
    }
}
