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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

#pragma warning disable CS1591

// department-mgr
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        /// <summary>
        ///     搜索所有的科室、病区、手术室等
        /// </summary>
        [HttpGet]
        [ActionName("search-all-department-details")]
        public Pager<Department> SearchAllDepartmentDetails(string content = null, int take = -1, int index = 0)
        {
            var departsLq = mongo.DepartmentCollection.AsQueryable().Where(d => !d.IsDisabled);
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                departsLq = departsLq.Where(d => d.DisplayName.ToLower().Contains(content) || d.Pinyin.ToLower().Contains(content) || d.PinyinFull.ToLower().Contains(content) || d.Code.ToLower().Contains(content));
            }
            var count = departsLq.Count();
            var departs = (take > 0 ? departsLq.Skip(index * take).Take(take) : departsLq).ToArray();
            return new Pager<Department> { Count = count, Data = departs, };
        }

        /// <summary>
        ///     同步所有部门信息
        /// </summary> 
        [HttpPost]
        [ActionName("sync-all-departments")]
        public async Task<ApiBack> SyncAllDepartments(string hospital)
        {
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseProxy = false, }))
            {
                var url = $"{Hospital.ApiAddress(nameof(ISyncObject.SyncAllDepartments))}?hospital={hospital}";
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ApiBack>(json);
            }
        }
        /// <summary>
        ///  修改部门
        /// </summary>
        /// <param name="department"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-departments-core")]
        public async Task<string> ModifyDepartmentsCoreAsync([FromBody] Department department)
        {
            department.UniqueId = department.Code ?? department.UniqueId;
            if (mongo.DepartmentCollection.AsQueryable().Any(x => x.UniqueId == department.UniqueId))
            {
                var builder = Builders<Department>.Update
                    .Set(x => x.DisplayName, department.DisplayName);
                await mongo.DepartmentCollection.UpdateOneAsync(x => x.UniqueId == department.UniqueId, builder);
            }
            else
            {
                department.DisplayName = department.DisplayName;
                department.CreatedTime = DateTime.Now;
                department.Code = department.Code;
                department.Pinyin = department.DisplayName.Pinyin();
                department.PinyinFull = department.DisplayName.PinyinFull();
                await mongo.DepartmentCollection.InsertOneAsync(department);
            }
            return department.UniqueId;
        }
    }

}

