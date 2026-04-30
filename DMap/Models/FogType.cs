namespace DMap.Models;

/// <summary>
/// Visual style of the fog overlay. <see cref="Color"/> renders a flat colour chosen by the DM;
/// the remaining values render procedurally generated noise textures themed to the name.
/// </summary>
public enum FogType
{
    /// <summary>Flat colour fill picked by the DM.</summary>
    Color,

    /// <summary>Soft, light cloud-like noise.</summary>
    Cloud,

    /// <summary>Low-contrast grey haze.</summary>
    Fog,

    /// <summary>Brown/tan textured noise resembling earth or sand.</summary>
    Earth,

    /// <summary>Mottled green textured noise resembling forest canopy.</summary>
    Forest,

    /// <summary>Blue/teal swells with white foam at peaks.</summary>
    Ocean,

    /// <summary>Mid-grey textured noise resembling rough stone.</summary>
    Stone,

    /// <summary>Stone bricks laid in a running-bond pattern with mortar lines and per-brick variation.</summary>
    Bricks,

    /// <summary>Dark blue/purple gradient for an otherworldly look.</summary>
    Void,

    /// <summary>Deep blue-black with sparse bright pixels resembling stars.</summary>
    Night,
}
