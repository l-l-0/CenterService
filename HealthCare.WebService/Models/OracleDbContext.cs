//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using System.Data.Entity;
using System.Linq;
using System.Reflection;

#pragma warning disable CS1591

namespace HealthCare.WebService.Models
{
    public class OracleDbContext : DbContext
    {
        public OracleDbContext() : base("name=conn")
        {
            // 禁用 db migration
            Database.SetInitializer<OracleDbContext>(null);
            Database.Log = log =>
            {
                if (log?.Trim().Any() == true)
                {
                    Global.MonitorLogger.Info(log?.Trim());
                }
            };
        }

        /// <summary>
        ///     摆药请求
        /// </summary>
        public virtual DbSet<SDBZV_DISPENSE_REQ> SDBZV_DISPENSE_REQ { get; set; }

        /// <summary>
        ///     全部的摆药信息
        /// </summary>
        public virtual DbSet<SDBZV_DISPENSARY_ORDER> SDBZV_DISPENSARY_ORDER { get; set; }

        /// <summary>
        ///     手术排班信息
        /// </summary>
        public virtual DbSet<BJFC_SFRA_PAIBAN> BJFC_SFRA_PAIBAN { get; set; }

        /// <summary>
        ///     手术排班信息
        /// </summary>
        public virtual DbSet<BJTTview_sfra_operationschedule> BJTTview_sfra_operationschedule { get; set; }

        protected override void OnModelCreating(DbModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Configurations.AddFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}