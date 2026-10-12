using ImogeIndex.Mur;

namespace ImogeIndex.Web2;


/// <summary>
/// IDﾋﾞｯﾄｾｯﾄのﾃｽﾄ
/// </summary>
public class IdSetController {

    [Fact(DisplayName = "追加したIDだけ昇順で出てくる")]
    public void AddAndEnumerate() {
        var set = new IdSet();
        set.AddRange([130, 0, 63, 64, 5]);
        set.Add(63);
        set.Add(100000);

        Assert.Equal([0L, 5L, 63L, 64L, 130L, 100000L], set);
        Assert.Equal(6, set.Count);
        Assert.True(set.Contains(64));
        Assert.False(set.Contains(65));
        Assert.False(set.Contains(-1));
        Assert.False(set.Contains(long.MaxValue / 2));
    }

    [Fact(DisplayName = "和と積は長さの違うビット列同士でも正しく出せる")]
    public void UnionAndIntersect() {
        var a = new IdSet();
        a.AddRange([1, 2, 3, 1000]);
        var b = new IdSet();
        b.AddRange([2, 3, 4]);

        var union = new IdSet();
        union.UnionWith(a);
        union.UnionWith(b);
        Assert.Equal([1L, 2L, 3L, 4L, 1000L], union);

        // 短い方との積で長い側の末尾が消えるはず
        a.IntersectWith(b);
        Assert.Equal([2L, 3L], a);

        var empty = new IdSet();
        b.IntersectWith(empty);
        Assert.True(b.IsEmpty);
    }

    [Fact(DisplayName = "空にしたら再利用できる")]
    public void ClearAndReuse() {
        var set = new IdSet(200);
        set.AddRange([10, 200]);
        set.Clear();
        Assert.True(set.IsEmpty);
        Assert.Empty(set);
        set.Add(7);
        Assert.Equal([7L], set);
    }

    [Fact(DisplayName = "負のIDは受け付けない")]
    public void RejectNegative() {
        var set = new IdSet();
        Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(-1));
    }
}
