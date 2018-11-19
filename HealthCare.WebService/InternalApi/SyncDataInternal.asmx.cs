//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using HealthCare.MongoData;
using HealthCare.WebService.Converters;
using HealthCare.WebService.Helper;
using HealthCare.WebService.Models;
using HPSocketCS;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.SqlServer;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.Script.Services;
using System.Web.Services;

#pragma warning disable CS1591

namespace HealthCare.WebService.InternalApi
{
    /// <summary>
    /// Summary description for GuangJiWebServiceInternal
    /// </summary>
    [WebService(Namespace = "http://www.sframed.com")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]    
    public partial class SyncDataInternal
    {
        [WebMethod(Description = "和 HIS 进行预支记录的上传   ApiBack"), ScriptMethod(ResponseFormat = ResponseFormat.Json, UseHttpGet = true)]
        public void SyncPrescriptions(string hospital)
        {
            var array = (HttpContext.Current.Request.Form["array"] ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
            var isCheckIn = Convert.ToBoolean(HttpContext.Current.Request.Form["isCheckIn"]);
            var result = new ApiBack<dynamic> { Code = -(short.MinValue + 1), Msg = $"Location Error '{hospital}'", Hospital = hospital, };
            switch (hospital)
            {
                case Hospital.SZGJ: SyncSZGJ(array); break;
                case Hospital.BJFC: SyncBJFC(array); break;
                case Hospital.SDEY: SyncSDEY(array, result); break;
                case Hospital.SDJN: SyncSDJN(array, result); break;
                case Hospital.SDBZ: SyncSDBZ(array, result); break;
                case Hospital.SDSL: SyncSDSL(array, result); break;
                case Hospital.BJTT: SyncBJTT(array, result); break;
                case Hospital.SZZY: SyncSZZY(array, result); break;
            }
            ResponseJSON(result);

            void SyncSZGJ(string[] medications)
            {
                try
                {
                    var finds = medications.Any() ? mongo.MedicationCollection.AsQueryable().Where(m => medications.Contains(m.UniqueId)).ToList() : new List<Medication>();
                    result.Msg = "请打印后到 HIS 系统录入医嘱";
                }
                catch (Exception ex)
                {
                    result.Msg = (ex.InnerException ?? ex).Message;
                    Global.GlobalLogger.Error(ex);
                }
            }

            void SyncBJFC(string[] medications)
            {
                try
                {
                    var bjfc = new BJFC.AddOrdInfoService();
                    var finds = medications.Any() ? mongo.MedicationCollection.AsQueryable().Where(m => medications.Contains(m.UniqueId) && !m.IsSynchronized).ToList() : new List<Medication>();
                    var orderCount = finds.Count();
                    int sucessCount = orderCount;
                    foreach (var item in finds)
                    {
                        string upxml = new BJFCConverter().BJFCUpHis(item);
                        string xml = bjfc.AddOrdInfo(upxml);

                        BaseConverter.WriteSyncLog(nameof(Medication), DateTime.Now.Ticks.ToString(), upxml + "/r/n" + xml);
                        var ser = new Serializer();
                        var data = ser.Deserialize<BJFCOrder>(xml);
                        if (data.Code != 0)
                        {
                            sucessCount--;
                            Global.GlobalLogger.Info($"'{item.UniqueId}' '{item.PatientId}'  '{data.OrdID}' upload Order fail.");
                        }
                        mongo.MedicationCollection.UpdateOne(d => d.UniqueId == item.UniqueId, Builders<Medication>.Update.Set(d => d.IsSynchronized, data.Code == 0).Set(d => d.PrescriptionId, data.OrdID));
                    }
                    result.Msg = "上传医嘱" + orderCount + "条，成功" + sucessCount + "条";
                }
                catch (Exception ex)
                {
                    result.Msg = (ex.InnerException ?? ex).Message;
                    Global.GlobalLogger.Error(ex);
                }
            }

            void SyncSDEY(string[] medications, ApiBack<dynamic> status)
            {
                var meds = mongo.MedicationCollection.AsQueryable().Where(m => medications.Contains(m.UniqueId)).ToList();
                var states = new List<(string medication, int code, string msg)>();
                using (var ctx = new OracleDbContext())
                {
                    ctx.Database.Connection.Open();
                    var cmd = (OracleCommand)ctx.Database.Connection.CreateCommand();
                    cmd.CommandText = "hisrun.p_gzhc_execute_zy";
                    cmd.CommandType = CommandType.StoredProcedure;

                    foreach (var item in meds)
                    {
                        for (var i = 0; i < (int)item.QtyActual; i++)
                        {
                            var fyxh = $"{item.Goods?.PriceSerialNumber}_{item.GoodsBarcodes.ElementAtOrDefault(i)}";
                            cmd.Parameters.Add("as_patientid", OracleDbType.Varchar2).Value = item.Patient?.Hospitalization.HospitalNumber;   // 住院号码
                            cmd.Parameters.Add("as_fyxh", OracleDbType.Varchar2).Value = fyxh;                   // 费用序号_条码号
                            cmd.Parameters.Add("as_userid", OracleDbType.Varchar2).Value = item.DoctorId;        // 操作人员工号
                            cmd.Parameters.Add("as_zxks", OracleDbType.Varchar2).Value = "126";                  // 执行科室 设备部 (126)
                            try
                            {
                                cmd.Parameters.Add("as_returncode", OracleDbType.Varchar2, 1024).Direction = ParameterDirection.Output;
                                cmd.Parameters.Add("as_returntext", OracleDbType.Varchar2, 1024).Direction = ParameterDirection.Output;
                                cmd.ExecuteNonQuery();
                                var code = int.Parse(cmd.Parameters["as_returncode"].Value.ToString());
                                var text = cmd.Parameters["as_returntext"].Value.ToString();

                                states.Add((item.UniqueId, code, text));
                                Global.MonitorLogger.Info($"{item.UniqueId},{string.Join(",", cmd.Parameters.Cast<OracleParameter>().Select(x => $"{x.ParameterName}={x.Value}"))}");
                            }
                            catch (Exception ex)
                            {
                                states.Add((item.UniqueId, -1001, ex.Message));
                                Global.MonitorLogger.Error($"{item.UniqueId},{string.Join(",", cmd.Parameters.Cast<OracleParameter>().Select(x => $"{x.ParameterName}={x.Value}"))}", ex);
                            }
                            finally
                            {
                                cmd.Parameters.Clear();
                            }
                        }
                        if (states.Where(s => s.medication == item.UniqueId).All(s => s.code == 0))
                        {
                            mongo.MedicationCollection.UpdateOne(m => m.UniqueId == item.UniqueId, Builders<Medication>.Update.Set(m => m.IsSynchronized, true));
                        }
                    }
                }
                status.Code = states.FirstOrDefault(s => s.code != 0).code;
                status.Msg = states.FirstOrDefault(s => s.code != 0).msg ?? $"计费成功 '{meds.Sum(m => m.QtyActual)}' 条";
                status.Data = states;
            }

            void SyncSDJN(string[] medications, ApiBack<dynamic> status)
            {
                using (var ctx = new OracleDbContext())
                {
                }
                status.Code = -1;
                status.Msg = "NotImplementedException";
            }

            void SyncSDBZ(string[] prescriptions, ApiBack<dynamic> status)
            {
                using (var ctx = new OracleDbContext())
                {
                    ctx.Database.Connection.Open();
                    var cmd = (OracleCommand)ctx.Database.Connection.CreateCommand();
                    cmd.CommandType = CommandType.StoredProcedure;

                    var executes = new List<string>();
                    var ps = mongo.PrescriptionCollection.AsQueryable().Where(m => prescriptions.Contains(m.UniqueId)).ToList();
                    foreach (var item in ps)
                    {
                        if (item.FeeTime != null)
                        {
                            // 避免重复计费
                            executes.Add(item.UniqueId);
                            continue;
                        }

                        cmd.Parameters.Clear();

                        try
                        {
                            switch (item.RecordType)
                            {
                                case "住院医嘱":
                                    {
                                        cmd.CommandText = "znyg.PRO_DIS_ODR";
                                        // TrackNumber = $"{p.VISIT_ID}@{p.ORDER_NO}@{p.ORDER_SUB_NO}",
                                        var args = item.TrackNumber.Split('@');
                                        cmd.Parameters.Add("V_PID", OracleDbType.Varchar2).Value = item.PatientId;
                                        cmd.Parameters.Add("v_vid", OracleDbType.Int32).Value = int.Parse(args[0]);
                                        cmd.Parameters.Add("V_DISPENSARY", OracleDbType.Varchar2).Value = item.DepartmentDestinationId;
                                        cmd.Parameters.Add("v_drug_code", OracleDbType.Varchar2).Value = item.GoodsId;
                                        cmd.Parameters.Add("v_order_no", OracleDbType.Int32).Value = int.Parse(args[1]);
                                        cmd.Parameters.Add("V_UNITS", OracleDbType.Varchar2).Value = item.Goods?.UsedUnit;
                                        cmd.Parameters.Add("v_order_sub_no", OracleDbType.Int32).Value = int.Parse(args[2]);
                                        cmd.Parameters.Add("V_ORDER_DEPT", OracleDbType.Varchar2).Value = item.DepartmentSourceId;
                                        cmd.Parameters.Add("V_DOCTOR_USER", OracleDbType.Varchar2).Value = item.DoctorId;
                                        cmd.Parameters.Add("v_AMOUNT", OracleDbType.Int32).Value = (int)item.Qty;
                                        cmd.Parameters.Add("v_OPERATOR", OracleDbType.Varchar2).Value = item.DepartmentDestinationId.Substring(2, 4);   // 操作人, HIS 该字段不能超过 4
                                        /*
                                            医嘱存储过程 znyg.PRO_DIS_ODR 所需参数
                                            V_PID IN VARCHAR2,          -- 病人ID
                                            v_vid IN number,	        -- 住院次数
                                            V_DISPENSARY IN VARCHAR2,	-- 发药药房
                                            v_drug_code IN varchar2,	-- 药品代码
                                            v_order_no IN NUMBER,       -- 医嘱号
                                            V_UNITS IN VARCHAR2,        -- 药品使用单位
                                            v_order_sub_no IN NUMBER,   -- 医嘱子序号
                                            V_ORDER_DEPT IN VARCHAR2,   -- 开单科室
                                            V_DOCTOR_USER IN VARCHAR2,  -- 开单医生
                                            v_AMOUNT IN number,		    -- 数量
                                            v_OPERATOR IN VARCHAR2,	    -- 发药操作员
                                            V_FLAG OUT NUMBER
                                        */
                                    }
                                    break;
                                case "门诊处方":
                                    {
                                        cmd.CommandText = "znyg.PRO_OUTPDIS_PRESC";
                                        // TrackNumber = $"{p.VISIT_NO}@{p.SERIAL_NO}@{p.ORDER_CLASS}@{p.ORDER_NO}@{p.ORDER_SUB_NO}@{p.PRESC_NO}@{p.ITEM_NO}@{p.COSTS}@{p.AMOUNT ?? 0.0}@{p.UNITS}",
                                        var args = item.TrackNumber.Split('@');
                                        cmd.Parameters.Add("V_PID", OracleDbType.Varchar2).Value = item.PatientId;
                                        cmd.Parameters.Add("V_VISIT_DATE", OracleDbType.Date).Value = item.IssuedTime;
                                        cmd.Parameters.Add("V_VISIT_NO", OracleDbType.Int32).Value = int.Parse(args[0]);
                                        cmd.Parameters.Add("V_SERIAL_NO", OracleDbType.Int32).Value = int.Parse(args[1]);
                                        cmd.Parameters.Add("V_ORDER_CLASS", OracleDbType.Varchar2).Value = args[2];
                                        cmd.Parameters.Add("V_ORDER_NO", OracleDbType.Int32).Value = int.Parse(args[3]);
                                        cmd.Parameters.Add("V_ORDER_SUB_NO", OracleDbType.Int32).Value = int.Parse(args[4]);
                                        cmd.Parameters.Add("v_presc_no", OracleDbType.Int32).Value = int.Parse(args[5]);
                                        cmd.Parameters.Add("V_ITEM_NO", OracleDbType.Int32).Value = int.Parse(args[6]);
                                        cmd.Parameters.Add("V_DISPENSARY", OracleDbType.Varchar2).Value = item.DepartmentDestinationId;
                                        cmd.Parameters.Add("v_drug_code", OracleDbType.Varchar2).Value = item.GoodsId;
                                        cmd.Parameters.Add("V_UNITS", OracleDbType.Varchar2).Value = args[9];
                                        cmd.Parameters.Add("v_AMOUNT", OracleDbType.Int32).Value = (int)(double.Parse(args[8]));
                                        cmd.Parameters.Add("V_COSTS", OracleDbType.Double).Value = double.Parse(args[7]);
                                        cmd.Parameters.Add("v_OPERATOR", OracleDbType.Varchar2).Value = item.DepartmentDestinationId.Substring(2, 4);   // 操作人, HIS 该字段不能超过 4
                                        /*
                                            门诊处方存储过程 znyg.PRO_OUTPDIS_PRESC 参数
                                            V_PID IN VARCHAR2,			-- 病人ID
                                            V_VISIT_DATE IN DATE,		-- 就诊日期
                                            V_VISIT_NO IN NUMBER,		-- 就诊序号
                                            V_SERIAL_NO IN NUMBER,		-- 就诊流水号
                                            V_ORDER_CLASS IN VARCHAR2,	-- 诊疗项目类型
                                            V_ORDER_NO IN NUMBER,		-- 医嘱号
                                            V_ORDER_SUB_NO IN NUMBER,	-- 医嘱子序号
                                            v_presc_no IN NUMBER,		-- 处方号
                                            V_ITEM_NO IN NUMBER,		-- 顺序号
                                            V_DISPENSARY IN VARCHAR2,	-- 发药药房
                                            v_drug_code IN varchar2,	-- 药品代码
                                            V_UNITS IN varchar2,		-- 单位
                                            v_AMOUNT IN number,			-- 数量
                                            V_COSTS IN number,			-- 金额
                                            v_OPERATOR IN VARCHAR2,		-- 发药操作人
                                            V_FLAG OUT NUMBER
                                        */
                                    }
                                    break;
                                case "住院处方":
                                    {
                                        cmd.CommandText = "znyg.PRO_DIS_PRESC";
                                        // TrackNumber = $"{p.VISIT_ID}@{p.PRESC_NO}@{p.ITEM_NO}@{p.QUANTITY}@{p.PACKAGE_UNITS}@{p.PAYMENTS}",
                                        var args = item.TrackNumber.Split('@');
                                        cmd.Parameters.Add("V_PID", OracleDbType.Varchar2).Value = item.PatientId;
                                        cmd.Parameters.Add("v_vid", OracleDbType.Int32).Value = int.Parse(args[0]);
                                        cmd.Parameters.Add("v_presc_date", OracleDbType.Date).Value = item.IssuedTime;
                                        cmd.Parameters.Add("v_presc_no", OracleDbType.Int32).Value = int.Parse(args[1]);
                                        cmd.Parameters.Add("v_item_no", OracleDbType.Int32).Value = int.Parse(args[2]);
                                        cmd.Parameters.Add("V_DISPENSARY", OracleDbType.Varchar2).Value = item.DepartmentDestinationId;
                                        cmd.Parameters.Add("v_drug_code", OracleDbType.Varchar2).Value = item.GoodsId;
                                        cmd.Parameters.Add("V_UNITS", OracleDbType.Varchar2).Value = args[4];
                                        cmd.Parameters.Add("V_DOCTOR_USER", OracleDbType.Varchar2).Value = item.DoctorId;
                                        cmd.Parameters.Add("v_AMOUNT", OracleDbType.Int32).Value = int.Parse(args[3]);
                                        cmd.Parameters.Add("V_COSTS", OracleDbType.Decimal).Value = double.Parse(args[5]);
                                        cmd.Parameters.Add("v_OPERATOR", OracleDbType.Varchar2).Value = item.DepartmentDestinationId.Substring(2, 4);   // 操作人, HIS 该字段不能超过 4
                                        /*
                                            住院处方存储过程 znyg.PRO_DIS_PRESC 所需参数
                                            V_PID IN VARCHAR2,        -- 病人ID
                                            v_vid IN number,	      -- 住院次数
                                            v_presc_date date,        -- 处方日期
                                            v_presc_no NUMBER,        -- 处方号
                                            v_item_no NUMBER,	      -- 处方项目序号
                                            V_DISPENSARY VARCHAR2,	  -- 发药药房
                                            v_drug_code IN varchar2,  -- 药品代码           
                                            V_UNITS IN varchar2,
                                            V_DOCTOR_USER VARCHAR2,	  -- 开单医生
                                            v_AMOUNT IN number,		  -- 数量
                                            V_COSTS IN number,
                                            v_OPERATOR IN VARCHAR2,	  -- 发药操作员
                                            V_FLAG OUT NUMBER
                                        */
                                    }
                                    break;
                            }

                            cmd.Parameters.Add("V_FLAG", OracleDbType.Int32).Direction = ParameterDirection.Output;
                            cmd.ExecuteNonQuery();
                            if (int.Parse(cmd.Parameters["V_FLAG"].Value.ToString()) == 1)
                            {
                                // 执行 HIS 的存储过程, 确定是否可以执行医嘱
                                // 计费成功的返回 1, 其余状态码计费失败
                                mongo.PrescriptionCollection.UpdateOne(p => p.UniqueId == item.UniqueId, Builders<Prescription>.Update.Set(o => o.FeeTime, DateTime.Now));
                                executes.Add(item.UniqueId);
                            }
                            Global.MonitorLogger.Info($"{item.UniqueId}, {item.RecordType}    {string.Join(",", cmd.Parameters.Cast<OracleParameter>().Select(x => $"{x.ParameterName}={x.Value}"))}");
                        }
                        catch (Exception ex)
                        {
                            Global.MonitorLogger.Error($"{item.UniqueId}, {item.RecordType}    {string.Join(",", cmd.Parameters.Cast<OracleParameter>().Select(x => $"{x.ParameterName}={x.Value}"))}", ex);
                        }
                    }

                    status.Code = executes.Any() ? 0 : -1;
                    status.Data = executes;
                    status.Msg = $"预计费数 '{ps.Count}', 计费成功数 '{executes.Count}'";
                }
            }

            void SyncBJTT(string[] prescriptions, ApiBack<dynamic> status)
            {
                using (var ctx = new OracleDbContext())
                {
                    ctx.Database.Connection.Open();
                    var cmd = (OracleCommand)ctx.Database.Connection.CreateCommand();
                    cmd.CommandType = CommandType.StoredProcedure;

                    var executes = new List<string>();
                    var pres = mongo.PrescriptionCollection.AsQueryable().Where(m => prescriptions.Contains(m.UniqueId)).ToList();
                    foreach (var item in pres)
                    {
                        if (item.IsSynchronized && !isCheckIn)
                        {
                            // 计费成功的，不再重复计费
                            executes.Add(item.UniqueId);
                            continue;
                        }
                        try
                        {
                            switch (item.Goods.Filter)
                            {
                                case "Drug":

                                    cmd.Parameters.Clear();
                                    cmd.CommandText = "tthis.prc_sfra_medicine_charge";
                                    cmd.Parameters.Add("Par_PATIENTID", OracleDbType.Varchar2).Value = item.PatientId;
                                    cmd.Parameters.Add("Par_INPATIENTNO", OracleDbType.Varchar2).Value = item.Patient.Hospitalization.UniqueId; // 计费时流水号
                                    cmd.Parameters.Add("Par_AMOUNT", OracleDbType.Double).Value = isCheckIn ? item.Qty * -1 : item.Qty;
                                    cmd.Parameters.Add("Par_OPERATOR", OracleDbType.Varchar2).Value = item.DoctorId;
                                    cmd.Parameters.Add("PAR_EXECSQN", OracleDbType.Varchar2).Value = item.OperationScheduleId;
                                    cmd.Parameters.Add("Par_EXECUNITID", OracleDbType.Double).Value = 133;  // 麻醉科 133
                                    cmd.Parameters.Add("Par_INSTOREID", OracleDbType.Varchar2).Value = item.GoodsId;
                                    cmd.Parameters.Add("Par_batchno", OracleDbType.Varchar2).Value = item.BatchNumber;
                                    cmd.Parameters.Add("Par_errmsg", OracleDbType.Varchar2, 5000).Direction = ParameterDirection.Output;
                                    cmd.ExecuteNonQuery();
                                    var log = $"{item.UniqueId}, {item.RecordType}    {string.Join(",", cmd.Parameters.Cast<OracleParameter>().Select(x => $"{x.ParameterName}={x.Value}"))}";
                                    switch (cmd.Parameters["Par_errmsg"].Value?.ToString())
                                    {
                                        case "":
                                        case null:
                                        case "null":
                                            executes.Add(item.UniqueId);
                                            Global.MonitorLogger.Info(log);
                                            if (isCheckIn)
                                            {
                                                mongo.PrescriptionCollection.UpdateOne(m => m.UniqueId == item.UniqueId, Builders<Prescription>.Update.Set(m => m.IsDisabled, true));
                                            }
                                            else
                                            {
                                                mongo.PrescriptionCollection.UpdateOne(m => m.UniqueId == item.UniqueId, Builders<Prescription>.Update.Inc(m => m.RetriesNumber, 1).Set(m => m.IsSynchronized, true).Set(m => m.IssuedTime, DateTime.Now).Set(m => m.TimeFilter, DateTime.Now));
                                            }
                                            break;
                                        default:
                                            Global.MonitorLogger.Error(log);
                                            if (!isCheckIn)
                                            {
                                                mongo.PrescriptionCollection.UpdateOne(m => m.UniqueId == item.UniqueId, Builders<Prescription>.Update.Inc(m => m.RetriesNumber, 1));
                                            }
                                            break;
                                    }

                                    /*
                                    create or replace procedure tthis.prc_sfra_medicine_charge
                                    (Par_PATIENTID          IN VARCHAR2,
                                     Par_INPATIENTNO        IN VARCHAR2,
                                     Par_AMOUNT             IN NUMBER,  
                                     Par_OPERATOR           IN VARCHAR2,--操作员ID
                                     PAR_EXECSQN            IN VARCHAR2,--手术主键
                                     Par_EXECUNITID         IN NUMBER,  --执行科室
                                     Par_INSTOREID          IN VARCHAR2,--药品主键
                                     Par_batchno            IN VARCHAR2,--药品批号
                                     Par_errmsg             OUT VARCHAR2
                                    )
                                    药品收退费存储过程（只能给住院患者收退费）
                                    Prc_sfra_medicine_charge(par_patientid,par_inpatientno,par_amount,par_operator,par_execsqn,par_execunitid,par_instoreid,par_batchno,par_errmsg）
                                    入参1 ：住院患者ID (Patient.UniqueId)  
                                    入参2：住院号（Patient.HospitalNumber）
                                    入参3：收/退费数量
                                    入参4：员工工号（Employee.JobNo）
                                    入参5：手术排班唯一标识（OperationSchedule.UniqueId）
                                    入参6：收费科室编码
                                    入参7：药品编码（Goods. UniqueId）
                                    入参8：药品批号（毒麻药品需提供；普通药品可不提供）
                                    出参1：报错信息（空则无报错）

                                    校验条件：1）患者是正常在院患者。
                                    2）药品退费数量不能大于已收费数量。
                                    3）工作人员JobNo在HIS中有效。
                                    4）手术排班UniqueId在HIS中有效。
                                    5）收费科室编码在HIS中有效。
                                    6）药品编码在HIS中有效。
                                    */

                                    break;
                                case "MedicalConsume":
                                    // 耗材计费时，使用的每个条码计费一次
                                    cmd.CommandText = "tthis.prc_sfra_material_charge";
                                    cmd.Parameters.Add("Par_PATIENTID", OracleDbType.Varchar2).Value = item.PatientId;
                                    cmd.Parameters.Add("Par_AMOUNT", OracleDbType.Double).Value = isCheckIn ? -1 : 1;
                                    cmd.Parameters.Add("Par_OPERATOR", OracleDbType.Varchar2).Value = item.DoctorId;
                                    cmd.Parameters.Add("PAR_EXECSQN", OracleDbType.Varchar2).Value = item.OperationScheduleId;
                                    cmd.Parameters.Add("Par_EXECUNITID", OracleDbType.Double).Value = 133;  // 麻醉科 133
                                    cmd.Parameters.Add("Par_BARCODE", OracleDbType.Varchar2).Value = item.Goods.UniqueId ?? string.Empty;
                                    cmd.Parameters.Add("par_errmsg", OracleDbType.Varchar2, 5000).Direction = ParameterDirection.Output;
                                    cmd.ExecuteNonQuery();
                                    var logm = $"{item.UniqueId}, {item.RecordType}    {string.Join(",", cmd.Parameters.Cast<OracleParameter>().Select(x => $"{x.ParameterName}={x.Value}"))}";
                                    switch (cmd.Parameters["Par_errmsg"].Value?.ToString())
                                    {
                                        case "":
                                        case null:
                                        case "null":
                                            executes.Add(item.UniqueId);
                                            Global.MonitorLogger.Info(logm);
                                            if (isCheckIn)
                                            {
                                                mongo.PrescriptionCollection.UpdateOne(m => m.UniqueId == item.UniqueId, Builders<Prescription>.Update.Set(m => m.IsDisabled, true));
                                            }
                                            else
                                            {
                                                mongo.PrescriptionCollection.UpdateOne(m => m.UniqueId == item.UniqueId, Builders<Prescription>.Update.Inc(m => m.RetriesNumber, 1).Set(m => m.IsSynchronized, true).Set(m => m.IssuedTime, DateTime.Now).Set(m => m.TimeFilter, DateTime.Now));
                                            }
                                            break;
                                        default:
                                            Global.MonitorLogger.Error(logm);
                                            if (!isCheckIn)
                                            {
                                                mongo.PrescriptionCollection.UpdateOne(m => m.UniqueId == item.UniqueId, Builders<Prescription>.Update.Inc(m => m.RetriesNumber, 1));
                                            }
                                            break;
                                    }
                                    /*
                                    高值耗材收退费存储过程
                                    create or replace procedure tthis.prc_sfra_material_charge
                                    (Par_PATIENTID          IN VARCHAR2,
                                     Par_AMOUNT             IN NUMBER,
                                     Par_OPERATOR           IN VARCHAR2,
                                     PAR_EXECSQN            IN VARCHAR2,
                                     Par_EXECUNITID         IN NUMBER,
                                     Par_BARCODE            IN VARCHAR2,
                                     par_errmsg             OUT VARCHAR2
                                    )

                                    Prc_sfra_material_charge(par_patientid,par_amount,par_operator,par_execsqn,par_execunitid,par_barcode,par_errmsg)
                                    入参1： (Patient.UniqueId)     
                                    入参2：收/退费数量           
                                    入参3：员工工号（Employee.JobNo）
                                    入参4：手术排班唯一标识（OperationSchedule.UniqueId）   
                                    入参5：收费科室编码
                                    入参6：高值耗材条码号
                                    出参1：报错信息（空则无报错）

                                    校验条件：
                                    1）患者是正常在院患者。
                                    2）收退费数量只能是1或-1。
                                    3）工作人员JobNo在HIS中有效。
                                    4）手术排班UniqueId在HIS中有效。
                                    5）收费科室编码在HIS中有效。
                                    6）高值耗材条码在库未被使用。
                                    7）该条码已收费数量为0则不能再退费
                                    */

                                    break;
                                default: Global.MonitorLogger.Error($"wrong goods filter: {item.Goods.Filter}"); break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Global.MonitorLogger.Error($"{item.UniqueId}, {item.RecordType}    {string.Join(",", cmd.Parameters.Cast<OracleParameter>().Select(x => $"{x.ParameterName}={x.Value}"))}", ex);
                        }
                    }
                    status.Code = executes.Distinct().Any() ? 0 : -1;
                    status.Data = executes.Distinct().ToArray();
                    status.Msg = $"预计费数 '{pres.Count}', 计费成功数 '{executes.Distinct().Count()}'";
                }
            }

            void SyncSZZY(string[] medications, ApiBack<dynamic> status)
            {
                var charged = 0;
                var client = new TcpClient();
                var pres = new List<string[]> { };
                try
                {
                    // 设置client事件
                    client.OnConnect += new TcpClientEvent.OnConnectEventHandler(OnConnect);
                    client.OnSend += new TcpClientEvent.OnSendEventHandler(OnSend);
                    client.OnReceive += new TcpClientEvent.OnReceiveEventHandler(OnReceive);
                    client.OnClose += new TcpClientEvent.OnCloseEventHandler(OnClose);

                    var port = Global.AppSettings["SZZY"]["RemotePort"].Value<ushort>();
                    var host = Global.AppSettings["SZZY"]["RemoteIP"].Value<string>();
                    if (client.Connect(host, port, false))
                    {
                        Global.MonitorLogger.Info($"$Client Start OK -> ({host}:{port})");
                    }
                    else
                    {
                        Global.MonitorLogger.Error($"$Client Start Error -> {client.ErrorMessage}({client.ErrorCode})");
                        return;
                    }

                    var sends = hl7Messages();
                    foreach (var send in sends)
                    {
                        var body = $"\v{send}\r\u001c\r";
                        byte[] msgBytes = Encoding.GetEncoding("gbk").GetBytes(body);
                        var base64 = Convert.ToBase64String(msgBytes);
                        // 发送
                        if (client.Send(msgBytes, msgBytes.Length))
                        {
                            Global.MonitorLogger.Info($"$ ({client.ConnectionId}) Send OK --> {body}\t{base64}");
                        }
                        else
                        {
                            Global.MonitorLogger.Error($"$ ({client.ConnectionId}) Send Fail --> {body} ({msgBytes.Length})\t{base64}");
                        }
                        // 防止粘包,延迟2秒发送下一组数据
                        Thread.Sleep(2000);
                    }

                    HandleResult OnConnect(TcpClient sender)
                    {
                        // 已连接 到达一次
                        Global.MonitorLogger.Info($" > [{sender.ConnectionId},OnConnect]");
                        return HandleResult.Ok;
                    }

                    HandleResult OnSend(TcpClient sender, byte[] bytes)
                    {
                        // 客户端发数据了
                        Global.MonitorLogger.Info($" > [{sender.ConnectionId},OnSend] -> ({bytes.Length} bytes)");
                        return HandleResult.Ok;
                    }

                    HandleResult OnReceive(TcpClient sender, byte[] bytes)
                    {
                        // 数据到达了
                        var base64 = Convert.ToBase64String(bytes);
                        var content = Encoding.GetEncoding("gbk").GetString(bytes);
                        Global.MonitorLogger.Info($" > [{sender.ConnectionId},OnReceive] -> ({bytes.Length} bytes)\t{base64}{Environment.NewLine}{content}");
                        charged += reserve(content) ? 1 : 0;
                        return HandleResult.Ok;
                    }

                    HandleResult OnClose(TcpClient sender, SocketOperation enOperation, int errorCode)
                    {
                        if (errorCode == 0)
                        {
                            // 连接关闭了
                            Global.MonitorLogger.Info($" > [{sender.ConnectionId},OnClose]");
                        }
                        else
                        {
                            // 出错了
                            Global.MonitorLogger.Info($" > [{sender.ConnectionId},OnError] -> OP:{enOperation},CODE:{errorCode}");
                        }
                        return HandleResult.Ok;
                    }
                }
                catch (Exception ex)
                {
                    Global.MonitorLogger.Error(ex);
                }
                finally
                {
                    if (client.Stop())
                    {
                        Global.MonitorLogger.Info("Stop OK");
                    }
                    else
                    {
                        Global.MonitorLogger.Error($"Stop Error -> {client.ErrorMessage}({client.ErrorCode})");
                    }

                    status.Code = charged > 0 ? 0 : -1;
                    status.Data = charged;
                    status.Msg = $"预计费数 '{medications.Length}', 计费成功数 '{charged}'";
                    Global.MonitorLogger.Info(status.Msg);
                }


                string[] hl7Messages()
                {
                    // 从 mongodb 查询数据，换成 hl7
                    var HL7Collector = new SZZYHL7Converter();
                    var clinicHl7Strings = HL7Collector.ClinicHL7(medications, out List<string[]> clinicPreIds);
                    var hospitalHl7Strings = HL7Collector.HospitalizationHL7(medications, out List<string[]> hospitalPreIds);
                    pres = clinicPreIds.Concat(hospitalPreIds).ToList();
                    return clinicHl7Strings.Concat(hospitalHl7Strings).ToArray();
                }

                bool reserve(string hl7)
                {
                    // 解析 HIS 返回的 hl7，更新 monogdb 数据库
                    // 成功就修改计费时间
                    var msa = hl7.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[1];                    
                    if (msa.Substring(4, 2) == "AA")
                    {
                        var id = msa.Substring(7, msa.LastIndexOf('|') - 7);
                        pres.Where(m => m.Contains(id)).SelectMany(o => o).ToList().ForEach(m =>
                        {
                            mongo.PrescriptionCollection.UpdateOne(p => p.UniqueId == m, Builders<Prescription>.Update.Inc(p => p.RetriesNumber, 1).Set(p => p.IsSynchronized, true).Set(p => p.IssuedTime, DateTime.Now).Set(p => p.TimeFilter, DateTime.Now));
                        });
                    }
                    return msa.Substring(4, 2) == "AA";
                }
            }

            void SyncSDSL(string[] medications, ApiBack<dynamic> status)
            {
                var oss = mongo.MedicationCollection.AsQueryable().Where(m => m.Mode == ExchangeMode.Medication && medications.Contains(m.UniqueId)).Select(m => m.OperationScheduleId).Distinct().ToList();
                if (oss.Any())
                {
                    mongo.OperationScheduleCollection.UpdateMany(os => oss.Contains(os.UniqueId), Builders<OperationSchedule>.Update.Set(os => os.ExecutionEndTime, DateTime.Now));
                }

                //using (var ctx = new SqlServerDbContext())
                //{
                //    var @params = new SqlParameter[]
                //                    {
                //    new SqlParameter { ParameterName = "@DealerID", Direction = ParameterDirection.Input, Value = 1, },
                //    new SqlParameter { ParameterName = "@ResultCode", Direction = ParameterDirection.Output, Size = 50, },
                //    new SqlParameter { ParameterName = "@ResultMsg", Direction = ParameterDirection.Output, Size = 100, },
                //                    };
                //    var data = ctx.Database.SqlQuery<dynamic>("PROC_INSERT_DATA_ID @DealerID, @ResultCode out, @ResultMsg out", @params).FirstOrDefault();
                //    result.Code = int.Parse(@params[1].Value.ToString());
                //    result.Msg = @params[2].Value.ToString();
                //}
                result.Code = -1;
                result.Msg = "NotImplementedException";
            }
        }
    }

    /// <summary>
    ///     跟 HIS 等第三方系统进行数据同步时，SFRAMED 内部使用
    /// </summary>
    public partial class SyncDataInternal : System.Web.Services.WebService, ISyncObject
    {
        protected readonly MongoContext mongo = new MongoContext();
        private readonly com.biohis.Medicine.AuthenticationToken szgjToken = new com.biohis.Medicine.AuthenticationToken
        {
            Username = Global.AppSettings["SZGJ"]["Username"].Value<string>(),
            Password = Global.AppSettings["SZGJ"]["Password"].Value<string>(),
        };

        [WebMethod(Description = "同步所有的员工"), ScriptMethod(ResponseFormat = ResponseFormat.Json, UseHttpGet = true)]
        public void SyncAllEmployees(string hospital)
        {
            var result = new ApiBack { Code = -(short.MinValue + 1), Msg = $"Location Error '{hospital}'", Hospital = hospital, };
            switch (hospital)
            {
                case Hospital.SZGJ: SyncSZGJ(); break;
                case Hospital.SDSL: SyncSDSL(); break;
                case Hospital.SDTZ: SyncSDTZ(); break;
                case Hospital.SDSL_BY: SyncSDSL_BY(); break;
                default: SyncFromCollector(); break;
            }
            ResponseJSON(result);

            void SyncSDTZ()
            {
                try
                {
                    var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "sdtz_agent.csv");
                    var Agents = File.ReadAllLines(file, System.Text.Encoding.UTF8).Select(c =>
                    {
                        var sp = c.Split(',');
                        return new { Id = sp[0], CertificateCode = sp[1], };
                    }).ToList();
                    Agents = Agents.Skip(1).ToList();
                    var dbEmployees = mongo.EmployeeCollection.AsQueryable().ToList();
                    var empIds = dbEmployees.Select(d => d.UniqueId).ToList();
                    var test = string.Empty;
                    dbEmployees.ForEach(e =>
                    {
                        var find = Agents.FirstOrDefault(f => f.Id == e.UniqueId);
                        var item = new Employee
                        {
                            UniqueId = e.UniqueId,
                            DisplayName = e.DisplayName,
                            Pinyin = e.DisplayName?.Pinyin(),
                            PinyinFull = e.DisplayName?.PinyinFull(),
                            JobNo = e.JobNo,
                            JobTitle = e.JobTitle,
                            Department = e.Department,
                            DepartmentId = e.DepartmentId,
                            DisplayOrder = int.TryParse(e.JobNo, out int order) ? order : -1,

                            CreatedTime = DateTime.Now,
                            IsDisabled = false,
                            Signature = null,

                            Address = null,
                            Age = null,
                            Birthday = null,
                            CellPhone = null,
                            CertificateCode = find?.CertificateCode,
                            CertificateType = null,
                            Code = null,
                            Email = null,
                            Gender = null,
                            JobState = null,
                            Nation = null,
                            Nationality = null,
                            Post = null,
                        };
                        mongo.EmployeeCollection.FindOneAndReplace<Employee>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Employee, Employee> { IsUpsert = true });
                        mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == item.UniqueId, Builders<User>.Update.Set(f => f.Employee, item));
                    });
                    result.Code = 0;
                    result.Msg = "成功";
                }
                catch (Exception ex)
                {
                    BaseConverter.WriteSyncLog(nameof(Employee), DateTime.Now.Ticks.ToString(), string.Empty, ex);
                    result.Code = -1;
                    result.Msg = ex.Message;
                }
            }

