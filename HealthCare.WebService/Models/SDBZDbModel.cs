//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using System;
using System.Data.Entity.ModelConfiguration;

#pragma warning disable CS1591

namespace HealthCare.WebService.Models
{
    public class SDBZV_DISPENSE_REQ
    {
        /// <summary>
        ///     护理单元. 病人所在的病房
        /// </summary>
        public string WARD { get; set; }
        /// <summary>
        ///     发药科室. 养老(020710) 和 门诊(020602) 
        /// </summary>
        public string DISPENSARY { get; set; }
        //public DateTime POST_DATE_TIME { get; set; }
        //public int? RECEIVE_INDICATOR { get; set; }
        //public DateTime? FINISH_DATE_TIME { get; set; }
        //public int? FETCH_INDICATOR { get; set; }
        //public string REQUEST_DESC { get; set; }
        //public int ITEM_NO { get; set; }
        /// <summary>
        ///     床号. 按照逗号分隔的整数串
        /// </summary>
        public string BED_NO { get; set; }
        //public int REPEAT_INDICATOR { get; set; }
        //public string DISPENSING_PROPERTY { get; set; }
        //public int DISPENSE_DAYS { get; set; }
        //public DateTime? DISPENSING_DATE_TIME { get; set; }
        //public string OPER_BATCH_NO { get; set; }
        //public string POST_OPERATOR { get; set; }
    }

    public class SDBZV_DISPENSE_REQConfig : EntityTypeConfiguration<SDBZV_DISPENSE_REQ>
    {
        public SDBZV_DISPENSE_REQConfig()
        {
            ToTable("V_DISPENSE_REQ", "ZNYG").HasKey(e => e.WARD);
        }
    }

    public class SDBZV_DISPENSARY_ORDER
    {
        public string PATIENT_ID { get; set; }
        public int VISIT_ID { get; set; }
        //public string INP_NO { get; set; }
        /// <summary>
        ///     护理单元. 病人所在的病房
        /// </summary>
        public string WARD_CODE { get; set; }
        /// <summary>
        ///     床号
        /// </summary>
        public int BED_NO { get; set; }
        //public string NAME { get; set; }
        //public string SEX { get; set; }
        //public DateTime? DATE_OF_BIRTH { get; set; }
        /// <summary>
        ///     0 为临时医嘱
        /// </summary>
        public int REPEAT_INDICATOR { get; set; }
        /// <summary>
        ///     医嘱序号
        /// </summary>
        public int ORDER_NO { get; set; }
        /// <summary>
        ///     医嘱子序号
        /// </summary>
        public int ORDER_SUB_NO { get; set; }
        /// <summary>
        ///     物品唯一标志
        /// </summary>
        public string ITEM_CODE { get; set; }
        //public string ITEM_NAME { get; set; }
        /// <summary>
        ///     物品规格厂家
        /// </summary>
        public string ITEM_SPEC { get; set; }
        //public double DOSAGE { get; set; }
        //public string DOSAGE_UNITS { get; set; }
        /// <summary>
        ///     医嘱中药品数量 (HIS 可能为空, 此时找 HIS 修改数据后再同步)
        /// </summary>
        public double? AMOUNT { get; set; }
        /// <summary>
        ///     使用方式
        /// </summary>
        public string ADMINISTRATION { get; set; }
        /// <summary>
        ///     使用频率
        /// </summary>
        public string FREQUENCY { get; set; }
        /// <summary>
        ///     开医嘱的时间
        /// </summary>
        public DateTime START_DATE_TIME { get; set; }
        //public DateTime? STOP_DATE_TIME { get; set; }
        //public int FREQ_COUNTER { get; set; }
        //public int FREQ_INTERVAL { get; set; }
        //public int FREQ_INTERVAL_UNIT { get; set; }
        //public string FREQ_DETAIL { get; set; }
        /// <summary>
        ///     开医嘱的科室.  **医疗组
        /// </summary>
        public string ORDERING_DEPT { get; set; }
        //public string ORDER_STATUS { get; set; }
        /// <summary>
        ///     开医嘱的医生的唯一标志
        /// </summary>
        public string DOCTOR_USER { get; set; }
        //public string DOCTOR { get; set; }
        //public string DEPT_CODE { get; set; }
        //public DateTime? LAST_PERFORM_DATE_TIME { get; set; }
        //public string CHARGE_TYPE { get; set; }
        //public string PERFORM_SCHEDULE { get; set; }
        //public DateTime? PERFORM_DATE_TIME { get; set; }
        //public double SPARE_PREPAYMENTS { get; set; }
        //public DateTime? DISCHARGE_DATE_EXPCTED { get; set; }
        //public string BED_LABEL { get; set; }
        //public string DISPENSING_PROPERTY { get; set; }
        //public int? ADAPT_SYMPTOM_INDICATE { get; set; }
        //public DateTime? V_SYSDATE { get; set; }
    }

    public class SDBZV_DISPENSARY_ORDERConfig : EntityTypeConfiguration<SDBZV_DISPENSARY_ORDER>
    {
        public SDBZV_DISPENSARY_ORDERConfig()
        {
            ToTable("V_DISPENSARY_ORDER", "ZNYG").HasKey(e => e.PATIENT_ID);
        }
    }
}