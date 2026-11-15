namespace ImogeIndex.Common;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ZundaHorizonAttribute : Attribute {
    internal string Horizon { get; }

    public ZundaHorizonAttribute(string horizon) {
        Horizon = horizon;
    }
}
