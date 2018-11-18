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
    public class BJFCConverter : BaseConverter
    {
        public ConverterResult<List<Employee>> BJFCEmps2Employees(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<BJFCResponse>(xml);
            var employees = data.Response[0].Employees?.Select(e => new Employee
            {
                UniqueId = e.UserCode,
                DisplayName = e.UserDesc,
                Pinyin = e.UserDesc?.Pinyin(),
                PinyinFull = e.UserDesc?.PinyinFull(),
                JobNo = e.UserCode,
                JobTitle = e.TechnicalTitle,
                Department = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == e.CtLocCode),
                DepartmentId = e.CtLocCode,
                DisplayOrder = int.TryParse(e.UserCode, out int order) ? order : -1,

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

        public ConverterResult<List<Department>> BJFCDepartments(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<BJFCResponse>(xml);
            var employees = data.Response[0].Depts?.Select(e =>
            {
                return new Department()
                {
                    UniqueId = e.DeptCode,

                    DisplayName = e.DeptDesc,
                    Pinyin = e.DeptDesc?.Pinyin(),
                    PinyinFull = e.DeptDesc?.PinyinFull(),
                    DisplayOrder = int.TryParse(e.DeptCode, out int order) ? order : -1,

                    CreatedTime = DateTime.Now,
                    IsDisabled = false,
                    Code = e.DeptCode
                };
            }).ToList() ?? new List<Department>();
            return new ConverterResult<List<Department>>
            {
                Code = data.Code,
                Message = data.Message,
                Data = employees,
            };
        }

        public ConverterResult<List<Goods>> BJFCDrugs(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<BJFCRequest>(xml);
            var goods = data.Requests[0].Drugs?.Select(d =>
               {
                   var name = (d.DrugName ?? string.Empty).ToDBC();
                   var dict = mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.Code == d.Id);
                   return new Goods
                   {
                       UniqueId = d.Id,
                       DisplayName = name,
                       Pinyin = name.Pinyin(),
                       PinyinFull = name.PinyinFull(),
                       TradeName = null,
                       GenericName = null,
                       Filter = "Drug",
                       Specification = (d.Specification ?? string.Empty).ToDBC(),
                       Manufacturer = d.Manuactory,
                       Code = d.ARCItemCatCode,
                       Dosage = $"{d.PackUnit}",
                       DosageForm = d.Dosage,
                       GoodsType = dict == null ? null : dict.GoodsType,  //字典转换
                       Price = string.IsNullOrEmpty(d.UnitPrice) ? 0.0 : double.Parse(d.UnitPrice),// d.UnitPrice ?? 0.0,
                       DisplayOrder = int.TryParse(d.Id, out int order) ? order : -1,

                       UsedUnit = d.MiniUnit,
                       Conversion = 1.0,
                       SmallPackageUnit = null,
                       IsSync = true,
                       CreatedTime = DateTime.Now,
                       IsDisabled = d.Allowing == "1",

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

        public ConverterResult<List<Goods>> BJFCConsumables(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<BJFCRequest>(xml);
            var goods = data.BJFCTars[0].TarItemInfos?.Select(d =>
            {
                var name = (d.ArcimName ?? string.Empty).ToDBC();
                double price = string.IsNullOrEmpty(d.TPPrice) ? 0.0 : double.TryParse(d.TPPrice, out price) ? price : double.Parse("0" + d.TPPrice);
                return new Goods
                {
                    UniqueId = d.Id,
                    DisplayName = name,
                    Pinyin = name.Pinyin(),
                    PinyinFull = name.PinyinFull(),
                    TradeName = null,
                    GenericName = null,
                    Filter = "MedicalConsume",
                    Specification = (d.InfoSpec ?? string.Empty).ToDBC(),
                    Manufacturer = d.Manufacturer,
                    Code = d.ArcimCode,
                    Dosage = null,
                    DosageForm = null,
                    GoodsType = d.TarscName,
                    Price = price,
                    DisplayOrder = int.TryParse(d.Id, out int order) ? order : -1,

                    UsedUnit = d.TariUOMDesc,
                    Conversion = 1.0,
                    SmallPackageUnit = null,
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

        public ConverterResult<List<Patient>> BJFCPatients(string xml)
        {
            BaseConverter.WriteSyncLog(nameof(Patient), DateTime.Now.Ticks.ToString(), xml);
            var ser = new Serializer();
            var data = ser.Deserialize<BJFCPatientROW>(xml);
            var list = new List<BJFCPatientROW>();
            list.Add(data);
            var patients = list?.Select(p => CreatePatientInstance(new PatientProfile
            {
                PId = p.RegisterNo,
                PatName = p.PatientName,
                PatDepCode = null,// p.AdmDept,
                PatDepName = p.AdmDept,
                BedNo = null,
                Sex = p.SexDesc,
                CertificateType = p.CredentialType,
                CertificateCode = p.CredentialNo,
                DateOfBirth = p.BirthDay,
                InPatientNo = p.AdmNo,
                OutPatientNo = null,   //门诊号  没有
            })).ToList() ?? new List<Patient>();
            return new ConverterResult<List<Patient>>
            {             
                Data = patients,
            };
        }
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
            public string Sex { get; set; }
            public string CertificateCode { get; set; }
            public string CertificateType { get; set; }
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
                ResidedArea = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == p.PatDepCode) ?? null,
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
            CertificateCode = p.CertificateCode,
            CertificateType = p.CertificateType,
            Address = null,
            MedicareNumber = null,
            Nation = null,
            Nationality = null,
            Post = null,
            Code = null,
            Email = null,
            Gender = p.Sex,
        };

        /// <summary>
        /// 上传医嘱到his
        /// </summary>
        /// <returns></returns>
        public string BJFCUpHis(Medication medication)
        {
            var ser = new Serializer();

            AddOrderItemRt advice = new AddOrderItemRt
            {
                AdmNo = medication.Patient?.Hospitalization?.HospitalNumber,
                ExtRowID = DateTime.Now.ToString("yyyyMMddHHmmssfff"),
                OrderTypeCode = "",
                ArcimCode = medication.GoodsId,
                ArcimDesc = medication.Goods.DisplayName,
                OrderStatus = "V",
                OrderQty = medication.QtyActual,
                OrderDeptCode = "2-07-04",
                OrderRecDepCode = "2-01-08",
                OrderDoctorCode = "2490",// medication.DoctorId,
                OrderUserCode = "2490", //medication.DoctorId,
                OrderSttDat = DateTime.Now.ToString("yyyy-MM-dd"),// medication.FinishTime.Value.ToString("yyyy-MM-dd"),
                OrderSttTim = DateTime.Now.ToString("HH:mm:ss"),// medication.FinishTime.Value.ToString("HH:mm:ss"),
                InsuTypeCode = "",
                OrdRowID = "",
                PHFreqCode = "",
                UomCode = medication.Goods.UsedUnit,
                DosageCode = medication.Goods.DosageForm,
                UsageCode = medication.Goods.GoodsType,
                Source = "SFRA-01",
                InsuFlag = "",
            };
            string xml = ser.Serialize<AddOrderItemRt>(advice);
            BaseConverter.WriteSyncLog(nameof(Medication), DateTime.Now.Ticks.ToString(), xml);
            return xml;
        }
    }
}