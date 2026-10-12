using ImogeIndex.Storage;
using ImogeIndex.Zenerators;
using ImogeIndex.Mur;
using System.Reflection;

namespace ImogeIndex.Web2;

/// <summary>
/// ﾒﾀﾃﾞーﾀﾑ検索ｲﾝﾃﾞｯｸｽのﾃｽﾄ
/// </summary>
public class MetadataIndexesController {
    [Fact(DisplayName = "同一エイリアスの複数メタデータムタグを統合")]
    public void MergeOfAlias() {
        var aliases = new[] {
            new MetaAlias { Alias = "make", Tag = "Make" },
            new MetaAlias { Alias = "make", Tag = "Manufacture" },
            new MetaAlias { Alias = "make", Tag = "Vendor" },
        };
        var source = new Dictionary<string, MetadataRecord[]>(StringComparer.OrdinalIgnoreCase) {
            ["Make"] = [new MetadataRecord { ImageId = 10, Value = "Canon" }],
            ["Manufacture"] = [new MetadataRecord { ImageId = 20, Value = " Canon " }],
            ["Vendor"] = [
                new MetadataRecord { ImageId = 10, Value = "CANON" },
                new MetadataRecord { ImageId = 30, Value = "Nikon" },
            ],
        };

        var values = TagIndexProzessor.CollectMetadataValues(aliases, tag => source[tag]);

        Assert.Equal([10L, 20L], values["canon"].Order());
        Assert.Equal([30L], values["nikon"]);
    }

    [Fact(DisplayName = "引用符付き")]
    public void Quoted() {
        var query = new MurQuery();

        query.Parse("meta:model:\"Canon EOS 5D\"");

        Assert.False(query.IsEmpty());
    }

    [Fact(DisplayName = "3-gramで候補を絞って画像IDを取得")]
    public void LookupByTrigram() {
        var index = JailIndex([
            ("canon eos 5d", new long[] { 10, 20 }),
            ("canon powershot", new long[] { 30 }),
            ("nikon f2", new long[] { 40 }),
        ]);
        var destination = new IdSet();

        index.Lookup("canon eos", destination);

        Assert.Equal([10L, 20L], destination.Order());
    }

    [Fact(DisplayName = "三文字未満は指定タグ内の値だけを走査")]
    public void LookupShortValue() {
        var index = JailIndex([
            ("canon eos 5d", new long[] { 10 }),
            ("nikon f2", new long[] { 20 }),
        ]);
        var destination = new IdSet();

        index.Lookup("f2", destination);

        Assert.Equal([20L], destination);
    }

    [Fact(DisplayName = "metaと日付のOR条件は部分的にインデックス使うはず")]
    public void MetaOr() {
        var datum = new ImogeDatum();
        SetMetaIndex(datum, "model", JailIndex([
            ("canon eos 5d", new long[] { 10 }),
        ]));
        var query = new MurQuery();
        query.Parse("meta:model:canon | dc:2025");

        query.PrepareTag(datum, CancellationToken.None);

        Assert.True(query.Evaluate(new FileItem {
            Id = 10,
            DateTime = new DateTime(2024, 1, 1),
        }, CancellationToken.None));
        Assert.True(query.Evaluate(new FileItem {
            Id = 20,
            DateTime = new DateTime(2025, 1, 1),
        }, CancellationToken.None));
        Assert.False(query.Evaluate(new FileItem {
            Id = 30,
            DateTime = new DateTime(2024, 1, 1),
        }, CancellationToken.None));
    }

    [Fact(DisplayName = "meta同士のOR条件は必ずインデックス解決するはず")]
    public void MetaOrMeta() {
        var datum = new ImogeDatum();
        SetMetaIndex(datum, "model", JailIndex([
            ("canon eos 5d", new long[] { 10 }),
            ("nikon f2", new long[] { 20 }),
        ]));
        var query = new MurQuery();
        query.Parse("meta:model:canon | meta:model:nikon");

        query.PrepareTag(datum, CancellationToken.None);

        Assert.True(query.Evaluate(new FileItem { Id = 10 }, CancellationToken.None));
        Assert.True(query.Evaluate(new FileItem { Id = 20 }, CancellationToken.None));
        Assert.False(query.Evaluate(new FileItem { Id = 30 }, CancellationToken.None));
    }

    [Fact(DisplayName = "インデックス解決できない条件同士のORは通常評価に流れろ")]
    public void NonMetaOr() {
        var datum = new ImogeDatum();
        var query = new MurQuery();
        query.Parse("dc:2025 | width:>1000");

        query.PrepareTag(datum, CancellationToken.None);

        Assert.True(query.Evaluate(new FileItem {
            Id = 10,
            DateTime = new DateTime(2025, 1, 1),
            Width = 500,
        }, CancellationToken.None));
        Assert.True(query.Evaluate(new FileItem {
            Id = 20,
            DateTime = new DateTime(2024, 1, 1),
            Width = 1200,
        }, CancellationToken.None));
        Assert.False(query.Evaluate(new FileItem {
            Id = 30,
            DateTime = new DateTime(2024, 1, 1),
            Width = 500,
        }, CancellationToken.None));
    }

