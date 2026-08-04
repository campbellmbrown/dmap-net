using Avalonia.Media;

using DMap.Protocol;

using NUnit.Framework;

namespace DMap.Tests.Protocol;

public class GridSettingsPayloadTests
{
    [Test]
    public void SerializeRoundTripPreservesGridColorAlpha()
    {
        var payload = new GridSettingsPayload
        {
            IsVisible = true,
            SquareSize = 70,
            LineWidth = 1.5,
            R = 12,
            G = 34,
            B = 56,
            A = 78,
            OffsetX = 0.25,
            OffsetY = 0.75,
        };

        var deserialized = GridSettingsPayload.Deserialize(payload.Serialize());

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.IsVisible, Is.True);
            Assert.That(deserialized.SquareSize, Is.EqualTo(70));
            Assert.That(deserialized.LineWidth, Is.EqualTo(1.5));
            Assert.That(deserialized.Color, Is.EqualTo(Color.FromArgb(78, 12, 34, 56)));
            Assert.That(deserialized.OffsetX, Is.EqualTo(0.25));
            Assert.That(deserialized.OffsetY, Is.EqualTo(0.75));
        });
    }
}
