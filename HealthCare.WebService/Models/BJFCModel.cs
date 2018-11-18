//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Xml.Serialization;

#pragma warning disable CS1591

namespace HealthCare.WebService.Models
{

    public abstract class BJFCObject
    {
        [XmlElement("ResultCode")]
        public int Code { get; set; }
        [XmlElement("ResultContent")]
        public string Message { get; set; }
    }

    /// <summary>
    /// 药品信息
    /// </summary>
    [XmlRoot("Request")]
    public partial class BJFCRequest: BJFCObject
    {
        [XmlElement("DrugInfos")]
        public BJFCDrugInfo[] Requests { get; set; }
        [XmlElement("Tars")]
        public BJFCTars[] BJFCTars { get; set; }
    }


    [XmlRoot("DrugInfos")]
    public partial class BJFCDrugInfo : BJFCObject
    {
        [XmlElement("DrugInfo")]
        public BJFCDrugInfoRow[] Drugs { get; set; }
    }
    [XmlRoot("DrugInfo")]
    public partial class BJFCDrugInfoRow
    {
        /// <summary>
        ///     唯一 ID
        /// </summary>
        [XmlElement("DrugCode")]
        public string Id { get; set; }
        /// <summary>
        ///     药名
        /// </summary>
        [XmlElement("DrugName")]
        public string DrugName { get; set; }
        /// <summary>
        ///     药品大类
        /// </summary>
        [XmlElement("OrdCategoryCode")]
        public string OrdCategoryCode { get; set; }
        /// <summary>
        ///     药品子类
        /// </summary>
        [XmlElement("ARCItemCatCode")]
        public string ARCItemCatCode { get; set; }
        /// <summary>
        ///     规格
        /// </summary>
        [XmlElement("Specification")]
        public string Specification { get; set; }
        /// <summary>
        ///     商品名
        /// </summary>
        [XmlElement("TradeName")]
        public string TradeName { get; set; }
        /// <summary>
        ///     药品剂型
        /// </summary>
        [XmlElement("Dosage")]
        public string Dosage { get; set; }
        /// <summary>
        ///     单价
        /// </summary>
        [XmlElement("UnitPrice")]
        public string UnitPrice { get; set; }
        /// <summary>
        ///     厂家
        /// </summary>
        [XmlElement("Manuactory")]
        public string Manuactory { get; set; }
        ///// <summary>
        /////     包装量
        ///// </summary>
        //[XmlElement("PackNumber")]
        //public double? PackNumber { get; set; }
        /// <summary>
        ///     包装单位
        /// </summary>
        [XmlElement("PackUnit")]
        public string PackUnit { get; set; }
        /// <summary>
        ///     最小包装单位
        /// </summary>
        [XmlElement("MiniUnit")]
        public string MiniUnit { get; set; }
        /// <summary>
        ///     重量
        /// </summary>
        [XmlElement("Weight")]
        public string Weight { get; set; }
        /// <summary>
        ///     重量单位
        /// </summary>
        [XmlElement("WeightUnit")]
        public string WeightUnit { get; set; }
        /// <summary>
        ///    体积（针剂、输液）
        /// </summary>
        [XmlElement("Volume")]
        public string Volume { get; set; }
        /// <summary>
        ///    体积单位
        /// </summary>
        [XmlElement("VolumeUnit")]
        public string VolumeUnit { get; set; }
        /// <summary>
        ///    处理方式(新增/更新)
        /// </summary>
        [XmlElement("SendFlag")]
        public string SendFlag { get; set; }
        /// <summary>
        ///   领用方式
        /// </summary>
        [XmlElement("DrawManager")]
        public string DrawManager { get; set; }
        /// <summary>
        ///   药品类型
        /// </summary>
        [XmlElement("DrugType")]
        public string DrugType { get; set; }
        /// <summary>
        ///   停用标志
        /// </summary>
        [XmlElement("Allowing")]
        public string Allowing { get; set; }
    }

    /// <summary>
    ///     员工信息
    /// </summary>
    [XmlRoot("Response")]
    public partial class BJFCResponse: BJFCObject
    {
        [XmlElement("DictInfos")]
        public BJFCEmployee[] Response { get; set; }
    }
    [XmlRoot("DictInfos")]
    public partial class BJFCEmployee: BJFCObject
    {
        [XmlElement("UserDict")]
        public BJFCEmployeeROW[] Employees { get; set; }

        [XmlElement("DeptDict")]
        public BJFCDeptROW[] Depts { get; set; }
    }

