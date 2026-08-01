using System;

using Avalonia.Controls;
using Avalonia.Svg.Skia;

using DMap.Commands;

namespace DMap.Controls.MapCanvas.Stamp;

public sealed class StampContextMenuBuilder
{
    readonly Uri _iconBaseUri;

    public StampContextMenuBuilder(Uri iconBaseUri)
    {
        _iconBaseUri = iconBaseUri;
    }

    public ContextMenu Build(
        Action bringToFront,
        Action bringForward,
        Action sendBackward,
        Action sendToBack,
        Action duplicate)
    {
        ArgumentNullException.ThrowIfNull(bringToFront);
        ArgumentNullException.ThrowIfNull(bringForward);
        ArgumentNullException.ThrowIfNull(sendBackward);
        ArgumentNullException.ThrowIfNull(sendToBack);
        ArgumentNullException.ThrowIfNull(duplicate);

        return new ContextMenu
        {
            Placement = PlacementMode.Pointer,
            ItemsSource = new Control[]
            {
                CreateMenuItem("Bring to Front", "bring-to-front.svg", bringToFront),
                CreateMenuItem("Bring Forward", "move-up.svg", bringForward),
                CreateMenuItem("Send Backward", "move-down.svg", sendBackward),
                CreateMenuItem("Send to Back", "send-to-back.svg", sendToBack),
                CreateMenuItem("Duplicate", "copy.svg", duplicate),
            },
        };
    }

    MenuItem CreateMenuItem(string header, string iconFileName, Action action)
    {
        var uri = new Uri(_iconBaseUri, iconFileName);
        return new MenuItem
        {
            Header = header,
            Icon = new Image
            {
                Width = 16,
                Height = 16,
                Source = new SvgImage { Source = SvgSource.Load(uri.ToString(), null) },
            },
            Command = new RelayCommand(action),
        };
    }
}
