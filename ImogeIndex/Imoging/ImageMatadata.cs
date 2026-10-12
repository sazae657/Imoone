using Hnx8.ReadJEnc;
using ImogeIndex.Common;
using ImogeIndex.Sources;
using ImogeIndex.Storage;
using MetadataExtractor;
using MetadataExtractor.Formats.Xmp;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ImogeIndex.Imoging;

/// <summary>
/// ﾒﾀﾃﾞーﾀﾑのあたい
/// </summary>
public class ImageMetadataValue : IEquatable<ImageMetadataValue> {
    public string? Directory { get; set; }
    public string? Type { get; set; }
    public string? Tag { get; set; }
    public string? Value { get; set; }
    public double? NumericValue { get; set; }

    public ImageMetadataValue()
    {
    }

    public ImageMetadataValue(MetadataRecord record)
    {
        Directory = record.Directory;
        Type = record.Type;
        Tag = record.Tag;
        Value = record.Value;
        NumericValue = record.NumericValue;
    }

    #region IEquatable

    public bool Equals(ImageMetadataValue? other) {
        if (ReferenceEquals(this, other)) {
            return true;
        }
        if (other is null) {
            return false;
        }
        return string.Equals(Directory, other.Directory, StringComparison.InvariantCulture) &&
            string.Equals(Tag, other.Tag, StringComparison.InvariantCulture);
    }

    public override bool Equals(object? obj) => obj is ImageMetadataValue other && Equals(other);

    public override int GetHashCode() =>
        (Directory is null ? 0 : StringComparer.Ordinal.GetHashCode(Directory)) ^ (Tag is null ? 0 : StringComparer.Ordinal.GetHashCode(Tag));
    #endregion
}

/// <summary>
/// ﾒﾀﾃﾞーﾀﾑ
/// </summary>
public class ImageMetadataDatum {
    public ImageMetadataDatum() { }
    public ImageMetadataDatum(IEnumerable<MetadataRecord> records) {
        foreach (var x in records) {
            Tags.Add(new ImageMetadataValue(x));
        }
    }

    public long FileId { get; set; }
    public List<ImageMetadataValue> Tags { get; } = new();

    public DateTime? Timestamp { get; set; }

    public void Merge(ImageMetadataDatum datum)
    {
        foreach (var x in datum.Tags) {
            if (!Tags.Contains(x)) {
                Tags.Add(x);
            }
        }
        Opportunism();
    }

    /// <summary>
    /// 日付を拾う
    /// </summary>
    public void Opportunism()
    {
        DateTime? fallback = null;
        foreach (var x in Tags.Where(w => !string.IsNullOrEmpty(w.Tag))) {
            if (fallback == null &&
                x.Tag!.Equals("Date/Time", StringComparison.OrdinalIgnoreCase)) {
                // Date/Time Originalが無かった時用に控える
                if (SloppyDateTime.TryParse(x.Value, out var ts1)) {
                    fallback = ts1;
                }
            }

            // 日付を拾う
            if (Timestamp == null &&
                !string.IsNullOrEmpty(x.Value) &&
                x.Tag!.Equals("Date/Time Original", StringComparison.OrdinalIgnoreCase))
            {
                if (SloppyDateTime.TryParse(x.Value, out var ts2)) {
                    Timestamp = ts2;
                }
            }
        }
        if (Timestamp == null && fallback != null) {
            Timestamp = fallback;
        }
    }
}

/// <summary>
/// 画像のﾒﾀﾃﾞーﾀﾑ読み込みﾍﾙﾊﾟー
/// </summary>
public partial class ImageMetadataAnalyzer {
    /// <summary>
    /// 無視するﾃﾞｨﾚｸﾄﾘ
    /// </summary>
    static readonly HashSet<string> IgnoreDirs = [
        "JPEG",
        "JFIF",
        "Huffman",
        "File Type",
        "ICC Profile",
        "Adobe JPEG",
        "Photoshop"
        ];

    /// <summary>
    /// ﾊﾞｲﾅﾘー詰まってる系のﾃﾞｨﾚｸﾄﾘ
    /// </summary>
    static readonly string[] IgnoreDirsPvt = [
         "Olympus", "Interoperability", "PrintIM","Thumbnail"
    ];

    /// <summary>
    /// 対象ﾃﾞｨﾚｸﾄﾘ
    /// </summary>
    static readonly string[] AcceptDirs = [
        "Exif", "XMP", "GPS","JpegComment","IPTC", "Ducky", "PNG-tEXt", "PNG-iTXt"
    ];