// employee-mgr
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        // searchDepartmentsOwnComputer

        public class EmployeeDetail : EmployeeProfile
        {
            public DepartmentProfile Department { get; set; }
            public bool IsDoctor { get; set; }
            public bool IsNurse { get; set; }
            public DateTime CreatedTime { get; set; }
        }

        [HttpGet]
        [ActionName("search-all-employee-details")]
        public Pager<EmployeeDetail> SearchAllEmployeeDetails(string department = null, string content = null, int take = -1, int index = 0)
        {
            var lq = mongo.EmployeeCollection.AsQueryable().Where(e => !e.IsDisabled);
            if (!string.IsNullOrEmpty(department))
            {
                lq = lq.Where(e => e.DepartmentId == department);
            }
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                lq = lq.Where(e => e.DisplayName.ToLower().Contains(content) || e.Pinyin.ToLower().Contains(content) || e.PinyinFull.ToLower().Contains(content) || e.Code.ToLower().Contains(content) || e.JobTitle.ToLower().Contains(content) || e.JobNo.ToLower().Contains(content));
            }

            var count = lq.Count();

            var emps = (take > 0 ? lq.Skip(index * take).Take(take) : lq).ToList();
            var docs = emps.Select(e => e.DepartmentId).Distinct().Select(d => $"{d}:Doctors").ToArray();
            var docsData = mongo.SystemConfigCollection.AsQueryable().Where(s => docs.Contains(s.Key)).Select(s => s.JObject).ToList().SelectMany(s => JsonConvert.DeserializeObject<List<string>>(s)).ToList();
            var nurs = emps.Select(e => e.DepartmentId).Distinct().Select(d => $"{d}:Nurses").ToArray();
            var nursData = mongo.SystemConfigCollection.AsQueryable().Where(s => nurs.Contains(s.Key)).Select(s => s.JObject).ToList().SelectMany(s => JsonConvert.DeserializeObject<List<string>>(s)).ToList();
            var cabs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).ToList();
            var cabsData = cabs.Select(c => (c.DepartmentId, c.Computer)).Distinct().ToList();

            var dpts = emps.Select(e => new EmployeeDetail
            {

                UniqueId = e.UniqueId,
                DisplayName = e.DisplayName,
                JobNo = e.JobNo,
                JobTitle = e.JobTitle,
                Department = new DepartmentProfile
                {
                    UniqueId = e.DepartmentId,
                    DisplayName = e.Department?.DisplayName,
                    Code = e.Department?.Code ?? e.DepartmentId,
                    Computer = cabsData.Where(c => c.DepartmentId == e.DepartmentId).Select(c => c.Computer).FirstOrDefault(),
                },
                CreatedTime = e.CreatedTime,
                IsDoctor = docsData.Any(d => d == e.UniqueId),
                IsNurse = nursData.Any(n => n == e.UniqueId),
                CertificateCode = e.CertificateCode,
            }).ToArray();
            return new Pager<EmployeeDetail> { Count = count, Data = dpts, };
        }

        /// <summary>
        ///     判定员工是医生还是护士
        /// </summary>
        /// <param name="department"></param>
        /// <param name="employee"></param>
        /// <param name="title">Doctor 或 Nurse</param>
        /// <param name="enable"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-employee-title")]
        public async Task<bool> ModifyEmployeeTitleAsync(string department, string employee, string title, bool enable)
        {
            string key = null;
            switch (title)
            {
                case "Doctor": key = $"{department}:Doctors"; break;
                case "Nurse": key = $"{department}:Nurses"; break;
            }

            var modified = false;
            if (!string.IsNullOrEmpty(key))
            {
                var config = mongo.SystemConfigCollection.AsQueryable().FirstOrDefault(f => f.Key == key) ?? new SystemConfig { Key = key, };
                var employees = string.IsNullOrEmpty(config.JObject) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(config.JObject);
                employees = employees.Where(d => d != employee).ToList();
                if (enable)
                {
                    employees.Add(employee);
                }
                config.JObject = JsonConvert.SerializeObject(employees);
                await mongo.SystemConfigCollection.FindOneAndReplaceAsync<SystemConfig>(x => x.UniqueId == config.UniqueId, config, new FindOneAndReplaceOptions<SystemConfig, SystemConfig> { IsUpsert = true });
                modified = true;
            }
            return modified;
        }

        /// <summary>
        ///     同步所有的员工信息
        /// </summary>
        [HttpPost]
        [ActionName("sync-all-employees")]
        public async Task<ApiBack> SyncAllEmployeesAsync(string hospital)
        {
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseProxy = false, }))
            {
                var url = $"{Hospital.ApiAddress(nameof(ISyncObject.SyncAllEmployees))}?hospital={hospital}";
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ApiBack>(json);
            }
        }

        /// <summary>
        ///     根据配置文件中医生、护士的关键词，判定员工是医生还是护士
        /// </summary>
        [HttpPut]
        [ActionName("modify-employees-title-by-keywords")]
        public int ModifyEmployeesTitleByKeywords()
        {
            // 医生、护士的判定 根据职称
            var docs = Global.AppSettings["DoctorKeys"].Values<string>().ToArray();
            var nurs = Global.AppSettings["NurseKeys"].Values<string>().ToArray();
            var emps = mongo.EmployeeCollection.AsQueryable().Where(e => !e.IsDisabled && !string.IsNullOrEmpty(e.JobTitle)).Select(e => new { e.UniqueId, e.JobTitle, e.DepartmentId, }).ToList();
            foreach (var item in emps)
            {
                var key = docs.Any(t => item.JobTitle.Contains(t)) ? $"{item.DepartmentId}:Doctors" : nurs.Any(t => item.JobTitle.Contains(t)) ? $"{item.DepartmentId}:Nurses" : null;
                if (!string.IsNullOrEmpty(key))
                {
                    var config = mongo.SystemConfigCollection.AsQueryable().FirstOrDefault(f => f.Key == key) ?? new SystemConfig { Key = key, };
                    var employees = string.IsNullOrEmpty(config.JObject) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(config.JObject);
                    config.JObject = JsonConvert.SerializeObject(employees.Concat(new[] { item.UniqueId }).Distinct());
                    mongo.SystemConfigCollection.FindOneAndReplace<SystemConfig>(x => x.UniqueId == config.UniqueId, config, new FindOneAndReplaceOptions<SystemConfig, SystemConfig> { IsUpsert = true });
                }
            }
            return emps.Count;
        }
    }
}

