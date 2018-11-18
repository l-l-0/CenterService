# SDBZ 数据对接

> Oracle 数据库

1. 科室 dept_dict
2. 患者 pat_master_index
3. 在院患者pats_in_hospital
4. 员工 staff_dict
5. 正式库: 192.168.8.114
6. drug_stock 药品库存表
7. outp_mr 门诊诊断表
8. diagnosis 住院诊断表
9. ORDERS 医嘱表
10. ORDERS_COSTS 医嘱计价项目表
11. drug_import_master 药品入库主记录
12. drug_import_detail 药品入库明细记录
13. drug_export_master 药品出库主记录
14. grant select on drug_export_detail  药品出库明细记录

## 部门 (Department)

``` SQL

SELECT DEPT_CODE, DEPT_NAME
FROM dept_dict

```

| 列名            | 说明         | 数据类型   | 可空   | HIS 列名 |
| ------------- | ---------- | ------ | ---- | ------ |
| `UniqueId`    | 唯一标识       | string |   ×   | DEPT_CODE |
| `DisplayName` | 部门、科室、病区名称 | string |   ×   | DEPT_NAME |

## 员工 (Employee)

``` SQL

SELECT EMP_NO, NAME, TITLE, DEPT_CODE
FROM staff_dict

```

| 列名             | 说明     | 数据类型   | 可空   | HIS 列名 |
| -------------- | ------ | ------ | ---- | ------ |
| `JobNo`        | 工号     | string |   ×   | EMP_NO |
| `DisplayName`  | 员工姓名   | string |   ×   | NAME |
| `JobTitle`     | 职称     | string |   ×   | TITLE |
| `DepartmentId` | 部门唯一标识 | string |   ×   | DEPT_CODE |

## 物品 (Goods)

``` SQL

SELECT a.DRUG_CODE || '|' || a.DRUG_SPEC || b.firm_id as DRUG_CODE, a.DRUG_NAME, a.DRUG_SPEC, b.firm_id, a.toxi_property, b.package_spec, a.units, b.sub_package_1, b.purchase_price
FROM drug_dict a, drug_stock b
where b.drug_code = a.drug_code

```

| 列名                     | 说明                        | 数据类型    | 可空   | HIS 列名 |
| ---------------------- | ------------------------- | ------- | ------ | ------ |
| `UniqueId`             | 唯一标识                      | string  |   ×   | DRUG_CODE |
| `DisplayName`          | 药品名称                      | string  |   ×   | DRUG_NAME |
| `Specification`        | 规格                        | string  |   ×   | DRUG_SPEC |
| `Manufacturer`         | 生产厂家                      | string  |   ×   | firm_id |
| `GoodsType`            | 药品类型（如麻、精一、精二）            | string  |   ×   | toxi_property |
| `SmallPackageUnit`     | 包装单位（如贴、盒）                | string  |   ×   | package_spec |
| `UsedUnit`             | 最小用药单位（如支、ml）             | string  |   ×   | units |
| `Conversion`           | 包装转换率（如 1 盒 = 6 支，此时计做 6） | double  |   ×   | sub_package_1 |
| Price                  | 价格                        | double  |   √   | purchase_price |
| Dosage          | 剂量                        | string  |  |  |
| DosageForm      | 剂型                        | string  |  |  |

## 医嘱 (Prescription)

> 医嘱  写在 OracleService 中

- 医嘱就是专门针对住院的，分临时和长期，都是需要护士校对，摆药，然后规定的按执行时间执行的。
- 处方就是医生直接在医生站下达处方，药房可以直接看到处方上的药品信息，患者就可以取药。就是处方不需要经过护士校对这一过程，执行时间什么的也没规定。

