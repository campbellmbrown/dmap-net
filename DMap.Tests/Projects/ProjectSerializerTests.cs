using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

using DMap.Models;
using DMap.Protocol;
using DMap.Services.Projects;

using NUnit.Framework;

namespace DMap.Tests.Projects;

public class ProjectSerializerTests
{
    string _path = null!;

    [SetUp]
    public void SetUp() => _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dmap");

    [TearDown]
    public void TearDown() => File.Delete(_path);

    [Test]
    public async Task SaveThenLoadRoundTripsProject()
    {
        var serializer = new ProjectSerializer();
        var seed = Guid.NewGuid();
        var stampId = Guid.NewGuid();
        var project = new ProjectFile(
            new ProjectManifest
            {
                MapWidth = 3,
                MapHeight = 2,
                Fog = new FogAppearancePayload { FogType = FogType.Cloud, R = 1, G = 2, B = 3, Seed = seed },
                FogOpacity = 0.25,
                Grid = new GridSettingsPayload { IsVisible = true, SquareSize = 70, LineWidth = 2, R = 4, G = 5, B = 6, A = 7, OffsetX = 0.5, OffsetY = 0.75 },
                PlayerViewport = new ViewportPayload { CenterMapX = 1.5, CenterMapY = 1, ZoomLevel = 1, RotationQuarterTurns = 3, WidthMap = 3, HeightMap = 2, PaddingPixels = 8, IsPixelSharpnessEnabled = true },
                Stamps =
                [
                    new StampInstance { Id = stampId, TemplateId = "spirit-guardians", X = 1, Y = 2, Width = 3, Height = 4, RotationDegrees = 45 },
                    new StampInstance { TemplateId = "blue-rectangle" },
                ],
            },
            [9, 8, 7],
            new FogMask(3, 2, [0, 255, 128, 1, 2, 3]));

        await serializer.SaveAsync(_path, project);
        var loaded = await serializer.LoadAsync(_path);

        var manifest = loaded.Manifest;
        Assert.Multiple(() =>
        {
            Assert.That(manifest.FormatVersion, Is.EqualTo(ProjectManifest.CurrentFormatVersion));
            Assert.That(loaded.MapBytes, Is.EqualTo(new byte[] { 9, 8, 7 }));
            Assert.That(loaded.FogMask.Width, Is.EqualTo(3));
            Assert.That(loaded.FogMask.Height, Is.EqualTo(2));
            Assert.That(loaded.FogMask.Data, Is.EqualTo(new byte[] { 0, 255, 128, 1, 2, 3 }));
            Assert.That(manifest.Fog.FogType, Is.EqualTo(FogType.Cloud));
            Assert.That((manifest.Fog.R, manifest.Fog.G, manifest.Fog.B), Is.EqualTo(((byte)1, (byte)2, (byte)3)));
            Assert.That(manifest.Fog.Seed, Is.EqualTo(seed));
            Assert.That(manifest.FogOpacity, Is.EqualTo(0.25));
            Assert.That(manifest.Grid.Serialize(), Is.EqualTo(project.Manifest.Grid.Serialize()));
            Assert.That(manifest.PlayerViewport.Serialize(), Is.EqualTo(project.Manifest.PlayerViewport.Serialize()));
            Assert.That(manifest.Stamps, Has.Count.EqualTo(2));
            Assert.That(manifest.Stamps[0].Id, Is.EqualTo(stampId));
            Assert.That(manifest.Stamps[0].TemplateId, Is.EqualTo("spirit-guardians"));
            Assert.That(manifest.Stamps[0].RotationDegrees, Is.EqualTo(45));
            Assert.That(manifest.Stamps[1].TemplateId, Is.EqualTo("blue-rectangle"));
        });
    }

    [Test]
    public void LoadRejectsNewerFormatVersion()
    {
        using (var archive = ZipFile.Open(_path, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open());
            writer.Write($$"""{ "FormatVersion": {{ProjectManifest.CurrentFormatVersion + 1}}, "Fog": {}, "Grid": {}, "PlayerViewport": {} }""");
        }

        Assert.ThrowsAsync<InvalidDataException>(() => new ProjectSerializer().LoadAsync(_path));
    }
}
