using ImogeIndex.Common;
using System.Diagnostics.CodeAnalysis;

namespace ImogeIndex.Sources;

/// <summary>
/// 場所からﾌｧｲﾙ供給元を引く
/// ｽｷーﾑ付き(scheme://...)ならｽｷーﾑで、それ以外はﾛーｶﾙとして扱う
/// </summary>
public sealed class FileSourceRegistry {
    /// <summary>
    /// 既定のｲﾝｽﾀﾝｽ
    /// </summary>
    public static FileSourceRegistry Instance { get; } = new(LocalFileSource.Default);

    const string SchemeSeparator = "://";

    readonly object gate = new();
    // 読む側はﾛｯｸ無しで見るので書き換えは丸ごと差し替える
    volatile Dictionary<string, IFileSource> schemes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// ｽｷーﾑ無しの場所を受け持つ供給元
    /// </summary>
    public IFileSource Local { get; }

    /// <summary>
    /// 場所の比較(供給元ごとの大文字小文字の扱いに従う)
    /// </summary>
    public StringComparer Comparer { get; }

    public FileSourceRegistry(IFileSource local, params IFileSource[] sources) {
        ArgumentNullException.ThrowIfNull(local);
        Local = local;
        Comparer = new LocationComparer(this);
        foreach (var x in sources) {
            Register(x);
        }
    }

    /// <summary>
    /// 供給元を登録する(Disposeで登録解除)
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">同じｽｷーﾑが登録済み</exception>
    public IDisposable Register(IFileSource source) {
        ArgumentNullException.ThrowIfNull(source);
        lock (gate) {
            if (schemes.ContainsKey(source.Scheme)) {
                throw new InvalidOperationException($"スキーム {source.Scheme} は登録済み");
            }
            schemes = new(schemes, StringComparer.OrdinalIgnoreCase) { [source.Scheme] = source };
        }
        return new Registration(this, source);
    }

    /// <summary>
    /// 登録解除
    /// </summary>
    /// <param name="source"></param>
    void Unregister(IFileSource source) {
        lock (gate) {
            if (!schemes.TryGetValue(source.Scheme, out var current) || !ReferenceEquals(current, source)) {
                return;
            }
            var next = new Dictionary<string, IFileSource>(schemes, StringComparer.OrdinalIgnoreCase);
            next.Remove(source.Scheme);
            schemes = next;
        }
    }

    /// <summary>
    /// 場所のｽｷーﾑを取り出す(ｽｷーﾑ無しはnull)
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    public static string? GetScheme(string location) {
        var i = location.IndexOf(SchemeSeparator, StringComparison.Ordinal);
        // 1文字はﾄﾞﾗｲﾌﾞﾚﾀーと紛らわしいので認めない
        if (i < 2 || !char.IsAsciiLetter(location[0])) {
            return null;
        }
        for (int n = 1; n < i; n++) {
            var c = location[n];
            if (!char.IsAsciiLetterOrDigit(c) && c != '+' && c != '-' && c != '.') {
                return null;
            }
        }
        return location[..i];
    }

    /// <summary>
    /// 場所の供給元を引く
    /// </summary>
    /// <param name="location"></param>
    /// <param name="source"></param>
    /// <returns>未登録のｽｷーﾑならfalse</returns>
    public bool TryResolve(string location, [NotNullWhen(true)] out IFileSource? source) {
        var scheme = GetScheme(location);
        if (scheme == null) {
            source = Local;
            return true;
        }
        return schemes.TryGetValue(scheme, out source);
    }

    /// <summary>
    /// 場所の供給元を引く
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    /// <exception cref="SourceUnavailableException">未登録のｽｷーﾑ</exception>
    public IFileSource Resolve(string location) {
        if (TryResolve(location, out var source)) {
            return source;
        }
        throw new SourceUnavailableException(Properties.Resources.FS_E_UnknownScheme.Morfat(location));
    }

    /// <summary>
    /// 読み込み用に開く
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    public Stream OpenRead(string location) => Resolve(location).OpenRead(location);

    /// <summary>
    /// 全部読む
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    public byte[] ReadAllBytes(string location) {
        using var stream = OpenRead(location);
        if (stream.CanSeek) {
            var bytes = new byte[stream.Length - stream.Position];
            stream.ReadExactly(bytes);
            return bytes;
        }
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }


    /// <summary>
    /// 登録解除用
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="source"></param>
    sealed class Registration(FileSourceRegistry owner, IFileSource source) : IDisposable {
        public void Dispose() => owner.Unregister(source);
    }


    /// <summary>
    /// 供給元ごとの比較方法に従う比較
    /// </summary>
    /// <param name="owner"></param>
    sealed class LocationComparer(FileSourceRegistry owner) : StringComparer {
        StringComparer Of(string location) =>
            owner.TryResolve(location, out var source) ? source.Comparer : Ordinal;

        public override int Compare(string? x, string? y) {
            if (x == null || y == null) {
                return Ordinal.Compare(x, y);
            }
            var sx = GetScheme(x);
            var sy = GetScheme(y);
            if (!string.Equals(sx, sy, StringComparison.OrdinalIgnoreCase)) {
                // 供給元が違えばｽｷーﾑで並べる
                return OrdinalIgnoreCase.Compare(sx ?? string.Empty, sy ?? string.Empty);
            }
            return Of(x).Compare(x, y);
        }

        public override bool Equals(string? x, string? y) {
            if (x == null || y == null) {
                return x == y;
            }
            if (!string.Equals(GetScheme(x), GetScheme(y), StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            return Of(x).Equals(x, y);
        }

        public override int GetHashCode(string obj) => Of(obj).GetHashCode(obj);
    }
}
