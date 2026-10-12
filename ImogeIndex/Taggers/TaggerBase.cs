using ImogeIndex.Common;
using ImogeIndex.Storage;
using ImogeIndex.Zenerators;
using Microsoft.ML.OnnxRuntime;
using System.Security.Cryptography;
using System.Security.Policy;

namespace ImogeIndex.Taggers;


/// <summary>
/// ﾀｸﾞ
/// </summary>
public class Tag {
    public int Index { get; set; }

    public long TagId { get; set; }
    public string? Name { get; set; }
    public int Category { get; set; }
    public long Count { get; set; }

    public float Score { get; set; }

    public Tag Copy(float score) {
        var r = new Tag
        {
            Index = Index,
            TagId = TagId,
            Name = Name,
            Category = Category,
            Count = Count,
            Score = score
        };
        return r;
    }
}

/// <summary>
/// 入力形式
/// </summary>
public class Shape
{
    public string? InputName { get; set; }
    public string? OutputName { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// ﾀｸﾞ付け結果
/// </summary>
public class TaggingResult {
    public List<Tag> Rating { get; } = new();
    public List<Tag> Character { get; } = new();
    public List<Tag> Tags { get; } = new();

    public string AsString() {
        var rate = string.Join(",", Rating.Select(w => w.Name));
        var chara = string.Join(",", Character.Select(w => w.Name));
        var tags = string.Join(",", Tags.Select(w => w.Name));

        return $"rat:{rate} chara:{chara} tags:{tags}";
    }
}

/// <summary>
/// Tagger
/// </summary>
public abstract class TaggerBase : IDisposable {
    PreparedSettings system;
    protected TaggerSetting setting;
    private bool disposedValue;

    protected TaggerBase(PreparedSettings setty) {
        system = setty;
        setting = system.Tagger;
    }

    public ProgressDelegaty? OnProgress;
    public MessageDelegaty? OnMessage;

    public int Threads { get; set; } = 1;
    public FileInfo? ModelPath { get; protected set; }
    public DirectoryInfo? CacheDir { get; protected set; }
    public ExecutionProviderDevicePolicy DevicePolicy { get; set; } = ExecutionProviderDevicePolicy.MAX_PERFORMANCE;

    protected SessionOptions? sessionOptions;
    protected InferenceSession? model;

    protected Shape shape = new();

    /// <summary>
    /// 入力形式
    /// </summary>
    public Shape Shape => shape;

    /// <summary>
    /// ﾀｸﾞの閾値
    /// </summary>
    public float Threshold { get; set; } = 0.35f;

    /// <summary>
    /// ｷｬﾗｸﾀーの閾値
    /// </summary>
    public float CharacterThreshold { get; set; } = 0.1f;

    /// <summary>
    /// 初期化
    /// </summary>
    /// <param name="cacheDir"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public virtual Task Sanitize(DirectoryInfo cacheDir, CancellationToken token) { throw new NotImplementedException(); }

    /// <summary>
    /// 推論
    /// </summary>
    /// <param name="img"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public virtual TaggingResult Predict(ImageTransaction img) { throw new NotImplementedException(); }


    /// <summary>
    /// 変数のｴｷﾊﾟﾝ
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    protected string ExpandingClass(string str) => system.Expand(str);
    

    /// <summary>
    /// ｷﾞｬー
    /// </summary>
    /// <param name="delegaty"></param>
    /// <returns></returns>
    protected  SessionOptions? TryConfigure(IEnumerable<Action<SessionOptions>> delegaty) {
        foreach (var d in delegaty) {
            var options = new SessionOptions();
            try {
                options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                d(options);
                return options;
            }
            catch {
                options?.Dispose();
            }
        }
        return null;
    }

    /// <summary>
    /// ﾓﾃﾞﾙ読み込みの標準実装
    /// </summary>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    protected void LoadModelDefault() {
        if (ModelPath == null) {
            throw new ArgumentNullException(nameof(ModelPath), Properties.Resources.TW_E_ModelPathIsNull);
        }
        var mudel = ModelPath;
        if (!mudel.Exists) {
            throw new FileNotFoundException(Properties.Resources.TW_E_ModelNotExists.Morfat(mudel));
        }

        try {
            OnMessage?.Invoke(Properties.Resources.TW_InitializingOrt);
            if (sessionOptions == null) {
                sessionOptions = TryConfigure([_ => { }]);
                if (sessionOptions == null) {
                    return;
                }
            }

            sessionOptions.SetEpSelectionPolicy(DevicePolicy);

            if (Threads > 1) {
                sessionOptions.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                sessionOptions.InterOpNumThreads = Threads;
                sessionOptions.IntraOpNumThreads = Threads;
            }

            var ccm = mudel;
            if (setting.Modepile) {
                ccm = CompileModel(mudel, false);
            }

            OnMessage?.Invoke(Properties.Resources.TW_LoadingModel.Morfat(ccm));
            model = TryLoadModel(ccm, !setting.Modepile);
            if (setting.Modepile && model == null) {
                // 再ｺﾝﾊﾟｲﾙしてﾄﾗｲ
                ccm = CompileModel(mudel, true);
                model = TryLoadModel(ccm, false);
                if (model == null) {
                    // ﾀﾞﾒなので素のﾓﾃﾞﾙでﾄﾗｲ
                    model = TryLoadModel(mudel, true);
                }
            }

        }
        catch (Exception) {
            model?.Dispose();
            model = null;
            throw;
        }
    }

    /// <summary>
    /// ﾓﾃﾞﾙ読み込み
    /// </summary>
    /// <param name="path"></param>
    /// <param name="throwIfFail"></param>
    /// <returns></returns>
    InferenceSession? TryLoadModel(FileInfo path, bool throwIfFail)
    {
        try {
            return new InferenceSession(path.FullName, sessionOptions);
        }
        catch {
            if (throwIfFail) {
                throw;
            }
        }
        return null;
    }

    /// <summary>
    /// ﾓﾃﾞﾊﾟｲﾙ
    /// </summary>
    /// <param name="baseModel"></param>
    /// <param name="recompile"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    FileInfo CompileModel(FileInfo baseModel, bool recompile)
    {
        var route = CacheDir!.CombineDir("CompiledModel")
            .CombineDir($"{baseModel.Directory!.Name}.d");
        var modelHash = CalcModelHash(baseModel);
        var compiledHash = LoadModelHash(route);
        if (string.IsNullOrEmpty(compiledHash)) {
            recompile = true;
        }
        else {
            // ﾊｯｼｭ一致しなかったらたぶん、ﾓﾃﾞﾙ変わってる
            if (compiledHash != modelHash) {
                recompile = true;
            }
        }

        if (recompile && route.Exists) {
            foreach (var x in route.GetFiles()) {
                try {
                    x.Delete();
                }catch(Exception r) {
                    Logger.Dictator.Error(r, $"{x} の削除にしっぱい");
                }
            }
            var vat = route.CombineFile($"{DevicePolicy}.onnx");
            if (vat.Exists) {
                throw new Exception(Properties.Resources.TW_E_DeleteFailed.Morfat(vat));
            }
        }
        var ccm = route.CombineFile($"{DevicePolicy}.onnx");
        if (!ccm.Exists) {
            OnMessage?.Invoke(Properties.Resources.TW_Modepiling.Morfat(baseModel.FullName));
            if (!ccm.Directory!.Exists) {
                ccm.Directory.Create();
            }
            using OrtModelCompilationOptions compileOptions = new(sessionOptions);
            compileOptions.SetInputModelPath(baseModel.FullName);
            compileOptions.SetOutputModelPath(ccm.FullName);
            compileOptions.CompileModel();
        }
        WriteModelHash(route, modelHash);
        return ccm;
    }

    /// <summary>
    /// 変更検知用ﾊｯｼｭ取得
    /// </summary>
    /// <param name="baseModel"></param>
    /// <returns></returns>
    string CalcModelHash(FileInfo baseModel) {
        using var fo = baseModel.OpenRead();
        var hash = SHA512.HashData(fo);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// ﾊｯｼｭ書き込み
    /// </summary>
    /// <param name="root"></param>
    /// <param name="hash"></param>
    void WriteModelHash(DirectoryInfo root, string hash)
    {
        var mo = root.CombineFile(".DS_Store");
        mo.OpenAndWriteText(hash);
    }

    /// <summary>
    /// ﾊｯｼｭ読み込み
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    string? LoadModelHash(DirectoryInfo root)
    {
        var mo = root.CombineFile(".DS_Store");
        if (!mo.Exists) {
            return null;
        }

        using var se = mo.OpenText();
        string? hash;
        while ((hash = se.ReadLine()?.Trim()) != null) {
            if (!string.IsNullOrEmpty(hash)) {
                break;
            }
        }
        return hash;
    }

    #region IDisposable
    protected virtual void Dispose(bool disposing) {
        if (!disposedValue) {
            disposedValue = true;
            model?.Dispose();
            model = null;
            sessionOptions?.Dispose();
            sessionOptions = null;
        }
    }

    public void Dispose() {
        // このｺーﾄﾞを変更しないでください。ｸﾘーﾝｱｯﾌﾟ ｺーﾄﾞを 'Dispose(bool disposing)' ﾒｿｯﾄﾞに記述します
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}

/// <summary>
/// Taggerﾎﾙﾀﾞー
/// </summary>
public class TaggerObiekt {
    static TaggerBase? instance;
    public static TaggerBase? Default => instance;

    public static ProgressDelegaty? OnProgress;
    public static MessageDelegaty? OnMessage;

    /// <summary>
    /// 設定確認
    /// </summary>
    /// <param name="stty"></param>
    /// <returns></returns>
    public static bool CheckConfiguration(PreparedSettings stty) {
        if (!stty.Tagger.Enable) {
            // 無効なのでok
            return true;
        }
        if (string.IsNullOrEmpty(stty.Tagger.Model) ||
            string.IsNullOrEmpty(stty.Tagger.Tags)) {
            return false;
        }
        return true; // 読み込み確認は実装に任せる
    }

    /// <summary>
    /// Tagger初期化
    /// </summary>
    /// <param name="stty"></param>
    /// <param name="cacheDir"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static async Task Sanitize(PreparedSettings stty, DirectoryInfo cacheDir, CancellationToken token) {
        if (instance != null) {
            return;
        }
        try {
            instance = stty.Tagger.Type switch
            {
                TaggerType.WD => new WDTagger(stty),
                TaggerType.Camie => new CamieTagger(stty),
                _ => throw new NotImplementedException()
            };
            instance.OnMessage += m => OnMessage?.Invoke(m);
            instance.OnProgress += (c, total, error, phase, message) => OnProgress?.Invoke(c, total, error, phase, message);
            await instance.Sanitize(cacheDir, token);
        }
        catch {
            Terminate();
            throw;
        }
    }

    /// <summary>
    /// 後始末
    /// </summary>
    public static void Terminate() {
        instance?.Dispose();
        instance = null;
    }
}

/// <summary>
/// WML
/// </summary>
public static class WmlStrapper {
    static OrtEnv? ortEnv;
    public static ProgressDelegaty? OnProgress;
    public static MessageDelegaty? OnMessage;

    /// <summary>
    /// WML初期化
    /// </summary>
    /// <param name="allowDownload"></param>
    /// <returns></returns>
    public static async Task Sanitize(bool allowDownload) {
        if (ortEnv != null) {
            return;
        }
        OnMessage?.Invoke(Properties.Resources.TW_InitializingWML);

        EnvironmentCreationOptions envOptions = new()
        {
            logId = nameof(ImogeIndex),
            logLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO,
            loggingFunction = CustomsLoggingsProcedures
        };
        ortEnv = OrtEnv.CreateInstanceWithOptions(ref envOptions);
        var catalog = Microsoft.Windows.AI.MachineLearning.ExecutionProviderCatalog.GetDefault();
        await catalog.EnsureAndRegisterCertifiedAsync();
        OnMessage?.Invoke(Properties.Resources.TW_WMLInitialized);
    }


    /// <summary>
    /// ORTのﾛｸﾞﾌｯｸ
    /// </summary>
    /// <param name="param"></param>
    /// <param name="severity"></param>
    /// <param name="category"></param>
    /// <param name="loggerId"></param>
    /// <param name="codeLocation"></param>
    /// <param name="message"></param>
    private static void CustomsLoggingsProcedures(IntPtr param, OrtLoggingLevel severity, string category, string loggerId, string codeLocation, string message)
    {
        var logLine = $"[{severity}] [{category}] {message}";
        Logger.Dictator.Log(severity switch
        {
            OrtLoggingLevel.ORT_LOGGING_LEVEL_VERBOSE => LogLevel.Debug,
            OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO => LogLevel.Info,
            OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING => LogLevel.Warn,
            OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR => LogLevel.Error,
            OrtLoggingLevel.ORT_LOGGING_LEVEL_FATAL => LogLevel.Error,
            _ => LogLevel.Debug
        }, logLine);
    }

    /// <summary>
    /// 後始末
    /// </summary>
    public static void Terminate() {
        ortEnv?.Dispose();
        ortEnv = null;
    }
}
