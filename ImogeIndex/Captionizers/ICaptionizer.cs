using ImogeIndex.Common;
using ImogeIndex.Imoging;
using SkiaSharp;
using System.Reflection;
using System.Text.Json;
using Tomlet;
using Tomlet.Attributes;
using Tomlet.Exceptions;
using Tomlet.Models;

namespace ImogeIndex.Captionizers;


/// <summary>
/// LLMからの応答
/// </summary>
public class GachasController {
    [TomlProperty("scene")]
    public string? Scene { get; set; }
    [TomlProperty("subjects")]
    public string? Subjects { get; set; }
    [TomlProperty("action")]
    public string? Action { get; set; }
    [TomlProperty("mood")]
    public string? Mood { get; set; }
    [TomlProperty("shot_type")]
    public string? ShotType { get; set; }

    [TomlProperty("impression")]
    public string? Impression { get; set; }


    [TomlProperty("costume")]
    public List<string> Costume { get; set; } = new();

    [TomlProperty("objects")]
    public List<string> Objects { get; set; } = new();
    [TomlProperty("use_cases")]
    public List<string> UseCases { get; set; } = new();

    public IEnumerable<KeyValuePair<string, string>> DumpFields() {
        foreach (var p in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            string? ps = null;
            if (p.PropertyType == typeof(List<string>)) {
                var lust = p.GetValue(this) as List<string>;
                if (lust != null) {
                    ps = string.Join(", ", lust);
                }
            }
            else {
                ps = p.GetValue(this) as string;
            }
            if (!string.IsNullOrEmpty(ps)) {
                yield return new KeyValuePair<string, string>(p.Name, ps);
            }
        }
    }
}

/// <summary>
/// LLMからの応答
/// </summary>
public class Gachas {
    [TomlProperty("time_of_day")]
    public string? TimeOfDay { get; set; }
    [TomlProperty("dominant_color_palette")]
    public string? DominantColorPalette { get; set; }

    [TomlProperty("people_count")]
    public int PeopleCount { get; set; } = 0;
    [TomlProperty("keywords")]
    public List<string> Keywords { get; set; } = new();

    [TomlProperty("details")]
    public GachasController Details { get; set; } = new();

    [TomlNonSerialized]
    public string? Model { get; set; }
}

public sealed class EmptyCaptionException : TomlException {
    public override string Message => Properties.Resources.CAP_E_EmptyCaption;
}

/// <summary>
/// ｷｬﾌﾟｼｮﾝ受信ﾃﾞﾘｹﾞーﾃｨー
/// </summary>
public delegate void CaptionRecievedDelegaty(TimeSpan elapsed, string filePath, byte[]? sentImage, string content);

/// <summary>
/// ｷｬﾌﾟｼｮﾆﾝｸﾞLLMｸﾗｲｱﾝﾄ
/// </summary>
public interface ICaptionizer {
    /// <summary>
    /// 受信ｲﾍﾞﾝﾄ
    /// </summary>
    event CaptionRecievedDelegaty? CaptionRecieved;

    /// <summary>
    /// HTTPﾀｲﾑｱｳﾄ(秒)
    /// </summary>
    int HttpTimeout { get; set; }

    /// <summary>
    /// 連続でﾘｸｴｽﾄが失敗したらｷｬﾌﾟｼｮﾆﾝｸﾞを打ち切る回数(0で無効)
    /// </summary>
    int AbortThreshold { get; set; }

    /// <summary>
    /// 連続失敗ｶｳﾝﾀをﾘｾｯﾄ(ｷｬﾌﾟｼｮﾆﾝｸﾞ開始時に呼ぶ)
    /// </summary>
    void ResetFailures();

    /// <summary>
    /// 接続&設定確認
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    Task Verify(CancellationToken token);

    /// <summary>
    /// 推論
    /// </summary>
    /// <param name="imagePath"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<Gachas?> Predict(string imagePath, CancellationToken token);

}