// goods-category-mgr
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        //     searchAllCustomerCore

        /// <summary>
        ///     搜索所有物品分类
        /// </summary>
        [HttpGet]
        [ActionName("search-categories-by-customer")]
        public List<GoodsCategory> SearchCategoriesByCustomer(string customer)
        {
            var categories = mongo.GoodsCategoryCollection.AsQueryable().Where(x => x.CustomerId == customer).OrderBy(x => x.DisplayOrder).ToList();
            var goodsIds = categories.SelectMany(d => d.GoodsKeys).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => !g.IsDisabled && goodsIds.Contains(g.UniqueId)).ToList();
            var templates = mongo.DesignerTemplateCollection.AsQueryable().Where(d => !d.IsDisabled).ToList();
            foreach (var cat in categories)
            {
                cat.Goods = goods.Join(cat.GoodsKeys, g => g.UniqueId, k => k, (g, k) => g).ToList();
                cat.Template = templates.Where(t => t.UniqueId == cat.TemplateId).FirstOrDefault();
            }
            return categories;
        }

        /// <summary>
        ///     修改药品组认证权限
        /// </summary>
        /// <param name="category">分组</param>
        /// <param name="permission">权限 DoubleCertify, Allocation </param>
        /// <param name="enable">启用、禁用</param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-category-permission")]
        public async Task<bool> ModifyCategoryPermissionAsync(string category, string permission, bool enable)
        {
            var modified = false;
            switch (permission)
            {
                case "DoubleCertify":
                    await mongo.GoodsCategoryCollection.UpdateOneAsync(e => e.UniqueId == category, Builders<GoodsCategory>.Update.Set(x => x.IsDoubleCertify, enable));
                    modified = true;
                    break;
                case "Allocation":
                    await mongo.GoodsCategoryCollection.UpdateOneAsync(e => e.UniqueId == category, Builders<GoodsCategory>.Update.Set(x => x.PermitAlloc, enable));
                    modified = true;
                    break;
                default: break;
            }
            return modified;
        }

        /// <summary>
        ///     新增、修改分组数据
        /// </summary>
        [HttpPut]
        [ActionName("modify-category")]
        public async Task<string> ModifyCategoryAsync([FromBody] GoodsCategory category)
        {
            category.UniqueId = category.UniqueId ?? SfraObject.GenerateId();
            await mongo.GoodsCategoryCollection.FindOneAndReplaceAsync<GoodsCategory>(x => x.UniqueId == category.UniqueId, category, new FindOneAndReplaceOptions<GoodsCategory, GoodsCategory> { IsUpsert = true });
            return category.UniqueId;
        }

        /// <summary>
        ///     自动生成分组
        /// </summary>
        [HttpGet]
        [ActionName("auto-create-category")]
        public async Task<bool> AutoCreateCategoryAsync(string customerId)
        {
            var goodsGroup = mongo.GoodsCollection.AsQueryable().Where(g => !string.IsNullOrEmpty(g.GoodsType)).ToList();
            var categorys = goodsGroup.GroupBy(d => d.GoodsType).Select(d =>
                 new GoodsCategory
                 {
                     UniqueId = d.Key.PinyinFull(),
                     DisplayName = d.Key,
                     Pinyin = d.Key.Pinyin(),
                     PinyinFull = d.Key.PinyinFull(),
                     CustomerId = customerId,
                     GoodsKeys = d.Select(g => g.UniqueId).ToList(),
                     Foreground = "#FFFFFF",
                     Background = "#000000",
                 }).ToList();
            foreach (var item in categorys)
            {
                item.DisplayOrder = categorys.IndexOf(item);
                await mongo.GoodsCategoryCollection.FindOneAndReplaceAsync<GoodsCategory>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<GoodsCategory, GoodsCategory> { IsUpsert = true });

            }
            return true;
        }

        /// <summary>
        ///     刪除指定物品組
        /// </summary>
        [HttpDelete]
        [ActionName("remove-category")]
        public async Task<bool> RemoveCategoryAsync(string category)
        {
            await mongo.GoodsCategoryCollection.DeleteOneAsync(x => x.UniqueId == category);
            return true;
        }

        /// <summary>
        ///     搜索未分组的药品
        /// </summary>
        [HttpGet]
        [ActionName("search-goods-outside-category")]
        public Pager<Goods> SearchGoodsOutsideCategory(string customer, string content = null, int take = -1, int index = 0)
        {
            var goodsIds = mongo.GoodsCategoryCollection.AsQueryable().Where(x => x.CustomerId == customer).SelectMany(x => x.GoodsKeys).ToList();
            var goodsLq = mongo.GoodsCollection.AsQueryable().Where(g => !g.IsDisabled && !goodsIds.Contains(g.UniqueId));
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                goodsLq = goodsLq.Where(g => g.DisplayName.ToLower().Contains(content) || g.Pinyin.ToLower().Contains(content) || g.PinyinFull.ToLower().Contains(content) || g.Specification.ToLower().Contains(content) || g.Manufacturer.ToLower().Contains(content));
            }
            var count = goodsLq.Count();
            var goods = (take > 0 ? goodsLq.Skip(index * take).Take(take) : goodsLq).ToArray();
            return new Pager<Goods> { Count = count, Data = goods, };
        }

        /// <summary>
        ///     给指定物品组分配物品
        /// </summary>
        [HttpPut]
        [ActionName("modify-category-goods")]
        public async Task<bool> ModifyCategoryGoodsAsync(string category, [FromBody] string[] goods)
        {
            await mongo.GoodsCategoryCollection.UpdateOneAsync(x => x.UniqueId == category, Builders<GoodsCategory>.Update.Set(x => x.GoodsKeys, goods.ToList()));
            return true;
        }
    }

}

