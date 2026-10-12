using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ImogeIndexWPF.Tools.Modelle;

/// <summary>
/// 正規表現練習帳
/// </summary>
public class Kabaddiing {
    public string? Group { get; set; }
    public int Index { get; set; }
    public string? Value { get; set; }

    public Kabaddiing() { }
    public Kabaddiing(string group, int index, string val) {
        Group = group; Index = index; Value = val;
    }
}

/// <summary>
/// 正規表現練習帳のﾓﾃﾞﾚ
/// </summary>
internal sealed partial class RegexSpielplatzModelle : ObservableObject {

    [ObservableProperty]
    public partial string Regex { get; set; }

    partial void OnRegexChanged(string value) {
        OnProcessRegex?.Invoke(value);
    }

    [ObservableProperty]
    public partial string TestString { get; set; }
    partial void OnTestStringChanged(string value) {
        OnEvaluate?.Invoke(value);
    }

    [ObservableProperty]
    public partial bool IgnoreCase { get; set; } = true;
    partial void OnIgnoreCaseChanged(bool value) {
        OnProcessRegex?.Invoke(Regex);
    }


    public ObservableCollection<Kabaddiing> Results { get; set; } = new();

    public event Action<string>? OnProcessRegex;
    public event Action<string>? OnEvaluate;
}
