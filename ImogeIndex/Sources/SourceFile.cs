namespace ImogeIndex.Sources;

/// <summary>
/// ﾌｧｲﾙ情報
/// </summary>
/// <param name="Location">正規化済みの場所</param>
/// <param name="Container">親ﾌｫﾙﾀﾞーの場所</param>
/// <param name="Name">ﾌｧｲﾙ名</param>
/// <param name="Length">ｻｲｽﾞ</param>
/// <param name="LastWriteTime">更新日時</param>
/// <param name="CreationTime">作成日時(取れない供給元はnull)</param>
public sealed record SourceFile(
    string Location,
    string Container,
    string Name,
    long Length,
    DateTime LastWriteTime,
    DateTime? CreationTime) {

    /// <summary>
    /// 拡張子(ﾄﾞｯﾄ付き)
    /// </summary>
    public string Extension => Path.GetExtension(Name);

    public override string ToString() => Location;
}

/// <summary>
/// ﾛーｶﾙﾌｧｲﾙ関連
/// </summary>
public static class LocalSourceExtension {
    /// <summary>
    /// FileInfoからSourceFileを作る
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public static SourceFile AsSourceFile(this FileInfo file) {
        ArgumentNullException.ThrowIfNull(file);
        return new SourceFile(
            file.FullName,
            file.DirectoryName ?? string.Empty,
            file.Name,
            file.Length,
            file.LastWriteTime,
            file.CreationTime);
    }
}