// goods-kit-mgr
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class KitGroup : IdName
        {
            public List<GoodsProfileQty> Goods { get; set; }
            public string CreatorId { get; set; }
            public string CreatorName { get; set; }
            public int DisplayOrder { get; set; }
        }

        public class GoodsProfileQty : GoodsProfile
        {
            public double Qty { get; set; }
        }

        [HttpGet]
        [ActionName("search-kits")]
        public Pager<KitGroup> SearchKits(string content, int take = -1, int index = 0, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal)?.LoginId;
            var secondary = ServiceStartup.GetSecondaryAuthorized(Terminal)?.LoginId;

            var kitLq = mongo.KitCollection.AsQueryable().Where(k => !k.IsDisabled && (k.CreatorId == primary || k.CreatorId == secondary));
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                kitLq = kitLq.Where(k => k.DisplayName.ToLower().Contains(content) || k.Pinyin.ToLower().Contains(content) || k.PinyinFull.ToLower().Contains(content));
            }
            var count = kitLq.Count();
            var kits = (take > 0 ? kitLq.Skip(take * index).Take(take) : kitLq).OrderBy(k => k.DisplayOrder).ToList();
            var goodsIds = kits.SelectMany(d => d.Kits).Select(g => g.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).ToList();

            kits.ForEach(k => k.Kits.ForEach(gp => gp.Goods = goods.FirstOrDefault(f => f.UniqueId == gp.GoodsId) ?? new Goods { UniqueId = gp.GoodsId, }));
            return new Pager<KitGroup>
            {
                Count = count,
                Data = kits.Select(k => new KitGroup
                {
                    UniqueId = k.UniqueId,
                    DisplayName = k.DisplayName,
                    CreatorId = k.CreatorId,
                    CreatorName = k.CreatorName,
                    DisplayOrder = k.DisplayOrder,
                    Goods = k.Kits.Select(gp => gp.Goods.ToGoodsProfileQty(string.Empty, DateTime.MaxValue.Date, gp.Qty)).ToList(),
                }).ToArray(),
            };
        }

        /// <summary>
        ///     同一个人只能创建一个同名套装（忽略大小写）
        /// </summary>
        [HttpGet]
        [ActionName("search-if-kit-exist")]
        public bool SearchIfKitExist(string kit, string name, string user)
        {
            name = (name ?? "").ToLower();
            return mongo.KitCollection.AsQueryable().Any(k => !k.IsDisabled && k.UniqueId != kit && k.DisplayName.ToLower() == name && k.CreatorId == user);
        }

        [HttpPut]
        [ActionName("modify-kit")]
        public async Task<bool> ModifyKitAsync([FromBody] Kit kit)
        {
            kit.UniqueId = kit.UniqueId ?? SfraObject.GenerateId();
            await mongo.KitCollection.FindOneAndReplaceAsync<Kit>(x => x.UniqueId == kit.UniqueId, kit, new FindOneAndReplaceOptions<Kit, Kit> { IsUpsert = true });
            return true;
        }

        [HttpPut]
        [ActionName("modify-copy-users-kit")]
        public async Task<bool> ModifyCopyUsersKitAsync([FromBody] Kit[] kits)
        {
            var mm = mongo.KitCollection.AsQueryable().Where(k => !k.IsDisabled).ToList();
            foreach (var kit in kits)
            {
                var name = (kit.DisplayName ?? "").ToLower();
                if (!mm.Any(k => k.DisplayName.ToLower() == name && k.CreatorId == kit.CreatorId))
                {
                    kit.UniqueId = kit.UniqueId ?? SfraObject.GenerateId();
                    kit.Code = kit.UniqueId;
                    await mongo.KitCollection.FindOneAndReplaceAsync<Kit>(x => x.UniqueId == kit.UniqueId, kit, new FindOneAndReplaceOptions<Kit, Kit> { IsUpsert = true });
                }
            }
            return true;
        }

        [HttpGet]
        [ActionName("search-goods-outside-kit")]
        public GoodsProfile[] SearchGoodsOutsideKit(string kit, string content)
        {
            var goodsIds = mongo.KitCollection.AsQueryable().Where(k => k.UniqueId == kit).SelectMany(k => k.Kits).ToList().Select(k => k.GoodsId).Distinct().ToList();
            var lq = mongo.GoodsCollection.AsQueryable().Where(g => !g.IsDisabled && !goodsIds.Contains(g.UniqueId));
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                lq = lq.Where(o => o.DisplayName.ToLower().Contains(content) || o.Pinyin.ToLower().Contains(content) || o.PinyinFull.ToLower().Contains(content) || o.Specification.ToLower().Contains(content) || o.Manufacturer.ToLower().Contains(content));
            }
            var goods = lq.ToList();
            // 只查询在终端物品配置中配置的物品
            goods = goods.Join(mongo.TerminalGoodsCollection.AsQueryable().Select(tg => tg.GoodsId).Distinct().ToList(), a => a.UniqueId, b => b, (a, b) => a).ToList();
            return goods.ToList().Select(g => g.ToGoodsProfile(string.Empty, DateTime.MaxValue.Date)).ToArray();
        }

        [HttpDelete]
        [ActionName("remove-goods-from-kit")]
        public async Task<bool> RemoveGoodsFromKitAsync(string kit, string goods)
        {
            var kits = mongo.KitCollection.AsQueryable().Where(k => k.UniqueId == kit).SelectMany(k => k.Kits).ToList();
            kits = kits.Where(k => k.GoodsId != goods).ToList();
            await mongo.KitCollection.UpdateOneAsync(k => k.UniqueId == kit, Builders<Kit>.Update.Set(k => k.Kits, kits));
            return true;
        }

        [HttpDelete]
        [ActionName("remove-kit")]
        public async Task<bool> RemoveKitAsync(string kit)
        {
            await mongo.KitCollection.UpdateOneAsync(k => k.UniqueId == kit, Builders<Kit>.Update.Set(k => k.IsDisabled, true));
            return true;
        }

        [HttpPut]
        [ActionName("modify-display-order")]
        public async Task<bool> ModifyDisplayOrderAsync(string collection, [FromBody] string[] items)
        {
            items = items ?? new string[0];
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                switch (collection)
                {
                    case nameof(Kit): await mongo.KitCollection.UpdateOneAsync(k => k.UniqueId == item, Builders<Kit>.Update.Set(k => k.DisplayOrder, i + 1)); break;
                    case nameof(GoodsCategory): await mongo.GoodsCategoryCollection.UpdateOneAsync(g => g.UniqueId == item, Builders<GoodsCategory>.Update.Set(g => g.DisplayOrder, i + 1)); break;
                    case nameof(Evaluate): await mongo.EvaluateCollection.UpdateOneAsync(e => e.UniqueId == item, Builders<Evaluate>.Update.Set(e => e.DisplayOrder, i + 1)); break;
                }
            }
            return items.Any();
        }
    }
}

