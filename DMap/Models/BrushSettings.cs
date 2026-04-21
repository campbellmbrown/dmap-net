namespace DMap.Models;

public sealed record BrushSettings(int Diameter, float Softness, float Opacity = 1.0f, bool Erase = false);
