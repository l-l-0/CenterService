using System;
using System.Data.Entity.ModelConfiguration;

#pragma warning disable CS1591

namespace HealthCare.WebService.Models
{
    public class BJFC_SFRA_PAIBAN
    {
        public string HIS_APPLY_NO { get; set; }
        public string PATIENT_ID { get; set; }
        public string INP_NO { get; set; }
        public string NAME { get; set; }
        public string SEX { get; set; }
        public string ID_NO { get; set; }
        public string BED_NO { get; set; }
        public DateTime? DATE_OF_BIRTH { get; set; }
        public string DEPT_STAYED_NAME { get; set; }
        public string DEPT_STAYED { get; set; }
        public DateTime SCHEDULED_DATE_TIME { get; set; }
        public string OPERATING_ROOM { get; set; }
        public string OPERATING_ROOM_NAME { get; set; }
        public string OPERATING_ROOM_NO { get; set; }
        public Int32? SEQUENCE { get; set; }
        public string DIAG_BEFORE_OPERATION { get; set; }
        public string ANESTHESIA_METHOD { get; set; }
        public string OPERATION { get; set; }
        public string OPERATION_SCALE { get; set; }
        public string OPERATING_DEPT_NAME { get; set; }
        public string OPERATING_DEPT { get; set; }
        public string SURGEON_NAME { get; set; }
        public string SURGEON { get; set; }
        public string FIRST_ASSISTANT_NAME { get; set; }
        public string FIRST_ASSISTANT { get; set; }
        public string SECOND_ASSISTANT_NAME { get; set; }
        public string SECOND_ASSISTANT { get; set; }
        public string ANESTHESIA_DOCTOR_NAME { get; set; }
        public string ANESTHESIA_DOCTOR { get; set; }
        public string ANESTHESIA_ASSISTANT_NAME { get; set; }
        public string ANESTHESIA_ASSISTANT { get; set; }
        public string FIRST_OPERATION_NURSE_NAME { get; set; }
        public string FIRST_OPERATION_NURSE { get; set; }
        public string SECOND_OPERATION_NURSE_NAME { get; set; }
        public string SECOND_OPERATION_NURSE { get; set; }
        public string FIRST_SUPPLY_NURSE_NAME { get; set; }
        public string FIRST_SUPPLY_NURSE { get; set; }
        public string SECOND_SUPPLY_NURSE_NAME { get; set; }
        public string SECOND_SUPPLY_NURSE { get; set; }
        public string NOTES_ON_OPERATION { get; set; }
        public string ABC { get; set; }
        public DateTime? START_DATE_TIME { get; set; }
        public DateTime? END_DATE_TIME { get; set; }
        public string TYPE { get; set; }
    }
    public class BJFC_SFRA_PAIBANConfig : EntityTypeConfiguration<BJFC_SFRA_PAIBAN>
    {
        public BJFC_SFRA_PAIBANConfig()
        {
            ToTable("SFRA_PAIBAN", "SSMZ").HasKey(e => e.PATIENT_ID);
        }
    }
}