// evaluate-rule-mgr
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class EvaluateGroup : IdName
        {
            public List<GoodsProfileQty> Goods { get; set; }
            public string Keywords { get; set; }
            public bool IsActived { get; set; }
            public int DisplayOrder { get; set; }
        }

        [HttpGet]
        [ActionName("search-evaluates")]
        public Pager<EvaluateGroup> SearchEvaluates(string content = null, int take = -1, int index = 0)
        {
            var lq = mongo.EvaluateCollection.AsQueryable().Where(e => !e.IsDisabled);
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                lq = lq.Where(e => e.DisplayName.ToLower().Contains(content) || e.Pinyin.ToLower().Contains(content) || e.PinyinFull.ToLower().Contains(content));
            }
            var count = lq.Count();
            var evaluates = (take > 0 ? lq.Skip(take * index).Take(take) : lq).OrderBy(k => k.DisplayOrder).ToList();
            var goodsIds = evaluates.SelectMany(d => d.Evaluates).Select(g => g.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).ToList();

            evaluates.ForEach(k => k.Evaluates.ForEach(o => o.Goods = goods.FirstOrDefault(f => f.UniqueId == o.GoodsId) ?? new Goods { UniqueId = o.GoodsId, }));
            return new Pager<EvaluateGroup>
            {
                Count = count,
                Data = evaluates.Select(k => new EvaluateGroup
                {
                    UniqueId = k.UniqueId,
                    DisplayName = k.DisplayName,
                    IsActived = k.IsActived,
                    Keywords = k.Keywords,
                    DisplayOrder = k.DisplayOrder,
                    Goods = k.Evaluates.Select(o => o.Goods.ToGoodsProfileQty(string.Empty, DateTime.MaxValue.Date, o.Qty)).ToList(),
                }).ToArray(),
            };
        }

        [HttpPut]
        [ActionName("modify-evaluate")]
        public async Task<bool> ModifyEvaluateAsync([FromBody] Evaluate evaluate)
        {
            evaluate.UniqueId = evaluate.UniqueId ?? SfraObject.GenerateId();
            await mongo.EvaluateCollection.FindOneAndReplaceAsync<Evaluate>(x => x.UniqueId == evaluate.UniqueId, evaluate, new FindOneAndReplaceOptions<Evaluate, Evaluate> { IsUpsert = true });
            return true;
        }

        [HttpGet]
        [ActionName("search-goods-outside-evaluate")]
        public Pager<GoodsProfile> SearchGoodsOutsideEvaluate(string evaluate, string content, int take = -1, int index = 0)
        {
            var goodsIds = mongo.EvaluateCollection.AsQueryable().Where(e => e.UniqueId == evaluate).SelectMany(e => e.Evaluates).ToList().Select(e => e.GoodsId).Distinct().ToList();
            var lq = mongo.GoodsCollection.AsQueryable().Where(g => !g.IsDisabled && !goodsIds.Contains(g.UniqueId));
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                lq = lq.Where(o => o.DisplayName.ToLower().Contains(content) || o.Pinyin.ToLower().Contains(content) || o.PinyinFull.ToLower().Contains(content) || o.Specification.ToLower().Contains(content) || o.Manufacturer.ToLower().Contains(content));
            }
            var goods = lq.ToList();
            // 只查询在终端物品配置中配置的物品
            goods = goods.Join(mongo.TerminalGoodsCollection.AsQueryable().Select(tg => tg.GoodsId).Distinct().ToList(), a => a.UniqueId, b => b, (a, b) => a).ToList();
            var count = goods.Count();
            var data = (take > 0 ? goods.Skip(index * take).Take(take) : goods).ToList().Select(g => g.ToGoodsProfile(string.Empty, DateTime.MaxValue.Date)).ToArray();
            return new Pager<GoodsProfile> { Count = count, Data = data, };
        }

        [HttpDelete]
        [ActionName("remove-evaluate")]
        public async Task<bool> RemoveEvaluateAsync(string evaluate)
        {
            await mongo.EvaluateCollection.UpdateOneAsync(e => e.UniqueId == evaluate, Builders<Evaluate>.Update.Set(e => e.IsDisabled, true));
            return true;
        }

        [HttpDelete]
        [ActionName("remove-goods-from-evaluate")]
        public async Task<bool> RemoveGoodsFromEvaluateAsync(string evaluate, string goods)
        {
            var evaluates = mongo.EvaluateCollection.AsQueryable().Where(e => e.UniqueId == evaluate).SelectMany(e => e.Evaluates).ToList();
            evaluates = evaluates.Where(k => k.GoodsId != goods).ToList();
            await mongo.EvaluateCollection.UpdateOneAsync(e => e.UniqueId == evaluate, Builders<Evaluate>.Update.Set(e => e.Evaluates, evaluates));
            return true;
        }
    }
}