    public partial class BJFCEmployeeROW
    {
        /// <summary>
        ///     医生代码
        /// </summary>
        [XmlElement("UserCode")]
        public string UserCode { get; set; }
        /// <summary>
        ///     医生描述
        /// </summary>
        [XmlElement("UserDesc")]
        public string UserDesc { get; set; }
        /// <summary>
        ///     助记符
        /// </summary>
        [XmlElement("Alias")]
        public string Alias { get; set; }
        /// <summary>
        ///     医生职称
        /// </summary>
        [XmlElement("TechnicalTitle")]
        public string TechnicalTitle { get; set; }
        /// <summary>
        ///     人员职称代码
        /// </summary>
        [XmlElement("TechnicalTitleCode")]
        public string TechnicalTitleCode { get; set; }
        /// <summary>
        ///     人员所属科室
        /// </summary>
        [XmlElement("CtLocCode")]
        public string CtLocCode { get; set; }
        /// <summary>
        ///     建立日期
        /// </summary>
        [XmlElement("CreateDate")]
        public string CreateDate { get; set; }
        /// <summary>
        ///     人员职务
        /// </summary>
        [XmlElement("UserJob")]
        public string UserJob { get; set; }
        /// <summary>
        ///     开始日期
        /// </summary>
        [XmlElement("StartDate")]
        public string StartDate { get; set; }
        /// <summary>
        ///     停用日期
        /// </summary>
        [XmlElement("StopDate")]
        public string StopDate { get; set; }
        /// <summary>
        ///     是否有毒麻药品权
        /// </summary>
        [XmlElement("IfPoison")]
        public string IfPoison { get; set; }
        /// <summary>
        ///     是否有开处方权
        /// </summary>
        [XmlElement("IfPrescribe")]
        public string IfPrescribe { get; set; }
        /// <summary>
        ///     执行资格证号
        /// </summary>
        [XmlElement("Qualification")]
        public string Qualification { get; set; }
    }


    /// <summary>
    ///     科室信息
    /// </summary>
    public partial class BJFCDeptROW
    {
        /// <summary>
        ///     科室代码
        /// </summary>
        [XmlElement("DeptCode")]
        public string DeptCode { get; set; }
        /// <summary>
        ///     科室描述
        /// </summary>
        [XmlElement("DeptDesc")]
        public string DeptDesc { get; set; }
        /// <summary>
        ///     助记符
        /// </summary>
        [XmlElement("Alias")]
        public string Alias { get; set; }
        /// <summary>
        ///     物理地址
        /// </summary>
        [XmlElement("Address")]
        public string Address { get; set; }

        /// <summary>
        ///     建立日期
        /// </summary>
        [XmlElement("CreateDate")]
        public string CreateDate { get; set; }

        /// <summary>
        ///     停用标志
        /// </summary>
        [XmlElement("Allowind")]
        public string Allowind { get; set; }
    }


    /// <summary>
    ///     耗材信息
    /// </summary>
    public partial class BJFCTars
    {
        [XmlElement("TarItemInfo")]
        public BJFCTarItemInfoRow[] TarItemInfos { get; set; }
    }

    public partial class BJFCTarItemInfoRow
    {
        /// <summary>
        ///     唯一 ID
        /// </summary>
        [XmlElement("ArcimRowId")]
        public string Id { get; set; }
        /// <summary>
        ///     医嘱项代码
        /// </summary>
        [XmlElement("ArcimCode")]
        public string ArcimCode { get; set; }
        /// <summary>
        ///     医嘱项名称
        /// </summary>
        [XmlElement("ArcimName")]
        public string ArcimName { get; set; }
        /// <summary>
        ///     收费项目ID
        /// </summary>
        [XmlElement("TariRowId")]
        public string TariRowId { get; set; }
        /// <summary>
        ///     收费项目代码
        /// </summary>
        [XmlElement("TariCode")]
        public string TariCode { get; set; }
        /// <summary>
        ///     收费项目描述
        /// </summary>
        [XmlElement("TariDesc")]
        public string TariDesc { get; set; }
        /// <summary>
        ///     开始时间
        /// </summary>
        [XmlElement("TariStartDate")]
        public string TariStartDate { get; set; }
        /// <summary>
        ///     结束时间
        /// </summary>
        [XmlElement("TariEndDate")]
        public string TariEndDate { get; set; }
        /// <summary>
        ///     单位代码
        /// </summary>
        [XmlElement("TariUOMCode")]
        public string TariUOMCode { get; set; }
        /// <summary>
        ///     单位描述
        /// </summary>
        [XmlElement("TariUOMDesc")]
        public string TariUOMDesc { get; set; }
        /// <summary>
        ///     规格
        /// </summary>
        [XmlElement("InfoSpec")]
        public string InfoSpec { get; set; }
        /// <summary>
        ///     价格
        /// </summary>
        [XmlElement("TPPrice")]
        public string TPPrice { get; set; }
        /// <summary>
        ///     产地
        /// </summary>
        [XmlElement("Manufacturer")]
        public string Manufacturer { get; set; }
        /// <summary>
        ///     医保类型
        /// </summary>
        [XmlElement("InsuranceType")]
        public string InsuranceType { get; set; }
        /// <summary>
        ///    医保类型备注
        /// </summary>
        [XmlElement("InsuranceNote")]
        public string InsuranceNote { get; set; }
        /// <summary>
        ///    收费项目子类编码
        /// </summary>
        [XmlElement("TarscCode")]
        public string TarscCode { get; set; }
        /// <summary>
        ///    收费项目子类描述
        /// </summary>
        [XmlElement("TarscName")]
        public string TarscName { get; set; }
        /// <summary>
        ///   领用方式
        /// </summary>
        [XmlElement("DrawManager")]
        public string DrawManager { get; set; }
        /// <summary>
        ///   收费项目大类编码
        /// </summary>
        [XmlElement("TarcCode")]
        public string TarcCode { get; set; }
        /// <summary>
        ///   收费项目大类名称
        /// </summary>
        [XmlElement("TarcName")]
        public string TarcName { get; set; }
    }


