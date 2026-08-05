using System.Collections.Generic;

namespace DMap.Models;

/// <summary>
/// Built-in stamp templates available in the first stamp-tool pass.
/// </summary>
public static class StampCatalog
{
    public static IReadOnlyList<StampTemplate> Templates { get; } =
    [
        new StampTemplate
        {
            Id = "blue-rectangle",
            DisplayName = "Bordered Blue Rectangle",
            AssetPath = "avares://DMap/Assets/Stamps/blue-rectangle.png",
            DefaultWidth = 180,
            DefaultHeight = 100,
        },
        new StampTemplate
        {
            Id = "spirit-guardians",
            DisplayName = "Spirit Guardians",
            AssetPath = "avares://DMap/Assets/Stamps/spirit-guardians.png",
            DefaultWidth = 100,
            DefaultHeight = 100,
        },
    ];

    public static StampTemplate? Find(string id)
    {
        foreach (var template in Templates)
        {
            if (template.Id == id)
                return template;
        }

        return null;
    }
}
