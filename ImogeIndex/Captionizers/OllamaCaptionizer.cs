using ImogeIndex.Common;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Tomlet;
using Tomlet.Attributes;

namespace ImogeIndex.Captionizers;

public class OllamaSetting
{
    [TomlPrecedingComment("ollamaのURL")]
    public string? Host { get; set; } = "http://localhost:11434";

    [TomlPrecedingComment("モデル")]
    public string? Model { get; set; } = "qwen3-vl:8b-instruct";

    [TomlPrecedingComment("ollamaに送信する画像の長辺サイズ")]
    public int ImageSize { get; set; } = 1024;

    [TomlPrecedingComment("失敗時のリトライ回数")]
    public int Retry { get; set; } = 2;

    [TomlPrecedingComment("カスタムプロンプトを使う")]
    public bool UseCustomPrompt { get; set; } = false;

    [TomlPrecedingComment("カスタムプロンプト")]
    public string CustomPrompt { get; set; } = string.Empty;

    public float Temperature { get; set; } = 0;
    public float TopP { get; set; } = 0.95f;
    public int TopK { get; set; } = 64;
    public float RepeatPenalty { get; set; } = 1.05f;
    public int NumCtx { get; set; } = 4096;
    public int NumPredict { get; set; } = 512;
}


/// <summary>
/// ICaptionizerのollama用実装
/// </summary>
public class OllamaCaptionizer : CaptionizerBase
{
    OllamaSetting setty;
    public OllamaCaptionizer(PreparedSettings root, OllamaSetting setty) :base() {
        this.setty = setty;
    }

