using System.IO.Pipes;
using System.Text;

namespace ImogeIndex.Territories;

/// <summary>
/// ｲﾝﾃﾞｯｸｽ生成関連の通知ﾏﾝ
/// </summary>
public static class IndexingCompletionNotifier {
    const string PipeName = "ImogeIndex.{8BE579D0-5424-4073-B82D-EA143D5E0028}.Web3";
    const int timeout = 200;
    const int RetryCount = 3;

    static PipeOptions PipeOptionsOf => PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;

    /// <summary>
    /// ｲﾓｹﾞｼﾝｸﾞにｲﾝﾃﾞｯｸｽ更新完了通知を発射
    /// </summary>
    /// <param name="profileName"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static async Task<bool> NotifyAsync(string profileName, CancellationToken token = default) {
        if (string.IsNullOrWhiteSpace(profileName)) {
            return false;
        }

        for (var attempt = 0; attempt < RetryCount; attempt++) {
            try {
                using var client = new NamedPipeClientStream(
                    ".", PipeName, PipeDirection.Out, PipeOptionsOf);
                await client.ConnectAsync(timeout, token);

                using var writer = new StreamWriter(
                    client, new UTF8Encoding(false), leaveOpen: true) {
                    AutoFlush = true
                };
                await writer.WriteLineAsync(profileName.AsMemory(), token);
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                return false;
            }
            catch (IOException) {
            }
            catch (TimeoutException) {
            }

            if (attempt + 1 < RetryCount) {
                try {
                    await Task.Delay(timeout, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    return false;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 完了通知を受信ﾏﾝ
    /// </summary>
    public sealed class Listener : IAsyncDisposable {
        readonly CancellationTokenSource tokenSource = new();
        Task? taskable;

        /// <summary>
        /// 受信ｲﾍﾞﾝﾄ
        /// </summary>
        public event Action<string>? Received;

        /// <summary>
        /// 受信待機
        /// </summary>
        public void Start() {
            if (taskable != null) {
                return;
            }
            taskable = ListenAsync(tokenSource.Token);
        }

        /// <summary>
        /// 受信待機
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task ListenAsync(CancellationToken cancellationToken) {
            while (!cancellationToken.IsCancellationRequested) {
                try {
                    using var server = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptionsOf);
                    await server.WaitForConnectionAsync(cancellationToken);

                    using var reader = new StreamReader(
                        server, new UTF8Encoding(false), false, 1024, leaveOpen: true);
                    var profileName = await reader.ReadLineAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(profileName) && profileName.Length <= 256) {
                        Received?.Invoke(profileName);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    break;
                }
                catch (IOException) {
                }
            }
        }

        /// <summary>
        /// 止める
        /// </summary>
        public async ValueTask DisposeAsync() {
            tokenSource.Cancel();
            if (taskable != null) {
                try {
                    await taskable;
                }
                catch (OperationCanceledException) {
                }
            }
            tokenSource.Dispose();
        }
    }
}
