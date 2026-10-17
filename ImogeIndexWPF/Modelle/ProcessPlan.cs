namespace ImogeIndexWPF.Modelle;

/// <summary>
/// なにする？
/// </summary>
[Flags]
internal enum SelectiveProcess
{
    Index =      1 << 0,
    TagIndex =   1 << 1,
    Captioning = 1 << 2,
    Vacuum =     1 << 3,
}

/// <summary>
/// VACUUMのやり方
/// </summary>
internal enum VacuumMode
{
    /// <summary>
    /// しない
    /// </summary>
    None,
    /// <summary>
    /// 通常
    /// </summary>
    Quick,
    /// <summary>
    /// 完全
    /// </summary>
    Full,
}

/// <summary>
/// ﾌﾟﾛｾｯｻーの実行計画
/// </summary>
/// <param name="Index">ｲﾝﾃﾞｯｸｽ生成する</param>
/// <param name="TagIndex">ｲﾝﾃﾞｯｸｽの変更有無に関わらず検索ｲﾝﾃﾞｯｸｽ生成する</param>
/// <param name="Captioning">ｷｬﾌﾟｼｮﾆﾝｸﾞする</param>
/// <param name="Vacuum">vacuumもーど</param>
/// <param name="NotifyAlways">ｲﾝﾃﾞｯｸｽの変更有無に関わらずｲﾓｹﾞｼﾝｸﾞへ通知する</param>
internal sealed record ProcessPlan(
    bool Index,
    bool TagIndex,
    bool Captioning,
    VacuumMode Vacuum,
    bool NotifyAlways)
{
    /// <summary>
    /// 全部やる
    /// </summary>
    public static ProcessPlan Full { get; } = new(
        Index: true,
        TagIndex: true,
        Captioning: true,
        Vacuum: VacuumMode.Quick,
        NotifyAlways: false);

    /// <summary>
    /// 選択実行の計画を作る
    /// </summary>
    /// <param name="selective"></param>
    /// <returns></returns>
    public static ProcessPlan FromSelective(SelectiveProcess selective)
    {
        var tagIndex = selective.HasFlag(SelectiveProcess.TagIndex);
        return new(
            // ｷｬﾌﾟｼｮﾆﾝｸﾞ対象を集めるためにｲﾝﾃﾞｯｸｽ生成が要る
            Index: selective.HasFlag(SelectiveProcess.Index) || selective.HasFlag(SelectiveProcess.Captioning),
            TagIndex: tagIndex,
            Captioning: selective.HasFlag(SelectiveProcess.Captioning),
            Vacuum: selective.HasFlag(SelectiveProcess.Vacuum) ? VacuumMode.Full : VacuumMode.None,
            // ﾀｸﾞ更新は無条件でｲﾓｹﾞｼﾝｸﾞへ通知
            NotifyAlways: tagIndex);
    }
}
