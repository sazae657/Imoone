namespace ImogeIndex.Web6.Ganadores;

/// <summary>
/// ﾄﾚｲｱｲｺﾝのｺﾏﾝﾄﾞﾊﾝﾄﾞﾗー
/// </summary>
/// <param name="text"></param>
/// <param name="execute"></param>
/// <param name="canExecute"></param>
public sealed class TrayMenuCommand(string text, Action execute, Func<bool>? canExecute = null)
{
    internal string Text => text;
    internal bool IsEnabled => canExecute?.Invoke() ?? true;

    internal void Invoke()
    {
        if (IsEnabled)
        {
            execute();
        }
    }
}
