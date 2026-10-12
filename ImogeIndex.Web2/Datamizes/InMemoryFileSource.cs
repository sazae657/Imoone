using ImogeIndex.Sources;

namespace ImogeIndex.Web2.Datamizes;

/// <summary>
/// ﾒﾓﾘ上のﾌｧｲﾙ供給元(ﾃｽﾄ用)
/// 場所は "scheme://ﾎｽﾄ/a/b.png" 形式で、大文字小文字を区別し、ﾌｫﾙﾀﾞーの日付は持たない
/// </summary>
sealed class InMemoryFileSource : IFileSource {
    readonly object gate = new();
    readonly Dictionary<string, Entry> files = new(StringComparer.Ordinal);
    readonly HashSet<string> directories = new(StringComparer.Ordinal);

    record Entry(byte[] Body, DateTime LastWriteTime);

    public string Scheme { get; }

    public StringComparer Comparer => StringComparer.Ordinal;

    /// <summary>
    /// 失敗させる場所(ﾌｫﾙﾀﾞー)
    /// </summary>
    public string? FailurePath { get; set; }

    public InMemoryFileSource(string scheme) {
        Scheme = scheme;
    }

    string Root => $"{Scheme}://";

    /// <summary>
    /// ﾌｧｲﾙを置く(親ﾌｫﾙﾀﾞーも作る)
    /// </summary>
    /// <param name="location"></param>
    /// <param name="body"></param>
    /// <param name="stamp"></param>
    public void Write(string location, byte[] body, DateTime stamp) {
        lock (gate) {
            files[location] = new Entry(body, stamp);
            for (var x = GetParent(location); x != null; x = GetParent(x)) {
                directories.Add(x);
            }
        }
    }

    /// <summary>
    /// ﾌｧｲﾙを消す
    /// </summary>
    /// <param name="location"></param>
    public void Delete(string location) {
        lock (gate) {
            files.Remove(location);
        }
    }

    public string Normalize(string location) {
        var trimmed = location.TrimEnd('/');
        return trimmed.Length < Root.Length ? location : trimmed;
    }

    public string? GetParent(string location) {
        location = Normalize(location);
        var i = location.LastIndexOf('/');
        // "scheme://ﾎｽﾄ" より上は無い
        return i < Root.Length ? null : location[..i];
    }

    public bool IsSubdirectoryOrSame(string target, string basePath) =>
        (Normalize(target) + "/").StartsWith(Normalize(basePath) + "/", StringComparison.Ordinal);

    void Check(string location) {
        if (location == FailurePath) {
            throw new IOException($"失敗のテスト: {location}");
        }
    }

    public SourceDirectory GetDirectory(string location) {
        Check(location);
        lock (gate) {
            if (!directories.Contains(location)) {
                throw new DirectoryNotFoundException(location);
            }
        }
        return new SourceDirectory(location, false);
    }

    public long? GetTimestamp(SourceDirectory directory) => null;

    public IReadOnlyList<SourceDirectory> GetDirectories(SourceDirectory directory) {
        Check(directory.Location);
        lock (gate) {
            return directories
                .Where(x => GetParent(x) == directory.Location)
                .Select(x => new SourceDirectory(x, false))
                .ToArray();
        }
    }

    public IReadOnlyList<SourceFile> GetFiles(SourceDirectory directory) {
        Check(directory.Location);
        lock (gate) {
            return files
                .Where(x => GetParent(x.Key) == directory.Location)
                .Select(x => ToSourceFile(x.Key, x.Value))
                .ToArray();
        }
    }

    public SourceFile? TryGetFile(string location) {
        lock (gate) {
            return files.TryGetValue(location, out var entry) ? ToSourceFile(location, entry) : null;
        }
    }

    public Stream OpenRead(string location) {
        lock (gate) {
            if (!files.TryGetValue(location, out var entry)) {
                throw new FileNotFoundException(location);
            }
            return new MemoryStream(entry.Body, writable: false);
        }
    }

    SourceFile ToSourceFile(string location, Entry entry) =>
        new(location, GetParent(location)!, location[(location.LastIndexOf('/') + 1)..],
            entry.Body.Length, entry.LastWriteTime, null);
}
