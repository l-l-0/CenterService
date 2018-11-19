//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

#pragma warning disable CS1591

namespace HealthCare.CenterService
{
    public static class Helper
    {
        /// <summary>
        ///     指纹版本
        /// </summary>
        public static string FingerVersion => "FingerVersion";
        /// <summary>
        ///     人脸版本
        /// </summary>
        public static string FaceVersion => "FaceVersion";
        /// <summary>
        ///     药柜配置版本（有IP作为前缀）
        /// </summary>
        public static string CabinetVersion => "CabinetVersion";
        public static string NowStringValue => DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-ffffff");

        /// <summary>
        ///     {UserId}:AllowedDepartments,  [depart,depart,...]
        /// </summary>
        public static string AllowedDepartments => "AllowedDepartments";

        /// <summary>
        /// {UserId}:AllowedGoods
        /// </summary>
        public static string AllowedGoods => "AllowedGoods";

        /// <summary>
        ///     计算用户的密码的 MD5 值
        /// </summary>
        public static string ComputeMd5Hash(string value, User user)
        {
            using (MD5 md5 = new MD5CryptoServiceProvider())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes($"{value}{user.UniqueId}"));
                return GetHexString(hash);
            }

            string GetHexString(byte[] bytes)
            {
                char[] hexArray = new char[bytes.Length << 1];
                for (int i = 0; i < hexArray.Length; i += 2)
                {
                    byte b = bytes[i >> 1];
                    hexArray[i] = GetHexValue(b >> 4);       // b / 16
                    hexArray[i + 1] = GetHexValue(b & 0xF);  // b % 16
                }
                return new string(hexArray, 0, hexArray.Length);
            }

            char GetHexValue(int i)
            {
                if (i < 10)
                {
                    return (char)(i + '0');
                }
                return (char)(i - 10 + 'a');
            }
        }
        
        /// <summary>
        ///     IP 地址转换成 uint32
        /// </summary>
        /// <param name="ipAddress">如果不是一个有效的 IP 地址则认为是 255.255.255.255</param>
        /// <returns></returns>
        public static uint ConvertToUInt32(string ipAddress)
        {
            var parts = ipAddress?.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Select(p => byte.Parse(p)).ToArray();
            var h1 = parts?.ElementAtOrDefault(0) ?? byte.MaxValue;
            var h2 = parts?.ElementAtOrDefault(1) ?? byte.MaxValue;
            var l2 = parts?.ElementAtOrDefault(2) ?? byte.MaxValue;
            var l1 = parts?.ElementAtOrDefault(3) ?? byte.MaxValue;
            return (uint)((h1 << 24) + (h2 << 16) + (l2 << 8) + l1);
        }

        public static string IP(this string ip) => new[] { "localhost", "127.0.0.1", "::1", }.Any(f => f == ip) ? "127.0.0.1" : ip;
    }
}
