using Avalonia.Input;

using DMap.Dm;

namespace DMap.Controls.MapCanvas;

/// <summary>Maps player viewport edit handles to pointer cursors.</summary>
internal static class PlayerViewportCursors
{
    public static Cursor GetCursor(PlayerViewportHandle handle) =>
        handle switch
        {
            PlayerViewportHandle.Move => new Cursor(StandardCursorType.SizeAll),
            PlayerViewportHandle.Top or PlayerViewportHandle.Bottom => new Cursor(StandardCursorType.SizeNorthSouth),
            PlayerViewportHandle.Left or PlayerViewportHandle.Right => new Cursor(StandardCursorType.SizeWestEast),
            PlayerViewportHandle.TopLeft or PlayerViewportHandle.BottomRight => new Cursor(StandardCursorType.TopLeftCorner),
            PlayerViewportHandle.TopRight or PlayerViewportHandle.BottomLeft => new Cursor(StandardCursorType.TopRightCorner),
            _ => Cursor.Default,
        };
}
