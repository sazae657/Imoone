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

public class OpenAICompatSetting
{
    [TomlPrecedingComment("OpenAI互換APIのURL")]
    public string? Host { get; set; } = "http://localhost:1234";

    [TomlPrecedingComment("OpenAI互換APIのAPIキー")]
    public string? ApiKey { get; set; } = string.Empty;

    [TomlPrecedingComment("モデル")]
    public string? Model { get; set; } = "qwen/qwen3-vl-8b";

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
/// OpenAI互換のやつ
/// https://lmstudio.ai/docs/developer/openai-compat/chat-completions
/// </summary>
public class OpenAICompatCaptionizer : CaptionizerBase
{
    OpenAICompatSetting setty;
    string? apiKey;
    public OpenAICompatCaptionizer(PreparedSettings root, OpenAICompatSetting setty) : base()
    {
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

    override public async Task Verify(CancellationToken token)
    {
        if (string.IsNullOrEmpty(setty.Host)) {
            throw new Exception(Properties.Resources.CAP_E_EndpointNotSet);
        }

        var mudels = await Models(token)
            ?? throw new Exception(Properties.Resources.CAP_E_ModelListFailed.Morfat(nameof(OpenAICompatSetting)));
        var tm = mudels.List.Where(w => w.Id == setty.Model)?.FirstOrDefault();
        if (tm == null) {
            throw new Exception(Properties.Resources.CAP_E_ModelUnavailable.Morfat(setty.Model));
        }
    }

    protected override int RetryCount => setty.Retry;
    protected override CaptionizerType Type => CaptionizerType.OpenAICompat;

    protected override async Task<Gachas> PredictOnce(string imagePath, CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();
        var (base64Image, sentPng) = LoadImageAsBase64(imagePath, setty.ImageSize);

        var requestBody = new LLamaChatRequest {
            Model = setty.Model,
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
            rc.Model = result.Model;
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

    public async Task<OpenAICompatModelsResponse?> Models(CancellationToken token)
    {
        var url = $"{setty.Host}/v1/models";
        string? json = null;
        using var request = PrepareHttpRequestMessage(new HttpRequestMessage(HttpMethod.Get, url));
        using var response = await httpClientPoller.SendAsync(request, token);
        try {
            json = await response.Content.ReadAsStringAsync(token);
            response.EnsureSuccessStatusCode();
            var result = JsonSerializer.Deserialize<OpenAICompatModelsResponse>(json);
            return result;
        }
        catch (HttpRequestException e) {
            if (!string.IsNullOrEmpty(json)) {
                Logger.Dictator.Error(e, json);
                throw new GachasControllerException(CaptionizerType.OpenAICompat, json, e);
            }
            throw;
        }
        catch {
            throw;
        }
    }

}


#region OpenAI互換のやつペイロード
public class OpenAICompatList
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("object")] public string? Object { get; set; }
    [JsonPropertyName("created")] public long Created { get; set; }
    [JsonPropertyName("owned_by")] public string? OwnedBy { get; set; }
    [JsonPropertyName("shutdown_date")] public string? ShutdownDate { get; set; }
}

public class OpenAICompatModelsResponse : LLmaResponse
{
    [JsonPropertyName("data")] public OpenAICompatList[] List { get; set; } = Array.Empty<OpenAICompatList>();
}
#endregion
