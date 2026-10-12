namespace ImogeIndex.Sources;

/// <summary>
/// ﾛーｶﾙFSの供給元
/// </summary>
public class LocalFileSource : IFileSource {
    /// <summary>
    /// 既定のｲﾝｽﾀﾝｽ
    /// </summary>
    public static LocalFileSource Default { get; } = new();

    public string Scheme => "file";

    public StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    public virtual string Normalize(string location) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(location));

    public virtual string? GetParent(string location) => Path.GetDirectoryName(location);

    public virtual bool IsSubdirectoryOrSame(string target, string basePath) {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(basePath)) {
            return false;
        }
        var fullTarget = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullBase = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullTarget.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase);
    }

    public virtual SourceDirectory GetDirectory(string location) {
        var attributes = File.GetAttributes(location);
        return new SourceDirectory(location, (attributes & FileAttributes.ReparsePoint) != 0);
    }

    public virtual long? GetTimestamp(SourceDirectory directory) =>
        new DateTimeOffset(new DirectoryInfo(directory.Location).LastWriteTime).ToUnixTimeMilliseconds();

    public virtual IReadOnlyList<SourceDirectory> GetDirectories(SourceDirectory directory) =>
        new DirectoryInfo(directory.Location).GetDirectories()
            .Select(x => new SourceDirectory(x.FullName, (x.Attributes & FileAttributes.ReparsePoint) != 0))
            .ToArray();

    public virtual IReadOnlyList<SourceFile> GetFiles(SourceDirectory directory) =>
        new DirectoryInfo(directory.Location).GetFiles().Select(x => x.AsSourceFile()).ToArray();

    public virtual SourceFile? TryGetFile(string location) {
        var file = new FileInfo(location);
        return file.Exists ? file.AsSourceFile() : null;
    }

    public virtual Stream OpenRead(string location) => File.OpenRead(location);
}
