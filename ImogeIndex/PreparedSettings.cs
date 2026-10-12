using ImogeIndex.Common;
using ImogeIndex.Niraction;
using ImogeIndex.Sources;
using System.Text.RegularExpressions;

namespace ImogeIndex;

/// <summary>
/// 色々処理したSettings
/// </summary>
public sealed class PreparedSettings {
    public Settings Source { get; }

    public DirectoryInfo Root => Source.Root!;
    public FileInfo? SettingFilePath => Source.SettingFilePath;
    public int LogLevel => Source.LogLevel;
    public int Threads => Source.Threads;
    public int BufferCount => Source.BufferCount;
    public ThumbnailSetting Thumbnail => Source.Thumbnail;
    public TaggerSetting Tagger => Source.Tagger;
    public CaptionizeSetting Captioning => Source.Captioning;

    public NiractionSetting Niraction => Source.Niraction;

    public bool CaptioningEnabled => Captioning.Enable;

    public IReadOnlyList<string> SearchPaths { get; }
    public IReadOnlyList<string> ExcludePaths { get; }
    public IReadOnlyList<Regex> ExcludePatterns { get; }
    public IReadOnlyList<Regex> ImageExtensions { get; }

    public IReadOnlyList<Regex> CaptioningMoreHere { get; }
    public IReadOnlyList<Regex> CaptioningNoMoreHere { get; }

    public IReadOnlyList<MetaAlias> MetadataSearchTags { get; }
    public DirectoryInfo CacheDirectory { get; }

    internal PreparedSettings(
        Settings source,
        IReadOnlyList<string> searchPaths,
        IReadOnlyList<string> excludePaths,
        IReadOnlyList<Regex> excludePatterns,
        IReadOnlyList<Regex> imageExtensions,
        IReadOnlyList<Regex> captMoreHere,
        IReadOnlyList<Regex> captNoMoreHere,
        DirectoryInfo cacheDirectory) {
        Source = source;
        SearchPaths = searchPaths;
        ExcludePaths = excludePaths;
        ExcludePatterns = excludePatterns;
        ImageExtensions = imageExtensions;
        CaptioningMoreHere = captMoreHere;
        CaptioningNoMoreHere = captNoMoreHere;
        // 一致もしくはalias一致を除いてﾃﾞﾌｫをまーじ
        MetadataSearchTags = SystemParams.DefaultMetaAliases
            .Where(defaultAlias => !source.MetadataSearchTags.Any(sourceAlias =>
                defaultAlias.Equals(sourceAlias) ||
                string.Equals(defaultAlias.Alias, sourceAlias.Alias, StringComparison.OrdinalIgnoreCase)))
            .Concat(source.MetadataSearchTags)
            .Distinct()
            .ToArray();
        MetaAlias.ValidateType(MetadataSearchTags);
        CacheDirectory = cacheDirectory;
    }

    /// <summary>
    /// それｻﾌﾞﾌｫﾙﾀﾞー？
    /// </summary>
    /// <param name="targetPath"></param>
    /// <param name="basePath"></param>
    /// <returns></returns>
    private static bool WasSubdirectoryOrSame(string targetPath, string basePath) {
        if (string.IsNullOrWhiteSpace(targetPath) || string.IsNullOrWhiteSpace(basePath)) {
            return false;
        }
        var sources = FileSourceRegistry.Instance;
        // 供給元が違えば配下ではない
        if (!sources.TryResolve(targetPath, out var target) ||
            !sources.TryResolve(basePath, out var source) ||
            !ReferenceEquals(target, source)) {
            return false;
        }
        try {
            return target.IsSubdirectoryOrSame(targetPath, basePath);
        }
        catch {
            return false;
        }
    }

    /// <summary>
    /// それ対象ﾌｫﾙﾀﾞー？
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool WasIncludePath(string path) => SearchPaths.Any(x => WasSubdirectoryOrSame(path, x));

    /// <summary>
    /// それ無視ﾌｫﾙﾀﾞー？
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool WasExcludePath(string path) => ExcludePaths.Any(x => WasSubdirectoryOrSame(path, x));

    /// <summary>
    /// それ対象の画像拡張子付いてる？
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool WasTargetImage(SourceFile path) {
        return WasTargetExtension(path.Extension) && !WasExcldeFile(path.Location);
    }

    /// <summary>
    /// それ対象の画像拡張子付いてる？
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool WasTargetImage(string path) {
        return WasTargetExtension(Path.GetExtension(path)) && !WasExcldeFile(path);
    }

    /// <summary>
    /// それ対象の拡張子？
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    bool WasTargetExtension(string extension) => ImageExtensions.Any(x => x.IsMatch(extension));

    /// <summary>
    /// それ無視ﾌｧｲﾙ？
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool WasExcldeFile(string path) => ExcludePatterns.Any(x => x.IsMatch(path));

    /// <summary>
    /// name置き場を作る
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public FileInfo CreateLocalStateFile(string name) {
        if (!Root.Exists) {
            Root.Create();
        }
        return Root.CombineFile(name);
    }

    /// <summary>
    /// nameﾌｫﾙﾀﾞー作る
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public DirectoryInfo CreateLocalStateDir(string name) {
        if (!Root.Exists) {
            Root.Create();
        }
        var directory = Root.CombineDir(name);
        if (!directory.Exists) {
            directory.Create();
        }
        return directory;
    }

    /// <summary>
    /// ｴｷﾊﾟﾝ
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public string Expand(string value) => Source.ExpandingClass(value);

    /// <summary>
    /// ｲﾝﾃﾞｯｸｽﾃﾞーﾀﾑのﾊﾟｽ
    /// </summary>
    /// <returns></returns>
    public FileInfo GetIndexDatumPath() => CreateLocalStateFile(SystemParams.IndexDatumName);

    /// <summary>
    /// ｻﾑﾈﾃﾞーﾀﾑのﾊﾟｽ
    /// </summary>
    /// <returns></returns>
    public FileInfo GetThumbnailDatumPath() => CreateLocalStateFile(SystemParams.ThumbnailDatumName);
    

}
