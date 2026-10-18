using ImogeIndex.Captionizers;
using ImogeIndex.Common;
using ImogeIndex.Storage;
using ImogeIndex.Mur;
using ImogeIndex.Web6.Storage;
using System.IO;
using Tomlet;

namespace ImogeIndex.Web6.Dictators;

/// <summary>
/// 詳細ﾍﾟｲﾝ
/// </summary>
/// <param name="texts">表示文言</param>
public sealed class DetailPaneDictator(IImoonePalabras texts)
{
    /// <summary>
    /// ｲﾝﾃﾞｯｸｽの接続はｽﾚｯﾄﾞｾーﾌでないのでｼﾘｱﾙ読み
    /// </summary>
    readonly SemaphoreSlim loadLock = new(1, 1);

    /// <summary>
    /// ﾈﾀ読み込み(ｽﾚﾌﾟで)
    /// </summary>
    /// <param name="fileItem"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<IReadOnlyList<MetadataPair>> LoadMetadataAsync(
        FileItem fileItem,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () => {
            await loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                cancellationToken.ThrowIfCancellationRequested();
                return LoadMetadata(fileItem, cancellationToken);
            }
            finally {
                loadLock.Release();
            }
        }, cancellationToken);
    }

    /// <summary>
    /// ﾈﾀ読み込み
    /// </summary>
    /// <param name="fileItem"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public IReadOnlyList<MetadataPair> LoadMetadata(
        FileItem fileItem,
        CancellationToken cancellationToken)
    {
        var fields = new List<MetadataPair>();
        AddFileFields(fields, fileItem);
        AddCaptionFields(fields, fileItem.Id, cancellationToken);
        AddIndexMetadata(fields, fileItem.Id, cancellationToken);
        return fields;
    }

    /// <summary>
    /// ﾘｽﾄ作成
    /// </summary>
    /// <param name="fields"></param>
    /// <param name="fileItem"></param>
    private void AddFileFields(List<MetadataPair> fields, FileItem fileItem)
    {
        // FileRecordは10万件抱えると重いので、表示に要る分はFileItemに持ってるやつを使う
        var fileInfo = new FileInfo(fileItem.FullName);
        fields.Add(new MetadataPair(texts.DetailLabel(DetailFieldLabel.FileName), fileItem.Name));
        fields.Add(new MetadataPair(texts.DetailLabel(DetailFieldLabel.CreatedAt), fileItem.DateTime.ToString("yyyy/MM/dd HH:mm:ss")));
        if (fileInfo.Exists)
        {
            fields.Add(new MetadataPair(texts.DetailLabel(DetailFieldLabel.Location), fileItem.Path));
            fields.Add(new MetadataPair(texts.DetailLabel(DetailFieldLabel.FileSize), fileInfo.Length.ToReadableSize()));
        }
        fields.Add(new MetadataPair(
            texts.DetailLabel(DetailFieldLabel.Resolution),
            $"{fileItem.Width}x{fileItem.Height}"));
        AddIfPresent(fields, texts.DetailLabel(DetailFieldLabel.Label), fileItem.Label);

        AddIfPresent(fields, nameof(FileRecord.Tags), fileItem.Tag);
        AddIfPresent(fields, nameof(FileRecord.Characters), fileItem.Character);
    }

    /// <summary>
    /// ｷｬﾌﾟｼｮﾝ読み込み
    /// </summary>
    /// <param name="fields"></param>
    /// <param name="fileId"></param>
    /// <param name="cancellationToken"></param>
    private static void AddCaptionFields(
        List<MetadataPair> fields,
        long fileId,
        CancellationToken cancellationToken)
    {
        var caption = IndexStore.Instance.GetCaption(fileId, cancellationToken);
        if (string.IsNullOrEmpty(caption))
        {
            return;
        }

        try
        {
            var document = new TomlParser().Parse(caption);
            var parsedCaption = TomletMain.To<Gachas>(document);
            fields.Add(new MetadataPair(
                nameof(parsedCaption.PeopleCount),
                parsedCaption.PeopleCount.ToString()));
            if (parsedCaption.Keywords.Count != 0)
            {
                fields.Add(new MetadataPair(
                    nameof(parsedCaption.Keywords),
                    string.Join(", ", parsedCaption.Keywords)));
            }
            foreach (var field in parsedCaption.Details.DumpFields())
            {
                fields.Add(new MetadataPair(field.Key, field.Value));
            }
        }
        catch
        {
            // ｷｬﾌﾟｼｮﾝ壊れてるっぽいので無視
        }
    }

    /// <summary>
    /// indexのﾒﾀﾃﾞーﾀﾑ
    /// </summary>
    /// <param name="fields"></param>
    /// <param name="fileId"></param>
    /// <param name="cancellationToken"></param>
    private static void AddIndexMetadata(
        List<MetadataPair> fields,
        long fileId,
        CancellationToken cancellationToken)
    {
        foreach (var metadata in IndexStore.Instance.GetMetadata(fileId, cancellationToken))
        {
            if (string.IsNullOrEmpty(metadata.Value) || string.IsNullOrEmpty(metadata.Tag))
            {
                continue;
            }
            fields.Add(new MetadataPair(metadata.Tag, metadata.Value));
        }
    }

    /// <summary>
    /// もしあったら追加
    /// </summary>
    /// <param name="fields"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    private static void AddIfPresent(List<MetadataPair> fields, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            fields.Add(new MetadataPair(name, value));
        }
    }
}
