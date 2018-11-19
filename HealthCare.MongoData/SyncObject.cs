//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace HealthCare.MongoData
{
    public interface ISyncObject
    {
        /// <summary>
        ///     同步物品信息 return: ApiBack
        /// </summary>
        void SyncAllGoods(string hospital);
        /// <summary>
        ///     同步部门信息 return: ApiBack
        /// </summary>
        void SyncAllDepartments(string hospital);
        /// <summary>
        ///     同步员工 return: ApiBack
        /// </summary>
        void SyncAllEmployees(string hospital);
        /// <summary>
        ///     同步指定调拨单的调拨数据 return: ApiBack string[]
        /// </summary>
        void SyncAllocationsByStockNo(string hospital, string stock);
        /// <summary>
        ///     查询指定的患者信息 return: ApiBack PatientProfile
        /// </summary>
        void SyncPatientByNo(string hospital, string patient);
        /// <summary>
        ///     和 HIS 进行预支记录的上传 return ApiBack
        /// </summary>
        void SyncPrescriptions(string hospital);
        /// <summary>
        ///     同步 HIS 的手术排班信息
        /// </summary>
        void SyncOperationSchedule(string hospital, string room, DateTime start, DateTime end);
    }

    public static class Helper
    {
        /// <summary>
        ///     全角字符转半角。目前 主要用于药品名字和规格
        /// </summary>
        public static string ToDBC(this string input) => new string(input.Select(ch => ch == 12288 ? (char)32 : ch > 65280 && ch < 65375 ? (char)(ch - 65248) : ch).ToArray());

        /// <summary>
        ///     从数据库的 Menu 中获取医院 <see cref="Hospital"/>
        /// </summary>
        /// <returns></returns>
        public static string GetHospital()
        {
            var menus = new Data.MongoContext().MenuCollection.AsQueryable().Where(m => !m.IsDisabled && !m.IsModule).ToList();
            var hospital = menus.Select(m =>
            {
                var match = System.Text.RegularExpressions.Regex.Match(m.Uri, @"[\w-]+/([A-Z]+)");
                return (match.Success, match.Groups[1].Value);
            }).Where(m => m.Success).FirstOrDefault().Value;
            return hospital;
        }
    }

    public class PatientProfile
    {
        [JsonProperty("_id")]
        public string UniqueId { get; set; }
        public string DisplayName { get; set; }
        public string BedNo { get; set; }
        public string SerialNumber { get; set; }
        public string HospitalNumber { get; set; }
        public string MedicareNumber { get; set; }
        public string RegisterNumber { get; set; }
        public string CertificateCode { get; set; }
        public string CertificateType { get; set; }
        public string Gender { get; set; }
        public int? Age { get; set; }
        public string Diagnostic { get; set; }
        public string CellPhone { get; set; }
    }
}
