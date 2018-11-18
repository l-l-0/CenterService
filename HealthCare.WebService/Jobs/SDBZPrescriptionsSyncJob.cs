//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using Newtonsoft.Json.Linq;
using System.Linq;
using Quartz;
using Quartz.Impl;
using System;
using HealthCare.Data;
using MongoDB.Driver;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;

#pragma warning disable CS1591

namespace HealthCare.WebService.Jobs
{
    public class SDBZPrescriptionsSyncScheduler
    {
        public static void Start()
        {
            var job = new JobDetailImpl(nameof(SDBZPrescriptionsSyncJob), typeof(SDBZPrescriptionsSyncJob));
            var trigger = CronScheduleBuilder.CronSchedule(new CronExpression(Global.AppSettings["SDBZ"]["PrescriptionsSyncScheduler"].Value<string>())).Build();
            trigger.Key = new TriggerKey($"{nameof(SDBZPrescriptionsSyncScheduler)}-{nameof(trigger)}");

            var scheduler = new StdSchedulerFactory().GetScheduler();
            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
            Global.SchedulerLogger.Info($"{nameof(SDBZPrescriptionsSyncScheduler)} Started");

            System.Threading.Tasks.Task.Run(() => new SDBZPrescriptionsSyncJob().Execute(null));
        }
    }

    public class SDBZPrescriptionsSyncJob : IJob
    {
        private MongoContext mongo = new MongoContext();

        public void Execute(IJobExecutionContext context)
        {
            // 只要最近一段时间内的数据
            var beginDate = DateTime.Now.Date.AddDays(-15);
            var cabinets = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).ToList();
            try
            {
                SyncPrescription();
            }
            catch (Exception ex)
            {
                Global.SchedulerLogger.Error(nameof(SyncPrescription), ex);
            }

            try
            {
                SyncClinicPrescription();
            }
            catch (Exception ex)
            {
                Global.SchedulerLogger.Error(nameof(SyncClinicPrescription), ex);
            }

            try
            {
                SyncHospitalizationPrescription();
            }
            catch (Exception ex)
            {
                Global.SchedulerLogger.Error(nameof(SyncHospitalizationPrescription), ex);
            }


            // 医嘱
            void SyncPrescription()
            {
                using (var ctx = new Models.OracleDbContext())
                {
                    // V_DISPENSE_REQ 存储护士的摆药请求 (可以理解为护士确认后的医嘱), V_DISPENSARY_ORDER 存储全部摆药信息 (可以理解为医生开的所有医嘱)
                    // 通过 V_DISPENSE_REQ 中的 WARD 和 BED_NO 到 V_DISPENSARY_ORDER 中进行摆药信息的查询
                    // WARD 是护理单元, BED_NO 是以逗号分隔的床号
                    // 判断依据是护理单元相同 且 床号相同
                    var reqs = ctx.SDBZV_DISPENSE_REQ.AsNoTracking().Where(s => s.DISPENSARY == "020710" || s.DISPENSARY == "020805").Distinct().ToList()
                        .Select(o => new { Ward = o.WARD, Dispensary = o.DISPENSARY, BedNos = o.BED_NO.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray(), }).ToList();
                    Global.MonitorLogger.Info($"V_DISPENSE_REQ -> {reqs.Count}");

                    var lq = from r in reqs
                             join p in ctx.SDBZV_DISPENSARY_ORDER.AsNoTracking().Where(o => o.REPEAT_INDICATOR == 0 && o.START_DATE_TIME >= beginDate) on r.Ward equals p.WARD_CODE  // 1. 查询数据库所有的临时医嘱
                             where r.BedNos.Contains(p.BED_NO)  // 2. 内存过滤数据
                             select new { p, r, };
                    var dbData = lq.ToList();
                    Global.MonitorLogger.Warn(JsonConvert.SerializeObject(dbData));

                    var orders = dbData.Select(x =>
                    {
                        var p = x.p;
                        var r = x.r;

                        var find = cabinets.FirstOrDefault(c => c.DepartmentId == r.Dispensary);
                        return new Prescription
                        {
                            UniqueId = $"{p.ORDER_NO}@{p.ORDER_SUB_NO}@{p.PATIENT_ID}@{p.VISIT_ID}@{p.ITEM_CODE}",
                            TrackNumber = $"{p.VISIT_ID}@{p.ORDER_NO}@{p.ORDER_SUB_NO}",  // 该字段在 SDBZ 项目没有实际用途, 暂时用作保留计费接口的存储过程的参数
                            Description = "临时医嘱",
                            UsedPurpose = p.ADMINISTRATION,
                            UsedFrequency = p.FREQUENCY,
                            UsedDosage = null,
                            RecordType = "住院医嘱",

                            CustomerId = find?.OwnerCode,
                            Computer = find?.Computer,
                            DepartmentSourceId = p.ORDERING_DEPT,
                            DepartmentSource = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.ORDERING_DEPT),
                            DepartmentDestinationId = r.Dispensary,
                            DepartmentDestination = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == r.Dispensary),
                            TimeFilter = p.START_DATE_TIME,
                            IssuedTime = p.START_DATE_TIME,

                            DoctorId = p.DOCTOR_USER,
                            Doctor = mongo.EmployeeCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.DOCTOR_USER),
                            PatientId = p.PATIENT_ID,
                            Patient = mongo.PatientCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.PATIENT_ID),
                            GoodsId = $"{p.ITEM_CODE}|{p.ITEM_SPEC}",   // 药品 规格 厂家, 否则不能唯一标识药品. 规格字段在医嘱中等于 规格+厂家
                            Goods = mongo.GoodsCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == $"{p.ITEM_CODE}|{p.ITEM_SPEC}"),
                            BatchNumber = string.Empty,
                            ExpiredDate = DateTime.MaxValue.Date,
                            Mode = ExchangeMode.CheckOut,
                            Qty = Math.Abs(p.AMOUNT ?? 0.0),
                            AgentId = null,
                            Agent = null,

