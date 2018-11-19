//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using HealthCare.MongoData;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

#pragma warning disable CS1591


// take-return
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        /// <summary>
        ///     删除医嘱 —— 柜内未存储指定的药品
        /// </summary>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpDelete]
        [ActionName("delete-prescriptions-for-none-storage-goods")]
        public async Task<bool> DeletePrescriptionsForNoneStorageGoodsAsync(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var goodsIds = mongo.TerminalGoodsCollection.AsQueryable().Where(t => t.Computer == Terminal).Select(t => t.GoodsId).ToList();
            await mongo.PrescriptionCollection.UpdateManyAsync(p => !p.IsDisabled && p.FinishTime == null && !goodsIds.Contains(p.GoodsId) && p.DepartmentDestinationId == department,
                Builders<Prescription>.Update.Set(p => p.IsDisabled, true));
            return true;
        }

        /// <summary>
        /// 恢复当天被禁用的医嘱数据
        /// </summary>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpDelete]
        [ActionName("add-prescriptions-for-storage-goods")]
        public async Task<bool> AddPrescriptionsForStorageGoodsAsync(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var date = Convert.ToDateTime(DateTime.Now.ToShortDateString());
            var goodsIds = mongo.TerminalGoodsCollection.AsQueryable().Where(t => t.Computer == Terminal).Select(t => t.GoodsId).ToList();
            await mongo.PrescriptionCollection.UpdateManyAsync(p => p.IsDisabled && p.FinishTime == null && p.TimeFilter >= date && p.TimeFilter < date.AddDays(1) && goodsIds.Contains(p.GoodsId) && p.DepartmentDestinationId == department,
                            Builders<Prescription>.Update.Set(p => p.IsDisabled, false));
            return true;
        }

        public class PrescriptionBack
        {
            public bool NonClinical { get; set; }
            public PrescriptionProfile[] Prescriptions { get; set; }
        }

        public class PrescriptionProfile
        {
            [JsonProperty("_id")]
            public string UniqueId { get; set; }
            public DateTime? IssuedTime { get; set; }
            public DateTime TimeFilter { get; set; }
            public ExchangeMode Mode { get; set; }
            public EmployeeProfile Doctor { get; set; }
            public PatientProfile Patient { get; set; }
            public DepartmentProfile Department { get; set; }
            public GoodsProfile Goods { get; set; }
            public double Qty { get; set; }
            public string DispensingName { get; set; }
            public string DispensingId { get; set; }
            public string DispensingNumber { get; set; }
            public string TrackNumber { get; set; }
            public int DisplayOrder { get; set; }

        }

        /// <summary>
        ///     按日期 Range 获取未执行的医嘱信息
        /// </summary>
        /// <param name="hospital">医院</param>
        /// <param name="begin">开始日期（包括）</param>
        /// <param name="end">结束日期（不包括）</param>
        /// <param name="mode">取药医嘱或退药医嘱</param>
        /// <param name="type">clinic 或 hospitalization</param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-prescriptions-by-date-range")]
        public PrescriptionBack SearchPrescriptionsByDateRange(string hospital, DateTime begin, DateTime end, ExchangeMode mode, string type, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var nonClinical = IsNonClinical;
            // 登录者的 ip 地址即发药科室
            var lq = mongo.PrescriptionCollection.AsQueryable().Where(d => !d.IsDisabled && !d.IsAddition && d.TimeFilter >= begin && d.TimeFilter < end && d.Mode == mode && d.DepartmentDestinationId == department);
            switch (type)
            {
                // 1. 直接住院成为住院患者
                // 2. 因病情变化从门诊患者成为住院患者
                // 所以住院患者可能包含门诊号和住院号, 门诊患者一定没有住院号
                case "clinic":
                    lq = lq.Where(d => d.Patient == null || !string.IsNullOrEmpty(d.Patient.Clinic.SerialNumber) && string.IsNullOrEmpty(d.Patient.Hospitalization.HospitalNumber));
                    break;
                case "hospitalization":
                    // 护士有可能存在跨科室的情况(如 SDBZ), 使用护士的所属部门来过滤数据已经不能满足需求
                    var keys = ServiceStartup.GetAuthorized(Terminal).Select(u => $"{u.UserId}:{Helper.AllowedDepartments}").ToArray();
                    var departments = mongo.SystemConfigCollection.AsQueryable().Where(k => keys.Contains(k.Key)).Select(k => k.JObject).ToArray().SelectMany(o => JsonConvert.DeserializeObject<string[]>(o)).Distinct().ToArray();
                    lq = lq.Where(d => !string.IsNullOrEmpty(d.Patient.Hospitalization.HospitalNumber))
                            .Where(d => departments.Length <= 0 || departments.Contains(d.Patient.Hospitalization.ResidedAreaId) || departments.Contains(d.Patient.Hospitalization.RoomId) || departments.Contains(d.Patient.Hospitalization.AdmittedDepartmentId));
                    break;
            }
            var prescriptions = lq.ToList().Where(d => d.QtyActual < d.Qty || d.Qty == 0.0).ToList();  // mongo 不能比较大小   SDBZ 发药数量可能为 0           
            var departIds = prescriptions.SelectMany(p => new[]
            {
                p.Patient?.Hospitalization.ResidedAreaId ?? p.Patient?.Hospitalization.RoomId?? p.Patient?.Hospitalization.AdmittedDepartmentId,
                p.DepartmentSourceId
            }).Where(o => !string.IsNullOrEmpty(o)).Distinct().ToArray();
            var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
            var departs = mongo.DepartmentCollection.AsQueryable().Where(d => departIds.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).ToList().Select(d =>
            {
                var computers = customers.SelectMany(c => c.Cabinets).Where(f => f.DepartmentId == d.UniqueId).Select(f => f.Computer).Distinct().ToArray();
                return new DepartmentProfile
                {
                    UniqueId = d.UniqueId,
                    DisplayName = d.DisplayName,
                    Code = d.Code,
                    Computer = computers.FirstOrDefault(o => o == Terminal),
                };
            }).ToList();
            var employees = mongo.EmployeeCollection.AsQueryable().ToList();
            var ps = prescriptions.Select(p =>
            {
                p.Doctor = p.Doctor ?? employees.FirstOrDefault(o => o.UniqueId == p.DoctorId) ?? new Employee { UniqueId = p.DoctorId, };
                p.Doctor.Signature = null;
                p.Patient = p.Patient ?? mongo.PatientCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.PatientId) ?? new Patient { UniqueId = p.PatientId, };
                p.Goods = p.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.GoodsId) ?? new Goods { UniqueId = p.GoodsId, };
                var dpt = p.Patient?.Hospitalization.ResidedAreaId ?? p.Patient?.Hospitalization.RoomId ?? p.Patient?.Hospitalization.AdmittedDepartmentId ?? p.DepartmentSourceId;
                return new PrescriptionProfile
                {
                    UniqueId = p.UniqueId,
                    DisplayOrder = p.DisplayOrder,
                    IssuedTime = p.IssuedTime,
                    TimeFilter = p.TimeFilter,
                    Mode = p.Mode,
                    Qty = p.Qty,
                    TrackNumber = p.TrackNumber,
                    Doctor = p.Doctor.ToEmployeeProfile(),
                    Patient = p.Patient.ToPatientProfile(),
                    Department = departs.FirstOrDefault(f => f.UniqueId == dpt) ?? new DepartmentProfile { UniqueId = dpt, },
                    Goods = p.Goods.ToGoodsProfile(p.BatchNumber, p.ExpiredDate.Date),
                    DispensingName = employees.FirstOrDefault(u => u.UniqueId == p.DispensingId)?.DisplayName,
                    DispensingId = p.DispensingId,
                    DispensingNumber = p.DispensingNumber,
                };
            }).ToArray();
            return new PrescriptionBack { NonClinical = nonClinical, Prescriptions = ps, };
        }

        public class PrescriptionPrintProfile
        {
            [JsonProperty("_id")]
            public string UniqueId { get; set; }
            public int DisplayOrder { get; set; }
            public ExchangeMode Mode { get; set; }
            public string TrackNumber { get; set; }
            public DateTime TimeFilter { get; set; }
            public string Description { get; set; }
            public string UsedFrequency { get; set; }
            public string UsedPurpose { get; set; }
            public string UsedDosage { get; set; }
            public string FeeType { get; set; }
            public EmployeeProfile Doctor { get; set; }
            public PatientProfile Patient { get; set; }
            public GoodsProfile Goods { get; set; }
            public DepartmentProfile ResidedArea { get; set; }
            public DepartmentProfile Department { get; set; }
            public DepartmentProfile DepartDestination { get; set; }
            public EmployeeProfile Agent { get; set; }
            public EmployeeProfile Dispensing { get; set; }
            public EmployeeProfile PrimaryUser { get; set; }
            public EmployeeProfile SecondaryUser { get; set; }
            public string TemplateId { get; set; }
        }
        /// <summary>
        /// 查询处方打印数据
        /// </summary>
        /// <param name="ids">处方id</param>
        /// <returns></returns>
        [HttpPost]
        [ActionName("search-print-red-prescription")]
        public PrescriptionPrintProfile[] SearchPrintRedPrescription([FromBody] string[] ids)
        {
            // 登录者的 ip 地址即发药科室
            var prescriptions = mongo.PrescriptionCollection.AsQueryable().Where(p => ids.Contains(p.UniqueId)).ToList();
            var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
            var kvps = customers.SelectMany(c => c.Cabinets).Select(c => new { c.DepartmentId, c.Computer }).ToList();

            var departIds = prescriptions.SelectMany(p => new[]
            {
                p.Patient?.Hospitalization.ResidedAreaId,
                p.Patient?.Hospitalization.RoomId?? p.Patient?.Hospitalization.AdmittedDepartmentId,
                p.DepartmentSourceId
            }).Where(o => !string.IsNullOrEmpty(o)).Distinct().ToArray();
            var departments = mongo.DepartmentCollection.AsQueryable().Where(d => departIds.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).ToList();
            var employees = mongo.EmployeeCollection.AsQueryable().ToList();
            var journal = mongo.ActionJournalCollection.AsQueryable();
            var categories = mongo.GoodsCategoryCollection.AsQueryable().OrderBy(x => x.DisplayOrder).ToList();
            var templates = mongo.DesignerTemplateCollection.AsQueryable().Where(d => !d.IsDisabled).ToList();

            return prescriptions.Select(p =>
            {
                var ajc = journal.Where(aj => aj.TargetType == nameof(Prescription) && aj.TargetId == p.UniqueId).FirstOrDefault();
                p.Doctor = p.Doctor ?? employees.FirstOrDefault(o => o.UniqueId == p.DoctorId) ?? new Employee { UniqueId = p.DoctorId, DisplayName = string.Empty, };
                p.Patient = p.Patient ?? mongo.PatientCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.PatientId) ?? new Patient { UniqueId = p.PatientId, DisplayName = string.Empty, };
                p.Goods = p.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.GoodsId) ?? new Goods { UniqueId = p.GoodsId, DisplayName = string.Empty, };
                p.Goods.GoodsType = categories.Where(c => c.GoodsKeys.Contains(p.GoodsId)).FirstOrDefault()?.DisplayName ?? p.Goods.GoodsType;
                var dpt = p.Patient?.Hospitalization.RoomId ?? p.Patient?.Hospitalization.AdmittedDepartmentId ?? p.DepartmentSourceId;
                var raea = p.Patient?.Hospitalization.ResidedAreaId;
                var template = categories.Where(c => c.GoodsKeys.Contains(p.GoodsId)).FirstOrDefault();
                return new PrescriptionPrintProfile
                {
                    UniqueId = p.UniqueId,
                    TemplateId = template?.TemplateId ?? templates.FirstOrDefault().UniqueId,
                    TimeFilter = p.TimeFilter,
                    DisplayOrder = p.DisplayOrder,
                    Mode = p.Mode,
                    TrackNumber = p.TrackNumber,
                    Description = p.Description ?? string.Empty,
                    UsedDosage = p.UsedDosage ?? string.Empty,
                    UsedFrequency = p.UsedFrequency ?? string.Empty,
                    UsedPurpose = p.UsedPurpose ?? string.Empty,
                    FeeType = p.FeeType ?? string.Empty,
                    Doctor = p.Doctor.ToEmployeeProfile(),
                    Patient = p.Patient.ToPatientProfile(),
                    Goods = p.Goods.ToGoodsProfileQty(p.BatchNumber, p.ExpiredDate.Date, p.Qty),
                    ResidedArea = (departments.FirstOrDefault(f => f.UniqueId == raea) ?? new Department { UniqueId = raea, }).ToDepartmentProfile(kvps.FirstOrDefault(f => f.DepartmentId == raea)?.Computer),
                    Department = (departments.FirstOrDefault(f => f.UniqueId == dpt) ?? new Department { UniqueId = dpt, }).ToDepartmentProfile(kvps.FirstOrDefault(f => f.DepartmentId == dpt)?.Computer),
                    DepartDestination = (departments.FirstOrDefault(f => f.UniqueId == p.DepartmentDestinationId) ?? new Department { UniqueId = p.DepartmentDestinationId, }).ToDepartmentProfile(kvps.FirstOrDefault(f => f.DepartmentId == p.DepartmentDestinationId)?.Computer),
                    Agent = (employees.FirstOrDefault(d => d.UniqueId == p.AgentId) ?? new Employee { UniqueId = p.AgentId, }).ToEmployeeProfile(),
                    Dispensing = (employees.FirstOrDefault(u => u.UniqueId == p.DispensingId) ?? new Employee { UniqueId = p.DispensingId, }).ToEmployeeProfile(),
                    PrimaryUser = (employees.FirstOrDefault(e => e.UniqueId == ajc?.PrimaryUserId) ?? new Employee { UniqueId = ajc?.PrimaryUserId, }).ToEmployeeProfile(),
                    SecondaryUser = (employees.FirstOrDefault(e => e.UniqueId == ajc?.SecondaryUserId) ?? new Employee { UniqueId = ajc?.SecondaryUserId, }).ToEmployeeProfile(),
                };
            }).ToArray();
        }

        [HttpPost]
        [ActionName("search-display-order-for-prescriptions")]
        public int[] SearchDisplayOrderForPrescriptions([FromBody] string[] ids)
        {
            var puis = mongo.PrescriptionCollection.AsQueryable().Select(p => new { p.UniqueId, p.IssuedTime, }).Where(p => ids.Contains(p.UniqueId)).ToList();
            var temp = puis.GroupBy(p => p.IssuedTime.Date).SelectMany(p =>
            {
                var s = p.Key.Date;
                var e = p.Key.Date.AddDays(1);
                return mongo.PrescriptionCollection.AsQueryable().Where(o => o.IssuedTime >= s && o.IssuedTime < e).Select(o => o.UniqueId).ToArray()
                    .Select((o, idx) => new { UniqueId = o, Index = idx + 1, }).Join(p, x => x.UniqueId, y => y.UniqueId, (x, y) => x);
            }).ToArray();
            return ids.Join(temp, x => x, y => y.UniqueId, (x, y) => y.Index).ToArray();
        }

        public class MedicationPrintProfile
        {
            [JsonProperty("_id")]
            public string UniqueId { get; set; }
            public string TemplateId { get; set; }
            public int DisplayOrder { get; set; }
            public DateTime TimeFilter { get; set; }
            public EmployeeProfile Doctor { get; set; }
            public PatientProfile Patient { get; set; }
            public GoodsProfile Goods { get; set; }
            public DepartmentProfile Department { get; set; }
            public DepartmentProfile DepartDestination { get; set; }
            public EmployeeProfile Dispensing { get; set; }
            public EmployeeProfile PrimaryUser { get; set; }
            public EmployeeProfile SecondaryUser { get; set; }
        }
        /// <summary>
        /// 预支打印数据
        /// </summary>
        /// <param name="ids">处方id</param>
        /// <returns></returns>
        [HttpPost]
        [ActionName("search-print-red-medication")]
        public MedicationPrintProfile[] SearchPrintRedMedication([FromBody] string[] ids)
        {
            var medications = mongo.MedicationCollection.AsQueryable().Where(m => ids.Contains(m.UniqueId)).ToList();
            var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
            var kvps = customers.SelectMany(c => c.Cabinets).Select(c => new { c.DepartmentId, c.Computer }).ToList();

            var departIds = medications.Select(m =>
                m.Patient?.Hospitalization.ResidedAreaId ?? m.Patient?.Hospitalization.RoomId ?? m.Patient?.Hospitalization.AdmittedDepartmentId
            ).Where(o => !string.IsNullOrEmpty(o)).Distinct().ToArray();
            var departments = mongo.DepartmentCollection.AsQueryable().Where(d => departIds.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).ToList();
            var employees = mongo.EmployeeCollection.AsQueryable().ToList();
            var journal = mongo.ActionJournalCollection.AsQueryable();
            var categories = mongo.GoodsCategoryCollection.AsQueryable().OrderBy(x => x.DisplayOrder).ToList();
            var templates = mongo.DesignerTemplateCollection.AsQueryable().Where(d => !d.IsDisabled).ToList();

            return medications.Select(m =>
            {
                var ajc = journal.Where(aj => aj.TargetType == nameof(Medication) && aj.TargetId == m.UniqueId).FirstOrDefault();
                m.Doctor = m.Doctor ?? employees.FirstOrDefault(o => o.UniqueId == m.DoctorId) ?? new Employee { UniqueId = m.DoctorId, DisplayName = string.Empty, };
                m.Patient = m.Patient ?? mongo.PatientCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == m.PatientId) ?? new Patient { UniqueId = m.PatientId, DisplayName = string.Empty, };
                m.Goods = m.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == m.GoodsId) ?? new Goods { UniqueId = m.GoodsId, DisplayName = string.Empty, };
                var dpt = m.Patient?.Hospitalization.ResidedAreaId ?? m.Patient?.Hospitalization.RoomId ?? m.Patient?.Hospitalization.AdmittedDepartmentId;
                var template = categories.Where(c => c.GoodsKeys.Contains(m.GoodsId)).FirstOrDefault();

                return new MedicationPrintProfile
                {
                    UniqueId = m.UniqueId,
                    TemplateId = template?.TemplateId ?? templates.FirstOrDefault().UniqueId,
                    TimeFilter = m.TimeFilter,
                    DisplayOrder = m.DisplayOrder,
                    Doctor = m.Doctor.ToEmployeeProfile(),
                    Patient = m.Patient.ToPatientProfile(),
                    Goods = m.Goods.ToGoodsProfileQty(m.BatchNumber, m.ExpiredDate.Date, m.Qty),
                    Department = (departments.FirstOrDefault(f => f.UniqueId == dpt) ?? new Department { UniqueId = dpt, }).ToDepartmentProfile(kvps.FirstOrDefault(f => f.DepartmentId == dpt)?.Computer),
                    DepartDestination = (departments.FirstOrDefault(f => f.UniqueId == dpt) ?? new Department { UniqueId = dpt, }).ToDepartmentProfile(kvps.FirstOrDefault(f => f.DepartmentId == dpt)?.Computer),
                    Dispensing = (employees.FirstOrDefault(u => u.UniqueId == ajc?.SecondaryUserId) ?? new Employee { UniqueId = ajc?.OperatorUserId, }).ToEmployeeProfile(),
                    PrimaryUser = (employees.FirstOrDefault(e => e.UniqueId == ajc?.PrimaryUserId) ?? new Employee { UniqueId = ajc?.PrimaryUserId, }).ToEmployeeProfile(),
                    SecondaryUser = (employees.FirstOrDefault(e => e.UniqueId == ajc?.SecondaryUserId) ?? new Employee { UniqueId = ajc?.SecondaryUserId, }).ToEmployeeProfile(),
                };
            }).ToArray();
        }
        /// <summary>
        ///     自动排列序号
        /// </summary>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-display-order-by-number")]
        public async Task<bool> ModifyDisplayOrderByNumberAsync(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var lq = mongo.PrescriptionCollection.AsQueryable().OrderBy(o => o.TimeFilter).Where(d => !d.IsDisabled && !d.IsAddition && (d.Mode == ExchangeMode.CheckIn || d.Mode == ExchangeMode.CheckOut) && d.DepartmentDestinationId == department);
            var dates = lq.Where(d => d.DisplayOrder < 1).Select(d => d.TimeFilter).ToArray().Select(d => d.Date).Distinct().ToArray();

            var ps = dates.SelectMany(d =>
            {
                var end = d.AddDays(1);
                return lq.Where(o => o.TimeFilter >= d && o.TimeFilter < end).Select(o => new { o.UniqueId, o.DisplayOrder, o.TimeFilter, }).ToArray();
            }).GroupBy(o => o.TimeFilter.Date).SelectMany(gp => gp.Select((o, idx) => new { o.UniqueId, o.DisplayOrder, Index = idx + 1 })).ToArray();
            foreach (var p in ps.Where(o => o.DisplayOrder != o.Index))
            {
                await mongo.PrescriptionCollection.UpdateOneAsync(o => o.UniqueId == p.UniqueId, Builders<Prescription>.Update.Set(o => o.DisplayOrder, p.Index));
            }
            return true;
        }

        [HttpPut]
        [ActionName("modify-medication-displayorder")]
        public async Task<bool> ModifyMedicationDisplayOrderAsync(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var lq = mongo.MedicationCollection.AsQueryable().OrderBy(o => o.TimeFilter).Where(d => !d.IsDisabled && d.Mode == ExchangeMode.Medication && d.OperationSchedule.RoomId == department);
            var dates = lq.Where(d => d.DisplayOrder < 1).Select(d => d.TimeFilter).ToArray().Select(d => d.Date).Distinct().ToArray();
            var med = dates.SelectMany(d =>
            {
                var end = d.AddDays(1);
                return lq.Where(o => o.TimeFilter >= d && o.TimeFilter < end).Select(o => new { o.UniqueId, o.DisplayOrder, o.TimeFilter, }).ToArray();
            }).GroupBy(o => o.TimeFilter.Date).SelectMany(gp => gp.Select((o, idx) => new { o.UniqueId, o.DisplayOrder, Index = idx + 1 })).ToArray();
            foreach (var m in med.Where(o => o.DisplayOrder != o.Index))
            {
                await mongo.MedicationCollection.UpdateOneAsync(o => o.UniqueId == m.UniqueId, Builders<Medication>.Update.Set(o => o.DisplayOrder, m.Index));
            }
            return true;
        }

        /// <summary>
        /// 退药处方的药品有效期显示为取药处方的药品有效期
        /// </summary>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-prescription-out-for-in")]
        public async Task<bool> ModifyPrescriptionOutForInAsync(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var prescriptions = mongo.PrescriptionCollection.AsQueryable().Where(d => !d.IsDisabled && !d.IsAddition && d.DepartmentDestinationId == department);
            var preIn = prescriptions.Where(d => d.FinishTime == null && d.Mode == ExchangeMode.CheckIn).ToArray();
            var preOut = prescriptions.Where(d => d.FinishTime != null && d.Mode == ExchangeMode.CheckOut).ToArray();
            var data = preIn.Join(preOut, a => new { a.DoctorId, a.PatientId, a.GoodsId }, b => new { b.DoctorId, b.PatientId, b.GoodsId }, (a, b) => new { a.UniqueId, b.Plans.Last().Box.Fills.First().BatchNumber, b.Plans.Last().Box.Fills.First().ExpiredDate }).ToArray();
            foreach (var item in data)
            {
                await mongo.PrescriptionCollection.UpdateOneAsync(o => o.UniqueId == item.UniqueId, Builders<Prescription>.Update
                    .Set(o => o.BatchNumber, item.BatchNumber)
                    .Set(o => o.ExpiredDate, item.ExpiredDate));
            }
            return true;
        }

        /// <summary>
        /// 未执行的取药处方和退药处方能够相互冲抵
        /// </summary>
        /// <param name="terminal"></param>
        /// <param name="deleted">抵消之后是否删除（即不参与报表统计等计算，某些医院精麻药品不允许退药）</param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-offset-prescription-out-in")]
        public async Task<bool> ModifyOffsetPrescriptionOutInAsync(string terminal = null, bool deleted = false)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var prescriptions = mongo.PrescriptionCollection.AsQueryable().Where(d => !d.IsDisabled && !d.IsAddition && d.DepartmentDestinationId == department && d.FinishTime == null).ToList();
            var preOut = prescriptions.Where(p => p.Mode == ExchangeMode.CheckOut).ToArray();
            var preIn = prescriptions.Where(p => p.Mode == ExchangeMode.CheckIn).ToArray();
            var find = preIn.Join(preOut, a => new { a.DoctorId, a.PatientId, a.GoodsId, a.Qty }, b => new { b.DoctorId, b.PatientId, b.GoodsId, b.Qty }, (a, b) => b).Select(f => f.UniqueId).ToList();
            var data = find.Any() ? await SearchPositionAsync(nameof(Prescription), new PositionArg { Exchanges = find, Boxes = new List<string>(), IsRecycle = false }) : new PositionResult { Exchanges = new PostionExchange[0], GoodsBarcodes = new string[0], };

            var outf = mongo.PrescriptionCollection.AsQueryable().Where(d => find.Contains(d.UniqueId)).ToArray();
            outf = outf.Join(data.Exchanges, a => a.UniqueId, b => b.ExchangeId, (a, b) => new { a, b }).Select(p =>
            {
                p.a.Plans.ForEach(d => d.IsExecuted = true);
                p.a.QtyActual = p.b.Plans.Sum(d => d.Qty);
                return p.a;
            }).ToArray();
            var result = outf.Join(preIn, a => new { a.DoctorId, a.PatientId, a.GoodsId }, b => new { b.DoctorId, b.PatientId, b.GoodsId }, (a, b) => new { a, b }).Select(p =>
            {
                p.b.Plans = p.a.Plans;
                p.b.QtyActual = p.a.QtyActual;
                return p.b;
            }).Union(outf).ToArray();
            foreach (var item in result)
            {
                mongo.PrescriptionCollection.UpdateOne(x => x.UniqueId == item.UniqueId,
                    Builders<Prescription>.Update.Set(p => p.Plans, item.Plans).Set(p => p.QtyActual, item.QtyActual).Set(p => p.IsDisabled, deleted));
            }
            return true;
        }

        [HttpPut]
        [ActionName("modify-prescription-accepted")]
        public async Task<bool> ModifyPrescriptionAcceptedAsync(string prescription, string dispensingId = null, string agentId = null)
        {
            var pres = mongo.PrescriptionCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == prescription);
            if (pres == null)
            {
                return false;
            }

            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal);
            var name = string.IsNullOrEmpty(dispensingId) ? null : mongo.UserCollection.AsQueryable().Where(u => u.LoginId == dispensingId).Select(u => u.Employee.DisplayName).FirstOrDefault();
            var person = string.IsNullOrEmpty(agentId) ? null : mongo.UserCollection.AsQueryable().Where(u => u.LoginId == agentId).Select(u => u.Employee).FirstOrDefault();
            var builder = Builders<Prescription>.Update
                .Set(x => x.DispensingId, dispensingId)
                .Set(x => x.AgentId, agentId)
                .Set(x => x.Agent, person);
            await mongo.PrescriptionCollection.UpdateOneAsync(e => e.UniqueId == prescription, builder);
            return true;
        }
    }
}

