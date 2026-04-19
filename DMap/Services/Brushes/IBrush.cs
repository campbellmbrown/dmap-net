using Avalonia;
using DMap.Models;

namespace DMap.Services.Brushes;

public interface IBrush
{
    string Name { get; }
    PixelRect Apply(FogMask mask, int centerX, int centerY, BrushSettings settings);
}
