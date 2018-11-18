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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

#pragma warning disable CS1591

// setting-intelligent-storage
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class IdName
        {
            [JsonProperty("_id")]
            public string UniqueId { get; set; }
            public string DisplayName { get; set; }
        }

        /// <summary>
        ///     搜索所有客户的核心数据
        /// </summary>
        [HttpGet]
        [ActionName("search-all-customer-core")]
        public List<IdName> SearchAllCustomerCore(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var customer = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).Select(c => c.OwnerCode).FirstOrDefault();
            var data = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).Select(x => new IdName
            {
                UniqueId = x.UniqueId,
                DisplayName = x.DisplayName,
            }).ToList();
            return data.Where(d => d.UniqueId == customer).Concat(data.Where(d => d.UniqueId != customer)).ToList();
        }

        /// <summary>
        ///     搜索指定customer的智能药柜
        /// </summary>
        [HttpGet]
        [ActionName("search-all-cabinets-by-customer")]
        public List<CabinetDevice> SearchAllCabinetsByCustomer(string customer, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).Select(c => new { c.Cabinets, c.OutOfCabinets, }).ToList();
            var cabinets = cs.SelectMany(c => c.Cabinets).Concat(cs.SelectMany(c => c.OutOfCabinets)).ToList();
            var departIds = cabinets.Select(c => c.DepartmentId).Distinct().ToArray();
            var departs = mongo.DepartmentCollection.AsQueryable().Where(d => departIds.Contains(d.UniqueId)).ToList();
            foreach (var cabinet in cabinets)
            {
                cabinet.Department = departs.FirstOrDefault(d => d.UniqueId == cabinet.DepartmentId) ?? new Department { UniqueId = cabinet.DepartmentId, };
            }

            var fills = cabinets.SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).ToList();
            var goodsIds = fills.Select(f => f.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).ToList();
            foreach (var f in fills)
            {
                f.Goods = goods.FirstOrDefault(g => g.UniqueId == f.GoodsId) ?? new Goods { UniqueId = f.GoodsId, };
                f.BatchNumber = f.BatchNumber ?? string.Empty;
            }
            return cabinets.Where(c => c.Computer == Terminal).OrderBy(c => c.DisplayOrder).Concat(cabinets.Where(c => c.Computer != Terminal).OrderBy(c => c.Department?.Code ?? c.DepartmentId).ThenBy(c => c.DisplayOrder)).ToList();
        }

        /// <summary>
        ///     获取终端物品
        /// </summary>
        /// <param name="mode">All 所有，Usage 在药柜中配置的   不包括回收箱</param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-terminal-goods")]
        public TerminalGoods[] SearchTerminalGoods(string mode, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var customer = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).ToList();
            var fills = customer.SelectMany(c => c.Drawers).Where(d => !d.IsRecycleBin).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).ToArray();
            var tGoods = mongo.TerminalGoodsCollection.AsQueryable().Where(t => t.Computer == Terminal).ToList().Select(t =>
            {
                t.StorageQuota = double.IsNaN(t.StorageQuota) ? 0.0 : t.StorageQuota;
                t.WarningQuota = double.IsNaN(t.WarningQuota) ? 0.0 : t.WarningQuota;
                t.Goods = t.Goods ?? new Goods { UniqueId = t.GoodsId, };
                return t;
            }).ToArray();

            switch (mode)
            {
                case "All": /* do nothing */ break;
                case "Usage": tGoods = tGoods.Join(fills.Select(f => f.GoodsId).Distinct(), tg => tg.GoodsId, g => g, (tg, g) => tg).ToArray(); break;
                default: tGoods = new TerminalGoods[0]; break;
            }
            foreach (var tg in tGoods)
            {
                tg.CurrentQuota = fills.Where(f => f.GoodsId == tg.GoodsId).Sum(f => f.QtyExisted);
            }
            return tGoods.OrderByDescending(t => t.CurrentQuota).ThenBy(t => t.Goods.Pinyin).ToArray();
        }

        /// <summary>
        ///     获取标签图片打印模板
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [ActionName("print-label-template")]
        public string[] PrintLabelTemplate()
        {
            string template = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "EPSON_TEMPLATE"), "ImageTemplate");
            List<string> imgs = new List<string>();
            if (Directory.Exists(template))
            {
                DirectoryInfo folder = new DirectoryInfo(template);
                var folders = folder.GetFiles("*.png");
                foreach (var file in folders)
                {
                    string Url = Path.Combine(Path.Combine("http://api.sframed.com:8000/", "wwwroot", "EPSON_TEMPLATE"), "ImageTemplate/" + file.Name);
                    imgs.Add(Url);
                }
            }
            return imgs.ToArray();
        }

        public class CategoryGoods : CategoryProfile
        {
            public GoodsProfile GoodsProfile { get; set; }
        }

        [HttpGet]
        [ActionName("search-goods-profiles-by-category")]
        public CategoryGoods[] SearchGoodsProfilesByCategory(string category)
        {
            var cat = mongo.GoodsCategoryCollection.AsQueryable().FirstOrDefault(gc => gc.UniqueId == category);
            if (cat == null)
            {
                return new CategoryGoods[0];
            }

            return mongo.GoodsCollection.AsQueryable().Where(g => cat.GoodsKeys.Contains(g.UniqueId)).ToList()
                .Select(g => new CategoryGoods
                {
                    UniqueId = cat.UniqueId,
                    DisplayName = cat.DisplayName,
                    Background = cat.Background,
                    Foreground = cat.Foreground,
                    GoodsProfile = g.ToGoodsProfile(string.Empty, DateTime.MaxValue.Date),
                }).ToArray();
        }

        /// <summary>
        ///     修改柜子存储信息，前提需配置customer关系
        /// </summary>
        [HttpPut]
        [ActionName("modify-customer-cabinets")]
        public async Task<bool> ModifyCustomerCabinetsAsync(string customer, [FromBody] CabinetDevice[] cabinets)
        {
            var find = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).FirstOrDefault();
            if (find == null)
            {
                return false;
            }

            foreach (var index in cabinets.Select(cabinet =>
            {
                var index = find.Cabinets.Concat(find.OutOfCabinets).ToList().FindIndex(f => f.No == cabinet.No);
                if (index < 0)
                {
                    return -1;
                }
                if (index < find.Cabinets.Count)
                {
                    find.Cabinets[index] = cabinet;
                }
                else
                {
                    find.OutOfCabinets[index - find.Cabinets.Count] = cabinet;
                }
                return index;
            }).Where(x => x >= 0))
            {
                if (index < find.Cabinets.Count)
                {
                    await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == customer, Builders<Customer>.Update.Set(x => x.Cabinets[index], find.Cabinets[index]));
                }
                else
                {
                    await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == customer, Builders<Customer>.Update.Set(x => x.OutOfCabinets[index - find.Cabinets.Count], find.OutOfCabinets[index - find.Cabinets.Count]));
                }
            }

