using System.Collections;
using System.Numerics;

namespace ImogeIndex.Mur;

/// <summary>
/// 画像IDのﾋﾞｯﾄｾｯﾄ
/// </summary>
public sealed class IdSet : IEnumerable<long> {
    ulong[] bits;

    public IdSet() => bits = [];

    /// <summary>
    /// maxIdまで伸長せずに入る容量で作る
    /// </summary>
    /// <param name="maxId"></param>
    public IdSet(long maxId) => bits = maxId < 0 ? [] : new ulong[WordOf(maxId) + 1];

    /// <summary>
    /// 要素数(処理が重い)
    /// </summary>
    public int Count {
        get {
            var count = 0;
            foreach (var w in bits) {
                count += BitOperations.PopCount(w);
            }
            return count;
        }
    }

    /// <summary>
    /// 空？
    /// </summary>
    public bool IsEmpty {
        get {
            foreach (var w in bits) {
                if (w != 0) {
                    return false;
                }
            }
            return true;
        }
    }

    public bool Contains(long id) {
        if (id < 0 || (id >> 6) >= bits.Length) {
            return false;
        }
        return (bits[(int)(id >> 6)] & MaskOf(id)) != 0;
    }

    public void Add(long id) {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        var w = WordOf(id);
        AllocateWords(w + 1);
        bits[w] |= MaskOf(id);
    }

    public void AddRange(ReadOnlySpan<long> ids) {
        if (ids.IsEmpty) {
            return;
        }
        // 伸長を一回で済ませる
        var max = -1L;
        foreach (var id in ids) {
            ArgumentOutOfRangeException.ThrowIfNegative(id);
            if (id > max) {
                max = id;
            }
        }
        AllocateWords(WordOf(max) + 1);
        foreach (var id in ids) {
            bits[WordOf(id)] |= MaskOf(id);
        }
    }

    /// <summary>
    /// 和集合
    /// </summary>
    /// <param name="other"></param>
    public void UnionWith(IdSet other) {
        var length = other.bits.Length;
        // 相手の末尾の0ﾜーﾄﾞ分まで伸ばさない
        while (length > 0 && other.bits[length - 1] == 0) {
            length--;
        }
        AllocateWords(length);
        for (var i = 0; i < length; i++) {
            bits[i] |= other.bits[i];
        }
    }

    /// <summary>
    /// 積集合
    /// </summary>
    /// <param name="other"></param>
    public void IntersectWith(IdSet other) {
        var common = Math.Min(bits.Length, other.bits.Length);
        for (var i = 0; i < common; i++) {
            bits[i] &= other.bits[i];
        }
        Array.Clear(bits, common, bits.Length - common);
    }

    /// <summary>
    /// 空にする(確保済みの領域は使い回す)
    /// </summary>
    public void Clear() => Array.Clear(bits);

    public IEnumerator<long> GetEnumerator() {
        for (var i = 0; i < bits.Length; i++) {
            var w = bits[i];
            while (w != 0) {
                var bit = BitOperations.TrailingZeroCount(w);
                yield return ((long)i << 6) | (long)bit;
                w &= w - 1;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void AllocateWords(int words) {
        if (words <= bits.Length) {
            return;
        }
        Array.Resize(ref bits, Math.Max(words, bits.Length * 2));
    }

    private static int WordOf(long id) => checked((int)(id >> 6));
    private static ulong MaskOf(long id) => 1UL << (int)(id & 63);
}
