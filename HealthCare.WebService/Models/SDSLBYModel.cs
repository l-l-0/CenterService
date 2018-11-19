//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using System;
using System.Xml.Serialization;

#pragma warning disable CS1591

namespace HealthCare.WebService.Models
{

    public abstract class SDSLObject
    {
        [XmlElement("ResultCode")]
        public int ResultCode { get; set; }
        [XmlElement("ResultContent")]
        public string ResultContent { get; set; }
    }
    [XmlRoot("Request")]
    public partial class Request : SDSLObject
    {
        [XmlElement("OPDrugOrds")]
        public OPDrugOrds[] OPDrugOrds { get; set; }
        [XmlElement("DictInfos")]
        public DictInfos[] DictInfos { get; set; }
        [XmlElement("DrugInfos")]
        public DrugInfos[] DrugInfos { get; set; }
    }
    /// <summary>
    /// 医嘱处方信息
    /// </summary>   
    [XmlRoot("OPDrugOrds")]
    public partial class OPDrugOrds
    {
        [XmlElement("OPDrugOrdInfo")]
        public OPDrugOrdInfo[] OPDrugOrdInfo { get; set; }
    }
    [XmlRoot("OPDrugOrdInfo")]
    public partial class OPDrugOrdInfo
    {
        [XmlElement("PatId")]
        public string PatId { get; set; }
        [XmlElement("Name")]
        public string Name { get; set; }
        [XmlElement("Age")]
        public System.DateTime Age { get; set; }
        public string Sex { get; set; }
        [XmlElement("DiagnoDesc")]
        public string DiagnoDesc { get; set; }
        [XmlElement("PatHeight")]
        public string PatHeight { get; set; }
        [XmlElement("PName")]
        public string PName { get; set; }
        [XmlElement("FName")]
        public string FName { get; set; }
        [XmlElement("PDateTime")]
        public string PDateTime { get; set; }
        [XmlElement("FDateTime")]
        public string FDateTime { get; set; }
        [XmlElement("DateTime")]
        public string DateTime { get; set; }
        [XmlElement("RecCtLoc")]
        public string RecCtLoc { get; set; }
        [XmlElement("PatCompany")]
        public string PatCompany { get; set; }
        [XmlElement("AdmDateTime")]
        public DateTime AdmDateTime { get; set; }
        [XmlElement("AdmCardNo")]
        public string AdmCardNo { get; set; }
        [XmlElement("PrescNo")]
        public string PrescNo { get; set; }
        [XmlElement("AdmCtLoc")]
        public string AdmCtLoc { get; set; }
        [XmlElement("AdmCtLocCode")]
        public string AdmCtLocCode { get; set; }
        [XmlElement("WindowNo")]
        public object WindowNo { get; set; }
        [XmlElement("TelePhone")]
        public string TelePhone { get; set; }
        [XmlElement("Address")]
        public string Address { get; set; }
        [XmlElement("PrescMake")]
        public string PrescMake { get; set; }
        [XmlElement("PrescNum")]
        public string PrescNum { get; set; }
        [XmlElement("PrescType")]
        public string PrescType { get; set; }
        [XmlElement("DocCtLoc")]
        public string DocCtLoc { get; set; }
        [XmlElement("Doctor")]
        public string Doctor { get; set; }
        [XmlElement("HerbPrescMake")]
        public string HerbPrescMake { get; set; }
        [XmlElement("HerbPrescNum")]
        public string HerbPrescNum { get; set; }
        [XmlElement("HerbPrescStartDate")]
        public string HerbPrescStartDate { get; set; }
        [XmlElement("HerbPrescStopDate")]
        public string HerbPrescStopDate { get; set; }
        [XmlElement("PrescNote")]
        public string PrescNote { get; set; }
        [XmlElement("OrdDateTime")]
        public string OrdDateTime { get; set; }
        [XmlElement("BalanceDateTime")]
        public string BalanceDateTime { get; set; }
        [XmlElement("Station")]
        public string Station { get; set; }
        [XmlElement("DrugInfos")]
        public DrugInfos DrugInfos { get; set; }
    }
    [XmlRoot("DrugInfos")]
    public partial class DrugInfos
    {
        [XmlElement("OPDrugInfo")]
        public OPDrugInfo[] OPDrugInfos { get; set; }
        [XmlElement("DrugInfo")]
        public DrugInfo[] DrugInfo { get; set; }
    }
    [XmlRoot("OPDrugInfo")]
    public partial class OPDrugInfo
    {
        [XmlElement("DrugCode")]
        public string DrugCode { get; set; }
        [XmlElement("DrugName")]
        public string DrugName { get; set; }
        [XmlElement("Qty")]
        public int Qty { get; set; }
        [XmlElement("Unit")]
        public string Unit { get; set; }
        [XmlElement("Dosage")]
        public decimal Dosage { get; set; }
        [XmlElement("DosageUnit")]
        public string DosageUnit { get; set; }
        [XmlElement("Common")]
        public string Common { get; set; }
        [XmlElement("Freq")]
        public string Freq { get; set; }
        [XmlElement("Phdur")]
        public string Phdur { get; set; }
        [XmlElement("Price")]
        public double Price { get; set; }
        [XmlElement("TotalPrice")]
        public double TotalPrice { get; set; }
        [XmlElement("Skintest")]
        public string Skintest { get; set; }
        [XmlElement("OrdreMark")]
        public string OrdreMark { get; set; }
        [XmlElement("Specinst")]
        public string Specinst { get; set; }
        [XmlElement("Invoice")]
        public string Invoice { get; set; }
        [XmlElement("Manuactory")]
        public string Manuactory { get; set; }
        [XmlElement("Specification")]
        public string Specification { get; set; }
        [XmlElement("quotiety")]
        public string Quotiety { get; set; }
        [XmlElement("OrdRowID")]
        public string OrdRowID { get; set; }
    }
    public partial class DictInfos
    {
        [XmlElement("UserDict")]
        public UserDict[] UserDict { get; set; }
        [XmlElement("DeptDict")]
        public DeptDict[] DeptDict { get; set; }
    }
    /// <summary>
    /// 员工信息
    /// </summary>
    [XmlRoot("UserDict")]
    public partial class UserDict
    {