// goods-mgr
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class Pager<T>
        {
            public int Count { get; set; }
            public T[] Data { get; set; }
        }

        /// <summary>
        ///     搜索指定过滤类型的物品
        /// </summary>
        /// <param name="filter">Drug, Consume, OperatingCoat, Slipper</param>
        /// <param name="content"></param>
        /// <param name="take">页面数据行数</param>
        /// <param name="index">分页索引，0 基</param>
        [HttpGet]
        [ActionName("search-goods-by-filter")]
        public Pager<Goods> SearchGoodsByFilter(string filter = null, string content = null, int take = -1, int index = 0)
        {
            var goodsLq = mongo.GoodsCollection.AsQueryable().OrderByDescending(g => g.CreatedTime).ThenBy(g => g.DisplayOrder).Where(g => (string.IsNullOrEmpty(filter) || g.Filter == filter) && !g.IsDisabled);
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                goodsLq = goodsLq.Where(g => g.DisplayName.ToLower().Contains(content) || g.Pinyin.ToLower().Contains(content) || g.PinyinFull.ToLower().Contains(content) || g.Specification.ToLower().Contains(content) || g.Manufacturer.ToLower().Contains(content));
            }
            var count = goodsLq.Count();
            var goods = (take > 0 ? goodsLq.Skip(take * index).Take(take) : goodsLq).ToArray();

            var keys = goods.Select(g => $"{g.UniqueId}:ZeroPrice").ToList();
            var configs = mongo.SystemConfigCollection.AsQueryable().Where(s => keys.Contains(s.Key)).ToList();
            foreach (var g in goods)
            {
                var config = configs.FirstOrDefault(s => s.Key == $"{g.UniqueId}:ZeroPrice")?.JObject ?? "false";
                g.Price = config == "false" ? g.Price : 0;
            }
            return new Pager<Goods> { Count = count, Data = goods, };
        }

        /// <summary>
        ///     新增、修改物品数据  (不能修改从 HIS 同步过来的物品)
        /// </summary>
        [HttpPut]
        [ActionName("modify-goods")]
        public async Task<string> ModifyGoodsAsync([FromBody] Goods goods)
        {
            goods.UniqueId = goods.UniqueId ?? SfraObject.GenerateId();
            if (mongo.GoodsCollection.AsQueryable().Any(g => g.UniqueId == goods.UniqueId && g.IsSync))
            {
                return null;
            }

            await mongo.GoodsCollection.FindOneAndReplaceAsync<Goods>(x => x.UniqueId == goods.UniqueId, goods, new FindOneAndReplaceOptions<Goods, Goods> { IsUpsert = true });

            // 把其它冗余数据也一并改掉
            await mongo.ExchangeCollection.UpdateManyAsync(e => e.GoodsId == goods.UniqueId, Builders<Exchange>.Update.Set(x => x.Goods, goods));
            await mongo.AllocationCollection.UpdateManyAsync(a => a.GoodsId == goods.UniqueId, Builders<Allocation>.Update.Set(x => x.Goods, goods));
            await mongo.PrescriptionCollection.UpdateManyAsync(p => p.GoodsId == goods.UniqueId, Builders<Prescription>.Update.Set(x => x.Goods, goods));
            await mongo.MedicationCollection.UpdateManyAsync(m => m.GoodsId == goods.UniqueId, Builders<Medication>.Update.Set(x => x.Goods, goods));
            await mongo.InternalAllocationCollection.UpdateManyAsync(i => i.GoodsId == goods.UniqueId, Builders<InternalAllocation>.Update.Set(x => x.Goods, goods));

            var tsfs = mongo.TransferCollection.AsQueryable().ToList();
            for (int i = 0; i < tsfs.Count; i++)
            {
                var tsf = tsfs[i];
                if ((tsf.TransferRecords ?? new List<Transfer.TransferRecord>()).All(t => t.GoodsId != goods.UniqueId))
                {
                    continue;
                }

                for (int j = 0; j < tsf.TransferRecords.Count; j++)
                {
                    await mongo.TransferCollection.UpdateOneAsync(t => t.UniqueId == tsf.UniqueId, Builders<Transfer>.Update.Set(x => x.TransferRecords[j].Goods, goods));
                }
            }
            await mongo.TerminalGoodsCollection.UpdateManyAsync(t => t.GoodsId == goods.UniqueId, Builders<TerminalGoods>.Update.Set(x => x.Goods, goods));

            return goods.UniqueId;
        }

        /// <summary>
        ///     删除物品。不可以删除从 HIS 同步过来的物品，只能删除 SFRAMED 自维护的物品
        /// </summary>
        [HttpDelete]
        [ActionName("remove-goods")]
        public async Task<bool> RemoveGoodsAsync(string goods)
        {
            if (mongo.GoodsCollection.AsQueryable().Any(f => f.UniqueId == goods && f.IsSync))
            {
                return false;
            }

            await mongo.GoodsCollection.UpdateOneAsync(g => g.UniqueId == goods, Builders<Goods>.Update.Set(o => o.IsDisabled, true));

            var cats = mongo.GoodsCategoryCollection.AsQueryable().Where(c => c.GoodsKeys.Contains(goods)).ToList();
            foreach (var cat in cats)
            {
                cat.GoodsKeys = cat.GoodsKeys.Where(g => g != goods).ToList();
                await mongo.GoodsCategoryCollection.UpdateOneAsync(gc => gc.UniqueId == cat.UniqueId, Builders<GoodsCategory>.Update.Set(gc => gc.GoodsKeys, cat.GoodsKeys));
            }

            await mongo.TerminalGoodsCollection.DeleteManyAsync(tg => tg.GoodsId == goods);
            return true;
        }

        /// <summary>
        ///     同步所有物品信息
        /// </summary> 
        [HttpPost]
        [ActionName("sync-all-goods")]
        public async Task<ApiBack> SyncAllGoodsAsync(string hospital)
        {
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseProxy = false, }))
            {
                var url = $"{Hospital.ApiAddress(nameof(ISyncObject.SyncAllGoods))}?hospital={hospital}";
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ApiBack>(json);
            }
        }
    }

}

