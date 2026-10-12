using ImogeIndex.Common;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Tomlet;
using Tomlet.Attributes;

namespace ImogeIndex.Captionizers;

public class LLamaCppSetting
{
    [TomlPrecedingComment("llama-serverのURL")]
    public string? Host { get; set; } = "http://localhost:8080";

    [TomlPrecedingComment("llama-serverのAPIキー")]
    public string? ApiKey { get; set; } = string.Empty;

    [TomlPrecedingComment("送信する画像の長辺サイズ")]
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
    public int MaxTokens { get; set; } = 512;
}

/// <summary>
/// ICaptionizerのllama.cpp用実装
/// だが、不定期に500ｴﾗーが返る
/// </summary>
public class LLamaCppCaptionizer : CaptionizerBase
{
    // llama-server --host 0.0.0.0
    // -m       /export/home/overseer/gguf/Qwen3VL-8B-Instruct-Q4_K_M.gguf
    // --mmproj /export/home/overseer/gguf/mmproj-Qwen3VL-8B-Instruct-F16.gguf

    LLamaCppSetting setty;
    string? apiKey;
    public LLamaCppCaptionizer(PreparedSettings root, LLamaCppSetting setty) : base() {
        this.setty = setty;
        if (!string.IsNullOrEmpty(setty.ApiKey)) {
            apiKey = root.Source.ExpandingClass(setty.ApiKey);
        }
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
        var health = await Health(token)
            ?? throw new Exception(Properties.Resources.CAP_E_HealthUnreachable);
        if (health.Status != "ok") {
            throw new Exception(Properties.Resources.CAP_E_Unhealthy.Morfat(health.Status));
        }
    }

    protected override int RetryCount => setty.Retry;
    protected override CaptionizerType Type => CaptionizerType.LLamaCpp;

    protected override async Task<Gachas> PredictOnce(string imagePath, CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();
        var (base64Image, sentPng) = LoadImageAsBase64(imagePath, setty.ImageSize);

        var requestBody = new LLamaChatRequest {
            Stream = false,
            Messages =
            [
                new LLamaChatMessage
                {
                    Role = "user",
                    Content = [
                        new LLamaTextContent{
                            Type = "text",
                            Text = setty.UseCustomPrompt ? setty.CustomPrompt :  SystemPrompt
                        },
                        new LLamaImageUrlContent{
                            Type = "image_url",
                            ImageUrl = new LLamaImageUrl{
                                Url = "data:image/png;base64," + base64Image
                            }
                        }
                    ]
                }
            ],

            Temperature = setty.Temperature, // 0.1f,
            TopP = setty.TopP, //0.95f,
            TopK = setty.TopK,
            RepeatPenalty = setty.RepeatPenalty,
            MaxTokens = setty.MaxTokens,
        };

        var url = $"{setty.Host}/v1/chat/completions";

        // TODO: JSONきらい、JSONさっさと滅びろ

        string jsonPayload = JsonSerializer.Serialize(requestBody);
        using var request = PrepareHttpRequestMessage(new HttpRequestMessage(HttpMethod.Post, url));
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        var jr = await SendJanitizedRequest(request, HttpTimeout, token);
        jr.EnsureSuccessStatusCode(Type);
        stopwatch.Stop();

        var result = JsonSerializer.Deserialize<LLmaChatResponse>(jr.Content);
        var rc = ParseCaption(result?.Choices?.FirstOrDefault()?.Message?.Content);
        if (result?.Model != null) {
            rc.Model = Path.GetFileName(result.Model);
        }
        var tv = TomletMain.TomlStringFrom(rc, TomlSerializerOptions.Default);
        InvokeCaptionRecieved(stopwatch.Elapsed, imagePath, sentPng, tv);
        return rc;
    }

    HttpRequestMessage PrepareHttpRequestMessage(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(apiKey)) {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
        return request;
    }