    public const string SystemPrompt =
    #region プロンプツ
"""
あなたは画像キャプショニング用の視覚分析モデルです。
入力画像を確認し、明確に見える内容だけを記述してください。

[出力ルール]
- 出力はTOML本文の生テキストのみとしてください。
- Markdownのコードフェンス（```toml ... ```）、前置き、解説、補足は一切禁止します。
- 値に改行を含めないでください。
- 文字列は必ずダブルクォート（""）で囲んでください。
- 配列はTOML配列（["a", "b"]）として出力してください。
- keywords、costume、objects、use_casesは0～10個の要素でそれ以上は出力しないでください。
- 許可値の指定が無いフィールドの値は、必ず日本語で記述してください。
- 不明な場合は、列挙型フィールドでは "unclear"、説明文では "不明" と書いてください。
- 個人の身元、名前、職業、国籍、年齢、および不確かな色や物体（反射や光によるもの）は推測しないでください。
- 「自信のある間違い」より「曖昧でも正しい記述」を優先してください。
- 上記のルールに完全に従い、前置きやMarkdownなしで、TOMLの生テキストの1文字目から出力を開始してください

[許可値（これ以外の値は使用禁止）]
time_of_day = "predawn", "dawn_morning", "midday", "afternoon", "golden_hour", "dusk", "night", "unclear"
shot_type = "wide", "medium", "close-up", "drone aerial", "handheld", "static", "POV", "unclear"

[出力フォーマット（この構造を完全に模倣してください）]
time_of_day = "ここに許可値から選択"
dominant_color_palette = "パレットの短い説明"
people_count = 0
keywords = ["tag1", "tag2", "tag3", "tag4"]

details.scene = "場所、時間帯、照明を1文で説明。"
details.subjects = "フレーム内の人数、役割、行動を一般化して説明。"
details.action = "画像内で読み取れる静止状態としての状況を説明"
details.mood = "感情的または空間的なトーン。"
details.shot_type = "ここに許可値から選択"
details.impression = "画像を見た感想を簡潔にカジュアルに記述してください"
details.costume = ["服装1", "服装2", "服装3","服装4"  ]
details.objects = ["物体1", "物体2", "物体3", "物体4"]
details.use_cases = ["用途1", "用途2", "用途3","用途4"]
""";
    #endregion
    static readonly Regex NULL_VALUE = new(@"(キャラクター?|服装|コスチューム|物体|オブジェクト|用途|タグ|tag)[0-9]+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    override protected bool IsNullValue(string str) => NULL_VALUE.IsMatch(str);
    override public async Task Verify(CancellationToken token) {
        if (string.IsNullOrEmpty(setty.Host)) {
            throw new Exception(Properties.Resources.CAP_E_EndpointNotSet);
        }
        var mudels = await Models(token)
            ?? throw new Exception(Properties.Resources.CAP_E_ModelListFailed.Morfat(nameof(OllamaCaptionizer)));
        var tm = mudels.Models.FirstOrDefault(w => w.Name == setty.Model);
        if (tm == null) {
            throw new Exception(Properties.Resources.CAP_E_ModelUnavailable.Morfat(setty.Model));
        }
    }

    protected override int RetryCount => setty.Retry;
    protected override CaptionizerType Type => CaptionizerType.Ollama;

    protected override async Task<Gachas> PredictOnce(string imagePath, CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();
        var (base64Image, sentPng) = LoadImageAsBase64(imagePath, setty.ImageSize);

        var modelName = setty.Model!;

        var requestBody = new OllamaChatRequest {
            Model = modelName,
            Stream = false, // 一括で結果を受け取る
            Messages =
            [
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = setty.UseCustomPrompt ? setty.CustomPrompt :  SystemPrompt,
                    Images = new[] { base64Image }
                }
            ],
            Options = new OllamaChatOptions {
                Temperature = setty.Temperature, // 0.1f,
                TopP = setty.TopP, //0.95f,
                TopK = setty.TopK,
                RepeatPenalty = setty.RepeatPenalty,
                NumCtx = setty.NumCtx,
                NumPredict = setty.NumPredict,
            }
        };

        var url = $"{setty.Host}/api/chat";

        // TODO: JSONきらい、JSONさっさと滅びろ

        string jsonPayload = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        var jr = await SendJanitizedRequest(request, HttpTimeout, token);
        jr.EnsureSuccessStatusCode(Type);
        stopwatch.Stop();

        var result = JsonSerializer.Deserialize<OllamaChatResponse>(jr.Content);
        var rc = ParseCaption(result?.Message?.Content);
        rc.Model = modelName;
        var tv = TomletMain.TomlStringFrom(rc, TomlSerializerOptions.Default);
        InvokeCaptionRecieved(stopwatch.Elapsed, imagePath, sentPng, tv);
        return rc;
    }

    public async Task<OllamaVersionResponse?> Version(CancellationToken token) {
        var url = $"{setty.Host}/api/version";
        string? json = null;
        using var response = await httpClientPoller.GetAsync(url, token);
        try {
            json = await response.Content.ReadAsStringAsync(token);
            response.EnsureSuccessStatusCode();
            var result = JsonSerializer.Deserialize<OllamaVersionResponse>(json);
            return result;
        }
        catch (HttpRequestException e) {
            if (!string.IsNullOrEmpty(json)) {
                Logger.Dictator.Error(e, json);
                throw new GachasControllerException(CaptionizerType.Ollama, json, e);
            }
            throw;
        }
        catch {
            throw;
        }
    }

    public async Task<OllamaTagsResponse?> Models(CancellationToken token) {
        var url = $"{setty.Host}/api/tags";
        string? json = null;
        using var response = await httpClientPoller.GetAsync(url, token);
        try {
            json = await response.Content.ReadAsStringAsync(token);
            response.EnsureSuccessStatusCode();
            var result = JsonSerializer.Deserialize<OllamaTagsResponse>(json);
            return result;
        }
        catch (HttpRequestException e) {
            if (!string.IsNullOrEmpty(json)) {
                Logger.Dictator.Error(e, json);
                throw new GachasControllerException(CaptionizerType.Ollama, json, e);
            }
            throw;
        }
        catch {
            throw;
        }
    }

