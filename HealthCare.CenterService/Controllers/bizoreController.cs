//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using HealthCare.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Http;

#pragma warning disable CS1591

namespace HealthCare.CenterService.Controllers
{
    public class bizcoreController : BaseController
    {
        /// <summary>
        ///     获取应用程序客户药柜配置数据
        /// </summary>
        [HttpGet]
        [ActionName("customer-cabinets")]
        public async Task<CabinetVersion> CustomerCabinetsAsync(string version = null, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var key = $"{Terminal}:{Helper.CabinetVersion}";
            var find = mongo.SystemConfigCollection.AsQueryable().FirstOrDefault(f => f.Key == key);
            if (find == null)
            {
                find = new SystemConfig { Key = key, JObject = Helper.NowStringValue, };
                await mongo.SystemConfigCollection.FindOneAndReplaceAsync<SystemConfig>(x => x.UniqueId == find.UniqueId, find, new FindOneAndReplaceOptions<SystemConfig, SystemConfig> { IsUpsert = true });
            }

            CabinetDevice[] cabinets = null;
            if (find.JObject != version)
            {
                cabinets = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).OrderBy(c => c.DisplayOrder).ToArray();
            }
            return new CabinetVersion { Version = find.JObject, Cabinets = cabinets, };
        }

        /// <summary>
        ///     获取用户指纹特征
        /// </summary>
        [HttpGet]
        [ActionName("finger-templates")]
        public async Task<FingerVersion> FingerTemplatesAsync(string version = null)
        {
            var find = mongo.SystemConfigCollection.AsQueryable().FirstOrDefault(f => f.Key == Helper.FingerVersion);
            if (find == null)
            {
                find = new SystemConfig { Key = Helper.FingerVersion, JObject = Helper.NowStringValue, };
                await mongo.SystemConfigCollection.FindOneAndReplaceAsync<SystemConfig>(x => x.UniqueId == find.UniqueId, find, new FindOneAndReplaceOptions<SystemConfig, SystemConfig> { IsUpsert = true });
            }

            FingerIdentity[] fingers = null;
            if (find.JObject != version)
            {
                var users = mongo.UserCollection.AsQueryable().Select(u => new User { LoginId = u.LoginId, Fingerprint = u.Fingerprint, Employee = u.Employee, }).ToList();
                fingers = users.Select(u => new FingerIdentity
                {
                    UserId = u.LoginId,
                    UserName = u.Employee == null ? u.LoginId : u.Employee.DisplayName,
                    Templates = (u.Fingerprint ?? new string[10]).Select(s => s ?? string.Empty).ToArray(),
                }).ToArray();
            }

            return new FingerVersion { Version = find.JObject, Fingers = fingers, };
        }

        /// <summary>
        ///     获取用户人脸特征
        /// </summary>
        [HttpGet]
        [ActionName("face-templates")]
        public async Task<FaceVersion> FaceTemplatesAsync(string version = null)
        {
            var find = mongo.SystemConfigCollection.AsQueryable().FirstOrDefault(f => f.Key == Helper.FaceVersion);
            if (find == null)
            {
                find = new SystemConfig { Key = Helper.FaceVersion, JObject = Helper.NowStringValue, };
                await mongo.SystemConfigCollection.FindOneAndReplaceAsync<SystemConfig>(x => x.UniqueId == find.UniqueId, find, new FindOneAndReplaceOptions<SystemConfig, SystemConfig> { IsUpsert = true });
            }

            FaceIdentity[] faces = null;
            if (find.JObject != version)
            {
                var users = mongo.UserCollection.AsQueryable().Select(u => new User { LoginId = u.LoginId, Face = u.Face, Employee = u.Employee, }).ToList();
                faces = users.Select(u => new FaceIdentity
                {
                    UserId = u.LoginId,
                    UserName = u.Employee == null ? u.LoginId : u.Employee.DisplayName,
                    Template = u.Face ?? "",
                }).ToArray();
            }

            return new FaceVersion { Version = find.JObject, Face = faces, };
        }

        /// <summary>
        ///     获取当前执行代码的程序集版本
        /// </summary>
        [HttpGet]
        [ActionName("get-executing-assembly-version")]
        public string GetExecutingAssemblyVersion()
        {
            var directory = "D:\\WebNg";
            DirectoryInfo folder = new DirectoryInfo(directory);
            var folders = folder.GetFiles("*.js");
            return folders.OrderByDescending(f => f.CreationTime).ToList().FirstOrDefault()?.CreationTime.ToString();
        }

