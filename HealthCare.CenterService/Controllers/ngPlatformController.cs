//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using HealthCare.MongoData;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

#pragma warning disable CS1591

// setting-cabinet-ownership
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class CustomerProfile : IdName
        {
            public CabinetProfile[] Cabinets { get; set; }
            public double QtyExisted { get; set; }
        }

        public class CabinetProfile
        {
            public string No { get; set; }
            public string DisplayText { get; set; }
            public string DepartmentId { get; set; }
            public string Department { get; set; }
            public string Type { get; set; }
            public string Computer { get; set; }
            public double QtyExisted { get; set; }
            public string ParentId { get; set; }
            public bool SingleAuth { get; set; }
            public bool NonClinical { get; set; }
            public bool IsPrimary { get; set; }
        }

        /// <summary>
        ///     搜索所有客户的简略信息
        /// </summary>
        [HttpGet]
        [ActionName("search-all-customers-profile")]
        public CustomerProfile[] SearchAllCustomersProfile()
        {
            var customer = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
            var department = customer.SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).Select(c => c.DepartmentId).FirstOrDefault();

            var departIds = customer.SelectMany(c => c.Cabinets).Concat(customer.SelectMany(c => c.OutOfCabinets)).Select(c => c.DepartmentId).Distinct().ToList();
            var departs = mongo.DepartmentCollection.AsQueryable().Where(d => departIds.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).ToList();
            var cfgKeys = customer.SelectMany(c => c.Cabinets).Select(c => c.Computer).Distinct().SelectMany(c => new[] { $"{c}:SingleAuth", $"{c}:NonClinical", }).ToArray();
            var configs = mongo.SystemConfigCollection.AsQueryable().Where(s => cfgKeys.Contains(s.Key)).ToList();
            return customer.Select(c =>
            {
                var xs = c.Cabinets.Concat(c.OutOfCabinets).Select(o => new
                {
                    o.No,
                    o.DisplayText,
                    o.DepartmentId,
                    Department = departs.FirstOrDefault(f => f.UniqueId == o.DepartmentId),
                    o.IsControlled,
                    Computer = o.IsControlled ? o.Computer : null,
                    QtyExisted = o.Drawers.SelectMany(d => d.Boxes).SelectMany(b => b.Fills).Sum(f => f.QtyExisted),
                    o.ParentId,
                    o.IsPrimary,
                }).OrderBy(r => r.Department?.DisplayOrder ?? c.Cabinets.Count + 1).ThenBy(r => r.No).ToArray();
                var cs = xs.Where(o => o.IsControlled);
                var vs = xs.Where(o => !o.IsControlled);
                var cabs = cs.Where(o => o.Computer == Terminal).Concat(vs.Where(o => o.DepartmentId == department)).Concat(cs.Where(o => o.Computer != Terminal)).Concat(vs.Where(o => o.DepartmentId != department))
                    .Select(rv => new CabinetProfile
                    {
                        No = rv.No,
                        DisplayText = rv.DisplayText,
                        DepartmentId = rv.DepartmentId,
                        Department = rv.Department?.DisplayName,
                        Type = rv.IsControlled ? "cabinet" : "virtual",
                        Computer = rv.Computer,
                        QtyExisted = rv.QtyExisted,
                        ParentId = rv.ParentId,
                        SingleAuth = (configs.Where(o => o.Key == $"{rv.Computer}:SingleAuth").Select(o => o.JObject).FirstOrDefault() ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase),
                        NonClinical = (configs.Where(o => o.Key == $"{rv.Computer}:NonClinical").Select(o => o.JObject).FirstOrDefault() ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase),
                        IsPrimary = rv.IsPrimary,
                    }).ToArray();

                return new CustomerProfile
                {
                    UniqueId = c.UniqueId,
                    DisplayName = c.DisplayName,
                    Cabinets = cabs,
                    QtyExisted = cabs.Sum(o => o.QtyExisted),
                };
            }).ToArray();
        }

        public class DepartmentProfile : IdName
        {
            public string Computer { get; set; }
            public string Code { get; set; }
        }

        /// <summary>
        ///     查询有 IP 地址的部门、病区、科室、手术室 或 所有
        /// </summary>
        /// <param name="mode">Self 或 All</param>
        /// <param name="customer"></param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-departments-own-computer")]
        public DepartmentProfile[] SearchDepartmentsOwnComputer(string mode, string customer = null, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;

            var kvps = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && (customer == null || c.UniqueId == customer)).SelectMany(c => c.Cabinets).ToList().Select(c => (c.DepartmentId, c.Computer)).Distinct().ToList();
            var departIds = kvps.Select(d => d.DepartmentId).Distinct().ToList();
            var dpts = mongo.DepartmentCollection.AsQueryable().Where(d => mode == "All" || departIds.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).ToList()
                .Select(d => new DepartmentProfile
                {
                    UniqueId = d.UniqueId,
                    DisplayName = d.DisplayName,
                    Code = d.Code,
                    Computer = kvps.Where(o => o.DepartmentId == d.UniqueId).Select(o => o.Computer).FirstOrDefault(),
                }).ToList();
            return dpts.Where(d => d.UniqueId == department).Concat(dpts.Where(d => d.UniqueId != department)).ToArray();
        }

        /// <summary>
        ///     查询所有的有设备的手术室
        /// </summary>
        /// <param name="mode">Self 或 All</param>
        /// <param name="customer"></param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-all-departments-own-computer")]
        public DepartmentProfile[] SearchAllDepartmentsOwnComputer(string mode, string customer = null, string terminal = null)
        {
            var departIds = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && (customer == null || c.UniqueId == customer)).SelectMany(c => c.Cabinets).ToList().Select(c => (c.DepartmentId)).Distinct().ToList();
            var dpts = mongo.DepartmentCollection.AsQueryable().Where(d => mode == "All" || departIds.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).ToList()
               .Select(d => new DepartmentProfile
               {
                   UniqueId = d.UniqueId,
                   DisplayName = d.DisplayName,
               }).ToList();
            return dpts.ToArray();
        }

        public class RoomProfile : DepartmentProfile
        {
            public bool InSurgery { get; set; }
        }

        /// <summary>
        /// 查询所有手术室并返回是否有手术正在进行
        /// </summary>
        /// <param name="apply"></param>
        /// <param name="customer"></param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-all-rooms-own-computer")]
        public RoomProfile[] SearchAllRoomsOwnComputer(DateTime apply, string customer = null, string terminal = null)
        {
            var departIds = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && (customer == null || c.UniqueId == customer)).SelectMany(c => c.Cabinets).ToList().Select(c => (c.DepartmentId)).Distinct().ToList();

            var opers = mongo.OperationScheduleCollection.AsQueryable().Where(o => departIds.Contains(o.RoomId) && o.ApplyTime == apply).Where(s => s.ExecutionEndTime == null).Select(s => s.UniqueId).ToList();
            var roomIds = mongo.MedicationCollection.AsQueryable().Where(m => m.FinishTime != null && opers.Contains(m.OperationScheduleId)).Select(s => s.OperationSchedule.RoomId).ToList();
            var dpts = mongo.DepartmentCollection.AsQueryable().Where(d => departIds.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).ToList()
                .Select(d => new RoomProfile
                {
                    UniqueId = d.UniqueId,
                    DisplayName = d.DisplayName,
                    Code = d.Code,
                    InSurgery = roomIds.Contains(d.UniqueId),
                }).ToList();
            return dpts.ToArray();
        }

        /// <summary>
        ///     创建或更新 Customer 简单属性信息
        /// </summary>
        [HttpPut]
        [ActionName("modify-customer-core")]
        public async Task<string> ModifyCustomerCoreAsync(string customer, string display, string parent = null, bool disabled = false)
        {
            var find = mongo.CustomerCollection.AsQueryable().Where(c => c.UniqueId == customer).FirstOrDefault() ?? new Customer
            {
                UniqueId = customer,
                DisplayOrder = mongo.CustomerCollection.AsQueryable().Count() + 1,
            };
            find.DisplayName = display;
            find.ParentId = parent;
            find.IsDisabled = disabled;
            await mongo.CustomerCollection.FindOneAndReplaceAsync<Customer>(x => x.UniqueId == find.UniqueId, find, new FindOneAndReplaceOptions<Customer, Customer> { IsUpsert = true });
            return find.UniqueId;
        }

        /// <summary>
        ///     删除 customer
        /// </summary>
        [HttpDelete]
        [ActionName("remove-customer")]
        public async Task<bool> RemoveCustomerAsync(string customer)
        {
            var target = mongo.CustomerCollection.AsQueryable().Where(c => c.UniqueId == customer).FirstOrDefault();
            if (target == null)
            {
                // 未找到和删除等效
                return true;
            }

            var fills = target.Cabinets.Concat(target.OutOfCabinets).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills);
            if (fills.Any(f => f.QtyExisted > 0))
            {
                // 有现存量不允许删除
                return false;
            }

            await mongo.CustomerCollection.DeleteOneAsync(x => x.UniqueId == customer);
            return true;
        }

        /// <summary>
        ///     新增修改智能柜基本信息
        /// </summary>
        [HttpPut]
        [ActionName("modify-cabinet-core")]
        public async Task<bool> ModifyCabinetCoreAsync(string customer, string previous, string current, string department, string display)
        {
            if (string.IsNullOrEmpty(previous))
            {
                var cabinet = new CabinetDevice
                {
                    Computer = current.Split(':')[0],
                    DepartmentId = department,
                    DisplayOrder = int.Parse(current.Split(':')[1]),
                    DisplayText = display,
                    IsControlled = true,
                    IsPrimary = int.Parse(current.Split(':')[1]) == 1,
                    No = current,
                    NodeType = "Cabinet",
                    OwnerCode = customer,
                };
                await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == customer, Builders<Customer>.Update.Push(x => x.Cabinets, cabinet));
            }
            else
            {
                var cabinets = mongo.CustomerCollection.AsQueryable().Where(c => c.UniqueId == customer).SelectMany(c => c.Cabinets).ToList();
                var index = cabinets.FindIndex(x => x.No == previous);
                if (cabinets[index].IsPrimary && previous.Split(':')[0] != current.Split(':')[0])
                {
                    // 修改了主柜的 IP 地址，则修改配置文件
                    await ModifyComputerAsync(previous.Split(':')[0], current.Split(':')[0]);
                }

                ModifyCabinetIp(cabinets[index], current.Split(':')[0], int.Parse(current.Split(':')[1]));
                cabinets[index].DepartmentId = department;
                cabinets[index].DisplayOrder = int.Parse(current.Split(':')[1]);
                cabinets[index].DisplayText = display;
                cabinets[index].IsPrimary = int.Parse(current.Split(':')[1]) == 1;
                await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == customer, Builders<Customer>.Update.Set(x => x.Cabinets[index], cabinets[index]));

                IssuedConfig(current.Split(':')[0]);
            }
            return true;

            //void Illlegal(string old, string now)
            //{
            //    mongo.AmpouleCollection.UpdateMany(o => o.Computer == old, Builders<Ampoule>.Update.Set(o => o.Computer, now));
            //    mongo.DestoryCollection.UpdateMany(o => o.Computer == old, Builders<Destory>.Update.Set(o => o.Computer, now));
            //    mongo.ExchangeCollection.UpdateMany(o => o.Computer == old, Builders<Exchange>.Update.Set(o => o.Computer, now));
            //    mongo.AllocationCollection.UpdateMany(o => o.Computer == old, Builders<Allocation>.Update.Set(o => o.Computer, now));
            //    mongo.MedicationCollection.UpdateMany(o => o.Computer == old, Builders<Medication>.Update.Set(o => o.Computer, now));
            //    mongo.PrescriptionCollection.UpdateMany(o => o.Computer == old, Builders<Prescription>.Update.Set(o => o.Computer, now));
            //    mongo.AccessJournalCollection.UpdateMany(o => o.Computer == old, Builders<AccessJournal>.Update.Set(o => o.Computer, now));
            //    mongo.ActionJournalCollection.UpdateMany(o => o.Computer == old, Builders<ActionJournal>.Update.Set(o => o.Computer, now));
            //    mongo.StorageJournalCollection.UpdateMany(o => o.Computer == old, Builders<StorageJournal>.Update.Set(o => o.Computer, now));
            //}

            async Task ModifyComputerAsync(string old, string now)
            {
                // 修改 SystemConfig
                var cfgs = mongo.SystemConfigCollection.AsQueryable().Where(x => x.Key.StartsWith(old)).ToList();
                foreach (var item in cfgs)
                {
                    var key = item.Key.Replace(old, now);
                    await mongo.SystemConfigCollection.UpdateOneAsync(x => x.UniqueId == item.UniqueId, Builders<SystemConfig>.Update.Set(x => x.Key, key));
                }

                // 修改 TerminalGoods 的 IP
                await mongo.TerminalGoodsCollection.UpdateManyAsync(x => x.Computer == old, Builders<TerminalGoods>.Update.Set(x => x.Computer, now));

                // 修改 角色中的硬件权限
                var roles = mongo.RoleCollection.AsQueryable().Select(r => new { r.UniqueId, r.AvailableStorages, }).ToList();
                foreach (var role in roles)
                {
                    var availableStorages = role.AvailableStorages.Select(a => a.Replace(old, now)).Distinct().ToList();
                    await mongo.RoleCollection.UpdateOneAsync(x => x.UniqueId == role.UniqueId, Builders<Role>.Update.Set(x => x.AvailableStorages, availableStorages));
                }

                //  修改 用户中的硬件权限
                var users = mongo.UserCollection.AsQueryable().Select(r => new { r.UniqueId, r.AvailableStorages, }).ToList();
                foreach (var user in users)
                {
                    var availableStorages = user.AvailableStorages.Select(a => a.Replace(old, now)).Distinct().ToList();
                    await mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == user.UniqueId, Builders<User>.Update.Set(x => x.AvailableStorages, availableStorages));
                }
            }

            void ModifyCabinetIp(CabinetDevice cabinet, string ip, int serial)
            {
                cabinet.No = $"{ip}:{serial:X2}";
                cabinet.Computer = ip;
                foreach (var drawer in cabinet.Drawers)
                {
                    drawer.No = drawer.No.Replace(drawer.Computer, ip);
                    drawer.Computer = ip;
                    foreach (var box in drawer.Boxes)
                    {
                        box.No = box.No.Replace(box.Computer, ip);
                        box.Computer = ip;
                        foreach (var injection in box.Injections ?? new List<InjectionDevice>())
                        {
                            injection.No = injection.No.Replace(injection.Computer, ip);
                            injection.Computer = ip;
                        }
                    }
                }
            }

            void IssuedConfig(string currentIp)
            {
                Task.Factory.StartNew(() =>
                {
                    // IP 修改后，发送药柜配置到客户端
                    var key = $"{currentIp}:{Helper.CabinetVersion}";
                    var find = mongo.SystemConfigCollection.AsQueryable().FirstOrDefault(c => c.Key == key) ?? new SystemConfig();
                    find.JObject = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-ffffff");
                    mongo.SystemConfigCollection.FindOneAndReplace<SystemConfig>(x => x.UniqueId == find.UniqueId, find, new FindOneAndReplaceOptions<SystemConfig, SystemConfig> { IsUpsert = true });

                    var url = $"http://{currentIp}:8002/api/bizcore/customer-cabinets?version={find.JObject}";
                    try
                    {
                        var body = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == currentIp).ToArray();
                        var b = new HttpRequest().Put<bool>(url, body);
                        Global.GlobalLogger.Info($"issued {url} => {b}");
                    }
                    catch (Exception ex)
                    {
                        Global.GlobalLogger.Error(url, ex);
                    }
                });
            }
        }

        /// <summary>
        ///     删除药柜    有现存量，不允许删除
        /// </summary>
        [HttpDelete]
        [ActionName("remove-cabinet")]
        public async Task<bool> RemoveCabinetAsync(string customer, string cabinet)
        {
            var find = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).FirstOrDefault();
            var index = find?.Cabinets.FindIndex(o => o.No == cabinet);
            if (index >= 0)
            {
                var f1 = find.Cabinets[index.Value];
                if (f1.Drawers.SelectMany(d => d.Boxes).SelectMany(b => b.Fills).Any(f => f.QtyExisted > 0))
                {
                    return false;
                }

                var number = Convert.ToInt32(f1.No.Split(':')[1], 16);
                for (int i = 0; i < find.Cabinets.Count; i++)
                {
                    var one = find.Cabinets[i];
                    var serial = Convert.ToInt32(one.No.Split(':')[1], 16);
                    if (one.Computer == f1.Computer && serial > number)
                    {
                        one.No = $"{one.Computer}:{serial - 1:X2}";
                        one.DisplayOrder = serial - 1;
                        await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == find.UniqueId, Builders<Customer>.Update.Set(x => x.Cabinets[i], one));
                    }
                }
                await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == find.UniqueId, Builders<Customer>.Update.Pull(x => x.Cabinets, f1));
            }

            index = find?.OutOfCabinets.FindIndex(o => o.No == cabinet);
            if (index >= 0)
            {
                var f2 = find.OutOfCabinets[index.Value];
                if (f2.Drawers.SelectMany(d => d.Boxes).SelectMany(b => b.Fills).Any(f => f.QtyExisted > 0))
                {
                    return false;
                }

                var number = Convert.ToInt32(f2.No.Split(':')[1], 16);
                for (int i = 0; i < find.OutOfCabinets.Count; i++)
                {
                    var one = find.OutOfCabinets[i];
                    var serial = Convert.ToInt32(one.No.Split(':')[1], 16);
                    if (one.DepartmentId == f2.DepartmentId && serial > number)
                    {
                        one.No = $"{one.DepartmentId}:{serial - 1:X2}";
                        one.DisplayOrder = serial - 1;
                        await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == find.UniqueId, Builders<Customer>.Update.Set(x => x.OutOfCabinets[i], one));
                    }
                }
                await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == find.UniqueId, Builders<Customer>.Update.Pull(x => x.OutOfCabinets, f2));
            }
            return true;
        }

        /// <summary>
        ///     新增、修改虚拟柜
        /// </summary>
        [HttpPut]
        [ActionName("modify-virtual-cabinet")]
        public async Task<bool> ModifyVirtualCabinetAsync(string customer, string no, string department, string display)
        {
            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).FirstOrDefault();
            if (cs == null)
            {
                return false;
            }

            var index = cs.OutOfCabinets.FindIndex(v => v.No == no);
            if (index >= 0)
            {
                var vtl = cs.OutOfCabinets[index];
                if (vtl.Drawers.SelectMany(d => d.Boxes).SelectMany(b => b.Fills).Any(f => f.QtyExisted > 0))
                {
                    return false;
                }

                if (vtl.DepartmentId != department)
                {
                    var number = Convert.ToInt32(vtl.No.Split(':')[1], 16);
                    for (var i = 0; i < cs.OutOfCabinets.Count; i++)
                    {
                        var v = cs.OutOfCabinets[i];
                        var serial = Convert.ToInt32(v.No.Split(':')[1], 16);
                        if (v.DepartmentId == vtl.DepartmentId && serial > number)
                        {
                            var curNo = $"{v.No.Split(':')[0]}:{serial - 1:X2}";
                            await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == customer, Builders<Customer>.Update.Set(x => x.OutOfCabinets[i].No, curNo));
                        }
                    }
                    var count = cs.OutOfCabinets.Count(v => v.DepartmentId == department);
                    vtl.No = $"{department}:{count + 1:X2}";
                    vtl.DepartmentId = department;
                }
                vtl.DisplayText = display;
                await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == customer, Builders<Customer>.Update.Set(x => x.OutOfCabinets[index], vtl));
            }
            else
            {
                var obj = new CabinetDevice
                {
                    DepartmentId = department,
                    DisplayText = display,
                    IsControlled = false,
                    No = no,
                    NodeType = "Virtual",
                    OwnerCode = customer,
                    DisplayOrder = cs.OutOfCabinets.Count(v => v.DepartmentId == department) + 1,
                };
                await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == customer, Builders<Customer>.Update.Push(x => x.OutOfCabinets, obj));
            }
            return true;
        }

        [HttpPut]
        [ActionName("modify-system-config")]
        public async Task<bool> ModifySystemConfigAsync(string key, [FromBody] string value)
        {
            if (mongo.SystemConfigCollection.AsQueryable().Any(s => s.Key == key))
            {
                await mongo.SystemConfigCollection.UpdateManyAsync(s => s.Key == key, Builders<SystemConfig>.Update.Set(s => s.JObject, value));
            }
            else
            {
                await mongo.SystemConfigCollection.InsertOneAsync(new SystemConfig { Key = key, JObject = value });
            }
            return true;
        }
    }

}

