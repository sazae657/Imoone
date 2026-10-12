namespace ImogeIndex.Storage;


/// <summary>
/// mur
/// </summary>
[Flags]
public enum MurColumnFlags {
    None = 0,
    NotNull = 1 << 0,
    Unique = 1 << 1,
    PrimaryKey = 1 << 2,
}

/// <summary>
/// mur
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class MurColumnAttribute : Attribute {
    /// <summary>
    /// 名前
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 追加されたDBﾊﾞーｼﾞｮﾝ
    /// </summary>
    public int HatchedAt { get; set; } = 1;

    /// <summary>
    /// SQLで表した既定値
    /// </summary>
    public string? DefaultSql { get; set; }

    /// <summary>
    /// ｽｷーﾏ生成時のﾌﾗｸﾞ
    /// </summary>
    public MurColumnFlags Flags { get; set; } = MurColumnFlags.None;

    public MurColumnAttribute(string name) {
        Name = name;
    }
    public MurColumnAttribute(string name, int ver) {
        Name = name;
        HatchedAt = ver;
    }

    public MurColumnAttribute(string name, MurColumnFlags flags) {
        Name = name;
        Flags = flags;
    }

    public MurColumnAttribute(string name, int ver, MurColumnFlags flags) {
        Name = name;
        Flags = flags;
        HatchedAt = ver;
    }
}

/// <summary>
/// mur
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field)]
public class MurTableAttribute : Attribute {
    /// <summary>
    /// 名前
    /// </summary>
    public string Name { get; set; }

    public MurTableAttribute(string name) {
        Name = name;
    }
}
