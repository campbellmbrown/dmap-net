using System;

using Avalonia.Controls;

using DMap.Commands;

namespace DMap.Controls.MapCanvas.Stamp;

public sealed class StampContextMenuBuilder
{
    public static ContextMenu Build(
        Action bringToFront,
        Action bringForward,
        Action sendBackward,
        Action sendToBack,
        Action duplicate,
        Action delete)
    {
        ArgumentNullException.ThrowIfNull(bringToFront);
        ArgumentNullException.ThrowIfNull(bringForward);
        ArgumentNullException.ThrowIfNull(sendBackward);
        ArgumentNullException.ThrowIfNull(sendToBack);
        ArgumentNullException.ThrowIfNull(duplicate);
        ArgumentNullException.ThrowIfNull(delete);

        return new ContextMenu
        {
            Placement = PlacementMode.Pointer,
            ItemsSource = new Control[]
            {
                CreateMenuItem("Bring Forward", "move-up", bringForward),
                CreateMenuItem("Send Backward", "move-down", sendBackward),
                new Separator(),
                CreateMenuItem("Bring to Front", "bring-to-front", bringToFront),
                CreateMenuItem("Send to Back", "send-to-back", sendToBack),
                new Separator(),
                CreateMenuItem("Duplicate", "copy", duplicate),
                new Separator(),
                CreateMenuItem("Delete", "trash-2", delete),
            },
        };
    }

    static MenuItem CreateMenuItem(string header, string iconName, Action action)
    {
        return new MenuItem
        {
            Header = header,
            Icon = new Icon
            {
                Width = 16,
                Height = 16,
                IconName = iconName,
            },
            Command = new RelayCommand(action),
        };
    }
}
