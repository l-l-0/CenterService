//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using Newtonsoft.Json;
using System.Xml.Serialization;

#pragma warning disable CS1591

namespace HealthCare.WebService.Models
{
    public abstract class SZGJObject
    {
        [XmlElement("resultCode")]
        public int Code { get; set; }
        [XmlElement("resultMessage")]
        public string Message { get; set; }
    }


    /// <summary>
    ///     药品信息
    /// </summary>
    [XmlRoot("response")]
    public partial class SZGJDrugInfo : SZGJObject
    {
        [XmlElement("drug")]
        public SZGJDrug[] Drugs { get; set; }
    }

    [XmlRoot("drug")]
    public partial class SZGJDrug
    {
        /// <summary>
        ///     唯一 ID
        /// </summary>
        [XmlElement("ID")]
        public string Id { get; set; }
        /// <summary>
        ///     药名
        /// </summary>
        [XmlElement("NAME")]
        public string Name { get; set; }
        /// <summary>
        ///     药品编码
        /// </summary>
        [XmlElement("CODE")]
        public string Code { get; set; }
        /// <summary>
        ///     进价
        /// </summary>
        [XmlElement("COSTPRICE")]
        public double CostPrice { get; set; }
        /// <summary>
        ///     单价
        /// </summary>
        [XmlElement("OUTPRICE")]
        public double OutPrice { get; set; }
        /// <summary>
        ///     状态
        /// </summary>
        [XmlElement("USESTATUS")]
        public string UseStatus { get; set; }
        /// <summary>
        ///     规格
        /// </summary>
        [XmlElement("REGU")]
        public string Regu { get; set; }
        /// <summary>
        ///     医保编码
        /// </summary>
        [XmlElement("INSURECODE")]
        public string InsureCode { get; set; }
        /// <summary>
        ///     助记码 1
        /// </summary>
        [XmlElement("HELPCODE")]
        public string HelpCode { get; set; }
        /// <summary>
        ///     助记码2
        /// </summary>
        [XmlElement("OTHERHELPCODE")]
        public string OtherHelpCode { get; set; }
        /// <summary>
        ///     助记码3
        /// </summary>
        [XmlElement("GBCODE")]
        public string GbCode { get; set; }
        /// <summary>
        ///     单位
        /// </summary>
        [XmlElement("UNIT")]
        public string Unit { get; set; }
        /// <summary>
        ///     进口国产
        /// </summary>
        [XmlElement("COUNTRYSTATUS")]
        public string CountryStatus { get; set; }
        /// <summary>
        ///     生产厂家
        /// </summary>
        [XmlElement("APPLY")]
        public string Apply { get; set; }
        /// <summary>
        ///     药理分类
        /// </summary>
        [XmlElement("TYPE")]
        public string Type { get; set; }
        /// <summary>
        ///     成份（剂量）
        /// </summary>
        [XmlElement("DOSAGE")]
        public string Dosage { get; set; }
        /// <summary>
        ///     成份单位
        /// </summary>
        [XmlElement("DOSAGEUNIT")]
        public string DosageUnit { get; set; }
        /// <summary>
        ///     剂型
        /// </summary>
        [XmlElement("DOSETYPE")]
        public string DoseType { get; set; }
        /// <summary>
        ///     皮试标识
        /// </summary>
        [XmlElement("TESTFLAG")]
        public string TestFlag { get; set; }
        /// <summary>
        ///     药品单位转换，会有多个，与unit关系，价格也需要乘以转换系数
        /// </summary>
        [XmlArray("unitConversions")]
        [XmlArrayItem("unitConvert", IsNullable = false)]
        public SZGJDrugUnitConvert[] UnitConversions { get; set; }
    }

    public partial class SZGJDrugUnitConvert
    {
        /// <summary>
        ///     单位
        /// </summary>
        [XmlAttribute("UNIT")]
        public string Unit { get; set; }
        /// <summary>
        ///     系数
        /// </summary>
        [XmlAttribute("CONVERSION")]
        public double Conversion { get; set; }
    }


    /// <summary>
    ///     医嘱信息
    /// </summary>
    [XmlRoot("APPLYS")]
    public partial class SZGJApplyInfo : SZGJObject
    {
        /// <summary>
        ///     申请信息，可以有多条
        /// </summary>
        [XmlElement("APPLY")]
        public SZGJApply[] Applies { get; set; }
    }

