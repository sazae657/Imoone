using ImogeIndex.Mur;

namespace ImogeIndex.Web6;

/// <summary>
/// 詳細ﾍﾟｲﾝの見出し
/// </summary>
public enum DetailFieldLabel
{
    FileName,
    CreatedAt,
    Location,
    FileSize,
    Resolution,
}

/// <summary>
/// 共通ﾛｼﾞｯｸが使う表示文言
/// </summary>
public interface IImoonePalabras
{
    /// <summary>
    /// 件数表示
    /// </summary>
    /// <param name="count"></param>
    /// <returns></returns>
    string ItemCount(int count);

    /// <summary>
    /// 類似検索中の見出し
    /// </summary>
    /// <param name="name">基準画像の名前</param>
    /// <param name="limit">最大件数</param>
    /// <returns></returns>
    string SimilarSearchActive(string name, int limit);

    /// <summary>
    /// 類似検索の実行中
    /// </summary>
    string SimilarSearching { get; }

    /// <summary>
    /// 類似検索中の検索ﾎﾞｯｸｽのﾌﾟﾚーｽﾎﾙﾀﾞー
    /// </summary>
    string SimilarSearchHint { get; }

    /// <summary>
    /// 設定ﾌｫﾙﾀﾞーを開くﾒﾆｭーの見出し
    /// </summary>
    /// <param name="profile">ﾌﾟﾛﾌｧｲﾙ名(未確定ならnull)</param>
    /// <returns></returns>
    string OpenSettingsFolder(string? profile);

    /// <summary>
    /// ﾃﾞーﾀﾑ読み込み段階
    /// </summary>
    /// <param name="phase"></param>
    /// <returns></returns>
    string LoadPhase(DatumLoadPhase phase);

    /// <summary>
    /// 詳細ﾍﾟｲﾝの見出し
    /// </summary>
    /// <param name="label"></param>
    /// <returns></returns>
    string DetailLabel(DetailFieldLabel label);
}
