using System.Threading.Tasks;

using Avalonia;
using Avalonia.Media.Imaging;

namespace DMap.Services.Maps;

/// <summary>
/// Loads map files into a decoded bitmap plus encoded image bytes suitable for network transmission.
/// </summary>
public interface IMapLoader
{
    /// <summary>
    /// Loads a map file from disk. PDF inputs are rasterized to PNG bytes; image inputs keep their original bytes.
    /// </summary>
    /// <param name="path">Local path to the map file.</param>
    Task<LoadedMap> LoadAsync(string path);
}

/// <summary>
/// Decoded map data and encoded bytes used by the DM canvas and player protocol.
/// </summary>
/// <param name="Image">Decoded bitmap for the local DM canvas.</param>
/// <param name="EncodedBytes">Encoded image bytes to send to players.</param>
/// <param name="PixelSize">Decoded pixel dimensions of the map.</param>
public sealed record LoadedMap(Bitmap Image, byte[] EncodedBytes, PixelSize PixelSize);
