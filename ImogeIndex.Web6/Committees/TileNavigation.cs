namespace ImogeIndex.Web6.Committees;

/// <summary>
/// ﾀｲﾙ表示でのｶーｿﾙ移動
/// </summary>
public enum TileMove
{
    Left,
    Right,
    Up,
    Down,
    Home,
    End,
    PageUp,
    PageDown,
}

/// <summary>
/// ﾀｲﾙ表示のｷー操作で移動先を決める
/// </summary>
public static class TileNavigation
{
    /// <summary>
    /// 移動先の位置
    /// </summary>
    /// <param name="move">移動</param>
    /// <param name="current">現在位置(無ければ負数)</param>
    /// <param name="columns">1行の列数</param>
    /// <param name="page">1画面の項目数</param>
    /// <param name="count">全項目数(1以上)</param>
    /// <returns>移動先(0～count-1)</returns>
    public static int Next(TileMove move, int current, int columns, int page, int count)
    {
        var hasCurrent = current >= 0;
        current = Math.Max(0, current);
        var next = move switch
        {
            TileMove.Left => current - 1,
            TileMove.Right => current + 1,
            TileMove.Up => current - columns,
            TileMove.Down => current + columns,
            TileMove.Home => 0,
            TileMove.End => count - 1,
            TileMove.PageUp => current - page,
            TileMove.PageDown => current + page,
            _ => current
        };
        // 現在位置が無ければ先頭から(Endだけは末尾へ)
        if (!hasCurrent && move != TileMove.End) {
            next = 0;
        }
        return Math.Clamp(next, 0, count - 1);
    }
}
