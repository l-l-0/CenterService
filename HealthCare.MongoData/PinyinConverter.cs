//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using Microsoft.International.Converters.PinYinConverter;
using System.Text;

namespace HealthCare.MongoData
{
    public static class PinyinConverter
    {
        public static string Pinyin(this string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var ch in value)
            {
                try
                {
                    if (ChineseChar.IsValidChar(ch))
                    {
                        var py = new ChineseChar(ch);
                        sb.Append(py.Pinyins[0][0]);
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                catch
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString().TrimEnd();
        }

        public static string PinyinFull(this string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var ch in value)
            {
                try
                {
                    if (ChineseChar.IsValidChar(ch))
                    {
                        var py = new ChineseChar(ch);
                        sb.Append(py.Pinyins[0][0]).Append(py.Pinyins[0].Substring(1, py.Pinyins[0].Length - 2).ToLower());
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                catch
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString().TrimEnd();
        }
    }
}
