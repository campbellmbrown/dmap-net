using System.Threading.Tasks;

using DMap.Models;

namespace DMap.Services.Projects;

/// <summary>
/// Reads and writes <c>.dmap</c> project files.
/// </summary>
public interface IProjectSerializer
{
    /// <summary>
    /// Writes <paramref name="project"/> to <paramref name="path"/>, replacing any existing file only once
    /// the new file has been written completely.
    /// </summary>
    Task SaveAsync(string path, ProjectFile project);

    /// <summary>Reads a project from <paramref name="path"/>.</summary>
    /// <exception cref="System.IO.InvalidDataException">The file is not a valid or supported project.</exception>
    Task<ProjectFile> LoadAsync(string path);
}

/// <summary>
/// The contents of a project file.
/// </summary>
/// <param name="Manifest">Project settings and stamps.</param>
/// <param name="MapBytes">Encoded map image bytes.</param>
/// <param name="FogMask">Fog mask matching the map dimensions.</param>
public sealed record ProjectFile(ProjectManifest Manifest, byte[] MapBytes, FogMask FogMask);
