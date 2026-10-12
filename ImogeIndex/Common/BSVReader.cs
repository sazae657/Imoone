using System.Text;

namespace ImogeIndex.Common;

/// <summary>
/// BSV読み込み
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class BSVReader<T> where T: IBSVContainer, new() {
    public BSVReader() {
    }

    /// <summary>
    /// あたい
    /// </summary>
    public List<T> Values { get; } = new();

    /// <summary>
    /// 読み込み
    /// </summary>
    /// <param name="path"></param>
    public void Load(FileInfo path)
    {
        using var fs = path.OpenRead();
        Load(fs);
    }

    /// <summary>
    /// 読み込み
    /// </summary>
    /// <param name="stream"></param>
    /// <exception cref="Exception"></exception>
    public void Load(Stream stream)
    {
        using var sr = new StreamReader(stream, new UTF8Encoding(true, true), false, leaveOpen:true);
        string? line = null;
        int lc = 0;
        while ((line = sr.ReadLine()) != null) {
            lc++;
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) {
                continue;
            }
            var vv = new T();
            if (!vv.TryParse(line)) {
                throw new Exception(Properties.Resources.CM_E_SyntaxError.Morfat(lc, line));
            }
            Values.Add(vv);
        }
    }
}