// temp-for-doctor
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        // searchTerminalGoodsQty

        // searchKitsForLoggedIn

        // searchKitGoods

        // goodsSupervisor

        // modifyMedications

        // searchDrawersForGoods

        // searchPosition

        // modifyQtyForCabinet

        public class DoctorMedicationProfile
        {
            public DateTime Date { get; set; }
            public ExecuteReturn[] Executes { get; set; }
            public OperationScheduleProfile[] Operations { get; set; }
        }

        public class DoctorMedicationByTime
        {
            public DateTime Date { get; set; }
            public GoodsProfileQty Goods { get; set; }
            public EmployeeProfile Employee { get; set; }
        }

        [HttpPost]
        [ActionName("search-medication-profiles-by-doctors")]
        public async Task<DoctorMedicationProfile[]> SearchMedicationProfilesByDoctorsAsync(string hospital, [FromBody] string[] doctors, bool asc, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var medications = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && m.InOutBalance != true && Terminal == m.Computer).ToList();
            var checkOuts = await JudgeBalancesPrivateAsync(medications);

            if (doctors?.Any() == true)
            {
                checkOuts = checkOuts.Where(m => doctors.Contains(m.DoctorId)).ToList();
            }
            else
            {
                var users = ServiceStartup.GetAuthorized(Terminal);
                if (users.All(u => !u.Kernel))
                {
                    var usrs = users.Select(u => u.UserId).Distinct().ToArray();
                    checkOuts = checkOuts.Where(m => usrs.Contains(m.DoctorId)).ToList();
                }
                // Kernel 查询所有，非 Kernel 查询部分
            }
            var finds = checkOuts.Where(m => m.PatientId == null && m.Mode == ExchangeMode.CheckOut).ToArray();

            var dates = finds.Select(m => m.TimeFilter.Date);
            var min = dates.Any() ? dates.Min() : DateTime.Now.Date;
            var max = (dates.Any() ? dates.Max() : DateTime.Now.Date).AddDays(1);
            var oss = await SearchOperationSchedulesAsync(hospital, min, max, DepartmentId);

            var data = finds.GroupBy(m => m.TimeFilter.Date).Select(ms => new DoctorMedicationProfile
            {
                Date = ms.Key,
                Executes = ms.GroupBy(o => o.GoodsId).Select(gm =>
                {
                    var gd = gm.FirstOrDefault(f => f.GoodsId == gm.Key)?.Goods ?? new Goods { UniqueId = gm.Key, };
                    return new ExecuteReturn
                    {
                        Goods = gd.ToGoodsProfile(string.Empty, DateTime.MaxValue.Date),
                        Medications = gm.Select(m => m.UniqueId).ToArray(),
                        QtyActuals = gm.Select(m => m.QtyActual).ToArray(),
                        Qty = gm.Sum(m => m.QtyActual),
                    };
                }).ToArray(),
                Operations = oss.Where(os => os.ApplyTime >= ms.Key && os.ApplyTime < ms.Key.AddDays(1)).ToArray(),
            });
            return (asc ? data.OrderBy(d => d.Date) : data.OrderByDescending(d => d.Date)).ToArray();
        }

        [HttpGet]
        [ActionName("search-medication-profiles-by-time")]
        public async Task<DoctorMedicationByTime[]> SearchMedicationProfilesByTime(DateTime start, DateTime end, string doctor = null, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var medications = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && m.InOutBalance != true && Terminal == m.Computer && m.TimeFilter >= start && m.TimeFilter <= end).ToList();
            var checkOuts = await JudgeBalancesPrivateAsync(medications);
            if (doctor != null)
            {
                checkOuts = checkOuts.Where(c => c.DoctorId == doctor).ToList();
            }
            checkOuts.ForEach(o => o.Goods = o.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == o.GoodsId) ?? new Goods { UniqueId = o.GoodsId, });

            var data = checkOuts.Select(c => new DoctorMedicationByTime
            {
                Date = c.TimeFilter,
                // TODO BatchNumber 和 ExpiredDate 根据 plans 生成, 此时需要改为 SelectMany
                Goods = c.Goods.ToGoodsProfileQty(string.Empty, DateTime.MaxValue.Date, c.QtyActual),
            }).ToArray();
            return data;
        }

        [HttpGet]
        [ActionName("search-doctor-medication-profiles-by-time")]
        public async Task<DoctorMedicationByTime[]> SearchDoctorMedicationProfilesByTime(DateTime start, DateTime end, string doctor = null, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var medications = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && Terminal == m.Computer && m.Mode == ExchangeMode.Medication && m.TimeFilter >= start && m.TimeFilter <= end).ToList();
            var unfinished = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && Terminal == m.Computer && m.InOutBalance != true).ToList();
            var checkOuts = await JudgeBalancesPrivateAsync(unfinished);
            var data = medications.Concat(checkOuts).ToList();
            if (doctor != null)
            {
                data = data.Where(c => c.DoctorId == doctor).ToList();
            }
            data.ForEach(d =>
            {
                d.Goods = d.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == d.GoodsId) ?? new Goods { UniqueId = d.GoodsId, };
                d.Doctor = d.Doctor ?? mongo.EmployeeCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == d.DoctorId) ?? new Employee { UniqueId = d.DoctorId, };
            });

            return data.Select(c => new DoctorMedicationByTime
            {
                Date = c.TimeFilter,
                Goods = c.Goods.ToGoodsProfileQty(c.BatchNumber, c.ExpiredDate, c.QtyActual),
                Employee = c.Doctor.ToEmployeeProfile(),
            }).ToArray();
        }

        [HttpPost]
        [ActionName("search-executed-exchange-plans")]
        public Exchange[] SearchExecutedExchangePlans(string collection, [FromBody] string[] exchanges)
        {
            Exchange[] finds;
            switch (collection)
            {
                case nameof(Exchange): finds = mongo.ExchangeCollection.AsQueryable().Where(e => exchanges.Contains(e.UniqueId)).Select(e => new Exchange { UniqueId = e.UniqueId, Plans = e.Plans, }).ToArray(); break;
                case nameof(Allocation): finds = mongo.AllocationCollection.AsQueryable().Where(e => exchanges.Contains(e.UniqueId)).Select(e => new Exchange { UniqueId = e.UniqueId, Plans = e.Plans, }).ToArray(); break;
                case nameof(Medication): finds = mongo.MedicationCollection.AsQueryable().Where(e => exchanges.Contains(e.UniqueId)).Select(e => new Exchange { UniqueId = e.UniqueId, Plans = e.Plans, }).ToArray(); break;
                case nameof(Prescription): finds = mongo.PrescriptionCollection.AsQueryable().Where(e => exchanges.Contains(e.UniqueId)).Select(e => new Exchange { UniqueId = e.UniqueId, Plans = e.Plans, }).ToArray(); break;
                default: finds = new Exchange[0]; break;
            }
            return finds.Select(f =>
            {
                f.Plans = (f.Plans ?? new List<ActionPlan>()).Where(p => p.IsExecuted).ToList();
                return f;
            }).ToArray();
        }

    }
}

