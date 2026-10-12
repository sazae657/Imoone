using ImogeIndex;
using ImogeIndex.Common;
using R = ImogeMerge.Properties.Resources;

namespace ImogeMerge;

/// <summary>
/// ﾌﾟﾛﾌｧｲﾙのﾏーｼﾞ一式
/// </summary>
public static class MergeDictator {
    const string IndexerMutexName = @"Local\ImogeIndexWPF.{F87F279A-6BE9-491A-B6F3-8BBB5231BFCC}.Web3";

    /// <summary>
    /// ﾏーｼﾞ実行
    /// </summary>
    /// <param name="century"></param>
    /// <param name="bongo"></param>
    /// <param name="output"></param>
    /// <param name="warnings"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static DatumMergeResult Merge(DirectoryInfo century, DirectoryInfo bongo, DirectoryInfo output, List<string> warnings) {
        var fa = Path.GetFullPath(century.FullName).TrimEnd('\\');
        var fb = Path.GetFullPath(bongo.FullName).TrimEnd('\\');
        var fo = Path.GetFullPath(output.FullName).TrimEnd('\\');
        if (fa.Equals(fb, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(R.PMG_E_SameProfile.Morfat(fa));
        }
        if (fo.Equals(fa, StringComparison.OrdinalIgnoreCase) || fo.Equals(fb, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(R.PMG_E_OutputIsSource.Morfat(fo));
        }
        output.Refresh();
        if (output.Exists && output.EnumerateFileSystemInfos().Any()) {
            throw new InvalidOperationException(R.PMG_E_OutputNotEmpty.Morfat(fo));
        }
        if (Mutex.TryOpenExisting(IndexerMutexName, out var running)) {
            running.Dispose();
            warnings.Add(R.PMG_W_Running);
        }

        var sa = SettingsMerger.Read(Config(century));
        var sb = SettingsMerger.Read(Config(bongo));
        var merged = SettingsMerger.Merge(sa, sb, warnings);

        output.Create();
        try {
            var rc = DatumMerger.Merge(century, bongo, output, warnings);
            SettingsMerger.Write(merged, output.CombineFile(SystemParams.ConfigName));
            return rc;
        }
        catch {
            // 中途半端な出力は残さない
            try {
                output.Delete(true);
            }
            catch (IOException) {
                // 消せなかったらまぁ仕方ない
            }
            throw;
        }
    }

    static FileInfo Config(DirectoryInfo dir) {
        var f = dir.CombineFile(SystemParams.ConfigName);
        if (!f.Exists) {
            throw new FileNotFoundException(R.PMG_E_DatumNotFound.Morfat(f.FullName));
        }
        return f;
    }
}
