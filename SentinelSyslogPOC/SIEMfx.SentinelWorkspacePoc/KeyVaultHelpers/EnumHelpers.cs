// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace SIEMfx.SentinelWorkspacePoc.KeyVaultHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;

    public class EnumComparer : IEqualityComparer<Enum>
    {
        public bool Equals(Enum x, Enum y)
        {
            if (x == null || y == null)
            {
                return ReferenceEquals(x, y);
            }

            return x.Equals(y);
        }

        public int GetHashCode(Enum obj)
        {
            Contract.Requires<ArgumentNullException>(obj != null, nameof(obj));

            return obj.GetHashCode();
        }
    }

    //public static class EnumHelpers
    //{
    //    private static readonly Func<Enum, string> GetUiString;

    //    static EnumHelpers()
    //    {
    //        GetUiString = MemoizationExtensions.Memoize<Enum, string>(LoadUiString);
    //    }

    //    public static string Label(this Enum en, string defaultValue = null)
    //    {
    //        return en != null ? GetUiString(en) : defaultValue ?? String.Empty;
    //    }

    //    private static string LoadUiString(Enum en)
    //    {
    //        Type enumType = en.GetType();
    //        string fieldName = en.ToString();
    //        var custAttr = enumType.GetField(fieldName).GetCustomAttributes(typeof(LabelAttribute), true).FirstOrDefault() as LabelAttribute;
    //        return custAttr != null ? custAttr.Translation : fieldName;
    //    }
    //}
}