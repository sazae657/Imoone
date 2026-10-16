using System.Data.SQLite;

namespace ImogeIndex.Web6.Dictators;

/// <summary>
/// 一覧用ｻﾑﾈのﾃﾞｺーﾀﾞー(UIﾌﾚーﾑﾜーｸごとに用意する)
/// </summary>
/// <typeparam name="TBitmap">表示用の画像型</typeparam>
public interface ITileBitmapDecoder<TBitmap> where TBitmap : class
{
    /// <summary>
    /// BLOBから画像を作る(ﾜーｶーから呼ばれる)
    /// </summary>
    /// <param name="bytes">ｻﾑﾈのBLOB</param>
    /// <param name="size">長辺の上限</param>
    /// <param name="portrait">縦長？</param>
    /// <returns>読めなければnull</returns>
    TBitmap? Decode(byte[] bytes, int size, bool portrait);

    /// <summary>
    /// ｷｬｯｼｭの重さ(ﾊﾞｲﾄ)
    /// </summary>
    /// <param name="bitmap"></param>
    /// <returns></returns>
    long Measure(TBitmap bitmap);
}

/// <summary>
/// 一覧用のｻﾑﾈ読み込むﾏﾝ(ﾜーｶーで動く事に注意)
/// </summary>
/// <typeparam name="TBitmap">表示用の画像型</typeparam>
public sealed class TileThumbnailDictator<TBitmap> : IDisposable, IAsyncDisposable where TBitmap : class
{
    const int MaxPending = 512;
    const int MaxEntries = 2048;
    const long MaxBytes = 32 * 1024 * 1024;
    readonly object zndmn = new();
    readonly SemaphoreSlim readySemtex = new(0, 1);
    readonly CancellationTokenSource stopping = new();
    readonly LinkedList<Request> queue = new();
    readonly Dictionary<Key, Request> pending = new();
    readonly Dictionary<Key, LinkedListNode<Cached>> cache = new();
    readonly LinkedList<Cached> recent = new();
    readonly Task[] workers;
    long cacheBytes;
    int generation;
    string? databasePath;
    bool disposedValue;

    private readonly record struct Key(int Generation, long Id, int Size);
    private sealed record Cached(Key Key, TBitmap? Bitmap, long Bytes);
    private sealed class Request(Key key, string path, bool portrait)
    {
        public Key Key { get; } = key;
        public string Path { get; } = path;
        public bool Portrait { get; } = portrait;
        public int Readers;
        public TaskCompletionSource<TBitmap?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// ﾃﾞｺーﾄﾞﾜーｶーの数(1本だと大ｱｲｺﾝで高速ｽｸﾛーﾙした時に追いつかない)
    /// </summary>
    static readonly int WorkerCount = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

    readonly ITileBitmapDecoder<TBitmap> decoder;

    public TileThumbnailDictator(ITileBitmapDecoder<TBitmap> bitmapDecoder)
    {
        decoder = bitmapDecoder;
        workers = Enumerable.Range(0, WorkerCount).Select(_ => Task.Run(WorkAsync)).ToArray();
    }
    public event Action? Changed;

    /// <summary>
    /// ﾘｾｯｼｭ
    /// </summary>
    /// <param name="path"></param>
    public void Reset(string? path)
    {
        lock (zndmn)
        {
            if (disposedValue) {
                return;
            }
            ClearState(path);
            Signal();
        }
        Changed?.Invoke();
    }

    private void ClearState(string? path)
    {
        generation++;
        databasePath = path;
        foreach (var request in pending.Values) {
            request.Completion.TrySetResult(null);
        }
        pending.Clear();
        queue.Clear();
        cache.Clear();
        recent.Clear();
        cacheBytes = 0;
    }

    /// <summary>
    /// ｻﾑﾈ要求
    /// </summary>
    /// <param name="id"></param>
    /// <param name="size"></param>
    /// <param name="portrait"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<TBitmap?> GetAsync(long id, int size, bool portrait, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Request request;
        lock (zndmn)
        {
            if (disposedValue || databasePath == null) {
                return null;
            }
            var key = new Key(generation, id, Math.Clamp(size, 1, 512));
            if (cache.TryGetValue(key, out var hit))
            {
                recent.Remove(hit);
                recent.AddFirst(hit);
                return hit.Value.Bitmap;
            }
            if (!pending.TryGetValue(key, out request!))
            {
                // 待ち行列と待機ﾀｽｸが詰まるので新しい表示範囲を優先しとく
                while (queue.Count >= MaxPending)
                {
                    var old = queue.First!.Value;
                    queue.RemoveFirst();
                    pending.Remove(old.Key);
                    old.Completion.TrySetResult(null);
                }
                request = new Request(key, databasePath, portrait);
                pending.Add(key, request);
                queue.AddLast(request);
            }
            request.Readers++;
            Signal();
        }
        try {
            return await request.Completion.Task.WaitAsync(token).ConfigureAwait(false);
        }
        finally {
            lock (zndmn) {
                request.Readers--;
            }
        }
    }

