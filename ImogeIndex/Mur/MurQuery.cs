using System.Text.RegularExpressions;

namespace ImogeIndex.Mur;

/// <summary>
/// MURのﾊﾟーｽと評価
/// 空白で区切られた条件はAND、|で区切られた条件はOR、-を前置した条件はNOT
/// </summary>
public partial class MurQuery {
    /// <summary>
    /// 転置ｲﾝﾃﾞｯｸｽで絞り込んだ候補ID
    /// </summary>
    public IdSet MatchId { get; } = new();

    // TODO: なんか格好悪いので格好いい定義方法考える
    const string TagPrefix = "tag:";
    const string KeywordPrefix = "kwd:";
    const string CharacterPrefix = "character:";
    const string CostumePrefix = "costume:";
    const string ScenePrefix = "scene:";
    const string ObjectPrefix = "object:";
    const string WidthPrefix = "width:";
    const string HeightPrefix = "height:";

    [GeneratedRegex(@"(?<or>\|)|(?<neg>-)?(?<value>meta:[^:\s\""|]+:(?:\""[^\""]*\""|[^\s\""|]+)|\w+:(?:\""[^\""]*\""|[^\s\""|]+)|\""[^\""]*\""|[^\s\""|-][^\s\""|]*)")]
    private static partial Regex TokenPattern();

    /// <summary>
    /// ｺﾞﾙーﾌﾟ
    /// </summary>
    readonly List<QueryGroup> gorupes = new();

    /// <summary>
    /// 転置ｲﾝﾃﾞｯｸｽで解決できず、文字列走査が要るｺﾞﾙーﾌﾟ
    /// </summary>
    readonly List<QueryGroup> residualGorupe = new();

    /// <summary>
    /// MatchId による絞り込みが有効か
    /// </summary>
    bool useMatchId;

    /// <summary>
    /// 否定条件を転置ｲﾝﾃﾞｯｸｽで解決した除外ID
    /// </summary>
    readonly IdSet excludeId = new();

    readonly IdSet resolveGorupen = new();
    readonly IReadOnlyList<IQueryTermParser> gachasControllers;

    // ﾊﾟーｻーは状態を持たないので、ｴｲﾘｱｽ設定が変わらない限り使い回す
    static readonly object parserLock = new();
    static (string Alias, MetadataValueType ValueType)[]? cachedAliasKey;
    static IReadOnlyList<IQueryTermParser>? cachedParsers;

    public MurQuery(IEnumerable<MetaAlias>? metadataAliases = null) {
        metadataAliases ??= Profile.Dictator.Settingure.TryGetValue(
            Profile.Dictator.ActiveProfile, out var settings)
            ? settings.MetadataSearchTags : SystemParams.DefaultMetaAliases;
        gachasControllers = GetParsers(metadataAliases);
    }

    /// <summary>
    /// ﾊﾟーｻー一式を取得(ｲﾝｸﾘﾒﾝﾀﾙ検索で毎回作り直さない用)
    /// </summary>
    /// <param name="metadataAliases"></param>
    /// <returns></returns>
    private static IReadOnlyList<IQueryTermParser> GetParsers(IEnumerable<MetaAlias> metadataAliases) {
        var configured = metadataAliases.ToArray();
        // MetaAliasは書き換え可能なので参照ではなく中身の写しで比べる
        var key = configured.Select(x => (x.Alias, x.ValueType)).ToArray();
        lock (parserLock) {
            if (cachedParsers is not null && cachedAliasKey.AsSpan().SequenceEqual(key)) {
                return cachedParsers;
            }
        }
        // 設定不正なら例外になるのでｷｬｯｼｭしない
        IReadOnlyList<IQueryTermParser> parsers = [
            new MetadataQueryTermParser(configured),
            new DateQueryTermParser(),
            new NumberQueryTermParser(WidthPrefix, static item => item.Width),
            new NumberQueryTermParser(HeightPrefix, static item => item.Height),
            new ContainsTextQueryTermParser(TagPrefix, [SearchField.Tag]),
            new ContainsTextQueryTermParser(KeywordPrefix, [SearchField.Keyword]),
            new ContainsTextQueryTermParser(CharacterPrefix, [SearchField.Character]),
            new ContainsTextQueryTermParser(CostumePrefix, [SearchField.Costume]),
            new ContainsTextQueryTermParser(ScenePrefix, [SearchField.Scene]),
            new ContainsTextQueryTermParser(ObjectPrefix, [SearchField.Objects]),
            new ContainsTextQueryTermParser(null, [SearchField.Name, SearchField.Path]),
        ];
        lock (parserLock) {
            cachedAliasKey = key;
            cachedParsers = parsers;
        }
        return parsers;
    }

