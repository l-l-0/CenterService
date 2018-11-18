//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

namespace HealthCare.MongoData
{
    public class Hospital
    {
        /// <summary>
        ///     苏州市广济医院
        /// </summary>
        public const string SZGJ = "SZGJ";
        /// <summary>
        ///     山东中医药大学第二附属医院
        /// </summary>
        public const string SDEY = "SDEY";
        /// <summary>
        ///     山东省立医院东院区
        /// </summary>
        public const string SDSL = "SDSL";
        /// <summary>
        ///     山东省立医院北院区
        /// </summary>
        public const string SDSL_BY = "SDSL_BY";
        /// <summary>
        ///     山东医学科学院附属医院
        /// </summary>
        public const string SDYKY = "SDYKY";
        /// <summary>
        ///     北京妇产医院
        /// </summary>
        public const string BJFC = "BJFC";
        /// <summary>
        ///     山东滨州市人民医院
        /// </summary>
        public const string SDBZ = "SDBZ";
        /// <summary>
        ///     江苏省南通市第三人民医院
        /// </summary>
        public const string NTSY = "NTSY";
        /// <summary>
        ///     山东大学附属济南市中心医院
        /// </summary>
        public const string SDJN = "SDJN";
        /// <summary>
        ///     山东省滕州市中心人民医院
        /// </summary>
        public const string SDTZ = "SDTZ";
        /// <summary>
        ///     上海中山医院青浦分院
        /// </summary>
        public const string SHQP = "SHQP";
        /// <summary>
        ///     深圳大学总医院
        /// </summary>
        public const string SZZY = "SZZY";
        /// <summary>
        ///     山东千佛山医院
        /// </summary>
        public const string SDQF = "SDQF";
        /// <summary>
        ///     北京天坛医院
        /// </summary>
        public const string BJTT = "BJTT";


        public static string ApiAddress(string action)
        {
            var port = 9090;  // 固定端口 9090
            return $"http://127.0.0.1:{port}/InternalApi/SyncDataInternal.asmx/{action}";
        }
    }

    public class ApiBack
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public string Hospital { get; set; }
    }

    public class ApiBack<T> : ApiBack
    {
        public T Data { get; set; }
    }
}