// temp-take-for-patient
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        /// <summary>
        ///     查询患者信息   临床药柜只能查询本部门，非临床查询所有
        /// </summary>
        [HttpGet]
        [ActionName("search-patients-profile")]
        public Pager<PatientProfile> SearchPatientsProfile(string content = null, int take = -1, int index = 0, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var nonClinical = IsNonClinical;
            var department = DepartmentId;

            var queryLq = mongo.PatientCollection.AsQueryable().Where(p => !p.IsDisabled && (nonClinical || department == p.Hospitalization.ResidedAreaId || department == p.Hospitalization.RoomId || department == p.Hospitalization.AdmittedDepartmentId));
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                queryLq = queryLq.Where(p => p.Hospitalization.BedNo.ToLower() == content || p.Hospitalization.HospitalNumber.ToLower() == content);
            }
            var count = queryLq.Count();

            var patients = (take > 0 ? queryLq.Skip(index * take).Take(take) : queryLq).Select(p => new PatientProfile
            {
                UniqueId = p.UniqueId,
                DisplayName = p.DisplayName,
                BedNo = p.Hospitalization.BedNo,
                HospitalNumber = p.Hospitalization.HospitalNumber,
                SerialNumber = p.Clinic.SerialNumber,
                MedicareNumber = p.MedicareNumber,
                RegisterNumber = p.RegisterNumber,
                Age = p.Age,
                Gender = p.Gender,
                Diagnostic = p.Diagnostic,
                CertificateCode = p.CertificateCode,
                CertificateType = p.CertificateType,
            }).ToArray();
            return new Pager<PatientProfile> { Count = count, Data = patients, };
        }

        /// <summary>
        ///     查询医生的基本信息       医生无论临床还是非临床都查询所有
        /// </summary>
        [HttpGet]
        [ActionName("search-doctors-profile")]
        public Pager<EmployeeProfile> SearchDoctorsProfile(string content = null, int take = -1, int index = 0)
        {
            var elq = mongo.EmployeeCollection.AsQueryable().Where(e => !e.IsDisabled);
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                elq = elq.Where(e => e.DisplayName.ToLower().Contains(content) || e.Pinyin.ToLower().Contains(content) || e.PinyinFull.ToLower().Contains(content) || e.JobNo.ToLower().Contains(content));
            }
            var cfgs = mongo.SystemConfigCollection.AsQueryable().Where(c => c.Key.EndsWith(":Doctors")).Select(c => c.JObject).ToList();
            var doctors = cfgs.Where(d => d.StartsWith(Terminal)).Concat(cfgs.Where(d => !d.StartsWith(Terminal))).SelectMany(s => JsonConvert.DeserializeObject<List<string>>(s)).ToList();
            elq = elq.Where(e => doctors.Contains(e.UniqueId));

            var count = elq.Count();

            var array = (take > 0 ? elq.Skip(index * take).Take(take) : elq).Select(x => new EmployeeProfile
            {
                UniqueId = x.UniqueId,
                DisplayName = x.DisplayName,
                JobNo = x.JobNo,
                JobTitle = x.JobTitle,
            }).ToArray();
            return new Pager<EmployeeProfile> { Count = count, Data = array, };
        }

        // searchTerminalGoodsQty

        /// <summary>
        ///     查询指定的患者信息
        /// </summary>  
        [HttpPost]
        [ActionName("sync-patient-by-no")]
        public async Task<ApiBack<PatientProfile>> SyncPatientByNoAsync(string hospital, string patient)
        {
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseProxy = false, }))
            {
                var url = $"{Hospital.ApiAddress(nameof(ISyncObject.SyncPatientByNo))}?hospital={hospital}&patient={patient}";
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ApiBack<PatientProfile>>(json);
            }
        }

        /// <summary>
        ///     检验药品是否还需要监督人
        /// </summary>
        [HttpPost]
        [ActionName("goods-supervisor")]
        public async Task<int> GoodsSupervisorAsync([FromBody] string[] goods, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            await ServiceStartup.ClearCertifyAuthorizedAsync(Terminal);

            if (ServiceStartup.GetPrimaryAuthorized(Terminal).Kernel)
            {
                // Kernel 登录
                return 0;
            }

            if (ServiceStartup.GetSecondaryAuthorized(Terminal) != null)
            {
                // 已经是双人登录，不需要再认证
                return 0;
            }

            var auths = mongo.GoodsCategoryCollection.AsQueryable().Where(g => g.IsDoubleCertify).SelectMany(g => g.GoodsKeys).Distinct().ToList();
            if (auths.Join(goods, a => a, b => b, (a, b) => a).Any())
            {
                // 需要监督人
                return 1;
            }

            // 没有需要监督使用的药品
            return 0;
        }

        /// <summary>
        ///     修改 Medications
        /// </summary>
        [HttpPut]
        [ActionName("modify-medications")]
        public async Task<string[]> ModifyMedicationsAsync([FromBody] Medication[] medications, string terminal = null)
        {
            Terminal = terminal ?? Terminal;

            foreach (var med in medications)
            {
                med.UniqueId = med.UniqueId ?? SfraObject.GenerateId();
                med.Computer = med.Computer ?? Terminal;
                med.GoodsBarcodes = med.GoodsBarcodes ?? new List<string>();
                med.BatchNumber = med.BatchNumber ?? string.Empty;
                med.CustomerId = med.CustomerId ?? mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).Select(c => c.OwnerCode).FirstOrDefault();
                med.Doctor = mongo.EmployeeCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == med.DoctorId);
                med.Patient = mongo.PatientCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == med.PatientId);
                med.Goods = mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == med.GoodsId);
                med.OperationSchedule = mongo.OperationScheduleCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == med.OperationScheduleId);

                if (med.Mode == ExchangeMode.Medication)
                {
                    // 根据 checkOutIds 和 checkInId 的取退位置和数量, 计算最终取药的 plans
                    var outPlans = mongo.MedicationCollection.AsQueryable().Where(m => med.CheckOutIds.Contains(m.UniqueId)).SelectMany(m => m.Plans).Where(p => p.IsExecuted).ToArray();
                    var inPlans = mongo.MedicationCollection.AsQueryable().Where(m => m.UniqueId == med.CheckInId).SelectMany(m => m.Plans).Where(p => p.IsExecuted).ToArray();
                    foreach (var o in outPlans)
                    {
                        o.Qty -= Math.Min(o.Qty, inPlans.Where(p => p.Box.No == o.Box.No).Sum(p => p.Qty));
                    }
                    med.Plans = outPlans.Where(o => o.Qty > 0.0).ToList();
                }

                await mongo.MedicationCollection.FindOneAndReplaceAsync<Medication>(x => x.UniqueId == med.UniqueId, med, new FindOneAndReplaceOptions<Medication, Medication> { IsUpsert = true });
            }
            return medications.Select(x => x.UniqueId).ToArray();
        }

        [HttpGet]
        [ActionName("search-system-config")]
        public string SearchSystemConfig(string key) => mongo.SystemConfigCollection.AsQueryable().FirstOrDefault(s => s.Key == key)?.JObject;
    }

}

