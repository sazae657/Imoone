using System.IO;

namespace Bsv2Resx;

/// <summary>
/// bsv→resx
/// </summary>
internal class ResxGenerator
{
    FileInfo resxPath;
    FileInfo bsvPath;

    bool forceGenerate = false;

    public ResxGenerator(FileInfo resx, FileInfo bsv, bool force)
    {
        resxPath = resx;
        bsvPath = bsv;
        forceGenerate = force;
    }

    public void Process() {
        if (!resxPath.Exists) {
            throw new FileNotFoundException($"resxが無い => {resxPath.FullName}", resxPath.FullName);
        }
        var resx = new ResxEditorize(resxPath);
        var strings = LoadText(bsvPath);
        if (Compare(strings, resx.Resources) && !forceGenerate) {
            Console.WriteLine($"変更差分無し => {bsvPath}");
            return; // 変更差分なし
        }
        Console.WriteLine($"resx 書き込み {bsvPath} => {resxPath}");
        resx.Write(strings);
    }

    Dictionary<string, string> LoadText(FileInfo path) {
        var bs = new ImogeIndex.Common.BSVReader<ImogeIndex.Common.BSVPair>();
        bs.Load(path);
        var rt = new Dictionary<string, string>();
        foreach (var item in bs.Values) {
            if (string.IsNullOrEmpty(item.Key)) {
                continue;
            }
            if (item.Value == null) {
                continue;
            }
            rt.Add(item.Key, item.Value);
        }
        return rt;
    }

    bool Compare(Dictionary<string, string> x1, Dictionary<string, string> x2)
    {
        if (x1.Count != x2.Count) {
            return false;
        }
        foreach (var x in x1) {
            if (!x2.TryGetValue(x.Key, out var val)){
                return false;
            }
            if (x.Value != val) {
                return false;
            }
        }
        return true;
    }

}
