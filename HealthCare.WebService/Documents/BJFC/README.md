# 首都医科大学附属北京妇产医院

## 与 HIS 对接

附件 all 为今天 HIS 给咱们推送的数据（员工、药品、材料【耗材+收费项目】、科室），这些信息 HIS 会每天定时推送给咱们，咱们给他们提供一个 webservice 接口地址，之前测试给提供的接口地址：http://192.168.124.125/FuChanInterface.asmx
附件 AddOrdInfo.wsdl 是 HIS 给咱们的接口地址，从浏览器里面到下面的 wsdl 文件。his 正式库接口地址：http://192.168.0.71/csp/i-operation/DHC.ZNYG.BS.AddOrderInfo.CLS
病人接口，HIS 给咱们提供的接口地址，获取病人基本信息。
上传医嘱接口，HIS 给咱们提供的接口地址，传入参数详情附件接口文档，HIS 接口会给我们返回上传的状态。
    
## 与手麻对接

读取手术排班数据，厂家数据库 oracle , 手术排班字段详情附件 (视图结构.sql)
