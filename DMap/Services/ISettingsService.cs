using DMap.Models;

namespace DMap.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }

    void Save();
}
