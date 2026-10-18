using ImogeIndex.Common;
using System.Data;
using System.Data.SQLite;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace ImogeIndex.Storage;

/// <summary>
/// ｽｷーﾏの対応
/// </summary>
public class MapOfSchema
{
    public required string Name { get; set; }
    public int Column { get; set; } = -1;
    public int Obzekt { get; set; } = -1;
}

/// <summary>
/// ﾌﾟﾛﾊﾟﾃｨとｶﾗﾑの対応(ﾘﾌﾚｸｼｮﾝは遅いので事前に引いておく)
/// </summary>
public sealed class ColumnBinding
{
    public required PropertyInfo Property { get; init; }
    public required MurColumnAttribute Column { get; init; }

    /// <summary>
    /// ﾊﾞｲﾝﾄﾞ変数名(@付き)
    /// </summary>
    public required string ParameterName { get; init; }

    /// <summary>
    /// 値の取り出し(事前ｺﾝﾊﾟｲﾙ済み)
    /// </summary>
    public required Func<object?, object?> Getter { get; init; }

    /// <summary>
    /// 空文字列をNULLとして書く？
    /// </summary>
    public bool EmptyStringAsNull { get; init; }

    /// <summary>
    /// DateTimeをUNIX秒にして書く？
    /// </summary>
    public bool IsDateTime { get; init; }

    /// <summary>
    /// ﾌﾟﾛﾊﾟﾃｨから作る
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static ColumnBinding Sanitize(PropertyInfo property)
    {
        var column = property.GetCustomAttribute<MurColumnAttribute>()
            ?? throw new Exception(Properties.Resources.SM_E_GetAttributeFailed.Morfat(nameof(MurColumnAttribute)));
        var target = Expression.Parameter(typeof(object), "target");
        var body = Expression.Convert(
            Expression.Property(Expression.Convert(target, property.DeclaringType!), property),
            typeof(object));
        return new ColumnBinding {
            Property = property,
            Column = column,
            ParameterName = $"@{column.Name}",
            Getter = Expression.Lambda<Func<object?, object?>>(body, target).Compile(),
            EmptyStringAsNull = property.PropertyType == typeof(string) && !column.Flags.HasFlag(MurColumnFlags.NotNull),
            IsDateTime = property.PropertyType == typeof(DateTime),
        };
    }
}

/// <summary>
/// 非常に都合のよいｽｷーﾏ
/// </summary>
public class RupSchema
{
    /// <summary>
    /// ｶﾗﾑ名とﾌｨーﾙﾄﾞの対応
    /// </summary>
    public List<MapOfSchema> Schemas { get; } = new();

    /// <summary>
    /// ﾊﾟﾁｨ
    /// </summary>
    public List<PropertyInfo> Properties { get; } = new();

    /// <summary>
    /// Propertiesと同じ並びのｶﾗﾑ対応
    /// </summary>
    public List<ColumnBinding> Bindings { get; } = new();

    /// <summary>
    /// 事前ｺﾝﾊﾟｲﾙ済みﾏｯﾊﾟー
    /// </summary>
    public Delegate? CompiledMapper { get; set; }
}

/// <summary>
/// ﾃﾞーﾀﾑｽﾄｱのﾍﾞーｽ
/// </summary>
public abstract class DataStoreBase : IDisposable
{
    /// <summary>
    /// ﾎﾞｩﾙ (Disposeｻﾎﾞるとﾃﾞｯﾄﾞﾛｯｸする)
    /// </summary>
    protected readonly struct ReaderBowl : IDisposable
    {
        readonly DataStoreBase owner;

        public SQLiteConnection Connection { get; }

        internal ReaderBowl(DataStoreBase owner, SQLiteConnection connection)
        {
            this.owner = owner;
            Connection = connection;
        }

        public void Dispose() => owner.ReturnReader(Connection);
    }

    /// <summary>
    /// 手元に置いておく読み取り接続の上限
    /// </summary>
    const int MaxIdleReaders = 4;

    /// <summary>
    /// 書き込み専用の接続(writeToiletの中でしか触らないこと)
    /// </summary>
    SQLiteConnection? writer;

    /// <summary>
    /// 書き込み接続の排他
    /// </summary>
    readonly object writeToilet = new();

    /// <summary>
    /// 返却された読み取り接続
    /// </summary>
    readonly Stack<SQLiteConnection> idleReaders = new();

    /// <summary>
    /// 読み取り接続の排他
    /// </summary>
    readonly object readerToilet = new();

