//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.CenterService.Controllers;
using HealthCare.Data;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using Quartz;
using Quartz.Impl;
using System;
using System.Linq;

#pragma warning disable CS1591

namespace HealthCare.CenterService.Jobs
{
    public class DailyStorageScheduler
    {
        public static void Start()
        {
            var key = $"{Global.Hospital}{nameof(DailyStorageScheduler)}";
            var cron = Global.AppSettings[Global.AppSettings.ContainsKey(key) ? key : nameof(DailyStorageScheduler)].Value<string>();

            var job = new JobDetailImpl(nameof(DailyStorageJob), typeof(DailyStorageJob));
            var trigger = CronScheduleBuilder.CronSchedule(new CronExpression(cron)).Build();
            trigger.Key = new TriggerKey($"{nameof(DailyStorageScheduler)}-{nameof(trigger)}");

            var scheduler = new StdSchedulerFactory().GetScheduler();
            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
            Global.GlobalLogger.Info($"{key} Started => {cron}");

            System.Threading.Tasks.Task.Run(() => new DailyStorageJob().Execute(null));
        }
    }

    public class DailyStorageJob : IJob
    {
        private MongoContext mongo = new MongoContext();
        public void Execute(IJobExecutionContext context)
        {
            // 计算存、取、结存依赖于使用详情 ActionJournal            
            try
            {
                Global.SchedulerLogger.Info($"{nameof(DailyStorageJob)} Begin");
                var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).ToList();
                var kvps = cs.Select(c => new { c.Computer, c.OwnerCode, }).Distinct();

                foreach (var dpt in cs.Select(o => new { UniqueId = o.DepartmentId, o.Computer, }).Distinct())
                {
                    var minTime = mongo.ActionJournalCollection.AsQueryable().Where(a => a.Computer == dpt.Computer).Select(a => a.CreatedTime).OrderBy(a => a).FirstOrDefault();
                    if (minTime == DateTime.MinValue)
                    {
                        Global.SchedulerLogger.Info($"'{dpt.UniqueId}' skiped for ActionJournal is Empty");
                        continue;
                    }

                    var lstTime = mongo.InventoryCollection.AsQueryable().Where(d => d.Computer == dpt.Computer).Select(d => d.StatsTime).OrderByDescending(d => d).FirstOrDefault();
                    var begin = lstTime == DateTime.MinValue ? minTime.Date : lstTime.Date;  // 如果没有计算过，则重新开始计算
                    var end = DateTime.Now.Date;
                    Global.SchedulerLogger.Info($"{dpt.UniqueId},{dpt.Computer}\t[{begin:yyyy-MM-dd}, {end:yyyy-MM-dd})");

                    var customer = kvps.FirstOrDefault(o => o.Computer == dpt.Computer)?.OwnerCode;
                    // 删除时间范围内的旧数据
                    mongo.InventoryCollection.DeleteMany(o => o.CustomerId == customer && o.Computer == dpt.Computer && o.StatsTime >= begin && o.StatsTime < end);


                    var result = new ngController().BuildInventory(customer, dpt.Computer, begin, end);
                    if (result.Any())
                    {
                        mongo.InventoryCollection.InsertMany(result);
                    }
                }
                Global.SchedulerLogger.Info($"{nameof(DailyStorageJob)} End");
            }
            catch (Exception ex)
            {
                Global.SchedulerLogger.Error(ex);
            }
        }
    }
}