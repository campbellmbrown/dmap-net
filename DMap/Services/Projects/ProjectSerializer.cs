using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using DMap.Models;

namespace DMap.Services.Projects;

/// <summary>
/// Stores a project as a zip archive containing <c>manifest.json</c>, the encoded map image,
/// and the raw fog mask bytes.
/// </summary>
public sealed class ProjectSerializer : IProjectSerializer
{
    const string ManifestEntryName = "manifest.json";
    const string MapEntryName = "map";
    const string FogEntryName = "fog.bin";

    static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <inheritdoc/>
    public Task SaveAsync(string path, ProjectFile project)
    {
        // Serialize the manifest on the caller's thread so mutable state (e.g. stamps) is captured consistently.
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(project.Manifest, _jsonOptions);

        return Task.Run(() =>
        {
            var tempPath = path + ".tmp";
            using (var stream = File.Create(tempPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(archive, ManifestEntryName, manifestBytes);
                WriteEntry(archive, MapEntryName, project.MapBytes);
                WriteEntry(archive, FogEntryName, project.FogMask.Data);
            }

            File.Move(tempPath, path, overwrite: true);
        });
    }

    /// <inheritdoc/>
    public Task<ProjectFile> LoadAsync(string path) =>
        Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(path);
            var manifest = JsonSerializer.Deserialize<ProjectManifest>(ReadEntry(archive, ManifestEntryName), _jsonOptions)
                ?? throw new InvalidDataException("Project manifest is empty.");

            if (manifest.FormatVersion > ProjectManifest.CurrentFormatVersion)
                throw new InvalidDataException(
                    $"Project format version {manifest.FormatVersion} is newer than this version of DMap supports.");

            var fogMask = new FogMask(manifest.MapWidth, manifest.MapHeight, ReadEntry(archive, FogEntryName));
            return new ProjectFile(manifest, ReadEntry(archive, MapEntryName), fogMask);
        });

    static void WriteEntry(ZipArchive archive, string name, byte[] bytes)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.Fastest).Open();
        stream.Write(bytes);
    }

    static byte[] ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name)
            ?? throw new InvalidDataException($"Project file is missing '{name}'.");

        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
