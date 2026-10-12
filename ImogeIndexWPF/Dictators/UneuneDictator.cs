using System.Windows.Threading;

namespace ImogeIndexWPF.Dictators;

/// <summary>
/// 子ﾌﾟﾛｸﾞﾚｽをｳﾈｳﾈさせる
/// </summary>
internal sealed class UneuneDictator
{
    /// <summary>
    /// 1回で動かす量
    /// </summary>
    public const double StepSize = 0.01;

    readonly Dispatcher dispatcher;
    readonly Action<double> delegaty;
    DispatcherTimer? timer;
    double value;
    bool forward = true;

    /// <summary>
    /// ｳﾈｳﾈ中
    /// </summary>
    public bool IsRunning => timer != null;

    /// <summary>
    /// ﾀー
    /// </summary>
    /// <param name="d"></param>
    /// <param name="m"></param>
    public UneuneDictator(Dispatcher d, Action<double> m)
    {
        dispatcher = d;
        delegaty = m;
    }

    /// <summary>
    /// ｳﾈｳﾈ開始
    /// </summary>
    public void Start()
    {
        if (timer != null) {
            return;
        }
        value = 0;
        forward = true;
        timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        timer.Tick += Timer_Tick;
        timer.Start();
    }

    /// <summary>
    /// ｳﾈｳﾈ停止
    /// </summary>
    public void Stop()
    {
        if (timer == null) {
            return;
        }
        timer.Stop();
        timer.Tick -= Timer_Tick;
        timer = null;
        delegaty(0);
    }

    /// <summary>
    /// ｳﾈｳﾆﾝｸﾞ
    /// </summary>
    /// <param name="value"></param>
    /// <param name="forward"></param>
    /// <returns></returns>
    internal static (double Value, bool Forward) Step(double value, bool forward)
    {
        if (forward) {
            value += StepSize;
            if (value > 1.0) {
                return (1.0, false);
            }
            return (value, true);
        }
        value -= StepSize;
        if (value < 0.0) {
            return (0.0, true);
        }
        return (value, false);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        (value, forward) = Step(value, forward);
        delegaty(value);
    }
}
