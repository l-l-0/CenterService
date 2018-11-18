//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using HealthCare.WebService.Converters;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using Quartz;
using Quartz.Impl;
using System;
using System.Linq;

#pragma warning disable CS1591

namespace HealthCare.WebService.Jobs
{
    public class SZGJPatientSyncScheduler
    {
        public static void Start()
        {
            var job = new JobDetailImpl(nameof(SZGJPatientSyncJob), typeof(SZGJPatientSyncJob));
            var trigger = CronScheduleBuilder.CronSchedule(new CronExpression(Global.AppSettings["SZGJ"]["PatientSyncScheduler"].Value<string>())).Build();
            trigger.Key = new TriggerKey($"{nameof(SZGJPatientSyncScheduler)}-{nameof(trigger)}");

            var scheduler = new StdSchedulerFactory().GetScheduler();
            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
            Global.GlobalLogger.Info($"{nameof(SZGJPatientSyncScheduler)} Started");
        }
    }

    public class SZGJPatientSyncJob : IJob
    {
        protected readonly MongoContext mongo = new MongoContext();
        private readonly com.biohis.Medicine.AuthenticationToken szgjToken = new com.biohis.Medicine.AuthenticationToken
        {
            Username = Global.AppSettings["SZGJ"]["Username"].Value<string>(),
            Password = Global.AppSettings["SZGJ"]["Password"].Value<string>(),
        };

        public void Execute(IJobExecutionContext context)
        {
            try
            {
                Run();
            }
            catch (Exception ex)
            {
                Global.SchedulerLogger.Error(nameof(Run), ex);
            }

            void Run()
            {
                var biohis = new com.biohis.Medicine { header = szgjToken, };

                var hospitalNumbers = mongo.PatientCollection.AsQueryable().Where(p => !p.IsDisabled).Select(p => p.Hospitalization.HospitalNumber).Where(p => !string.IsNullOrEmpty(p)).ToList();
                Global.SchedulerLogger.Info($"{hospitalNumbers.Count} patients will be resynced");

                var cvtr = new SZGJConverter();
                foreach (var number in hospitalNumbers)
                {
                    try
                    {
                        var xml = biohis.getInpatient(number);
                        var cvt = cvtr.SuZhouPatients2Patients(xml);
                        if (cvt.Code == 1)
                        {
                            mongo.PatientCollection.UpdateOne(p => p.Hospitalization.HospitalNumber == number, Builders<Patient>.Update.Set(p => p.IsDisabled, true));
                        }
                        else
                        {
                            var patObj = cvt?.Data.FirstOrDefault();
                            if (patObj != null)
                            {
                                mongo.PatientCollection.FindOneAndReplace<Patient>(x => x.UniqueId == patObj.UniqueId, patObj, new FindOneAndReplaceOptions<Patient, Patient> { IsUpsert = true });
                            }
                            else
                            {
                                Global.SchedulerLogger.Info($"'{number}' not found.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Global.SchedulerLogger.Error($"HospitalNumber => '{number}'", ex);
                    }
                }
            }
        }
    }
}