// temp-for-operation
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class OperationScheduleProfile
        {
            [JsonProperty("_id")]
            public string UniqueId { get; set; }
            public bool IsCancelled { get; set; }
            public string OperationType { get; set; }
            public DateTime ApplyTime { get; set; }
            public PatientProfile Patient { get; set; }
            public DateTime? ExecutionBeginTime { get; set; }
            public DateTime? ExecutionEndTime { get; set; }
            public string IdentityBarcode { get; set; }

            public EmployeeProfile PrimaryDoctor { get; set; }
            public EmployeeProfile Anesthetist { get; set; }
            public string AnesthesiaMode { get; set; }

            public EmployeeProfile PrimaryAssistant { get; set; }
            public EmployeeProfile PrimaryHandwashingNurse { get; set; }
            public EmployeeProfile PrimaryTourNurse { get; set; }
            public EmployeeProfile SecondaryAssistant { get; set; }
            public EmployeeProfile SecondaryHandwashingNurse { get; set; }
            public EmployeeProfile SecondaryTourNurse { get; set; }

            public int ExecutesCount { get; set; }
            public string Remark { get; set; }
        }

        /// <summary>
        ///     查询申请时间在指定范围内的手术排班
        /// </summary>
        [HttpGet]
        [ActionName("search-operation-schedules")]
        public async Task<OperationScheduleProfile[]> SearchOperationSchedulesAsync(string hospital, DateTime begin, DateTime end, string anesthetist = null, string department = null)
        {
            department = department ?? DepartmentId;
            var rooms = VisitorDepartments(hospital, department);
            var oses = mongo.OperationScheduleCollection.AsQueryable().OrderBy(os => os.ApplyTime).Where(os => !os.IsDisabled && !os.IsCancelled && rooms.Contains(os.RoomId) && os.ApplyTime >= begin && os.ApplyTime < end && (os.AnesthetistId == anesthetist || anesthetist == null)).ToList();
            var opes = oses.Select(x => new OperationScheduleProfile
            {
                UniqueId = x.UniqueId,
                ApplyTime = x.ApplyTime,
                Patient = (x.Patient ?? new Patient { UniqueId = x.PatientId, }).ToPatientProfile(),
                IdentityBarcode = x.IdentityBarcode,
                ExecutionBeginTime = x.ExecutionBeginTime,
                ExecutionEndTime = x.ExecutionEndTime,

                PrimaryDoctor = (x.PrimaryDoctor ?? new Employee { UniqueId = x.PrimaryDoctorId, }).ToEmployeeProfile(),
                Anesthetist = (x.Anesthetist ?? new Employee { UniqueId = x.AnesthetistId, }).ToEmployeeProfile(),
                AnesthesiaMode = x.AnesthesiaMode,

                PrimaryAssistant = (x.PrimaryAssistant ?? new Employee { UniqueId = x.PrimaryAssistantId, }).ToEmployeeProfile(),
                PrimaryHandwashingNurse = (x.PrimaryHandwashingNurse ?? new Employee { UniqueId = x.PrimaryHandwashingNurseId, }).ToEmployeeProfile(),
                PrimaryTourNurse = (x.PrimaryTourNurse ?? new Employee { UniqueId = x.PrimaryTourNurseId, }).ToEmployeeProfile(),
                SecondaryAssistant = (x.SecondaryAssistant ?? new Employee { UniqueId = x.SecondaryAssistantId, }).ToEmployeeProfile(),
                SecondaryHandwashingNurse = (x.SecondaryHandwashingNurse ?? new Employee { UniqueId = x.SecondaryHandwashingNurseId, }).ToEmployeeProfile(),
                SecondaryTourNurse = (x.SecondaryTourNurse ?? new Employee { UniqueId = x.SecondaryTourNurseId, }).ToEmployeeProfile(),
            }).ToArray();
            foreach (var os in opes.Where(item => item.ExecutionEndTime == null))
            {
                var rtns = await SearchPatientsForReturnAsync(os.Patient.UniqueId, Terminal);
                os.ExecutesCount = rtns.Sum(r => r.Executes.Count);
            }
            return opes;
        }

        public class TerminalGoodsProfile : GoodsProfile
        {
            public double QtyExisted { get; set; }
            public double? KitQty { get; set; }
            public bool IsCurrent { get; set; }
            public string Computer { get; set; }
            public string DisplayText { get; set; }
            public string Background { get; set; }
            public string Foreground { get; set; }
        }

        [HttpGet]
        [ActionName("search-permit-terminal-goods-with-qty")]
        public TerminalGoodsProfile[] SearchPermitTerminalGoodsWithQty(string hospital, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;

            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).Select(c => new
            {
                Cabinets = c.Cabinets.Where(o => o.Computer == Terminal),
                OutOfCabinets = c.OutOfCabinets.Where(o => o.DepartmentId == department),
            }).ToList();

            var pmtBoxes = SearchPermitBoxes(Terminal);
            var pmtFills = cs.SelectMany(c => c.Cabinets).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).Join(pmtBoxes, x => x.No, y => y, (x, y) => x).SelectMany(b => b.Fills).GroupBy(f => f.GoodsId).Select(g => g.First()).Join(SearchPermitGoods(Terminal), x => x.GoodsId, y => y, (x, y) => x).ToList();
            var goodsIds = pmtFills.Select(t => t.GoodsId).Distinct().ToList();

            // 登录人智能柜硬件权限
            var usrs = ServiceStartup.GetAuthorized(Terminal);

            var goodskeys = usrs.Select(u => $"{u.UserId}:{Helper.AllowedGoods}").ToArray();
            var goodsconfigs = mongo.SystemConfigCollection.AsQueryable().Where(k => goodskeys.Contains(k.Key)).ToArray().Select(d => new
            {
                JobNo = d.Key.Split(':')[0],
                goods = JsonConvert.DeserializeObject<string[]>(d.JObject),
            }).FirstOrDefault(g => g.JobNo == usrs.FirstOrDefault()?.UserId)?.goods.ToArray();

            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).ToList();

            if (goodsconfigs != null && goodsconfigs.Length > 0)
            {
                goods = goods.Where(g => goodsconfigs.Contains(g.Filter)).ToList();
            }
            var lq = cs.SelectMany(c => c.Cabinets).Concat(cs.SelectMany(v => v.OutOfCabinets)).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes);
            if (usrs.All(u => !u.Kernel))
            {
                var uIds = usrs.Select(x => x.UserId).ToList();
                var boxes = mongo.UserCollection.AsQueryable().Where(u => uIds.Contains(u.LoginId)).SelectMany(u => u.AvailableStorages).Distinct().ToList();
                lq = lq.Join(boxes, a => a.No, b => b, (a, b) => a);
            }
            var fills = lq.SelectMany(b => b.Fills).ToList();

            var categories = mongo.GoodsCategoryCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
            var data = pmtFills.Select(t =>
            {
                t.Goods = goods.FirstOrDefault(g => g.UniqueId == t.GoodsId);
                var category = categories.FirstOrDefault(f => f.GoodsKeys.Any(k => k == t.GoodsId));
                return new TerminalGoodsProfile
                {
                    UniqueId = t.GoodsId,
                    DisplayName = t.Goods?.DisplayName,
                    GoodsType = t.Goods?.GoodsType,
                    Manufacturer = t.Goods?.Manufacturer,
                    Specification = t.Goods?.Specification,
                    Trader = t.Goods?.Trader,
                    UsedUnit = t.Goods?.UsedUnit,
                    Conversion = t.Goods?.Conversion ?? 1.0,
                    Filter = t.Goods?.Filter,
                    KitQty = null,
                    BatchNumber = string.Empty,
                    ExpiredDate = DateTime.MaxValue.Date,
                    QtyExisted = fills.Where(f => f.GoodsId == t.GoodsId).Sum(f => f.QtyExisted),
                    IsCurrent = true,
                    Computer = Terminal,
                    DisplayText = cs.SelectMany(c => c.Cabinets).Concat(cs.SelectMany(c => c.OutOfCabinets)).Where(c => c.Drawers.Any(d => d.Boxes.Any(b => b.Fills.Any(f => f.GoodsId == t.GoodsId)))).Select(c => c.DisplayText).FirstOrDefault(),
                    Background = category?.Background,
                    Foreground = category?.Foreground,
                };
            }).Where(d => d.DisplayName != null).ToArray();

            if (hospital == Hospital.SDEY)
            {
                data = data.Concat(OtherTerminals(data)).ToArray();
            }
            return data;

            TerminalGoodsProfile[] OtherTerminals(TerminalGoodsProfile[] ownes)
            {
                // SDEY 患者预支时选择物品, 当前药柜没有的物品任然需要显示 并且 显示柜子名称
                // 校验硬件权限
                var others = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).Select(c => new
                {
                    Cabinets = c.Cabinets.Where(o => o.Computer != Terminal),
                    OutOfCabinets = c.OutOfCabinets.Where(o => o.DepartmentId != department),
                }).ToList();
                var otherFills = others.SelectMany(c => c.Cabinets).Concat(others.SelectMany(c => c.OutOfCabinets)).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).Join(pmtBoxes, x => x.No, y => y, (x, y) => x).SelectMany(b => b.Fills).ToList();
                var outIds = otherFills.Select(f => f.GoodsId).Distinct().Except(ownes.Select(d => d.UniqueId)).ToList();
                var outGoods = mongo.GoodsCollection.AsQueryable().Where(g => outIds.Contains(g.UniqueId)).ToList();
                var outsData = otherFills.GroupBy(f => f.GoodsId).Select(g => g.First()).Select(g =>
                {
                    g.Goods = outGoods.FirstOrDefault(o => o.UniqueId == g.GoodsId);
                    var category = categories.FirstOrDefault(f => f.GoodsKeys.Any(k => k == g.GoodsId));
                    return new TerminalGoodsProfile
                    {
                        UniqueId = g.GoodsId,
                        DisplayName = g.Goods?.DisplayName,
                        GoodsType = g.Goods?.GoodsType,
                        Manufacturer = g.Goods?.Manufacturer,
                        Specification = g.Goods?.Specification,
                        Trader = g.Goods?.Trader,
                        UsedUnit = g.Goods?.UsedUnit,
                        Conversion = g.Goods?.Conversion ?? 1.0,
                        Filter = g.Goods?.Filter,
                        KitQty = null,
                        BatchNumber = string.Empty,
                        ExpiredDate = DateTime.MaxValue.Date,
                        QtyExisted = 0,  // 不在本药柜的物品, 现存量都标记为 0
                        IsCurrent = false,
                        Computer = others.SelectMany(o => o.Cabinets).Where(c => c.Drawers.Any(d => d.Boxes.Any(b => b.Fills.Any(f => f.GoodsId == g.GoodsId)))).Select(c => c.Computer).FirstOrDefault(),
                        DisplayText = others.SelectMany(c => c.Cabinets).Concat(others.SelectMany(c => c.OutOfCabinets)).Where(c => c.Drawers.Any(d => d.Boxes.Any(b => b.Fills.Any(f => f.GoodsId == g.GoodsId)))).Select(c => c.DisplayText).FirstOrDefault(),
                        Background = category?.Background,
                        Foreground = category?.Foreground,
                    };
                }).ToArray();
                return outsData;
            }
        }

        /// <summary>
        ///     保存预支记录
        /// </summary>
        [HttpPut]
        [ActionName("modify-prescriptions")]
        public async Task<string[]> ModifyPrescriptionsAsync([FromBody] Prescription[] prescriptions, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;

            foreach (var pre in prescriptions)
            {
                pre.UniqueId = pre.UniqueId ?? SfraObject.GenerateId();
                pre.Computer = pre.Computer ?? Terminal;
                pre.GoodsBarcodes = pre.GoodsBarcodes ?? new List<string>();
                pre.DepartmentDestinationId = DepartmentId;
                pre.DepartmentSourceId = DepartmentId;
                pre.CustomerId = pre.CustomerId ?? mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).Select(c => c.OwnerCode).FirstOrDefault();
                pre.Doctor = mongo.EmployeeCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == pre.DoctorId);
                pre.Patient = mongo.PatientCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == pre.PatientId);
                pre.Goods = mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == pre.GoodsId);
                pre.RetriesNumber = 0;
                await mongo.PrescriptionCollection.FindOneAndReplaceAsync<Prescription>(x => x.UniqueId == pre.UniqueId, pre, new FindOneAndReplaceOptions<Prescription, Prescription> { IsUpsert = true });
            }
            mongo.OperationScheduleCollection.UpdateOne(os => os.UniqueId == prescriptions.FirstOrDefault().OperationScheduleId, Builders<OperationSchedule>.Update.Set(os => os.ExecutionEndTime, DateTime.Now));
            return prescriptions.Select(x => x.UniqueId).ToArray();
        }
        [NonAction]
        internal List<string> SearchPermitGoods(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var allGoods = mongo.GoodsCollection.AsQueryable().Where(g => !g.IsDisabled).Select(g => new { g.UniqueId, g.Filter, }).ToList();

            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal);
            var secondary = ServiceStartup.GetSecondaryAuthorized(Terminal);
            return Gds(primary?.LoginId, primary?.Kernel ?? false).Concat(Gds(secondary?.LoginId, secondary?.Kernel ?? false)).Distinct().ToList();

            List<string> Gds(string user, bool kernel)
            {
                if (kernel)
                {
                    return allGoods.Select(g => g.UniqueId).ToList();
                }

                var key = $"{user}:{Helper.AllowedGoods}";
                var filters = mongo.SystemConfigCollection.AsQueryable().Where(c => c.Key == key).Select(c => c.JObject).ToArray().SelectMany(c => JsonConvert.DeserializeObject<string[]>(c)).ToList();
                // 默认可以使用所有物品 
                return filters.Any() ? filters.Join(allGoods, a => a, b => b.Filter, (a, b) => b.UniqueId).ToList() : allGoods.Select(g => g.UniqueId).ToList();
            }
        }

        public class KitProfile : IdName
        {
            public IEnumerable<KitDetail> Kits { get; set; }
        }

        public class KitDetail
        {
            public string GoodsId { get; set; }
            public double Qty { get; set; }
        }

        [HttpGet]
        [ActionName("search-kits-for-logged-in")]
        public KitProfile[] SearchKitsForLoggedIn(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal)?.LoginId;
            var secondary = ServiceStartup.GetSecondaryAuthorized(Terminal)?.LoginId;
            return mongo.KitCollection.AsQueryable().Where(k => !k.IsDisabled && k.Kits.Count > 0 && (k.CreatorId == primary || k.CreatorId == secondary))
                .OrderBy(k => k.DisplayOrder)
                .Select(k => new KitProfile
                {
                    UniqueId = k.UniqueId,
                    DisplayName = k.DisplayName,
                    Kits = k.Kits.Select(o => new KitDetail { GoodsId = o.GoodsId, Qty = o.Qty, }),
                }).ToArray();
        }

        [ActionName("search-kit-goods")]
        [HttpGet]
        public TerminalGoodsProfile[] SearchKitGoods(string kit, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;

            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).Select(c => new
            {
                Cabinets = c.Cabinets.Where(o => o.Computer == Terminal),
                OutOfCabinets = c.OutOfCabinets.Where(o => o.DepartmentId == department),
            }).ToList();

            var find = mongo.KitCollection.AsQueryable().Where(k => !k.IsDisabled && k.UniqueId == kit).FirstOrDefault() ?? new Kit();
            var goodsIds = find.Kits.Select(k => k.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => !g.IsDisabled && goodsIds.Contains(g.UniqueId)).ToList();

            var categories = mongo.GoodsCategoryCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
            return find.Kits.Select(k =>
            {
                k.Goods = goods.FirstOrDefault(f => f.UniqueId == k.GoodsId);
                var qtySum = cs.SelectMany(c => c.Cabinets).Concat(cs.SelectMany(c => c.OutOfCabinets)).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).Where(f => f.GoodsId == k.GoodsId).Sum(f => f.QtyExisted);
                var category = categories.FirstOrDefault(f => f.GoodsKeys.Any(o => o == k.GoodsId));
                return new TerminalGoodsProfile
                {
                    UniqueId = k.GoodsId,
                    DisplayName = k.Goods?.DisplayName,
                    GoodsType = k.Goods?.GoodsType,
                    Manufacturer = k.Goods?.Manufacturer,
                    Specification = k.Goods?.Specification,
                    Trader = k.Goods?.Trader,
                    UsedUnit = k.Goods?.UsedUnit,
                    Conversion = k.Goods?.Conversion ?? 1.0,
                    Filter = k.Goods?.Filter,
                    BatchNumber = string.Empty,
                    ExpiredDate = DateTime.MaxValue.Date,
                    QtyExisted = qtySum,
                    KitQty = k.Qty,
                    IsCurrent = true,
                    Computer = Terminal,
                    DisplayText = cs.SelectMany(c => c.Cabinets).Concat(cs.SelectMany(c => c.OutOfCabinets)).Where(c => c.Drawers.Any(d => d.Boxes.Any(b => b.Fills.Any(f => f.GoodsId == k.GoodsId)))).Select(c => c.DisplayText).FirstOrDefault(),
                    Background = category?.Background,
                    Foreground = category?.Foreground,
                };
            }).ToArray();
        }

        /// <summary>
        ///     查询手术已经预支的物品
        /// </summary>
        /// <param name="operation">手术排班</param>
        /// <param name="terminal"></param>
        /// <param name="doctor"></param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-prescription-profiles-by-operation-schedule")]
        public AdvanceItems[] SearchPrescriptionProfilesByOperationSchedule(string operation, string terminal = null, string doctor = null)
        {
            Terminal = terminal ?? Terminal;

            var prescriptions = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && p.Mode == ExchangeMode.CheckOut && p.OperationScheduleId == operation).ToList();
            var medications = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && m.OperationScheduleId == operation && (m.DoctorId == doctor || doctor == null)).ToList();
            var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
            return GetAdvanceItems(prescriptions, medications, customers);
        }

        /// <summary>
        /// 获取预支记录
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <param name="medications"></param>
        /// <param name="customers"></param>
        /// <returns></returns>
        private AdvanceItems[] GetAdvanceItems(List<Prescription> prescriptions, List<Medication> medications, List<Customer> customers)
        {
            var executes = new List<AdvanceItems>();
            foreach (var item in medications.GroupBy(m => new { m.DoctorId, m.OperationScheduleId, m.PatientId }))
            {
                var mm = item.Select(d => d as Medication).ToList();
                var cs = customers.Select(c => new
                {
                    Cabinets = c.Cabinets.Where(o => o.Computer == mm.FirstOrDefault()?.Computer),
                }).SelectMany(c => c.Cabinets).SelectMany(c => c.Drawers).SelectMany(c => c.Boxes).SelectMany(c => c.Fills);
                var goodsId = cs.Select(f => { f.BatchNumber = f.BatchNumber ?? string.Empty; return f; }).GroupBy(g => new { g.GoodsId, g.BatchNumber, g.ExpiredDate });
                foreach (var g in item.Select(d => d as Medication).SelectMany(d => d.Plans).SelectMany(p => p.Box.Fills).Select(f => { f.BatchNumber = f.BatchNumber?.ToUpper() ?? string.Empty; return f; }).GroupBy(g => new { g.GoodsId, g.BatchNumber, g.ExpiredDate }))
                {
                    var groupAll = medications.SelectMany(d => d.Plans).Where(p => p.Box.Fills.Intersect(g).Any()).Select(p => { p.Qty *= p.Mode == ExchangeMode.CheckIn ? -1.0 : 1.0; return p; }).ToArray();
                    var ps = prescriptions.Where(p => p.DoctorId == item.Key.DoctorId && p.GoodsId == g.Key.GoodsId && p.BatchNumber == g.Key.BatchNumber && p.ExpiredDate == g.Key.ExpiredDate).ToList();
                    foreach (var p in ps)                 // 医嘱有多少条就显示多少条
                    {
                        var pd = groupAll.Where(m => m.CreatedTime < p.CreatedTime).ToArray();
                        if (pd.Sum(d => d.Qty) == p.QtyActual)
                        {
                            groupAll = groupAll.Except(pd).ToArray();
                            executes.Add(new AdvanceItems
                            {
                                PrescriptionId = p.UniqueId,
                                Goods = (p?.Goods ?? new Goods { UniqueId = g.Key.GoodsId, }).ToGoodsProfileQty(g.Key.BatchNumber, g.Key.ExpiredDate.Date, p.QtyActual),
                                Doctor = (p?.Doctor ?? new Employee { UniqueId = p?.DoctorId, }).ToEmployeeProfile(),
                                Patient = (p?.Patient ?? new Patient { UniqueId = p?.PatientId, }).ToPatientProfile(),
                                RetriesNumber = p.RetriesNumber,
                                RecordType = p.RecordType,
                                CreatedTime = p.CreatedTime,
                                IsSynchronized = p.IsSynchronized,
                                RoomId = p.Computer,
                                IsEntry = goodsId.Where(d => d.Key.GoodsId == g.Key.GoodsId && d.Key.BatchNumber == g.Key.BatchNumber && d.Key.ExpiredDate == g.Key.ExpiredDate).Any(),
                                Barcodes = p.GoodsBarcodes.ToArray(),
                            });
                        };
                    }
                    var gdm = medications.FirstOrDefault(m => m.Plans.Intersect(groupAll).Count() > 0);
                    executes.Add(new AdvanceItems                     // 预支汇总显示
                    {
                        Goods = (gdm?.Goods ?? new Goods { UniqueId = g.Key.GoodsId, }).ToGoodsProfileQty(g.Key.BatchNumber, g.Key.ExpiredDate.Date, groupAll.Sum(o => o.Qty)),
                        Doctor = (gdm?.Doctor ?? new Employee { UniqueId = gdm?.DoctorId, }).ToEmployeeProfile(),
                        Patient = (gdm?.Patient ?? new Patient { UniqueId = gdm?.PatientId, }).ToPatientProfile(),
                        RecordType = gdm?.RecordType,
                        CreatedTime = gdm?.CreatedTime,
                        RoomId = gdm?.Computer,
                        IsEntry = goodsId.Where(d => d.Key.GoodsId == g.Key.GoodsId && d.Key.BatchNumber == g.Key.BatchNumber && d.Key.ExpiredDate == g.Key.ExpiredDate).Any(),
                    });
                }
            }
            return executes.Where(d => d.Goods.Qty > 0).ToArray();
        }
        /// <summary>
        ///     手术排班柜外录入修改
        /// </summary>
        [HttpPut]
        [ActionName("modify-medications-for-outside-qty-sum")]
        public async Task<string[]> ModifyMedicationsForOutsideQtySumAsync(string hospital, [FromBody] Medication[] medications, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var customer = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).Select(c => c.OwnerCode).FirstOrDefault();
            var ops = mongo.MedicationCollection.AsQueryable().Where(d => !d.IsDisabled).ToList();
            foreach (var m in medications)
            {
                m.Plans = new List<ActionPlan>
                {
                    new ActionPlan
                    {
                        Mode = m.Mode,
                        Box = new BoxDevice
                        {
                            BoxMode = BoxMode.VirtualBox,
                            DisplayText = "虚拟位置",
                            IsControlled = false,
                            Fills = new List<NodeGoodsInfo>
                            {
                                new NodeGoodsInfo
                                {
                                    GoodsId = m.GoodsId,
                                    QtyExisted = m.Qty,
                                    BatchNumber = m.BatchNumber,
                                    ExpiredDate = m.ExpiredDate,
                                    QtyMax = m.Qty,
                                },
                            },
                        },
                        Qty = m.Qty,
                        IsExecuted = true,
                    },
                };
                m.UniqueId = m.UniqueId ?? SfraObject.GenerateId();
                m.CustomerId = customer;
                m.Computer = Terminal;
                m.Doctor = string.IsNullOrEmpty(m.DoctorId) ? null : mongo.EmployeeCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == m.DoctorId);
                m.Patient = string.IsNullOrEmpty(m.PatientId) ? null : mongo.PatientCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == m.PatientId);
                m.Goods = string.IsNullOrEmpty(m.GoodsId) ? null : mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == m.GoodsId);
                m.BatchNumber = m.BatchNumber ?? string.Empty;
                ops = ops.Where(d => d.OperationScheduleId == m.OperationScheduleId && d.DoctorId == m.DoctorId).ToList();
                var ngi = ops.SelectMany(o => o.Plans).SelectMany(p => p.Box.Fills).ToList().Select(f => { f.BatchNumber = f.BatchNumber?.ToUpper() ?? string.Empty; return f; }).Where(f => f.GoodsId == m.GoodsId && f.BatchNumber == m.BatchNumber?.ToUpper() && f.ExpiredDate == m.ExpiredDate).ToList();
                var find = ops.Where(f => f.Plans.ToList().Intersect(ops.SelectMany(o => o.Plans).Where(o => o.Box.Fills.Intersect(ngi).Count() > 0)).Count() > 0).OrderByDescending(o => o.CreatedTime).ToList();
                var prescriptions = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && p.OperationScheduleId == m.OperationScheduleId && p.DoctorId == m.DoctorId && p.GoodsId == m.GoodsId && p.ExpiredDate == m.ExpiredDate && p.BatchNumber == m.BatchNumber).ToList();
                if (find.Count() > 0 && find.Sum(f => f.QtyActual) != prescriptions.Sum(p => p.QtyActual))
                {
                    m.Qty = m.Qty + find.FirstOrDefault().QtyActual;
                    m.Plans = new List<ActionPlan>
                    {
                        new ActionPlan
                        {
                            Mode = m.Mode,
                            Box = new BoxDevice
                            {
                                BoxMode = BoxMode.VirtualBox,
                                DisplayText = "虚拟位置",
                                IsControlled = false,
                                Fills = new List<NodeGoodsInfo>
                                {
                                    new NodeGoodsInfo
                                    {
                                        GoodsId = m.GoodsId,
                                        QtyExisted = m.Qty,
                                        BatchNumber = m.BatchNumber,
                                        ExpiredDate = m.ExpiredDate,
                                        QtyMax = m.Qty,
                                    },
                                },
                            },
                            Qty = m.Qty,
                            IsExecuted = true,
                        },
                    };
                    await mongo.MedicationCollection.UpdateOneAsync(o => o.UniqueId == find.FirstOrDefault().UniqueId, Builders<Medication>.Update.Set(o => o.Plans, m.Plans).Set(o => o.QtyActual, m.Qty).Set(o => o.IsDisabled, m.Qty <= 0));
                }
                else
                {
                    await mongo.MedicationCollection.FindOneAndReplaceAsync<Medication>(x => x.UniqueId == m.UniqueId, m, new FindOneAndReplaceOptions<Medication, Medication> { IsUpsert = true });
                }
            }
            return medications.Select(o => o.UniqueId).ToArray();
        }
        /// <summary>
        ///     修改手术排班的手术室，已经修改的不能修改
        /// </summary>
        /// <param name="patient">患者id，住院号，门诊号，身份条码，就诊卡号</param>
        /// <param name="date">某一天</param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-patient-room-by-visitor")]
        public bool ModifyPatientRoomByVisitor(string patient, DateTime date, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;

            var from = date.Date;
            var to = date.AddDays(1).Date;
            mongo.OperationScheduleCollection.UpdateMany(os => (os.PatientId == patient
                || os.Patient.Hospitalization.HospitalNumber == patient
                || os.Patient.Clinic.SerialNumber == patient
                || os.IdentityBarcode == patient
                || os.Patient.RegisterNumber == patient
            ) && os.RoomId == null && os.ApplyTime >= from && os.ApplyTime < to, Builders<OperationSchedule>.Update.Set(os => os.RoomId, department));
            return true;
        }

        /// <summary>
        ///     查询存储指定 filter 的物品的抽屉的编号
        /// </summary>
        [HttpGet]
        [ActionName("search-drawers-for-goods")]
        public string[] SearchDrawersForGoods(string filter, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var boxes = SearchPermitBoxes(Terminal);

            var goodsIds = mongo.GoodsCollection.AsQueryable().Where(g => !g.IsDisabled && g.Filter == filter).Select(g => g.UniqueId).ToList();
            var cabinets = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).ToList();
            // 抽屉中有相关的物品 且 当前登录人有硬件权限
            var drawers = cabinets.SelectMany(c => c.Drawers).Where(d => d.Boxes.Any(b => b.Fills.Join(goodsIds, x => x.GoodsId, y => y, (x, y) => x).Any()) && d.Boxes.Join(boxes, a => a.No, b => b, (a, b) => a).Any());
            return drawers.Select(d => d.No).OrderBy(n => n).Distinct().ToArray();
        }

        // searchPosition

        // modifyQtyForCabinet

        [HttpGet]
        [ActionName("search-goods-by-barcode")]
        public GoodsProfile SearchGoodsByBarcode(string barcode)
        {
            var config = mongo.SystemConfigCollection.AsQueryable().Where(s => s.Key == $"{barcode}:GoodsBarcode").Select(s => s.JObject).FirstOrDefault();
            return config == null ? null : mongo.GoodsCollection.AsQueryable().Where(g => g.UniqueId == config).ToList().Select(g => g.ToGoodsProfile(string.Empty, DateTime.MaxValue.Date)).FirstOrDefault();
        }
    }
}

