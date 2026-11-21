using ImogeIndex.Captionizers;
using System.Text;
using Tomlet;
using Tomlet.Models;

namespace ImogeIndex.Common;

/// <summary>
/// TOML関連もろもろ
/// </summary>
public static class TomlHelper
{
    /// <summary>
    /// 書き込み
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="path"></param>
    /// <param name="obzekt"></param>
    public static void Write<T>(FileInfo path, T obzekt) where T : class
    {
        var t = TomletMain.TomlStringFrom(obzekt, TomlSerializerOptions.Default);
        path.OpenAndWriteText(t);
    }

    /// <summary>
    /// 文字列に
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obzekt"></param>
    public static string AsToml<T>(T obzekt) where T : class {
        return TomletMain.TomlStringFrom(obzekt, TomlSerializerOptions.Default);
    }

    /// <summary>
    /// 書き込み
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="path"></param>
    /// <param name="obzekt"></param>
    public static Task WriteAsync<T>(FileInfo path, T obzekt) where T : class {
        var t = AsToml(obzekt);
        return path.WriteAndStoreAsync(fs =>
            fs.WriteAsync(Encoding.UTF8.GetBytes(t)).AsTask());
    }

    /// <summary>
    /// 読み込み
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="path"></param>
    /// <returns></returns>
    public static T Load<T>(FileInfo path) where T : class
    {
        var t = TomlParser.ParseFile(path.FullName);
        return TomletMain.To<T>(t);
    }

    /// <summary>
    /// 読み込み
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="datum"></param>
    /// <returns></returns>
    public static T Parse<T>(string datum) where T : class {
        TomlParser parser = new TomlParser();
        TomlDocument document = parser.Parse(datum);
        return TomletMain.To<T>(document);
    }
}