    public async Task<LLmaStatusResponse?> Health(CancellationToken token) {
        var url = $"{setty.Host}/v1/health";
        string? json = null;
        using var request = PrepareHttpRequestMessage(new HttpRequestMessage(HttpMethod.Get, url));
        using var response = await httpClientPoller.SendAsync(request, token);
        try {
            json = await response.Content.ReadAsStringAsync(token);
            response.EnsureSuccessStatusCode();
            var result = JsonSerializer.Deserialize<LLmaStatusResponse>(json);
            return result;
        }
        catch (HttpRequestException e) {
            if (!string.IsNullOrEmpty(json)) {
                Logger.Dictator.Error(e, json);
                throw new GachasControllerException(CaptionizerType.LLamaCpp, json, e);
            }
            throw;
        }
        catch {
            throw;
        }
    }
}

#region llama.cppペイロード

//
// TODO: LLamaて名付けたけどたぶんOpenAI(しらんけど)
//
[JsonConverter(typeof(LLamaChatContentConverter))]
public abstract class LLamaChatContent
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
}

public class LLamaTextContent : LLamaChatContent {
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
}

public class LLamaImageUrl
{
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
}

public class LLamaImageUrlContent : LLamaChatContent
{
    [JsonPropertyName("image_url")] public LLamaImageUrl ImageUrl { get; set; } = new();
}

public sealed class LLamaChatContentConverter : JsonConverter<LLamaChatContent>
{
    public override LLamaChatContent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var type = root.GetProperty("type").GetString();
        // TODO: typeのﾊーﾄﾞｺーﾄﾞが地味にﾀﾞｻいので考える
        return type switch
        {
            "text" => new LLamaTextContent
            {
                Type = type,
                Text = root.GetProperty("text").GetString() ?? string.Empty
            },
            "image_url" => new LLamaImageUrlContent
            {
                Type = type,
                ImageUrl = new LLamaImageUrl
                {
                    Url = root.GetProperty("image_url").GetProperty("url").GetString() ?? string.Empty
                }
            },
            _ => throw new JsonException(Properties.Resources.GL_E_UAP.Morfat(type))
        };
    }

    public override void Write(Utf8JsonWriter writer, LLamaChatContent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        // TODO: typeのﾊーﾄﾞｺーﾄﾞが地味にﾀﾞｻいので考える
        switch (value)
        {
            case LLamaTextContent textContent:
                writer.WriteString("type", textContent.Type);
                writer.WriteString("text", textContent.Text);
                break;
            case LLamaImageUrlContent imageUrlContent:
                writer.WriteString("type", imageUrlContent.Type);
                writer.WritePropertyName("image_url");
                JsonSerializer.Serialize(writer, imageUrlContent.ImageUrl, options);
                break;
            default:
                throw new JsonException(Properties.Resources.GL_E_UAP.Morfat(value.GetType().FullName));
        }

        writer.WriteEndObject();
    }
}

public class LLamaChatMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("content")] public LLamaChatContent[] Content { get; set; } = Array.Empty< LLamaChatContent>();
}

public class LLamaChatRequest
{
    [JsonPropertyName("model")] public string? Model { get; set; }

    [JsonPropertyName("messages")] public LLamaChatMessage[] Messages { get; set; } = Array.Empty<LLamaChatMessage>();
    [JsonPropertyName("temperature")] public float Temperature { get; set; }
    [JsonPropertyName("top_p")] public float TopP { get; set; }
    [JsonPropertyName("top_k")] public float TopK { get; set; }
    [JsonPropertyName("repeat_penalty")] public float RepeatPenalty { get; set; } = 1.05f;
    [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; } = 512;
    [JsonPropertyName("stream")] public bool Stream { get; set; } = false;
}

public class LLmaStatusResponse {
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
}

public class LLamaErrorResponseBody {
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("code")] public int Code { get; set; }
}

public class LLamaErrorResponse {
    [JsonPropertyName("error")] public LLamaErrorResponseBody? Error { get; set; }
}

public class LLmaResponse {
    [JsonPropertyName("created")] public int Created { get; set; }
    [JsonPropertyName("model")] public string? Model { get; set; }
    [JsonPropertyName("object")] public string? Object { get; set; }
}

public class LLamaMessage {
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("content")] public string? Content { get; set; }
}

public class LLamaChoice {
    [JsonPropertyName("finish_reason")] public string FinishReason { get; set; } = string.Empty;
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("message")] public LLamaMessage? Message { get; set; }
}

public class LLmaChatResponse : LLmaResponse {
    [JsonPropertyName("choices")] public LLamaChoice[] Choices { get; set; } = Array.Empty<LLamaChoice>();
}
#endregion
