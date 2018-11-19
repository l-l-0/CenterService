using HealthCare.Data;
using HealthCare.WebService.Converters;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Services;

#pragma warning disable CS1591

namespace HealthCare.WebService.WebService
{
    /// <summary>
    /// SDSLBYWebService 的摘要说明
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // 若要允许使用 ASP.NET AJAX 从脚本中调用此 Web 服务，请取消注释以下行。 
    // [System.Web.Script.Services.ScriptService]
    public class SDSLBYWebService : BaseWebService
    {

        [WebMethod(Description = "接收门诊处方")]
        public bool ReceivePrescriptionClinic(string xml)
        {
            try
            {
                var cvtr = new SDSLBYConverter();
                var cvt = cvtr.SDSLBYPrescriptions(xml);
                foreach (var item in cvt.Data)
                {
                    mongo.PrescriptionCollection.FindOneAndReplace<Prescription>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Prescription, Prescription> { IsUpsert = true });
                }
                BaseConverter.WriteSyncLog(nameof(Prescription), DateTime.Now.Ticks.ToString(), xml);
                var cvtPatient = cvtr.SDSLBYPatients(xml);     //患者信息来自门诊处方
                foreach (var item in cvtPatient.Data)
                {
                    mongo.PatientCollection.FindOneAndReplace<Patient>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Patient, Patient> { IsUpsert = true });
                }
                BaseConverter.WriteSyncLog(nameof(Patient), DateTime.Now.Ticks.ToString(), xml);
                return true;
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
                return null;
            }

            var xmlDoc = new System.Xml.XmlDocument();
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testData", nameof(Prescription));
            if (Directory.Exists(directory))
            {
                var files = Directory.EnumerateFiles(directory, "*.xml", SearchOption.TopDirectoryOnly);
                var xml = "";
                foreach (var file in files)
                {
                    var reader = System.Xml.XmlReader.Create(file, new System.Xml.XmlReaderSettings { IgnoreComments = true });
                    xmlDoc.Load(reader);
                    xml = xmlDoc.InnerXml;
                    var cvtr = new SDSLBYConverter();
                    if (file.Contains("Dept"))
                    {
                        var cvt = cvtr.SDSLBYDepartments(xml);
                        var mm = cvt.Data;
                        foreach (var item in cvt.Data)
                        {
                            mongo.DepartmentCollection.FindOneAndReplace<Department>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Department, Department> { IsUpsert = true });
                        }
                    }
                    else if (file.Contains("Drug"))
                    {
                        var cvt = cvtr.SDSLBYGoods(xml);
                        var mm = cvt.Data;
                        foreach (var item in cvt.Data)
                        {
                            mongo.GoodsCollection.FindOneAndReplace<Goods>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Goods, Goods> { IsUpsert = true });
                        }
                    }
                    else if(file.Contains("User"))
                    {
                        var cvt = cvtr.SDSLBYEmployees(xml);
                        var mm = cvt.Data;
                        foreach (var item in cvt.Data)
                        {
                            mongo.EmployeeCollection.FindOneAndReplace<Employee>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Employee, Employee> { IsUpsert = true });
                        }
                    }                  
                    return "成功";
                }
                return "";
            }
            return null;
        }
    }
}