// labels-print-mgr
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        /// <summary>
        ///     修改终端物品标签打印模板
        /// </summary>
        [HttpPut]
        [ActionName("modify-terminal-goods-LabelTemplate")]
        public async Task<bool> ModifyTerminalGoodsLabelTemplate(string id, string goodId, string color, bool isColor, string imageUrl)
        {
            string fileName = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            string writeUrl = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "EPSON_TEMPLATE"), fileName + ".png");
            string templateUrl = Path.Combine(Path.Combine("http://api.sframed.com:8000/", "wwwroot", "EPSON_TEMPLATE"), fileName + ".png");
            if (isColor)
            {
                using (Bitmap b1 = new Bitmap(1054, 376))
                {
                    using (Graphics g1 = Graphics.FromImage(b1))
                    {
                        Brush brush = new SolidBrush(ColorTranslator.FromHtml(color));      //Color.FromArgb(216, 204, 44)
                        g1.FillRectangle(brush, new Rectangle(0, 0, 1054, 376));
                    }
                    b1.Save(writeUrl, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            else
            {
                string img = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "EPSON_TEMPLATE"), "ImageTemplate", Path.GetFileName(imageUrl));
                using (Image image = Image.FromFile(img))
                {
                    image.Save(writeUrl, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            var oldurl = mongo.GoodsCollection.AsQueryable().Where(x => x.UniqueId == goodId).FirstOrDefault()?.LabelTemplate;
            if (!string.IsNullOrEmpty(oldurl))
            {
                writeUrl = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "EPSON_TEMPLATE"), Path.GetFileName(oldurl));
                if (File.Exists(writeUrl)) File.Delete(writeUrl);
            }
            await mongo.GoodsCollection.UpdateOneAsync(x => x.UniqueId == goodId, Builders<Goods>.Update.Set(x => x.LabelTemplate, templateUrl));
            await mongo.TerminalGoodsCollection.UpdateOneAsync(x => x.UniqueId == id, Builders<TerminalGoods>.Update.Set(x => x.Goods.LabelTemplate, templateUrl));
            return true;
        }

    }
}

