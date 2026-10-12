using ImogeIndex.Common;
using System.Text.Json;

namespace ImogeIndex.Captionizers;

/// <summary>
/// すごい例外
/// </summary>
public class GachasControllerException : Exception {
    public GachasControllerException() : base() { msg = string.Empty; }
    public GachasControllerException(string message) : base(message) { msg = message; }
    public GachasControllerException(string message, Exception? inner) : base(message, inner) { msg = message; }
    public GachasControllerException(CaptionizerType type, string message) : base(message) { msg = ProcessPayload(type, message); }
    public GachasControllerException(CaptionizerType type, string message, Exception? inner) : base(message, inner) { msg = ProcessPayload(type, message); }

    /// <summary>
    /// ﾍﾟｲﾛーﾄﾞ都合良く解釈
    /// </summary>
    /// <param name="type"></param>
    /// <param name="payload"></param>
    /// <returns></returns>
    private string ProcessPayload(CaptionizerType type, string payload) {
        if (string.IsNullOrEmpty(payload)) {
            return string.Empty;
        }
        try {
            // TODO: JSONきらい、JSONさっさと滅びろ
            return type switch
            {
                CaptionizerType.Ollama => ((Func<string>)(() => {
                    var err = JsonSerializer.Deserialize<OllamaErrorResponse>(payload);
                    if (err == null) {
                        return payload;
                    }
                    return err.Error;
                }))(),
                CaptionizerType.LLamaCpp => ((Func<string>)(() => {
                    var err = JsonSerializer.Deserialize<LLamaErrorResponse>(payload);
                    if (err?.Error == null) {
                        return payload;
                    }
                    return err.Error.Message ?? payload;
                }))(),
                CaptionizerType.OpenAICompat => ((Func<string>)(() => {
                    var err = JsonSerializer.Deserialize<LLamaErrorResponse>(payload);
                    if (err?.Error == null) {
                        return payload;
                    }
                    return err.Error.Message ?? payload;
                }))(),
                _ => payload
            };
        }
        catch {
            return payload;
        }
    }

    string msg;
    public override string Message => msg;
}

/// <summary>
/// ｷｬﾌﾟｼｮﾆﾝｸﾞ失敗の分類
/// </summary>
public enum CaptionErrorType {
    /// <summary>
    /// ﾎｽﾄに届かない/応答しない(接続拒否・ﾀｲﾑｱｳﾄ・5xx)
    /// </summary>
    Unreachable,
    /// <summary>
    /// その画像固有の失敗(4xx・TOML不正・空応答・画像読込失敗)
    /// </summary>
    Content,
    /// <summary>
    /// 設定ﾐｽ等で続行不能(401/403/404)
    /// </summary>
    Fatal,
}

/// <summary>
/// 分類付きのｷｬﾌﾟｼｮﾆﾝｸﾞ例外
/// </summary>
public class CaptionFailureException : GachasControllerException {
    /// <summary>
    /// 失敗の分類
    /// </summary>
    public CaptionErrorType Typo { get; }

    /// <summary>
    /// ﾘﾄﾗｲする価値がある？
    /// </summary>
    public bool Retryable { get; }

    public CaptionFailureException(CaptionErrorType type, bool retryable, string message, Exception? inner = null)
        : base(message, inner) {
        Typo = type;
        Retryable = retryable;
    }

    public CaptionFailureException(CaptionErrorType etr, bool retryable, CaptionizerType type, string payload, Exception? inner = null)
        : base(type, payload, inner) {
        Typo = etr;
        Retryable = retryable;
    }
}

/// <summary>
/// 連続でﾘｸｴｽﾄが失敗したのでｷｬﾌﾟｼｮﾆﾝｸﾞを打ち切る
/// </summary>
public sealed class CaptionAbortedException : GachasControllerException {
    /// <summary>
    /// 連続失敗回数
    /// </summary>
    public int Failures { get; }

    public CaptionAbortedException(int failures, Exception last)
        : base(Properties.Resources.CAP_E_AbortedException.Morfat(failures, last.Message), last) {
        Failures = failures;
    }
}

/// <summary>
/// VLMでｷｬﾌﾟｼｮﾆﾝｸﾞする
/// </summary>
public class Captionizer {
    //static Captionizer() { }
    static ICaptionizer? captionizer;
    public static ICaptionizer? Dictator => captionizer;

    static CaptionizeSetting? setting;

    /// <summary>
    /// 受信ｲﾍﾞﾝﾄ
    /// </summary>
    public static event CaptionRecievedDelegaty? CaptionRecieved;

    /// <summary>
    /// ICaptionizer取得
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T? Obiekt<T>() where T: class? {
        if (captionizer == null || captionizer is not T obiekt) {
            return null;
        }
        return obiekt;
    }

    /// <summary>
    /// 設定
    /// </summary>
    /// <param name="setty"></param>
    public static void Sanitize(PreparedSettings root, CaptionizeSetting setty) {
        Contaminate();
        setting = setty;
        switch (setting.Type) {
            case CaptionizerType.Ollama:
                captionizer = new OllamaCaptionizer(root, setty.Ollama);
                break;
            case CaptionizerType.LLamaCpp:
                captionizer = new LLamaCppCaptionizer(root, setty.LLamaCpp);
                break;
            case CaptionizerType.OpenAICompat:
                captionizer = new OpenAICompatCaptionizer(root, setty.OpenAICompat);
                break;
            default:
                throw new NotImplementedException($"{setting.Type}");
        }
        if (captionizer != null) {
            captionizer.CaptionRecieved += Captionizer_CaptionRecieved;
            captionizer.HttpTimeout = setting.HttpTimeout;
            captionizer.AbortThreshold = setting.AbortThreshold;
        }
    }

    /// <summary>
    /// ｸﾘﾅｯﾌﾟ
    /// </summary>
    public static void Contaminate() {
        if (captionizer != null) {
            captionizer.CaptionRecieved -= Captionizer_CaptionRecieved;
        }
        captionizer = null;
        setting = null;
    }

    /// <summary>
    /// 推論
    /// </summary>
    /// <param name="imagePath"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static Task<Gachas?> Predict(string imagePath, CancellationToken token) {
        if (captionizer == null) {
            throw new GachasControllerException(Properties.Resources.CAP_E_NotSanitized);
        }
        return captionizer.Predict(imagePath, token);
    }

    static void Captionizer_CaptionRecieved(TimeSpan elapsed, string filePath, byte[]? sentImage, string content) {
        CaptionRecieved?.Invoke(elapsed, filePath, sentImage, content);
    }
}
