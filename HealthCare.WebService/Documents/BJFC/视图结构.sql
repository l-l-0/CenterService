---------------------------------------------
-- Export file for user SSMZ               --
-- Created by SFRA on 2018-04-24, 15:57:18 --
---------------------------------------------

spool 视图结构.log

prompt
prompt Creating view SFRA_PAIBAN
prompt =========================
prompt
create or replace view ssmz.sfra_paiban as
select
       d.his_apply_no,                                      --手术唯一号
       A.PATIENT_ID,                                       --病人ID
       C.INP_NO,                                           --住院号
       C.NAME,                                             --患者姓名
       c.sex,                                              --性别
       c.id_no,                                             --身份证号
       A.bed_no,                                           --床号
       C.DATE_OF_BIRTH,                                    --出生日期

       nvl((select med_dept_dict.DEPT_NAME   from med_dept_dict where med_dept_dict.dept_code = a.dept_stayed ),a.dept_stayed ) dept_stayed_name,                             --病人所在科室
       a.dept_stayed,                            --病人所在科室编码
       A.scheduled_date_time,                              --手术申请日期及时间
       a.operating_room,                                   --手术室代码
        nvl((select med_dept_dict.DEPT_NAME   from med_dept_dict where med_dept_dict.dept_code = a.operating_room ),a.operating_room ) operating_room_name,                  --手术室名称
       A.operating_room_no,                                --手术间
       A.sequence,                                         --台次
       A.diag_before_operation,                            --术前诊断
       A.anesthesia_method,                                --麻醉方法
       b.operation ,                                       --手术名称
       A.operation_scale,                                  --手术等级
        --手术科室
       nvl((select med_dept_dict.DEPT_NAME   from med_dept_dict where med_dept_dict.dept_code = a.operating_dept ),a.operating_dept ) operating_dept_name,
       a.operating_dept,              --手术科室编码
       --手术者
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.surgeon ),a.surgeon ) surgeon_name,
       a.surgeon,                             --术者编码
       --第一手术助手
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.first_assistant ),a.first_assistant ) first_assistant_name,
       a.first_assistant,             --一助编码
       --第二手术助手
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.second_assistant ),a.second_assistant ) second_assistant_name,
       a.second_assistant,           --二助编码
       --麻醉医生
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.anesthesia_doctor ),a.anesthesia_doctor ) anesthesia_doctor_name,
       a.anesthesia_doctor,         --麻醉医生编码
       --麻醉助手
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.anesthesia_assistant ),a.anesthesia_assistant ) anesthesia_assistant_name,
       a.anesthesia_assistant,   --麻醉医生助手编码
       --第一洗手护士
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.first_operation_nurse ),a.first_operation_nurse ) first_operation_nurse_name,
       a.first_operation_nurse,  --第一洗手护士编码
       --第二洗手护士
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.second_operation_nurse ),a.second_operation_nurse ) second_operation_nurse_name,
       a.second_operation_nurse,  --第二洗手护士编码
       --第一巡回护士
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.first_supply_nurse ),a.first_supply_nurse ) first_supply_nurse_name,
       a.first_supply_nurse,       --第一巡回护士编码
       --第二巡回护
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.second_supply_nurse ),a.second_supply_nurse ) second_supply_nurse_name,
       a.second_supply_nurse,     --第二巡回护士编码
       A.notes_on_operation,                               --备注
       DECODE( A.emergency_indicator,0,'择期',1,'急诊','择期') as abc,   --急诊择期
       null START_DATE_TIME,                                    --手术开始时间
       null END_DATE_TIME,                                    --手术结束时间
       case A.anesthesia_method when '局' then '术中' else '术前' end  type

 from med_operation_schedule a , med_scheduled_operation_name b , med_pat_master_index c , med_vs_his_oper_apply_v2 d
 where a.patient_id = b.patient_id                            and
       a.visit_id   = b.visit_id                              and
       a.schedule_id= b.schedule_id                           and
       a.patient_id = c.patient_id                            and
       a.patient_id = d.med_patient_id                            and
       a.visit_id   = d.med_visit_id                              and
       a.schedule_id= d.med_schedule_id                          and
       b.operation_no = 1                                     and
       a.state = 2