// temp-return-for-patient
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class PatientReturn
        {
            public EmployeeProfile Doctor { get; set; }
            public PatientProfile Patient { get; set; }
            public List<ExecuteReturn> Executes { get; set; }
        }

        public class ExecuteReturn
        {
            public string[] Medications { get; set; }
            public double[] QtyActuals { get; set; }
            public GoodsProfile Goods { get; set; }
            public double Qty { get; set; }
            public string[] Barcodes { get; set; }
            public DateTime?[] FinishTimes { get; set; }
        }

        public class AdvanceItems
        {
            public string RoomId { get; set; }
            public string PrescriptionId { get; set; }
            public GoodsProfileQty Goods { get; set; }
            public PatientProfile Patient { get; set; }
            public EmployeeProfile Doctor { get; set; }
            public string RecordType { get; set; }
            public int RetriesNumber { get; set; }
            public bool IsEntry { get; set; }
            public string[] Barcodes { get; set; }
            public bool IsSynchronized { get; set; }
            public DateTime? CreatedTime { get; set; }
        }

        private async Task<List<Medication>> JudgeBalancesPrivateAsync(List<Medication> preTreats)
        {
            foreach (var item in preTreats)
            {
                item.CheckOutIds = item.CheckOutIds ?? new string[0];
                item.Plans = item.Plans ?? new List<ActionPlan>();
            }

            // 判断预支是否已经平衡, 规则:
            // 取药数量 = 退药数量 + 药方数量
            var balances = preTreats.Where(p => p.Mode == ExchangeMode.CheckIn || p.Mode == ExchangeMode.Medication).SelectMany(p =>
            {
                var meds = preTreats.Where(o => o.Mode == ExchangeMode.Medication && o.CheckInId == p.UniqueId);
                var outIds = p.CheckOutIds.Concat(meds.SelectMany(o => o.CheckOutIds)).Distinct().ToArray();
                var outs = preTreats.Where(x => x.Mode == ExchangeMode.CheckOut).Join(outIds, a => a.UniqueId, b => b, (a, b) => a).ToArray();
                var outQty = outs.SelectMany(o => o.Plans).Where(o => o.IsExecuted).Sum(x => x.Qty);
                p.InOutBalance = outQty == p.QtyActual + meds.Sum(x => x.QtyActual);

                // MERGE
                var quantity = p.QtyActual + meds.Sum(x => x.QtyActual);
                foreach (var checkOut in outs)
                {
                    foreach (var plan in checkOut.Plans.Where(o => o.IsExecuted))
                    {
                        var qty = Math.Min(plan.Qty, quantity);
                        plan.Qty -= qty;
                        checkOut.QtyActual -= qty;
                        checkOut.Qty -= qty;
                        quantity -= qty;
                    }
                    checkOut.Plans = checkOut.Plans.Where(o => o.Qty > 0 && o.IsExecuted).ToList();
                    checkOut.InOutBalance = p.InOutBalance;
                }

                return p.InOutBalance == true ? outIds : new string[0];
            }).Distinct().ToList();

            if (balances.Any())
            {
                await mongo.MedicationCollection.UpdateManyAsync(m => balances.Contains(m.UniqueId), Builders<Medication>.Update.Set(m => m.InOutBalance, true));
            }

            return preTreats.Where(p => p.InOutBalance != true && p.Mode == ExchangeMode.CheckOut).ToList();
        }

        /// <summary>
        ///     获取所有预支结果 或 指定患者的预支结果
        /// </summary>
        [HttpGet]
        [ActionName("search-patients-for-return")]
        public async Task<PatientReturn[]> SearchPatientsForReturnAsync(string patient = null, string terminal = null)
        {
            Terminal = terminal ?? Terminal;

            var preTreats = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && m.InOutBalance != true && Terminal == m.Computer).ToList();
            var checkOuts = await JudgeBalancesPrivateAsync(preTreats);
            var medications = checkOuts.Where(m => string.IsNullOrEmpty(patient) || patient == m.PatientId).ToList();

            var users = ServiceStartup.GetAuthorized(Terminal).Select(o => o.UserId).ToList();
            var data = medications.GroupBy(m => new { m.DoctorId, m.PatientId, }).Select(gp =>
            {
                var ds = gp.SelectMany(m =>
                {
                    return m.Plans?.Any() == true
                        ? m.Plans.Where(p => p.IsExecuted).Select(p =>
                            {
                                var fill = p.Box.Fills.First(f => f.GoodsId == m.GoodsId);
                                return new { p.Qty, BatchNumber = fill.BatchNumber ?? string.Empty, ExpiredDate = fill.ExpiredDate.Date, };
                            }).GroupBy(f => new { f.BatchNumber, f.ExpiredDate, }).Select(g => new
                            {
                                Medication = m.UniqueId,
                                m.GoodsId,
                                Qty = g.Sum(o => o.Qty),
                                g.Key.BatchNumber,
                                g.Key.ExpiredDate,
                                m.FinishTime,
                            })
                        : new[] { new { Medication = m.UniqueId, m.GoodsId, Qty = m.QtyActual, BatchNumber = m.BatchNumber ?? string.Empty, ExpiredDate = m.ExpiredDate.Date, m.FinishTime, } };
                }).GroupBy(x => new { x.GoodsId, x.BatchNumber, x.ExpiredDate }).Select(g =>
                {
                    var gd = medications.FirstOrDefault(f => f.GoodsId == g.Key.GoodsId && f.Goods != null)?.Goods ?? new Goods { UniqueId = g.Key.GoodsId, };
                    return new ExecuteReturn
                    {
                        Goods = gd.ToGoodsProfile(g.Key.BatchNumber, g.Key.ExpiredDate.Date),
                        Medications = g.Select(o => o.Medication).ToArray(),
                        QtyActuals = g.Select(o => o.Qty).ToArray(),
                        Qty = g.Sum(o => o.Qty),
                        FinishTimes = g.Select(o => o.FinishTime).ToArray(),
                        Barcodes = medications.Where(m => m.GoodsId == g.Key.GoodsId).SelectMany(m => m.GoodsBarcodes).OrderBy(b => b).ToArray(),
                    };
                }).ToList();

                return new PatientReturn
                {
                    Doctor = (gp.First().Doctor ?? new Employee { UniqueId = gp.Key.DoctorId, }).ToEmployeeProfile(),
                    Patient = (gp.First().Patient ?? new Patient { UniqueId = gp.Key.PatientId, }).ToPatientProfile(),
                    Executes = ds,
                };
            }).Where(x => x.Executes.Any()).ToArray();
            return data.Where(d => users.Any(u => u == d.Doctor.UniqueId)).Concat(data.Where(d => users.Any(u => u != d.Doctor.UniqueId))).ToArray();
        }

        // modifyMedications
    }

}

