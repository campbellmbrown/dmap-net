using DMap.Protocol;

using NUnit.Framework;

namespace DMap.Tests.Protocol;

public class ViewportPayloadTests
{
    [Test]
    public void SerializeRoundTripPreservesPixelSharpness()
    {
        var payload = new ViewportPayload
        {
            CenterMapX = 10,
            CenterMapY = 20,
            ZoomLevel = 1.5,
            RotationQuarterTurns = 1,
            WidthMap = 100,
            HeightMap = 200,
            PaddingPixels = 16,
            IsPixelSharpnessEnabled = false,
        };

        var deserialized = ViewportPayload.Deserialize(payload.Serialize());

        Assert.That(deserialized.IsPixelSharpnessEnabled, Is.False);
    }

    [Test]
    public void DeserializeLegacyPayloadDefaultsPixelSharpnessOff()
    {
        var legacyPayload = new byte[sizeof(double) + sizeof(double) + sizeof(double) + sizeof(int)];

        var deserialized = ViewportPayload.Deserialize(legacyPayload);

        Assert.That(deserialized.IsPixelSharpnessEnabled, Is.False);
    }
}
