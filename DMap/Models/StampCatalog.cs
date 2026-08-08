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
            Id = "circle",
            DisplayName = "Circle",
            AssetPath = "avares://DMap/Assets/Stamps/circle.png",
            DefaultWidth = 180,
            DefaultHeight = 180,
        },
        new StampTemplate
        {
            Id = "square",
            DisplayName = "Square",
            AssetPath = "avares://DMap/Assets/Stamps/square.png",
            DefaultWidth = 180,
            DefaultHeight = 180,
        },
        new StampTemplate
        {
            Id = "spirit-guardians",
            DisplayName = "Spirit Guardians",
            AssetPath = "avares://DMap/Assets/Stamps/spirit-guardians.png",
            DefaultWidth = 200,
            DefaultHeight = 200,
        },
    ];

    public static StampTemplate? Find(string id)
    {
        foreach (var template in Templates)
        {
            if (template.Id == id)
            {
                return template;
            }
        }

        return null;
    }
}
