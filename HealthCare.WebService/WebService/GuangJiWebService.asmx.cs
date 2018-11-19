//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using HealthCare.WebService.Converters;
using MongoDB.Driver;
using System;
using System.IO;
using System.Linq;
using System.Web.Services;

#pragma warning disable CS1591

namespace HealthCare.WebService.WebService
{
    /// <summary>
    /// Summary description for GuangJiWebService
    /// </summary>
    [WebService(Namespace = "http://www.sframed.com")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]
    public class GuangJiWebService : BaseWebService
    {
        [WebMethod(Description = "传递病人发药数据 —— 医嘱（xml 编码要求 UTF-8 ）。查询接收病区的药柜库存，若存在缺药情况则返回 false")]
        public bool reciveData(string xml)
        {
            try
            {
                var code = 0;
                string msg = null;

                var cvtr = new SZGJConverter();
                var prescriptions = cvtr.SuZhouApply2Prescriptions(xml).Data;
                if (prescriptions.All(p => p.IsAddition))
                {
                    foreach (var p in prescriptions)
                    {
                        var medication = mongo.MedicationCollection.AsQueryable().First(m => !m.IsDisabled && m.Mode == ExchangeMode.Medication && m.DoctorId == p.DoctorId && m.PatientId == p.PatientId && m.GoodsId == p.GoodsId && m.QtyActual == p.Qty && m.PrescriptionId == null);
                        mongo.MedicationCollection.UpdateOne(x => x.UniqueId == medication.UniqueId, Builders<Medication>.Update.Set(o => o.PrescriptionId, p.UniqueId).Set(o => o.IsSynchronized, true));

                        p.QtyActual = medication.QtyActual;
                        p.Plans = medication.Plans;
                        mongo.PrescriptionCollection.FindOneAndReplace<Prescription>(x => x.UniqueId == p.UniqueId, p, new FindOneAndReplaceOptions<Prescription, Prescription> { IsUpsert = true });
                    }
                    code = 0;
                    msg = "先预支后补充的医嘱";
                }
                else
                {
                    if (prescriptions.Any(p => p.Qty != Math.Ceiling(p.Qty)))
                    {
                        // 2017-12-17 取药量为浮点数时医嘱发送到中心药房
                        code = 2;
                        msg = "非整数医嘱";
                    }
                    else
                    {
                        // 临时医嘱，查询接收部门是否库存足够
                        var dst = prescriptions.Select(p => p.DepartmentDestinationId).Where(p => !string.IsNullOrEmpty(p)).Distinct().FirstOrDefault();
                        var data = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).Select(cs => new
                        {
                            Cabinets = cs.Cabinets.Where(c => c.DepartmentId == dst),
                            OutOfCabinets = cs.OutOfCabinets.Where(v => v.DepartmentId == dst),
                        }).ToList();
                        var fills = data.SelectMany(d => d.Cabinets).Concat(data.SelectMany(d => d.OutOfCabinets)).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).ToList();
                        var unExes = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && p.DepartmentDestinationId == dst && p.FinishTime == null)
                            .Select(p => new { p.GoodsId, p.BatchNumber, p.ExpiredDate, p.Mode, p.Qty, p.QtyActual, }).ToList();
                        if (prescriptions.All(p =>
                        {
                            var qty = fills.Where(f => f.GoodsId == p.GoodsId && f.ExpiredDate > DateTime.Now && (string.IsNullOrEmpty(p.BatchNumber) || f.BatchNumber == p.BatchNumber)).Sum(f => f.QtyExisted);
                            qty -= unExes.Where(e => e.GoodsId == p.GoodsId && (p.BatchNumber ?? string.Empty) == e.BatchNumber && e.Mode == ExchangeMode.CheckOut).Sum(e => e.Qty - e.QtyActual); // 取药
                            qty += unExes.Where(e => e.GoodsId == p.GoodsId && (p.BatchNumber ?? string.Empty) == e.BatchNumber && e.Mode == ExchangeMode.CheckIn).Sum(e => e.Qty - e.QtyActual);  // 退药
                            return qty >= p.Qty;
                        }))
                        {
                            foreach (var p in prescriptions)
                            {
                                mongo.PrescriptionCollection.FindOneAndReplace<Prescription>(x => x.UniqueId == p.UniqueId, p, new FindOneAndReplaceOptions<Prescription, Prescription> { IsUpsert = true });
                                mongo.PatientCollection.FindOneAndReplace<Patient>(x => x.UniqueId == p.Patient.UniqueId, p.Patient, new FindOneAndReplaceOptions<Patient, Patient> { IsUpsert = true });
                            }

                            code = 0;
                            msg = "临时医嘱库存充足";
                        }
                        else
                        {
                            code = 1;
                            msg = "本地库存不足";
                        }
                    }
                }
                BaseConverter.WriteSyncLog(nameof(Prescription), $"{SfraObject.GenerateId()}_{msg}", xml);
                return code == 0;
            }
            catch (Exception ex)
            {
                BaseConverter.WriteSyncLog(nameof(Prescription), SfraObject.GenerateId(), xml, ex);
                return false;
            }
        }

        [WebMethod(Description = "测试")]
        public string test()
        {
            var client = Context.Request.UserHostAddress;
            var local = new[] { "localhost", "127.0.0.1", "::1" };
            if (local.All(x => x != client))
            {
                return client;
            }

            var xmlDoc = new System.Xml.XmlDocument();
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testData", nameof(Prescription));
            if (Directory.Exists(directory))
            {
                var files = Directory.EnumerateFiles(directory, "*.xml", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    var reader = System.Xml.XmlReader.Create(file, new System.Xml.XmlReaderSettings { IgnoreComments = true });
                    xmlDoc.Load(reader);
                    var xml = xmlDoc.InnerXml;
                    reciveData(xml);
                }
                return files.Any() ? "success" : "empty";
            }
            return "fail";
        }
    }
}
