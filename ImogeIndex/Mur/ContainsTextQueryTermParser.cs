
namespace ImogeIndex.Mur;

/// <summary>
/// 普通のﾃｷｽﾄ検索
/// </summary>
internal sealed class ContainsTextQueryTermParser : IQueryTermParser
{
    readonly string? prefix;
    readonly SearchField[] fields;

    public ContainsTextQueryTermParser(
        string? prefix,
        SearchField[] fields)
    {
        this.prefix = prefix;
        this.fields = fields;
    }

    public bool TryParse(string token, out IQueryTerm? term)
    {
        if (prefix is not null) {
            if (!token.StartsWith(prefix, StringComparison.Ordinal)) {
                term = null;
                return false;
            }
            token = token[prefix.Length..];
        }
        var value = Unqo(token);
        if (string.IsNullOrWhiteSpace(value)) {
            term = null;
            return true;
        }

        term = new ContainsTextQueryTerm(value, fields);
        return true;
    }

    private static string Unqo(string value)
    {
        if (value.Length < 2) {
            return value;
        }
        return (value[0], value[^1]) switch {
            ('\"', '\"') => value[1..^1],
            ('\'', '\'') => value[1..^1],
            _ => value,
        };
    }

}
