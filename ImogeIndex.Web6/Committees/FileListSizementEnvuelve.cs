using ImogeIndex.Web6.Modelle;

namespace ImogeIndex.Web6.Committees;

/// <summary>
/// 一覧の拡縮(表示方式とｱｲｺﾝの寸法)
/// </summary>
/// <param name="Mode">表示方式</param>
/// <param name="IconSize">ｱｲｺﾝの寸法</param>
public readonly record struct FileListSizementEnvuelve(MurDisplayMode Mode, int IconSize)
{
    /// <summary>
    /// ｱｲｺﾝの最小寸法(小ｱｲｺﾝ)
    /// </summary>
    public const int MinIconSize = 16;

    /// <summary>
    /// ﾎｲーﾙ1段で変わる寸法
    /// </summary>
    public const int IconSizeStep = 16;

    /// <summary>
    /// ｱｲｺﾝの寸法の天井(ｻﾑﾈ読み込むﾏﾝの上限に合わせる)
    /// </summary>
    public const int IconSizeCeiling = 512;

    /// <summary>
    /// ｻﾑﾈが無効な時の最大寸法(特大ｱｲｺﾝと同じ)
    /// </summary>
    public const int DefaultMaxIconSize = 128;

    /// <summary>
    /// ﾎｲーﾙ1段分のDelta
    /// </summary>
    public const int WheelDelta = 120;

    /// <summary>
    /// 小ｱｲｺﾝより大きい時のﾀｲﾙの最小幅
    /// </summary>
    const int MinTileWidth = 128;

    /// <summary>
    /// 小ｱｲｺﾝのﾀｲﾙ寸法
    /// </summary>
    const int SmallTileWidth = 200;
    const int SmallTileHeight = 28;

    /// <summary>
    /// ｱｲｺﾝの左右の余白
    /// </summary>
    const int TileHorizontalPadding = 16;

    /// <summary>
    /// ｱｲｺﾝの下に置く名前の分の高さ
    /// </summary>
    const int TileLabelHeight = 32;

    /// <summary>
    /// ｱｲｺﾝ表示？
    /// </summary>
    public bool IsIcon => Mode == MurDisplayMode.Icon;

    /// <summary>
    /// ｱｲｺﾝの右に名前を出す？(小ｱｲｺﾝ)
    /// </summary>
    public bool Horizontal => IsIcon && IconSize <= MinIconSize;

    /// <summary>
    /// ﾀｲﾙの幅
    /// </summary>
    public double TileWidth => Horizontal ? SmallTileWidth : Math.Max(MinTileWidth, IconSize + TileHorizontalPadding);

    /// <summary>
    /// ﾀｲﾙの高さ
    /// </summary>
    public double TileHeight => Horizontal ? SmallTileHeight : IconSize + TileLabelHeight;

    /// <summary>
    /// ﾌﾟﾛﾌｧｲﾙから最大寸法を決める
    /// </summary>
    /// <param name="thumbnailEnabled">ｻﾑﾈ有効？</param>
    /// <param name="thumbnailSize">ｻﾑﾈの寸法</param>
    /// <returns></returns>
    public static int MaxIconSizeOf(bool thumbnailEnabled, int thumbnailSize)
        => thumbnailEnabled ? Math.Clamp(thumbnailSize, MinIconSize, IconSizeCeiling) : DefaultMaxIconSize;

    /// <summary>
    /// ﾌﾟﾘｾｯﾄの寸法
    /// </summary>
    /// <param name="preset"></param>
    /// <returns>ｱｲｺﾝ以外はnull</returns>
    public static int? PresetIconSize(MurDisplayPreset preset) => preset switch {
        MurDisplayPreset.IconS => 16,
        MurDisplayPreset.IconM => 32,
        MurDisplayPreset.IconL => 64,
        MurDisplayPreset.IconH => 128,
        _ => null,
    };

    /// <summary>
    /// ﾌﾟﾘｾｯﾄから作る(寸法は最大で切る)
    /// </summary>
    /// <param name="preset"></param>
    /// <param name="maxIconSize">最大寸法</param>
    /// <returns></returns>
    public static FileListSizementEnvuelve FromPreset(MurDisplayPreset preset, int maxIconSize) => preset switch {
        MurDisplayPreset.List => new(MurDisplayMode.List, MinIconSize),
        MurDisplayPreset.ThumbList => new(MurDisplayMode.ThumbList, MinIconSize),
        _ => new FileListSizementEnvuelve(MurDisplayMode.Icon, PresetIconSize(preset) ?? MinIconSize).Clamp(maxIconSize),
    };

    /// <summary>
    /// 現在の状態に当てはまるﾌﾟﾘｾｯﾄ
    /// 最大で切られて複数が同じ寸法になった時は小さい方を返す
    /// </summary>
    /// <param name="maxIconSize">最大寸法</param>
    /// <returns>どれにも当てはまらなければnull</returns>
    public MurDisplayPreset? MatchPreset(int maxIconSize)
    {
        foreach (var preset in Enum.GetValues<MurDisplayPreset>()) {
            if (FromPreset(preset, maxIconSize) == this) {
                return preset;
            }
        }
        return null;
    }

    /// <summary>
    /// 寸法を範囲に収める(ｱｲｺﾝ以外は寸法を最小に揃える)
    /// </summary>
    /// <param name="maxIconSize">最大寸法</param>
    /// <returns></returns>
    public FileListSizementEnvuelve Clamp(int maxIconSize)
    {
        if (!Enum.IsDefined(Mode)) {
            return new(MurDisplayMode.List, MinIconSize);
        }
        if (!IsIcon) {
            return this with { IconSize = MinIconSize };
        }
        return this with { IconSize = Math.Clamp(IconSize, MinIconSize, Math.Max(MinIconSize, maxIconSize)) };
    }

    /// <summary>
    /// 拡大縮小
    /// </summary>
    /// <param name="steps">正で拡大、負で縮小</param>
    /// <param name="maxIconSize">最大寸法</param>
    /// <returns></returns>
    public FileListSizementEnvuelve Step(int steps, int maxIconSize)
    {
        var current = Clamp(maxIconSize);
        for (var i = 0; i < Math.Abs(steps); i++) {
            current = steps > 0 ? current.Larger(maxIconSize) : current.Smaller();
        }
        return current;
    }

    /// <summary>
    /// 1段拡大
    /// </summary>
    /// <param name="maxIconSize"></param>
    /// <returns></returns>
    private FileListSizementEnvuelve Larger(int maxIconSize) => Mode switch {
        MurDisplayMode.List => new(MurDisplayMode.ThumbList, MinIconSize),
        MurDisplayMode.ThumbList => new(MurDisplayMode.Icon, MinIconSize),
        // 最大が刻みに乗っていない時は最後の1段だけ端数になる
        _ => new FileListSizementEnvuelve(Mode, (IconSize / IconSizeStep + 1) * IconSizeStep).Clamp(maxIconSize),
    };

    /// <summary>
    /// 1段縮小(ﾘｽﾄより先は無い)
    /// </summary>
    /// <returns></returns>
    private FileListSizementEnvuelve Smaller()
    {
        if (Mode == MurDisplayMode.List) {
            return this;
        }
        if (Mode == MurDisplayMode.ThumbList) {
            return new(MurDisplayMode.List, MinIconSize);
        }
        if (IconSize <= MinIconSize) {
            return new(MurDisplayMode.ThumbList, MinIconSize);
        }
        // 端数からは刻みに戻す
        return this with { IconSize = Math.Max(MinIconSize, (IconSize - 1) / IconSizeStep * IconSizeStep) };
    }
}

/// <summary>
/// ﾎｲーﾙのDeltaを段数に直す
/// </summary>
public sealed class WheelNotchAccumulator
{
    int pending;

    /// <summary>
    /// Deltaを足して、溜まった段数を返す
    /// </summary>
    /// <param name="delta">正で奥(拡大)</param>
    /// <returns></returns>
    public int Add(int delta)
    {
        // 向きが変わったら溜めていた分は捨てる
        if (pending != 0 && Math.Sign(pending) != Math.Sign(delta)) {
            pending = 0;
        }
        pending += delta;
        var steps = pending / FileListSizementEnvuelve.WheelDelta;
        pending -= steps * FileListSizementEnvuelve.WheelDelta;
        return steps;
    }

    /// <summary>
    /// 溜めていた分を捨てる
    /// </summary>
    public void Reset() => pending = 0;
}
