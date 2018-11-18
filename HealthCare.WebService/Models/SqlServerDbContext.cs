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
    public class SqlServerDbContext : DbContext
    {
        public SqlServerDbContext() : base("name=conn")
        {
            // 禁用 db migration
            Database.SetInitializer<SqlServerDbContext>(null);
            Database.Log = log =>
            {
                if (log?.Trim().Any() == true)
                {
                    Global.MonitorLogger.Info(log?.Trim());
                }
            };
        }

        public virtual DbSet<SDSLView_HaoCai_Department> SDSLView_HaoCai_Department { get; set; }
        public virtual DbSet<SDSLView_HaoCai_Employee> SDSLView_HaoCai_Employee { get; set; }
        public virtual DbSet<SDSLView_HaoCai_Goods> SDSLView_HaoCai_Goods { get; set; }
        public virtual DbSet<SDSLView_HaoCai_OperationSchedule> SDSLView_HaoCai_OperationSchedule { get; set; }
        public virtual DbSet<SDSLView_HaoCai_Patient> SDSLView_HaoCai_Patient { get; set; }
        public virtual DbSet<SDSLView_HaoCai_Room> SDSLView_HaoCai_Room { get; set; }

        protected override void OnModelCreating(DbModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Configurations.AddFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}