| 列名                        | 说明                    | 数据类型     | 可空   | HIS 列名 |
| ------------------------- | --------------------- | -------- | ---- | ------ |
| `UniqueId`                | 医嘱唯一标识                | string   |   ×   |  |
| TrackNumber               | 单号，处方号                | string   |   √   |  |
| `Description`             | 医嘱类型，如长期医嘱、临时医嘱，出院带药  | string   |   ×   |  |
| UsedFrequency             | 用药频次                   | string |   √   |  |
| UsedPurpose               | 途径, 用法                 | string |   √   |  |
| `IssuedTime`              | 开嘱时间                  | DateTime |   ×   |  |
| `DoctorId`                | 开嘱医生                  | string   |   ×   |  |
| `PatientId`               | 患者                    | string   |   ×   |  |
| `GoodsId`                 | 药品                    | string   |   ×   |  |
| BatchNumber               | 批号                    | string   |   √   |  |
| ExpiredDate               | 有效期                   | DateTime |   √   |  |
| `Qty`                     | 药品数量                  | double   |   ×   |  |
| `UsedUnit`                | 使用单位                  | string   |   ×   |  |
| `Mode`                    | 取药医嘱, 退药医嘱         | string   |   ×   |  |
| DispensingId              | 发药确认人                | string   |   √   |  |
| DispensingTime            | 发药确认时间              | string   |   √   |  |
| `DepartmentSourceId`      | 开单科室                  | string   |   ×   |  |
| `DepartmentDestinationId` | 发药科室                  | string   |   ×   |  |

## 患者 (Patient)

``` SQL

-- 住院患者, 只要在院的
SELECT a.patient_id, b.name, b.citizenship, b.nation, b.sex, b.date_of_birth, a.diagnosis, b.inp_no || '|' || a.visit_id as inp_no, null as visit_no, a.dept_code, a.bed_no, a.admission_date_time, '住院' as patType
FROM pats_in_hospital A JOIN pat_master_index B on A.PATIENT_ID = B.PATIENT_ID

union

-- 门诊患者, 只要近一周的
select a.patient_id, b.name, b.citizenship, b.nation, b.sex, b.date_of_birth, c.Diag_Desc AS diagnosis, null as inp_no, c.visit_no || '|' || TO_CHAR(c.visit_date, 'YYYY-MM-DD') as visit_no, null as dept_code, null as bed_no, null as admission_date_time, '门诊' as patType
from pat_master_index B,outp_mr C,
(select patient_id,name,max(visit_date) as visit_date,max(visit_no)  as visit_no from clinic_master where visit_date>sysdate-7 group by patient_id,name) a
where a.visit_date = c.visit_date
and a.visit_no = c.visit_no
and a.patient_id = b.patient_id
and b.patient_id = c.patient_id
and a.visit_date >= sysdate - 7

```

| 列名                     | 说明          | 数据类型     | 可空   | HIS 列名 |
| ---------------------- | ----------- | -------- | ---- | ------ |
| `UniqueId`             | 患者编号        | string   |   ×   | PATIENT_ID |
| `DisplayName`          | 患者名称        | string   |   ×   | NAME  |
| Nationality | 国籍 | string |   √   | CITIZENSHIP |
| Nation | 民族 | string |   √   | NATION |
| Gender                | 性别          | string   |   √   | SEX |
| Birthday              | 出生日期        | DateTime |   √   | DATE_OF_BIRTH |
| CertificateType | 证件类型 | string |   √   |  |
| CertificateCode | 证件号码 | string |   √   |  |
| `Diagnostic`           | 诊断          | string   |   ×   | DIAGNOSIS |
| `SerialNumber`         | 门诊号         | string   | 门诊患者不为空, 住院患者为空 | visit_no |
| `AdmittedDepartmentId` | 患者入院科室的唯一标识 | string   |   √   |  |
| `BedNo`                | 床号          | string   | 住院患者不为空 | BED_NO |
| `HospitalNumber`       | 住院号         | string   | 住院患者不为空 | inp_no |
| `InitiationTime`       | 入院时间        | DateTime | 住院患者不为空 | ADMISSION_DATE_TIME |
| `ResidedAreaId`        | 患者所属病区的唯一标识 | string   | 住院患者不为空 | DEPT_CODE |
| `RoomId`               | 所属手术间唯一标识   | string   |   √   |  |
| MedicareNumber | 医保号、社保号 | string   |   √   |  |
| RegisterNumber | 就诊卡号 | string   |   √   |  |