    [Fact(DisplayName = "複数metaと通常条件の混合OR")]
    public void MetaVSMetaVsOtherOr() {
        var datum = new ImogeDatum();
        SetMetaIndex(datum, "model", JailIndex([
            ("canon eos 5d", new long[] { 10 }),
            ("nikon f2", new long[] { 20 }),
        ]));
        var query = new MurQuery();
        query.Parse("meta:model:canon | dc:2025 | meta:model:nikon");

        query.PrepareTag(datum, CancellationToken.None);

        Assert.True(query.Evaluate(new FileItem {
            Id = 10,
            DateTime = new DateTime(2024, 1, 1),
        }, CancellationToken.None));
        Assert.True(query.Evaluate(new FileItem {
            Id = 20,
            DateTime = new DateTime(2024, 1, 1),
        }, CancellationToken.None));
        Assert.True(query.Evaluate(new FileItem {
            Id = 30,
            DateTime = new DateTime(2025, 1, 1),
        }, CancellationToken.None));
        Assert.False(query.Evaluate(new FileItem {
            Id = 40,
            DateTime = new DateTime(2024, 1, 1),
        }, CancellationToken.None));
    }

    [Fact(DisplayName = "単独の否定条件はインデックスで除外する")]
    public void NegatedMetaIsExcludedById() {
        var datum = new ImogeDatum();
        SetMetaIndex(datum, "model", JailIndex([
            ("canon eos 5d", new long[] { 10 }),
            ("nikon f2", new long[] { 20 }),
        ]));
        var query = new MurQuery();
        query.Parse("-meta:model:canon");

        query.PrepareTag(datum, CancellationToken.None);

        // ｲﾝﾃﾞｯｸｽで除外されるので項目側のﾒﾀﾃﾞーﾀﾑが空でも一致しない
        Assert.False(query.Evaluate(new FileItem { Id = 10 }, CancellationToken.None));
        Assert.True(query.Evaluate(new FileItem { Id = 20 }, CancellationToken.None));
        Assert.True(query.Evaluate(new FileItem { Id = 30 }, CancellationToken.None));
    }

    [Fact(DisplayName = "肯定条件と否定条件のインデックス解決")]
    public void MetaAndNegatedMeta() {
        var datum = new ImogeDatum();
        SetMetaIndex(datum, "model", JailIndex([
            ("canon eos 5d", new long[] { 10, 11 }),
            ("canon eos r5", new long[] { 11 }),
        ]));
        var query = new MurQuery();
        query.Parse("meta:model:canon -meta:model:r5");

        query.PrepareTag(datum, CancellationToken.None);

        Assert.True(query.Evaluate(new FileItem { Id = 10 }, CancellationToken.None));
        Assert.False(query.Evaluate(new FileItem { Id = 11 }, CancellationToken.None));
        Assert.False(query.Evaluate(new FileItem { Id = 20 }, CancellationToken.None));
    }

    /// <summary>
    /// 仮ｲﾝﾃﾞｯｸｽの作成
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    private static NgramInvertedIndex JailIndex(
        IReadOnlyList<(string Value, long[] Ids)> source) {
        const string tag = "model";
        var data = new SearchIndexDataum();
        var field = SearchIndexDataum.MetadataField(tag);
        data.AddField(field, source.Select(x =>
            new KeyValuePair<string, HashSet<long>>(x.Value, x.Ids.ToHashSet())),
            40, 0, CancellationToken.None);
        return NgramInvertedIndex.Sanitize(data, 40, 0, CancellationToken.None)[field];
    }

    /// <summary>
    /// ﾃｽﾄ対象へﾒﾀﾃﾞーﾀﾑｲﾝﾃﾞｯｸｽを設定
    /// </summary>
    /// <param name="datum"></param>
    /// <param name="tag"></param>
    /// <param name="index"></param>
    private static void SetMetaIndex(
        ImogeDatum datum, string tag, NgramInvertedIndex index) {
        var field = typeof(ImogeDatum).GetField(
            "current",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var generation = field.GetValue(datum)!;
        var built = generation.GetType().GetProperty("Indices")!.GetValue(generation)!;
        var indices = Assert.IsType<Dictionary<string, NgramInvertedIndex>>(
            built.GetType().GetProperty("Metadata")!.GetValue(built));
        indices[tag] = index;
    }
}
