//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using System;
using System.Data.Entity.ModelConfiguration;

#pragma warning disable CS1591, CS1587

namespace HealthCare.WebService.Models
{
    public class SDSLView_HaoCai_Department
    {
        public string ID { get; set; }
        public string DisplayName { get; set; }
    }


    public class SDSLView_HaoCai_DepartmentConfig : EntityTypeConfiguration<SDSLView_HaoCai_Department>
    {
        public SDSLView_HaoCai_DepartmentConfig()
        {
            ToTable("View_HaoCai_Department").HasKey(e => e.ID);
        }
    }

    public class SDSLView_HaoCai_Employee
    {
        public string UniqueId { get; set; }
        public string DisplayName { get; set; }
        public string JobNo { get; set; }
        public string JobTitle { get; set; }
        public string DepartmentId { get; set; }
        // public string DepartmentName { get; set; }
    }


    public class SDSLView_HaoCai_EmployeeConfig : EntityTypeConfiguration<SDSLView_HaoCai_Employee>
    {
        public SDSLView_HaoCai_EmployeeConfig()
        {
            ToTable("View_HaoCai_Employee").HasKey(e => e.UniqueId);
        }
    }

    public class SDSLView_HaoCai_Goods
    {
        /// <summary>
        ///     UniqueId	唯一标识
        /// </summary>
        public long id { get; set; }
        /// <summary>
        ///     Code = ARCIM_CODE
        /// </summary>
        public string ARCIM_CODE { get; set; }
        /// <summary>
        ///     DisplayName	药品名称
        /// </summary>
        public string ARCIM_NAME { get; set; }
        /// <summary>
        /// 
        /// </summary>
        //public string INPUT_CODE { get; set; }
        /// <summary>
        /// 
        /// </summary>
        //public string TARI_DESC { get; set; }
        /// <summary>
        ///     Price
        /// </summary>
        public string ORD_PRICE { get; set; }
        /// <summary>
        ///     GoodsType	药品类型（如麻、精一、精二）
        /// </summary>
        public string ORD_SUBCAT { get; set; }
        /// <summary>
        /// 
        /// </summary>
        //public string PACKAGE_UOM { get; set; }
        /// <summary>
        /// 
        /// </summary>
        //public string UNIT_PRICE { get; set; }
    }

    public class SDSLView_HaoCai_GoodConfig : EntityTypeConfiguration<SDSLView_HaoCai_Goods>
    {
        public SDSLView_HaoCai_GoodConfig()
        {
            ToTable("View_HaoCai_Goods").HasKey(e => e.id);
        }
    }

    public class SDSLView_HaoCai_OperationSchedule
    {
        /// <summary>
        ///     UniqueId	唯一标识
        /// </summary>
        public string OP_record_ID { get; set; }
        /// <summary>
        ///     Number	患者住院号
        /// </summary>
        //public string INPATIID { get; set; }
        /// <summary>
        ///     ApplyTime	手术申请时间
        /// </summary>
        //public string ApplyDate { get; set; }
        /// <summary>
        ///     OperationType	手术类别，择期、急诊
        /// </summary>
        public string Op_Type { get; set; }
        /// <summary>
        ///     
        /// </summary>
        //public string OperatingTable { get; set; }
        /// <summary>
        ///     displayOrder = OperatingIndex
        /// </summary>
        public string OperatingIndex { get; set; }
        ///// <summary>
        ///// 
        ///// </summary>
        //public DateTime? 手术开始时间 { get; set; }
        ///// <summary>
        ///// 
        ///// </summary>
        // public DateTime? 手术结束时间 { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string 主刀 { get; set; }
        /// <summary>
        ///     IsCancelled	手术是否被取消
        /// </summary>
        public string Status { get; set; }
        /// <summary>
        ///     RoomId	手术房间
        /// </summary>
        public long roomid { get; set; }
    }

    public class SDSLView_HaoCai_OperationScheduleConfig : EntityTypeConfiguration<SDSLView_HaoCai_OperationSchedule>
    {
        public SDSLView_HaoCai_OperationScheduleConfig()
        {
            ToTable("View_HaoCai_OperationSchedule").HasKey(e => e.OP_record_ID);
        }
    }

    public class SDSLView_HaoCai_Patient
    {
        /// <summary>
        ///     UniqueId	患者编号
        /// </summary>
        public string UniqeId { get; set; }
        /// <summary>
        ///     DisplayName	患者名称
        /// </summary>
        public string DisplayName { get; set; }
        /// <summary>
        ///     Birthday	出生日期
        /// </summary>
        //public string birthday { get; set; }
        /// <summary>
        ///     Gender	性别
        /// </summary>
        public string Gender { get; set; }
        /// <summary>
        /// 
        /// </summary>
        //public string ordid { get; set; }
        /// <summary>
        /// 
        /// </summary>
        //public string ApplyID { get; set; }
        /// <summary>
        ///     Diagnostic	诊断
        /// </summary>
        public string Diagnostic { get; set; }
        /// <summary>
        ///     AdmittedDepartmentId	患者入院科室的唯一标识
        /// </summary>
        public string AdmittedDepartmentId { get; set; }
        /// <summary>
        ///     Number	住院号
        /// </summary>
        public string Number { get; set; }
        /// <summary>
        ///     InitiationTime	入院时间
        /// </summary>
        public string IntiationTime { get; set; }
        /// <summary>
        ///     BedNo	床号
        /// </summary>
        public string BedNo { get; set; }
        /// <summary>
        ///     ResidedAreaId	患者所属病区的唯一标识
        /// </summary>
        public string ResidedAreaId { get; set; }
        /// <summary>
        ///
        /// </summary>
        //public string RoomName { get; set; }
        /// <summary>
        ///     RoomId	所属手术间唯一标识
        /// </summary>
        public long RoomId { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string 手术日期 { get; set; }
    }

    public class SDSLView_HaoCai_PatientConfig : EntityTypeConfiguration<SDSLView_HaoCai_Patient>
    {
        public SDSLView_HaoCai_PatientConfig()
        {
            ToTable("View_HaoCai_Patient").HasKey(e => e.UniqeId);
        }
    }

    public class SDSLView_HaoCai_Room
    {
        public long id { get; set; }
        public string name { get; set; }
    }

    public class SDSLView_HaoCai_RoomConfig : EntityTypeConfiguration<SDSLView_HaoCai_Room>
    {
        public SDSLView_HaoCai_RoomConfig()
        {
            ToTable("View_HaoCai_Room").HasKey(e => e.id);
        }
    }
}