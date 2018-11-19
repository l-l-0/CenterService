using HealthCare.Data;
using HealthCare.MongoData;
using HealthCare.WebService.Converters;
using HealthCare.WebService.InternalApi;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using Quartz;
using Quartz.Impl;
using System;
using System.IO;
using System.Linq;
using System.Xml;

#pragma warning disable CS1591

namespace HealthCare.WebService.Jobs
{
    public class BJFCPatientSyncScheduler
    {
        public static void Start()
        {
            var job = new JobDetailImpl(nameof(BJFCPatientSyncJob), typeof(BJFCPatientSyncJob));
            var trigger = CronScheduleBuilder.CronSchedule(new CronExpression(Global.AppSettings["BJFC"]["PatientSyncScheduler"].Value<string>())).Build();
            trigger.Key = new TriggerKey($"{nameof(BJFCPatientSyncScheduler)}-{nameof(trigger)}");

            var scheduler = new StdSchedulerFactory().GetScheduler();
            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
            Global.GlobalLogger.Info($"{nameof(BJFCPatientSyncScheduler)} Started");
        }
    }
    public class BJFCPatientSyncJob : IJob
    {
        protected readonly MongoContext mongo = new MongoContext();
        public void Execute(IJobExecutionContext context)
        {
            try
            {
                Run();
                SyncData();
            }
            catch (Exception ex)
            {
                Global.SchedulerLogger.Error(nameof(Run), ex);
            }

            void Run()
            {
                var ctrl = new SyncDataInternal();
                var spans = Global.AppSettings["BJFC"]["DateSpan"].Values<int>().ToList();
                var begin = DateTime.Now.AddDays(spans[0]).Date;
                var end = DateTime.Now.AddDays(spans[1] + 1).Date;
                ctrl.SyncOperationSchedule(Hospital.BJFC, null, begin, end);
            }
            void SyncData()
            {
                string directory = Global.AppSettings["BJFC"]["SyncDir"].Value<string>();
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                DirectoryInfo folder = new DirectoryInfo(directory);
                if (folder.GetFiles("*.xml").Length > 0)
                {
                    var xmlDepts = folder.GetFiles("Department_*.xml").OrderByDescending(d => d.CreationTime);
                    var xmlDoctors = folder.GetFiles("Doctor_*.xml").OrderByDescending(d => d.CreationTime);
                    var xmlSupply = folder.GetFiles("Supply_*.xml").OrderByDescending(d => d.CreationTime);
                    var xmlDrug = folder.GetFiles("Drug_*.xml").OrderByDescending(d => d.CreationTime);
                    var cvtr = new BJFCConverter();
                    string xml;
                    foreach (var dept in xmlDepts)
                    {
                        using (StreamReader stream = new StreamReader(dept.FullName))
                        {
                            XmlTextReader rader = new XmlTextReader(stream);
                            var xmlDoc = new XmlDocument();
                            xmlDoc.Load(rader);
                            xml = xmlDoc.InnerXml;
                            try
                            {
                                var cvt = cvtr.BJFCDepartments(xml);
                                foreach (var item in cvt.Data)
                                {
                                    mongo.DepartmentCollection.FindOneAndReplace<Department>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Department, Department> { IsUpsert = true });
                                }
                            }
                            catch (Exception ex)
                            {
                                BaseConverter.WriteSyncLog(nameof(Department), SfraObject.GenerateId(), xml, ex);
                            }
                        }
                    }

                    foreach (var doctor in xmlDoctors)
                    {
                        using (StreamReader stream = new StreamReader(doctor.FullName))
                        {
                            XmlTextReader rader = new XmlTextReader(stream);
                            var xmlDoc = new XmlDocument();
                            xmlDoc.Load(rader);
                            xml = xmlDoc.InnerXml;
                            try
                            {
                                var cvt = cvtr.BJFCEmps2Employees(xml);
                                foreach (var item in cvt.Data)
                                {
                                    mongo.EmployeeCollection.FindOneAndReplace<Employee>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Employee, Employee> { IsUpsert = true });
                                }
                            }
                            catch (Exception ex)
                            {
                                BaseConverter.WriteSyncLog(nameof(Employee), SfraObject.GenerateId(), xml, ex);
                            }
                        }
                    }

                    foreach (var supply in xmlSupply)
                    {
                        using (StreamReader stream = new StreamReader(supply.FullName))
                        {
                            XmlTextReader rader = new XmlTextReader(stream);
                            var xmlDoc = new XmlDocument();
                            xmlDoc.Load(rader);
                            xml = xmlDoc.InnerXml;
                            try
                            {
                                var cvt = cvtr.BJFCConsumables(xml);
                                foreach (var item in cvt.Data)
                                {
                                    mongo.GoodsCollection.FindOneAndReplace<Goods>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Goods, Goods> { IsUpsert = true });
                                }
                            }
                            catch (Exception ex)
                            {
                                BaseConverter.WriteSyncLog(nameof(Goods), SfraObject.GenerateId(), xml, ex);
                            }
                        }
                    }

                    foreach (var drug in xmlDrug)
                    {
                        using (StreamReader stream = new StreamReader(drug.FullName))
                        {
                            XmlTextReader rader = new XmlTextReader(stream);
                            var xmlDoc = new XmlDocument();
                            xmlDoc.Load(rader);
                            xml = xmlDoc.InnerXml;
                            try
                            {
                                var cvt = cvtr.BJFCDrugs(xml);
                                foreach (var item in cvt.Data)
                                {
                                    mongo.GoodsCollection.FindOneAndReplace<Goods>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Goods, Goods> { IsUpsert = true });
                                }
                            }
                            catch (Exception ex)
                            {
                                BaseConverter.WriteSyncLog(nameof(Goods), SfraObject.GenerateId(), xml, ex);
                            }
                        }
                    }
                    folder.Delete(true);
                }
            }
        }
    }
}