// running-message
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        /// <summary>
        ///     查询未销账的预支记录
        /// </summary>
        [HttpGet]
        [ActionName("search-none-prescriptioned-medications")]
        public List<AdvanceItems> SearchNonePrescriptionedMedications(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var prescriptions = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && p.Mode == ExchangeMode.CheckOut && p.Computer == Terminal).ToList();
            var medications = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && m.Computer == Terminal).ToList();
            var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
            return GetAdvanceItems(prescriptions, medications, customers).Where(a => a.PrescriptionId == null).ToList();
        }

        public class TerminalInventoryDetail
        {
            public GoodsProfile Goods { get; set; }
            public double QtyExisted { get; set; }
            public double QtyMax { get; set; }
            public double QtyWarning { get; set; }
            public string Category { get; set; }
            public string Foreground { get; set; }
            public string Background { get; set; }
        }

        /// <summary>
        ///     获取终端过期药品 或 数量不足药品
        /// </summary>
        [HttpGet]
        [ActionName("search-expired-or-warning-quota-goods-by-termial")]
        public List<TerminalInventoryDetail> SearchExpiredOrWarningQuotaGoodsByTermial(string type, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;

            var tGoods = mongo.TerminalGoodsCollection.AsQueryable().Where(t => t.Computer == Terminal).ToList().Select(t =>
            {
                t.StorageQuota = double.IsNaN(t.StorageQuota) ? 0.0 : t.StorageQuota;
                t.WarningQuota = double.IsNaN(t.WarningQuota) ? 0.0 : t.WarningQuota;
                return t;
            }).ToList();
            // 智能药柜靠 IP 地址区分
            var cabinets = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(x => x.Cabinets).Where(x => x.Computer == Terminal).ToList();
            // 非智能药柜无 IP 地址，靠部门区分
            var virtuals = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(x => x.OutOfCabinets).Where(x => x.DepartmentId == department).ToList();

            var fills = cabinets.Concat(virtuals).SelectMany(x => x.Drawers).Where(d => !d.IsRecycleBin).SelectMany(x => x.Boxes).SelectMany(x => x.Fills).ToList();
            if (type == "Expired")
            {
                // 查询最近 3 个月内快过期以及已经过期的物品
                var expired = fills.Where(g => g.ExpiredDate.AddMonths(-3) < DateTime.Now).ToList();
                return expired.GroupBy(x => new { x.GoodsId, x.ExpiredDate, }).Select(gp =>
                {
                    var tg = tGoods.FirstOrDefault(t => t.GoodsId == gp.Key.GoodsId);
                    if (tg == null)
                    {
                        return null;
                    }
                    return CreateInstance(tg, gp.First(), gp.Sum(x => x.QtyExisted), gp.Sum(x => x.QtyMax));
                }).Where(g => g != null).OrderBy(g => g.Goods.ExpiredDate).ThenBy(g => g.QtyExisted).ToList();
            }
            if (type == "Warning")
            {
                // 现存量 <= 报警基数 或 现存量 > 存储基数 的物品
                return fills.GroupBy(x => x.GoodsId).Select(gp =>
                {
                    var tg = tGoods.FirstOrDefault(t => t.GoodsId == gp.Key);
                    if (tg == null)
                    {
                        return null;
                    }
                    var qtyExisted = gp.Sum(t => t.QtyExisted);
                    return qtyExisted <= tg.WarningQuota || qtyExisted > tg.StorageQuota ? CreateInstance(tg, gp.First(), qtyExisted, tg.StorageQuota) : null;
                }).Where(x => x != null).ToList();
            }
            return new List<TerminalInventoryDetail>();

            TerminalInventoryDetail CreateInstance(TerminalGoods tg, NodeGoodsInfo gd, double qtyExisted, double qtyMax) => new TerminalInventoryDetail
            {
                Goods = (tg.Goods ?? new Goods { UniqueId = gd.GoodsId, }).ToGoodsProfile(gd.BatchNumber, gd.ExpiredDate.Date),
                QtyExisted = qtyExisted,
                QtyMax = qtyMax,
                QtyWarning = tg.WarningQuota,
                Category = null,
                Background = null,
                Foreground = null,
            };
        }

        /// <summary>
        ///     查询被标记为损坏的药盒(便于实施人员检修)
        /// </summary>
        [HttpGet]
        [ActionName("search-broken-boxes")]
        public string[] SearchBrokenBoxes(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            return mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).ToList().SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).Where(b => b.IsBreakdown).Select(b => b.No).ToArray();
        }

        // modifyBoxBreakState
    }

}

