using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImogeIndex;
using ImogeIndexWPF.Dictators;
using ImogeIndexWPF.Settingure.Dictators;
using Microsoft.ML.OnnxRuntime;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace ImogeIndexWPF.Settingure.Modelle.Pages;


/// <summary>
/// tagger設定
/// </summary>
internal sealed partial class TaggerPageModell : PageModell {

    private readonly IFileDialogDictator _fileDialogDictator;

    public TaggerPageModell(SettingureEditDictator s) : base(s) {
        Sanitizing();
        Enable = s.Draft.Tagger.Enable;
        Model = s.Draft.Tagger.Model;
        Modepile =s.Draft.Tagger.Modepile;
        Threshold = s.Draft.Tagger.Threshold;
        CharacterThreshold = s.Draft.Tagger.CharacterThreshold;
        Threads = s.Draft.Tagger.Threads;
        DevicePolicy = s.Draft.Tagger.DevicePolicy;

        _fileDialogDictator = new FileDialogDictator();
        Sanitized();
    }

    [RelayCommand]
    private void OpenModelFile()
    {
        var exm = session.Draft.ExpandingClass(Model);
        var filePath = _fileDialogDictator.OpenFileDialog(
            Properties.Resources.SW_FC_Model,
            Properties.Resources.SW_FC_ModelFilter,
            Path.GetDirectoryName(exm) ?? string.Empty);
        if (!string.IsNullOrEmpty(filePath)) {
            Model = filePath;
        }
    }


    [RelayCommand]
    private void OpenTagFile()
    {
        var ext = session.Draft.ExpandingClass(Tags);
        var filePath = _fileDialogDictator.OpenFileDialog(
            Properties.Resources.SW_FC_Tags,
            session.Draft.Tagger.Type switch {
                TaggerType.WD => Properties.Resources.SW_FC_TagCsvFilter,
                TaggerType.Camie => Properties.Resources.SW_FC_TagJsonFilter,
                _ => Properties.Resources.SW_FC_AllFile
            },
            Path.GetDirectoryName(ext) ?? string.Empty);

        if (!string.IsNullOrEmpty(filePath)) {
            Tags = filePath;
        }
    }

    [ObservableProperty]
    public partial bool Enable { get; set; }

    partial void OnEnableChanged(bool value) {
        session.Draft.Tagger.Enable = value;
        MarkDirty();
        ValidateTaggerFiles();
    }

    public override bool ValidateAll() {
        var isValid = base.ValidateAll();

        if (!Enable) {
            ClearErrors(nameof(Model));
            ClearErrors(nameof(Tags));
        }

        return Enable ? isValid : !HasErrors;
    }

    [ObservableProperty]
    public partial bool Modepile { get; set; }

    partial void OnModepileChanged(bool value) {
        session.Draft.Tagger.Modepile = value;
        MarkDirty();
    }


    public IEnumerable<TaggerType> TaggerTypes => Enum.GetValues<TaggerType>();

    public TaggerType TaggerType {
        get => session.Draft.Tagger.Type;
        set {
            if (session.Draft.Tagger.Type == value) {
                return;
            }
            session.Draft.Tagger.Type = value;
            MarkDirty();
            OnPropertyChanged();
        }
    }

    [Required]
    [ObservableProperty]
    public partial string Model { get; set; }
    partial void OnModelChanged(string value)
    {
        if (session.Draft.Tagger.Model != value) {
            MarkDirty();
        }
        session.Draft.Tagger.Model = value;
        ValidateTaggerFiles();
    }

    [Required]
    public string Tags {
        get => session.Draft.Tagger.Tags;
        set {
            if (session.Draft.Tagger.Tags != value) {
                session.Draft.Tagger.Tags = value;
                MarkDirty();
                // ﾀﾞｲｱﾛｸﾞからの設定も画面に反映させる
                OnPropertyChanged();
            }
            ValidateTaggerFiles();
        }
    }

    private void ValidateTaggerFiles() {
        if (Enable) {
            ValidateProperty(Model, nameof(Model));
            ValidateProperty(Tags, nameof(Tags));
            return;
        }

        ClearErrors(nameof(Model));
        ClearErrors(nameof(Tags));
    }

    public IEnumerable<ExecutionProviderDevicePolicy> DevicePolicyOf => Enum.GetValues<ExecutionProviderDevicePolicy>();

    public ExecutionProviderDevicePolicy DevicePolicy {
        get => session.Draft.Tagger.DevicePolicy;
        set {
            if (session.Draft.Tagger.DevicePolicy == value) {
                return;
            }
            session.Draft.Tagger.DevicePolicy = value;
            MarkDirty();
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0f, 1.0f)]
    public partial float Threshold { get; set; }

    partial void OnThresholdChanged(float value) {
        session.Draft.Tagger.Threshold = value;
        MarkDirty();
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0f, 1.0f)]
    public partial float CharacterThreshold { get; set; }
    partial void OnCharacterThresholdChanged(float value) {
        session.Draft.Tagger.CharacterThreshold = value;
        MarkDirty();
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [ThreadBegrenzung]
    public partial int Threads { get; set; }
    partial void OnThreadsChanged(int value) {
        session.Draft.Tagger.Threads = value;
        MarkDirty();
    }
}
