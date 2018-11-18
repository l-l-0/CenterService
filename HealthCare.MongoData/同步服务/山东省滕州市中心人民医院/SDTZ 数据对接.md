# SDTZ 数据对接

>

## 部门 (Department)

``` SQL



```

| 列名            | 说明         | 数据类型   | 可空   | HIS 列名 |
| ------------- | ---------- | ------ | ---- | ------ |
| `UniqueId`    | 唯一标识       | string |   ×   |  |
| `DisplayName` | 部门、科室、病区名称 | string |   ×   |  |

## 员工 (Employee)

``` SQL



```

| 列名             | 说明     | 数据类型   | 可空   | HIS 列名 |
| -------------- | ------ | ------ | ---- | ------ |
| `JobNo`        | 工号     | string |   ×   |  |
| `DisplayName`  | 员工姓名   | string |   ×   |  |
| `JobTitle`     | 职称     | string |   ×   |  |
| `DepartmentId` | 部门唯一标识 | string |   ×   |  |

## 物品 (Goods)

``` SQL



```

| 列名                     | 说明                        | 数据类型    | 可空   | HIS 列名 |
| ---------------------- | ------------------------- | ------- | ------ | ------ |
| `UniqueId`             | 唯一标识                      | string  |   ×   |  |
| `DisplayName`          | 药品名称                      | string  |   ×   |  |
| `Specification`        | 规格                        | string  |   ×   |  |
| `Manufacturer`         | 生产厂家                      | string  |   ×   |  |
| `GoodsType`            | 药品类型（如麻、精一、精二）            | string  |   ×   |  |
| `SmallPackageUnit`     | 包装单位（如贴、盒）                | string  |   ×   |  |
| `UsedUnit`             | 最小用药单位（如支、ml）             | string  |   ×   |  |
| `Conversion`           | 包装转换率（如 1 盒 = 6 支，此时计做 6） | double  |   ×   |  |
| Price                  | 价格                        | double  |   √   |  |
| Dosage          | 剂量                        | string  |  |  |
| DosageForm      | 剂型                        | string  |  |  |

## 医嘱 (Prescription)

``` SQL



```

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



```

| 列名                     | 说明          | 数据类型     | 可空   | HIS 列名 |
| ---------------------- | ----------- | -------- | ---- | ------ |
| `UniqueId`             | 患者编号        | string   |   ×   |  |
| `DisplayName`          | 患者名称        | string   |   ×   |   |
| Nationality | 国籍 | string |   √   |  |
| Nation | 民族 | string |   √   |  |
| Gender                | 性别          | string   |   √   |  |
| Birthday              | 出生日期        | DateTime |   √   |  |
| CertificateType | 证件类型 | string |   √   |  |
| CertificateCode | 证件号码 | string |   √   |  |
| `Diagnostic`           | 诊断          | string   |   ×   |  |
| `SerialNumber`         | 门诊号         | string   | 门诊患者不为空, 住院患者为空 |  |
| `AdmittedDepartmentId` | 患者入院科室的唯一标识 | string   |   √   |  |
| `BedNo`                | 床号          | string   | 住院患者不为空 |  |
| `HospitalNumber`       | 住院号         | string   | 住院患者不为空 |  |
| `InitiationTime`       | 入院时间        | DateTime | 住院患者不为空 |  |
| `ResidedAreaId`        | 患者所属病区的唯一标识 | string   | 住院患者不为空 |  |
| `RoomId`               | 所属手术间唯一标识   | string   |   √   |  |
| MedicareNumber | 医保号、社保号 | string   |   √   |  |
| RegisterNumber | 就诊卡号 | string   |   √   |  |

## 调拨 (Allocation)

``` SQL



```

| 列名                        | 说明                    | 数据类型     | 可空   | HIS 列名 |
| ------------------------- | --------------------- | -------- | ---- | ------ |
| `UniqueId`                | 调拨唯一标识                | string   |   ×   |  |
| `ApplyId`                 | 申请号                   | string   |   ×   |  |
| `ApplyQty`                | 申请数量                  | int      |   ×   |  |
| `GoodsId`                 | 物品唯一标识                | string   |   ×   |  |
| BatchNumber               | 批号                    | string   |   √   |  |
| ExpiredDate               | 有效期                   | DateTime |   √   |  |
| `UsedUnit`                | 出库单位                  | string   |   ×   |  |
| `Mode`                    | 调拨入库, 调拨出库         | string   |   ×   |  |
| `DepartmentSourceId`      | 出库部门                  | string   |   ×   |  |
| `DepartmentDestinationId` | 接收部门                  | string   |   ×   |  |
| DeliveryNumber            | 出库序号                  | string   |   √   |  |
| DeliveryTime              | HIS方确认发出的时刻        | DateTime |   √   |  |
| `IsCancelled`             | 调拨是否被取消            | boolean  |   ×   |  |
