//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

#pragma warning disable CS1591

namespace HealthCare.CenterService.Controllers
{
    /// <summary>
    ///     SFRAMED WebAPI 基类，包括 <see cref="MongoContext"/> 实例，访问者 IP 地址
    /// </summary>
    public abstract class BaseController : ApiController
    {
        /// <summary>
        ///     数据库查询
        /// </summary>
        protected readonly MongoContext mongo = new MongoContext();

        private string terminal;
        /// <summary>
        ///     访问者 IP 地址
        /// </summary>
        protected string Terminal
        {
            // 如果网络拓扑中服务器和终端分属两个网络
            // 此时依靠 IP 地址不能判断终端
            // 需要终端在 HTTP 报文头中提交所属的部门
            // 此时 Computer 以报文头中的为准
            get => terminal ?? HttpContext.Current.Request.UserHostAddress.IP();
            set => terminal = value;
        }

        private string departmentId;
        /// <summary>
        ///     访问者所在的部门
        /// </summary>
        protected string DepartmentId => departmentId ?? (departmentId = mongo.CustomerCollection.AsQueryable().SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).ToList().Select(c => c.DepartmentId).FirstOrDefault() ?? string.Empty);

        private bool? isNonClinical;
        /// <summary>
        ///     非临床设备
        /// </summary>
        protected bool IsNonClinical => isNonClinical ?? (isNonClinical = (mongo.SystemConfigCollection.AsQueryable().Where(s => s.Key == $"{Terminal}:NonClinical").Select(s => s.JObject).FirstOrDefault() ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase)).Value;

        private bool? isSingleAuth;
        protected bool IsSingleAuth => isSingleAuth ?? (isSingleAuth = (mongo.SystemConfigCollection.AsQueryable().Where(s => s.Key == $"{Terminal}:SingleAuth").Select(s => s.JObject).FirstOrDefault() ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase)).Value;

        protected string[] Computers(string department) => mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == department).ToList().Select(c => c.Computer).Distinct().ToArray();
    }

    internal static class ApiObjectExtension
    {
        internal static ngController.GoodsProfile ToGoodsProfile(this Goods goods, string batch, DateTime expired) => ToGoodsProfileQty(goods, batch, expired, 0.0);

        internal static ngController.GoodsProfileQty ToGoodsProfileQty(this Goods goods, string batch, DateTime expired, double qty) => InitStringProperty(new ngController.GoodsProfileQty
        {
            UniqueId = goods.UniqueId,
            DisplayName = goods.DisplayName,
            Manufacturer = goods.Manufacturer,
            Specification = goods.Specification,
            GoodsType = goods.GoodsType,
            Trader = goods.Trader,
            SmallPackageUnit = goods.SmallPackageUnit,
            UsedUnit = goods.UsedUnit,
            Conversion = goods.Conversion,
            Filter = goods.Filter,
            Price = goods.Price,
            DosageForm = goods.DosageForm,
            BatchNumber = batch,
            ExpiredDate = expired,
            Qty = qty,
        });

        internal static MongoData.PatientProfile ToPatientProfile(this Patient patient) => InitStringProperty(new MongoData.PatientProfile
        {
            UniqueId = patient.UniqueId,
            DisplayName = patient.DisplayName,
            BedNo = patient.Hospitalization?.BedNo,
            SerialNumber = patient.Clinic?.SerialNumber,
            HospitalNumber = patient.Hospitalization?.HospitalNumber,
            MedicareNumber = patient.MedicareNumber,
            RegisterNumber = patient.RegisterNumber,
            Age = patient.Age ?? (DateTime.TryParse(patient.Birthday.ToString(), out DateTime birth) ? DateTime.Now.Year - birth.Year : (int?)null),
            Gender = patient.Gender,
            Diagnostic = patient.Diagnostic,
            CertificateCode = patient.CertificateCode,
            CertificateType = patient.CertificateType,
            CellPhone = patient.CellPhone,
        });

        internal static ngController.EmployeeProfile ToEmployeeProfile(this Employee employee) => InitStringProperty(new ngController.EmployeeProfile
        {
            UniqueId = employee.UniqueId,
            DisplayName = employee.UniqueId == ServiceStartup.Kernel ? "Kernel User" : employee.DisplayName,
            JobNo = employee.JobNo,
            JobTitle = employee.JobTitle,
            Signature = employee.Signature,
            CertificateCode = employee.CertificateCode,
            CertificateType = employee.CertificateType,
            CellPhone = employee.CellPhone,
        });

        internal static ngController.DepartmentProfile ToDepartmentProfile(this Department department, string computer = null) => InitStringProperty(new ngController.DepartmentProfile
        {
            UniqueId = department.UniqueId,
            DisplayName = department.DisplayName,
            Code = department.Code,
            Computer = computer,
        });

        private static T InitStringProperty<T>(T t)
        {
            foreach (var prop in typeof(T).GetProperties().Where(p => p.CanWrite && p.PropertyType == typeof(string)))
            {
                var value = prop.GetValue(t) ?? string.Empty;
                prop.SetValue(t, value);
            }
            return t;
        }
    }

    internal class HttpRequest
    {
        internal T Get<T>(string url) => Grap<T>(url, Method.Get);

        internal T Post<T>(string url, object body) => Grap<T>(url, Method.Post, body);

        internal T Put<T>(string url, object body) => Grap<T>(url, Method.Put, body);

        private T Grap<T>(string url, Method method, object body = null)
        {
            using (var client = new HttpClient(new HttpClientHandler { UseProxy = false, }) { Timeout = TimeSpan.FromSeconds(8), })
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/61.0.3163.79 Safari/537.36");

                Task<HttpResponseMessage> response;
                switch (method)
                {
                    case Method.Get: response = client.GetAsync(url); break;
                    case Method.Post: response = client.PostAsJsonAsync(url, body); break;
                    case Method.Put: response = client.PutAsJsonAsync(url, body); break;
                    default: throw new NotImplementedException($"{method} Not Implemented");
                }
                var json = response.Result.Content.ReadAsStringAsync().Result;
                var data = JObject.Parse(json)["value"];
                return data.ToObject<T>();
            }
        }

        internal async Task<T> GetAsync<T>(string url) => await GrapAsync<T>(url, Method.Get);

        internal async Task<T> PostAsync<T>(string url, object body) => await GrapAsync<T>(url, Method.Post, body);

        internal async Task<T> PutAsync<T>(string url, object body) => await GrapAsync<T>(url, Method.Put, body);

        private async Task<T> GrapAsync<T>(string url, Method method, object body = null)
        {
            using (var client = new HttpClient(new HttpClientHandler { UseProxy = false, }) { Timeout = TimeSpan.FromSeconds(8), })
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/61.0.3163.79 Safari/537.36");

                HttpResponseMessage response;
                switch (method)
                {
                    case Method.Get: response = await client.GetAsync(url); break;
                    case Method.Post: response = await client.PostAsJsonAsync(url, body); break;
                    case Method.Put: response = await client.PutAsJsonAsync(url, body); break;
                    default: throw new NotImplementedException($"{method} Not Implemented");
                }
                var json = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(json)["value"];
                return data.ToObject<T>();
            }
        }

        enum Method
        {
            Get = 1,
            Post,
            Put,
        }
    }
}