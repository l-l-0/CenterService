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
    public class BJTTOperationSyncScheduler
    {
        public static void Start()
        {
            var job = new JobDetailImpl(nameof(BJTTOperationSyncJob), typeof(BJTTOperationSyncJob));
            var trigger = CronScheduleBuilder.CronSchedule(new CronExpression(Global.AppSettings["BJTT"]["OperationSyncScheduler"].Value<string>())).Build();
            trigger.Key = new TriggerKey($"{nameof(BJTTOperationSyncScheduler)}-{nameof(trigger)}");

            var scheduler = new StdSchedulerFactory().GetScheduler();
            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
            Global.SchedulerLogger.Info($"{nameof(BJTTOperationSyncScheduler)} Started");

            System.Threading.Tasks.Task.Run(() => new BJTTOperationSyncJob().Execute(null));
        }
    }

    public class BJTTOperationSyncJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            try
            {
                var ctrl = new SyncDataInternal();
                var spans = Global.AppSettings["BJTT"]["DateSpan"].Values<int>().ToList();
                var begin = DateTime.Now.Date.AddDays(spans[0]);
                var end = DateTime.Now.Date.AddDays(spans[1] + 1);
                ctrl.SyncOperationSchedule(Hospital.BJTT, null, begin, end);
                Global.SchedulerLogger.Info($"[{begin:yyyy-MM-dd}, {end:yyyy-MM-dd})");
            }
            catch (Exception ex)
            {
                Global.SchedulerLogger.Error(nameof(BJTTOperationSyncJob), ex);
            }
        }
    }
}