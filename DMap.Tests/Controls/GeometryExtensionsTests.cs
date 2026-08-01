using Avalonia;

using DMap.Controls.MapCanvas;

using NUnit.Framework;

namespace DMap.Tests.Controls;

public class GeometryExtensionsTests
{
    [TestCase(0, 0, 5, 5, 0, 0, 5, 5)] // top-left to bottom-right
    [TestCase(5, 5, 0, 0, 0, 0, 5, 5)] // bottom-right to top-left
    [TestCase(0, 5, 5, 0, 0, 0, 5, 5)] // bottom-left to top-right
    [TestCase(5, 0, 0, 5, 0, 0, 5, 5)] // top-right to bottom-left
    [TestCase(1, 2, 5, 8, 1, 2, 4, 6)] // Offset
    [TestCase(5, 8, 1, 2, 1, 2, 4, 6)] // Offset reversed
    [TestCase(-3, 7, 2, -1, -3, -1, 5, 8)] // Mixed positive and negative coordinates
    public void MakeRect_NormalizesPointsToTopLeftAndSize(
        double startX,
        double startY,
        double endX,
        double endY,
        double expectedX,
        double expectedY,
        double expectedWidth,
        double expectedHeight)
    {
        // Arrange
        Point start = new(startX, startY);
        Point end = new(endX, endY);

        // Act
        var rect = start.MakeRect(end);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(rect.X, Is.EqualTo(expectedX));
            Assert.That(rect.Y, Is.EqualTo(expectedY));
            Assert.That(rect.Width, Is.EqualTo(expectedWidth));
            Assert.That(rect.Height, Is.EqualTo(expectedHeight));
        });
    }

    [TestCase(2, 4, 5, 4, 3.5, 4)] // Horizontal line
    [TestCase(2, 4, 2, 9, 2, 6.5)] // Vertical line
    [TestCase(0, 0, 4, 6, 2, 3)] // Slope
    [TestCase(5, 8, 1, 2, 3, 5)] // Negative slope
    [TestCase(-3, 7, 2, -1, -0.5, 3)] // Mixed positive and negative coordinates
    [TestCase(-5, -5, -1, -1, -3, -3)] // All negative coordinates
    public void MidpointReturnsAverageOfBothCoordinates(
        double point1X,
        double point1Y,
        double point2X,
        double point2Y,
        double expectedMidpointX,
        double expectedMidpointY)
    {
        // Arrange
        Point point1 = new(point1X, point1Y);
        Point point2 = new(point2X, point2Y);

        // Act
        var midpoint = point1.Midpoint(point2);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(midpoint.X, Is.EqualTo(expectedMidpointX));
            Assert.That(midpoint.Y, Is.EqualTo(expectedMidpointY));
        });
    }

    [TestCase(10, 20, 12, 23, 3, true)] // Point is within the threshold
    [TestCase(10, 20, 13, 23, 3, true)] // Point is on the edge of the threshold
    [TestCase(10, 20, 14, 23, 3, false)] // Point is outside the threshold in X direction
    [TestCase(10, 20, 12, 24, 3, false)] // Point is outside the threshold in Y direction
    public void IsNearUsesInclusiveAxisAlignedThreshold(
        double pointX,
        double pointY,
        double centerX,
        double centerY,
        double threshold,
        bool expected)
    {
        // Arrange
        Point point = new(pointX, pointY);
        Point center = new(centerX, centerY);

        // Act
        var isNear = point.IsWithinSquare(center, threshold);

        // Assert
        Assert.That(isNear, Is.EqualTo(expected));
    }
}
