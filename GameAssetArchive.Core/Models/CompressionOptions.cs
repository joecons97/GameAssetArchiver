using GameAssetArchive.Core.Enums;
using System.IO.Compression;

namespace GameAssetArchive.Core.Models;

public record CompressionOptions(CompressionType Type, CompressionLevel Level);