    /// <summary>
    /// 読み取り接続の接続文字列
    /// </summary>
    string? readerConnectionString;

    /// <summary>
    /// 閉じた後？
    /// </summary>
    volatile bool closed = true;

    /// <summary>
    /// 型とご都合ｽｷーﾏの対応
    /// </summary>
    protected Dictionary<Type, RupSchema> recordTypeMap = new();
    private bool disposedValue;

    bool wasWritable = false;

    /// <summary>
    /// 書き込み許可してる？
    /// </summary>
    public bool IsWritable => wasWritable;

    /// <summary>
    /// 開いてる？
    /// </summary>
    public bool IsConnected => !closed;

    /// <summary>
    /// 書き込める状態？
    /// </summary>
    protected bool CanWrite => IsWritable && !closed;

    /// <summary>
    /// 開いた後の処理
    /// </summary>
    protected virtual void Sanitize() { }

    /// <summary>
    /// Vacuum
    /// </summary>
    /// <param name="force"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual Task Vacuum(bool force, CancellationToken token) => Task.CompletedTask;

    /// <summary>
    /// 書き込み接続でVACUUMする
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    protected Task RunVacuum(CancellationToken token) => Task.Run(() => {
        WithWriter(w => {
            using var cmd = new SQLiteCommand("VACUUM;", w);
            return cmd.ExecuteNonQuery();
        });
    }, token);

    /// <summary>
    /// 開く
    /// </summary>
    /// <param name="path"></param>
    /// <param name="writable"></param>
    protected void OpenConnection(FileInfo path, bool writable)
    {
        wasWritable = writable;
        // 読み取りは操作ごとに別接続を使うので、書き込み接続とは常に分ける
        readerConnectionString = new SQLiteConnectionStringBuilder {
            DataSource = path.FullName,
            ReadOnly = true,
            FailIfMissing = true,
            BusyTimeout = 5000,
        }.ToString();
        lock (readerToilet) {
            closed = false;
        }
        try {
            if (writable) {
                var w = new SQLiteConnection(new SQLiteConnectionStringBuilder {
                    DataSource = path.FullName,
                    BusyTimeout = 5000,
                }.ToString());
                try {
                    w.Open();
                    ExecutePragmas(w, [
                        "PRAGMA journal_mode = WAL;",
                        "PRAGMA synchronous  = NORMAL;",
                        "PRAGMA mmap_size    = 268435456;",
                        "PRAGMA cache_size   = -64000;"
                        ]);
                }
                catch {
                    w.Dispose();
                    throw;
                }
                lock (writeToilet) {
                    writer = w;
                }
            }
            Sanitize();
        }
        catch {
            CloseConnection();
            throw;
        }
    }

