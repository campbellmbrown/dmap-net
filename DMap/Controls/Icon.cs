using System;
using System.Collections.Concurrent;
using System.Threading;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace DMap.Controls;

public sealed class Icon : Control
{
    public static readonly StyledProperty<string?> IconNameProperty =
        AvaloniaProperty.Register<Icon, string?>(nameof(IconName));

    public static readonly StyledProperty<IconVariant> VariantProperty =
        AvaloniaProperty.Register<Icon, IconVariant>(nameof(Variant), IconVariant.Normal);

    static readonly ConcurrentDictionary<string, Lazy<SvgImage>> _imageCache = new();

    SvgImage? _image;

    public string? IconName
    {
        get => GetValue(IconNameProperty);
        set => SetValue(IconNameProperty, value);
    }

    public IconVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    static Icon()
    {
        AffectsMeasure<Icon>(IconNameProperty, VariantProperty);
        AffectsRender<Icon>(IconNameProperty, VariantProperty);
        IconNameProperty.Changed.AddClassHandler<Icon>((icon, _) => icon.UpdateImage());
        VariantProperty.Changed.AddClassHandler<Icon>((icon, _) => icon.UpdateImage());
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_image is null)
        {
            UpdateImage();
        }

        if (_image is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var sourceRect = new Rect(_image.Size);
        var targetRect = GetUniformTargetRect(_image.Size, Bounds.Size);
        ((IImage)_image).Draw(context, sourceRect, targetRect);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = _image?.Size ?? new Size(16, 16);
        var width = double.IsInfinity(availableSize.Width) ? size.Width : Math.Min(size.Width, availableSize.Width);
        var height = double.IsInfinity(availableSize.Height) ? size.Height : Math.Min(size.Height, availableSize.Height);
        return new Size(width, height);
    }

    void UpdateImage()
    {
        if (string.IsNullOrWhiteSpace(IconName))
        {
            _image = null;
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        _image = GetImage(IconSource.ForVariant(IconName, Variant));
        InvalidateMeasure();
        InvalidateVisual();
    }

    static SvgImage GetImage(Uri uri)
    {
        return _imageCache.GetOrAdd(
            uri.ToString(),
            _ => new Lazy<SvgImage>(() => CreateImage(uri), LazyThreadSafetyMode.ExecutionAndPublication)
        ).Value;
    }

    static SvgImage CreateImage(Uri uri)
    {
        return new SvgImage
        {
            Source = SvgSource.Load(uri.ToString(), null),
        };
    }

    static Rect GetUniformTargetRect(Size sourceSize, Size bounds)
    {
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return new Rect(bounds);
        }

        var scale = Math.Min(bounds.Width / sourceSize.Width, bounds.Height / sourceSize.Height);
        var width = sourceSize.Width * scale;
        var height = sourceSize.Height * scale;
        return new Rect((bounds.Width - width) / 2, (bounds.Height - height) / 2, width, height);
    }
}