union
select
       e.his_apply_no,                                      --手术唯一号
       A.patient_id,                                       --病人ID
       D.INP_NO,                                           --住院号
       D.NAME,                                             --患者姓名
       D.sex,                                              --性别
       D.id_no,
       a.bed_no,                                           --床号
       D.DATE_OF_BIRTH,                                    --出生日期

       --病人所在科室
      nvl((select med_dept_dict.DEPT_NAME   from med_dept_dict where med_dept_dict.dept_code = a.dept_stayed ),a.dept_stayed ) dept_stayed_name,

      a.dept_stayed,                             --所在科室编码
       A.Scheduled_Date_Time,                              --手术申请日期及时间

       a.operating_room,                                   --手术室编码
      nvl((select med_dept_dict.DEPT_NAME   from med_dept_dict where med_dept_dict.dept_code = a.operating_room ),a.operating_room ) operating_room_name,                  --手术室
       A.operating_room_no,                                --手术间
       A.sequence,                                         --台次
       A.diag_before_operation,                            --术前诊断
       A.anesthesia_method,                                --麻醉方法
       a.operation_name operation ,                                       --手术名称
       A.operation_scale,                                  --手术等级
       --手术科室
      nvl((select med_dept_dict.DEPT_NAME   from med_dept_dict where med_dept_dict.dept_code = a.operating_dept ),a.operating_dept ) operating_dept_name,
       a.operating_dept,               --手术科室编码
       --手术者
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.surgeon ),a.surgeon ) surgeon_name,
       a.surgeon,                             --术者编码
       --第一手术助手
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.first_assistant ),a.first_assistant ) first_assistant_name,
       a.first_assistant,             --一助编码
       --第二手术助手
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.second_assistant ),a.second_assistant ) second_assistant_name,
       a.second_assistant,           --二助编码
       --麻醉医生
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.anesthesia_doctor ),a.anesthesia_doctor ) anesthesia_doctor_name,
       a.anesthesia_doctor,         --麻醉医生编码
       --麻醉助手
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.anesthesia_assistant ),a.anesthesia_assistant ) anesthesia_assistant_name,
       a.anesthesia_assistant,   --麻醉助手编码
       --第一洗手护士
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.first_operation_nurse ),a.first_operation_nurse ) first_operation_nurse_name,
       a.first_operation_nurse,  --一洗手护士编码
       --第二洗手护士
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.second_operation_nurse ),a.second_operation_nurse ) second_operation_nurse_name,
       a.second_operation_nurse,  --二洗手护士编码
       --第一巡回护士
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.first_supply_nurse ),a.first_supply_nurse ) first_supply_nurse_name,
       a.first_supply_nurse ,        --一巡回护士编码
       --第二巡回护士
       nvl( ( select  med_his_users.USER_NAME from med_his_users where med_his_users.USER_ID = a.second_supply_nurse ),a.second_supply_nurse ) second_supply_nurse_name,
       a.second_supply_nurse,      --二巡回护士编码
       null,                                               --备注
       DECODE( A.emergency_indicator,0,'择期',1,'急诊','择期') as abc,  --急诊择期
       a.START_DATE_TIME,                                    --手术开始时间
       a.END_DATE_TIME,                                      --手术结束时间

       --DECODE( A.OPER_STATUS, 5,'术中',10,'术中',15,'术中',25,'术中',30,'术中',35,'术后',45,'复苏室') type,
       case A.anesthesia_method when '局麻' then '术中' else
       DECODE( A.OPER_STATUS,0,'术前',5,'术中',10,'术中',15,'术中',25,'术中',30,'术中',35,'术后',40,'复苏室',45,'复苏室',55,'出复苏',60,'回病房') end  type


from med_operation_master a , med_operation_name b , med_anesthesia_plan c , med_pat_master_index d , med_vs_his_oper_apply_v2 e
 where a.patient_id = b.patient_id(+)                            and
       a.visit_id   = b.visit_id(+)                              and
       a.oper_id= b.oper_id(+)                                   and
       a.patient_id = d.patient_id                            and
       a.patient_id = c.patient_id(+)                            and
       a.visit_id = c.visit_id(+)                                and
       a.oper_id = c.oper_id(+)                                  and
       a.patient_id = e.med_patient_id(+)                            and
       a.visit_id = e.med_visit_id(+)                                and
       a.oper_id = e.med_schedule_id(+)                                  and
       a.OPER_STATUS > 1                                      and
       b.operation_no(+) = 1 and A.OPER_STATUS < 70;


spool off
