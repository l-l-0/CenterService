//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using HealthCare.MongoData;
using HealthCare.WebService.Helper;
using HealthCare.WebService.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable CS1591

namespace HealthCare.WebService.Converters
{
    public class SZGJConverter : BaseConverter
    {
        public ConverterResult<List<Employee>> SuZhouEmps2Employees(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<SZGJEmployee>(xml);
            var employees = data.Employees?.Select(e => new Employee
            {
                UniqueId = e.WorkNO,
                DisplayName = e.Name,
                Pinyin = e.Name?.Pinyin(),
                PinyinFull = e.Name?.PinyinFull(),
                JobNo = e.WorkNO,
                JobTitle = e.Position,
                Department = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == e.DepartCode) ?? CreateDeparmentInstance(e.DepartCode, e.DepartName),
                DepartmentId = e.DepartCode,
                DisplayOrder = int.TryParse(e.WorkNO, out int order) ? order : -1,

                CreatedTime = DateTime.Now,
                IsDisabled = false,
                Signature = null,

                Address = null,
                Age = null,
                Birthday = null,
                CellPhone = null,
                CertificateCode = null,
                CertificateType = null,
                Code = null,
                Email = null,
                Gender = null,
                JobState = null,
                Nation = null,
                Nationality = null,
                Post = null,
            }).ToList() ?? new List<Employee>();
            return new ConverterResult<List<Employee>>
            {
                Code = data.Code,
                Message = data.Message,
                Data = employees,
            };
        }

        public ConverterResult<List<Goods>> SuZhouDrugs2Goods(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<SZGJDrugInfo>(xml);
            var goods = data.Drugs?.Select(d =>
            {
                var name = (d.Name ?? string.Empty).ToDBC();
                return new Goods
                {
                    UniqueId = d.Id,
                    DisplayName = name,
                    Pinyin = name.Pinyin(),
                    PinyinFull = name.PinyinFull(),
                    TradeName = null,
                    GenericName = null,
                    Filter = "Drug",
                    Specification = (d.Regu ?? string.Empty).ToDBC(),
                    Manufacturer = d.Apply,
                    Code = d.Code,
                    Dosage = $"{d.Dosage}{d.DosageUnit}",
                    DosageForm = d.DoseType,
                    GoodsType = d.Type,
                    Price = d.OutPrice,
                    DisplayOrder = int.TryParse(d.Id, out int order) ? order : -1,

                    UsedUnit = d.Unit,
                    Conversion = d.UnitConversions.Where(u => u.Unit != d.Unit).FirstOrDefault()?.Conversion ?? 1.0,
                    SmallPackageUnit = d.UnitConversions.Where(u => u.Unit != d.Unit).FirstOrDefault()?.Unit ?? null,
                    IsSync = true,
                    CreatedTime = DateTime.Now,
                    IsDisabled = false,

                    IsAmpoule = false,
                    PrescriptionType = null,
                    Reimburse = null,
                    Trader = null,
                    PriceSerialNumber = null,
                };
            }).ToList() ?? new List<Goods>();
            return new ConverterResult<List<Goods>>
            {
                Code = data.Code,
                Message = data.Message,
                Data = goods,
            };
        }

        public ConverterResult<List<Patient>> SuZhouPatients2Patients(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<SZGJPatient>(xml);
            var patients = data.Patients?.Select(p => CreatePatientInstance(new PatientProfile
            {
                PId = p.PId,
                PatName = p.PatName,
                PatDepCode = p.PatDepCode,
                PatDepName = p.PatDepName,
                BedNo = p.BedNo,
                DateOfBirth = p.DateOfBirth,
                InPatientNo = p.InPatientNo,
                OutPatientNo = p.OutPatientNo,
            })).ToList() ?? new List<Patient>();
            return new ConverterResult<List<Patient>>
            {
                Code = data.Code,
                Message = data.Message,
                Data = patients,
            };
        }