    [XmlRoot("APPLY")]
    public partial class SZGJApply
    {
        /// <summary>
        ///     发药单号
        /// </summary>
        [XmlElement("RECORDNO")]
        public string RecordNo { get; set; }
        /// <summary>
        ///     发药时间
        /// </summary>
        [XmlElement("OPERDATETIME")]
        public string OperDateTime { get; set; }
        /// <summary>
        ///     发药员工号
        /// </summary>
        [XmlElement("OPERNO")]
        public string OperNo { get; set; }
        /// <summary>
        ///     发药员姓名
        /// </summary>
        [XmlElement("OPERNAME")]
        public string OperName { get; set; }
        /// <summary>
        ///     患者科室编码
        /// </summary>
        [XmlElement("PATDEPCODE")]
        public string PatDepCode { get; set; }
        /// <summary>
        ///     科室名称
        /// </summary>
        [XmlElement("PATDEPNAME")]
        public string PatDepName { get; set; }
        /// <summary>
        ///     药品科室编码
        /// </summary>
        [XmlElement("DRUGDEPCODE")]
        public string DrugDepCode { get; set; }
        /// <summary>
        ///     药品科室名称
        /// </summary>
        [XmlElement("DRUGDEPNAME")]
        public string DrugDepName { get; set; }
        /// <summary>
        ///     诊断
        /// </summary>
        [XmlElement("DIAGNOSE")]
        public string Diagnose { get; set; }
        /// <summary>
        ///     患者 ID
        /// </summary>
        [XmlElement("PID")]
        public string PId { get; set; }
        /// <summary>
        ///     住院号
        /// </summary>
        [XmlElement("INPATIENTNO")]
        public string InPatientNo { get; set; }
        /// <summary>
        ///     门诊号
        /// </summary>
        [XmlElement("OUTPATIENTNO")]
        public string OutPatientNo { get; set; }
        /// <summary>
        ///     病人姓名
        /// </summary>
        [XmlElement("PATNAME")]
        public string PatName { get; set; }
        /// <summary>
        ///     出生日期
        /// </summary>
        [XmlElement("DATEOFBIRTH")]
        public string DateOfBirth { get; set; }
        /// <summary>
        ///     床号
        /// </summary>
        [XmlElement("bedno")]
        public string BedNo { get; set; }
        [XmlArray("ROWSET")]
        [XmlArrayItem("ROW", IsNullable = false)]
        public SZGJAPPLYROW[] RowSet { get; set; }
    }

    [XmlRoot("ROWSET")]
    public partial class SZGJAPPLYROW
    {
        /// <summary>
        ///     医嘱 ID
        /// </summary>
        [XmlElement("DOCTORADVICEDETAILID")]
        public string DoctorAdviceDetailId { get; set; }
        /// <summary>
        ///     医嘱内容
        /// </summary>
        [XmlElement("CONTENT")]
        public string Content { get; set; }
        /// <summary>
        ///     药品内容
        /// </summary>
        [XmlElement("DRUG_ID")]
        public string DrugId { get; set; }
        /// <summary>
        ///     药品编码
        /// </summary>
        [XmlElement("CODE")]
        public string Code { get; set; }
        /// <summary>
        ///     药品名称
        /// </summary>
        [XmlElement("NAME")]
        public string Name { get; set; }
        /// <summary>
        ///     开嘱医生工号
        /// </summary>
        [XmlElement("INPUTDOCNUM")]
        public string InputDocNum { get; set; }
        /// <summary>
        ///     开嘱医生姓名
        /// </summary>
        [XmlElement("INPUTDOCNAME")]
        public string InputDocName { get; set; }
        /// <summary>
        ///     开嘱时间
        /// </summary>
        [XmlElement("STARTDATETIME")]
        public string StartDateTime { get; set; }
        /// <summary>
        ///     数量 退药时为负数
        /// </summary>
        [XmlElement("AMOUNT")]
        public double Amount { get; set; }
        /// <summary>
        ///     单位
        /// </summary>
        [XmlElement("UNIT")]
        public string Unit { get; set; }
        /// <summary>
        ///     单价
        /// </summary> 
        [XmlElement("OUTPRICE")]
        public double OutPrice { get; set; }
    }


