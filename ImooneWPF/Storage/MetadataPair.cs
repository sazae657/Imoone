namespace ImooneWPF.Storage;

/// <summary>
/// ﾒﾀﾒﾀ
/// </summary>
public class MetadataPair {
    public string? Name { get; set; }
    public string? Value { get; set; }

    public MetadataPair() { }
    public MetadataPair(string name, string val) {
        Name = name; Value = val;
    }
}