            void SyncSZGJ()
            {
                var cvtr = new SZGJConverter();
                string xml = null;
                try
                {
                    var biohis = new com.biohis.Medicine { header = szgjToken, };
                    xml = biohis.getEmp(string.Empty);
                    var cvt = cvtr.SuZhouEmps2Employees(xml);
                    foreach (var item in cvt.Data)
                    {
                        var key = item.JobTitle.Contains("医") ? $"{item.DepartmentId}:Doctors" : item.JobTitle.Contains("护士") ? $"{item.DepartmentId}:Nurses" : null;
                        if (!string.IsNullOrEmpty(key))
                        {
                            var config = mongo.SystemConfigCollection.AsQueryable().FirstOrDefault(f => f.Key == key) ?? new SystemConfig { Key = key, };
                            var employees = string.IsNullOrEmpty(config.JObject) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(config.JObject);
                            employees = employees.Concat(new[] { item.UniqueId }).Distinct().ToList();
                            config.JObject = JsonConvert.SerializeObject(employees);
                            mongo.SystemConfigCollection.FindOneAndReplace<SystemConfig>(x => x.UniqueId == config.UniqueId, config, new FindOneAndReplaceOptions<SystemConfig, SystemConfig> { IsUpsert = true });
                        }

                        item.IsDisabled = item.DisplayName.Contains("停用");
                        mongo.EmployeeCollection.FindOneAndReplace<Employee>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Employee, Employee> { IsUpsert = true });
                    }
                    BaseConverter.WriteSyncLog(nameof(Employee), DateTime.Now.Ticks.ToString(), xml);
                    result.Code = cvt.Code;
                    result.Msg = cvt.Message;
                }
                catch (Exception ex)
                {
                    BaseConverter.WriteSyncLog(nameof(Employee), DateTime.Now.Ticks.ToString(), xml, ex);
                    result.Code = -1;
                    result.Msg = "无法连接到 HIS 系统";
                }
            }

