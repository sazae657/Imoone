using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ImogeIndex.Common;

public static class EnumExtensions
{
    /// <summary>
    /// Tで指定した属性を持っていたらtrue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="e"></param>
    /// <returns></returns>
    public static bool HasAttribute<T>(this Enum e) where T : Attribute
    {
        return GetAttrubute<T>(e) != null;
    }

    /// <summary>
    /// 属性取得
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="e"></param>
    /// <returns></returns>
    public static T? GetAttrubute<T>(this Enum e) where T : Attribute
    {
        var field = e.GetType().GetField(e.ToString());
        if (field == null) {
            return null;
        }
        if (field.GetCustomAttribute<T>() is T att) {
            return att;
        }

        return null;
    }

    /// <summary>
    /// 属性取得
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="e"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool TryGetAttribute<T>(this Enum e, out T? type) where T : Attribute
    {
        var field = e.GetType().GetField(e.ToString());
        if (field == null) {
            type = null;
            return false;
        }
        if (field.GetCustomAttribute<T>() is T att) {
            type = att;
            return true;
        }

        type = null;
        return false;
    }
}