    /// <summary>
    ///     调拨记录
    /// </summary>
    [XmlRoot("ROWSET")]
    public partial class SZGJStock : SZGJObject
    {
        [XmlElement("ROW")]
        public SZGJStockROW[] Stocks { get; set; }
    }

    public partial class SZGJStockROW
    {
        /// <summary>
        ///     药品 ID
        /// </summary>
        [XmlElement("DRUG_ID")]
        public string DrugId { get; set; }
        /// <summary>
        ///     药品编码
        /// </summary>
        [XmlElement("CODE")]
        public string Code { get; set; }
        /// <summary>
        ///     药品名称
        /// </summary>
        [XmlElement("NAME")]
        public string Name { get; set; }
        /// <summary>
        ///     药品规格
        /// </summary>
        [XmlElement("REGU")]
        public string Regu { get; set; }
        /// <summary>
        ///     进价
        /// </summary>
        [XmlElement("INPRICE")]
        public double InPrice { get; set; }
        /// <summary>
        ///     单价
        /// </summary>
        [XmlElement("OUTPRICE")]
        public double OutPrice { get; set; }
        /// <summary>
        ///     数量
        /// </summary>
        [XmlElement("AMOUNT")]
        public int Amount { get; set; }
        /// <summary>
        ///     单位
        /// </summary>
        [XmlElement("UNIT")]
        public string Unit { get; set; }
        /// <summary>
        ///     生产厂家
        /// </summary>
        [XmlElement("APPLY")]
        public string Apply { get; set; }
        [XmlElement("sendDepart")]
        public string SendDepart { get; set; }
        [XmlElement("sendName")]
        public string SendName { get; set; }
        [XmlElement("reciveDepart")]
        public string ReciveDepart { get; set; }
        [XmlElement("reciveName")]
        public string ReciveName { get; set; }
        [XmlElement("expireDate")]
        public string ExpireDate { get; set; }
        [XmlElement("batchNo")]
        public string BatchNo { get; set; }
    }


    /// <summary>
    ///     员工信息
    /// </summary>
    [XmlRoot("ROWSET")]
    public partial class SZGJEmployee : SZGJObject
    {
        [XmlElement("ROW")]
        public SZGJEmployeeROW[] Employees { get; set; }
    }

    public partial class SZGJEmployeeROW
    {
        /// <summary>
        ///     职工工号
        /// </summary>
        [XmlElement("workNO")]
        public string WorkNO { get; set; }
        /// <summary>
        ///     职工姓名
        /// </summary>
        [XmlElement("name")]
        public string Name { get; set; }
        /// <summary>
        ///     科室代码
        /// </summary>
        [XmlElement("departCode")]
        public string DepartCode { get; set; }
        /// <summary>
        ///     科室名称
        /// </summary>
        [XmlElement("departName")]
        public string DepartName { get; set; }
        /// <summary>
        ///     岗位
        /// </summary>
        [XmlElement("position")]
        public string Position { get; set; }
    }


    /// <summary>
    ///     患者信息
    /// </summary>
    [XmlRoot("ROWSET")]
    public partial class SZGJPatient : SZGJObject
    {
        [XmlElement("ROW")]
        public SZGJPatientROW[] Patients { get; set; }
    }

    public partial class SZGJPatientROW
    {
        /// <summary>
        ///     患者ID
        /// </summary>
        [XmlElement("PID")]
        public string PId { get; set; }
        /// <summary>
        ///     住院号
        /// </summary>
        [XmlElement("INPATIENTNO")]
        public string InPatientNo { get; set; }
        /// <summary>
        ///     门诊号
        /// </summary>
        [XmlElement("OUTPATIENTNO")]
        public string OutPatientNo { get; set; }
        /// <summary>
        ///     病人姓名
        /// </summary>
        [XmlElement("PATNAME")]
        public string PatName { get; set; }
        /// <summary>
        ///     出生日期
        /// </summary>
        [XmlElement("DATEOFBIRTH")]
        public string DateOfBirth { get; set; }
        /// <summary>
        ///     患者科室编码
        /// </summary>
        [XmlElement("PATDEPCODE")]
        public string PatDepCode { get; set; }
        /// <summary>
        ///     科室名称
        /// </summary>
        [XmlElement("PATDEPNAME")]
        public string PatDepName { get; set; }
        /// <summary>
        ///     床号
        /// </summary>
        [XmlElement("bedno")]
        public string BedNo { get; set; }
    }
}