// ampoule-recycle
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class RecycleAmpouleBack
        {
            public bool NonClinical { get; set; }
            public RecycleAmpoule[] RecycleAmpoules { get; set; }
        }

        public class RecycleAmpoule
        {
            [JsonProperty("_id")]
            public string UniqueId { get; set; }
            public DateTime IssuedTime { get; set; }
            public DepartmentProfile Department { get; set; }
            public EmployeeProfile Doctor { get; set; }
            public PatientProfile Patient { get; set; }
            public GoodsProfile Goods { get; set; }
            public double Qty { get; set; }
            public string Collection { get; set; }
            public string[] Ampoules { get; set; }
        }

        [HttpGet]
        [ActionName("search-recycle-ampoules")]
        public RecycleAmpouleBack SearchRecycleAmpoules(bool ampoule, string department)
        {
            var computers = Computers(department);
            var nonClinical = computers.Any(c => (mongo.SystemConfigCollection.AsQueryable().Where(s => s.Key == $"{c}:NonClinical").Select(s => s.JObject).FirstOrDefault() ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase));

            // 查询医嘱、调拨、预支 中包含空安瓿的
            var pres = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && p.QtyActual > 0 && (p.Mode == ExchangeMode.CheckIn || p.Mode == ExchangeMode.CheckOut) && !p.FinishedAmpoule && p.Goods.IsAmpoule == ampoule && (nonClinical || department == p.DepartmentDestinationId)).ToList();
            var alos = mongo.AllocationCollection.AsQueryable().Where(a => !a.IsDisabled && a.QtyActual > 0 && (a.Mode == ExchangeMode.CheckIn || a.Mode == ExchangeMode.CheckOut) && !a.FinishedAmpoule && a.Goods.IsAmpoule == ampoule && (nonClinical || department == a.DepartmentSourceId)).ToList();
            var meds = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && m.QtyActual > 0 && (m.Mode == ExchangeMode.CheckIn || m.Mode == ExchangeMode.CheckOut) && !m.FinishedAmpoule && m.Goods.IsAmpoule == ampoule && (nonClinical || computers.Contains(m.Computer))).ToList();

            var prs = pres.Where(p => p.Mode == ExchangeMode.CheckOut).SelectMany(p =>
            {
                var inActions = pres.Where(o => o.Mode == ExchangeMode.CheckIn && o.DoctorId == p.DoctorId && o.PatientId == p.PatientId && o.GoodsId == p.GoodsId).SelectMany(o => o.Plans ?? new List<ActionPlan>()).Where(o => o.IsExecuted).ToList();
                var plans = (p.Plans ?? new List<ActionPlan>()).Where(o => o.IsExecuted).GroupBy(o =>
                {
                    var fill = o.Box.Fills.First(f => f.GoodsId == p.GoodsId);
                    return new { fill.BatchNumber, fill.ExpiredDate, };
                }).Select(o => new
                {
                    o.Key.BatchNumber,
                    o.Key.ExpiredDate,
                    // 1. 减去退药医嘱的数量 (医嘱，医生、患者、药品相等，即为对应的退药医嘱)
                    Qty = o.Sum(x => x.Qty) - inActions.Where(f => f.Box.Fills.Any(x => x.BatchNumber == o.Key.BatchNumber && x.ExpiredDate == o.Key.ExpiredDate)).Sum(a => a.Qty),
                }).ToList();
                return plans.Select(o =>
                {
                    // 2. 减去已经回收了的空安瓿数量
                    var qty = o.Qty - (p.AssignAmpouleRecords ?? new List<Exchange.AmpouleRecord>()).Where(a => a.BatchNumber == o.BatchNumber && a.ExpiredDate == o.ExpiredDate).Sum(a => a.Qty);
                    return new { p.UniqueId, p.DepartmentDestinationId, p.DoctorId, p.PatientId, p.GoodsId, Qty = qty, o.BatchNumber, o.ExpiredDate, p.IssuedTime, Collection = nameof(Prescription), };
                }).Where(o => o.Qty > 0);


            }).ToList();

            var prsp = pres.Where(p => p.Mode == ExchangeMode.CheckOut && p.Plans == null && p.QtyActual > 0).Select(p =>
            {
                return new { p.UniqueId, p.DepartmentDestinationId, p.DoctorId, p.PatientId, p.GoodsId, Qty = p.QtyActual, p.BatchNumber, p.ExpiredDate, p.IssuedTime, Collection = nameof(Prescription), };
            }).Where(o => o.Qty > 0).ToList();

            var als = alos.Where(a => a.Mode == ExchangeMode.CheckOut).SelectMany(a =>
             {
                var inActions = alos.Where(o => o.Mode == ExchangeMode.CheckIn && o.GoodsId == a.GoodsId).SelectMany(o => o.Plans ?? new List<ActionPlan>()).Where(o => o.IsExecuted).ToList();

                var plans = (a.Plans ?? new List<ActionPlan>()).Where(o => o.IsExecuted).GroupBy(o =>
                {
                    var fill = o.Box.Fills.First(f => f.GoodsId == a.GoodsId);
                    return new { fill.BatchNumber, fill.ExpiredDate, };
                }).Select(o => new
                {
                    o.Key.BatchNumber,
                    o.Key.ExpiredDate,
                    Qty = o.Sum(x => x.Qty) - inActions.Where(f => f.Box.Fills.Any(x => x.BatchNumber == o.Key.BatchNumber && x.ExpiredDate == o.Key.ExpiredDate)).Sum(f => f.Qty),
                }).ToList();
                return plans.Select(o =>
                {
                    var qty = o.Qty - (a.AssignAmpouleRecords ?? new List<Exchange.AmpouleRecord>()).Where(p => p.BatchNumber == o.BatchNumber && p.ExpiredDate == o.ExpiredDate).Sum(p => p.Qty);
                    return new { a.UniqueId, a.DepartmentDestinationId, DoctorId = (string)null, PatientId = (string)null, a.GoodsId, Qty = qty, o.BatchNumber, o.ExpiredDate, IssuedTime = a.CreatedTime, Collection = nameof(Allocation) };
                }).Where(o => o.Qty > 0);
              }).ToList();

            var mds = meds.Where(m => m.Mode == ExchangeMode.CheckOut).SelectMany(m =>
            {
                var inActions = meds.Where(o => o.Mode == ExchangeMode.CheckIn && o.GoodsId == m.GoodsId).SelectMany(o => o.Plans ?? new List<ActionPlan>()).Where(o => o.IsExecuted).ToList();

                var plans = (m.Plans ?? new List<ActionPlan>()).Where(o => o.IsExecuted).GroupBy(o =>
                {
                    var fill = o.Box.Fills.First(f => f.GoodsId == m.GoodsId);
                    return new { fill.BatchNumber, fill.ExpiredDate, };
                }).Select(o => new
                {
                    o.Key.BatchNumber,
                    o.Key.ExpiredDate,
                    // 1. 减去调拨入库的数量 (医嘱，医生、患者、药品相等，即为对应的退药医嘱)
                    Qty = o.Sum(x => x.Qty) - inActions.Where(f => f.Box.Fills.Any(x => x.BatchNumber == o.Key.BatchNumber && x.ExpiredDate == o.Key.ExpiredDate)).Sum(a => a.Qty),
                }).ToList();

                return plans.Select(o =>
                {
                    // 2. 减去已经回收了的空安瓿数量
                    var qty = o.Qty - (m.AssignAmpouleRecords ?? new List<Exchange.AmpouleRecord>()).Where(a => a.BatchNumber == o.BatchNumber && a.ExpiredDate == o.ExpiredDate).Sum(p => p.Qty);
                    return new { m.UniqueId, DepartmentDestinationId = department, m.DoctorId, m.PatientId, m.GoodsId, Qty = qty, o.BatchNumber, o.ExpiredDate, IssuedTime = m.CreatedTime, Collection = nameof(Medication) };
                }).Where(o => o.Qty > 0);
            }).ToList();

            var data = prs.Concat(prsp).Concat(als).Concat(mds).OrderBy(x => x.IssuedTime).ToList();

            var finds = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).ToList();
            var departIds = data.Select(d => d.DepartmentDestinationId).Where(d => !string.IsNullOrEmpty(d)).Distinct().ToArray();
            var departs = mongo.DepartmentCollection.AsQueryable().Where(d => departIds.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).ToList().Select(d => new DepartmentProfile
            {
                UniqueId = d.UniqueId,
                DisplayName = d.DisplayName,
                Code = d.Code,
                Computer = finds.Where(f => f.DepartmentId == d.UniqueId).Select(f => f.Computer).FirstOrDefault(),
            }).ToList();

            var doctorIds = data.Select(d => d.DoctorId).Distinct().ToList();
            var doctors = mongo.EmployeeCollection.AsQueryable().Where(e => doctorIds.Contains(e.UniqueId)).ToList().Select(o => o.ToEmployeeProfile()).ToList();
            var patientIds = data.Select(d => d.PatientId).Distinct().ToList();
            var patients = mongo.PatientCollection.AsQueryable().Where(p => patientIds.Contains(p.UniqueId)).ToList().Select(o => o.ToPatientProfile()).ToList();
            var goodsIds = data.Select(d => d.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).ToList().Select(g => g).ToList();

            return new RecycleAmpouleBack
            {
                NonClinical = nonClinical,
                RecycleAmpoules = data.Select(ct =>
                {
                    var dt = doctors.FirstOrDefault(d => d.UniqueId == ct.DoctorId) ?? new EmployeeProfile { UniqueId = ct.DoctorId, };
                    var pt = patients.FirstOrDefault(p => p.UniqueId == ct.PatientId) ?? new PatientProfile { UniqueId = ct.PatientId, };
                    var gd = goods.FirstOrDefault(g => g.UniqueId == ct.GoodsId) ?? new Goods { UniqueId = ct.GoodsId };
                    return new RecycleAmpoule
                    {
                        UniqueId = ct.UniqueId,
                        Department = departs.FirstOrDefault(o => o.UniqueId == ct.DepartmentDestinationId) ?? new DepartmentProfile { UniqueId = ct.DepartmentDestinationId, },
                        Goods = gd.ToGoodsProfile(ct.BatchNumber, ct.ExpiredDate.Date),
                        Doctor = dt,
                        Patient = pt,
                        Qty = ct.Qty,
                        IssuedTime = ct.IssuedTime,
                        Collection = ct.Collection,
                    };
                }).ToArray(),
            };
        }

        // modifyAmpouleRecords

        [HttpGet]
        [ActionName("search-un-destory-ampoules")]
        public RecycleAmpoule[] SearchUnDestoryAmpoules(string department)
        {
            var computers = Computers(department);
            var nonClinical = computers.Any(c => (mongo.SystemConfigCollection.AsQueryable().Where(s => s.Key == $"{c}:NonClinical").Select(s => s.JObject).FirstOrDefault() ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase));

            var amps = mongo.AmpouleCollection.AsQueryable().Where(a => !a.IsDisabled && !a.FinishedDestory && (nonClinical || computers.Contains(a.Computer))).Select(a => new { a.UniqueId, a.GoodsId, a.BatchNumber, a.ExpiredDate, a.ActualQty, }).ToList();
            var goodsIds = amps.Select(a => a.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).ToList().Select(g => g.ToGoodsProfile(string.Empty, DateTime.MaxValue.Date)).ToList();

            return amps.GroupBy(a => new { a.GoodsId, BatchNumber = a.BatchNumber ?? string.Empty, ExpiredDate = a.ExpiredDate.Date, })
                 .Select(gp =>
                 {
                     var g = goods.FirstOrDefault(x => x.UniqueId == gp.Key.GoodsId) ?? new GoodsProfile { UniqueId = gp.Key.GoodsId };
                     g.BatchNumber = gp.Key.BatchNumber;
                     g.ExpiredDate = gp.Key.ExpiredDate;
                     return new RecycleAmpoule
                     {
                         Goods = g,
                         Ampoules = gp.Select(x => x.UniqueId).ToArray(),
                         Qty = gp.Sum(x => x.ActualQty),
                     };
                 }).ToArray();
        }

        [HttpPut]
        [ActionName("modify-destory-ampoules")]
        public async Task<bool> ModifyDestoryAmpoulesAsync(string goods, string batch, DateTime expired, double qty, string address, string mode, [FromBody] string[] ampoules, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal);
            var secondary = ServiceStartup.GetSecondaryAuthorized(Terminal);

            var des = new Destory
            {
                GoodsId = goods,
                Goods = null,
                BatchNumber = batch ?? string.Empty,
                ExpiredDate = expired,
                DestoryQty = qty,
                Ampoules = ampoules.ToList(),
                Computer = Terminal,
                ExecutorId = primary.LoginId,
                ExecutorName = primary.DisplayName,

                Address = address,
                DestroyMode = mode,
                SupervisorId = secondary?.LoginId,
                SupervisorName = secondary?.DisplayName,
            };
            await mongo.DestoryCollection.InsertOneAsync(des);
            await mongo.AmpouleCollection.UpdateManyAsync(a => ampoules.Contains(a.UniqueId), Builders<Ampoule>.Update.Set(a => a.FinishedDestory, true));
            return true;
        }
    }
}