                            QtyActual = 0.0,
                            Plans = null,
                            FinishTime = null,

                            DispensingId = null,
                            DispensingTime = null,
                            FeeCollectorId = null,
                            FeeTime = null,
                            FeeType = null,
                            FlowState = "HIS 已审核",
                            FlowRemark = "护士摆药",
                            DisplayName = null,
                            CreatedTime = DateTime.Now,
                            DisplayOrder = -1,
                            ChargeOffId = null,
                            Deposit = null,
                            ExchangeBarcode = null,
                            FinishedAmpoule = false,
                            AssignAmpouleRecords = null,
                            PrintNumber = null,
                            PrintRecords = null,
                            GoodsBarcodes = new List<string>(),
                            IsAddition = false,
                            IsDisabled = false,
                            IsWhole = false,
                        };
                    }).ToList();

                    foreach (var item in orders)
                    {
                        if (mongo.PrescriptionCollection.AsQueryable().Any(o => o.UniqueId == item.UniqueId && o.Qty > 0))
                        {
                            // 已经在数据库里面的医嘱, 不能被覆盖
                            Global.MonitorLogger.Info($"{item.UniqueId} Skiped");

                            mongo.PrescriptionCollection.UpdateOne(x => x.UniqueId == item.UniqueId && x.Doctor == null, Builders<Prescription>.Update.Set(x => x.Doctor, item.Doctor));
                            mongo.PrescriptionCollection.UpdateOne(x => x.UniqueId == item.UniqueId && x.Patient == null, Builders<Prescription>.Update.Set(x => x.Patient, item.Patient));
                            mongo.PrescriptionCollection.UpdateOne(x => x.UniqueId == item.UniqueId && x.Goods == null, Builders<Prescription>.Update.Set(x => x.Goods, item.Goods));
                            continue;
                        }

                        // HIS 的数据可能出现发药数量为 0 的情况, 此时可以覆盖
                        (TimeSpan begin, TimeSpan end)[] timeSpans =
                            new[]
                            {
                                    // 六月一号后, 只要 11:30~14:00 和 18:00~次日8:00
                                    (new TimeSpan(11, 30, 0), new TimeSpan(14, 0, 1)),
                                    (new TimeSpan(18, 0, 0), new TimeSpan(24, 0, 0)),
                                    (new TimeSpan(0, 0, 0), new TimeSpan(8, 0, 1)),
                            };
                        //if (timeSpans.Any(t => item.IssuedTime.TimeOfDay >= t.begin && item.IssuedTime.TimeOfDay < t.end))
                        //{
                        mongo.PrescriptionCollection.FindOneAndReplace<Prescription>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Prescription, Prescription> { IsUpsert = true });
                        Global.MonitorLogger.Info($"{item.UniqueId} FindOneAndReplace");
                        //}
                    }
                }
            }

            // 门诊处方
            void SyncClinicPrescription()
            {
                var sql = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "sdbz_clinic_prescription_sql.txt"), System.Text.Encoding.UTF8);
                using (var ctx = new Models.OracleDbContext())
                {
                    var dbData = ctx.Database.SqlQuery<ClinicPrescription>(sql).ToList();
                    Global.MonitorLogger.Warn(JsonConvert.SerializeObject(dbData));

                    var orders = dbData.Select(p =>
                    {
                        var goodsId = $"{p.DRUG_CODE}|{p.DRUG_SPEC}{p.FIRM_ID}";  // 药品 规格 厂家, 否则不能唯一标识药品
                        var goods = mongo.GoodsCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == goodsId);
                        var find = cabinets.FirstOrDefault(c => c.DepartmentId == p.DISPENSARY);
                        return new Prescription
                        {
                            UniqueId = $"{p.ORDER_NO}@{p.ORDER_SUB_NO}@{p.PATIENT_ID}@{p.VISIT_NO}@{p.ITEM_NO}",
                            TrackNumber = $"{p.VISIT_NO}@{p.SERIAL_NO}@{p.ORDER_CLASS}@{p.ORDER_NO}@{p.ORDER_SUB_NO}@{p.PRESC_NO}@{p.ITEM_NO}@{p.COSTS}@{p.AMOUNT ?? 0.0}@{p.UNITS}",  // 该字段在 SDBZ 项目没有实际用途, 暂时用作保留计费接口的存储过程的参数
                            Description = "临时医嘱",
                            UsedPurpose = null,
                            UsedFrequency = null,
                            UsedDosage = null,
                            RecordType = "门诊处方",

                            CustomerId = find?.OwnerCode,
                            Computer = find?.Computer,
                            DepartmentSourceId = p.ORDERED_BY_DEPT,
                            DepartmentSource = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.ORDERED_BY_DEPT),
                            DepartmentDestinationId = p.DISPENSARY,
                            DepartmentDestination = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.DISPENSARY),
                            TimeFilter = p.VISIT_DATE,
                            IssuedTime = p.VISIT_DATE,

                            DoctorId = p.ORDERED_BY_DOCTOR,
                            Doctor = mongo.EmployeeCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.ORDERED_BY_DOCTOR),
                            PatientId = p.PATIENT_ID,
                            Patient = mongo.PatientCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.PATIENT_ID),
                            GoodsId = goodsId,
                            Goods = goods,
                            BatchNumber = string.Empty,
                            ExpiredDate = DateTime.MaxValue.Date,
                            Mode = ExchangeMode.CheckOut,
                            Qty = Math.Abs(p.AMOUNT ?? 0.0) * (p.UNITS == goods?.SmallPackageUnit ? goods.Conversion : 1.0),
                            IsWhole = p.UNITS == goods?.SmallPackageUnit,        // 门诊处方 整包装取
                            Agent = null,
                            AgentId = null,

                            QtyActual = 0.0,
                            Plans = null,
                            FinishTime = null,

                            DispensingId = null,
                            DispensingTime = null,
                            FeeCollectorId = null,
                            FeeTime = null,
                            FeeType = null,
                            FlowState = "HIS 已审核",
                            FlowRemark = "护士摆药",
                            DisplayName = null,
                            CreatedTime = DateTime.Now,
                            DisplayOrder = -1,
                            ChargeOffId = null,
                            Deposit = null,
                            ExchangeBarcode = null,
                            FinishedAmpoule = false,
                            AssignAmpouleRecords = null,
                            PrintNumber = null,
                            PrintRecords = null,
                            GoodsBarcodes = new List<string>(),
                            IsAddition = false,
                            IsDisabled = false,
                        };
                    }).ToList();
                    Global.MonitorLogger.Info($"ClinicPrescription => rows = {orders.Count}");

                    foreach (var item in orders)
                    {
                        if (mongo.PrescriptionCollection.AsQueryable().Any(o => o.UniqueId == item.UniqueId && o.Qty > 0))
                        {
                            // 已经在数据库里面的医嘱, 不能被覆盖
                            Global.MonitorLogger.Info($"{item.UniqueId} {item.RecordType} Skiped");

                            mongo.PrescriptionCollection.UpdateOne(x => x.UniqueId == item.UniqueId && x.Doctor == null, Builders<Prescription>.Update.Set(x => x.Doctor, item.Doctor));
                            mongo.PrescriptionCollection.UpdateOne(x => x.UniqueId == item.UniqueId && x.Patient == null, Builders<Prescription>.Update.Set(x => x.Patient, item.Patient));
                            mongo.PrescriptionCollection.UpdateOne(x => x.UniqueId == item.UniqueId && x.Goods == null, Builders<Prescription>.Update.Set(x => x.Goods, item.Goods).Set(x => x.Qty, item.Qty).Set(x => x.IsWhole, item.IsWhole));
                            continue;
                        }
                        // HIS 的数据可能出现发药数量为 0 的情况, 此时可以覆盖
                        mongo.PrescriptionCollection.FindOneAndReplace<Prescription>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Prescription, Prescription> { IsUpsert = true });
                        Global.MonitorLogger.Info($"{item.UniqueId} {item.RecordType} FindOneAndReplace");
                    }
                }
            }

            // 住院处方
            void SyncHospitalizationPrescription()
            {
                var sql = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "sdbz_hospitalization_prescription_sql.txt"), System.Text.Encoding.UTF8);
                using (var ctx = new Models.OracleDbContext())
                {
                    var dbData = ctx.Database.SqlQuery<HospitalizationPrescription>(sql).ToList();
                    Global.MonitorLogger.Warn(JsonConvert.SerializeObject(dbData));

                    var orders = dbData.Select(p =>
                    {
                        var goodsId = $"{p.DRUG_CODE}|{p.PACKAGE_SPEC}{p.FIRM_ID}";   // 药品 规格 厂家, 否则不能唯一标识药品
                        var goods = mongo.GoodsCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == goodsId);
                        var find = cabinets.FirstOrDefault(c => c.DepartmentId == p.DISPENSARY);
                        return new Prescription
                        {
                            UniqueId = $"{p.PRESC_NO}@{p.PATIENT_ID}@{p.VISIT_ID}@{p.ITEM_NO}",
                            TrackNumber = $"{p.VISIT_ID}@{p.PRESC_NO}@{p.ITEM_NO}@{p.QUANTITY ?? 0.0}@{p.PACKAGE_UNITS}@{p.PAYMENTS ?? 0.0}",  // 该字段在 SDBZ 项目没有实际用途, 暂时用作保留计费接口的存储过程的参数
                            Description = "临时医嘱",
                            UsedPurpose = p.ADMINISTRATION,
                            UsedFrequency = p.FREQUENCY,
                            UsedDosage = null,
                            RecordType = "住院处方",

                            CustomerId = find?.OwnerCode,
                            Computer = find?.Computer,
                            DepartmentSourceId = p.DISPENSARY,
                            DepartmentSource = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.DISPENSARY),
                            DepartmentDestinationId = p.DISPENSARY,
                            DepartmentDestination = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.DISPENSARY),
                            TimeFilter = p.PRESC_DATE,
                            IssuedTime = p.PRESC_DATE,

                            DoctorId = p.DOCTOR_USER,
                            Doctor = mongo.EmployeeCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.DOCTOR_USER),
                            PatientId = p.PATIENT_ID,
                            Patient = mongo.PatientCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == p.PATIENT_ID),
                            GoodsId = goodsId,
                            Goods = goods,
                            BatchNumber = string.Empty,
                            ExpiredDate = DateTime.MaxValue.Date,
                            Mode = ExchangeMode.CheckOut,
                            Qty = Math.Abs(p.QUANTITY ?? 0.0) * (p.PACKAGE_UNITS == goods?.SmallPackageUnit ? goods.Conversion : 1.0),
                            IsWhole = p.PACKAGE_UNITS == goods?.SmallPackageUnit,        // 住院处方 整包装取
                            Agent = null,
                            AgentId = null,

                            QtyActual = 0.0,
                            Plans = null,
                            FinishTime = null,

                            DispensingId = null,
                            DispensingTime = null,
                            FeeCollectorId = null,
                            FeeTime = null,
                            FeeType = null,
                            FlowState = "HIS 已审核",
                            FlowRemark = "护士摆药",
                            DisplayName = null,
                            CreatedTime = DateTime.Now,
                            DisplayOrder = -1,
                            ChargeOffId = null,
                            Deposit = null,
                            ExchangeBarcode = null,
                            FinishedAmpoule = false,
                            AssignAmpouleRecords = null,
                            PrintNumber = null,
                            PrintRecords = null,
                            GoodsBarcodes = new List<string>(),
                            IsAddition = false,
                            IsDisabled = false,
                        };
                    }).ToList();
                    Global.MonitorLogger.Info($"HospitalizationPrescription => rows = {orders.Count}");

                    foreach (var item in orders)
                    {
                        if (mongo.PrescriptionCollection.AsQueryable().Any(o => o.UniqueId == item.UniqueId && o.Qty > 0))
                        {
                            // 已经在数据库里面的医嘱, 不能被覆盖
                            Global.MonitorLogger.Info($"{item.UniqueId} {item.RecordType} Skiped");

                            mongo.PrescriptionCollection.UpdateOne(x => x.UniqueId == item.UniqueId && x.Doctor == null, Builders<Prescription>.Update.Set(x => x.Doctor, item.Doctor));
                            mongo.PrescriptionCollection.UpdateOne(x => x.UniqueId == item.UniqueId && x.Patient == null, Builders<Prescription>.Update.Set(x => x.Patient, item.Patient));
                            mongo.PrescriptionCollection.UpdateOne(x => x.UniqueId == item.UniqueId && x.Goods == null, Builders<Prescription>.Update.Set(x => x.Goods, item.Goods));
                            continue;
                        }
                        // HIS 的数据可能出现发药数量为 0 的情况, 此时可以覆盖
                        mongo.PrescriptionCollection.FindOneAndReplace<Prescription>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Prescription, Prescription> { IsUpsert = true });
                        Global.MonitorLogger.Info($"{item.UniqueId} {item.RecordType} FindOneAndReplace");
                    }
                }
            }
        }
    }

    public class ClinicPrescription
    {
        public string PATIENT_ID { get; set; }      // VARCHAR2(10)
        public DateTime VISIT_DATE { get; set; }    // DATE
        public int? VISIT_NO { get; set; }          // NUMBER(5)
        public string SERIAL_NO { get; set; }       // VARCHAR2(10)
        public string ORDER_CLASS { get; set; }     // VARCHAR2(1)
        public int? ORDER_NO { get; set; }          // NUMBER(3)
        public int? ORDER_SUB_NO { get; set; }      // NUMBER(2)
        public int? PRESC_NO { get; set; }          // NUMBER(5)
        public int? ITEM_NO { get; set; }           // NUMBER(3)
        public string DRUG_CODE { get; set; }       // VARCHAR2(20)
        public string DRUG_NAME { get; set; }       // VARCHAR2(100)
        public string DRUG_SPEC { get; set; }       // VARCHAR2(20)
        public string FIRM_ID { get; set; }         // VARCHAR2(10)
        public string UNITS { get; set; }           // VARCHAR2(8)
        public double? AMOUNT { get; set; }         // NUMBER(8,4)
        public double? COSTS { get; set; }          // NUMBER(10,4)
        public string DISPENSARY { get; set; }      // VARCHAR2(8)
        public string ORDERED_BY_DEPT { get; set; } // VARCHAR2(8)
        public string ORDERED_BY_DOCTOR { get; set; }   // VARCHAR2(20)
    }

    public class HospitalizationPrescription
    {
        public string PATIENT_ID { get; set; }      // VARCHAR2(10)
        public int? VISIT_ID { get; set; }          // NUMBER(2)
        public string DISPENSARY { get; set; }      // VARCHAR2(8)
        public DateTime PRESC_DATE { get; set; }    // DATE
        public int? PRESC_NO { get; set; }          // NUMBER(4)
        public int? ITEM_NO { get; set; }           // NUMBER(2)
        public int? ORDER_NO { get; set; }          // NUMBER(4)
        public int? ORDER_SUB_NO { get; set; }      // NUMBER(2)
        public string DRUG_CODE { get; set; }       // VARCHAR2(20)
        public string DRUG_SPEC { get; set; }       // VARCHAR2(50)
        public string DRUG_NAME { get; set; }       // VARCHAR2(100)
        public string FIRM_ID { get; set; }         // VARCHAR2(10)
        public string PACKAGE_SPEC { get; set; }    // VARCHAR2(20)
        public string PACKAGE_UNITS { get; set; }   // VARCHAR2(8)
        public double? QUANTITY { get; set; }       // NUMBER(6,2)
        public double? COSTS { get; set; }          // NUMBER(10,4)
        public double? PAYMENTS { get; set; }       // NUMBER(10,4)
        public int? PRESC_STATUS { get; set; }      // NUMBER(1)
        public string ADMINISTRATION { get; set; }  // VARCHAR2(16)
        public double? DOSAGE { get; set; }         // NUMBER(12,4)
        public string DOSAGE_UNITS { get; set; }    // VARCHAR2(8)
        public int? PRESC_TYPE { get; set; }        // NUMBER(1)
        public double? AMOUNT_PER_PACKAGE { get; set; }  // NUMBER(5)
        public string FREQUENCY { get; set; }       // VARCHAR2(16)
        public double? DOSAGE_EACH { get; set; }    // NUMBER(10,4)
        public string FREQ_DETAIL { get; set; }     // VARCHAR2(80)
        public string DOCTOR_USER { get; set; }     // VARCHAR2(16)
        public int? PRESC_FLAG { get; set; }        // NUMBER(1)
    }
}