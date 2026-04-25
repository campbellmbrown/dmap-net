namespace DMap.Models;

/// <summary>
/// Immutable configuration for a single brush stroke operation.
/// </summary>
/// <param name="Diameter">Diameter of the brush in map pixels.</param>
/// <param name="Softness">
/// Edge feathering in the range [0, 1]. 0 produces a hard edge; 1 makes the entire
/// brush area a gradient from the centre outward.
/// </param>
/// <param name="Opacity">
/// Maximum alpha applied by one stroke in the range [0, 1]. Defaults to 1 (fully opaque).
/// </param>
/// <param name="Erase">
/// When <see langword="true"/> the stroke removes fog; when <see langword="false"/> it adds fog.
/// </param>
public sealed record BrushSettings(int Diameter, float Softness, float Opacity = 1.0f, bool Erase = false);