    /// <summary>
    ///     患者信息
    /// </summary>
    public partial class BJFCPatient : BJFCObject
    {
        public List<BJFCPatientROW> Patients { get; set; }
    }

    [XmlRoot("PatInfo")]
    public partial class BJFCPatientROW
    {
        /// <summary>
        ///     患者ID
        /// </summary>
        [XmlElement("RegisterNo")]
        public string RegisterNo { get; set; }
        /// <summary>
        ///     医保号
        /// </summary>
        [XmlElement("InsuranceNo")]
        public string InsuranceNo { get; set; }
        /// <summary>
        ///     证件类型
        /// </summary>
        [XmlElement("CredentialType")]
        public string CredentialType { get; set; }
        /// <summary>
        ///     证件号
        /// </summary>
        [XmlElement("CredentialNo")]
        public string CredentialNo { get; set; }
        /// <summary>
        ///     病人姓名
        /// </summary>
        [XmlElement("PatientName")]
        public string PatientName { get; set; }
        /// <summary>
        ///     性别描述
        /// </summary>
        [XmlElement("SexDesc")]
        public string SexDesc { get; set; }
        /// <summary>
        ///     出生日期
        /// </summary>
        [XmlElement("BirthDay")]
        public string BirthDay { get; set; }
        /// <summary>
        ///     联系电话
        /// </summary>
        [XmlElement("Telephone")]
        public string Telephone { get; set; }
        /// <summary>
        ///     住院日期
        /// </summary>
        [XmlElement("AdmDate")]
        public string AdmDate { get; set; }
        /// <summary>
        ///     科室名称
        /// </summary>
        [XmlElement("AdmDept")]
        public string AdmDept { get; set; }
        /// <summary>
        ///     住院号
        /// </summary>
        [XmlElement("AdmNo")]
        public string AdmNo { get; set; }  
    }


    public partial class AddOrderItemRt
    {
        /// <summary>
        /// 就诊号
        /// </summary>
        public string AdmNo { get; set; }
        /// <summary>
        /// 医嘱号
        /// </summary>
        public string ExtRowID { get; set; }
        /// <summary>
        /// 医嘱类型 传空
        /// </summary>
        public string OrderTypeCode { get; set; }
        /// <summary>
        /// 医嘱字典代码
        /// </summary>
        public string ArcimCode { get; set; }
        /// <summary>
        /// 医嘱字典名称
        /// </summary>
        public string ArcimDesc { get; set; }
        /// <summary>
        /// 医嘱状态
        /// </summary>
        public string OrderStatus { get; set; }
        /// <summary>
        /// 数量
        /// </summary>
        public double OrderQty { get; set; }
        /// <summary>
        /// 开医嘱科室代码
        /// </summary>
        public string OrderDeptCode { get; set; }
        /// <summary>
        /// 接受科室代码
        /// </summary>
        public string OrderRecDepCode { get; set; }
        /// <summary>
        /// 开医嘱人
        /// </summary>
        public string OrderDoctorCode { get; set; }
        /// <summary>
        /// 录入人代码
        /// </summary>
        public string OrderUserCode { get; set; }
        /// <summary>
        /// 要求执行医嘱日期
        /// </summary>
        public string OrderSttDat { get; set; }
        /// <summary>
        /// 要求执行医嘱时间
        /// </summary>
        public string OrderSttTim { get; set; }
        /// <summary>
        /// 医保类型代码 传空
        /// </summary>
        public string InsuTypeCode { get; set; }
        /// <summary>
        /// 关联HIS医嘱ROWID   传空
        /// </summary>
        public string OrdRowID { get; set; }
        /// <summary>
        /// 频次代码  传空
        /// </summary>
        public string PHFreqCode { get; set; }
        /// <summary>
        /// 单位代码
        /// </summary>
        public string UomCode { get; set; }
        /// <summary>
        /// 剂型代码
        /// </summary>
        public string DosageCode { get; set; }
        /// <summary>
        /// 药品用法代码
        /// </summary>
        public string UsageCode { get; set; }
        /// <summary>
        /// 来源
        /// </summary>
        public string Source { get; set; }
        /// <summary>
        /// 是否按医保处理   传空
        /// </summary>
        public string InsuFlag { get; set; }
    }


    [XmlRoot("Response")]
    public partial class BJFCOrder : BJFCObject
    {
        /// <summary>
        /// 医嘱Rowid 
        /// </summary>
        [XmlElement("OrdID")]
        public string OrdID { get; set; }
    }
}