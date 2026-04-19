using System;
using Avalonia;
using DMap.Models;
using DMap.Services.Brushes;
using DMap.Services.Networking;

namespace DMap.Services.Fog;

public interface IFogMaskService
{
    FogMask? Mask { get; }

    event EventHandler<PixelRect>? MaskChanged;

    void Initialize(int width, int height);

    PixelRect ApplyBrush(IBrush brush, int centerX, int centerY, BrushSettings settings);

    void Replace(FogMask mask);

    void ApplyDelta(FogDelta delta);
}
