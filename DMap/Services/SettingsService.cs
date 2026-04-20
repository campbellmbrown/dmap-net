using System;
using System.IO;
using System.Text.Json;

using DMap.Models;

namespace DMap.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DMap",
        "settings.json");

    public AppSettings Settings { get; private set; }

    public SettingsService()
    {
        Settings = Load();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch (IOException) { }
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException) { }

        return new AppSettings();
    }
}
