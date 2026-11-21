using ImogeIndex.Captionizers;
using ImogeIndex.Common;

namespace ImogeIndex.Web2.Captionize;

public class CaptionResponsesController {
    sealed class TestCaptionizer : CaptionizerBase {
        public Gachas? Parse(string response) => ProcessTomlResponse(response);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("# コメントのみ")]
    [InlineData("```toml\n\n```")]
    [InlineData("unknown = \"値\"")]
    [InlineData("people_count = 0\nkeywords = []\n[details]\n")]
    [InlineData("time_of_day = \"不明\"\nkeywords = [\"unclear\", \"\"]\n[details]\nscene = \"unclear\"")]
    [InlineData("keywords = [\" \", \"  \"]\n[details]\nscene = \" \"")]
    public void EmptyCaptionFails(string response) {
        Assert.Throws<EmptyCaptionException>(() => new TestCaptionizer().Parse(response));
    }

    [Theory]
    [InlineData("time_of_day = \"昼\"")]
    [InlineData("dominant_color_palette = \"青\"")]
    [InlineData("people_count = 1")]
    [InlineData("keywords = [\"風景\"]")]
    [InlineData("[details]\nscene = \"風景\"")]
    [InlineData("[details]\nimpression = \"穏やか\"")]
    [InlineData("[details]\ncostume = [\"制服\"]")]
    [InlineData("[details]\nobjects = [\"机\"]")]
    [InlineData("[details]\nuse_cases = [\"背景\"]")]
    [InlineData("```toml\n[details]\nscene = \"風景\"\n```")]
    public void NonemptyCaptionSucceeds(string response) {
        Assert.NotNull(new TestCaptionizer().Parse(response));
    }

    [Theory]
    [InlineData("toml", "\n")]
    [InlineData("TOML", "\n")]
    [InlineData("ToMl", "\r\n")]
    [InlineData("", "\n")]
    [InlineData("", "\r\n")]
    public void FencedCaptionPreservesContent(string language, string newline) {
        var response = $"説明{newline}```{language}{newline}[details]{newline}scene = \"風景\"{newline}```{newline}補足";
        var result = new TestCaptionizer().Parse(response);
        Assert.NotNull(result);
        Assert.Equal("風景", result.Details.Scene);
    }

    /// <summary>
    /// Gemmaが生成する半壊TOMLをなんとかする
    /// </summary>
    /// <param name="actual"></param>
    /// <param name="expected"></param>
    [Theory]   
    [InlineData(CaptionReference.GEMMA_01, CaptionReference.GEMMA_01_C)]
    [InlineData(CaptionReference.GEMMA_02, CaptionReference.GEMMA_02_C)]
    [InlineData(CaptionReference.GEMMA_03, CaptionReference.GEMMA_03_C)]
    [InlineData(CaptionReference.GEMMA_04, CaptionReference.GEMMA_04_C)]
    [InlineData(CaptionReference.GEMMA_05, CaptionReference.GEMMA_05_C)]
    [InlineData(CaptionReference.GEMMA_06, CaptionReference.GEMMA_06_C)]  
    public void FixableContent(string actual, string expected) {
        Assert.ThrowsAny<Exception>(()=>TomlHelper.Parse<Gachas>(actual));
        var ra = new TestCaptionizer().Parse(actual);
        var re = TomlHelper.Parse<Gachas>(expected);
        Assert.Equivalent(re, ra);
    }

    /// <summary>
    /// 正常なTOMLは補正を通さない([details]行がｷー推定で壊されない)
    /// </summary>
    [Fact]
    public void ValidTomlIsNotErupted() {
        var response = "time_of_day = \"昼\"\ndominant_color_palette = \"青\"\nkeywords = [\"風景\"]\n[details]\nscene = \"海辺\"";
        var result = new TestCaptionizer().Parse(response);
        Assert.NotNull(result);
        Assert.Equal(["風景"], result.Keywords);
        Assert.Equal("海辺", result.Details.Scene);
    }

    /// <summary>
    /// ｸｫーﾄ無しの文字列値は対になる " で囲う
    /// </summary>
    [Theory]
    [InlineData("time_of_day = afternoon")]
    [InlineData("time_of_day = 'afternoon")]
    [InlineData("time_of_day = afternoon'")]
    [InlineData("time_of_day = \"afternoon")]
    public void UnquotedStringIsFixed(string response) {
        var result = new TestCaptionizer().Parse(response);
        Assert.NotNull(result);
        Assert.Equal("afternoon", result.TimeOfDay);
    }

    /// <summary>
    /// 欠落行があってもｷー付き行で位置を同期して、ｷーなし行を正しいｷーに入れる
    /// </summary>
    [Fact]
    public void KeylessLineFollowsKeyedLine() {
        var response = "time_of_day = \"昼\"\ndominant_color_palette = \"青\"\nkeywords = [\"風景\"]\n\"海辺\"\n\"人物なし\"";
        var result = new TestCaptionizer().Parse(response);
        Assert.NotNull(result);
        Assert.Equal("海辺", result.Details.Scene);
        Assert.Equal("人物なし", result.Details.Subjects);
    }
}