// user-mgr
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class UserDetail : User
        {
            public DepartmentProfile[] Departments { get; set; }
            public string[] GoodsPermissions { get; set; }
        }

        [HttpGet]
        [ActionName("search-users")]
        public Pager<UserDetail> SearchUsers(string content = null, int take = -1, int index = 0)
        {
            var usrsLq = mongo.UserCollection.AsQueryable().Where(t => true);
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                usrsLq = usrsLq.Where(u => u.LoginId.ToLower().Contains(content) || u.Employee.DisplayName.ToLower().Contains(content) || u.Employee.Pinyin.ToLower().Contains(content) || u.Employee.PinyinFull.ToLower().Contains(content) || u.Employee.JobNo.ToLower().Contains(content));
            }

            usrsLq = usrsLq.Select(u => new User
            {
                UniqueId = u.UniqueId,
                LoginId = u.LoginId,
                AutoExpiredTime = u.AutoExpiredTime,
                CanCardAuth = u.CanCardAuth,
                CanIrisAuth = u.CanIrisAuth,
                CanPasswordAuth = u.CanPasswordAuth,
                CanPrintfingerAuth = u.CanPrintfingerAuth,
                CanFaceAuth = u.CanFaceAuth,
                CardNumber = u.CardNumber,
                DisplayName = u.DisplayName,
                DisplayOrder = u.DisplayOrder,
                Employee = u.Employee,
                IsDisabled = u.IsDisabled,
                CreatedTime = u.CreatedTime,

                AvailableStorages = null,
                DataPermissions = null,
                Iris = null,
                Fingerprint = null,
                Face = null,
                Menus = null,
                Roles = null,
                Password = null,
            }).OrderBy(u => u.Employee.JobNo);
            var total = usrsLq.Count();
            var users = (take > 0 ? usrsLq.Skip(index * take).Take(take) : usrsLq).ToArray();

            var keys = users.Select(u => $"{u.UniqueId}:{Helper.AllowedDepartments}").ToArray();
            var configs = mongo.SystemConfigCollection.AsQueryable().Where(k => keys.Contains(k.Key)).ToArray().Select(d => new
            {
                JobNo = d.Key.Split(':')[0],
                Departments = JsonConvert.DeserializeObject<string[]>(d.JObject),
            }).ToArray();
            var goodskeys = users.Select(u => $"{u.UniqueId}:{Helper.AllowedGoods}").ToArray();
            var goodsconfigs = mongo.SystemConfigCollection.AsQueryable().Where(k => goodskeys.Contains(k.Key)).ToArray().Select(d => new
            {
                JobNo = d.Key.Split(':')[0],
                goods = JsonConvert.DeserializeObject<string[]>(d.JObject),
            }).ToArray();
            var departIds = configs.SelectMany(c => c.Departments).Distinct().ToArray();
            var departments = mongo.DepartmentCollection.AsQueryable().Where(d => departIds.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).Select(d => new DepartmentProfile
            {
                UniqueId = d.UniqueId,
                DisplayName = d.DisplayName,
                Code = d.Code,
                Computer = null,
            }).ToArray();

            var data = users.Select(u =>
            {
                var obj = JsonConvert.DeserializeObject<UserDetail>(JsonConvert.SerializeObject(u, Formatting.None, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Serialize, }));
                obj.Departments = departments.Join(configs.FirstOrDefault(f => f.JobNo == u.UniqueId)?.Departments ?? new string[0], a => a.UniqueId, b => b, (a, b) => a).ToArray();
                obj.GoodsPermissions = goodsconfigs.FirstOrDefault(g => g.JobNo == u.UniqueId)?.goods;
                return obj;
            }).ToArray();
            return new Pager<UserDetail> { Count = total, Data = data, };
        }

        /// <summary>
        ///     修改用户认证权限
        /// </summary>
        /// <param name="user">用户Id</param>
        /// <param name="enable">结果 true:启用 false:禁用</param>
        /// <param name="auth">认证权限,Account|Password|Finger|Card|Iris</param>
        [HttpPut]
        [ActionName("modify-user-permission")]
        public async Task<bool> ModifyUserPermissionAsync(string user, bool enable, string auth)
        {
            switch (auth)
            {
                case "Account": await mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == user, Builders<User>.Update.Set(x => x.IsDisabled, enable)); break;
                case "Password": await mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == user, Builders<User>.Update.Set(x => x.CanPasswordAuth, enable)); break;
                case "Finger": await mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == user, Builders<User>.Update.Set(x => x.CanPrintfingerAuth, enable)); break;
                case "Card": await mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == user, Builders<User>.Update.Set(x => x.CanCardAuth, enable)); break;
                case "Iris": await mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == user, Builders<User>.Update.Set(x => x.CanIrisAuth, enable)); break;
                case "Face": await mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == user, Builders<User>.Update.Set(x => x.CanFaceAuth, enable)); break;
            }
            return true;
        }

        /// <summary>
        ///     重置用户密码(默认密码 123456)
        /// </summary>
        [HttpPut]
        [ActionName("modify-user-password")]
        public async Task<bool> ModifyUserPasswordAsync(string user)
        {
            var find = mongo.UserCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == user);
            if (find != null)
            {
                find.Password = Helper.ComputeMd5Hash("123456", find);
                await mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == find.UniqueId, Builders<User>.Update.Set(x => x.Password, find.Password));
            }
            return find != null;
        }

        public class EmployeeProfile : IdName
        {
            public string JobNo { get; set; }
            public string JobTitle { get; set; }
            public string Signature { get; set; }
            public string CertificateCode { get; set; }
            public string CertificateType { get; set; }
            public string CellPhone { get; set; }
        }

        /// <summary>
        ///     根据指定工号匹配员工信息（此接口在录入用户时使用）
        ///     2017-08-19 已经是用户的员工，不再可查询
        /// </summary>
        /// <param name="job">工号</param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-employees-by-job")]
        public List<EmployeeProfile> SearchEmployeesByJob(string job)
        {
            job = (job ?? string.Empty).ToLower();
            var employees = mongo.UserCollection.AsQueryable().Select(u => u.Employee.UniqueId).Where(x => x != null).Distinct().ToList();
            return mongo.EmployeeCollection.AsQueryable().Where(e => e.JobNo.ToLower().Contains(job) && !employees.Contains(e.UniqueId))
                 .Select(s => new EmployeeProfile
                 {
                     UniqueId = s.UniqueId,
                     JobNo = s.JobNo,
                     DisplayName = s.DisplayName,
                     JobTitle = s.JobTitle,
                 }).ToList();
        }

        /// <summary>
        ///     搜索指定 ID 的员工信息
        /// </summary>
        [HttpGet]
        [ActionName("search-employee-by-id")]
        public Employee SearchEmployeeById(string id)
        {
            var employee = mongo.EmployeeCollection.AsQueryable().FirstOrDefault(x => x.UniqueId == id);
            if (employee != null)
            {
                employee.Department = employee.Department ?? mongo.DepartmentCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == employee.DepartmentId);
            }
            return employee;
        }

        /// <summary>
        ///     新增、修改用户信息(不包用户权限数据)，仅包括Employee 登录方式、密码、禁用、锁定等
        /// </summary>
        [HttpPut]
        [ActionName("modify-user-core")]
        public async Task<string> ModifyUserCoreAsync([FromBody] User user)
        {
            user.UniqueId = user.UniqueId ?? user.LoginId;
            if (mongo.UserCollection.AsQueryable().Any(x => x.UniqueId == user.UniqueId))
            {
                var builder = Builders<User>.Update
                    .Set(x => x.CanPrintfingerAuth, user.CanPrintfingerAuth)
                    .Set(x => x.CanIrisAuth, user.CanIrisAuth)
                    .Set(x => x.CanFaceAuth, user.CanFaceAuth)
                    .Set(x => x.CanPasswordAuth, user.CanPasswordAuth)
                    .Set(x => x.DisplayName, user.DisplayName)
                    .Set(x => x.DisplayOrder, user.DisplayOrder)
                    .Set(x => x.IsDisabled, user.IsDisabled)
                    .Set(x => x.Employee, user.Employee);
                await mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == user.UniqueId, builder);
            }
            else
            {
                user.Fingerprint = Enumerable.Range(0, 10).Select(o => string.Empty).ToArray();
                user.Iris = Enumerable.Range(0, 2).Select(o => string.Empty).ToArray();
                user.Face = string.Empty;
                user.Password = Helper.ComputeMd5Hash("123456", user);
                user.Roles = new List<string>();
                user.Menus = new List<string>();
                user.AvailableStorages = new List<string>();
                user.DataPermissions = new List<string>();
                await mongo.UserCollection.InsertOneAsync(user);
            }
            return user.UniqueId;
        }

        [HttpPut]
        [ActionName("modify-Employee-core")]
        public async Task<string> ModifyEmployeeCoreAsync([FromBody] Employee employee)
        {
            employee.UniqueId = employee.JobNo ?? employee.UniqueId;
            if (mongo.EmployeeCollection.AsQueryable().Any(x => x.UniqueId == employee.UniqueId))
            {
                var builder = Builders<Employee>.Update
                    .Set(x => x.DisplayName, employee.DisplayName)
                    .Set(x => x.DepartmentId, employee.DepartmentId)
                    .Set(x => x.Department, mongo.DepartmentCollection.AsQueryable().FirstOrDefault(d => d.UniqueId == employee.DepartmentId))
                    .Set(x => x.JobNo, employee.JobNo)
                    .Set(x => x.JobTitle, employee.JobTitle);
                await mongo.EmployeeCollection.UpdateOneAsync(x => x.UniqueId == employee.UniqueId, builder);
            }
            else
            {
                employee.DisplayName = employee.DisplayName;
                employee.DepartmentId = employee.DepartmentId;
                employee.Department = mongo.DepartmentCollection.AsQueryable().FirstOrDefault(d => d.UniqueId == employee.DepartmentId);
                employee.CreatedTime = DateTime.Now;
                employee.Code = employee.Code;
                employee.JobNo = employee.JobNo;
                employee.JobTitle = employee.JobTitle;
                employee.Pinyin = employee.DisplayName.Pinyin();
                employee.PinyinFull = employee.DisplayName.PinyinFull();
                await mongo.EmployeeCollection.InsertOneAsync(employee);
            }
            return employee.UniqueId;
        }

    }
}