    private void Signal()
    {
        if (readySemtex.CurrentCount == 0) {
            readySemtex.Release();
        }
    }

    /// <summary>
    /// ﾃﾞｺーﾄﾞしたり色々するﾜーｶー
    /// </summary>
    /// <returns></returns>
    private async Task WorkAsync()
    {
        SQLiteConnection? connection = null;
        // 毎回ｺﾏﾝﾄﾞを作り直すとSQLの準備が走るので接続ごとに使い回す
        SQLiteCommand? command = null;
        var connectionGeneration = -1;
        try
        {
            while (true)
            {
                await readySemtex.WaitAsync(stopping.Token).ConfigureAwait(false);
                while (true)
                {
                    Request? request;
                    int nextGeneration;
                    bool abandoned;
                    lock (zndmn)
                    {
                        nextGeneration = generation;
                        request = queue.First?.Value;
                        if (request != null) {
                            queue.RemoveFirst();
                        }
                        abandoned = request != null && request.Readers == 0;
                        if (abandoned) {
                            pending.Remove(request!.Key);
                            request.Completion.TrySetResult(null);
                        }
                        if (queue.Count > 0) {
                            // 残りは他のﾜーｶーにも起きてもらう
                            Signal();
                        }
                    }

                    if (connectionGeneration != nextGeneration) {
                        // ここはずんだもんﾛｯｸ外でやる(なんかﾌﾘーｽﾞする)
                        CloseConnection();
                        connectionGeneration = nextGeneration;
                    }
                    if (request == null) {
                        break;
                    }
                    if (abandoned) {
                        continue;
                    }
                    stopping.Token.ThrowIfCancellationRequested();
                    TBitmap? bitmap = null;
                    try
                    {
                        if (connection == null && File.Exists(request.Path))
                        {
                            connection = new SQLiteConnection(new SQLiteConnectionStringBuilder
                            {
                                DataSource = request.Path, ReadOnly = true, FailIfMissing = true, BusyTimeout = 100
                            }.ToString());
                            connection.Open();
                            command = new SQLiteCommand("SELECT thumbnail FROM thumbnails WHERE id=@id", connection);
                            command.Parameters.Add("@id", System.Data.DbType.Int64);
                            command.Prepare();
                        }
                        if (command != null) {
                            bitmap = Read(command, request);
                        }
                    }
                    catch (Exception) {
                        // 欠損してるか破損してる
                        CloseConnection();
                    }
                    lock (zndmn) {
                        if (!disposedValue && request.Key.Generation == generation) {
                            AddCache(request.Key, bitmap);
                            pending.Remove(request.Key);
                            request.Completion.TrySetResult(bitmap);
                        }
                        else {
                            request.Completion.TrySetResult(null);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        finally {
            CloseConnection();
        }

        void CloseConnection()
        {
            command?.Dispose();
            command = null;
            connection?.Dispose();
            connection = null;
        }
    }

    /// <summary>
    /// BLOBから画像ﾛーﾄﾞ
    /// </summary>
    /// <param name="command">準備済みのSELECT</param>
    /// <param name="request"></param>
    /// <returns></returns>
    private TBitmap? Read(SQLiteCommand command, Request request)
    {
        command.Parameters[0].Value = request.Key.Id;
        if (command.ExecuteScalar() is not byte[] bytes) {
            return null;
        }
        return decoder.Decode(bytes, request.Key.Size, request.Portrait);
    }

    /// <summary>
    /// 表示用ｷｬｯｼｭ
    /// </summary>
    /// <param name="key"></param>
    /// <param name="bitmap"></param>
    private void AddCache(Key key, TBitmap? bitmap)
    {
        var bytes = bitmap == null ? 0 : decoder.Measure(bitmap);
        if (bytes > MaxBytes) {
            return;
        }
        while (recent.Count > 0 && (cache.Count >= MaxEntries || cacheBytes + bytes > MaxBytes))
        {
            var old = recent.Last!.Value;
            cacheBytes -= old.Bytes;
            cache.Remove(old.Key);
            recent.RemoveLast();
        }
        cache[key] = recent.AddFirst(new Cached(key, bitmap, bytes));
        cacheBytes += bytes;
    }

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.WhenAll(workers).ConfigureAwait(false);
    }

    private void Dispose(bool disposing) {
        lock (zndmn) {
            if (disposedValue) {
                return;
            }
            disposedValue = true;
            ClearState(null);
        }
        Changed?.Invoke();
        stopping.Cancel();
        // 終了時にUIｽﾚｯﾄﾞを待たせるとUX悪いのでﾜーｶーで後始末する
        _ = Task.WhenAll(workers)
            .ContinueWith(_ => {
                readySemtex.Dispose();
                stopping.Dispose();
            }, TaskScheduler.Default);
    }

    public void Dispose() {
        // このｺーﾄﾞを変更しないでください。ｸﾘーﾝｱｯﾌﾟ ｺーﾄﾞを 'Dispose(bool disposing)' ﾒｿｯﾄﾞに記述します
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