    public async Task<OllamaPsResponse?> PS(CancellationToken token) {
        var url = $"{setty.Host}/api/ps";
        string? json = null;

        using var response = await httpClientPoller.GetAsync(url, token);
        try {
            json = await response.Content.ReadAsStringAsync(token);
            response.EnsureSuccessStatusCode();
            var result = JsonSerializer.Deserialize<OllamaPsResponse>(json);
            return result;
        }
        catch (HttpRequestException e) {
            if (!string.IsNullOrEmpty(json)) {
                Logger.Dictator.Error(e, json);
                throw new GachasControllerException(CaptionizerType.Ollama, json, e);
            }
            throw;
        }
        catch {
            throw;
        }
    }
}

#region Ollamaペイロード

public class OllamaRunningModel {
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("details")] public OllamaModelDetails? Details { get; set; }
    [JsonPropertyName("digest")] public string Digest { get; set; } = string.Empty;

    [JsonPropertyName("expires_at")] public DateTime ExpiresAt { get; set; }
    [JsonPropertyName("size_vram")] public long SizeVram { get; set; }
    [JsonPropertyName("context_length")] public long ContextLength { get; set; }
}

public class OllamaPsResponse {
    [JsonPropertyName("models")] public OllamaRunningModel[] Models { get; set; } = Array.Empty<OllamaRunningModel>();
}


public class OllamaModelDetails {
    [JsonPropertyName("format")] public string Format { get; set; } = string.Empty;
    [JsonPropertyName("family")] public string Family { get; set; } = string.Empty;

    [JsonPropertyName("families")] public string[] Families { get; set; } = Array.Empty<string>();
    [JsonPropertyName("parameter_size")] public string ParameterSize { get; set; } = string.Empty;
    [JsonPropertyName("quantization_level")] public string QuantizationLevel { get; set; } = string.Empty;
}

public class OllamaModel {
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
    [JsonPropertyName("remote_model")] public string RemoteModel { get; set; } = string.Empty;
    [JsonPropertyName("remote_host")] public string RemoteHost { get; set; } = string.Empty;
    [JsonPropertyName("modified_at")] public DateTime ModifiedAt { get; set; }
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("digest")] public string Digest { get; set; } = string.Empty;
    [JsonPropertyName("details")] public OllamaModelDetails? Details { get; set; }
}


public class OllamaTagsResponse {
    [JsonPropertyName("models")] public OllamaModel[] Models { get; set; } = Array.Empty<OllamaModel>();
}


public class OllamaChatRequest {
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
    [JsonPropertyName("messages")] public OllamaChatMessage[] Messages { get; set; } = Array.Empty<OllamaChatMessage>();
    [JsonPropertyName("think")] public bool Think { get; set; } = false;

    [JsonPropertyName("stream")] public bool Stream { get; set; }
    [JsonPropertyName("options")] public OllamaChatOptions? Options { get; set; }
}

public class OllamaChatMessage {
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    [JsonPropertyName("images")] public string[]? Images { get; set; } // Base64の配列
}

public class OllamaChatOptions {
    [JsonPropertyName("temperature")] public float Temperature { get; set; }
    [JsonPropertyName("top_p")] public float TopP { get; set; }
    [JsonPropertyName("top_k")] public int TopK { get; set; }
    [JsonPropertyName("repeat_penalty")] public float RepeatPenalty { get; set; }

    [JsonPropertyName("num_ctx")] public int NumCtx { get; set; } = 4096;
    [JsonPropertyName("num_predict")] public int NumPredict { get; set; } = 512;
}

public class OllamaChatResponse {
    [JsonPropertyName("message")] public OllamaResponseMessage? Message { get; set; }
}

public class OllamaResponseMessage {
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
}

public class OllamaErrorResponse {
    [JsonPropertyName("error")] public string Error { get; set; } = string.Empty;
}

public class OllamaVersionResponse {
    [JsonPropertyName("version")] public string? Version { get; set; }
}

#endregion