        [XmlElement("UserCode")]
        public string UserCode { get; set; }
        [XmlElement("UserDesc")]
        public string UserDesc { get; set; }
        [XmlElement("Alias")]
        public string Alias { get; set; }
        [XmlElement("TechnicalTitle")]
        public string TechnicalTitle { get; set; }
        [XmlElement("TechnicalTitleCode")]
        public string TechnicalTitleCode { get; set; }
        [XmlElement("CtLocCode")]
        public string CtLocCode { get; set; }
        [XmlElement("CreateDate")]
        public string CreateDate { get; set; }
        [XmlElement("UserJob")]
        public string UserJob { get; set; }
        [XmlElement("UserPsw")]
        public string UserPsw { get; set; }
    }
    [XmlRoot("Response")]
    public partial class Response : SDSLObject
    {

        [XmlElement("DictInfos")]
        public DictInfos[] DictInfos { get; set; }
    }
    /// <summary>
    /// 科室信息
    /// </summary>
    public partial class DeptDict
    {
        [XmlElement("DeptCode")]
        public string DeptCode { get; set; }
        [XmlElement("DeptDesc")]
        public string DeptDesc { get; set; }
        [XmlElement("Alias")]
        public string Alias { get; set; }
        [XmlElement("TypeCode")]
        public string TypeCode { get; set; }
        [XmlElement("Address")]
        public string Address { get; set; }
        [XmlElement("CreateDate")]
        public string CreateDate { get; set; }
        [XmlElement("Allowind")]
        public string Allowind { get; set; }
    }
    /// <summary>
    /// 物品信息
    /// </summary>
    [XmlRoot("DrugInfo")]
    public partial class DrugInfo
    {
        [XmlElement("ArcimCode")]
        public string ArcimCode { get; set; }
        [XmlElement("ArcimDesc")]
        public string ArcimDesc { get; set; }
        [XmlElement("OrdCategoryCode")]
        public int OrdCategoryCode { get; set; }
        [XmlElement("ARCItemCatCode")]
        public string ARCItemCatCode { get; set; }
        [XmlElement("Specification")]
        public string Specification { get; set; }
        [XmlElement("TradeName")]
        public string TradeName { get; set; }
        [XmlElement("Dosage")]
        public string Dosage { get; set; }
        [XmlElement("UnitPrice")]
        public double UnitPrice { get; set; }
        [XmlElement("Manuactory")]
        public string Manuactory { get; set; }
        [XmlElement("PackNumber")]
        public string PackNumber { get; set; }
        [XmlElement("PackUnit")]
        public string PackUnit { get; set; }
        [XmlElement("MiniUnit")]
        public string MiniUnit { get; set; }
        [XmlElement("Weight")]
        public decimal Weight { get; set; }
        [XmlElement("WeightUnit")]
        public string WeightUnit { get; set; }
        [XmlElement("Volume")]
        public string Volume { get; set; }
        [XmlElement("VolumeUnit")]
        public string VolumeUnit { get; set; }
        [XmlElement("SendFlag")]
        public int SendFlag { get; set; }
        [XmlElement("DrawManager")]
        public string DrawManager { get; set; }
        [XmlElement("DrugType")]
        public string DrugType { get; set; }
        [XmlElement("Allowing")]
        public int Allowing { get; set; }
        [XmlElement("DefaultInstruc")]
        public string DefaultInstruc { get; set; }
    }
}