    /// <summary>
    /// PRAGMAをまとめて流す
    /// </summary>
    /// <param name="target"></param>
    /// <param name="pragmas"></param>
    static void ExecutePragmas(SQLiteConnection target, string[] pragmas)
    {
        using var cmd = new SQLiteCommand(target);
        foreach (var g in pragmas) {
            cmd.CommandText = g;
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// とじこ
    /// </summary>
    protected void CloseConnection()
    {
        SQLiteConnection[] idles;
        lock (readerToilet) {
            closed = true;
            idles = idleReaders.ToArray();
            idleReaders.Clear();
        }
        foreach (var c in idles) {
            c.Dispose();
        }
        lock (writeToilet) {
            writer?.Dispose();
            writer = null;
        }
    }

    /// <summary>
    /// 読み取り接続の払い出し
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected ReaderBowl JustawayReader()
    {
        SQLiteConnection? c = null;
        lock (readerToilet) {
            if (closed || readerConnectionString == null) {
                throw new InvalidOperationException(Properties.Resources.SM_E_DatumNotOpen);
            }
            idleReaders.TryPop(out c);
        }
        if (c == null) {
            c = new SQLiteConnection(readerConnectionString);
            try {
                c.Open();
                ExecutePragmas(c, ["PRAGMA mmap_size = 268435456;"]);
            }
            catch {
                c.Dispose();
                throw;
            }
        }
        return new ReaderBowl(this, c);
    }

    /// <summary>
    /// 読み取り接続を返す
    /// </summary>
    /// <param name="c"></param>
    void ReturnReader(SQLiteConnection c)
    {
        lock (readerToilet) {
            // 閉じた後と、溢れた分は捨てる
            if (!closed && idleReaders.Count < MaxIdleReaders && c.State == ConnectionState.Open) {
                idleReaders.Push(c);
                return;
            }
        }
        c.Dispose();
    }

    /// <summary>
    /// 書き込み接続を握って処理する
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="func"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected TResult WithWriter<TResult>(Func<SQLiteConnection, TResult> func)
    {
        lock (writeToilet) {
            if (writer == null || !IsWritable) {
                throw new InvalidOperationException(Properties.Resources.SM_E_NotOpenOrReadOnly);
            }
            return func(writer);
        }
    }

    /// <summary>
    /// user_version取得
    /// </summary>
    /// <returns></returns>
    protected static long GetUserVersion(SQLiteConnection target, SQLiteTransaction? transaction = null) {
        using var cmd = new SQLiteCommand(target) { Transaction = transaction };
        cmd.CommandText = "PRAGMA user_version";
        return (long)cmd.ExecuteScalar();
    }

    /// <summary>
    /// user_version設定
    /// </summary>
    /// <param name="ver"></param>
    /// <param name="transaction"></param>
    protected static void SetUserVersion(long ver, SQLiteTransaction transaction) {
        using var cmd = new SQLiteCommand(transaction.Connection) { Transaction = transaction };
        cmd.CommandText = $"PRAGMA user_version = {ver}";
        cmd.ExecuteNonQuery();
    }


    /// <summary>
    /// ﾗﾝｻﾞｸｼｮﾝ
    /// </summary>
    /// <param name="transaction"></param>
    public delegate void SpartanTransactionDelegaty(SQLiteTransaction transaction);

    /// <summary>
    /// ﾄﾗﾝｻﾞｸｼｮﾝ張るだけの凄いﾒｿｯﾄﾞ
    /// </summary>
    /// <param name="delegaty"></param>
    /// <param name="token"></param>
    /// <returns>ｺﾐｯﾄしたらtrue</returns>
    /// <exception cref="Exception"></exception>
    public bool TransactionOf(SpartanTransactionDelegaty delegaty, CancellationToken token)
    {
        if (token.IsCancellationRequested) {
            return false;
        }
        return WithWriter(w => {
            using var transaction = w.BeginTransaction();
            try {
                delegaty(transaction);
                if (token.IsCancellationRequested) {
                    transaction.Rollback();
                    return false;
                }
                transaction.Commit();
                return true;
            }
            catch {
                transaction.Rollback();
                throw;
            }
        });
    }

    /// <summary>
    /// ﾄﾗﾝｻﾞｸｼｮﾝ張るだけの凄いﾒｿｯﾄﾞ読み取りVer
    /// </summary>
    /// <param name="delegaty"></param>
    /// <param name="token"></param>
    public void ReadTransactionOf(SpartanTransactionDelegaty delegaty, CancellationToken token)
    {
        if (token.IsCancellationRequested) {
            return;
        }
        using var justify = JustawayReader();
        using var transaction = justify.Connection.BeginTransaction(IsolationLevel.ReadCommitted);
        delegaty(transaction);
        transaction.Commit();
    }

    /// <summary>
    /// 豪華ﾄﾗﾝｻﾞｸｼｮﾝ
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="transaction"></param>
    public delegate void LuxuryTransactionDelegaty(SQLiteCommand cmd, SQLiteTransaction transaction);

    /// <summary>
    /// 豪華ﾄﾗﾝｻﾞｸｼｮﾝ
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="delegaty"></param>
    /// <param name="token"></param>
    /// <returns>ｺﾐｯﾄしたらtrue</returns>
    /// <exception cref="Exception"></exception>
    public bool TransactionOf(string sql, LuxuryTransactionDelegaty delegaty, CancellationToken token)
    {
        return TransactionOf(transaction => {
            using var cmd = new SQLiteCommand(sql, transaction.Connection, transaction);
            delegaty(cmd, transaction);
        }, token);
    }

    /// <summary>
    /// Tからﾃーﾌﾞﾙ名を獲る
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public string TableOf<T>()
    {
        return TableOf(typeof(T));
    }

    /// <summary>
    /// Typeからﾃーﾌﾞﾙ名を取る
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public string TableOf(Type t)
    {
        var a = t.GetCustomAttribute<MurTableAttribute>();
        if (a == null) {
            throw new Exception(Properties.Resources.SM_E_GetAttributeFailed.Morfat(nameof(MurTableAttribute)));
        }
        return a.Name;
    }

    /// <summary>
    /// readerからｵﾌﾞｼﾞｪｸﾄ生成
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="reader"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    protected T MapInstance<T>(SQLiteDataReader reader) where T : new()
    {
        if (!recordTypeMap.TryGetValue(typeof(T), out var prop)) {
            throw new Exception(Properties.Resources.SM_E_NotInTypeMap.Morfat(typeof(T).Name));
        }
        return MapInstance<T>(reader, prop);
    }

    /// <summary>
    /// readerからｵﾌﾞｼﾞｪｸﾄ生成
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="reader"></param>
    /// <param name="prop"></param>
    /// <returns></returns>
    protected T MapInstance<T>(SQLiteDataReader reader, RupSchema prop) where T : new()
    {
        if (prop.CompiledMapper is Func<SQLiteDataReader, T> mapper) {
            return mapper(reader);
        }
        var compiled = CompileMapper<T>(prop);
        prop.CompiledMapper = compiled;
        return compiled(reader);
    }

    /// <summary>
    /// ﾘﾌﾚｸｼｮﾝ遅いので事前ｺﾝﾊﾟｲﾙしとく
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="prop"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private static Func<SQLiteDataReader, T> CompileMapper<T>(RupSchema prop) where T : new()
    {
        var readerParam = Expression.Parameter(typeof(SQLiteDataReader), "reader");
        var bindings = new List<MemberBinding>();

        for (int i = 0; i < prop.Properties.Count; i++) {
            var property = prop.Properties[i];
            var schema = prop.Schemas[i];
            if (schema == null || schema.Column < 0) {
                continue;
            }

            var colExpr = Expression.Constant(schema.Column, typeof(int));
            var propertyType = property.PropertyType;
            Expression valueExpr;

            if (propertyType == typeof(byte[])) {
                var isDbNull = Expression.Call(readerParam, nameof(SQLiteDataReader.IsDBNull), Type.EmptyTypes, colExpr);
                var readBlobMethod = typeof(DataStoreBase).GetMethod(nameof(ReadBlob), BindingFlags.NonPublic | BindingFlags.Static)!;
                var readBlobCall = Expression.Call(readBlobMethod, readerParam, colExpr);
                valueExpr = Expression.Condition(isDbNull, Expression.Constant(null, typeof(byte[])), readBlobCall);
            }
            else if (propertyType.IsEnum) {
                var readEnumMethod = typeof(DataStoreBase).GetMethod(nameof(ReadEnum), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(propertyType);
                valueExpr = Expression.Call(readEnumMethod, readerParam, colExpr);
            }
            else if (propertyType == typeof(string)) {
                var isDbNull = Expression.Call(readerParam, nameof(SQLiteDataReader.IsDBNull), Type.EmptyTypes, colExpr);
                var getStringCall = Expression.Call(readerParam, nameof(SQLiteDataReader.GetString), Type.EmptyTypes, colExpr);
                valueExpr = Expression.Condition(isDbNull, Expression.Constant(null, typeof(string)), getStringCall);
            }
            else if (propertyType == typeof(int)) {
                var isDbNull = Expression.Call(readerParam, nameof(SQLiteDataReader.IsDBNull), Type.EmptyTypes, colExpr);
                var getIntCall = Expression.Call(readerParam, nameof(SQLiteDataReader.GetInt32), Type.EmptyTypes, colExpr);
                valueExpr = Expression.Condition(isDbNull, Expression.Constant(0, typeof(int)), getIntCall);
            }
            else if (propertyType == typeof(uint)) {
                // int.MaxValueを超える値はInt64で保存しているので64bitで読む
                var isDbNull = Expression.Call(readerParam, nameof(SQLiteDataReader.IsDBNull), Type.EmptyTypes, colExpr);
                var getIntCall = Expression.Call(readerParam, nameof(SQLiteDataReader.GetInt64), Type.EmptyTypes, colExpr);
                var castExpr = Expression.Convert(getIntCall, typeof(uint));
                valueExpr = Expression.Condition(isDbNull, Expression.Constant(0u, typeof(uint)), castExpr);
            }
            else if (propertyType == typeof(long)) {
                var isDbNull = Expression.Call(readerParam, nameof(SQLiteDataReader.IsDBNull), Type.EmptyTypes, colExpr);
                var getLongCall = Expression.Call(readerParam, nameof(SQLiteDataReader.GetInt64), Type.EmptyTypes, colExpr);
                valueExpr = Expression.Condition(isDbNull, Expression.Constant(0L, typeof(long)), getLongCall);
            }
            else if (propertyType == typeof(ulong)) {
                var isDbNull = Expression.Call(readerParam, nameof(SQLiteDataReader.IsDBNull), Type.EmptyTypes, colExpr);
                var getLongCall = Expression.Call(readerParam, nameof(SQLiteDataReader.GetInt64), Type.EmptyTypes, colExpr);
                var castExpr = Expression.Convert(getLongCall, typeof(ulong));
                valueExpr = Expression.Condition(isDbNull, Expression.Constant(0UL, typeof(ulong)), castExpr);
            }
            else if (propertyType == typeof(float)) {
                var isDbNull = Expression.Call(readerParam, nameof(SQLiteDataReader.IsDBNull), Type.EmptyTypes, colExpr);
                var getFloatCall = Expression.Call(readerParam, nameof(SQLiteDataReader.GetFloat), Type.EmptyTypes, colExpr);
                valueExpr = Expression.Condition(isDbNull, Expression.Constant(0f, typeof(float)), getFloatCall);
            }
            else if (propertyType == typeof(double?)) {
                var isDbNull = Expression.Call(readerParam, nameof(SQLiteDataReader.IsDBNull), Type.EmptyTypes, colExpr);
                var getDoubleCall = Expression.Call(readerParam, nameof(SQLiteDataReader.GetDouble), Type.EmptyTypes, colExpr);
                valueExpr = Expression.Condition(isDbNull, Expression.Constant(null, typeof(double?)),
                    Expression.Convert(getDoubleCall, typeof(double?)));
            }
            else if (propertyType == typeof(double)) {
                var isDbNull = Expression.Call(readerParam, nameof(SQLiteDataReader.IsDBNull), Type.EmptyTypes, colExpr);
                var getDoubleCall = Expression.Call(readerParam, nameof(SQLiteDataReader.GetDouble), Type.EmptyTypes, colExpr);
                valueExpr = Expression.Condition(isDbNull, Expression.Constant(0.0, typeof(double)), getDoubleCall);
            }
            else if (propertyType == typeof(bool)) {
                var isDbNull = Expression.Call(readerParam, nameof(SQLiteDataReader.IsDBNull), Type.EmptyTypes, colExpr);
                var getBoolCall = Expression.Call(readerParam, nameof(SQLiteDataReader.GetBoolean), Type.EmptyTypes, colExpr);
                valueExpr = Expression.Condition(isDbNull, Expression.Constant(false, typeof(bool)), getBoolCall);
            }
            else if (propertyType == typeof(DateTime)) {
                var readDateTimeMethod = typeof(DataStoreBase).GetMethod(nameof(ReadDateTime), BindingFlags.NonPublic | BindingFlags.Static)!;
                valueExpr = Expression.Call(readDateTimeMethod, readerParam, colExpr);
            }
            else {
                throw new Exception(Properties.Resources.SM_E_UnknownBindValueType.Morfat(propertyType));
            }

            bindings.Add(Expression.Bind(property, valueExpr));
        }

        var memberInit = Expression.MemberInit(Expression.New(typeof(T)), bindings);
        var lambda = Expression.Lambda<Func<SQLiteDataReader, T>>(memberInit, readerParam);
        return lambda.Compile();
    }

    /// <summary>
    /// blob読み込み
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    protected static byte[]? ReadBlob(SQLiteDataReader reader, int column)
    {
        if (reader.IsDBNull(column)) {
            return null;
        }
        long dataSize = reader.GetBytes(column, 0, null, 0, 0);
        byte[] blobData = new byte[dataSize];
        reader.GetBytes(column, 0, blobData, 0, (int)dataSize);
        return blobData;
    }

    /// <summary>
    /// 日付読み込み(unixtime→DateTime)
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    private static DateTime ReadDateTime(SQLiteDataReader reader, int column)
    {
        if (reader.IsDBNull(column)) {
            return default;
        }
        return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(column)).LocalDateTime;
    }

    /// <summary>
    /// enum読み込み
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="reader"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    protected static TEnum ReadEnum<TEnum>(SQLiteDataReader reader, int column) where TEnum : struct
    {
        if (reader.IsDBNull(column)) {
            return default;
        }
        var es = reader.GetString(column);
        if (!Enum.TryParse<TEnum>(es, true, out var mu)) {
            throw new Exception(Properties.Resources.SM_E_EnumParseFailed.Morfat(typeof(TEnum).Name,es));
        }
        return mu;
    }

    /// <summary>
    /// null対策付き文字列読み込み
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    protected string? RetriveString(SQLiteDataReader reader, int index)
    {
        if (reader.IsDBNull(index)) {
            return null;
        }
        return reader.GetString(index);
    }

    /// <summary>
    /// Tからﾏｯﾊﾟーを作る
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="cmd"></param>
    protected void CreateMapOfSchema<T>(SQLiteCommand cmd)
    {
        CreateMapOfSchema(cmd, typeof(T), TableOf(typeof(T)));
    }

    /// <summary>
    /// Typeからﾏｯﾊﾟーを作る
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="type"></param>
    /// <param name="table"></param>
    /// <exception cref="Exception"></exception>
    protected void CreateMapOfSchema(SQLiteCommand cmd, Type type, string table)
    {
        if (!recordTypeMap.TryGetValue(type, out var typeMap)) {
            throw new Exception(Properties.Resources.SM_E_NotInTypeMap.Morfat(type.Name));
        }

        typeMap.Schemas.Clear();
        typeMap.CompiledMapper = null;

        // ﾏｯﾌﾟ
        for (int i = 0; i < typeMap.Properties.Count; i++) {
            var attr = typeMap.Properties[i].GetCustomAttribute<MurColumnAttribute>();
            if (attr == null) {
                throw new Exception(Properties.Resources.SM_E_GetAttributeFailed.Morfat(nameof(MurColumnAttribute)));
            }
            typeMap.Schemas.Add(new MapOfSchema { Name = attr.Name, Column = -1, Obzekt = i });
        }

        cmd.CommandText = "PRAGMA table_info('" + table + "');";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            var index = reader.GetInt32(0);
            var col = reader.GetString(1);
            var pmm = typeMap.Schemas.Where(w => w.Name == col)?.FirstOrDefault();
            if (pmm == null) {
                throw new Exception(Properties.Resources.SM_E_NotInSchema.Morfat(col));
            }
            pmm.Column = index;
        }
    }

    /// <summary>
    /// Typeからｽｷーﾏを作る
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    protected string CreateSchema(Type type)
    {
        var proto = new RupSchema();
        proto.Properties.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<MurColumnAttribute>() != null));
        proto.Bindings.AddRange(proto.Properties.Select(ColumnBinding.Sanitize));
        recordTypeMap[type] = proto;
        return string.Join(", ", proto.Properties.Select(CreateColumnDefinition));
    }


    /// <summary>
    /// ｶﾗﾑてゐ義作る
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private static string CreateColumnDefinition(PropertyInfo x)
    {
        var attr = x.GetCustomAttribute<MurColumnAttribute>()!;
        var dt = x.PropertyType switch {
            Type t when t == typeof(int) => "INTEGER",
            Type t when t == typeof(uint) => "INTEGER",
            Type t when t == typeof(ulong) => "INTEGER",
            Type t when t == typeof(long) => "INTEGER",
            Type t when t == typeof(float) => "REAL",
            Type t when t == typeof(double) || t == typeof(double?) => "REAL",
            Type t when t == typeof(string) => "TEXT",
            Type t when t == typeof(DateTime) => "INTEGER",
            Type t when t == typeof(bool) => "INTEGER",
            Type t when t == typeof(byte[]) => "BLOB",
            Type t when t.IsEnum => "TEXT NOT NULL",
            _ => throw new Exception(Properties.Resources.SM_E_UnknownBindValueType.Morfat(x.PropertyType))
        };
        bool pk = false;
        if (attr.Flags.HasFlag(MurColumnFlags.NotNull)) {
            dt += " NOT NULL";
        }
        if (attr.Flags.HasFlag(MurColumnFlags.Unique)) {
            pk = true;
            dt += " UNIQUE";
        }
        if (attr.Flags.HasFlag(MurColumnFlags.PrimaryKey)) {
            pk = true;
            dt += " PRIMARY KEY";
        }
        if (attr.DefaultSql != null) {
            dt += $" DEFAULT {attr.DefaultSql}";
        }
        else if (dt.StartsWith("INTEGER") && !pk) {
            dt += " DEFAULT 0";
        }

        return $"{QuoteIdentifier(attr.Name)} {dt}";
    }

    /// <summary>
    /// くおー
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private static string QuoteIdentifier(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";


    /// <summary>
    /// ｽｷーﾏの初期化とﾊﾞーｼﾞｮﾝ見て追加ｶﾗﾑの追加などする
    /// </summary>
    /// <param name="targetVersion"></param>
    /// <param name="tables"></param>
    /// <param name="initialize"></param>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    protected void InitializeSchema(long targetVersion, Type[] tables, Action<SQLiteTransaction>? initialize = null)
    {
        if (closed) {
            throw new InvalidOperationException(Properties.Resources.SM_E_DatumNotOpen);
        }
        if (targetVersion < 1) {
            throw new ArgumentOutOfRangeException(nameof(targetVersion));
        }
        foreach (var type in tables) {
            CreateSchema(type);
            if (recordTypeMap[type].Properties.Any(p => {
                var version = p.GetCustomAttribute<MurColumnAttribute>()!.HatchedAt;
                return version < 1 || version > targetVersion;
            })) {
                throw new InvalidOperationException(Properties.Resources.SM_E_InvalidColumnVersion.Morfat(type.Name));
            }
        }

        if (IsWritable) {
            WithWriter(w => {
                MigrateSchema(w, targetVersion, tables, initialize);
                return true;
            });
        }
        else {
            using var justify = JustawayReader();
            var currentVersion = GetUserVersion(justify.Connection);
            ValidateDatabaseVersion(currentVersion, targetVersion);
            if (currentVersion < targetVersion) {
                throw new InvalidOperationException(Properties.Resources.SM_E_DatunNeedsUpdate);
            }
        }
        using (var justify = JustawayReader()) {
            using var mapCommand = new SQLiteCommand(justify.Connection);
            foreach (var type in tables) {
                CreateMapOfSchema(mapCommand, type, TableOf(type));
                if (recordTypeMap[type].Schemas.Any(column => column.Column < 0)) {
                    throw new InvalidOperationException(Properties.Resources.SM_E_MissingColumn.Morfat(TableOf(type)));
                }
                // 複数ｽﾚｯﾄﾞから遅延生成されないよう開く時に作っておく
                var compile = typeof(DataStoreBase).GetMethod(nameof(CompileMapper), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(type);
                recordTypeMap[type].CompiledMapper = (Delegate)compile.Invoke(null, [recordTypeMap[type]])!;
            }
        }
    }

    /// <summary>
    /// ﾃーﾌﾞﾙ作成とｶﾗﾑ追加
    /// </summary>
    /// <param name="w"></param>
    /// <param name="targetVersion"></param>
    /// <param name="tables"></param>
    /// <param name="initialize"></param>
    /// <exception cref="InvalidOperationException"></exception>
    void MigrateSchema(SQLiteConnection w, long targetVersion, Type[] tables, Action<SQLiteTransaction>? initialize)
    {
        // 書き込み権を取得してからﾊﾞーｼﾞｮﾝを読むことで、同時更新を直列化する。
        using var transaction = w.BeginTransaction(IsolationLevel.Serializable);
        var currentVersion = GetUserVersion(w, transaction);
        ValidateDatabaseVersion(currentVersion, targetVersion);
        using var cmd = new SQLiteCommand(w) { Transaction = transaction };
        if (currentVersion == 0) {
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
            if (Convert.ToInt64(cmd.ExecuteScalar()) != 0) {
                throw new InvalidOperationException(Properties.Resources.SM_E_DatumVersionNotFount);
            }
        }
        foreach (var type in tables) {
            var table = TableOf(type);
            cmd.CommandText = $"PRAGMA table_info({QuoteIdentifier(table)});";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = cmd.ExecuteReader()) {
                while (reader.Read()) {
                    columns.Add(reader.GetString(1));
                }
            }
            var expectedColumns = recordTypeMap[type].Properties
                .Select(p => p.GetCustomAttribute<MurColumnAttribute>()!.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (columns.Any(name => !expectedColumns.Contains(name))) {
                throw new InvalidOperationException(Properties.Resources.SM_E_ColumnNotIncludedDefinition.Morfat(table));
            }
            if (columns.Count == 0) {
                cmd.CommandText = $"CREATE TABLE {QuoteIdentifier(table)} ({CreateSchema(type)});";
                cmd.ExecuteNonQuery();
                continue;
            }
            foreach (var property in recordTypeMap[type].Properties
                .OrderBy(p => p.GetCustomAttribute<MurColumnAttribute>()!.HatchedAt)) {
                var column = property.GetCustomAttribute<MurColumnAttribute>()!;
                var added = column.HatchedAt > currentVersion;
                if (columns.Contains(column.Name)) {
                    if (added) {
                        throw new InvalidOperationException(Properties.Resources.SM_E_ColumnMustNotExist.Morfat(table, column.Name));
                    }
                    continue;
                }
                if (!added) {
                    throw new InvalidOperationException(Properties.Resources.SM_E_MissingColumnEx.Morfat(table, column.Name));
                }
                if ((column.Flags & (MurColumnFlags.PrimaryKey | MurColumnFlags.Unique)) != 0) {
                    throw new InvalidOperationException(Properties.Resources.SM_E_PkeyNotMigratable.Morfat(table, column.Name));
                }
                // 失敗時は例外がぶっ飛ぶのでそいつに任せてﾛーﾙﾊﾞｯｸ
                cmd.CommandText = $"ALTER TABLE {QuoteIdentifier(table)} ADD COLUMN {CreateColumnDefinition(property)};";
                cmd.ExecuteNonQuery();
            }
        }
        initialize?.Invoke(transaction);
        SetUserVersion(targetVersion, transaction);
        transaction.Commit();
    }

    /// <summary>
    /// ﾊﾞーｼﾞｮﾝちぇっく
    /// </summary>
    /// <param name="cur"></param>
    /// <param name="target"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private static void ValidateDatabaseVersion(long cur, long target)
    {
        if (cur > target) {
            throw new InvalidOperationException(Properties.Resources.SM_E_DatumFromTheFuture.Morfat(cur, target));
        }
    }

    /// <summary>
    /// Tからﾊﾟﾗﾒーﾀを組み上げる
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
    /// <param name="command"></param>
    /// <exception cref="Exception"></exception>
    protected void MapRow<T>(T data, SQLiteCommand command)
    {
        if (!recordTypeMap.TryGetValue(typeof(T), out var prop)) {
            throw new Exception(Properties.Resources.SM_E_NotInTypeMap.Morfat(typeof(T).Name));
        }
        var parameters = command.Parameters;
        foreach (var b in prop.Bindings) {
            var gtv = b.Getter(data);
            if (gtv != null && b.EmptyStringAsNull && ((string)gtv).Length == 0) {
                gtv = null;
            }
            if (gtv != null && b.IsDateTime) {
                gtv = ((DateTime)gtv).AsUnixTimeSeconds();
            }
            parameters[b.ParameterName].Value = gtv ?? DBNull.Value;
        }
    }

    /// <summary>
    /// Tのｶﾗﾑ名一覧
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    protected IEnumerable<string> ColumnNamesOf<T>()
    {
        if (!recordTypeMap.TryGetValue(typeof(T), out var prop)) {
            throw new Exception(Properties.Resources.SM_E_NotInTypeMap.Morfat(typeof(T).Name));
        }
        return prop.Bindings.Select(b => b.Column.Name);
    }

    /// <summary>
    /// commandの前処理
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="command"></param>
    /// <exception cref="Exception"></exception>
    protected void PrepareCommand<T>(SQLiteCommand command)
    {
        if (!recordTypeMap.TryGetValue(typeof(T), out var prop)) {
            throw new Exception(Properties.Resources.SM_E_NotInTypeMap.Morfat(typeof(T).Name));
        }
        for (int i = 0; i < prop.Properties.Count; i++) {
            if (prop.Properties[i]?.PropertyType == null) {
                throw new Exception(Properties.Resources.SM_E_PropertyTypeIsNull.Morfat(prop.Properties[i].Name));
            }
            var typo = prop.Properties[i]?.PropertyType!;
            var dv = typo switch {
                Type t when t == typeof(int) => System.Data.DbType.Int32,
                // Int32だとint.MaxValue超えでｵーﾊﾞーﾌﾛーする
                Type t when t == typeof(uint) => System.Data.DbType.Int64,
                Type t when t == typeof(ulong) => System.Data.DbType.UInt64,
                Type t when t == typeof(long) => System.Data.DbType.Int64,
                Type t when t == typeof(float) => System.Data.DbType.Double,
                Type t when t == typeof(double) || t == typeof(double?) => System.Data.DbType.Double,
                Type t when t == typeof(string) => System.Data.DbType.String,
                Type t when t == typeof(DateTime) => System.Data.DbType.Int64,
                Type t when t == typeof(bool) => System.Data.DbType.Boolean,
                _ => throw new Exception(Properties.Resources.SM_E_UnknownBindValueType.Morfat(typo))
            };
            command.Parameters.Add(prop.Bindings[i].ParameterName, dv);
        }
    }

    #region IDisposable
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue) {
            disposedValue = true;
            if (disposing) {
                CloseConnection();
            }
        }
    }

    ~DataStoreBase()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // このｺーﾄﾞを変更しないでください。ｸﾘーﾝｱｯﾌﾟ ｺーﾄﾞを 'Dispose(bool disposing)' ﾒｿｯﾄﾞに記述します
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
