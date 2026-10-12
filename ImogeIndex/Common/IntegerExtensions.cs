using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace ImogeIndex.Common;

public static class IntegerExtensions
{
    /// <summary>
    /// 読みやすいﾊﾞｲﾄ数にする
    /// </summary>
    public static string ToReadableSize(this long bytes)
    {
        // 単位
        string[] suffixes = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

        if (bytes == 0)
            return "0 B";

        int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));

        if (place >= suffixes.Length)
            place = suffixes.Length - 1;

        double num = Math.Round(bytes / Math.Pow(1024, place), 2);

        return $"{num} {suffixes[place]}";
    }

    /// <summary>
    /// ぽじ
    /// </summary>
    /// <param name="val"></param>
    /// <returns></returns>
    public static int Positive(this int val)
    {
        return Math.Max(1, val);
    }

    /// <summary>
    /// ｴﾝﾃﾞｨｱﾝ反転
    /// </summary>
    /// <param name="val"></param>
    /// <returns></returns>
    public static int ReverseEndian(this int val)
    {
        return BinaryPrimitives.ReverseEndianness(val);
    }

    /// <summary>
    /// ｴﾝﾃﾞｨｱﾝ反転
    /// </summary>
    /// <param name="val"></param>
    /// <returns></returns>
    public static uint ReverseEndian(this uint val)
    {
        return BinaryPrimitives.ReverseEndianness(val);
    }

    /// <summary>
    /// ｴﾝﾃﾞｨｱﾝ反転
    /// </summary>
    /// <param name="val"></param>
    /// <returns></returns>
    public static long ReverseEndian(this long val)
    {
        return BinaryPrimitives.ReverseEndianness(val);
    }

    /// <summary>
    /// ｴﾝﾃﾞｨｱﾝ反転
    /// </summary>
    /// <param name="val"></param>
    /// <returns></returns>
    public static ulong ReverseEndian(this ulong val)
    {
        return BinaryPrimitives.ReverseEndianness(val);
    }
}