// role-mgr
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class RoleProfile : IdName
        {
            public UserProfile[] Users { get; set; }
            public List<string> Menus { get; set; }
            public string DefaultMenu { get; set; }
            public List<string> AvailableStorages { get; set; }
            public List<string> DataPermissions { get; set; }
        }

        public class UserProfile : IdName
        {
            public string LoginId { get; set; }
            public string JobNo { get; set; }
            public string JobTitle { get; set; }
        }

        /// <summary>
        ///     搜索所有角色以及角色用户、功能权限、药柜权限等
        /// </summary>
        [HttpGet]
        [ActionName("search-all-roles-with-auth")]
        public RoleProfile[] SearchAllRolesWithAuth(int take = -1, int index = 0)
        {
            var lq = mongo.RoleCollection.AsQueryable();
            var roles = (take > 0 ? lq.Skip(index * take).Take(take) : lq).ToArray();
            var userIds = roles.SelectMany(r => r.Users).ToList();
            var users = mongo.UserCollection.AsQueryable().Where(u => userIds.Contains(u.UniqueId)).Select(u => new UserProfile
            {
                UniqueId = u.UniqueId,
                LoginId = u.LoginId,
                DisplayName = u.Employee.DisplayName,
                JobNo = u.Employee.JobNo,
                JobTitle = u.Employee.JobTitle,
            }).ToList();

            return roles.Select(r => new RoleProfile
            {
                UniqueId = r.UniqueId,
                DisplayName = r.DisplayName,
                Users = users.Join(r.Users, a => a.UniqueId, b => b, (a, b) => a).ToArray(),
                Menus = r.Menus,
                DefaultMenu = r.DefaultMenu,
                AvailableStorages = r.AvailableStorages,
                DataPermissions = r.DataPermissions,
            }).ToArray();
        }

        /// <summary>
        ///     新增、修改角色信息(可包含角色授权数据)。 同时自动新增、修改角色用户的页面授权
        /// </summary>
        /// <param name="role">角色</param>
        [HttpPut]
        [ActionName("modify-role")]
        public async Task<string> ModifyRoleAsync([FromBody] Role role)
        {
            role.UniqueId = role.UniqueId ?? SfraObject.GenerateId();
            await mongo.RoleCollection.FindOneAndReplaceAsync<Role>(x => x.UniqueId == role.UniqueId, role, new FindOneAndReplaceOptions<Role, Role> { IsUpsert = true });

            var roles = mongo.RoleCollection.AsQueryable().ToList();

            var users = mongo.UserCollection.AsQueryable().Where(u => role.Users.Contains(u.UniqueId)).ToList();
            foreach (var user in users)
            {
                user.Roles = user.Roles.Concat(new[] { role.UniqueId }).Distinct().ToList();
                user.DataPermissions = user.Roles.Join(roles, ur => ur, r => r.UniqueId, (ur, r) => r.DataPermissions).SelectMany(d => d).Distinct().ToList();
                user.AvailableStorages = user.Roles.Join(roles, ur => ur, r => r.UniqueId, (ur, r) => r.AvailableStorages).SelectMany(s => s).Distinct().ToList();
                user.Menus = user.Roles.Join(roles, ur => ur, r => r.UniqueId, (ur, r) => r.Menus).SelectMany(m => m).Distinct().ToList();

                var builder = Builders<User>.Update.Set(x => x.Roles, user.Roles)
                    .Set(x => x.DataPermissions, user.DataPermissions)
                    .Set(x => x.AvailableStorages, user.AvailableStorages)
                    .Set(x => x.Menus, user.Menus);
                await mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == user.UniqueId, builder);
            }

            return role.UniqueId;
        }

        /// <summary>
        ///     删除指定的角色
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        [HttpDelete]
        [ActionName("remove-role")]
        public async Task<bool> RemoveRoleAsync(string role)
        {
            await mongo.RoleCollection.DeleteOneAsync(r => r.UniqueId == role);

            var roles = mongo.RoleCollection.AsQueryable().Where(r => !r.IsDisabled).ToList();
            var users = mongo.UserCollection.AsQueryable().Where(r => r.Roles.Contains(role)).ToList();
            foreach (var user in users)
            {
                user.Roles = user.Roles.Where(ur => ur != role).Distinct().ToList();
                user.DataPermissions = user.Roles.Join(roles, ur => ur, r => r.UniqueId, (ur, r) => r.DataPermissions).SelectMany(d => d).Distinct().ToList();
                user.AvailableStorages = user.Roles.Join(roles, ur => ur, r => r.UniqueId, (ur, r) => r.AvailableStorages).SelectMany(s => s).Distinct().ToList();
                user.Menus = user.Roles.Join(roles, ur => ur, r => r.UniqueId, (ur, r) => r.Menus).SelectMany(m => m).Distinct().ToList();

                var builder = Builders<User>.Update.Set(x => x.Roles, user.Roles)
                    .Set(x => x.DataPermissions, user.DataPermissions)
                    .Set(x => x.AvailableStorages, user.AvailableStorages)
                    .Set(x => x.Menus, user.Menus);
                await mongo.UserCollection.UpdateOneAsync(Builders<User>.Filter.Eq(x => x.UniqueId, user.UniqueId), builder);
            }

            return true;
        }

        [HttpGet]
        [ActionName("search-users-outsides-any-role")]
        public UserProfile[] SearchUsersOutsidesAnyRole()
        {
            var users = mongo.RoleCollection.AsQueryable().SelectMany(r => r.Users).ToList();
            return mongo.UserCollection.AsQueryable().Where(u => !users.Contains(u.UniqueId))
                .Select(u => new UserProfile
                {
                    UniqueId = u.UniqueId,
                    LoginId = u.LoginId,
                    DisplayName = u.Employee.DisplayName,
                    JobNo = u.Employee.JobNo,
                    JobTitle = u.Employee.JobTitle,
                })
                .ToArray();
        }

        [HttpPut]
        [ActionName("modify-users-for-role")]
        public async Task<bool> ModifyUsersForRoleAsync(string role, [FromBody] string[] users)
        {
            var find = mongo.RoleCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == role);
            if (find == null)
            {
                return false;
            }

            var rms = find.Users.Except(find.Users.Intersect(users)).ToList();
            if (rms.Any())
            {
                // 清除原有的权限
                await mongo.UserCollection.UpdateManyAsync(u => rms.Contains(u.UniqueId), Builders<User>.Update.Set(u => u.Roles, new List<string>())
                    .Set(u => u.DataPermissions, new List<string>())
                    .Set(u => u.AvailableStorages, new List<string>())
                    .Set(u => u.Menus, new List<string>())
                    .Set(u => u.Roles, new List<string>()));
            }

            find.Users = users.ToList();
            await ModifyRoleAsync(find);
            return true;
        }

        public class Module
        {
            public Menu Menu { get; set; }
            public List<Menu> Children { get; set; }
        }

        /// <summary>
        ///     搜索模块
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-module-for-menu")]
        public List<Module> SearchModuleForMenu()
        {
            var finds = mongo.MenuCollection.AsQueryable().ToList();
            return finds.Where(x => x.IsModule).Select(item => new Module
            {
                Menu = item,
                Children = finds.Where(x => x.ParentId == item.UniqueId).OrderBy(x => x.DisplayOrder).ToList()
            }).OrderBy(x => x.Menu.DisplayOrder).ToList();
        }

        [HttpPut]
        [ActionName("modify-menu-for-role")]
        public async Task<bool> ModifyMenuForRoleAsync(string role, string menu, bool enable)
        {
            var find = mongo.RoleCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == role);
            if (find == null)
            {
                return false;
            }
            find.Menus = find.Menus.Where(m => m != menu).ToList();
            if (enable)
            {
                find.Menus.Add(menu);
            }
            await ModifyRoleAsync(find);
            return true;
        }

        [HttpPut]
        [ActionName("modify-default-menu-for-role")]
        public async Task<bool> ModifyDefaultMenuForRoleAsync(string role, string menu)
        {
            var find = mongo.RoleCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == role);
            if (find == null)
            {
                return false;
            }
            find.DefaultMenu = menu;
            await ModifyRoleAsync(find);
            return true;
        }

        //     searchAllCustomerCore

        //     searchAllCustomerIntelligentCabinets

        //     searchDepartmentsOwnComputer

        [HttpGet]
        [ActionName("search-goods-filters")]
        public string[] SearchGoodsFilters() => mongo.GoodsCollection.AsQueryable().Where(g => !g.IsDisabled).Select(g => g.Filter).Distinct().ToArray();
    }
}