#pragma warning disable CS4014
            var computers = cabinets.Where(c => c.IsControlled).Select(c => c.Computer).Where(c => !string.IsNullOrEmpty(c)).Distinct();
            if (computers.Any())
            {
                Task.Factory.StartNew(() =>
                {
                    var version = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-ffffff");
                    Parallel.ForEach(computers, computer =>
                    {
                        var url = $"http://{computer}:8002/api/bizcore/customer-cabinets?version={version}";
                        try
                        {
                            var key = $"{computer}:{Helper.CabinetVersion}";
                            var config = mongo.SystemConfigCollection.AsQueryable().FirstOrDefault(f => f.Key == key) ?? new SystemConfig { Key = key, };
                            config.JObject = version;
                            mongo.SystemConfigCollection.FindOneAndReplace<SystemConfig>(x => x.UniqueId == config.UniqueId, config, new FindOneAndReplaceOptions<SystemConfig, SystemConfig> { IsUpsert = true });

                            // 药柜配置修改后，发送到客户端
                            var body = find.Cabinets.Where(c => c.Computer == computer).ToArray();
                            var b = new HttpRequest().Put<bool>(url, body);
                            Global.GlobalLogger.Info($"issued {url} => {b}");
                        }
                        catch (Exception ex)
                        {
                            Global.GlobalLogger.Error(url, ex);
                        }
                    });
                });
            }
#pragma warning restore CS4014
            return true;
        }
    }

}

// setting-storage-unit
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        /// <summary>
        ///     搜索终端智能药柜
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-all-cabinets-for-terminal")]
        public List<CabinetDevice> SearchAllCabinetsForTerminal(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).Select(c => new
            {
                Cabinets = c.Cabinets.Where(x => x.Computer == Terminal),
                OutOfCabinets = c.OutOfCabinets.Where(x => x.DepartmentId == department),
            }).ToList();
            var cabinets = cs.SelectMany(c => c.Cabinets).OrderBy(c => c.DisplayOrder).Concat(cs.SelectMany(v => v.OutOfCabinets).OrderBy(v => v.DisplayOrder)).ToList();
            var fills = cabinets.SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).ToList();
            var goodsIds = fills.Select(f => f.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).ToList();
            foreach (var f in fills)
            {
                f.Goods = goods.FirstOrDefault(x => x.UniqueId == f.GoodsId) ?? new Goods { UniqueId = f.GoodsId, };
                f.BatchNumber = f.BatchNumber ?? string.Empty;
            }
            return cabinets;
        }

        // searchTerminalGoods All

        // modifyCustomerCabinets
    }

}

