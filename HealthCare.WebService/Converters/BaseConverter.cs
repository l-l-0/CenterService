//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using System;
using System.IO;
using System.Text;

#pragma warning disable CS1591

namespace HealthCare.WebService.Converters
{
    public class BaseConverter
    {
        protected MongoContext mongo = new MongoContext();

        /// <summary>
        ///     记录同步日志
        /// </summary>
        public static void WriteSyncLog(string collection, string name, string xml, Exception ex = null)
        {
            if (ex == null)
            {
                var directory = $"{AppDomain.CurrentDomain.BaseDirectory}/Sync/{collection}";
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText($"{directory}/{name}.xml", xml, Encoding.UTF8);
            }
            else
            {
                var directory = $"{AppDomain.CurrentDomain.BaseDirectory}/Sync/{collection}/Error";
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText($"{directory}/{name}.xml", xml, Encoding.UTF8);
                var error = $"{ex.Message} {ex.InnerException?.Message}{Environment.NewLine}{ex.StackTrace}";
                File.WriteAllText($"{directory}/{name}.err", error, Encoding.UTF8);
            }
        }
    }

    public class ConverterResult<T>
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
    }
}