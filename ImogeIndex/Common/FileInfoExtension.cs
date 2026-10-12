using System.Text;

namespace ImogeIndex.Common;

/// <summary>
/// ﾌｧｲﾙ関連拡張
/// </summary>
public static class FileInfoExtension {
    public delegate IEnumerable<string> WriteStringDelegaty();
    public delegate string WriteSingleStringDelegaty();


    /// <summary>
    /// こんばいん
    /// </summary>
    /// <param name="path"></param>
    /// <param name="elem"></param>
    /// <returns></returns>
    public static string CombinePath(this string path, params string[] elem)
    {
        if (elem.Length == 1) {
            return Path.Combine(path, elem[0]);
        }

        var nar = new List<string> { path };
        nar.AddRange(elem);
        return Path.Combine(nar.ToArray());
    }

    /// <summary>
    /// こんばいん
    /// </summary>
    /// <param name="path"></param>
    /// <param name="elem"></param>
    /// <returns></returns>
    public static DirectoryInfo CombineDir(this DirectoryInfo path, params string[] elem) => path.FullName.CombinePath(elem).AsDirectoryInfo();

    /// <summary>
    /// こんばいん
    /// </summary>
    /// <param name="path"></param>
    /// <param name="elem"></param>
    /// <returns></returns>
    public static FileInfo CombineFile(this DirectoryInfo path, params string[] elem) => path.FullName.CombinePath(elem).AsFileInfo();


    /// <summary>
    /// FIに
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static FileInfo AsFileInfo(this string path) => new FileInfo(path);

    /// <summary>
    /// FIに
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string GetNameWithoutExtension(this FileInfo path) => Path.GetFileNameWithoutExtension(path.FullName);


    /// <summary>
    /// DIに
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static DirectoryInfo AsDirectoryInfo(this string path) => new DirectoryInfo(path);

    /// <summary>
    /// .DS_*Store候補作成
    /// </summary>
    static string DS_Store() => $".DS_{Guid.NewGuid():N}.Store";

    /// <summary>
    /// .DS_*Store付き書き込み
    /// </summary>
    /// <param name="file"></param>
    /// <param name="write"></param>
    public static void WriteAndStore(this FileInfo file, Action<Stream> write)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(write);
        var ds_store = Path.Combine(file.DirectoryName!, DS_Store());
        var stream = new FileStream(ds_store, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try {
            using (stream) {
                write(stream);
            }
            File.Move(ds_store, file.FullName, true);
        }
        finally {
            DeleteStore(ds_store);
        }
    }


    /// <summary>
    /// .DS_*Store付き書き込み(非同期)
    /// </summary>
    /// <param name="file"></param>
    /// <param name="write"></param>
    /// <returns></returns>
    public static async Task WriteAndStoreAsync(this FileInfo file, Func<Stream, Task> write)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(write);
        var ds_store = Path.Combine(file.DirectoryName!, DS_Store());
        var stream = new FileStream(ds_store, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 4096, FileOptions.Asynchronous);
        try {
            await using (stream.ConfigureAwait(false)) {
                await write(stream).ConfigureAwait(false);
            }
            File.Move(ds_store, file.FullName, true);
        }
        finally {
            DeleteStore(ds_store);
        }
    }

    /// <summary>
    /// .DS_*Store削除削除
    /// </summary>
    /// <param name="path"></param>
    static void DeleteStore(string path) {
        try {
            File.Delete(path);
        }
        catch (IOException) {
            // 後片付け失敗
        }
        catch (UnauthorizedAccessException) {
            // 削除できないので手に負えぬ
        }
    }


    /// <summary>
    /// ﾃｷｽﾄ書き込み
    /// </summary>
    /// <param name="text"></param>
    /// <param name="str"></param>
    public static void OpenAndWriteText(this FileInfo text, IEnumerable<string> str)
    {
        text.WriteAndStore(fs => {
            using var sw = new StreamWriter(fs, new UTF8Encoding(false), leaveOpen: true);
            sw.NewLine = "\r\n";
            foreach (var s in str) {
                sw.WriteLine(s);
            }
        });
    }


    /// <summary>
    /// ﾃｷｽﾄ書き込み
    /// </summary>
    /// <param name="text"></param>
    /// <param name="str"></param>
    public static void OpenAndWriteText(this FileInfo text, string str)
    {
        text.OpenAndWriteText(new[] { str });
    }


    /// <summary>
    /// ﾃｷｽﾄ書き込みﾃﾞﾘｹﾞーﾃｨー
    /// </summary>
    /// <param name="text"></param>
    /// <param name="delegaty"></param>
    public static void OpenAndWriteText(this FileInfo text, WriteStringDelegaty delegaty)
    {
        text.OpenAndWriteText(delegaty());
    }
}