            void SyncFromCollector()
            {
                try
                {
                    using (var client = new System.Net.Http.HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseProxy = false, }))
                    {
                        var url = $"http://127.0.0.1:10240/api/collector/once?collection={nameof(Employee)}";
                        var response = client.GetAsync(url).Result;
                        var json = response.Content.ReadAsStringAsync().Result;
                        var obj = JsonConvert.DeserializeObject<JObject>(json);
                        result.Code = obj["code"].Value<int>();
                        result.Msg = $"同步员工信息{(result.Code == 0 ? "成功" : "失败")}, 耗时 {obj["span"].Value<string>()}";
                    }
                }
                catch (Exception ex)
                {
                    Global.MonitorLogger.Error($"hospital = '{hospital}'", ex);
                    result.Code = -1;
                    result.Msg = $"{ex.InnerException?.Message} {ex.Message}";
                }
            }

            void SyncSDSL()
            {
                try
                {
                    int take = 5120;
                    using (var ctx = new SqlServerDbContext())
                    {
                        var count = ctx.SDSLView_HaoCai_Employee.Count();
                        for (int i = 0; i < (int)Math.Ceiling(count * 1.0 / take); i++)
                        {
                            var emps = ctx.SDSLView_HaoCai_Employee.AsNoTracking().OrderBy(e => e.UniqueId).Skip(take * i).Take(take).Where(e => e != null).ToList().Select(e =>
                            {
                                var name = (e?.DisplayName?.Split('-')[0] ?? string.Empty).Trim();
                                return new Employee
                                {
                                    UniqueId = e?.JobNo,
                                    DisplayName = name,
                                    Pinyin = name?.Pinyin(),
                                    PinyinFull = name?.PinyinFull(),
                                    JobNo = e?.JobNo,
                                    JobTitle = e?.JobTitle ?? string.Empty,
                                    Department = e?.DepartmentId == null ? null : mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == e.DepartmentId),
                                    DepartmentId = e?.DepartmentId,
                                };
                            });
                            foreach (var item in emps)
                            {
                                var key = item.JobTitle.Contains("医") ? $"{item.DepartmentId}:Doctors" : item.JobTitle.Contains("护") ? $"{item.DepartmentId}:Nurses" : null;
                                if (!string.IsNullOrEmpty(key))
                                {
                                    var config = mongo.SystemConfigCollection.AsQueryable().FirstOrDefault(f => f.Key == key) ?? new SystemConfig { Key = key, };
                                    var employees = string.IsNullOrEmpty(config.JObject) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(config.JObject);
                                    employees = employees.Concat(new[] { item.UniqueId }).Distinct().ToList();
                                    config.JObject = JsonConvert.SerializeObject(employees);
                                    mongo.SystemConfigCollection.FindOneAndReplace<SystemConfig>(x => x.UniqueId == config.UniqueId, config, new FindOneAndReplaceOptions<SystemConfig, SystemConfig> { IsUpsert = true });
                                }

                                item.IsDisabled = item.DisplayName.Contains("停用");
                                mongo.EmployeeCollection.FindOneAndReplace<Employee>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Employee, Employee> { IsUpsert = true });
                            }
                        }

                        result.Code = 0;
                        result.Msg = $"同步 {count} 名员工";
                    }
                }
                catch (Exception ex)
                {
                    Global.MonitorLogger.Error($"hospital = '{hospital}'", ex);
                    result.Code = -1;
                    result.Msg = $"{ex.InnerException?.Message} {ex.Message}";
                }
            }

            void SyncSDSL_BY()
            {
                var cvtr = new SDSLBYConverter();
                string xml = null;
                try
                {
                    var cvt = cvtr.SDSLBYEmployees(xml);
                    foreach (var item in cvt.Data)
                    {
                        var key = item.JobTitle.Contains("医") ? $"{item.DepartmentId}:Doctors" : item.JobTitle.Contains("护士") ? $"{item.DepartmentId}:Nurses" : null;
                        if (!string.IsNullOrEmpty(key))
                        {
                            var config = mongo.SystemConfigCollection.AsQueryable().FirstOrDefault(f => f.Key == key) ?? new SystemConfig { Key = key, };
                            var employees = string.IsNullOrEmpty(config.JObject) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(config.JObject);
                            employees = employees.Concat(new[] { item.UniqueId }).Distinct().ToList();
                            config.JObject = JsonConvert.SerializeObject(employees);
                            mongo.SystemConfigCollection.FindOneAndReplace<SystemConfig>(x => x.UniqueId == config.UniqueId, config, new FindOneAndReplaceOptions<SystemConfig, SystemConfig> { IsUpsert = true });
                        }

                        item.IsDisabled = item.DisplayName.Contains("停用");
                        mongo.EmployeeCollection.FindOneAndReplace<Employee>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Employee, Employee> { IsUpsert = true });
                    }
                    BaseConverter.WriteSyncLog(nameof(Employee), DateTime.Now.Ticks.ToString(), xml);
                    result.Code = cvt.Code;
                    result.Msg = cvt.Message;
                }
                catch (Exception ex)
                {
                    BaseConverter.WriteSyncLog(nameof(Employee), DateTime.Now.Ticks.ToString(), xml, ex);
                    result.Code = -1;
                    result.Msg = "无法连接到 HIS 系统";
                }
            }
        }

        [WebMethod(Description = "同步部门信息"), ScriptMethod(ResponseFormat = ResponseFormat.Json, UseHttpGet = true)]
        public void SyncAllDepartments(string hospital)
        {
            var result = new ApiBack { Code = -(short.MinValue + 1), Msg = $"Location Error '{hospital}'", Hospital = hospital, };
            switch (hospital)
            {
                case Hospital.SZGJ: SyncSZGJ(); break;
                case Hospital.SDSL: SyncSDSL(); break;
                case Hospital.SDSL_BY: SyncSDSLBY(); break;
                case Hospital.BJTT: SyncBJTT(); SyncFromCollector(); break;
                default: SyncFromCollector(); break;
            }
            ResponseJSON(result);

            void SyncSZGJ()
            {
                try
                {
                    var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "szgj_department.csv");
                    var departs = File.ReadAllLines(file, System.Text.Encoding.UTF8).Select(c =>
                    {
                        var sp = c.Split(',');
                        return new { Code = sp[0], Name = sp[1], };
                    }).ToList();
                    var dbDeparts = mongo.DepartmentCollection.AsQueryable().ToList();
                    var departIds = dbDeparts.Select(d => d.UniqueId).ToList();
                    mongo.DepartmentCollection.DeleteMany(d => departIds.Contains(d.UniqueId));

                    for (int i = 0; i < departs.Count; i++)
                    {
                        var code = departs[i].Code;
                        var name = departs[i].Name;
                        var find = dbDeparts.FirstOrDefault(f => f.UniqueId == code);
                        var item = new Department
                        {
                            UniqueId = code,
                            DisplayName = name,
                            Pinyin = name.Pinyin(),
                            PinyinFull = name.PinyinFull(),
                            Code = code,
                            DisplayOrder = i + 1,
                            Filter = nameof(Department),
                            CreatedTime = DateTime.Now,
                            IsDisabled = false,
                        };
                        mongo.DepartmentCollection.FindOneAndReplace<Department>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Department, Department> { IsUpsert = true });
                    }
                    result.Code = 0;
                    result.Msg = "成功";
                }
                catch (Exception ex)
                {
                    BaseConverter.WriteSyncLog(nameof(Department), DateTime.Now.Ticks.ToString(), string.Empty, ex);
                    result.Code = -1;
                    result.Msg = ex.Message;
                }
            }

            void SyncFromCollector()
            {
                try
                {
                    using (var client = new System.Net.Http.HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseProxy = false, }))
                    {
                        var url = $"http://127.0.0.1:10240/api/collector/once?collection={nameof(Department)}";
                        var response = client.GetAsync(url).Result;
                        var json = response.Content.ReadAsStringAsync().Result;
                        var obj = JsonConvert.DeserializeObject<JObject>(json);
                        result.Code = obj["code"].Value<int>();
                        result.Msg = $"同步部门信息{(result.Code == 0 ? "成功" : "失败")}, 耗时 {obj["span"].Value<string>()}";
                    }
                }
                catch (Exception ex)
                {
                    Global.MonitorLogger.Error($"hospital = '{hospital}'", ex);
                    result.Code = -1;
                    result.Msg = $"{ex.InnerException?.Message} {ex.Message}";
                }
            }

            void SyncSDSL()
            {
                try
                {
                    using (var ctx = new SqlServerDbContext())
                    {
                        var dbDeparts = mongo.DepartmentCollection.AsQueryable().ToList();
                        var data = ctx.SDSLView_HaoCai_Room.AsNoTracking().Select(r => new { Id = r.id.ToString(), DisplayName = r.name }).Concat(ctx.SDSLView_HaoCai_Department.AsNoTracking().Select(d => new { Id = d.ID, d.DisplayName, })).ToList();
                        for (var i = 0; i < data.Count; i++)
                        {
                            var code = data[i].Id;
                            var parts = data[i].DisplayName.Split('-');
                            var name = (parts.ElementAtOrDefault(1) ?? parts.ElementAtOrDefault(0) ?? string.Empty).Trim();
                            var find = dbDeparts.FirstOrDefault(f => f.UniqueId == code);
                            var item = new Department
                            {
                                UniqueId = code,
                                DisplayName = name,
                                Pinyin = name.Pinyin(),
                                PinyinFull = name.PinyinFull(),
                                Code = code,
                                DisplayOrder = i + 1,
                                Filter = nameof(Department),
                                CreatedTime = DateTime.Now,
                                IsDisabled = name.Contains("停用"),
                            };
                            mongo.DepartmentCollection.FindOneAndReplace<Department>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Department, Department> { IsUpsert = true });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Global.MonitorLogger.Error($"hospital = '{hospital}'", ex);
                    result.Code = -1;
                    result.Msg = $"{ex.InnerException?.Message} {ex.Message}";
                }
            }

            void SyncSDSLBY()
            {
                var cvtr = new SDSLBYConverter();
                string xml = null;
                try
                {
                    var cvt = cvtr.SDSLBYDepartments(xml);
                    foreach (var item in cvt.Data)
                    {
                        mongo.DepartmentCollection.FindOneAndReplace<Department>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Department, Department> { IsUpsert = true });
                    }
                    BaseConverter.WriteSyncLog(nameof(Department), DateTime.Now.Ticks.ToString(), xml);
                    result.Code = cvt.Code;
                    result.Msg = cvt.Message;
                }
                catch (Exception ex)
                {
                    Global.MonitorLogger.Error($"hospital = '{hospital}'", ex);
                    result.Code = -1;
                    result.Msg = $"{ex.InnerException?.Message} {ex.Message}";
                }
            }

            void SyncBJTT()
            {
                try
                {
                    var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "bjtt_rooms.csv");
                    var departs = File.ReadAllLines(file, System.Text.Encoding.UTF8).Select(c =>
                    {
                        var sp = c.Split(',');
                        return new { Code = sp[0], Name = sp[1], };
                    }).ToList();
                    for (int i = 0; i < departs.Count; i++)
                    {
                        var code = departs[i].Code;
                        var name = departs[i].Name;
                        var item = new Department
                        {
                            UniqueId = code,
                            DisplayName = name,
                            Pinyin = name.Pinyin(),
                            PinyinFull = name.PinyinFull(),
                            Code = code,
                            DisplayOrder = i + 1,
                            Filter = "Room",
                            CreatedTime = DateTime.Now,
                            IsDisabled = false,
                        };
                        mongo.DepartmentCollection.FindOneAndReplace<Department>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Department, Department> { IsUpsert = true });
                    }
                    result.Code = 0;
                    result.Msg = "成功";
                }
                catch (Exception ex)
                {
                    BaseConverter.WriteSyncLog("Room", DateTime.Now.Ticks.ToString(), string.Empty, ex);
                    result.Code = -1;
                    result.Msg = ex.Message;
                }
            }
        }

        [WebMethod(Description = "同步所有物品信息"), ScriptMethod(ResponseFormat = ResponseFormat.Json, UseHttpGet = true)]
        public void SyncAllGoods(string hospital)
        {
            var result = new ApiBack { Code = -(short.MinValue + 1), Msg = $"Location Error '{hospital}'", Hospital = hospital, };
            switch (hospital)
            {
                case Hospital.SZGJ: SyncSZGJ(); break;
                case Hospital.SDSL: SyncSDSL(); break;
                case Hospital.SDSL_BY: SyncSDSL_BY(); break;
                default: SyncFromCollector(); break;
            }
            ResponseJSON(result);

            void SyncSZGJ()
            {
                var cvtr = new SZGJConverter();
                string xml = null;
                try
                {
                    var biohis = new com.biohis.Medicine { header = szgjToken, };
                    xml = biohis.getDrug(0, true);
                    var cvt = cvtr.SuZhouDrugs2Goods(xml);
                    foreach (var item in cvt.Data)
                    {
                        mongo.GoodsCollection.FindOneAndReplace<Goods>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Goods, Goods> { IsUpsert = true });
                    }
                    BaseConverter.WriteSyncLog(nameof(Goods), DateTime.Now.Ticks.ToString(), xml);
                    result.Code = cvt.Code;
                    result.Msg = cvt.Message;
                }
                catch (Exception ex)
                {
                    BaseConverter.WriteSyncLog(nameof(Goods), DateTime.Now.Ticks.ToString(), xml, ex);
                    result.Code = -1;
                    result.Msg = "无法连接到 HIS 系统";
                }
            }

            void SyncFromCollector()
            {
                try
                {
                    using (var client = new System.Net.Http.HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseProxy = false, }))
                    {
                        var url = $"http://127.0.0.1:10240/api/collector/once?collection={nameof(Goods)}";
                        var response = client.GetAsync(url).Result;
                        var json = response.Content.ReadAsStringAsync().Result;
                        var obj = JsonConvert.DeserializeObject<JObject>(json);
                        result.Code = obj["code"].Value<int>();
                        result.Msg = $"同步物品信息{(result.Code == 0 ? "成功" : "失败")}, 耗时 {obj["span"].Value<string>()}";
                    }
                }
                catch (Exception ex)
                {
                    Global.MonitorLogger.Error($"hospital = '{hospital}'", ex);
                    result.Code = -1;
                    result.Msg = $"{ex.InnerException?.Message} {ex.Message}";
                }
            }

            void SyncSDSL()
            {
                try
                {
                    int take = 5120;
                    using (var ctx = new SqlServerDbContext())
                    {
                        var names = Global.AppSettings["SDSL"]["Drugs"].Values<string>().ToList();
                        var speExp = Global.AppSettings["SDSL"]["SpecExp"].Value<string>();
                        var categories = mongo.GoodsCategoryCollection.AsQueryable().ToList();
                        var drugs = categories.Where(c => names.Contains(c.DisplayName)).SelectMany(c => c.GoodsKeys).ToList();
                        var comss = categories.Where(c => !names.Contains(c.DisplayName)).SelectMany(c => c.GoodsKeys).ToList();

                        var count = ctx.SDSLView_HaoCai_Goods.Count();
                        for (int i = 0; i < (int)Math.Ceiling(count * 1.0 / take); i++)
                        {
                            var dbData = ctx.SDSLView_HaoCai_Goods.AsNoTracking().OrderBy(g => g.ARCIM_CODE).Skip(i * take).Take(take).ToList();
                            var goods = dbData.Select(g =>
                            {
                                var displayName = g.ARCIM_NAME.ToDBC();
                                var value = new Goods
                                {
                                    UniqueId = g.ARCIM_CODE,
                                    DisplayName = displayName,
                                    Pinyin = displayName.Pinyin(),
                                    PinyinFull = displayName.PinyinFull(),
                                    Code = g.ARCIM_CODE,
                                    GoodsType = g.ORD_SUBCAT,
                                    Price = double.TryParse(g.ORD_PRICE, out double price) ? price : double.NaN,
                                    Conversion = 1.0,
                                    SmallPackageUnit = string.Empty,
                                    IsSync = true,
                                    CreatedTime = DateTime.Now,
                                    IsDisabled = displayName.Contains("停用"),
                                    UsedUnit = string.Empty,
                                };

                                var match = Regex.Match(displayName, speExp);
                                if (match.Success)
                                {
                                    value.DisplayName = displayName.Replace(match.Groups[0].Value, string.Empty);
                                    value.Specification = match.Groups[0].Value.Trim('(', ')');
                                }
                                var isDrug = drugs.Any(k => k == g.ARCIM_CODE.ToString());
                                var isConsume = comss.Any(k => k == g.ARCIM_CODE.ToString());
                                value.Filter = isDrug ? "Drug" : isConsume ? "MedicalConsume" : match.Success ? "Drug" : "MedicalConsume";
                                return value;
                            });
                            foreach (var item in goods)
                            {
                                mongo.GoodsCollection.FindOneAndReplace<Goods>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Goods, Goods> { IsUpsert = true });
                                mongo.TerminalGoodsCollection.UpdateOne(t => t.GoodsId == item.UniqueId, Builders<TerminalGoods>.Update.Set(t => t.Goods, item));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {

                    Global.MonitorLogger.Error($"hospital = '{hospital}'", ex);
                    result.Code = -1;
                    result.Msg = $"{ex.InnerException?.Message} {ex.Message}";
                }
            }

            void SyncSDSL_BY()
            {
                var cvtr = new SDSLBYConverter();
                string xml = null;
                try
                {
                    var cvt = cvtr.SDSLBYGoods(xml);
                    foreach (var item in cvt.Data)
                    {
                        mongo.GoodsCollection.FindOneAndReplace<Goods>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Goods, Goods> { IsUpsert = true });
                    }
                    BaseConverter.WriteSyncLog(nameof(Goods), DateTime.Now.Ticks.ToString(), xml);
                    result.Code = cvt.Code;
                    result.Msg = cvt.Message;
                }
                catch (Exception ex)
                {
                    BaseConverter.WriteSyncLog(nameof(Goods), DateTime.Now.Ticks.ToString(), xml, ex);
                    result.Code = -1;
                    result.Msg = "无法连接到 HIS 系统";
                }
            }
        }

        [WebMethod(Description = "同步指定调拨单的调拨数据"), ScriptMethod(ResponseFormat = ResponseFormat.Json, UseHttpGet = true)]
        public void SyncAllocationsByStockNo(string hospital, string stock)
        {
            var result = new ApiBack<string[]> { Code = -(short.MinValue + 1), Msg = $"Location Error '{hospital}'", Hospital = hospital, Data = new string[0], };
            switch (hospital)
            {
                case Hospital.SZGJ: SyncSZGJ(); break;
            }
            ResponseJSON(result);

            void SyncSZGJ()
            {
                var cvtr = new SZGJConverter();
                string xml = null;
                try
                {
                    var biohis = new com.biohis.Medicine { header = szgjToken, };
                    xml = biohis.getStock(stock);
                    var cvt = cvtr.SuZhouStocks2Allocations(xml, stock);

                    var allocIds = cvt.Data.Select(d => d.UniqueId).ToArray();
                    for (int i = 0; i < allocIds.Length; i++)
                    {
                        var alloc = cvt.Data[i];
                        if (mongo.AllocationCollection.AsQueryable().Any(a => a.UniqueId == alloc.UniqueId))
                        {
                            continue;
                        }
                        if (alloc.Goods == null)
                        {
                            // 新药
                            var xmlDg = biohis.getDrug(long.Parse(alloc.GoodsId), true);
                            var dgs = cvtr.SuZhouDrugs2Goods(xmlDg).Data;
                            alloc.Goods = dgs.FirstOrDefault();
                            if (alloc.Goods != null)
                            {
                                mongo.GoodsCollection.FindOneAndReplace<Goods>(g => g.UniqueId == alloc.GoodsId, alloc.Goods, new FindOneAndReplaceOptions<Goods, Goods> { IsUpsert = true });
                            }
                        }
                        mongo.AllocationCollection.FindOneAndReplace<Allocation>(x => x.UniqueId == alloc.UniqueId, alloc, new FindOneAndReplaceOptions<Allocation, Allocation> { IsUpsert = true });
                    }
                    BaseConverter.WriteSyncLog(nameof(Allocation), stock, xml);
                    result.Code = cvt.Code;
                    result.Msg = cvt.Message;
                    result.Data = allocIds;
                }
                catch (Exception ex)
                {
                    BaseConverter.WriteSyncLog(nameof(Allocation), stock, xml, ex);
                    result.Code = -1;
                    result.Msg = "无法连接到 HIS 系统";
                }
            }
        }

        [WebMethod(Description = "查询指定的患者信息"), ScriptMethod(ResponseFormat = ResponseFormat.Json, UseHttpGet = true)]
        public void SyncPatientByNo(string hospital, string patient)
        {
            var result = new ApiBack<PatientProfile> { Code = -(short.MinValue + 1), Msg = $"Location Error '{hospital}'", Hospital = hospital, };
            switch (hospital)
            {
                case Hospital.SZGJ: SyncSZGJ(); break;
            }
            ResponseJSON(result);

            void SyncSZGJ()
            {
                var cvtr = new SZGJConverter();
                string xml = null;
                try
                {
                    var biohis = new com.biohis.Medicine { header = szgjToken, };
                    xml = biohis.getInpatient(patient);
                    var cvt = cvtr.SuZhouPatients2Patients(xml);
                    var patObj = cvt?.Data.FirstOrDefault();
                    if (patObj != null)
                    {
                        mongo.PatientCollection.FindOneAndReplace<Patient>(x => x.UniqueId == patObj.UniqueId, patObj, new FindOneAndReplaceOptions<Patient, Patient> { IsUpsert = true });
                        result.Data = new PatientProfile
                        {
                            UniqueId = patObj.UniqueId,
                            DisplayName = patObj.DisplayName,
                            BedNo = patObj.Hospitalization.BedNo,
                            HospitalNumber = patObj.Hospitalization.HospitalNumber,
                            SerialNumber = patObj.Clinic.SerialNumber,
                            MedicareNumber = patObj.MedicareNumber,
                            RegisterNumber = patObj.RegisterNumber,
                            Age = patObj.Age,
                            Gender = patObj.Gender,
                            Diagnostic = patObj.Diagnostic,
                            CertificateCode = patObj.CertificateCode,
                            CertificateType = patObj.CertificateType,
                        };
                    }
                    BaseConverter.WriteSyncLog(nameof(Patient), patient, xml);
                    result.Code = cvt.Code;
                    result.Msg = cvt.Message;
                }
                catch (Exception ex)
                {
                    BaseConverter.WriteSyncLog(nameof(Patient), patient, xml, ex);
                    result.Code = -1;
                    result.Msg = "无法连接到 HIS 系统";
                }
            }
        }

        [WebMethod(Description = "同步手术排班信息"), ScriptMethod(ResponseFormat = ResponseFormat.Json, UseHttpGet = true)]
        public void SyncOperationSchedule(string hospital, string room, DateTime begin, DateTime end)
        {
            var result = new ApiBack { Code = -(short.MinValue + 1), Msg = $"Location Error '{hospital}'", Hospital = hospital, };
            switch (hospital)
            {
                case Hospital.SDSL: SyncSDSL(); break;
                case Hospital.BJFC: SyncBJFC(); break;
                case Hospital.BJTT: SyncBJTT(); break;
            }
            ResponseJSON(result);

            void SyncSDSL()
            {
                using (var ctx = new SqlServerDbContext())
                {
                    var dataSource = ctx.SDSLView_HaoCai_Patient.Where(p => (room == null || p.RoomId.ToString() == room) && SqlFunctions.DateAdd("day", 0, p.手术日期) >= begin && SqlFunctions.DateAdd("day", 0, p.手术日期) < end)
                        .Join(ctx.SDSLView_HaoCai_OperationSchedule, p => p.UniqeId, os => os.OP_record_ID, (p, os) => new { p, os })
                        .Distinct().AsNoTracking().ToList();

                    var cvtData = dataSource.Select(o =>
                    {
                        var patient = new Patient
                        {
                            UniqueId = o.p.UniqeId,
                            DisplayName = o.p.DisplayName,
                            Pinyin = o.p.DisplayName.Pinyin(),
                            PinyinFull = o.p.DisplayName.PinyinFull(),
                            Gender = o.p.Gender,
                            Diagnostic = o.p.Diagnostic,
                            Hospitalization = new Hospitalization
                            {
                                AdmittedDepartmentId = o.p.AdmittedDepartmentId,
                                AdmittedDepartment = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == o.p.AdmittedDepartmentId),
                                HospitalNumber = o.p.Number,
                                InitiationTime = DateTime.TryParse(o.p.IntiationTime, out DateTime time) ? time : DateTime.MinValue,
                                BedNo = o.p.BedNo,
                                RoomId = o.p.RoomId.ToString(),
                                Room = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == o.p.RoomId.ToString()),
                                ResidedAreaId = o.p.ResidedAreaId,
                                ResidedArea = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == o.p.ResidedAreaId),
                                CumulativeCount = 1,
                            },
                        };

                        var date = DateTime.Parse(o.p.手术日期);
                        var uniqueId = $"{date:yyyMMdd}@{o.os.OP_record_ID}@{o.p.Number}";
                        return new OperationSchedule
                        {
                            UniqueId = uniqueId,
                            PrimaryDoctor = mongo.EmployeeCollection.AsQueryable().FirstOrDefault(e => e.UniqueId == o.os.主刀),
                            PrimaryDoctorId = o.os.主刀,
                            Patient = patient,
                            PatientId = patient.UniqueId,
                            RoomId = o.os.roomid.ToString(),
                            ApplyTime = date,
                            OperationType = o.os.Op_Type,
                            DisplayOrder = int.TryParse(o.os.OperatingIndex, out int index) ? index : -1,
                            // ExecutionBeginTime = o.os.手术开始时间,
                            ExecutionEndTime = mongo.OperationScheduleCollection.AsQueryable().Where(x => x.UniqueId == uniqueId).Select(x => x.ExecutionEndTime).FirstOrDefault(), // 手术结束时间以 SFRA 为准
                            Remark = o.os.Status,
                            IsCancelled = o.os.Status != "已排班",
                        };
                    });
                    foreach (var item in cvtData)
                    {
                        mongo.OperationScheduleCollection.FindOneAndReplace<OperationSchedule>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<OperationSchedule, OperationSchedule> { IsUpsert = true });
                        // 患者信息不单独查询，随着手术排班同步
                        mongo.PatientCollection.FindOneAndReplace<Patient>(x => x.UniqueId == item.PatientId, item.Patient, new FindOneAndReplaceOptions<Patient, Patient> { IsUpsert = true });
                    }
                    var count = dataSource.Select(os => os.os.OP_record_ID).Distinct().Count();
                    result.Code = count > 0 ? 0 : 1;
                    result.Msg = $"同步手术排班信息 {count} 条";
                }
            }

            void SyncBJFC()
            {
                BJFC.AddOrdInfoService bjfc = new BJFC.AddOrdInfoService();
                using (var ctx = new OracleDbContext())
                {
                    var osp = ctx.BJFC_SFRA_PAIBAN.AsNoTracking().Where(d => d.SCHEDULED_DATE_TIME >= begin && d.SCHEDULED_DATE_TIME < end && (room == null || d.OPERATING_ROOM_NO == room)).ToList();//&& d.HIS_APPLY_NO!=null
                    foreach (var item in osp)
                    {
                        var patient = mongo.PatientCollection.AsQueryable().Where(p => p.UniqueId == item.PATIENT_ID).FirstOrDefault() ?? new BJFCConverter().BJFCPatients(bjfc.GetPatInfo(item.PATIENT_ID)).Data.ElementAtOrDefault(0);
                        patient.Diagnostic = item.DIAG_BEFORE_OPERATION;
                        var op = new OperationSchedule
                        {
                            UniqueId = item.HIS_APPLY_NO ?? (item.SCHEDULED_DATE_TIME).ToString("yyyyMMdd") + item.PATIENT_ID,
                            PatientId = item.PATIENT_ID,
                            Patient = patient,
                            ApplyTime = item.SCHEDULED_DATE_TIME,
                            OperationState = item.TYPE,
                            OperationType = item.ABC,
                            RoomId = item.OPERATING_ROOM_NO,
                            AnesthesiaMode = item.ANESTHESIA_METHOD,
                            PrimaryAssistantId = item.FIRST_ASSISTANT_NAME,
                        };
                        mongo.PatientCollection.FindOneAndReplace<Patient>(x => x.UniqueId == patient.UniqueId, patient, new FindOneAndReplaceOptions<Patient, Patient> { IsUpsert = true });
                        mongo.OperationScheduleCollection.FindOneAndReplace<OperationSchedule>(o => o.UniqueId == op.UniqueId, op, new FindOneAndReplaceOptions<OperationSchedule, OperationSchedule> { IsUpsert = true });
                    }
                    result.Code = osp.Count > 0 ? 0 : 1;
                    result.Msg = $"同步手术排班信息 {osp.Count} 条";
                    Global.SchedulerLogger.Info($"同步手术排班信息 {osp.Count} 条");
                }
            }

            void SyncBJTT()
            {
                using (var ctx = new OracleDbContext())
                {
                    var osp = ctx.BJTTview_sfra_operationschedule.AsNoTracking().Where(os => os.APPLYTIME >= begin && os.APPLYTIME < end).ToList();
                    foreach (var os in osp)
                    {
                        // HIS 的手术排版不指定到手术室，需要后续手动配置
                        var op = new OperationSchedule
                        {
                            UniqueId = os.UNIQUEID,
                            PatientId = os.PATIENTID,
                            Patient = mongo.PatientCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == os.PATIENTID),
                            ApplyTime = os.APPLYTIME,
                            OperationType = os.OPERATIONTYPE,
                            IdentityBarcode = os.IDENTITYBARCODE,
                        };
                        if (mongo.OperationScheduleCollection.AsQueryable().Any(o => o.UniqueId == op.UniqueId))
                        {
                            mongo.OperationScheduleCollection.UpdateOne(o => o.UniqueId == op.UniqueId, Builders<OperationSchedule>.Update
                                .Set(o => o.PatientId, op.PatientId).Set(o => o.Patient, op.Patient).Set(o => o.ApplyTime, op.ApplyTime).Set(o => o.OperationType, op.OperationType).Set(o => o.IdentityBarcode, op.IdentityBarcode));
                        }
                        else
                        {
                            mongo.OperationScheduleCollection.InsertOne(op);
                        }
                    }
                    result.Code = osp.Count > 0 ? 0 : 1;
                    result.Msg = $"同步手术排班信息 {osp.Count} 条";
                    Global.SchedulerLogger.Info($"同步手术排班信息 {osp.Count} 条");
                }
            }
        }

        private void ResponseJSON(object result)
        {
            Context.Response.ContentType = "application/json";
            Context.Response.Write(JsonConvert.SerializeObject(result, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            }));
            Context.Response.End();
        }
    }
}

namespace HealthCare.WebService.com.biohis
{
    public partial class Medicine
    {
        public AuthenticationToken header { get; set; }

        /// <remarks/>
        [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.0.30319.17929")]
        [System.SerializableAttribute()]
        [System.Diagnostics.DebuggerStepThroughAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        public class AuthenticationToken : System.Web.Services.Protocols.SoapHeader
        {
            /// <remarks/>
            public string Username { get; set; }  //要传入的账号

            /// <remarks/>
            public string Password { get; set; }   //要传入的密码
        }
    }
}
