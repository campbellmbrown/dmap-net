using System;
using System.Collections.Generic;
using System.IO;

using DMap.Models;

namespace DMap.Protocol;

/// <summary>
/// Full stamp layer state broadcast from the DM to players.
/// </summary>
public sealed class StampLayerPayload : IPayload
{
    public IReadOnlyList<StampInstance> Stamps { get; init; } = [];

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(Stamps.Count);

        foreach (var stamp in Stamps)
        {
            writer.Write(stamp.Id.ToByteArray());
            writer.Write(stamp.TemplateId);
            writer.Write(stamp.X);
            writer.Write(stamp.Y);
            writer.Write(stamp.Width);
            writer.Write(stamp.Height);
            writer.Write(stamp.RotationDegrees);
        }

        return ms.ToArray();
    }

    public static StampLayerPayload Deserialize(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);
        var count = reader.ReadInt32();
        var stamps = new List<StampInstance>(count);

        for (var i = 0; i < count; i++)
        {
            stamps.Add(new StampInstance
            {
                Id = new Guid(reader.ReadBytes(16)),
                TemplateId = reader.ReadString(),
                X = reader.ReadDouble(),
                Y = reader.ReadDouble(),
                Width = reader.ReadDouble(),
                Height = reader.ReadDouble(),
                RotationDegrees = ms.Position < ms.Length ? reader.ReadDouble() : 0,
            });
        }

        return new StampLayerPayload { Stamps = stamps };
    }
}
