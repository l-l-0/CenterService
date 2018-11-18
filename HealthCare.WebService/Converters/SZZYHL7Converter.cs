using HealthCare.Data;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace HealthCare.WebService.Converters
{
    public class SZZYHL7Converter
    {
        private MongoContext mongo = new MongoContext();
        public string[] ClinicHL7(string[] medications, out List<string[]> clinicPreIds)
        {
            var ids = new List<string[]> { };
            // 根据患者分组
            var prescriptionGroups = mongo.PrescriptionCollection.AsQueryable().Where(m => medications.Contains(m.UniqueId) && m.Patient.Clinic.SerialNumber.Length > 0).ToList().GroupBy(m => m.Patient);
            var data = prescriptionGroups.Select(g =>
            {
                //给MSH、PID、PV1 赋值 (Header)
                var propertie = new List<string>();
                var patient = g.Key;
                var headerProperties = new List<string>();
                ids.Add(g.Select(m => m.UniqueId).ToArray());

                //MSH|^~\&|SFRA||HIS||201808301922||OMP^O09|1462711203|P|2.4
                var msh = new HL7MessageMSH { MessageType = "OMP^O09", MessageControl = g.FirstOrDefault().UniqueId, };
                var mshProperties = typeof(HL7MessageMSH).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(msh)).ToArray();
                var mshString = string.Join("|", mshProperties);
                headerProperties.Add(mshString);

                //PID||0000014627|0000014627^^^^IDCard~^^^^PatientNO~0000014627^^^^IdentifyNO||陈文俊||19891031000000|M
                var pid = new HL7MessagePID
                {
                    PatientID = patient.UniqueId,
                    PatientIdentifierList = $"{patient.UniqueId}^^^^IDCard{patient?.Hospitalization?.HospitalNumber}~^^^^PatientNO~{patient?.CertificateCode}^^^^IdentifyNO",
                    PatientName = patient.DisplayName,
                    Birthday = string.Format("{0:yyyyMMddHHmmss}", patient?.Birthday),
                    AdministrativeSex = patient.Gender == "男" ? "M" : "F",
                };
                var pidProperties = typeof(HL7MessagePID).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(pid)).ToArray();
                var pidString = string.Join("|", pidProperties);
                headerProperties.Add(pidString);

                //PV1||O|||||||||||1|||||02|29120
                var pv1 = new HL7MessagePV1
                {
                    PatientClass = "O",
                    ReAdmissionIndicator = g.ToArray().Length.ToString(), // 患者预支次数
                    PatientType = "自费/医保",
                    VisitNumber = string.Format("{0:yyyyMMddHHmmss}", DateTime.Now),//"就诊"
                };
                var pv1Properties = typeof(HL7MessagePV1).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(pv1)).ToArray();
                var pv1String = string.Join("|", pv1Properties);
                headerProperties.Add(pv1String);
                var header = string.Join("\r\n", headerProperties);

                // body
                var body = g.SelectMany(m =>
                {
                    var bodyProperties = new List<string>();
                    var action = mongo.ActionJournalCollection.AsQueryable().FirstOrDefault(a => a.TargetId == m.UniqueId);
                    var dateStr = string.Format("{0:yyyyMMddHHmmssffff}", DateTime.Now);
                    //ORC|NW|1462711203CSS|256594|291200|||100&ml^&ONCE^^^^A||20180830192248|MDSD||100069|||||2008
                    var orc = new HL7MessageORC
                    {
                        PlacerOrderNumber = $"{dateStr.Substring(10)}SFRA", // 1462711203CSS "CSS"意为手麻
                        FillerOrderNumber = $"{dateStr.Substring(10)}", // 256594
                        PlacerGroupNumber = $"{dateStr.Substring(6, 6)}", // 291200
                        QuantityTiming = $@"{m.Goods?.Dosage}&{m.Goods.UsedUnit}^&ONCE^^^^A",
                        DateTimeofTransaction = string.Format("{0:yyyyMMddHHmmss}", m.CreatedTime), // 事务时间 20180830192248
                        EnteredBy = action?.PrimaryUserId,  //操作员ID MDSD
                        OrderingProvider = m.DoctorId, // 下单方提供者 100069
                        EnteringOrganization = m.Doctor?.DepartmentId, //操作员所在科室
                        OrderingFacilityName = m.Patient?.Hospitalization?.AdmittedDepartmentId, //患者所在部门
                    };
                    var orcProperties = typeof(HL7MessageORC).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(orc)).ToArray();
                    var orcString = string.Join("|", orcProperties);
                    bodyProperties.Add(orcString);

                    //RXO|Y00000002766^Y00000002766^d|1||^袋||||^^^&6000||||袋
                    var rxo = new HL7MessageRXO
                    {
                        RequestedGiveCode = $"{m.GoodsId}^{m.GoodsId}^{(m.Goods?.Filter == nameof(Goods) ? "d" : "n")}",
                        RequestedGiveAmountMix = m.Qty.ToString(),
                        RequestedGiveUnits = m.Goods?.UsedUnit,
                        DeliverToLocation = $"^^^&{m.DepartmentDestinationId}", //发药部门
                        RequestedDispenseUnits = m.Goods?.UsedUnit,
                    };
                    var rxoProperties = typeof(HL7MessageRXO).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(rxo)).ToArray();
                    var rxoString = string.Join("|", rxoProperties);
                    bodyProperties.Add(rxoString);

                    //RXR | 39
                    var rxr = new HL7MessageRXR { };
                    var rxrProperties = typeof(HL7MessageRXR).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(rxr)).ToArray();
                    var rxrString = string.Join("|", rxrProperties);
                    bodyProperties.Add(rxrString);

                    //FT1||||||||||||^^2.76
                    var ft1 = new HL7MessageFT1 { TransactionAmountUnit = $"^^{m.Goods.Price.ToString()}", };
                    var ft1Properties = typeof(HL7MessageFT1).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(ft1)).ToArray();
                    var ft1String = string.Join("|", ft1Properties);
                    bodyProperties.Add(ft1String);
                    return bodyProperties;
                }).ToList();

                // 合并 header body
                propertie.Add(header);
                propertie.AddRange(body);
                return string.Join("\r\n", propertie);
            }).ToArray();
            // 预支记录ID集合
            clinicPreIds = ids;
            return data;
        }

        public string[] HospitalizationHL7(string[] medications, out List<string[]> hospitalPreIds)
        {
            var ids = new List<string[]> { };
            var properties = new List<string>();
            //m.Patient.Hospitalization.HospitalNumber.Length > 0 && m.FinishTime != null && m.FeeTime == null
            var prescriptionGroups = mongo.PrescriptionCollection.AsQueryable().Where(m => medications.Contains(m.UniqueId) && m.Patient.Hospitalization.HospitalNumber.Length > 0).ToList().GroupBy(m => m.Patient);
            var data = prescriptionGroups.Select(g =>
            {
                var propertie = new List<string>();
                var patient = g.Key;
                var headerProperties = new List<string>();
                ids.Add(g.Select(m => m.UniqueId).ToArray());

                //MSH|^~\&|SFRA||HIS||201809251223||RAS^O17|26898426016788|P|2.4
                var msh = new HL7MessageMSH { MessageType = "RAS^O17", MessageControl = g.FirstOrDefault().UniqueId, };
                var mshProperties = typeof(HL7MessageMSH).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(msh)).ToArray();
                var mshString = string.Join("|", mshProperties);
                headerProperties.Add(mshString);

                //PID||0000026898|0000026898^^^^IDCard~0000000082^^^^patientNo~432822195610096342^^^^IdentifyNO||刘三女||19561009000000|F|||||||||||||||湖南省郴州市
                var pid = new HL7MessagePID
                {
                    PatientID = patient.UniqueId,
                    PatientIdentifierList = $"{patient.Clinic?.SerialNumber}^^^^IDCard~{patient.Hospitalization?.HospitalNumber}^^^^PatientNO~{patient?.CertificateCode}^^^^IdentifyNO",
                    PatientName = patient.DisplayName,
                    Birthday = string.Format("{0:yyyyMMddHHmmss}", patient?.Birthday),
                    AdministrativeSex = patient?.Gender == "男" ? "M" : "F",
                };
                var pidProperties = typeof(HL7MessagePID).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(pid)).ToArray();
                var pidString = string.Join("|", pidProperties);
                headerProperties.Add(pidString);

                //PV1||I|9906^^^1127||||||||||2|||||01|38176
                var pv1 = new HL7MessagePV1
                {
                    PatientClass = patient.Clinic.SerialNumber.Length > 0 ? "O" : "P",
                    AssignedPatientLocation = $"{patient.Hospitalization?.ResidedAreaId}^^{patient.Hospitalization?.BedNo}^{patient.Hospitalization?.AdmittedDepartmentId}",
                    ReAdmissionIndicator = "就诊次数",
                    PatientType = "自费/医保",
                    VisitNumber = patient.UniqueId,//"就诊号码"
                };
                var pv1Properties = typeof(HL7MessagePV1).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(pv1)).ToArray();
                var pv1String = string.Join("|", pv1Properties);
                headerProperties.Add(pv1String);
                var header = string.Join("\r\n", headerProperties);

                // body
                var body = g.SelectMany(m =>
                {
                    var bodyProperties = new List<string>();
                    var action = mongo.ActionJournalCollection.AsQueryable().FirstOrDefault(a => a.TargetId == m.UniqueId);
                    var dateStr = string.Format("{0:yyyyMMddHHmmssffff}", DateTime.Now);
                    //ORC|NW|2689842601CSS|256707|381760|||^&1^^^^A||20180925122348|MDSD||100082|||||3001||||^^1127
                    var orc = new HL7MessageORC
                    {
                        PlacerOrderNumber = $"{dateStr.Substring(10)}SFRA", // 1462711203CSS
                        FillerOrderNumber = $"{dateStr.Substring(10)}", // 256594
                        PlacerGroupNumber = $"{dateStr.Substring(6, 6)}", // 291200
                        QuantityTiming = $@"{m.Goods?.Dosage}&{m.Goods?.UsedUnit}^&ONCE^^^^A",
                        DateTimeofTransaction = Regex.Replace(m.CreatedTime.GetDateTimeFormats('s')[0].ToString(), @"[^0-9]+", ""), // 事务时间 20180830192248
                        EnteredBy = m.DoctorId,  //操作员ID MDSD
                        OrderingProvider = m.DoctorId, // 下单方提供者 100069
                        EnteringOrganization = m.Doctor?.DepartmentId, // 操作员所属科室
                        OrderingFacilityName = m.Patient.Hospitalization?.AdmittedDepartmentId, //患者所在部门
                    };
                    var orcProperties = typeof(HL7MessageORC).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(orc)).ToArray();
                    var orcString = string.Join("|", orcProperties);
                    bodyProperties.Add(orcString);

                    //RXO|F00000005296^F00000005296^n|1||2小时|||||0|||2小时
                    var rxo = new HL7MessageRXO
                    {
                        RequestedGiveCode = $"{m.GoodsId}^{m.GoodsId}^{(m.Goods?.Filter == nameof(Goods) ? "d" : "n")}",
                        RequestedGiveAmountMix = m.Qty.ToString(), //剂量?
                        RequestedGiveUnits = m.Goods.UsedUnit,
                        DeliverToLocation = $"^^^&{m.DepartmentDestinationId}", //发药部门
                        RequestedDispenseUnits = m.Goods?.UsedUnit,
                    };
                    var rxoProperties = typeof(HL7MessageRXO).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(rxo)).ToArray();
                    var rxoString = string.Join("|", rxoProperties);
                    bodyProperties.Add(rxoString);

                    //RXA|0||20180925122348|20180925122348|0^0^5|1|^2小时|||000000|3001|||||||||2|A|20180925122348
                    var rxa = new HL7MessageRXA
                    {
                        GiveSubID = "0",
                        StartDatetime = string.Format("{0:yyyyMMddHHmmss}", m.CreatedTime),
                        EndDatetime = string.Format("{0:yyyyMMddHHmmss}", m.FinishTime == null ? DateTime.Now: m.FinishTime),
                        AdministeredCode = $"0^{(m.Goods.Filter == nameof(Goods) ? "3" : "5")}^5",
                        AdministeredAmount = m.QtyActual.ToString(),
                        AdministeredUnits = m.Goods.UsedUnit,
                        AdministeringProvider = m.DoctorId.ToString(),
                        AdministeredLocation = m.Doctor?.DepartmentId, //"执行科室"
                        SystemntryTime = string.Format("{0:yyyyMMddHHmmss}", m.FinishTime == null ? DateTime.Now : m.FinishTime),
                    };
                    var rxaProperties = typeof(HL7MessageRXA).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(rxa)).ToArray();
                    var rxaString = string.Join("|", rxaProperties);
                    bodyProperties.Add(rxaString);

                    //RXR | 39
                    var rxr = new HL7MessageRXR { };
                    var rxrProperties = typeof(HL7MessageRXR).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(rxr)).ToArray();
                    var rxrString = string.Join("|", rxrProperties);
                    bodyProperties.Add(rxrString);

                    var obxProperties = typeof(HL7MessageOBX).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(new HL7MessageOBX { })).ToArray();
                    var obxString = string.Join("|", obxProperties);
                    bodyProperties.Add(obxString);

                    //NTE|||1400
                    var net = new HL7MessageNET { Comment = m.Goods?.Price.ToString(), };
                    var netProperties = typeof(HL7MessageNET).GetProperties().Where(p => p.CanWrite).Select(o => o.GetValue(net)).ToArray();
                    var netString = string.Join("|", netProperties);
                    bodyProperties.Add(netString);
                    return bodyProperties;
                }).ToList();
                // 合并 header body
                propertie.Add(header);
                propertie.AddRange(body);
                return string.Join("\r\n", propertie);
            }).ToArray();
            // 预支记录ID集合
            hospitalPreIds = ids;
            return data;
        }
    }

    public class HL7MessageMSH
    {
        public string FieldSeparator { get; set; } = "MSH";
        public string EncodingCharacters { get; set; } = @"^~\&";
        public string SendingApplication { get; set; } = "SFRA";
        public string SendingFacility { get; set; } = string.Empty;
        public string ReceingApplication { get; set; } = "HIS";
        public string ReceingFacility { get; set; } = string.Empty;
        public string DateTimeOfMessage { get; set; } = Regex.Replace(DateTime.Now.GetDateTimeFormats('s')[0].ToString(), @"[^0-9]+", "");
        public string Security { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public string MessageControl { get; set; } = string.Empty;
        public string Processing { get; set; } = "P";
        public string Version { get; set; } = "2.4";
        //public string SequenceNumber { get; set; }
        //public string ContinuationPointer { get; set; }
        //public string AcceptAcknowledgmentType { get; set; }
        //public string ApplicationAcknowledgmentType { get; set; }
        //public string CountryCode { get; set; }
        //public string CharacterSet { get; set; }
        //public string PrincipalLanguageOfMessage { get; set; }
        //public string AlternateCharacterSetHandlingScheme { get; set; }
        //public string ConformanceStatement { get; set; }
    }

    public class HL7MessagePID
    {
        public string MessageHeader { get; set; } = "PID";
        //
        public string Pid { get; set; } = string.Empty;
        //患者ID
        public string PatientID { get; set; } = string.Empty;
        //患者标识表
        public string PatientIdentifierList { get; set; }
        //备选患者ID
        public string AlternatePatientID { get; set; } = string.Empty;
        //患者姓名
        public string PatientName { get; set; } = string.Empty;
        //母亲的婚前姓
        public string MotherMaidenName { get; set; } = string.Empty;
        //出生日期
        public string Birthday { get; set; } = string.Empty;
        //性别
        public string AdministrativeSex { get; set; } = string.Empty;
        //public string PatientAlias { get; set; }
        //public string Race { get; set; }
        //public string PatientAddress { get; set; }
        //public string CountyCode { get; set; }
        //public string PhoneNumberHome { get; set; }
        //public string PhoneNumberBusiness { get; set; }
        //public string PrimaryLanguage { get; set; }
        //public string MaritalStatus { get; set; }
        //public string Religion { get; set; }
        //public string PatientAccountNumber { get; set; }
        //public string SSNNumberPatient { get; set; }
        //public string DriverLicenseNumber { get; set; }
        //public string MotherIdentifier { get; set; }
        //public string EthnicGroup { get; set; }
        //public string BirthPlace { get; set; }
        //public string MultipleBirthIndicator { get; set; }
        //public string BirthOrder { get; set; }
        //public string Citizenship { get; set; }
        //public string VeteransMilitaryStatus { get; set; }
        //public string Nationality { get; set; }
        //public string PatientDeathDateAndTime { get; set; }
        //public string PatientDeathIndicator { get; set; }
        //public string IdentityUnknownIndicator { get; set; }
        //public string IdentityReliabilityCode { get; set; }
        //public string LastUpdateDateTime { get; set; }
        //public string LastUpdateFacility { get; set; }
        //public string SpeciesCode { get; set; }
        //public string BreedCode { get; set; }
        //public string Strain { get; set; }
        //public string ProductionClassCode { get; set; }

    }
    //public class PatientIdentifier
    //{
    //    public string IDCard { get; set; }
    //    public string PatientNO { get; set; }
    //    public string IdentifyNO { get; set; }
    //}

    public class HL7MessagePV1
    {
        public string MessageHeader { get; set; } = "PV1";
        //设置ID-PV1
        public string PV1 { get; set; } = string.Empty;
        //患者类别
        public string PatientClass { get; set; } = string.Empty; // --
        //患者当前位置
        public string AssignedPatientLocation { get; set; } = string.Empty;
        //入院类型
        public string AdmissionType { get; set; } = string.Empty;
        //预收入院号码
        public string PreadmitNumber { get; set; } = string.Empty;
        //前患者位置 
        public string PriorPatientLocation { get; set; } = string.Empty;
        //接诊医生
        public string AttendingDoctor { get; set; } = string.Empty;
        //转诊医生
        public string ReferringDoctor { get; set; } = string.Empty;
        //咨询医生
        public string ConsultingDoctor { get; set; } = string.Empty;
        //医院服务 - 10
        public string HospitalService { get; set; } = string.Empty;
        //临时位置
        public string TemporaryLocation { get; set; } = string.Empty;
        public string PreadmitTestIndicator { get; set; } = string.Empty;
        //预收入院检验标识
        public string ReAdmissionIndicator { get; set; } = string.Empty; // --
        //入院来源
        public string AdmitSource { get; set; } = string.Empty;
        //走动状况
        public string AmbulatoryStatus { get; set; } = string.Empty;
        //VIP标识
        public string VIPIndicator { get; set; } = string.Empty;
        //入院医生
        public string AdmittingDoctor { get; set; } = string.Empty;
        //患者类型
        public string PatientType { get; set; } = string.Empty;
        //就诊号码 - 19
        public string VisitNumber { get; set; } = string.Empty;
    }

    public class HL7MessageORC
    {
        public string MessageHeader { get; set; } = "ORC";
        //申请控制
        public string OrderControl { get; set; } = "NW";
        //下单方申请单编号
        public string PlacerOrderNumber { get; set; } = string.Empty;
        //执行方申请单编号
        public string FillerOrderNumber { get; set; } = string.Empty;
        //下单方申请单组编号
        public string PlacerGroupNumber { get; set; } = string.Empty;
        //申请单状态 - 5
        public string OrderStatus { get; set; } = string.Empty;
        //应答标记
        public string ResponseFlag { get; set; } = string.Empty;
        //数量/频率
        public string QuantityTiming { get; set; } = string.Empty;
        //父层
        public string Parent { get; set; } = string.Empty;
        //事务日期/时间
        public string DateTimeofTransaction { get; set; } = string.Empty;
        //输入者 - 10
        public string EnteredBy { get; set; } = string.Empty;
        //校验者
        public string VerifiedBy { get; set; } = string.Empty;
        //下单方提供者
        public string OrderingProvider { get; set; } = string.Empty;
        //输入者所在位置
        public string EntererLocation { get; set; } = string.Empty;
        //回访电话号码
        public string CallBackPhoneNumber { get; set; } = string.Empty;
        //申请生效日期/时间 -15
        public string OrderEffectiveDateTime { get; set; } = string.Empty;
        //申请控制编码原因
        public string OrderControlCodeReason { get; set; } = string.Empty;
        //输入者所属组织
        public string EnteringOrganization { get; set; } = string.Empty;
        //输入设备
        public string EnteringDevice { get; set; } = string.Empty;
        //申请发动者
        public string ActionBy { get; set; } = string.Empty;
        //费用支付相关事项 - 20
        public string AdvancedBeneficiaryNoticeCode { get; set; } = string.Empty;
        ////提出申请的机构名称
        public string OrderingFacilityName { get; set; } = string.Empty;
        ////提出申请的机构地址
        //public string OrderingFacilityAddress { get; set; } = string.Empty;
        ////提出申请的机构电话号码
        //public string OrderingFacilityPhoneNumber { get; set; } = string.Empty;
        ////申请提供者地址
        //public string OrderingProviderAddress { get; set; } = string.Empty;
        ////医嘱状态修饰符 -25
        //public string OrderStatusModifier { get; set; } = string.Empty;
    }

    public class HL7MessageRXO
    {
        public string MessageHeader { get; set; } = "RXO";
        //药品信息
        public string RequestedGiveCode { get; set; }
        //请求给予的最小量
        public string RequestedGiveAmountMix { get; set; }
        //请求给予的最大量
        public string RequestedGiveAmountMax { get; set; }
        //给予单位
        public string RequestedGiveUnits { get; set; }
        //请求给予的剂型 - 5
        public string RequestedDosageForm { get; set; }
        //供应方的药物/治疗指导
        public string PharmacyOrTreatmentInstructions { get; set; }
        //供应方的执行指导
        public string AdministrationInstructions { get; set; }
        //传送位置
        public string DeliverToLocation { get; set; }
        //允许替代
        public string AllowSubstitutions { get; set; }
        //请求分发代码 - 10
        public string RequestedDispenseCode { get; set; }
        //请求分发量
        public string RequestedDispenseAmount { get; set; }
        //请求分发单位
        public string RequestedDispenseUnits { get; set; }
        //补充数量
        //public string NumberOfRefills { get; set; }
        ////申请供应方的DEA号码
        //public string DEANumber { get; set; }
        //// - 15
        //public string SuppliersVerifierID { get; set; }
        ////需要人评审
        //public string NeedsHumanReview { get; set; }
        ////请求给予每时间单位的量
        //public string RequestedGivePer { get; set; }
        ////请求给予浓度
        //public string RequestedGiveStrength { get; set; }
        ////给予浓度单位
        //public string RequestedGiveStrengthUnits { get; set; }
        ////适应症 - 20
        //public string Indication { get; set; }
        ////请求给予速度
        //public string RequestedGiveRateAmount { get; set; }
        ////请求给予速度单位
        //public string RequestedGiveRateUnits { get; set; }
        //// 每日总剂量
        //public string TotalDailyDose { get; set; }
        ////补充代码
        //public string SupplementaryCode { get; set; }
    }

    public class HL7MessageRXR
    {
        public string MessageHeader { get; set; } = "RXR";
        //途径
        public string Route { get; set; }
        ////使用部位
        //public string AdministrationSite { get; set; }
        ////使用设施
        //public string AdministrationDevice { get; set; }
        ////使用方法
        //public string AdministrationMethod { get; set; }
        ////途径指导
        //public string RoutingInstruction { get; set; }
    }

    public class HL7MessageRXA
    {
        public string MessageHeader { get; set; } = "RXA";
        public string GiveSubID { get; set; }
        public string AdministrationSubID { get; set; }
        //医嘱开始时间
        public string StartDatetime { get; set; }
        //医嘱结束时间
        public string EndDatetime { get; set; }
        //执行代码 - 5
        public string AdministeredCode { get; set; }
        //执行数量
        public string AdministeredAmount { get; set; }
        //执行单位
        public string AdministeredUnits { get; set; }
        //执行剂型
        public string AdministeredDosageForm { get; set; }
        //执行注意事项
        public string AdministrationNotes { get; set; }
        //执行者 - 10
        public string AdministeringProvider { get; set; }
        //执行定位
        public string AdministeredLocation { get; set; }
        //执行每时间单位的量
        public string MyPrAdministeredPeroperty { get; set; }
        //执行浓度
        public string AdministeredStrength { get; set; }
        //执行浓度单位
        public string AdministeredStrengthUnits { get; set; }
        //物品批号
        public string SubstanceLotNumber { get; set; }
        //物品过期时间
        public string SubstanceExpirationDate { get; set; }
        //物品生产商名称
        public string SubstanceManufacturerName { get; set; }
        //拒绝物品/治疗原因
        public string RefusalReason { get; set; }
        //适应症
        public string Indication { get; set; }
        //完成情况 - 20
        public string CompletionStatus { get; set; } = "2";
        //RXA行动代码
        public string ActionCodeRXA { get; set; } = "A";
        //系统录入的日期/时间 
        public string SystemntryTime { get; set; }
    }

    public class HL7MessageFT1
    {
        public string MessageHeader { get; set; } = "FT1";
        public string FT1 { get; set; }
        //事务ID 
        public string TransactionID { get; set; }
        //事务批处理ID
        public string TransactionBatchID { get; set; }
        //事务日期
        public string TransactionDate { get; set; }
        //事务过账日期
        public string TransactionPostingDate { get; set; }
        //事务类型
        public string TransactionType { get; set; }
        //事务代码
        public string TransactionCode { get; set; }
        //事务描述
        public string TransactionDescription { get; set; }
        //事务描述备选
        public string TransactionDescriptionAlt { get; set; }
        //事务数量
        public string TransactionQuantity { get; set; }
        //事务总金额
        public string TransactionAmountExtended { get; set; }
        //事务单价
        public string TransactionAmountUnit { get; set; }
    }

    public class HL7MessageOBX
    {
        public string MessageHeader { get; set; } = "OBX";
    }

    public class HL7MessageNET
    {
        public string MessageHeader { get; set; } = "NTE";
        public string NTE { get; set; }
        //注释的来源
        public string SourceOfComment { get; set; }
        //具体的说明
        public string Comment { get; set; }
        //注释类型
        public string CommentType { get; set; }
    }
}