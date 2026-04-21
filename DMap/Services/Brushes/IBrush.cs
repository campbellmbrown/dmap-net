using Avalonia;

using DMap.Models;

namespace DMap.Services.Brushes;

public interface IBrush
{
    string Name { get; }
    PixelRect Apply(FogMask mask, int x1, int y1, int x2, int y2, BrushSettings settings, byte[]? snapshot = null);
}
