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
    /// BJFCWebService 的摘要说明
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // 若要允许使用 ASP.NET AJAX 从脚本中调用此 Web 服务，请取消注释以下行。 
    // [System.Web.Script.Services.ScriptService]
    public class BJFCWebService : BaseWebService
    {

        [WebMethod]
        public bool DrugDict(string xml)
        {
            try
            {

                var cvtr = new BJFCConverter();
                var cvt = cvtr.BJFCDrugs(xml);
                foreach (var item in cvt.Data)
                {
                    mongo.GoodsCollection.FindOneAndReplace<Goods>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Goods, Goods> { IsUpsert = true });
                }
                BaseConverter.WriteSyncLog(nameof(Goods), DateTime.Now.Ticks.ToString(), xml);

                return true;
            }
            catch (Exception ex)
            {
                BaseConverter.WriteSyncLog(nameof(Goods), SfraObject.GenerateId(), xml, ex);
                return false;
            }
        }

        [WebMethod]
        public bool DoctorDict(string xml)
        {
            try
            {
                var cvtr = new BJFCConverter();
                var cvt = cvtr.BJFCEmps2Employees(xml);
                foreach (var item in cvt.Data)
                {
                    mongo.EmployeeCollection.FindOneAndReplace<Employee>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Employee, Employee> { IsUpsert = true });
                }
                BaseConverter.WriteSyncLog(nameof(Employee), DateTime.Now.Ticks.ToString(), xml);

                return true;
            }
            catch (Exception ex)
            {
                BaseConverter.WriteSyncLog(nameof(Employee), SfraObject.GenerateId(), xml, ex);
                return false;
            }
        }

        [WebMethod]
        public bool DeptDict(string xml)
        {
            try
            {
                var cvtr = new BJFCConverter();
                var cvt = cvtr.BJFCDepartments(xml);
                foreach (var item in cvt.Data)
                {
                    mongo.DepartmentCollection.FindOneAndReplace<Department>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Department, Department> { IsUpsert = true });
                }
                BaseConverter.WriteSyncLog(nameof(Department), DateTime.Now.Ticks.ToString(), xml);

                return true;
            }
            catch (Exception ex)
            {
                BaseConverter.WriteSyncLog(nameof(Department), SfraObject.GenerateId(), xml, ex);
                return false;
            }
        }

        [WebMethod]
        public bool SupplyDict(string xml)
        {
            try
            {
                var cvtr = new BJFCConverter();
                var cvt = cvtr.BJFCConsumables(xml);
                foreach (var item in cvt.Data)
                {
                    mongo.GoodsCollection.FindOneAndReplace<Goods>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Goods, Goods> { IsUpsert = true });
                }
                BaseConverter.WriteSyncLog(nameof(Goods), DateTime.Now.Ticks.ToString(), xml);

                return true;
            }
            catch (Exception ex)
            {
                BaseConverter.WriteSyncLog(nameof(Goods), SfraObject.GenerateId(), xml, ex);
                return false;
            }
        }
        [WebMethod]
        public string test()
        {
            string fname = "";
            var client = Context.Request.UserHostAddress;
            var local = new[] { "localhost", "127.0.0.1", "::1" };
            if (local.All(x => x != client))
            {
                return client;
            }

            var xmlDoc = new System.Xml.XmlDocument();
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testData/BJFC");
            if (Directory.Exists(directory))
            {
                var files = Directory.EnumerateFiles(directory, "*.xml", SearchOption.TopDirectoryOnly);
                DirectoryInfo folder = new DirectoryInfo(directory);
                var folders = folder.GetFiles("*.xml");
                foreach (var file in folders)
                {
                    fname = file.FullName;
                    var reader = System.Xml.XmlReader.Create(file.FullName, new System.Xml.XmlReaderSettings { IgnoreComments = true });
                    xmlDoc.Load(reader);
                    var xml = xmlDoc.InnerXml;
                    string fileName = file.Name.Split('_')[0];
                    switch (fileName)
                    {
                        case "Department": if (DeptDict(xml) == false) return "fail    " + fname; break;
                        case "Doctor": if (DoctorDict(xml) == false) return "fail    " + fname; break;
                        case "Drug": if (DrugDict(xml) == false) return "fail    " + fname; break;
                        case "Supply": if (SupplyDict(xml) == false) return "fail    " + fname; break;
                        default:
                            new BJFCConverter().BJFCPatients(xml);
                            return "路径出现问题：" + file.DirectoryName;

                    }
                }
                return files.Any() ? "成功" : "empty";
            }
            return "fail    " + fname;
        }

        [WebMethod(Description = "北京妇产上传医嘱测试")]
        public string TestBJFCUpHis(string upxml)
        {
            
            var client = Context.Request.UserHostAddress;
            var local = new[] { "localhost", "127.0.0.1", "::1" };
            if (local.All(x => x != client))
            {
                return client;
            }
            BJFC.AddOrdInfoService bjfc = new BJFC.AddOrdInfoService();
            var finds =mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && m.Mode==ExchangeMode.Medication).FirstOrDefault();
            //string upxml = new BJFCConverter().BJFCUpHis(finds);
            var xml=bjfc.AddOrdInfo(upxml);
            BaseConverter.WriteSyncLog(nameof(Medication), DateTime.Now.Ticks.ToString(), upxml + "/r/n" + xml);
            return xml;
           
        }
        [WebMethod(Description = "北京妇产上传医嘱xml")]
        public string TestBJFCxml()
        {

            var client = Context.Request.UserHostAddress;
            var local = new[] { "localhost", "127.0.0.1", "::1" };
            if (local.All(x => x != client))
            {
                return client;
            }
            BJFC.AddOrdInfoService bjfc = new BJFC.AddOrdInfoService();
            var finds = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && m.Mode == ExchangeMode.Medication).FirstOrDefault();
            string upxml = new BJFCConverter().BJFCUpHis(finds);          
            return upxml;

        }
    }
}
