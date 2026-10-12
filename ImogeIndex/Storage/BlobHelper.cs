using ImogeIndex.Common;
using System.Runtime.InteropServices;

namespace ImogeIndex.Storage;

/// <summary>
/// blob用のﾍﾙﾊﾟー
/// </summary>
public class BlobHelper {

    /// <summary>
    /// intとかの配列をﾊﾞｲﾅﾘーに
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <returns></returns>
    public static byte[] ToByteArray<T>(List<T> list) where T : unmanaged {
        if (list == null || list.Count == 0)
            return Array.Empty<byte>();

        ReadOnlySpan<T> longSpan = CollectionsMarshal.AsSpan(list);
        ReadOnlySpan<byte> byteSpan = MemoryMarshal.AsBytes(longSpan);
        return byteSpan.ToArray();
    }

    /// <summary>
    /// byte配列をintとかのListに
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static List<T> ToTypedList<T>(ReadOnlySpan<byte> bytes) where T : unmanaged {
        if (bytes.Length == 0) {
            return new List<T>();
        }

        int elemSize = Marshal.SizeOf<T>();
        if (bytes.Length % elemSize != 0)
            throw new ArgumentException(Properties.Resources.SM_E_LengthMultipleOf.Morfat(elemSize));

        // byte[] を T の Span にｷｬｽﾄ
        ReadOnlySpan<T> longSpan = MemoryMarshal.Cast<byte, T>(bytes);

        // List に変換して返す
        return [.. longSpan.ToArray()];
    }

    /// <summary>
    /// byte配列をintとかの配列に
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static T[] ToTypedArray<T>(ReadOnlySpan<byte> bytes) where T : unmanaged {
        if (bytes.Length == 0) {
            return [];
        }

        int elemSize = Marshal.SizeOf<T>();
        if (bytes.Length % elemSize != 0)
            throw new ArgumentException(Properties.Resources.SM_E_LengthMultipleOf.Morfat(elemSize));

        ReadOnlySpan<T> longSpan = MemoryMarshal.Cast<byte, T>(bytes);
        return longSpan.ToArray();
    }
}