## 调拨 (Allocation)

``` SQL

--入库
SELECT A.DOCUMENT_NO||'@'||b.ITEM_NO as UniqueId, 
A.DOCUMENT_NO,B.QUANTITY,B.DRUG_CODE || '|' || B.Drug_Spec || B.firm_id as DRUG_CODE, B.BATCH_NO,B.EXPIRE_DATE,B.PACKAGE_UNITS, '调拨入库' allocMode, -- A.IMPORT_CLASS,
(select dept_code from dept_dict where dept_name = a.supplier AND ROWNUM =1) as SourceId, a.storage as DestinationId, -- supplier 出库部门，storage 接收部门
a.import_date as DeliveryTime, a.account_indicator --  0是未记账，1是记账，2是作废
FROM DRUG_IMPORT_MASTER A,DRUG_IMPORT_DETAIL B
WHERE A.DOCUMENT_NO = B.DOCUMENT_NO
and a.IMPORT_date >= TO_DATE('2018-01-01','YYYY-MM-DD')
/*AND A.STORAGE IN ('020710','020602')*/

UNION ALL

-- 出库
SELECT C.DOCUMENT_NO||'@'||D.ITEM_NO as UniqueId, 
C.DOCUMENT_NO,D.QUANTITY,D.DRUG_CODE || '|' || D.Drug_Spec || D.firm_id as DRUG_CODE, D.BATCH_NO,D.EXPIRE_DATE,D.PACKAGE_UNITS,'调拨出库' allocMode, -- C.EXPORT_CLASS,
C.STORAGE as SourceId,  (select dept_code from dept_dict where dept_name = C.RECEIVER AND ROWNUM =1) as  DestinationId, -- STORAGE 出库部门，RECEIVER 接收部门
C.EXPORT_DATE as DeliveryTime, C.ACCOUNT_INDICATOR
FROM DRUG_EXPORT_MASTER C,DRUG_EXPORT_DETAIL D
WHERE C.DOCUMENT_NO = D.DOCUMENT_NO
AND C.EXPORT_DATE >= TO_DATE('2018-01-01','YYYY-MM-DD')
/*AND C.STORAGE IN ('020710','020602')*/

```

| 列名                        | 说明                    | 数据类型     | 可空   | HIS 列名 |
| ------------------------- | --------------------- | -------- | ---- | ------ |
| `UniqueId`                | 调拨唯一标识                | string   |   ×   | UniqueId |
| `ApplyId`                 | 申请号                   | string   |   ×   | DOCUMENT_NO |
| `ApplyQty`                | 申请数量                  | int      |   ×   | QUANTITY |
| `GoodsId`                 | 物品唯一标识                | string   |   ×   | DRUG_CODE |
| BatchNumber               | 批号                    | string   |   √   | BATCH_NO |
| ExpiredDate               | 有效期                   | DateTime |   √   | EXPIRE_DATE |
| `UsedUnit`                | 出库单位                  | string   |   ×   | PACKAGE_UNITS |
| `Mode`                    | 调拨入库, 调拨出库         | string   |   ×   | allocMode 判断 checkIn 或 checkOut |
| `DepartmentSourceId`      | 出库部门                  | string   |   ×   | supplier |
| `DepartmentDestinationId` | 接收部门                  | string   |   ×   | storage |
| DeliveryNumber            | 出库序号                  | string   |   √   |  |
| DeliveryTime              | HIS方确认发出的时刻        | DateTime |   √   | import_date |
| `IsCancelled`             | 调拨是否被取消            | boolean  |   ×   | account_indicator != 1 |
