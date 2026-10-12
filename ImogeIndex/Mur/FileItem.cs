using ImogeIndex.Storage;

namespace ImogeIndex.Mur;

/// <summary>
/// 検索対象のﾌｨーﾙﾄﾞ
/// </summary>
public enum SearchField {
    Name,
    Path,
    Tag,
    Keyword,
    Character,
    Costume,
    Scene,
    Objects,
}

/// <summary>
/// 都合の良いimoge_index
/// </summary>
public class FileItem {
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public string? Keyword { get; set; }

    public string? Character { get; set; }

    public string? Costume { get; set; }
    public string? Scene { get; set; }

    public string? Objects { get; set; }
    public DateTime DateTime { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }

    public ulong PHash { get; set; }
    public ulong AHash { get; set; }
    public ulong DHash { get; set; }
    public ulong WHash { get; set; }

    /// <summary>
    /// 解析完了ﾌﾗｸﾞ(ﾌﾟﾚーﾝ画像とか0になるので0 == 未完了はﾀﾞﾒ)
    /// </summary>
    public bool HasImageHashes { get; set; }

    /// <summary>
    /// ﾌｨーﾙﾄﾞ数
    /// </summary>
    private static readonly int SearchFieldCount = Enum.GetValues<SearchField>().Length;

    /// <summary>
    /// 検索用に正規化した文字列
    /// </summary>
    private string[]? searchCache;

    public FileItem() { }


    /// <summary>
    /// 検索語/検索対象の正規化＆Ordinal比較するのに小文字で揃える
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string Normalize(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return string.Empty;
        }
        foreach (var c in value) {
            if (char.IsSurrogate(c) || char.ToLowerInvariant(c) != c) {
                return SearchIndexDataum.Normalize(value);
            }
        }
        return value;
    }


    /// <summary>
    /// 正規化済みの検索対象文字列
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    public string GetSearchText(SearchField field)
        => (searchCache ?? BuildSearchCache())[(int)field];


    /// <summary>
    /// 正規化済み文字列作るﾏﾝ
    /// </summary>
    /// <returns></returns>
    public string[] BuildSearchCache() {
        var cache = new string[SearchFieldCount];
        cache[(int)SearchField.Name] = Normalize(Name);
        cache[(int)SearchField.Path] = Normalize(Path);
        cache[(int)SearchField.Tag] = Normalize(Tag);
        cache[(int)SearchField.Keyword] = Normalize(Keyword);
        cache[(int)SearchField.Character] = Normalize(Character);
        cache[(int)SearchField.Costume] = Normalize(Costume);
        cache[(int)SearchField.Scene] = Normalize(Scene);
        cache[(int)SearchField.Objects] = Normalize(Objects);
        searchCache = cache;
        return cache;
    }

    /// <summary>
    /// 中身が同じ文字列を共有させる
    /// </summary>
    /// <param name="pool"></param>
    internal void ShareStrings(Dictionary<string, string> pool) {
        Path = Share(pool, Path);
        if (searchCache != null) {
            searchCache[(int)SearchField.Path] = Share(pool, searchCache[(int)SearchField.Path]);
        }

        static string Share(Dictionary<string, string> pool, string value) {
            if (pool.TryGetValue(value, out var shared)) {
                return shared;
            }
            pool.Add(value, value);
            return value;
        }
    }

    /// <summary>
    /// FileRecordを都合良く加工
    /// FileRecordはｷｬﾌﾟｼｮﾝ由来の文字列を山ほど抱えてるので参照は残さない
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    public static FileItem? Parse(FileRecord x) {
        if (string.IsNullOrEmpty(x.Path)) {
            return null;
        }
        var fileName = System.IO.Path.GetFileName(x.Path);
        if (string.IsNullOrEmpty(fileName)) {
            return null;
        }
        var dirName = !string.IsNullOrEmpty(x.Container)
            ? x.Container
            : (System.IO.Path.GetDirectoryName(x.Path) ?? string.Empty);
        var fr = new FileItem
        {
            Id = x.Id,
            Name = fileName,
            Path = dirName,
            FullName = x.Path,
            Tag = x.Tags ?? "",
            Character = x.Characters,
            Keyword = x.Keywords,
            Costume = x.Costume,
            Scene = x.Scene,
            Objects = x.Objects,
            DateTime = x.CreatedAt,
            Width = x.Width,
            Height = x.Height,
            PHash = x.PHash,
            AHash = x.AHash,
            DHash = x.DHash,
            WHash = x.WHash,
            HasImageHashes = x.Version == SystemParams.VersionCurrent && x.Width > 0 && x.Height > 0,
        };
        fr.BuildSearchCache();
        return fr;
    }
}
