//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.MongoData;
using HealthCare.WebService.InternalApi;
using Newtonsoft.Json.Linq;
using Quartz;
using Quartz.Impl;
using System;
using System.Linq;

#pragma warning disable CS1591

namespace HealthCare.WebService.Jobs
{
    public class SDSLOperationSyncScheduler
    {
        public static void Start()
        {
            var job = new JobDetailImpl(nameof(SDSLOperationSyncJob), typeof(SDSLOperationSyncJob));
            var trigger = CronScheduleBuilder.CronSchedule(new CronExpression(Global.AppSettings["SDSL"]["OperationSyncScheduler"].Value<string>())).Build();
            trigger.Key = new TriggerKey($"{nameof(SDSLOperationSyncScheduler)}-{nameof(trigger)}");

            var scheduler = new StdSchedulerFactory().GetScheduler();
            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
            Global.SchedulerLogger.Info($"{nameof(SDSLOperationSyncScheduler)} Started");

            System.Threading.Tasks.Task.Run(() => new SDSLOperationSyncJob().Execute(null));
        }
    }

    public class SDSLOperationSyncJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            try
            {
                var ctrl = new SyncDataInternal();
                var spans = Global.AppSettings["SDSL"]["DateSpan"].Values<int>().ToList();
                var begin = DateTime.Now.Date.AddDays(spans[0]);
                var end = DateTime.Now.Date.AddDays(spans[1] + 1);
                ctrl.SyncOperationSchedule(Hospital.SDSL, null, begin, end);
                Global.SchedulerLogger.Info($"[{begin:yyyy-MM-dd}, {end:yyyy-MM-dd})");
            }
            catch (Exception ex)
            {
                Global.SchedulerLogger.Error(nameof(SDSLOperationSyncJob), ex);
            }
        }
    }
}