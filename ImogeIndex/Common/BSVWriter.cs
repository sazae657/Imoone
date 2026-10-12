using System.Text;

namespace ImogeIndex.Common;

/// <summary>
/// BSV書き込み
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class BSVWriter<T> where T : IBSVContainer, new() {
    public BSVWriter() { }

    public BSVWriter(IEnumerable<T> values) {
        Values = [.. values];
    }

    public List<T> Values { get; } = new();

    /// <summary>
    /// 保存
    /// </summary>
    /// <param name="path"></param>
    public void Save(FileInfo path) {
        path.WriteAndStore(Save);
    }

    /// <summary>
    /// 保存
    /// </summary>
    public void Save(Stream stream)
    {
        foreach (var x in Values) {
            var dso = $"{x.ToBSV()}\r\n";
            stream.Write(new UTF8Encoding(false, true).GetBytes(dso));
        }
    }
}