/// <summary>
/// Profileを差し替えるﾃｽﾄは並列に走らせない
/// </summary>
[CollectionDefinition("プロファイル", DisableParallelization = true)]
public class ProfileCollection { }

[Collection("プロファイル")]
public class CaptionFailuresController : IDisposable {
    readonly DirectoryInfo temporary = Directory.CreateTempSubdirectory("imoge-caption-");
    readonly string profileKey = Profile.Dictator.ActiveProfile;
    readonly PreparedSettings? previousSettings;

    public CaptionFailuresController() {
        // ﾛｶﾞーがProfile.Activeを参照するので用意しておく
        Profile.Dictator.Settingure.TryGetValue(profileKey, out previousSettings);
        Profile.Dictator.Settingure[profileKey] = new PreparedSettings(new Settings(), [], [], [], [], [], [], temporary);
    }

    public void Dispose() {
        if (previousSettings == null) {
            Profile.Dictator.Settingure.Remove(profileKey);
        }
        else {
            Profile.Dictator.Settingure[profileKey] = previousSettings;
        }
        temporary.Delete(true);
    }

    /// <summary>
    /// 結果を順番に返す偽ｷｬﾌﾟｼｮﾅｲｻﾞー
    /// </summary>
    sealed class FakeCaptionizer(bool verifyOk, params Func<Gachas>[] results) : CaptionizerBase {
        int index;
        public int Calls => index;
        public int Verifies { get; private set; }

        protected override Task<Gachas> PredictOnce(string imagePath, CancellationToken token) =>
            Task.FromResult(results[Math.Min(index++, results.Length - 1)]());

        public override Task Verify(CancellationToken token) {
            Verifies++;
            return verifyOk ? Task.CompletedTask : throw new HttpRequestException("down");
        }
    }

    /// <summary>
    /// ﾛーｶﾙ画像を読むだけの偽ｷｬﾌﾟｼｮﾅｲｻﾞー
    /// </summary>
    sealed class LocalImageCaptionizer : CaptionizerBase {
        public int Calls { get; private set; }
        protected override int RetryCount => 3;

        protected override Task<Gachas> PredictOnce(string imagePath, CancellationToken token) {
            Calls++;
            LoadImageAsBase64(imagePath, 64);
            return Task.FromResult(new Gachas());
        }

        public override Task Verify(CancellationToken token) => throw new HttpRequestException("down");
    }

    [Fact]
    public async Task MissingLocalImageIsContentFailure() {
        var cap = new LocalImageCaptionizer { AbortThreshold = 2 };
        var missing = Path.Combine(temporary.FullName, "missing.png");
        // 移動済みのﾌｧｲﾙはﾘﾄﾗｲせず、何回続いても打ち切らない
        for (int i = 0; i < 5; i++) {
            var e = await Assert.ThrowsAsync<CaptionFailureException>(() => cap.Predict(missing, CancellationToken.None));
            Assert.Equal(CaptionErrorType.Content, e.Typo);
            Assert.False(e.Retryable);
            Assert.IsAssignableFrom<IOException>(e.InnerException);
        }
        Assert.Equal(5, cap.Calls);
    }

    static Gachas Down() => throw new HttpRequestException("接続拒否");
    static Gachas Broken() => throw new EmptyCaptionException();
    static Gachas Ok() => new();

    [Fact]
    public async Task AbortsAfterConsecutiveFailures() {
        var cap = new FakeCaptionizer(false, Down) { AbortThreshold = 3 };
        for (int i = 0; i < 2; i++) {
            var e = await Assert.ThrowsAsync<CaptionFailureException>(() => cap.Predict("a", CancellationToken.None));
            Assert.Equal(CaptionErrorType.Unreachable, e.Typo);
        }
        await Assert.ThrowsAsync<CaptionAbortedException>(() => cap.Predict("a", CancellationToken.None));
        Assert.Equal(1, cap.Verifies);
    }

    [Fact]
    public async Task VerifyRescuesOnlyOnce() {
        var cap = new FakeCaptionizer(true, Down) { AbortThreshold = 2 };
        for (int i = 0; i < 3; i++) {
            await Assert.ThrowsAsync<CaptionFailureException>(() => cap.Predict("a", CancellationToken.None));
        }
        await Assert.ThrowsAsync<CaptionAbortedException>(() => cap.Predict("a", CancellationToken.None));
        Assert.Equal(1, cap.Verifies);
    }

    [Fact]
    public async Task SuccessOrContentFailureResetsCounter() {
        var cap = new FakeCaptionizer(false, Down, Down, Ok, Down, Down, Broken, Down, Down) { AbortThreshold = 3 };
        for (int i = 0; i < 8; i++) {
            try {
                await cap.Predict("a", CancellationToken.None);
            }
            catch (CaptionFailureException) {
            }
        }
        Assert.Equal(8, cap.Calls);
        Assert.Equal(0, cap.Verifies);
    }

    [Fact]
    public async Task DisabledWhenThresholdIsZero() {
        var cap = new FakeCaptionizer(false, Down) { AbortThreshold = 0 };
        for (int i = 0; i < 10; i++) {
            await Assert.ThrowsAsync<CaptionFailureException>(() => cap.Predict("a", CancellationToken.None));
        }
    }
}