        /// <summary>
        ///     打印模板
        /// </summary>
        [HttpPost]
        [ActionName("printer-template")]
        [UserAuthorize]
        public string PrinterTemplate([FromBody]PrinterPacket pkg, string file, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var fileName = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "PRINTER_TEMPLATE"), file);
            if (File.Exists(fileName))
            {
                pkg.PrintUser = ServiceStartup.GetPrimaryAuthorized(Terminal).DisplayName;
                return Sew(File.ReadAllText(fileName));
            }
            return string.Empty;

            string Sew(string template) => string.Join(Environment.NewLine, template.Split(new[] { '\r', '\n' }).Select(row =>
            {
                string line;
                if (row.StartsWith("loop:"))
                {
                    var r = row.Replace("loop:", string.Empty);
                    line = string.Join(Environment.NewLine, pkg.Goods.Select((g, index) => r.Replace("@serial", (index + 1).ToString()).Replace("@goodsName", g.DisplayName).Replace("@specification", g.Specification).Replace("@qty", g.Qty.ToString()).Replace("@usedUnit", g.UsedUnit)));
                }
                else
                {
                    line = row.Replace("@doctorName", pkg.Doctor.DisplayName).Replace("@jobNo", pkg.Doctor.JobNo)
                        .Replace("@patientName", pkg.Patient.DisplayName).Replace("@hospitalNumber", pkg.Patient.HospitalNumber).Replace("@bedNo", pkg.Patient.BedNo)
                        .Replace("@printTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Replace("@printUser", pkg.PrintUser);
                }
                return line;
            }));
        }

        /// <summary>
        ///     接受客户端上送的日志
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ActionName("receive-client-running-logs")]
        public dynamic ReceiveClientRunningLogs([FromBody] string logs, string name)
        {
            var department = DepartmentId;

            if (string.IsNullOrEmpty(department))
            {
                return new { code = -2, msg = "未知设备", };
            }

            try
            {
                var dir = $"{AppDomain.CurrentDomain.BaseDirectory}/Upload/ClientLogs/{string.Join("", department.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))}";
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var file = Path.Combine(dir, $"{name}.zip");

                var buffer = Convert.FromBase64String(logs);
                File.WriteAllBytes(file, buffer);
                return new { code = 0, msg = "ok" };
            }
            catch (Exception e)
            {
                return new { code = -1, msg = new[] { e.Message, e.InnerException?.Message } };
            }
        }

        [HttpGet]
        public int[] TsApi()
        {
            var reqs = typeof(ngController).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(ApiController)) && !t.IsAbstract)
                .Select(t =>
                {
                    var methods = t.GetMethods().Select(m =>
                    {
                        var args = m.GetParameters().Select(p =>
                        {
                            var dft = p.HasDefaultValue ? $" = {(p.DefaultValue?.ToString().ToLower() ?? "undefined")}" : "";
                            return $"{p.Name}: {TsTypeName(p.ParameterType)}{dft}";
                        }).ToArray();

                        var pams = m.GetParameters().Where(p => !p.GetCustomAttributes(typeof(FromBodyAttribute), false).Any()).Select(p =>
                        {
                            string fmt;
                            if (p.ParameterType == typeof(DateTime))
                            {
                                fmt = m.Name == nameof(ngController.SearchPrescriptionDetails) ? $"{p.Name}.format('YYYY-MM-DD HH:mm')" : $"{p.Name}.format('YYYY-MM-DD')";
                            }
                            else if (p.ParameterType != typeof(String))
                            {
                                fmt = $"{p.Name}.toString()";
                            }
                            else
                            {
                                fmt = p.Name;
                            }
                            return $"params.set('{p.Name}', {fmt});";
                        }).ToArray();

                        var body = m.GetParameters().Where(p => p.GetCustomAttributes(typeof(FromBodyAttribute), false).Any()).FirstOrDefault()?.Name ?? "undefined";

                        string rtnType;
                        if (m.ReturnType.IsGenericType)
                        {
                            var df = m.ReturnType.GetGenericTypeDefinition();
                            var tp = m.ReturnType.GenericTypeArguments[0];
                            if (df.Name == "Task`1")
                            {
                                rtnType = tp.IsGenericType ? tp.Name.Replace("`1", $"<{TsTypeName(tp.GenericTypeArguments[0])}>").Replace("ApiBack", "ApiServiceModel.ApiBackExt") : TsTypeName(tp);
                            }
                            else
                            {
                                rtnType = df == typeof(List<>) ? $"{TsTypeName(tp)}[]" : df.Name.StartsWith("Task`1") ? TsTypeName(tp) : TsTypeName(df).Replace("`1", $"<{TsTypeName(tp)}>");
                            }
                        }
                        else
                        {
                            rtnType = TsTypeName(m.ReturnType);
                        }

                        var anattrs = m.GetCustomAttributes(typeof(ActionNameAttribute), false);
                        if (anattrs.Length <= 0)
                        {
                            return null;
                        }
                        var anAttr = (ActionNameAttribute)anattrs[0];

                        var getAttr = m.GetCustomAttributes(typeof(HttpGetAttribute), false);
                        var postAttr = m.GetCustomAttributes(typeof(HttpPostAttribute), false);
                        var putAttr = m.GetCustomAttributes(typeof(HttpPutAttribute), false);
                        var deleteAttr = m.GetCustomAttributes(typeof(HttpDeleteAttribute), false);
                        var http = getAttr.Length > 0 ? "return this.http.get(url, params);"
                            : postAttr.Length > 0 ? $"return this.http.post(url, {body}, params);"
                            : putAttr.Length > 0 ? $"return this.http.put(url, {body}, params);"
                            : deleteAttr.Length > 0 ? "return this.http.delete(url, params);"
                            : null;
                        if (http == null)
                        {
                            return null;
                        }

                        return $@"    {ToCamelCase(m.Name.Replace("Async", ""))}({string.Join(", ", args)}): Observable<{rtnType}> {{
        const url = `/api/{t.Name.Replace("Controller", "")}/{anAttr.Name}`;
        const params = new URLSearchParams();
{(pams.Any() ? new string(' ', 8) : "")}{string.Join($"{Environment.NewLine}{new string(' ', 8)}", pams)}
        {http}
    }}";
                    }).Where(o => o != null).OrderBy(o => o).ToList();
                    methods.InsertRange(0, new[] { $"{Environment.NewLine}    // ------------------- {t.Name} -------------------" });
                    return (methods, methods.Count);
                }).ToArray();

            var file = $@"import {{ ApiServiceModel }} from './api.service.model';
