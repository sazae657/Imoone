using ImogeIndex.Common;
using ImogeIndex.Storage;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Tomlet.Attributes;

namespace ImogeIndex.Niraction;

/// <summary>
/// ﾆﾗｸｼｮﾝ設定
/// </summary>
public class NiractionSetting {
    [TomlPrecedingComment("有効/無効")]
    public bool Enable { get; set; }

    [TomlPrecedingComment("ﾆﾗｸｼｮﾝのURL")]
    public string? Host { get; set; } = "http://127.0.0.1:1192";
}

/// <summary>
/// ﾆﾗｸｼｮﾝ値
/// </summary>
public class NiractionValue
{
    public string? Name { get; set; }
    public string[]? Values { get; set; }

    public static NiractionValue Parse(BSVRow? row)
    {
        if (row == null || row.Values == null) {
            return new();
        }
        return new NiractionValue {
            Name = row.Values[0],
            Values = row.Values.Skip(1).ToArray(),
        };
    }
}

/// <summary>
/// ﾆﾗｸｼｮﾝ応答(BSV形式)
/// </summary>
public class NiractionResponse {
    public List<NiractionValue> Values { get; } = new();
}

/// <summary>
/// Glueの中身
/// </summary>
public class NiractionGluePayload
{
    public long Id { get; set; }
    public byte[]? Image { get; set; }
}

/// <summary>
/// Glueのﾚｽﾎﾟﾝｽ
/// </summary>
public class NiractionGlueResponse
{
    public List<NiractionGluePayload> Values { get; } = new();
}

/// <summary>
/// ﾆﾗｸｼｮﾝ接続
/// </summary>
public class NiractionClient {        
    static HttpClient httpClient = new HttpClient {
        Timeout = TimeSpan.FromSeconds(10) // 基本ﾐﾘ秒で返る
    };
    NiractionSetting setty;
    public NiractionClient(NiractionSetting setty) {
        this.setty = setty;
    }

