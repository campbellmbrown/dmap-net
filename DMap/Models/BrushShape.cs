namespace DMap.Models;

/// <summary>
/// The shape of the brush tip used when painting fog of war.
/// </summary>
public enum BrushShape
{
    /// <summary>Circular brush tip, using Euclidean distance.</summary>
    Circle,

    /// <summary>Square brush tip, using Chebyshev (infinity-norm) distance.</summary>
    Square,

    /// <summary>Diamond brush tip, using Manhattan (L1-norm) distance.</summary>
    Diamond,
}