// setting-goods-scope
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        //    searchAllCustomerCore

        //    searchDepartmentsOwnComputer

        //    searchTerminalGoods

        public class CategoryProfile : IdName
        {
            public string Foreground { get; set; }
            public string Background { get; set; }
        }

        [HttpGet]
        [ActionName("search-category-profiles-by-customer")]
        public CategoryProfile[] SearchCategoryProfilesByCustomer(string customer)
        {
            return mongo.GoodsCategoryCollection.AsQueryable().Where(x => x.CustomerId == customer).OrderBy(x => x.DisplayOrder)
                .Select(c => new CategoryProfile
                {
                    UniqueId = c.UniqueId,
                    DisplayName = c.DisplayName,
                    Foreground = c.Foreground,
                    Background = c.Background,
                }).ToArray();
        }

        [HttpGet]
        [ActionName("search-goods-outside-terminal")]
        public Pager<Goods> SearchGoodsOutsideTerminal(string content = null, string terminal = null, int take = -1, int index = 0)
        {
            Terminal = terminal ?? Terminal;
            var goods = mongo.TerminalGoodsCollection.AsQueryable().Where(t => t.Computer == Terminal).Select(t => t.GoodsId).ToList();
            var goodsLq = mongo.GoodsCollection.AsQueryable().Where(g => !g.IsDisabled && !goods.Contains(g.UniqueId));
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                goodsLq = goodsLq.Where(g => g.DisplayName.ToLower().Contains(content) || g.Pinyin.ToLower().Contains(content) || g.PinyinFull.ToLower().Contains(content) || g.Specification.ToLower().Contains(content) || g.Manufacturer.ToLower().Contains(content));
            }
            var count = goodsLq.Count();
            var data = (take > 0 ? goodsLq.Skip(index * take).Take(take) : goodsLq).ToArray();
            return new Pager<Goods> { Count = count, Data = data, };
        }

        /// <summary>
        ///     分配终端物品
        /// </summary>
        /// <param name="terminal">终端</param>
        /// <param name="appends">物品标识</param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-goods-for-terminal")]
        public async Task<bool> ModifyGoodsForTerminalAsync(string terminal, [FromBody] TerminalGoods[] appends)
        {
            var insides = mongo.TerminalGoodsCollection.AsQueryable().Where(t => t.Computer == terminal).ToList();
            foreach (var item in insides)
            {
                // 修改已有的数据
                var find = appends.FirstOrDefault(f => f.GoodsId == item.GoodsId);
                if (find != null)
                {
                    item.StorageQuota = find.StorageQuota;
                    item.WarningQuota = find.WarningQuota;
                }
            }
            // 添加新来的数据
            var outsides = appends.Where(c => insides.All(t => t.GoodsId != c.GoodsId)).ToList();
            var currents = insides.Concat(outsides).ToList();

            foreach (var item in currents)
            {
                item.Goods = item.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == item.GoodsId);
                var category = mongo.GoodsCategoryCollection.AsQueryable().FirstOrDefault(f => f.GoodsKeys.Contains(item.UniqueId));
                if (category != null)
                {
                    item.Background = category.Background;
                    item.Foreground = category.Foreground;
                }
                await mongo.TerminalGoodsCollection.FindOneAndReplaceAsync<TerminalGoods>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<TerminalGoods, TerminalGoods> { IsUpsert = true });
            }

            return currents.Any();
        }

        /// <summary>
        ///     修改终端物品信息
        /// </summary>
        [HttpPut]
        [ActionName("modify-terminal-goods")]
        public async Task<string> ModifyTerminalGoodsAsync([FromBody] TerminalGoods tg)
        {
            tg.UniqueId = tg.UniqueId ?? SfraObject.GenerateId();
            tg.Goods = tg.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == tg.GoodsId);
            await mongo.TerminalGoodsCollection.FindOneAndReplaceAsync<TerminalGoods>(x => x.UniqueId == tg.UniqueId, tg, new FindOneAndReplaceOptions<TerminalGoods, TerminalGoods> { IsUpsert = true });
            return tg.UniqueId;
        }

        [HttpDelete]
        [ActionName("remove-terminal-goods")]
        public async Task<bool> RemoveTerminalGoodsAsync(string terminal, string goods)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            if (string.IsNullOrEmpty(department))
            {
                return false;
            }

            var fills = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == terminal).ToList()
                .SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).ToList();
            if (fills.Where(f => f.GoodsId == goods).Any(f => f.QtyExisted > 0))
            {
                // 有库存不允许删除
                return false;
            }

            await mongo.TerminalGoodsCollection.DeleteOneAsync(t => t.Computer == terminal && t.GoodsId == goods);
            // 不更新 Customer 中药盒的 Fills
            return true;
        }
    }
}
