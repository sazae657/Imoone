namespace ImogeIndex.Zenerators;

/// <summary>
/// 例外拡張
/// </summary>
public static class ExceptionOfExtensions
{
    /// <summary>
    /// 停止要求による中断か判定
    /// </summary>
    /// <param name="r"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static bool IsCancellationOf(this Exception r, CancellationToken token)
    {
        if (!token.IsCancellationRequested) {
            return false;
        }
        return r switch {
            OperationCanceledException => true,
            AggregateException x => x.Flatten().InnerExceptions is { Count: > 0 } inp && inp.All(e => e is OperationCanceledException),
            _ => false,
        };
    }
}
