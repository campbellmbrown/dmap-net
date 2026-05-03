using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using DMap.Models;
using DMap.Services.Fog;

namespace DMap.Controls;

/// <summary>Snapshot of fog inputs needed to rebuild or update the renderable fog bitmap.</summary>
public readonly record struct FogBitmapSettings(
    FogMask? Mask,
    FogType FogType,
    Color FogColor,
    double FogOpacity,
    Guid FogSeed);

/// <summary>
/// Owns fog bitmap, fog texture generation, and dirty-region bitmap updates for <see cref="MapCanvas"/>.
/// </summary>
public sealed class FogBitmapController
{
    static readonly FogTextureGenerator _textureGenerator = new();

    WriteableBitmap? _bitmap;
    byte[]? _texture;
    int _generationVersion;
    FogBitmapSettings _settings;
    bool _isGenerating;

    /// <summary>Current renderable fog bitmap, or <see langword="null"/> when no mask is loaded.</summary>
    public WriteableBitmap? Bitmap => _bitmap;

    /// <summary><see langword="true"/> while a textured fog overlay is being generated.</summary>
    public bool IsGenerating
    {
        get => _isGenerating;
        private set
        {
            if (_isGenerating == value)
                return;

            _isGenerating = value;
            IsGeneratingChanged?.Invoke(this, value);
        }
    }

    /// <summary>Whether fog rendering has all resources required for the current fog type.</summary>
    public bool CanRender(FogBitmapSettings settings) =>
        settings.FogType == FogType.Color || _texture is not null;

    /// <summary>Raised when the owning control should redraw.</summary>
    public event EventHandler? Invalidated;

    /// <summary>Raised when <see cref="IsGenerating"/> changes.</summary>
    public event EventHandler<bool>? IsGeneratingChanged;

    /// <summary>Recreates the fog bitmap for the current mask.</summary>
    public void Rebuild(FogBitmapSettings settings)
    {
        _settings = settings;
        var mask = settings.Mask;
        if (mask is null)
        {
            _bitmap = null;
            _texture = null;
            _generationVersion++;
            IsGenerating = false;
            Invalidated?.Invoke(this, EventArgs.Empty);
            return;
        }

        _bitmap = new WriteableBitmap(
            new PixelSize(mask.Width, mask.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        RefreshTextureAndBitmap(settings);
    }

    /// <summary>Regenerates texture resources if needed, then refreshes the fog bitmap.</summary>
    public void RefreshTextureAndBitmap(FogBitmapSettings settings)
    {
        _settings = settings;
        var mask = settings.Mask;
        if (mask is null)
        {
            _texture = null;
            _generationVersion++;
            IsGenerating = false;
            Invalidated?.Invoke(this, EventArgs.Empty);
            return;
        }

        var type = settings.FogType;
        if (type == FogType.Color)
        {
            _texture = null;
            _generationVersion++;
            IsGenerating = false;
            InvalidateRegion(new PixelRect(0, 0, mask.Width, mask.Height), settings);
            return;
        }

        _ = RefreshTexturedFogAsync(mask.Width, mask.Height, type, settings.FogSeed, ++_generationVersion);
    }

    /// <summary>Updates the current bitmap for a changed fog-mask region.</summary>
    public void InvalidateRegion(PixelRect dirtyRect, FogBitmapSettings settings)
    {
        _settings = settings;
        UpdateRegion(dirtyRect, settings);
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    async Task RefreshTexturedFogAsync(int width, int height, FogType type, Guid seed, int generationVersion)
    {
        IsGenerating = true;
        Invalidated?.Invoke(this, EventArgs.Empty);

        try
        {
            var texture = await Task.Run(() => _textureGenerator.Generate(width, height, type, seed));
            var settings = _settings;
            var mask = settings.Mask;
            if (generationVersion != _generationVersion
                || mask is null
                || mask.Width != width
                || mask.Height != height
                || settings.FogType != type
                || settings.FogSeed != seed)
                return;

            _texture = texture;
            InvalidateRegion(new PixelRect(0, 0, width, height), settings);
        }
        finally
        {
            if (generationVersion == _generationVersion)
                IsGenerating = false;
        }
    }

    unsafe void UpdateRegion(PixelRect dirtyRect, FogBitmapSettings settings)
    {
        var mask = settings.Mask;
        if (mask is null || _bitmap is null)
            return;

        var fogOpacity = settings.FogOpacity;
        var texture = _texture;
        var color = settings.FogColor;
        var width = mask.Width;
        var useTexture = settings.FogType != FogType.Color;

        if (useTexture && texture is null)
            return;

        using var fb = _bitmap.Lock();
        var ptr = (byte*)fb.Address;
        var stride = fb.RowBytes;

        var minX = Math.Max(0, dirtyRect.X);
        var minY = Math.Max(0, dirtyRect.Y);
        var maxX = Math.Min(width, dirtyRect.X + dirtyRect.Width);
        var maxY = Math.Min(mask.Height, dirtyRect.Y + dirtyRect.Height);

        if (!useTexture)
        {
            byte cb = color.B, cg = color.G, cr = color.R;
            for (var y = minY; y < maxY; y++)
            {
                var row = ptr + y * stride;
                for (var x = minX; x < maxX; x++)
                {
                    var alpha = fogOpacity * (255 - mask[x, y]) / 255;
                    var offset = x * 4;
                    row[offset + 0] = (byte)(cb * alpha);
                    row[offset + 1] = (byte)(cg * alpha);
                    row[offset + 2] = (byte)(cr * alpha);
                    row[offset + 3] = (byte)(alpha * 255);
                }
            }

            return;
        }

        if (texture is null)
            return;

        fixed (byte* texPtr = texture)
        {
            for (var y = minY; y < maxY; y++)
            {
                var row = ptr + y * stride;
                var texRow = texPtr + y * width * 3;
                for (var x = minX; x < maxX; x++)
                {
                    var alpha = fogOpacity * (255 - mask[x, y]) / 255;
                    var offset = x * 4;
                    var texOffset = x * 3;
                    row[offset + 0] = (byte)(texRow[texOffset + 0] * alpha);
                    row[offset + 1] = (byte)(texRow[texOffset + 1] * alpha);
                    row[offset + 2] = (byte)(texRow[texOffset + 2] * alpha);
                    row[offset + 3] = (byte)(alpha * 255);
                }
            }
        }
    }
}