// fill-temp-details
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class PatientMedication
        {
            public List<AdvanceItems> AdvanceItems { get; set; }
        }
        /// <summary>
        ///     查询患者指定时间段内的预支记录
        /// </summary>
        /// <param name="departs"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="supplement">补充。预支记录需要录入HIS，同步后进行销账。 true 时仅查询未销账的记录，false 时查询所有的记录</param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpPost]
        [ActionName("search-patient-medications-by-date-range")]
        public List<AdvanceItems> SearchPatientMedicationsByDateRange([FromBody] string[] departs, DateTime start, DateTime end, bool supplement = false, string terminal = null)
        {
            Terminal = terminal ?? Terminal;

            var prescriptions = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && p.Mode == ExchangeMode.CheckOut).ToList();
            var medications = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && departs.Contains(m.Computer) && m.TimeFilter >= start && m.TimeFilter < end).ToList();
            var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
            if (supplement)
            {
                medications = medications.Where(m => !m.IsSynchronized).ToList();
            }
            return GetAdvanceItems(prescriptions, medications, customers).ToList();
        }

        [HttpGet]
        [ActionName("search-medications-by-operation-schedule")]
        public Kit[] SearchMedicationsByOperationSchedule(string schedule)
        {
            var medications = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && m.Mode == ExchangeMode.Medication && m.OperationScheduleId == schedule).OrderBy(m => m.Goods.DisplayOrder).ToList();
            var kits = mongo.KitCollection.AsQueryable().Where(k => !k.IsDisabled).ToList();
            foreach (var kit in kits)
            {
                foreach (var item in kit.Kits)
                {
                    item.Goods = medications.FirstOrDefault(o => o.Goods != null && o.GoodsId == item.GoodsId)?.Goods;
                    item.Qty = medications.Where(o => o.GoodsId == item.GoodsId).Sum(o => o.QtyActual);
                }
            }
            return kits.ToArray();
        }

        [HttpPost]
        [ActionName("sync-prescriptions")]
        public async Task<ApiBack<dynamic>> SyncPrescriptionsAsync(string hospital, [FromBody] string[] array, bool isCheckIn = false)
        {
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseProxy = false, }))
            {
                var url = $"{Hospital.ApiAddress(nameof(ISyncObject.SyncPrescriptions))}?{nameof(hospital)}={hospital}";
                var response = await client.PostAsync(url, new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>(nameof(hospital), hospital),
                    new KeyValuePair<string, string>(nameof(array), string.Join(",", array)),
                    new KeyValuePair<string, string>(nameof(isCheckIn), isCheckIn.ToString())
                }));
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ApiBack<dynamic>>(json);
            }
        }

        public class EmergencyData
        {
            public Medication Medication { get; set; }
            public string Base64Image { get; set; }
        }

        /// <summary>
        ///     急症补录照片，格式 jpg
        /// </summary>
        [HttpPut]
        [ActionName("modify-emergency-medication")]
        public async Task<string> ModifyEmergencyMedicationAsync([FromBody] EmergencyData emergency, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;

            emergency.Medication.UniqueId = emergency.Medication.UniqueId ?? SfraObject.GenerateId();
            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal);
            // 上传图片自带元数据信息
            // 部门Id_主登录人_医生_患者_物品_数量_预支记录Id 
            var file = string.Join("", $"{department}_{primary.LoginId}_{emergency.Medication.DoctorId}_{emergency.Medication.PatientId}_{emergency.Medication.GoodsId}_{emergency.Medication.QtyActual}_{emergency.Medication.UniqueId}.jpg".Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            emergency.Medication.GoodsSnapshot = Path.Combine("Upload", "EmergencyImage", file);
            var ids = await ModifyMedicationsAsync(new[] { emergency.Medication });
            if (ids.FirstOrDefault() == null)
            {
                return null;
            }

            var fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, emergency.Medication.GoodsSnapshot);
            if (!Directory.Exists(Path.GetDirectoryName(fileName)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            }

            var buffer = Convert.FromBase64String(emergency.Base64Image);
            File.WriteAllBytes(fileName, buffer);
            return emergency.Medication.UniqueId;
        }

        /// <summary>
        ///     追加空安瓿回收记录
        /// </summary>
        /// <param name="recycleAmpoule"></param>
        /// <param name="ActualQty"></param>
        /// <param name="repaird">归还空安瓿的人</param>
        /// <param name="receive">接收空安瓿的人</param>
        /// <param name="recycleType">回收类型</param>
        /// <param name="certifier"></param>
        /// <param name="raffinate"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-ampoule-records")]
        public async Task<bool> ModifyAmpouleRecordsAsync([FromBody] RecycleAmpoule[] recycleAmpoule, double ActualQty, string repaird, string recycleType, string certifier, string raffinate, string receive = null)
        {

            foreach (var item in recycleAmpoule)
            {
                Exchange find = null;
                switch (item.Collection)
                {
                    case nameof(Prescription): find = mongo.PrescriptionCollection.AsQueryable().Where(p => p.UniqueId == item.UniqueId).FirstOrDefault(); break;
                    case nameof(Medication): find = mongo.MedicationCollection.AsQueryable().Where(m => m.UniqueId == item.UniqueId).FirstOrDefault(); break;
                    case nameof(Allocation): find = mongo.AllocationCollection.AsQueryable().Where(a => a.UniqueId == item.UniqueId).FirstOrDefault(); break;
                }

                if (find == null)
                {
                    continue;
                }

                var department = DepartmentId;
                receive = receive ?? ServiceStartup.GetPrimaryAuthorized(Terminal).LoginId;
                var ampoule = new Ampoule
                {
                    Computer = find.Computer,
                    DepartmentId = department,
                    Department = null,
                    GoodsId = item.Goods.UniqueId,
                    Goods = null,
                    BatchNumber = item.Goods.BatchNumber ?? string.Empty,
                    ExpiredDate = item.Goods.ExpiredDate,
                    ExpectedQty = item.Qty,
                    ActualQty = recycleAmpoule.Length > 1 ? item.Qty : ActualQty,
                    FinishedDestory = false,
                    RepaidPerson = repaird,
                    ReceivePerson = receive,
                    Certifier = certifier,
                    Raffinate = raffinate,
                };
                await mongo.AmpouleCollection.InsertOneAsync(ampoule);

                var records = find.AssignAmpouleRecords ?? new List<Exchange.AmpouleRecord>();
                records.Add(new Exchange.AmpouleRecord { AmpouleId = ampoule.UniqueId, BatchNumber = item.Goods.BatchNumber ?? string.Empty, ExpiredDate = item.Goods.ExpiredDate, Qty = recycleAmpoule.Length > 1 ? item.Qty : ActualQty, RepaidPerson = repaird, ReceivePerson = receive, OwnerCode = item.UniqueId, RecycleType = recycleType });
                var finished = records.Sum(a => a.Qty) == find.Qty;

                switch (item.Collection)
                {
                    case nameof(Prescription): await mongo.PrescriptionCollection.UpdateOneAsync(p => p.UniqueId == item.UniqueId, Builders<Prescription>.Update.Set(p => p.AssignAmpouleRecords, records).Set(p => p.FinishedAmpoule, finished)); break;
                    case nameof(Medication): await mongo.MedicationCollection.UpdateOneAsync(p => p.UniqueId == item.UniqueId, Builders<Medication>.Update.Set(p => p.AssignAmpouleRecords, records).Set(p => p.FinishedAmpoule, finished)); break;
                    case nameof(Allocation): await mongo.AllocationCollection.UpdateOneAsync(p => p.UniqueId == item.UniqueId, Builders<Allocation>.Update.Set(p => p.AssignAmpouleRecords, records).Set(p => p.FinishedAmpoule, finished)); break;
                }
            };

            return true;
        }

        /// <summary>
        ///     追加打印记录
        /// </summary>
        [HttpPut]
        [ActionName("modify-print-records")]
        public async Task<bool> ModifyPrintRecordsAsync(string collection, [FromBody] string[] exchanges, string remark = null)
        {
            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal);
            var record = new PrintRecordInfo
            {
                Operator = mongo.EmployeeCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == primary.UniqueId) ?? new Employee { UniqueId = primary.UniqueId, DisplayName = primary.DisplayName, },
                // PrintCount = find.PrintRecords.Count + 1,
                PrintTime = DateTime.Now,
                Remark = remark,
                // UniqueId = Observable.GenerateId(),
                CreatedTime = DateTime.Now,
            };

            bool recorded = false;
            switch (collection)
            {
                case nameof(Medication):
                    {
                        var finds = mongo.MedicationCollection.AsQueryable().Where(m => exchanges.Contains(m.UniqueId)).ToArray();
                        foreach (var find in finds)
                        {
                            var printRecords = find.PrintRecords ?? new List<PrintRecordInfo>();
                            record.PrintCount = printRecords.Count + 1;
                            printRecords.Add(record);
                            await mongo.MedicationCollection.UpdateOneAsync(x => x.UniqueId == find.UniqueId, Builders<Medication>.Update.Set(o => o.PrintRecords, printRecords));
                        }
                        recorded = true;
                    }
                    break;
            }
            return recorded;
        }


    }

}

