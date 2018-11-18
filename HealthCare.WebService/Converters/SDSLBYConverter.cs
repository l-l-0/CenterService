using HealthCare.Data;
using HealthCare.MongoData;
using HealthCare.WebService.Helper;
using HealthCare.WebService.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

#pragma warning disable CS1591

namespace HealthCare.WebService.Converters
{
    public class SDSLBYConverter : BaseConverter
    {
        public ConverterResult<List<Prescription>> SDSLBYPrescriptions(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<Request>(xml);
            var employees = mongo.EmployeeCollection.AsQueryable();
            var prescriptions = data.OPDrugOrds[0].OPDrugOrdInfo?.Select(e =>
            {
                var src = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == e.AdmCtLocCode) ?? CreateDeparmentInstance(e.AdmCtLocCode, e.AdmCtLoc);
                var dst = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == e.RecCtLoc);
                var cus = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == e.RecCtLoc).ToList();
                var customer = cus.Select(c => c.OwnerCode).FirstOrDefault();
                return e.DrugInfos.OPDrugInfos.Select(d =>
                {
                    var goods = mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == d.DrugCode);
                    // 如果是包装单位而不是使用单位，则转换为使用单位
                    var qty = Math.Abs(d.Qty) * (goods?.SmallPackageUnit == d.Unit ? goods?.Conversion ?? 1.0 : 1.0);
                    var doctor = employees.FirstOrDefault(f => f.DisplayName == e.Doctor) ?? new Employee();
                    return new Prescription()
                    {
                        UniqueId = d.OrdRowID,
                        DisplayOrder = -1,
                        TrackNumber = e.PrescNo,
                        UsedPurpose = d.Common,
                        IsWhole = d.Common.Contains("【带药】"),
                        ExchangeBarcode = null,
                        Description = null,
                        UsedFrequency = d.Freq,
                        IssuedTime = DateTime.TryParse(e.OrdDateTime, out DateTime issue) ? issue : DateTime.MinValue,
                        TimeFilter = issue,

                        DepartmentSourceId = e.AdmCtLocCode,
                        DepartmentSource = src,
                        DepartmentDestinationId = e.RecCtLoc,
                        DepartmentDestination = dst,
                        IsDisabled = false,
                        Doctor = doctor,
                        DoctorId = doctor?.UniqueId,           //只有医生名称
                        PatientId = e.PatId,
                        Patient = CreatePatientInstance(new PatientProfile
                        {
                            PId = e.PatId,
                            PatDepCode = e.PatCompany,
                            PatDepName = null,
                            PatName = e.Name,
                            BedNo = null,
                            DateOfBirth = e.Age.ToString(),
                            InPatientNo = null,
                            OutPatientNo = e.AdmCardNo,
                        }),
                        GoodsId = d.DrugCode,
                        Goods = goods,
                        BatchNumber = string.Empty,
                        ExpiredDate = DateTime.MaxValue.Date,
                        GoodsBarcodes = new List<string>(),
                        Mode = d.Qty > 0 ? ExchangeMode.CheckOut : ExchangeMode.CheckIn,
                        Qty = qty,
                        // 医生、患者、药品、数量相等且未和 HIS 同步则为先预支后补录的医嘱
                        IsAddition = mongo.MedicationCollection.AsQueryable().Any(m => !m.IsDisabled && m.Mode == ExchangeMode.Medication && m.DoctorId == doctor.UniqueId && m.PatientId == e.PatId && m.GoodsId == d.DrugCode && m.QtyActual == qty && m.PrescriptionId == null),
                        FeeCollectorId = null,
                        FeeTime = null,
                        FeeType = null,
                        DispensingTime = DateTime.TryParse(e.FDateTime, out DateTime confirm) ? confirm : DateTime.MinValue,
                        DispensingId = e.FName,
                        FlowRemark = "HIS 已执行",
                        FlowState = "HIS 已执行",
                        RecordType = "HIS 医嘱",

                        QtyActual = 0,
                        Plans = null,
                        FinishTime = null,

                        CustomerId = customer,
                        Computer = cus.Select(o => o.Computer).FirstOrDefault(),
                        CreatedTime = DateTime.Now,

                        AssignAmpouleRecords = null,
                        ChargeOffId = null,
                        Deposit = null,
                        FinishedAmpoule = false,
                        PrintNumber = null,
                        PrintRecords = null,
                    };
                });
            }).SelectMany(p => p).ToList() ?? new List<Prescription>();
            return new ConverterResult<List<Prescription>>
            {
                Code = data.ResultCode,
                Message = data.ResultContent,
                Data = prescriptions,
            };
        }
        public ConverterResult<List<Patient>> SDSLBYPatients(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<Request>(xml);
            var employees = mongo.EmployeeCollection.AsQueryable();
            var patients = data.OPDrugOrds[0].OPDrugOrdInfo?.Select(p => CreatePatientInstance(new PatientProfile
            {
                PId = p.PatId,
                PatName = p.Name,
                PatDepCode = p.AdmCtLocCode,
                PatDepName = p.AdmCtLoc,
                BedNo = null,
                Sex = p.Sex,
                CertificateType = null,
                CertificateCode = null,
                DateOfBirth = p.Age.ToString(),
                InPatientNo = null,
                OutPatientNo = p.AdmCardNo,
                Diagnostic = p.DiagnoDesc,
            })).ToList() ?? new List<Patient>();
            return new ConverterResult<List<Patient>>
            {
                Data = patients,
            };
        }

        public ConverterResult<List<Employee>> SDSLBYEmployees(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<Request>(xml);
            var employees = data.DictInfos[0].UserDict?.Select(e => new Employee
            {
                UniqueId = e.UserCode,
                DisplayName = e.UserDesc,
                Pinyin = e.UserDesc?.Pinyin(),
                PinyinFull = e.UserDesc?.PinyinFull(),
                JobNo = e.Alias,
                JobTitle = e.UserJob,
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
                Code = data.ResultCode,
                Message = data.ResultContent,
                Data = employees,
            };
        }
        public ConverterResult<List<Department>> SDSLBYDepartments(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<Response>(xml);
            var employees = data.DictInfos[0].DeptDict?.Select(e =>
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
                Code = data.ResultCode,
                Message = data.ResultContent,
                Data = employees,
            };
        }
        public ConverterResult<List<Goods>> SDSLBYGoods(string xml)
        {
            var ser = new Serializer();
            var data = ser.Deserialize<Request>(xml);
            var goods = data.DrugInfos[0].DrugInfo?.Select(d =>
            {
                var name = (d.ArcimDesc ?? string.Empty).ToDBC();
                var dict = mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.Code == d.ArcimCode);
                return new Goods
                {
                    UniqueId = d.ArcimCode,
                    DisplayName = name,
                    Pinyin = name.Pinyin(),
                    PinyinFull = name.PinyinFull(),
                    TradeName = d.TradeName,
                    GenericName = null,
                    Filter = "Drug",
                    Specification = (d.Specification ?? string.Empty).ToDBC(),
                    Manufacturer = d.Manuactory,
                    Code = d.ARCItemCatCode,
                    Dosage = $"{d.PackUnit}",
                    DosageForm = d.Dosage,
                    GoodsType = dict == null ? null : dict.GoodsType,
                    Price = string.IsNullOrEmpty(d.UnitPrice.ToString()) ? 0.0 : d.UnitPrice,
                    DisplayOrder = int.TryParse(d.ArcimCode, out int order) ? order : -1,

                    UsedUnit = d.MiniUnit,
                    Conversion = string.IsNullOrEmpty(d.PackNumber) ? 1.0 : double.Parse(Regex.Replace(d.PackNumber, @"[^0-9]+", "")),
                    SmallPackageUnit = d.MiniUnit,
                    IsSync = true,
                    CreatedTime = DateTime.Now,
                    IsDisabled = d.Allowing == 1,

                    IsAmpoule = false,
                    PrescriptionType = null,
                    Reimburse = null,
                    Trader = null,
                    PriceSerialNumber = null,
                };
            }).ToList() ?? new List<Goods>();
            return new ConverterResult<List<Goods>>
            {
                Code = data.ResultCode,
                Message = data.ResultContent,
                Data = goods,
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
            public string Sex { get; set; }
            public string CertificateCode { get; set; }
            public string CertificateType { get; set; }
            public string Diagnostic { get; set; }

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
            Diagnostic = p.Diagnostic,
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
    }
}