    /// <summary>
    /// Verify送信
    /// </summary>
    /// <param name="record"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<NiractionResponse?> Verify(FileRecord record, CancellationToken token) {
        var url = $"{setty.Host}/Niractions/Verify/{record.FileHash}";
        NiractionResponse? ret = null;
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        try {
            response.EnsureSuccessStatusCode();
            using (var ms = await RecieveRleBSV(response)) {
                ret = ProcessResponse(ms);
            }
            return ret;
        }
        catch (Exception r) {
            Logger.Dictator.Error(r, $"{url}");
            throw;
        }
    }

    /// <summary>
    /// Distance送信
    /// </summary>
    /// <param name="record"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<NiractionResponse?> Distance(FileRecord record, CancellationToken token) {
        var url = $"{setty.Host}/Niractions/Distance";
        var req = new BSVWriter<BSVPair>([
            new BSVPair("P", record.PHash.ToString()),
            new BSVPair("A", record.AHash.ToString()),
            new BSVPair("D", record.DHash.ToString()),
            new BSVPair("W", record.WHash.ToString())
            ]
        );
        using var ins = new MemoryStream();
        req.Save(ins);
        ins.Seek(0, SeekOrigin.Begin);

        using var sess = new MemoryStream();
        Internal.RleBSV.Compress(ins, sess);
        sess.Seek(0, SeekOrigin.Begin);

        using var content = new StreamContent(sess);
        content.Headers.ContentType = null;
        content.Headers.TryAddWithoutValidation("Content-Type", "application/x-rle-bsv");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = content;
        NiractionResponse? ret = null;
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        try {
            response.EnsureSuccessStatusCode();
            CheckHeader(response);
            using (var ms = await RecieveRleBSV(response)) {
                ret = ProcessResponse(ms);
            }
            return ret;
        }
        catch(Exception r) {
            Logger.Dictator.Error(r, $"{url}");
            throw;
        }
    }

    NiractionResponse? ProcessResponse(Stream stream)
    {
        var br = new BSVReader<BSVRow>();
        br.Load(stream);
        var t = new NiractionResponse();
        t.Values.AddRange(br.Values
            .Where(w => w != null)
            .Select(w => NiractionValue.Parse(w)));
        return t;
    }

    void CheckHeader(HttpResponseMessage? massage)
    {
        ArgumentNullException.ThrowIfNull(massage);
        // ﾆﾗｸｼｮﾝはﾁｬﾝｸを返さないのでContent-Lengthは必ず取れる
        ArgumentNullException.ThrowIfNull(massage.Content.Headers.ContentLength);
        if (massage.Content.Headers.ContentLength is { } len && len > 1048576) {
            throw new Exception(Properties.Resources.NIR_E_ResponseTooLarge.Morfat(massage.Content.Headers.ContentLength));
        }
    }

    async Task<Stream> RecieveRleBSV(HttpResponseMessage massage)
    {
        var ms = new MemoryStream();
        CheckHeader(massage);
        Internal.RleBSV.Decompress(await massage.Content.ReadAsStreamAsync(), ms);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    /// <summary>
    /// Glue送信
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<NiractionGlueResponse?> Glue(IEnumerable<long> keys, CancellationToken token)
    {
        var url = $"{setty.Host}/Niractions/Glue";
        var req = new BSVWriter<BSVRow>([
            new BSVRow(keys.Select(w => w.ToString()))
            ]
        );
        using var ins = new MemoryStream();
        req.Save(ins);
        ins.Seek(0, SeekOrigin.Begin);

        using var sess = new MemoryStream();
        Internal.RleBSV.Compress(ins, sess);
        sess.Seek(0, SeekOrigin.Begin);

        using var content = new StreamContent(sess);
        content.Headers.ContentType = null;
        content.Headers.TryAddWithoutValidation("Content-Type", "application/x-rle-bsv");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = content;
        NiractionGlueResponse? ret = null;
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        try {
            response.EnsureSuccessStatusCode();
            CheckHeader(response);
            ret = await ProcessGlueResponse(await response.Content.ReadAsStreamAsync(), token);
            return ret;
        }
        catch (Exception r) {
            Logger.Dictator.Error(r, $"{url}");
            throw;
        }
    }

    /// <summary>
    /// Glueのﾚｽﾎﾟﾝｽを捌く(ﾆﾗｸｼｮﾝはSPARCなのでﾋﾞｯｸﾞｴﾝﾃﾞｨｱﾝ注意)
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    async Task<NiractionGlueResponse> ProcessGlueResponse(Stream stream, CancellationToken token)
    {
        var longBuf = new byte[8];
        stream.ReadExactly(longBuf);
        var count = BinaryPrimitives.ReadInt64BigEndian(longBuf);
        if(count < 0 || count > 126){
            throw new Exception(Properties.Resources.NIR_E_GCount.Morfat(count));
        }
        var hash = new byte[64];
        stream.ReadExactly(hash);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, token);
        ms.Seek(0, SeekOrigin.Begin);
        var hash2 = SHA512.HashData(ms);
        if (!hash2.SequenceEqual(hash)) {
            throw new Exception(Properties.Resources.NIR_E_GHash);
        }
        var rc = new NiractionGlueResponse();
        ms.Seek(0, SeekOrigin.Begin);
        for (long i = 0; i < count; i++) {
            ms.ReadExactly(longBuf);
            var iid = BinaryPrimitives.ReadInt64BigEndian(longBuf);
            if (iid == 0) {
                break; // ここでおわり
            }
            if (iid < 0) {
                throw new Exception(Properties.Resources.NIR_E_GId.Morfat(iid));
            }

            ms.ReadExactly(longBuf);
            var isz = BinaryPrimitives.ReadInt64BigEndian(longBuf);
            var remain = ms.Length - ms.Position;
            if (isz <= 0 || isz > remain) {
                throw new Exception(Properties.Resources.NIR_E_GSize.Morfat(isz));
            }

            var glue = new NiractionGluePayload { Id = iid, Image = new byte[isz] };
            if (ms.Read(glue.Image) != glue.Image.Length) {
                throw new Exception(Properties.Resources.NIR_E_GImage);
            }
            rc.Values.Add(glue);
        }
        return rc;
    }

}
