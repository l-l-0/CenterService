using System;
using System.Data.Entity.ModelConfiguration;

#pragma warning disable CS1591

namespace HealthCare.WebService.Models
{
    public class BJTTview_sfra_operationschedule
    {
        public string UNIQUEID { get; set; }
        public string PATIENTID { get; set; }
        public DateTime APPLYTIME { get; set; }
        public string OPERATIONTYPE { get; set; }
        public string IDENTITYBARCODE { get; set; }
    }

    public class BJTTview_sfra_operationscheduleConfig : EntityTypeConfiguration<BJTTview_sfra_operationschedule>
    {
        public BJTTview_sfra_operationscheduleConfig()
        {
            ToTable("VIEW_SFRA_OPERATIONSCHEDULE", "TTHIS").HasKey(e => e.UNIQUEID);
        }
    }
}