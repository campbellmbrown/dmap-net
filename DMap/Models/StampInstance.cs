using System;

namespace DMap.Models;

/// <summary>
/// A placed stamp in map-space coordinates.
/// </summary>
public sealed class StampInstance
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string TemplateId { get; init; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public double RotationDegrees { get; set; }
}
