namespace ImogeIndex.Sources;

/// <summary>
/// ﾌｫﾙﾀﾞー情報
/// </summary>
/// <param name="Location">正規化済みの場所</param>
/// <param name="IsLink">ﾘﾝｸ(再解析ﾎﾟｲﾝﾄなど)なので辿らない</param>
public sealed record SourceDirectory(string Location, bool IsLink) {
    public override string ToString() => Location;
}

/// <summary>
/// ﾌｧｲﾙ供給元(列挙や読み込みの失敗はｲﾝﾃﾞｸｻーの都合上、IOExceptionかUnauthorizedAccessExceptionで投げる)
/// </summary>
public interface IFileSource {
    /// <summary>
    /// ｽｷーﾑ(ﾛーｶﾙは "file")
    /// </summary>
    string Scheme { get; }

    /// <summary>
    /// 場所の比較方法(大文字小文字を区別するか)
    /// </summary>
    StringComparer Comparer { get; }

    /// <summary>
    /// 場所の正規化(末尾の区切りを落とすなど)
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    string Normalize(string location);

    /// <summary>
    /// 親の場所(ﾙーﾄならnull)
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    string? GetParent(string location);

    /// <summary>
    /// targetはbasePathと同じかその配下？
    /// </summary>
    /// <param name="target"></param>
    /// <param name="basePath"></param>
    /// <returns></returns>
    bool IsSubdirectoryOrSame(string target, string basePath);

    /// <summary>
    /// ﾌｫﾙﾀﾞーを取得
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    SourceDirectory GetDirectory(string location);

    /// <summary>
    /// ﾌｫﾙﾀﾞーの更新日時(UnixTimeﾐﾘ秒)、あてにならない供給元はnull
    /// </summary>
    /// <param name="directory"></param>
    /// <returns></returns>
    long? GetTimestamp(SourceDirectory directory);

    /// <summary>
    /// 直下のﾌｫﾙﾀﾞー
    /// </summary>
    /// <param name="directory"></param>
    /// <returns></returns>
    IReadOnlyList<SourceDirectory> GetDirectories(SourceDirectory directory);

    /// <summary>
    /// 直下のﾌｧｲﾙ
    /// </summary>
    /// <param name="directory"></param>
    /// <returns></returns>
    IReadOnlyList<SourceFile> GetFiles(SourceDirectory directory);

    /// <summary>
    /// ﾌｧｲﾙを取得(無ければnull)
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    SourceFile? TryGetFile(string location);

    /// <summary>
    /// 読み込み用に開く
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    Stream OpenRead(string location);
}

/// <summary>
/// 供給元が見つからない、または繋がらない
/// </summary>
public sealed class SourceUnavailableException : IOException {
    public SourceUnavailableException(string message) : base(message) { }
}