        public ConverterResult<List<Allocation>> SuZhouStocks2Allocations(string xml, string stockNo)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<SZGJStock>(xml);
            var allocations = data.Stocks?.Select((x, index) =>
            {
                var src = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == x.SendDepart) ?? CreateDeparmentInstance(x.SendDepart, x.SendName);
                var dst = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == x.ReciveDepart) ?? CreateDeparmentInstance(x.ReciveDepart, x.ReciveName);
                var cus = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == x.ReciveDepart).ToList();
                var customer = cus.Select(c => c.OwnerCode).FirstOrDefault();
                return new Allocation
                {
                    UniqueId = $"{stockNo}@{index + 1}",
                    ExchangeBarcode = null,
                    DisplayName = null,
                    ApplyId = stockNo,
                    ApplyQty = Math.Abs(x.Amount),
                    DeliverTime = null, // ??
                    DeliveryNumber = stockNo,

                    DepartmentSource = src,
                    DepartmentSourceId = x.SendDepart,
                    DepartmentDestination = dst,
                    DepartmentDestinationId = x.ReciveDepart,
                    Goods = mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == x.DrugId),
                    GoodsId = x.DrugId,
                    BatchNumber = x.BatchNo ?? string.Empty,
                    ExpiredDate = DateTime.TryParse(x.ExpireDate, out DateTime expired) ? expired.Date : DateTime.MaxValue.Date,
                    Mode = x.Amount > 0 ? ExchangeMode.CheckIn : ExchangeMode.CheckOut,
                    Qty = Math.Abs(x.Amount),
                    GoodsBarcodes = new List<string>(),

                    Computer = cus.Select(o => o.Computer).FirstOrDefault(),        // 广济 ———— 部门单主柜
                    CustomerId = customer,
                    RecordType = "HIS 调拨",
                    DisplayOrder = -1,
                    IsDisabled = false,
                    CreatedTime = DateTime.Now,
                    TimeFilter = DateTime.Now,

                    AcceptedQty = 0,
                    Receiver = null,
                    ReceiverName = null,
                    ReceivedTime = null,
                    Deliverer = null,
                    DelivererName = null,
                    QtyActual = 0,
                    Plans = null,
                    FinishTime = null,
                    Storager = null,
                    StoragerName = null,

                    PrintNumber = null,
                    UnCompleteReason = null,
                    PrintRecords = null,
                    ChargeOffId = null,

                    ExchangeId = null,
                    FinishedAmpoule = false,
                    AssignAmpouleRecords = null,
                };
            }).ToList() ?? new List<Allocation>();
            return new ConverterResult<List<Allocation>>
            {
                Code = data.Code,
                Message = data.Message,
                Data = allocations,
            };
        }

        public ConverterResult<List<Prescription>> SuZhouApply2Prescriptions(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<SZGJApplyInfo>(xml);
            var prescriptions = data.Applies?.SelectMany(p => p.RowSet.Select((x, index) =>
            {
                var goods = mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == x.DrugId);
                var src = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == p.DrugDepCode) ?? CreateDeparmentInstance(p.DrugDepCode, p.DrugDepName);
                var dst = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == p.PatDepCode) ?? CreateDeparmentInstance(p.PatDepCode, p.PatDepName);
                var cus = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == p.PatDepCode).ToList();
                var customer = cus.Select(c => c.OwnerCode).FirstOrDefault();
                // 如果是包装单位而不是使用单位，则转换为使用单位
                var qty = Math.Abs(x.Amount) * (goods?.SmallPackageUnit == x.Unit ? goods?.Conversion ?? 1.0 : 1.0);
                return new Prescription
                {
                    UniqueId = x.DoctorAdviceDetailId,
                    DisplayName = $"{p.RecordNo}@{index + 1}",
                    TrackNumber = p.RecordNo,
                    UsedPurpose = x.Content,
                    IsWhole = x.Content.Contains("【带药】"),
                    ExchangeBarcode = null,
                    Description = null,
                    UsedFrequency = null,
                    IssuedTime = DateTime.TryParse(x.StartDateTime, out DateTime issue) ? issue : DateTime.MinValue,
                    TimeFilter = issue,

                    DepartmentSourceId = p.DrugDepCode,
                    DepartmentSource = src,
                    DepartmentDestinationId = p.PatDepCode,
                    DepartmentDestination = dst,
                    DoctorId = x.InputDocNum,
                    Doctor = mongo.EmployeeCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == x.InputDocNum) ?? new Employee
                    {
                        UniqueId = x.InputDocNum,
                        DisplayName = x.InputDocName,
                        Pinyin = x.InputDocName?.Pinyin(),
                        PinyinFull = x.InputDocName?.PinyinFull(),
                        Code = x.InputDocNum,
                        JobNo = x.InputDocNum,
                    },
                    PatientId = p.PId,
                    Patient = CreatePatientInstance(new PatientProfile
                    {
                        PId = p.PId,
                        PatDepCode = p.PatDepCode,
                        PatDepName = p.PatDepName,
                        PatName = p.PatName,
                        BedNo = p.BedNo,
                        DateOfBirth = p.DateOfBirth,
                        InPatientNo = p.InPatientNo,
                        OutPatientNo = p.OutPatientNo,
                    }),
                    GoodsId = x.DrugId,
                    Goods = goods,
                    BatchNumber = string.Empty,
                    ExpiredDate = DateTime.MaxValue.Date,
                    GoodsBarcodes = new List<string>(),
                    Mode = x.Amount > 0 ? ExchangeMode.CheckOut : ExchangeMode.CheckIn,
                    Qty = qty,
                    // 医生、患者、药品、数量相等且未和 HIS 同步则为先预支后补录的医嘱
                    IsAddition = mongo.MedicationCollection.AsQueryable().Any(m => !m.IsDisabled && m.Mode == ExchangeMode.Medication && m.DoctorId == x.InputDocNum && m.PatientId == p.PId && m.GoodsId == x.DrugId && m.QtyActual == qty && m.PrescriptionId == null),
                    FeeCollectorId = p.OperNo,
                    FeeTime = DateTime.TryParse(p.OperDateTime, out DateTime confirm) ? confirm : DateTime.MinValue,
                    FeeType = null,
                    DispensingTime = null,
                    DispensingId = null,
                    FlowRemark = "HIS 已执行",
                    FlowState = "HIS 已执行",
                    RecordType = "HIS 医嘱",

                    QtyActual = 0,
                    Plans = null,
                    FinishTime = null,

                    CustomerId = customer,
                    Computer = cus.Select(o => o.Computer).FirstOrDefault(),        // 广济 ———— 部门单主柜
                    CreatedTime = DateTime.Now,
                    IsDisabled = false,
                    DisplayOrder = int.TryParse(x.DoctorAdviceDetailId, out int id) ? id + (index + 1) : -1,

                    AssignAmpouleRecords = null,
                    ChargeOffId = null,
                    Deposit = null,
                    FinishedAmpoule = false,
                    PrintNumber = null,
                    PrintRecords = null,
                };
            })).ToList() ?? new List<Prescription>();
            return new ConverterResult<List<Prescription>>
            {
                Code = data.Code,
                Message = data.Message,
                Data = prescriptions,
            };
        }


        private Department CreateDeparmentInstance(string code, string name) => new Department
        {
            UniqueId = code,
            DisplayName = name,
            Code = code,
            Filter = nameof(Department),
            Pinyin = name?.Pinyin(),
            PinyinFull = name?.PinyinFull(),
            CreatedTime = DateTime.Now,
            DisplayOrder = -1,
            IsDisabled = false,
        };

        private class PatientProfile
        {
            public string PId { get; set; }
            public string PatName { get; set; }
            public string OutPatientNo { get; set; }
            public string InPatientNo { get; set; }
            public string PatDepCode { get; set; }
            public string PatDepName { get; set; }
            public string BedNo { get; set; }
            public string DateOfBirth { get; set; }
        }

        private Patient CreatePatientInstance(PatientProfile p) => new Patient
        {
            UniqueId = p.PId,
            DisplayName = p.PatName,
            Pinyin = p.PatName?.Pinyin(),
            PinyinFull = p.PatName?.PinyinFull(),
            Clinic = new Clinic
            {
                UniqueId = p.OutPatientNo,
                SerialNumber = p.OutPatientNo,
                CreatedTime = DateTime.Now,
            },
            Diagnostic = null,
            Hospitalization = new Hospitalization
            {
                UniqueId = p.InPatientNo,
                HospitalNumber = p.InPatientNo,
                ResidedArea = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == p.PatDepCode) ?? CreateDeparmentInstance(p.PatDepCode, p.PatDepName),
                ResidedAreaId = p.PatDepCode,
                BedNo = p.BedNo,

                AdmittedDepartment = null,
                AdmittedDepartmentId = null,
                Room = null,
                RoomId = null,
                CreatedTime = DateTime.Now,
                CumulativeCount = 1,
                InitiationTime = DateTime.MinValue,
            },
            Age = DateTime.TryParse(p.DateOfBirth, out DateTime birth) ? DateTime.Now.Year - birth.Year : (int?)null,
            Birthday = birth == DateTime.MinValue ? (DateTime?)null : birth,
            DisplayOrder = int.TryParse(p.InPatientNo, out int order) ? order : -1,
            CreatedTime = DateTime.Now,
            IsDisabled = false,
            RegisterNumber = null,

            CellPhone = null,
            CertificateCode = null,
            CertificateType = null,
            Address = null,
            MedicareNumber = null,
            Nation = null,
            Nationality = null,
            Post = null,
            Code = null,
            Email = null,
            Gender = null,
        };
    }
}