    public void Parse(string filterText) {
        gorupes.Clear();
        residualGorupe.Clear();
        MatchId.Clear();
        excludeId.Clear();
        useMatchId = false;

        if (string.IsNullOrWhiteSpace(filterText)) {
            return;
        }

        QueryGroup? cwd = null;
        var joinWithOr = false;
        foreach (Match match in TokenPattern().Matches(filterText)) {
            if (match.Groups["or"].Success) {
                joinWithOr = cwd is not null;
                continue;
            }

            var term = ParseTerm(match.Groups["value"].Value);
            if (term is not null && match.Groups["neg"].Success) {
                term = NotQueryTerm.Sanitize(term);
            }
            if (term is null) {
                // 空の条件(入力途中のtag:など)は無視するが、直前の|まで後続の条件に繋げない
                // (繋げると 奈 | tag: 良 が 奈 OR 良 になって結果が広がる)
                joinWithOr = false;
                continue;
            }

            if (!joinWithOr || cwd is null) {
                cwd = new QueryGroup();
                gorupes.Add(cwd);
            }
            cwd.Add(term);
            joinWithOr = false;
        }

        // ANDは順序を問わないので、安いｸﾞﾙーﾌﾟから評価して短絡させる
        gorupes.Sort(static (a, b) => a.Cost.CompareTo(b.Cost));
        foreach (var group in gorupes) {
            group.SortByCost();
        }
        residualGorupe.AddRange(gorupes);
    }

    private IQueryTerm? ParseTerm(string token) {
        foreach (var parser in gachasControllers) {
            if (parser.TryParse(token, out var term)) {
                return term;
            }
        }
        return null;
    }


    /// <summary>
    /// 転置ｲﾝﾃﾞｯｸｽで引ける条件を先に解決して候補IDを絞る
    /// </summary>
    /// <param name="imoge"></param>
    /// <param name="token"></param>
    public void PrepareTag(ImogeDatum imoge, CancellationToken token) {
        MatchId.Clear();
        excludeId.Clear();
        residualGorupe.Clear();
        useMatchId = false;

        foreach (var group in gorupes) {
            if (token.IsCancellationRequested) {
                // 割り込まれたので全文字列評価に倒す
                MatchId.Clear();
                excludeId.Clear();
                residualGorupe.Clear();
                foreach (var resetGroup in gorupes) {
                    resetGroup.ResetResolution();
                }
                residualGorupe.AddRange(gorupes);
                useMatchId = false;
                return;
            }

            resolveGorupen.Clear();
            if (group.SoleNegation is { } negation) {
                // 否定条件だけのｸﾞﾙーﾌﾟは中身を解決して除外IDに回す(母集合が要らない)
                if (negation.Inner.TryResolveIds(imoge, resolveGorupen)) {
                    excludeId.UnionWith(resolveGorupen);
                }
                else {
                    residualGorupe.Add(group);
                }
                continue;
            }
            if (!group.TryResolveIds(imoge, resolveGorupen)) {
                residualGorupe.Add(group);
                continue;
            }

            if (!useMatchId) {
                MatchId.UnionWith(resolveGorupen);
                useMatchId = true;
            }
            else {
                // ｸﾞﾙーﾌﾟ間はANDなので積を取る
                MatchId.IntersectWith(resolveGorupen);
            }
            if (MatchId.IsEmpty) {
                break;
            }
        }
        resolveGorupen.Clear();
    }

    /// <summary>
    /// 空？
    /// </summary>
    /// <returns></returns>
    public bool IsEmpty() => gorupes.Count == 0;

    /// <summary>
    /// PrepareTag後、文字列走査が要る条件が残ってる？
    /// 残ってなければEvaluateはID照合だけなので並列化するほどでもない
    /// </summary>
    public bool RequiresTextScan => residualGorupe.Count != 0 || !useMatchId;


    /// <summary>
    /// このｸｴﾘーの結果がpreviousの結果に必ず含まれるか
    /// </summary>
    /// <param name="previous"></param>
    /// <returns></returns>
    public bool IsNarrowerThan(MurQuery previous) {
        for (var i = 0; i < previous.gorupes.Count; i++) {
            var covered = false;
            for (var j = 0; j < gorupes.Count; j++) {
                if (gorupes[j].IsNarrowerThan(previous.gorupes[i])) {
                    covered = true;
                    break;
                }
            }
            if (!covered) {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 項目の判定
    /// </summary>
    /// <param name="item"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public bool Evaluate(FileItem item, CancellationToken token) {
        // ｲﾝﾃﾞｯｸｽで解決済みの条件はIDで当たってるはず
        if (useMatchId && !MatchId.Contains(item.Id)) {
            return false;
        }
        if (excludeId.Contains(item.Id)) {
            return false;
        }
        // ｲﾝﾃﾞｯｸｽで解決済みでないﾔﾂだけ文字列で
        for (var i = 0; i < residualGorupe.Count; i++) {
            if (!residualGorupe[i].IsMatch(item)) {
                return false;
            }
        }
        return true;
    }

}