import {{ Injectable }} from '@angular/core';
import {{ Observable }} from 'rxjs/Observable';
import {{ SHttpService }} from './s.http.service';
import {{ URLSearchParams }} from '@angular/http';
import {{
    CabinetDevice, TerminalGoods, NodeGoodsInfo, Goods, GoodsCategory, Department, Medication,
    User, Employee, Role, Allocation, Exchange, Transfer, ExchangeMode, OperationSchedule, AmpouleRecord,
    SystemConfig, moment, Kit, Evaluate, InternalAllocation, Prescription,
}} from '../commons';

@Injectable()
export class ApiService {{

    constructor(private http: SHttpService) {{ }}
{string.Join(Environment.NewLine, reqs.SelectMany(r => r.methods))}

    // ------------------- 硬件服务 -------------------
    resizeAndWatermark(data: {{ base64: string, font: number, opacity: number, width: number, height: number }}): Observable<string> {{
        const url = `http://127.0.0.1:8002/api/image/resize-and-watermark`;
        const params = new URLSearchParams();
        return this.http.post(url, data, params);
    }}
    rotate(data: {{ base64: string, degrees: number }}): Observable<string> {{
        const url = `http://127.0.0.1:8002/api/image/rotate`;
        const params = new URLSearchParams();
        return this.http.post(url, data, params);
    }}
    epsonImageForLabel(data: ApiServiceModel.PrintGoodsLabels[], type: ApiServiceModel.PrinterMode): Observable<string> {{
        const url = `http://127.0.0.1:8002/api/image/epson-image-for-label`;
        const params = new URLSearchParams();
        params.set('type', type.toString());
        return this.http.post(url, data, params);
    }}
    printSvgToImage(data: ApiServiceModel.PrintSvg): Observable<string> {{
        const url = `http://127.0.0.1:8002/api/image/print-svg-to-image`;
        const params = new URLSearchParams();
        return this.http.put(url, data, params);
    }}
    searchAppSettings(): Observable<ApiServiceModel.Printers[]> {{
        const url = `http://127.0.0.1:8002/api/image/search-appsettings`;
        return this.http.get(url);
    }}
    modifyAppSettings(data: ApiServiceModel.Printers): Observable<string> {{
        const url = `http://127.0.0.1:8002/api/image/modify-appsettings`;
        return this.http.put(url, data);
    }}
    searchInstalledPrinters(): Observable<string[]> {{
        const url = `http://127.0.0.1:8002/api/image/search-installed-printers`;
        return this.http.get(url);
    }}
}}
";
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "api.service.ts"), file, System.Text.Encoding.UTF8);
            return reqs.Select(r => r.Count).ToArray();


        }

        private string TsTypeName(Type type, string prefix = "ApiServiceModel.")
        {
            var isList = type.Name == "List`1";
            if (type.IsGenericType && type.GenericTypeArguments.Any())
            {
                type = type.GenericTypeArguments[0];
            }
            var x = type.IsPrimitive || type.Name == nameof(String) || type.Name == "String[]" ? type.Name.ToLower() : type.Name;
            x = x.Replace("Int32", "number").Replace("int32", "number").Replace("double", "number").Replace("DateTime", "moment.Moment").Replace("JObject", "Object");
            var name = type.Namespace.StartsWith("HealthCare") && type.Namespace != "HealthCare.Data" ? $"{prefix}{x}" : x;
            return isList ? $"{name}[]" : name;
        }

        private string ToCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            var firstChar = value[0];
            if (char.IsLower(firstChar))
            {
                return value;
            }
            firstChar = char.ToLowerInvariant(firstChar);
            return firstChar + value.Substring(1);
        }

        /// <summary>
        ///     把指定 IP 的柜子，库存都改为 0.   同时清除的还有条码、批号、有效期
        /// </summary>
        /// <remarks>
        ///     仅测试时使用，不能用于生产环境
        /// </remarks>
        [HttpDelete]
        public async Task<bool> ClearInventoryAsync(string terminal)
        {
            var finds = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
            foreach (var find in finds)
            {
                await ClearAsync(find, terminal?.Trim());
            }
            return true;

            async Task ClearAsync(Customer find, string flag)
            {
                for (var i = 0; i < find.Cabinets.Count; i++)
                {
                    var cabinet = find.Cabinets[i];
                    if (cabinet.Computer != flag)
                    {
                        continue;
                    }

                    for (var j = 0; j < cabinet.Drawers.Count; j++)
                    {
                        var drawer = cabinet.Drawers[j];
                        for (var k = 0; k < drawer.Boxes.Count; k++)
                        {
                            var box = drawer.Boxes[k];
                            for (var m = 0; m < box.Fills.Count; m++)
                            {
                                await mongo.CustomerCollection.UpdateOneAsync(c => c.UniqueId == find.UniqueId, Builders<Customer>.Update
                                    .Set(c => c.Cabinets[i].Drawers[j].Boxes[k].Fills[m].QtyExisted, 0)
                                    .Set(c => c.Cabinets[i].Drawers[j].Boxes[k].Fills[m].Barcodes, new string[0])
                                    .Set(c => c.Cabinets[i].Drawers[j].Boxes[k].Fills[m].BatchNumber, string.Empty)
                                    .Set(c => c.Cabinets[i].Drawers[j].Boxes[k].Fills[m].ExpiredDate, DateTime.MaxValue.Date));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     校正 Role 和 User 集合中 Menu 的格式
        /// </summary>
        [HttpPost]
        public async Task<int> AdjustMenuAsync()
        {
            // role && user
            var roles = mongo.RoleCollection.AsQueryable().ToList();
            foreach (var role in roles)
            {
                role.DefaultMenu = adjust(role.DefaultMenu);
                role.Menus = role.Menus.Select(m => adjust(m)).ToList();
                await mongo.RoleCollection.UpdateOneAsync(r => r.UniqueId == role.UniqueId, Builders<Role>.Update.Set(r => r.DefaultMenu, role.DefaultMenu).Set(r => r.Menus, role.Menus));
            }

            var users = mongo.UserCollection.AsQueryable().ToList();
            foreach (var user in users)
            {
                user.Menus = user.Menus.Select(m => adjust(m)).ToList();
                await mongo.UserCollection.UpdateOneAsync(u => u.UniqueId == user.UniqueId, Builders<User>.Update.Set(u => u.Menus, user.Menus));
            }

            return roles.Count + users.Count;

            string adjust(string menu) => string.Join("/", (menu ?? string.Empty).Split('/').Take(3));
        }

        /// <summary>
        ///     根据给药柜 (Cabinets) 设置的 IP, 更新相应的 No. 包括 药柜, 抽屉(玻璃门), 药盒(LED), 针剂
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<int> AdjustNosAsync()
        {
            int count = 0;
            var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
            foreach (var customer in customers)
            {
                for (int i = 0; i < customer.Cabinets.Count; i++)
                {
                    // 硬件   computer   no                       displayText
                    //
                    // 柜子   ip         ip:01                    页面输入
                    // 抽屉   ip         ip:01-0102               D0102, G0102
                    // 药盒   ip         ip:01-0102-0103          D0102-B0103, G0102-B0103
                    // 针剂   ip         ip:01-0102-0103-0101     1[,2,3,4,5,6]
                    var cabinet = customer.Cabinets[i];
                    cabinet.No = $"{cabinet.Computer}:{cabinet.No.Split(':')[1]}";
                    await mongo.CustomerCollection.UpdateOneAsync(c => c.UniqueId == customer.UniqueId,
                        Builders<Customer>.Update.Set(c => c.Cabinets[i].No, cabinet.No));
                    count++;

                    for (int j = 0; j < cabinet.Drawers.Count; j++)
                    {
                        var drawer = cabinet.Drawers[j];
                        drawer.Computer = cabinet.Computer;
                        drawer.No = $"{cabinet.Computer}:{drawer.No.Split(':')[1]}";

                        await mongo.CustomerCollection.UpdateOneAsync(c => c.UniqueId == customer.UniqueId,
                            Builders<Customer>.Update.Set(c => c.Cabinets[i].Drawers[j].Computer, cabinet.Computer).Set(c => c.Cabinets[i].Drawers[j].No, drawer.No));
                        count++;

                        drawer.Boxes = drawer.Boxes.OrderBy(b => b.Location.CellStart.X).GroupBy(b => b.Location.CellStart.X)
                            .SelectMany((gp, col) => gp.OrderBy(o => o.Location.CellStart.Y).Select((o, idx) =>
                            {
                                // 0123456789A
                                // D0101-B0203
                                var prefix = o.DisplayText.Substring(0, o.DisplayText.Length - 4);
                                o.DisplayText = $"{prefix}{col + 1:X2}{idx + 1:X2}";
                                return o;
                            })).ToList();

                        for (int k = 0; k < drawer.Boxes.Count; k++)
                        {
                            var box = drawer.Boxes[k];
                            box.Computer = cabinet.Computer;
                            box.No = $"{cabinet.Computer}:{box.No.Split(':')[1]}";
                            await mongo.CustomerCollection.UpdateOneAsync(c => c.UniqueId == customer.UniqueId,
                                Builders<Customer>.Update.Set(c => c.Cabinets[i].Drawers[j].Boxes[k].Computer, cabinet.Computer).Set(c => c.Cabinets[i].Drawers[j].Boxes[k].No, box.No));
                            count++;

                            for (int m = 0; m < (box.Injections?.Count ?? 0); m++)
                            {
                                var inj = box.Injections[m];
                                inj.Computer = cabinet.Computer;
                                await mongo.CustomerCollection.UpdateOneAsync(c => c.UniqueId == customer.UniqueId,
                                    Builders<Customer>.Update.Set(c => c.Cabinets[i].Drawers[j].Boxes[k].Injections[m].Computer, cabinet.Computer).Set(c => c.Cabinets[i].Drawers[j].Boxes[k].Injections[m].No, inj.No));
                                count++;
                            }
                        }
                    }
                }
            }
            return count;
        }

        [HttpGet]
        public int TsModel()
        {
            var types = GetType().Assembly.GetTypes().Where(t => !t.Name.Contains("<") && !t.Name.Contains("Controller") && t.Namespace == "HealthCare.CenterService.Controllers" && t.GetProperties().Any()).ToArray();
            var ts = types.Select(t =>
            {
                var ps = t.GetProperties().Select(p => $"\t{ToCamelCase(p.Name)}: {TsTypeName(p.PropertyType, string.Empty)};").ToArray();
                var klass = t.IsEnum ? "enum" : t.IsGenericType ? $"{t.Name.Replace("`1", "")}<T>" : t.Name;
                return $@"  export {(t.IsEnum ? "enum" : "class")} {klass} {{
{string.Join(Environment.NewLine, ps)}
    }}";
            });

            var file = $@"import {{ Goods,Department, ActionJournal, ExchangeMode, BoxMode, CabinetDevice, Menu, Medication, User, Patient, Hospitalization }} from '../commons/models';
import {{ LocatePriority }} from 'app/pages/shared';

export namespace ApiServiceModel {{
{string.Join(Environment.NewLine, ts)}
}}";

            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "api.service.model.ts"), file, System.Text.Encoding.UTF8);

            return 0;
        }

        [HttpGet]
        public dynamic UpgradePrescription()
        {
            var meds = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && m.Mode == ExchangeMode.Medication).ToList();
            var ids = meds.SelectMany(m => new[] { m.CheckInId, }.Concat(m.CheckOutIds ?? new string[0])).Where(x => !string.IsNullOrEmpty(x)).ToArray();
            var inOuts = mongo.MedicationCollection.AsQueryable().Where(m => ids.Contains(m.UniqueId)).ToList();
            var pres = meds.Select(m =>
            {
                m.Doctor = m.Doctor ?? mongo.EmployeeCollection.AsQueryable().FirstOrDefault(e => e.UniqueId == m.DoctorId);
                m.Patient = m.Patient ?? mongo.PatientCollection.AsQueryable().FirstOrDefault(e => e.UniqueId == m.PatientId);
                m.Goods = m.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(e => e.UniqueId == m.GoodsId);
                var lq = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && p.UniqueId != m.UniqueId && p.Mode == ExchangeMode.CheckOut && p.CreatedTime > m.CreatedTime
                    && p.DoctorId == m.DoctorId && p.PatientId == m.PatientId && p.GoodsId == m.GoodsId && p.Qty == m.Qty);
                return new Prescription
                {
                    UniqueId = m.UniqueId,
                    DoctorId = m.DoctorId,
                    Doctor = m.Doctor,
                    PatientId = m.PatientId,
                    Patient = m.Patient,
                    GoodsId = m.GoodsId,
                    Goods = m.Goods,
                    BatchNumber = m.BatchNumber,
                    ExpiredDate = m.ExpiredDate,
                    Mode = ExchangeMode.CheckOut,
                    Qty = m.Qty,
                    QtyActual = m.QtyActual,

                    TimeFilter = m.TimeFilter,
                    IssuedTime = m.CreatedTime,
                    FinishTime = m.CreatedTime,

                    DepartmentSourceId = null,
                    DepartmentSource = null,
                    DepartmentDestination = null,
                    DepartmentDestinationId = null,

                    Plans = m.Plans,
                    PrintRecords = m.PrintRecords,
                    FinishedAmpoule = m.FinishedAmpoule,
                    AssignAmpouleRecords = m.AssignAmpouleRecords,
                    OperationScheduleId = m.OperationScheduleId,
                    ExchangeBarcode = m.ExchangeBarcode,
                    GoodsBarcodes = m.GoodsBarcodes,
                    CustomerId = m.CustomerId,
                    Computer = m.Computer,
                    CreatedTime = m.CreatedTime,
                    RecordType = m.RecordType,
                    FlowState = "SFRA 已执行",
                    FlowRemark = $"已执行{m.RecordType}医嘱",

                    TrackNumber = null,
                    PrintNumber = null,
                    IsSynchronized = lq.Any(),
                    RetriesNumber = lq.Count(),
                    IsDisabled = lq.Any(),
                    IsAddition = lq.Any(),

                    #region USELESS

                    DisplayName = null,
                    DisplayOrder = -1,
                    Description = null,
                    UsedDosage = null,
                    UsedFrequency = null,
                    UsedPurpose = null,
                    AgentId = null,
                    Agent = null,
                    ChargeOffId = null,
                    Deposit = null,
                    IsWhole = false,
                    FeeCollectorId = null,
                    FeeTime = null,
                    FeeType = null,
                    DispensingId = null,
                    DispensingNumber = null,
                    DispensingTime = null,

                    #endregion
                };
            }).ToArray();
            return pres;
        }
    }
}