/// <summary>
/// 色々都合の良いﾍﾞーｽｸﾗｽ
/// </summary>
public abstract class CaptionizerBase : ICaptionizer
{
    static CaptionizerBase() {
        // ﾀｲﾑｱｳﾄはHttpTimeoutを使ってﾘｸｴｽﾄ毎にCTSで制御する
        httpClient = new HttpClient {
            Timeout = Timeout.InfiniteTimeSpan
        };
        httpClientPoller = new HttpClient {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }
    protected static readonly HttpClient httpClient;
    protected static readonly HttpClient httpClientPoller;

    protected CaptionizerBase() {
    }

    /// <summary>
    /// HTTPﾀｲﾑｱｳﾄ(秒)
    /// </summary>
    public int HttpTimeout { get; set; } = 180;

    /// <summary>
    /// ｷｬﾌﾟｼｮﾝ受信(ﾃﾞﾊﾞｯｸﾞ用)
    /// </summary>
    public event CaptionRecievedDelegaty? CaptionRecieved;

    /// <summary>
    /// ｲﾝｽﾀﾝｽ生成後の確認処理
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public virtual Task Verify(CancellationToken token) { throw new NotImplementedException(); }

    /// <summary>
    /// 連続でﾘｸｴｽﾄが失敗したらｷｬﾌﾟｼｮﾆﾝｸﾞを打ち切る回数(0で無効)
    /// </summary>
    public int AbortThreshold { get; set; } = 5;

    /// <summary>
    /// 連続ﾘｸｴｽﾄ失敗回数
    /// </summary>
    int consecutiveFailures;

    /// <summary>
    /// 今の連続失敗中にVerifyで一度救済済み？
    /// </summary>
    bool verifyRescued;

    /// <summary>
    /// 連続失敗ｶｳﾝﾀをﾘｾｯﾄ
    /// </summary>
    public void ResetFailures() {
        consecutiveFailures = 0;
        verifyRescued = false;
    }

    /// <summary>
    /// 失敗時のﾘﾄﾗｲ回数
    /// </summary>
    protected virtual int RetryCount => 0;

    /// <summary>
    /// 実装の種類(ｴﾗーﾍﾟｲﾛーﾄﾞの解釈に使う)
    /// </summary>
    protected virtual CaptionizerType Type => CaptionizerType.Ollama;

    /// <summary>
    /// ｷｬﾌﾟｼｮﾆﾝｸﾞ実行
    /// ﾘﾄﾗｲと連続失敗による打ち切りはここで面倒を見る
    /// </summary>
    /// <param name="imagePath"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="CaptionFailureException">この画像のｷｬﾌﾟｼｮﾆﾝｸﾞに失敗</exception>
    /// <exception cref="CaptionAbortedException">連続失敗が閾値に達した</exception>
    public async Task<Gachas?> Predict(string imagePath, CancellationToken token) {
        for (int i = 0; ; i++) {
            try {
                var rc = await PredictOnce(imagePath, token);
                ResetFailures();
                return rc;
            }
            catch (Exception e) when (!token.IsCancellationRequested) {
                var f = Classify(e);
                Logger.Dictator.Warn($"キャプショニング失敗({f.Typo}) {i + 1}/{RetryCount + 1} : {imagePath} : {f.Message}");
                if (f.Typo == CaptionErrorType.Fatal) {
                    throw f;
                }
                if (f.Typo == CaptionErrorType.Unreachable) {
                    consecutiveFailures++;
                    if (AbortThreshold > 0 && consecutiveFailures >= AbortThreshold) {
                        // 一度だけﾎｽﾄの生存確認をして、生きていれば続行する
                        if (verifyRescued || !await TryVerify(token)) {
                            throw new CaptionAbortedException(consecutiveFailures, f);
                        }
                        Logger.Dictator.Warn($"リクエストが{consecutiveFailures}回連続で失敗したが、Verifyは成功したので続行する");
                        verifyRescued = true;
                        consecutiveFailures = 0;
                    }
                }
                else {
                    // ﾎｽﾄは応答しているので連続失敗扱いしない
                    ResetFailures();
                }
                if (!f.Retryable || i >= RetryCount) {
                    throw f;
                }
                await Task.Delay(RetryDelay(i), token);
            }
        }
    }

    /// <summary>
    /// 1回分の推論
    /// </summary>
    /// <param name="imagePath"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    protected virtual Task<Gachas> PredictOnce(string imagePath, CancellationToken token) { throw new NotImplementedException(); }

    /// <summary>
    /// Verifyが通るか確認
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    async Task<bool> TryVerify(CancellationToken token) {
        try {
            await Verify(token);
            return true;
        }
        catch (Exception e) when (!token.IsCancellationRequested) {
            Logger.Dictator.Warn($"Verify失敗 : {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// ﾘﾄﾗｲ待ち時間(1,2,4,8...最大30秒)
    /// </summary>
    /// <param name="attempt"></param>
    /// <returns></returns>
    static TimeSpan RetryDelay(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(30, 1 << Math.Min(attempt, 5)));

    /// <summary>
    /// 例外を分類する
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    static CaptionFailureException Classify(Exception e) => e switch {
        CaptionFailureException cf => cf,
        // ﾕーｻﾞーｷｬﾝｾﾙ以外 = ﾀｲﾑｱｳﾄ
        OperationCanceledException => new(CaptionErrorType.Unreachable, true, Properties.Resources.CAP_E_RequestTimeout, e),
        // ｽﾃーﾀｽｺーﾄﾞが無い = そもそも接続できていない
        HttpRequestException { StatusCode: null } => new(CaptionErrorType.Unreachable, true, Properties.Resources.CAP_E_HostUnreachable.Morfat(e.Message), e),
        HttpRequestException hre => new(CaptionErrorType.Unreachable, true, Properties.Resources.CAP_E_HttpError.Morfat(hre.StatusCode, e.Message), e),
        IOException => new(CaptionErrorType.Unreachable, true, Properties.Resources.CAP_E_NetworkError.Morfat(e.Message), e),
        TomlException or JsonException => new(CaptionErrorType.Content, true, e.Message, e),
        _ => new(CaptionErrorType.Content, false, e.Message, e),
    };

    /// <summary>
    /// ﾔﾆﾀｲｽﾞﾚｽﾎﾟﾝｽ
    /// </summary>
    protected class JanitizedResponse
    {
        public Uri? Uri { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ReasonPhrase { get; set; }
        public System.Net.HttpStatusCode HttpStatusCode { get; set; }

        /// <summary>
        /// 例のあれ
        /// ｽﾃーﾀｽｺーﾄﾞで失敗を分類して投げる
        /// </summary>
        /// <param name="type"></param>
        /// <exception cref="CaptionFailureException"></exception>
        public void EnsureSuccessStatusCode(CaptionizerType type)
        {
            if (Success) {
                return;
            }
            var code = (int)HttpStatusCode;
            var (typo, retryable) = code switch {
                401 or 403 or 404 => (CaptionErrorType.Fatal, false),
                408 or 429 or >= 500 => (CaptionErrorType.Unreachable, true),
                _ => (CaptionErrorType.Content, false),
            };
            var status = string.IsNullOrEmpty(ReasonPhrase) ?
                $"{Uri} {HttpStatusCode}" : $"{Uri} : {ReasonPhrase}({HttpStatusCode})";
            Logger.Dictator.Error($"{status}{Environment.NewLine}{Content}");
            var inner = new HttpRequestException(status, inner: null, statusCode: HttpStatusCode);
            if (string.IsNullOrEmpty(Content)) {
                throw new CaptionFailureException(typo, retryable, status, inner);
            }
            throw new CaptionFailureException(typo, retryable, type, Content, inner);
        }
    }

    /// <summary>
    /// ﾔﾆﾀｲｽﾞﾘｸｴｽﾄ送信
    /// </summary>
    /// <param name="massage"></param>
    /// <param name="timeout"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected async Task<JanitizedResponse> SendJanitizedRequest(HttpRequestMessage massage, int timeout, CancellationToken token) {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
        using var response = await httpClient.SendAsync(massage, linkedCts.Token);
        var res = await response.Content.ReadAsStringAsync(linkedCts.Token);
        return new JanitizedResponse {
            Uri = massage.RequestUri,
            Content = res ?? string.Empty,
            Success = response.IsSuccessStatusCode,
            HttpStatusCode = response.StatusCode,
            ReasonPhrase = response.ReasonPhrase
        };
    }

    /// <summary>
    /// そのTOMLの値はnull扱いする？
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    protected virtual bool IsNullValue(string str) => false;

    /// <summary>
    /// CaptionRecievedｲﾍﾞﾝﾄ発射
    /// </summary>
    /// <param name="elapsed"></param>
    /// <param name="filePath"></param>
    /// <param name="sentImage">実際に送信したPNG</param>
    /// <param name="content"></param>
    protected void InvokeCaptionRecieved(TimeSpan elapsed, string filePath, byte[]? sentImage, string content)
    {
        CaptionRecieved?.Invoke(elapsed, filePath, sentImage, content);
    }

    /// <summary>
    /// 受信ﾃﾞーﾀのﾌｨﾙﾀﾘﾝｸﾞ
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obzekt"></param>
    protected void ProcessProperties<T>(T obzekt)
    {
        if (obzekt == null) {
            return;
        }
        foreach (var p in typeof(T).GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) {
            if (p.PropertyType == typeof(List<string>)) {
                ProcessListOfString(p, obzekt);
                continue;
            }
            if (p.PropertyType != typeof(string)) {
                continue;
            }
            var str = (string?)p.GetValue(obzekt);
            p.SetValue(obzekt, HandleNullValue(str));
        }
    }

    /// <summary>
    /// Listのﾌｨﾙﾀﾘﾝｸﾞ
    /// </summary>
    /// <param name="property"></param>
    /// <param name="obzekt"></param>
    protected void ProcessListOfString(PropertyInfo property, object obzekt)
    {
        var mur = (List<string>?)property.GetValue(obzekt);
        if (mur == null || mur.Count == 0) {
            return;
        }
        var list = mur.Where(w => HandleNullValue(w) != null)?.ToList();
        property.SetValue(obzekt, list);
    }

    /// <summary>
    /// それnull？
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    protected string? HandleNullValue(string? str)
    {
        if (string.IsNullOrEmpty(str)) {
            return null;
        }
        if (str == "不明" || str == "unclear") {
            return null;
        }
        if (IsNullValue(str)) {
            return null;
        }
        return str;
    }

    /// <summary>
    /// TOMLをﾊﾟーｽしてｶﾞﾁｬ化
    /// </summary>
    /// <param name="mur"></param>
    /// <returns></returns>
    protected Gachas? ProcessTomlResponse(string mur) {
        var si = mur.IndexOf("```toml", StringComparison.OrdinalIgnoreCase);
        if (si >= 0) {
            mur = mur.Substring(si + "```toml".Length);
        }
        else {
            si = mur.IndexOf("```", StringComparison.Ordinal);
            if (si >= 0) {
                mur = mur.Substring(si + "```".Length);
            }
        }
        si = mur.IndexOf("```");
        if (si >= 0) {
            mur = mur.Substring(0, si);
        }
        TomlParser parser = new TomlParser();
        TomlDocument document = parser.Parse(mur);
        var rc = TomletMain.To<Gachas>(document);
        if (rc != null) {
            ProcessProperties(rc);
            ProcessProperties(rc.Details);
            // ﾓﾃﾞﾙ名や既定値の人数だけではｷｬﾌﾟｼｮﾝ成功としない
            var details = rc.Details;
            var fields = new[] {
                rc.TimeOfDay, rc.DominantColorPalette,
                details.Scene, details.Subjects, details.Action, details.Mood,
                details.ShotType, details.Impression
            };
            if (rc.PeopleCount == 0 &&
                !fields.Concat(rc.Keywords).Concat(details.Costume)
                    .Concat(details.Objects).Concat(details.UseCases)
                    .Any(value => !string.IsNullOrWhiteSpace(value))) {
                throw new EmptyCaptionException();
            }
        }
        return rc;
    }

    /// <summary>
    /// LLMの応答本文をｶﾞﾁｬ化する
    /// 失敗時はTOMLをﾛｸﾞに吐く
    /// </summary>
    /// <param name="mur"></param>
    /// <returns></returns>
    /// <exception cref="CaptionFailureException"></exception>
    protected Gachas ParseCaption(string? mur) {
        if (string.IsNullOrEmpty(mur)) {
            throw new CaptionFailureException(CaptionErrorType.Content, true, Properties.Resources.CAP_E_EmptyVlmRespnse);
        }
        try {
            return ProcessTomlResponse(mur)
                ?? throw new CaptionFailureException(CaptionErrorType.Content, true, Properties.Resources.CAP_E_TomlCorruption);
        }
        catch (Exception) {
            Logger.Dictator.Error("=== BEGIN TOML === " + Environment.NewLine + mur + Environment.NewLine + "=== END TOML === ");
            throw;
        }
    }

    /// <summary>
    /// 画像を読み込んでﾘｻｲｽﾞして送信用のPNGとBase64作る
    /// </summary>
    /// <param name="imagePath"></param>
    /// <param name="imageSize"></param>
    /// <returns>Base64とPNG</returns>
    /// <exception cref="CaptionFailureException"></exception>
    protected static (string Base64, byte[] Png) LoadImageAsBase64(string imagePath, int imageSize) {
        SKBitmap? imoge;
        try {
            imoge = ImageLoader.LoadAndResize(imagePath, imageSize);
        }
        catch (Exception e) when (e is not OperationCanceledException) {
            // ﾛーｶﾙの画像が読めないのはﾎｽﾄ不達ではないので、ﾘﾄﾗｲせずこの画像だけ失敗にする
            throw new CaptionFailureException(CaptionErrorType.Content, false, Properties.Resources.CAP_E_LoadImageNtr.Morfat(imagePath, e.Message), e);
        }
        using (imoge) {
            if (imoge == null) {
                throw new CaptionFailureException(CaptionErrorType.Content, false, Properties.Resources.CAP_E_LoadImage.Morfat(imagePath));
            }
            var png = imoge.AsByteArray();
            return (Convert.ToBase64String(png), png);
        }
    }
}