// temp-for-evaluate
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class OperationEvaluateProfile : OperationScheduleProfile
        {
            public DepartmentProfile Room { get; set; }
            public Medication[] Medications { get; set; }
        }

        [HttpPost]
        [ActionName("search-operation-schedules-by-departments")]
        public OperationEvaluateProfile[] SearchOperationSchedulesByDepartments(string hospital, DateTime start, DateTime end, [FromBody] string[] rooms, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            // BJFC 恢复室管理精麻药品（5-7种药品）， 手术间管理常规药品
            // 估药预支由恢复室使用， 手术预支和医生预支由手术间使用
            var cabints = mongo.CustomerCollection.AsQueryable().SelectMany(o => o.Cabinets).Where(c => c.DepartmentId == department).ToArray();
            var fills = cabints.SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).ToArray();
            var goodsIds = fills.Select(f => f.GoodsId).Distinct().ToArray();
            //
            var oss = mongo.OperationScheduleCollection.AsQueryable().Where(os => !os.IsDisabled && !os.IsCancelled && rooms.Contains(os.RoomId) && os.ApplyTime >= start && os.ApplyTime < end).OrderBy(os => os.ApplyTime).ToArray();
            var ossIds = oss.Select(o => o.UniqueId).ToArray();
            // 仅获取恢复室管理精麻药品的预支记录
            var medications = mongo.MedicationCollection.AsQueryable().Where(m => ossIds.Contains(m.OperationScheduleId) && m.Mode == ExchangeMode.CheckOut).ToList().Join(goodsIds, a => a.GoodsId, b => b, (a, b) => a).ToArray();
            var roomIds = oss.Select(o => o.RoomId).Distinct().ToArray();
            var departs = mongo.DepartmentCollection.AsQueryable().Where(d => roomIds.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).Select(d => new DepartmentProfile
            {
                UniqueId = d.UniqueId,
                DisplayName = d.DisplayName,
                Code = d.Code,
                Computer = null,
            }).ToArray();

            return oss.Select(x => new OperationEvaluateProfile
            {
                UniqueId = x.UniqueId,
                ApplyTime = x.ApplyTime,
                Patient = (x.Patient ?? new Patient { UniqueId = x.PatientId, }).ToPatientProfile(),
                IdentityBarcode = x.IdentityBarcode,
                ExecutionBeginTime = x.ExecutionBeginTime,
                ExecutionEndTime = x.ExecutionEndTime,
                Room = departs.FirstOrDefault(o => o.UniqueId == x.RoomId),
                Medications = medications.Where(m => m.OperationScheduleId == x.UniqueId).ToArray(),

                PrimaryDoctor = (x.PrimaryDoctor ?? new Employee { UniqueId = x.PrimaryDoctorId, }).ToEmployeeProfile(),
                Anesthetist = (x.Anesthetist ?? new Employee { UniqueId = x.AnesthetistId, }).ToEmployeeProfile(),
                AnesthesiaMode = x.AnesthesiaMode,

                PrimaryAssistant = (x.PrimaryAssistant ?? new Employee { UniqueId = x.PrimaryAssistantId, }).ToEmployeeProfile(),
                PrimaryHandwashingNurse = (x.PrimaryHandwashingNurse ?? new Employee { UniqueId = x.PrimaryHandwashingNurseId, }).ToEmployeeProfile(),
                PrimaryTourNurse = (x.PrimaryTourNurse ?? new Employee { UniqueId = x.PrimaryTourNurseId, }).ToEmployeeProfile(),
                SecondaryAssistant = (x.SecondaryAssistant ?? new Employee { UniqueId = x.SecondaryAssistantId, }).ToEmployeeProfile(),
                SecondaryHandwashingNurse = (x.SecondaryHandwashingNurse ?? new Employee { UniqueId = x.SecondaryHandwashingNurseId, }).ToEmployeeProfile(),
                SecondaryTourNurse = (x.SecondaryTourNurse ?? new Employee { UniqueId = x.SecondaryTourNurseId, }).ToEmployeeProfile(),

                ExecutesCount = 0,
            }).ToArray();
        }

        [HttpPost]
        [ActionName("modify-operation-schedules-evaluate")]
        public Medication[] ModifyOperationSchedulesEvaluate(string hospital, [FromBody] string[] operations, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal);

            // BJFC 恢复室的药品
            var department = DepartmentId;
            var cabinets = mongo.CustomerCollection.AsQueryable().SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == department).ToArray();
            var goodsIds = cabinets.SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).Select(f => f.GoodsId).Distinct().ToArray();
            var goods = mongo.GoodsCollection.AsQueryable().Where(o => goodsIds.Contains(o.UniqueId)).ToArray();

            // 获取估药规则
            var evaluates = mongo.EvaluateCollection.AsQueryable().Where(e => !e.IsDisabled).OrderBy(e => e.DisplayOrder).ToList().Select(o => new
            {
                o.IsActived,
                o.DisplayOrder,
                Keywords = o.Keywords.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries),
                o.Evaluates,
            }).ToArray();
            // 手术排班记录
            var osData = mongo.OperationScheduleCollection.AsQueryable().Where(os => operations.Contains(os.UniqueId)).OrderBy(os => os.ApplyTime).ToList();
            // BJFC 恢复室的预支记录，即估药记录
            var medications = mongo.MedicationCollection.AsQueryable().Where(m => operations.Contains(m.OperationScheduleId) && !m.IsDisabled && m.Mode == ExchangeMode.CheckOut).ToList().Join(goodsIds, a => a.GoodsId, b => b, (a, b) => a).ToList();

            var meds = osData.SelectMany(os =>
            {
                var diagnostic = os.Patient?.Diagnostic ?? string.Empty;
                var eva = evaluates.FirstOrDefault();
                if (eva == null)
                {
                    // 无估药规则 或 不发药
                    return new Medication[0];
                }
                var ids = medications.Where(o => o.OperationScheduleId == os.UniqueId).Select(t => t.GoodsId).ToArray();
                return goods.Where(g => ids.All(o => o != g.UniqueId)).Select(g => new Medication
                {
                    CustomerId = cabinets.FirstOrDefault()?.OwnerCode,
                    Computer = cabinets.FirstOrDefault()?.Computer,
                    OperatorId = primary.LoginId,
                    OperatorName = primary.DisplayName,
                    RecordType = "估药",
                    TimeFilter = DateTime.Now,

                    OperationScheduleId = os.UniqueId,
                    OperationSchedule = os,
                    Doctor = null,
                    DoctorId = null,
                    PatientId = os.PatientId,
                    Patient = os.Patient,

                    GoodsId = g.UniqueId,
                    Goods = goods.FirstOrDefault(o => o.UniqueId == g.UniqueId),
                    BatchNumber = "",
                    ExpiredDate = DateTime.MaxValue.Date,
                    Mode = ExchangeMode.CheckOut,
                    Qty = evaluates.Where(s => s.IsActived).SelectMany(o => o.Evaluates).FirstOrDefault(t => t.GoodsId == g.UniqueId)?.Qty ?? 0.0,

                    Plans = null,
                    QtyActual = 0,
                    FinishTime = null,
                }).ToArray();
            }).ToArray();

            if (meds.Where(m => m.Qty > 0.0).Any())
            {
                mongo.MedicationCollection.InsertMany(meds.Where(m => m.Qty > 0.0));
            }
            return medications.Concat(meds).ToArray();
        }
    }
}