    /// <summary>
    /// それ無視対象？
    /// </summary>
    /// <param name="dir"></param>
    /// <returns></returns>
    static bool IsIgnoreDir(string dir) {
        if (IgnoreDirs.Contains(dir)) {
            return true;
        }
        foreach (var x in IgnoreDirsPvt) {
            if (dir.Contains(x, StringComparison.InvariantCultureIgnoreCase)) {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// それ対象？
    /// </summary>
    /// <param name="dir"></param>
    /// <returns></returns>
    static bool IsAcceptDir(string dir) {
        foreach (var x in AcceptDirs) {
            if (dir.Contains(x, StringComparison.InvariantCultureIgnoreCase)) {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// ﾒﾀﾃﾞーﾀﾑを集める
    /// </summary>
    /// <param name="path"></param>
    /// <param name="datum"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public static bool TryAnalyze(string path,
        [NotNullWhen(true)] out ImageMetadataDatum? datum,
        [NotNullWhen(false)] out Exception? error) =>
        TryAnalyze(FileSourceRegistry.Instance, path, out datum, out error);

    /// <summary>
    /// 供給元からﾒﾀﾃﾞーﾀﾑを集める
    /// </summary>
    /// <param name="sources"></param>
    /// <param name="location"></param>
    /// <param name="datum"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public static bool TryAnalyze(FileSourceRegistry sources, string location,
        [NotNullWhen(true)] out ImageMetadataDatum? datum,
        [NotNullWhen(false)] out Exception? error) {
        Stream stream;
        try {
            stream = sources.OpenRead(location);
        }
        catch (Exception ex) {
            Logger.Dictator.Error(ex, $"ImageMetadataDatum: {location}");
            datum = null;
            error = ex;
            return false;
        }
        using (stream) {
            return TryAnalyze(stream, location, out datum, out error);
        }
    }

    /// <summary>
    /// ｽﾄﾘーﾑからﾒﾀﾃﾞーﾀﾑを集める
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="name">ﾛｸﾞ用の名前</param>
    /// <param name="datum"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public static bool TryAnalyze(Stream stream, string name,
        [NotNullWhen(true)] out ImageMetadataDatum? datum,
        [NotNullWhen(false)] out Exception? error) {
        datum = null;
        error = null;
        try {
            var directories = ImageMetadataReader.ReadMetadata(stream);
            if (directories == null) {
                throw new InvalidDataException(Properties.Resources.IM_E_ReadMetadata);
            }
            datum = Collect(directories);
            return true;
        }
        catch (Exception ex) {
            Logger.Dictator.Error(ex, $"ImageMetadataDatum: {name}");
            datum = null;
            error = ex;
            return false;
        }
    }

    /// <summary>
    /// ﾃﾞｨﾚｸﾄﾘーからﾒﾀﾃﾞーﾀﾑを組み立てる
    /// </summary>
    /// <param name="directories"></param>
    /// <returns></returns>
    static ImageMetadataDatum Collect(IEnumerable<MetadataExtractor.Directory> directories) {
        var ret = new ImageMetadataDatum();
        foreach (var directory in directories) {
            if (IsIgnoreDir(directory.Name)) {
                continue;
            }
            if (!IsAcceptDir(directory.Name)) {
                continue;
            }
            if (directory.Name.Contains("Thumbnail", StringComparison.InvariantCultureIgnoreCase)) {
                continue;
            }
            if (directory is XmpDirectory xmp) {
                // XMPは別途処理
                ret.Tags.AddRange(ProcessXMP(xmp));
                continue;
            }

            var dkey = directory.Name.Split(' ').Last();
            foreach (var tag in directory.Tags) {
                var obz = directory.GetObject(tag.Type);
                if (obz == null) {
                    continue;
                }
                string? cvts = TagAsString(directory, tag);
                var vv = (cvts != null ? cvts : tag.Description?.Replace("\0", ""))?.Trim();
                if (string.IsNullOrEmpty(vv)) {
                    continue;
                }
                if (vv.Length > 2048) {
                    continue;
                }
                var tn = tag.Name;
                var tv = vv.Trim();
                if (dkey.StartsWith("PNG-") && tn == "Textual Data") {
                    // ｷーﾜーﾄﾞあったらそっち使う
                    var dso = tv.IndexOf(':');
                    if (dso != -1 && dso < 80) {
                        // 割る
                        tn = tv.Substring(0, dso);
                        tv = tv.Substring(dso+1);
                    }
                }

                ret.Tags.Add(new ImageMetadataValue {
                    Type = obz.GetType().ToString(),
                    Directory = directory.Name,
                    Tag = tn.Trim(),
                    NumericValue = AsNumericValue(obz),
                    Value = tv.Trim() });

            }
        }
        ret.Opportunism();
        return ret;
    }

    /// <summary>
    /// 使い勝手の悪いXMPの名前空間
    /// </summary>
    static readonly HashSet<string> IgnoreXmpPrefixes = new(StringComparer.Ordinal) {
        "xmpMM", "stEvt", "stRef", "crs", "xmpTPg", "xmpG", "xmpGImg", "stDim", "stFnt"
    };

    /// <summary>
    /// 個別に無視するXMPﾌﾟﾛﾊﾟﾃｨ
    /// </summary>
    static readonly HashSet<string> IgnoreXmpProperties = new(StringComparer.Ordinal) {
        // これやばい、3000ﾚｺーﾄﾞ位入ってる画像ある
        "photoshop:DocumentAncestors",
        // 埋め込みｻﾑﾈｲﾙのBase64
        "xmp:Thumbnails",
    };

    /// <summary>
    /// Exifと同じ名前に読み替えるXMPﾌﾟﾛﾊﾟﾃｨ
    /// </summary>
    static readonly Dictionary<string, string> XmpTagNames = new(StringComparer.Ordinal) {
        ["tiff:Make"] = "Make",
        ["tiff:Model"] = "Model",
        ["aux:Lens"] = "Lens Model",
        ["exifEX:LensModel"] = "Lens Model",
        ["xmp:CreatorTool"] = "Software",
        ["tiff:Software"] = "Software",
        ["exif:DateTimeOriginal"] = "Date/Time Original",
        ["xmp:CreateDate"] = "Date/Time",
        ["xmp:ModifyDate"] = "Date/Time",
        ["exif:ISOSpeedRatings"] = "ISO Speed Ratings",
        ["exifEX:PhotographicSensitivity"] = "ISO Speed Ratings",
        ["exif:ExposureTime"] = "Exposure Time",
        ["exif:FNumber"] = "F-Number",
        ["exif:FocalLength"] = "Focal Length",
    };

    // 配列の添字 "[1]" 部分
    [GeneratedRegex(@"\[\d+\]")]
    private static partial Regex XmpArrayIndex();

    /// <summary>
    /// XMPの1要素
    /// </summary>
    /// <param name="Value">値</param>
    sealed record XmpItem(string Value) {
        /// <summary>
        /// xml:lang
        /// </summary>
        public string? Language { get; set; }
    }

    /// <summary>
    /// XMP取り出し
    /// </summary>
    /// <param name="directory"></param>
    /// <returns></returns>
    internal static IEnumerable<ImageMetadataValue> ProcessXMP(XmpDirectory directory) {
        var meta = directory.XmpMeta;
        if (meta == null) {
            yield break;
        }

        var groups = new List<(string Key, List<XmpItem> Items)>();
        var groupIndex = new Dictionary<string, List<XmpItem>>(StringComparer.Ordinal);
        var eitem = new Dictionary<string, XmpItem>(StringComparer.Ordinal);

        foreach (var p in meta.Properties) {
            var path = p.Path;
            if (string.IsNullOrEmpty(path) || p.Options.IsSchemaNode) {
                continue;
            }
            if (IsIgnoreXmpPath(path)) {
                continue;
            }
            if (p.Options.IsQualifier) {
                // dc:title[1]/xml:langの形で来るのでxml:langだけ拾って付け替え
                var owner = path.LastIndexOf('/');
                if (owner != -1 &&
                    path.EndsWith("/xml:lang", StringComparison.Ordinal) &&
                    eitem.TryGetValue(path.Substring(0, owner), out var ownerItem)) {
                    ownerItem.Language = p.Value;
                }
                continue;
            }
            if (p.Options.IsCompositeProperty) {
                // 配列や構造体の親ﾉーﾄﾞ
                continue;
            }
            var value = p.Value?.Replace("\0", "").Trim();
            if (string.IsNullOrEmpty(value)) {
                continue;
            }

            var item = new XmpItem(value) { Language = p.Language };
            eitem[path] = item;
            var key = XmpArrayIndex().Replace(path, "");
            if (!groupIndex.TryGetValue(key, out var items)) {
                items = [];
                groupIndex[key] = items;
                groups.Add((key, items));
            }
            items.Add(item);
        }

        foreach (var (key, items) in groups) {
            var value = JoinXmpItems(items);
            if (value.Length > 2048) {
                continue;
            }
            var numeric = ParseXmpNumber(value);
            yield return new ImageMetadataValue {
                Type = numeric != null ? typeof(double).ToString() : typeof(string).ToString(),
                Directory = directory.Name,
                Tag = XmpTagNames.TryGetValue(key, out var alias) ? alias : key,
                NumericValue = numeric,
                Value = value };
        }
    }

    /// <summary>
    /// それ無視するXMPﾌﾟﾛﾊﾟﾃｨ？
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    static bool IsIgnoreXmpPath(string path) {
        var top = path;
        var slash = top.IndexOf('/');
        if (slash != -1) {
            top = top.Substring(0, slash);
        }
        var bracket = top.IndexOf('[');
        if (bracket != -1) {
            top = top.Substring(0, bracket);
        }
        if (IgnoreXmpProperties.Contains(top)) {
            return true;
        }
        // 構造体の中身も含めて名前空間を見る
        foreach (var segment in path.Split('/')) {
            var colon = segment.IndexOf(':');
            if (colon <= 0) {
                continue;
            }
            if (IgnoreXmpPrefixes.Contains(segment.Substring(0, colon))) {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// XMP配列の要素をﾏーｼﾞ
    /// </summary>
    /// <param name="items"></param>
    /// <returns></returns>
    static string JoinXmpItems(List<XmpItem> items) {
        if (items.Count == 1) {
            return items[0].Value;
        }
        if (items.Any(x => x.Language != null)) {
            // Alt-Textはx-defaultを優先、無ければ先頭
            var preferred = items.FirstOrDefault(x => string.Equals(x.Language, "x-default", StringComparison.OrdinalIgnoreCase));
            return (preferred ?? items[0]).Value;
        }
        return string.Join(", ", items.Select(x => x.Value));
    }

    /// <summary>
    /// XMPの数値から有理数を取り出し
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    internal static double? ParseXmpNumber(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        var sp = value.IndexOf('/');
        if (sp != -1) {
            if (long.TryParse(value.AsSpan(0, sp), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numerator) &&
                long.TryParse(value.AsSpan(sp + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var denominator) &&
                denominator != 0) {
                return AsNumericValue(new Rational(numerator, denominator));
            }
            return null;
        }
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)) {
            return number;
        }
        return null;
    }

    /// <summary>
    /// 数値か有理数を取り出し
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    internal static double? AsNumericValue(object value) {
        var number = value switch {
            Rational rational when rational.Denominator != 0 => (double)rational.Numerator / rational.Denominator,
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
                => Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
            _ => double.NaN,
        };
        return double.IsFinite(number) ? number : null;
    }

    /// <summary>
    /// あたいを文字列に
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="tag"></param>
    /// <returns></returns>
    static string? TagAsString(MetadataExtractor.Directory directory, MetadataExtractor.Tag tag) {
        var obz = directory.GetObject(tag.Type);
        if (obz == null) {
            return null;
        }
        var sobj = obz.GetType() ?? typeof(object);
        if (IsNumericTypeWithInterface(sobj)) {
            return obz.ToString();
        }
        if (sobj.IsArray) {
            var etr = sobj.GetElementType();
            if (etr == null) {
                return null;
            }
            if (IsNumericTypeWithInterface(etr)) {
                if (obz is IEnumerable enumerable){
                    return string.Join(", ", enumerable.Cast<object>());
                }
            }
            return null;
        }

        string? cvts = null;
        var objkt = directory.GetByteArray(tag.Type);
        if (objkt != null) {
            try {
                // なんか、ｸｿ古いjpegにSJISとかEUCの文字が入ってる時がある
                var ck = ReadJEnc.JP.GetEncoding(objkt, objkt.Length, out cvts);
            }
            catch (Exception) {
                return null;
            }
        }
        return cvts;

    }

    /// <summary>
    /// それ数値？
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    static bool IsNumericTypeWithInterface(Type type) {
        if (type == typeof(Rational)) {
            return true;
        }
        return type.GetInterfaces()
                   .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(System.Numerics.INumber<>));
    }

}