// operation-schedule-mgr
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        [HttpGet]
        [ActionName("search-operation-schedules-by-date-range")]
        public OperationEvaluateProfile[] SearchOperationSchedulesByDateRange(DateTime begin, DateTime end, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;

            var opes = mongo.OperationScheduleCollection.AsQueryable().OrderBy(o => o.ApplyTime).Where(o => !o.IsDisabled && o.ApplyTime >= begin && o.ApplyTime < end).ToList();
            var roomIds = opes.Select(os => os.RoomId).Distinct().ToArray();
            var rooms = mongo.DepartmentCollection.AsQueryable().Where(d => roomIds.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).Select(d => new DepartmentProfile
            {
                UniqueId = d.UniqueId,
                Code = d.Code,
                DisplayName = d.DisplayName,
            }).ToArray();
            var emp = mongo.UserCollection.AsQueryable().Where(e => !e.IsDisabled).Select(e => e.Employee).ToList();

            return opes.Where(os => os.RoomId == department).Concat(opes.Where(os => os.RoomId != department))
                .Select(x => new OperationEvaluateProfile
                {
                    UniqueId = x.UniqueId,
                    IsCancelled = x.IsCancelled,
                    OperationType = x.OperationType,
                    ApplyTime = x.ApplyTime,
                    Patient = (x.Patient ?? new Patient { UniqueId = x.PatientId, }).ToPatientProfile(),
                    IdentityBarcode = x.IdentityBarcode,
                    ExecutionBeginTime = x.ExecutionBeginTime,
                    ExecutionEndTime = x.ExecutionEndTime,
                    Remark = x.Remark,
                    Room = rooms.FirstOrDefault(o => o.UniqueId == x.RoomId) ?? new DepartmentProfile { UniqueId = x.RoomId, },

                    PrimaryDoctor = (x.PrimaryDoctor ?? new Employee { UniqueId = x.PrimaryDoctorId, }).ToEmployeeProfile(),
                    Anesthetist = (x.Anesthetist ?? emp.FirstOrDefault(e => e.JobNo == x.AnesthetistId) ?? new Employee { UniqueId = x.AnesthetistId, }).ToEmployeeProfile(),
                    AnesthesiaMode = x.AnesthesiaMode,

                    PrimaryAssistant = (x.PrimaryAssistant ?? new Employee { UniqueId = x.PrimaryAssistantId, }).ToEmployeeProfile(),
                    PrimaryHandwashingNurse = (x.PrimaryHandwashingNurse ?? new Employee { UniqueId = x.PrimaryHandwashingNurseId, }).ToEmployeeProfile(),
                    PrimaryTourNurse = (x.PrimaryTourNurse ?? new Employee { UniqueId = x.PrimaryTourNurseId, }).ToEmployeeProfile(),
                    SecondaryAssistant = (x.SecondaryAssistant ?? new Employee { UniqueId = x.SecondaryAssistantId, }).ToEmployeeProfile(),
                    SecondaryHandwashingNurse = (x.SecondaryHandwashingNurse ?? new Employee { UniqueId = x.SecondaryHandwashingNurseId, }).ToEmployeeProfile(),
                    SecondaryTourNurse = (x.SecondaryTourNurse ?? new Employee { UniqueId = x.SecondaryTourNurseId, }).ToEmployeeProfile(),
                }).ToArray();
        }

        [HttpPut]
        [ActionName("modify-operation-schedule")]
        public async Task<bool> ModifyOperationScheduleAsync([FromBody]OperationSchedule schedule)
        {
            if (mongo.OperationScheduleCollection.AsQueryable().Any(o => o.UniqueId == schedule.UniqueId))
            {
                mongo.OperationScheduleCollection.UpdateOne(o => o.UniqueId == schedule.UniqueId, Builders<OperationSchedule>.Update
                .Set(o => o.IsCancelled, schedule.IsCancelled)
                .Set(o => o.RoomId, schedule.RoomId)
                .Set(o => o.ExecutionEndTime, schedule.ExecutionEndTime)
                .Set(o => o.Remark, schedule.Remark)
                .Set(o => o.AnesthetistId, schedule.AnesthetistId));
            }
            else
            {
                await mongo.OperationScheduleCollection.InsertOneAsync(schedule);
            }
            return true;
        }
    }
}