using System.Windows.Input;

namespace ImogeIndex.Web6;

/// <summary>
/// ｺﾏﾝﾄﾞ
/// </summary>
public class KetCommand<T> : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public event Action<T>? Auction;
    public KetCommand()
    {
    }

    public KetCommand(Action<T> delegaty)
    {
        Auction = delegaty;
    }

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public void Execute(object? parameter)
    {
        if (parameter is T value) {
            Auction?.Invoke(value);
        }
    }
}
