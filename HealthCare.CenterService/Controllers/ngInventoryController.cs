//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.CenterService.Algorithm;
using HealthCare.Data;
using HealthCare.MongoData;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

#pragma warning disable CS1591, CS8321

// search-pharmacy-allot
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class SyncAllocProfile
        {
            [JsonProperty("_id")]
            public string UniqueId { get; set; }
            public string ApplyId { get; set; }
            public DepartmentProfile DepartmentSource { get; set; }
            public DepartmentProfile DepartmentDestination { get; set; }
            public GoodsProfile Goods { get; set; }
            public ExchangeMode Mode { get; set; }
            public double ApplyQty { get; set; }
        }

        /// <summary>
        ///     同步指定调拨单的调拨数据
        /// </summary> 
        [HttpPost]
        [ActionName("sync-allocations-by-stock-no")]
        public async Task<ApiBack<SyncAllocProfile[]>> SyncAllocationsByStockNoAsync(string hospital, string stock)
        {
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseProxy = false, }))
            {
                var url = $"{Hospital.ApiAddress(nameof(ISyncObject.SyncAllocationsByStockNo))}?hospital={hospital}&stock={stock}";
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeObject<ApiBack<List<string>>>(json);
                var allocs = new SyncAllocProfile[0];
                if (obj.Code == 0 && obj.Data.Any())
                {
                    var finds = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).ToList();
                    allocs = mongo.AllocationCollection.AsQueryable().Where(a => obj.Data.Contains(a.UniqueId)).ToList()
                        .Select(a => new SyncAllocProfile
                        {
                            UniqueId = a.UniqueId,
                            ApplyId = a.ApplyId,
                            DepartmentSource = new DepartmentProfile
                            {
                                UniqueId = a.DepartmentSourceId,
                                DisplayName = a.DepartmentSource?.DisplayName,
                                Code = a.DepartmentSource?.Code ?? a.DepartmentSourceId,
                                Computer = finds.Where(c => c.DepartmentId == a.DepartmentSourceId).Select(c => c.Computer).FirstOrDefault(),
                            },
                            DepartmentDestination = new DepartmentProfile
                            {
                                UniqueId = a.DepartmentDestinationId,
                                DisplayName = a.DepartmentDestination?.DisplayName,
                                Code = a.DepartmentDestination?.Code ?? a.DepartmentDestinationId,
                                Computer = finds.Where(c => c.DepartmentId == a.DepartmentDestinationId).Select(c => c.Computer).FirstOrDefault(),
                            },
                            Goods = a.Goods.ToGoodsProfile(a.BatchNumber, a.ExpiredDate.Date),
                            Mode = a.Mode,
                            ApplyQty = a.ApplyQty,
                        }).ToArray();
                }
                return new ApiBack<SyncAllocProfile[]> { Code = obj.Code, Msg = obj.Msg, Data = allocs, };
            }
        }

        /// <summary>
        ///     校正 HIS 调拨，批号、有效期
        /// </summary>
        [ActionName("modify-allocation-for-correct")]
        [HttpPut]
        public async Task<bool> ModifyAlloctionForCorrectAsync(string allocation, string batch, DateTime expired)
        {
            await mongo.AllocationCollection.UpdateOneAsync(x => x.UniqueId == allocation, Builders<Allocation>.Update.Set(x => x.BatchNumber, batch).Set(x => x.ExpiredDate, expired));
            return true;
        }
    }

}

// generate-pharmacy-allot
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        // searchAllCustomerCore

        // searchIntelligentCabinetsByCustomer

        public class InventoryProfile : DepartmentProfile
        {
            public double QtyGaps { get; set; }
            public bool Warnning { get; set; }
        }

        /// <summary>
        ///     各个病区缺药的汇总值
        /// </summary>
        [HttpGet]
        [ActionName("search-departments-own-computer-with-inventory")]
        public List<InventoryProfile> SearchDepartmentsOwnComputerWithInventory(string customer, string hospital, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var find = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).FirstOrDefault();
            if (find == null)
            {
                return new List<InventoryProfile>();
            }

            // 自己不能给自己调拨
            var department = DepartmentId;
            string[] departs;
            if (ServiceStartup.GetPrimaryAuthorized(Terminal).Kernel)
            {
                departs = find.Cabinets.Select(c => c.DepartmentId).Distinct().Where(c => c != department).ToArray();
            }
            else
            {
                var keys = ServiceStartup.GetAuthorized(Terminal).Select(u => $"{u.UserId}:{Helper.AllowedDepartments}").ToList();
                departs = mongo.SystemConfigCollection.AsQueryable().Where(c => keys.Contains(c.Key)).Select(c => c.JObject).ToArray()
                    .SelectMany(c => JsonConvert.DeserializeObject<List<string>>(c)).Distinct().Where(c => c != department).ToArray();
                if (departs.Length <= 0)
                {
                    departs = find.Cabinets.Select(c => c.DepartmentId).Distinct().Where(c => c != department).ToArray();
                }
            }

            var goodsIds = mongo.GoodsCategoryCollection.AsQueryable().Where(c => c.CustomerId == customer && c.PermitAlloc).SelectMany(c => c.GoodsKeys).ToList();
            var tGoods = mongo.TerminalGoodsCollection.AsQueryable().Where(t => goodsIds.Contains(t.GoodsId)).ToList();
            var allocs = mongo.AllocationCollection.AsQueryable().Where(a => !a.IsDisabled && a.ExchangeId != null && a.FinishTime == null).ToList();  // 仅查询调拨指派生成且未执行

            var data = mongo.DepartmentCollection.AsQueryable().Where(d => departs.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).ToList().Select(d => new DepartmentProfile
            {
                UniqueId = d.UniqueId,
                DisplayName = d.DisplayName,
                Code = d.Code,
                Computer = find.Cabinets.Where(c => c.DepartmentId == d.UniqueId).Select(x => x.Computer).FirstOrDefault(),
            }).Select(dpt =>
            {
                var tgs = tGoods.Join(find.Cabinets.Where(c => c.DepartmentId == dpt.UniqueId).Select(x => x.Computer).Distinct(), a => a.Computer, b => b, (a, b) => a).ToList();
                var fills = find.Cabinets.Where(c => c.DepartmentId == dpt.UniqueId).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).ToList();
                var gps = fills.GroupBy(o => o.GoodsId).Select(g =>
                {
                    var x = tgs.FirstOrDefault(f => f.GoodsId == g.Key);
                    if (x == null)
                    {
                        return (gaps: 0.0, warn: false);
                    }
                    var dpts = VisitorDepartments(hospital, dpt.UniqueId);
                    // 添加调拨指派中的数据
                    var exists = g.Sum(o => o.QtyExisted) + allocs.Where(a => dpts.Contains(a.DepartmentDestinationId) && a.GoodsId == g.Key).Sum(a => a.ReceivedTime == null ? a.ApplyQty : a.ApplyQty - a.QtyActual);
                    var supply = x.StorageQuota - exists;
                    return (gaps: supply > 0 ? supply : 0.0, warn: exists <= x.WarningQuota);
                }).ToArray();

                return new InventoryProfile { UniqueId = dpt.UniqueId, DisplayName = dpt.DisplayName, Code = dpt.Code, Computer = dpt.Computer, QtyGaps = gps.Sum(x => x.gaps), Warnning = gps.Any(x => x.warn), };
            }).ToList();

            var summary = new InventoryProfile { UniqueId = null, DisplayName = "汇总", Code = null, Computer = null, QtyGaps = data.Sum(d => d.QtyGaps), Warnning = false, };
            return new[] { summary }.Concat(data.Where(d => d.QtyGaps > 0)).Concat(data.Where(d => d.QtyGaps <= 0)).ToList();
        }
        public class InventorySummary
        {
            public GoodsProfile Goods { get; set; }
            public double QtyExisted { get; set; }
            public double QtyWarnning { get; set; }
            public double QtyMax { get; set; }
            public ExpiredAndBatch[] ExpiredAndBatch { get; set; }
        }

        public class ExpiredAndBatch
        {
            public string BatchNumber { get; set; }
            public DateTime ExpiredDate { get; set; }
            public double SumQty { get; set; }
        }

        /// <summary>
        ///     病区缺药的详情值
        /// </summary>
        [HttpPost]
        [ActionName("search-inventory-detail-for-departments")]
        public Pager<InventorySummary> SearchInventoryDetailForDepartments(string customer, string hospital, [FromBody] string[] departments, int take = -1, int index = 0, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).ToList();
            if (customers.Count <= 0)
            {
                return new Pager<InventorySummary>();
            }

            var goodsIds = mongo.GoodsCategoryCollection.AsQueryable().Where(c => c.CustomerId == customer && c.PermitAlloc).SelectMany(c => c.GoodsKeys).ToList();
            var tGoods = mongo.TerminalGoodsCollection.AsQueryable().Where(t => goodsIds.Contains(t.GoodsId)).ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).ToList();
            var allocs = mongo.AllocationCollection.AsQueryable().Where(a => !a.IsDisabled && a.ExchangeId != null && a.FinishTime == null).ToList();  // 仅查询调拨指派生成且未执行

            var localFills = customers.SelectMany(c => c.Cabinets).Concat(customers.SelectMany(c => c.OutOfCabinets))
                .Where(c => c.DepartmentId == department).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).ToList();
            var fillsGroup = localFills.GroupBy(f => new { BatchNumber = f.BatchNumber ?? string.Empty, ExpiredDate = f.ExpiredDate.Date }).Where(f => f.Any(a => a.QtyExisted > 0)).ToList();

            var data = mongo.DepartmentCollection.AsQueryable().Where(d => departments.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).ToList().Select(d => new DepartmentProfile
            {
                UniqueId = d.UniqueId,
                DisplayName = d.DisplayName,
                Code = d.Code,
                Computer = customers.SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == d.UniqueId).Select(c => c.Computer).FirstOrDefault(),
            }).SelectMany(dpt =>
            {
                var tgs = tGoods.Join(customers.SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == dpt.UniqueId).Select(c => c.Computer).Distinct(), a => a.Computer, b => b, (a, b) => a).ToList();
                var fills = customers.SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == dpt.UniqueId).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).ToList();
                return fills.GroupBy(o => o.GoodsId).Select(g =>
                {
                    var dpts = VisitorDepartments(hospital, dpt.UniqueId);
                    // 添加调拨指派中的数据
                    var allocQty = allocs.Where(a => dpts.Contains(a.DepartmentDestinationId) && a.GoodsId == g.Key).Sum(a => a.ReceivedTime == null ? a.ApplyQty : a.ApplyQty - a.QtyActual);
                    var exist = g.Sum(o => o.QtyExisted) + allocQty;
                    var quota = tgs.FirstOrDefault(f => f.GoodsId == g.Key)?.StorageQuota ?? 0.0;
                    var warn = tgs.FirstOrDefault(f => f.GoodsId == g.Key)?.WarningQuota ?? 0.0;
                    return quota > exist ? new { GoodsId = g.Key, QtyExisted = exist, QtyMax = quota, QtyWarnning = double.IsNaN(warn) ? 0.0 : warn, } : null;
                }).Where(g => g != null);
            }).GroupBy(s => s.GoodsId).Select(s => new InventorySummary
            {
                Goods = (goods.FirstOrDefault(f => f.UniqueId == s.Key) ?? new Goods { UniqueId = s.Key, }).ToGoodsProfile(string.Empty, DateTime.MaxValue.Date),
                QtyExisted = s.Sum(o => o.QtyExisted),
                QtyWarnning = s.Sum(o => o.QtyWarnning),
                QtyMax = s.Sum(o => o.QtyMax),
                ExpiredAndBatch = fillsGroup.Select(f => new ExpiredAndBatch
                {
                    BatchNumber = f.Key.BatchNumber,
                    ExpiredDate = f.Key.ExpiredDate,
                    SumQty = f.Where(a => a.GoodsId == s.Key).Sum(g => g.QtyExisted)
                }).Where(f => f.SumQty > 0).ToArray(),
            });

            return new Pager<InventorySummary> { Count = data.Count(), Data = (take > 0 ? data.Skip(take * index).Take(take) : data).ToArray(), };
        }

        public class UnReceivedSFRAAlloc : DepartmentProfile
        {
            public int Count { get; set; }
        }

        [HttpGet]
        [ActionName("search-departments-with-unreveived-sfra-allocations")]
        public List<UnReceivedSFRAAlloc> SearchDepartmentsWithUnReceivedSFRAAllocations(string customer, string hospital, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var find = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).FirstOrDefault();
            if (find == null)
            {
                return new List<UnReceivedSFRAAlloc>();
            }

            string[] departs;
            if (ServiceStartup.GetPrimaryAuthorized(Terminal).Kernel)
            {
                departs = find.Cabinets.Select(c => c.DepartmentId).Distinct().ToArray();
            }
            else
            {
                var keys = ServiceStartup.GetAuthorized(Terminal).Select(u => $"{u.UserId}:{Helper.AllowedDepartments}").ToArray();
                departs = mongo.SystemConfigCollection.AsQueryable().Where(c => keys.Contains(c.Key)).Select(c => c.JObject).ToArray()
                    .SelectMany(c => JsonConvert.DeserializeObject<List<string>>(c)).Distinct().ToArray();
                if (departs.Length <= 0)
                {
                    departs = find.Cabinets.Select(c => c.DepartmentId).Distinct().Where(c => c != department).ToArray();
                }
            }

            var allocsDsts = mongo.AllocationCollection.AsQueryable().Where(a => !a.IsDisabled && departs.Contains(a.DepartmentDestinationId) && a.ExchangeId != null && a.Receiver == null).Select(a => a.DepartmentDestinationId).ToList();
            var dpts = mongo.DepartmentCollection.AsQueryable().Where(d => departs.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).ToList().Select(d => new UnReceivedSFRAAlloc
            {
                UniqueId = d.UniqueId,
                DisplayName = d.DisplayName,
                Code = d.Code,
                Computer = find.Cabinets.Where(c => c.DepartmentId == d.UniqueId).Select(c => c.Computer).FirstOrDefault(),
                Count = allocsDsts.Count(dst => dst == d.UniqueId),
            }).ToList();
            var allocSummary = new UnReceivedSFRAAlloc { UniqueId = null, DisplayName = "汇总", Computer = null, Code = null, Count = dpts.Sum(d => d.Count), };
            return new[] { allocSummary }.Concat(dpts.Where(d => d.Count > 0)).Concat(dpts.Where(d => d.Count <= 0)).ToList();
        }

        public class SfraAlloc
        {
            [JsonProperty("_id")]
            public string UniqueId { get; set; }
            public string ApplyId { get; set; }
            public double ApplyQty { get; set; }
            public IdName Department { get; set; }
            public GoodsProfile Goods { get; set; }
        }

        /// <summary>
        ///     查询 SFRA 调拨记录（未验收 或 验收量不等于申请量）
        /// </summary>
        [HttpPost]
        [ActionName("search-unreveived-sfra-allocations")]
        public Pager<SfraAlloc> SearchUnReceivedSFRAAllocations(string customer, [FromBody] string[] departments, int take = -1, int index = 0)
        {
            var allocs = mongo.AllocationCollection.AsQueryable().Where(a => !a.IsDisabled && departments.Contains(a.DepartmentDestinationId) && a.ExchangeId != null && a.Receiver == null).ToList()
                .Select(a =>
                {
                    a.Goods = a.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == a.GoodsId) ?? new Goods { UniqueId = a.GoodsId, };
                    return new SfraAlloc
                    {
                        UniqueId = a.UniqueId,
                        ApplyId = a.ApplyId,
                        ApplyQty = a.ApplyQty,
                        Department = new IdName { UniqueId = a.DepartmentDestinationId, DisplayName = a.DepartmentDestination?.DisplayName, },
                        Goods = a.Goods.ToGoodsProfile(a.BatchNumber, a.ExpiredDate.Date),
                    };
                });
            return new Pager<SfraAlloc> { Count = allocs.Count(), Data = (take > 0 ? allocs.Skip(take * index).Take(take) : allocs).ToArray(), };
        }

        /// <summary>
        ///     申请流转
        /// </summary>
        /// <param name="exchanges"></param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-exchanges-for-apply")]
        public async Task<string[]> ModifyExchangesForApplyAsync([FromBody] Exchange[] exchanges, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var customer = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).Select(c => c.OwnerCode).FirstOrDefault();

            foreach (var exchange in exchanges)
            {
                exchange.UniqueId = exchange.UniqueId ?? SfraObject.GenerateId();
                exchange.Goods = string.IsNullOrEmpty(exchange.GoodsId) ? null : mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == exchange.GoodsId);
                exchange.BatchNumber = exchange.BatchNumber ?? string.Empty;
                exchange.CustomerId = customer;
                exchange.Computer = Terminal;
                await mongo.ExchangeCollection.FindOneAndReplaceAsync<Exchange>(x => x.UniqueId == exchange.UniqueId, exchange, new FindOneAndReplaceOptions<Exchange, Exchange> { IsUpsert = true });
            }

            return exchanges.Select(e => e.UniqueId).ToArray();
        }

        /// <summary>
        ///     SFRA 生成调拨单
        /// </summary>
        [HttpPut]
        [ActionName("modify-sfra-allocation")]
        public async Task<int> ModifySFRAAllocationAsync(string customer, string exchange, [FromBody] string[] departments, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var cstmr = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).FirstOrDefault();
            var department = DepartmentId;
            var find = mongo.ExchangeCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == exchange);
            if (cstmr == null || department == null || find == null)
            {
                return 0;
            }

            var tGoods = mongo.TerminalGoodsCollection.AsQueryable().Where(t => t.GoodsId == find.GoodsId).ToList();
            var applyId = DateTime.Now.ToString("yyyyMMddHHmmss");
            var departs = mongo.DepartmentCollection.AsQueryable().Where(d => departments.Contains(d.UniqueId)).OrderBy(d => d.DisplayOrder).ToArray();

            var allocs = departs.Select(d => new DepartmentProfile
            {
                UniqueId = d.UniqueId,
                DisplayName = d.DisplayName,
                Code = d.Code,
                Computer = cstmr.Cabinets.Where(c => c.DepartmentId == d.UniqueId).Select(c => c.Computer).FirstOrDefault(),
            }).SelectMany(dpt =>
            {
                var tg = tGoods.Join(cstmr.Cabinets.Where(c => c.DepartmentId == dpt.UniqueId).Select(c => c.Computer).Distinct(), a => a.Computer, b => b, (a, b) => a).FirstOrDefault();
                var fills = cstmr.Cabinets.Where(c => c.DepartmentId == dpt.UniqueId).SelectMany(c => c.Drawers).Where(d => !d.IsRecycleBin).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).ToList();
                var spaceQty = (tg?.StorageQuota ?? 0) - fills.Where(f => f.GoodsId == find.GoodsId).Sum(f => f.QtyExisted);
                if (tg == null || spaceQty <= 0)
                {
                    // dpt 不能存储该物品 或 无空余存储空间
                    return new Allocation[0];
                }

                return find.Plans.Where(p => p.IsExecuted && p.Qty > 0).Select(plan =>
                {
                    var qty = Math.Min(spaceQty, plan.Qty);
                    if (qty <= 0)
                    {
                        return null;
                    }
                    spaceQty -= qty;
                    plan.Qty -= qty;
                    var fill = plan.Box.Fills.FirstOrDefault(f => f.GoodsId == find.GoodsId);
                    return new Allocation
                    {
                        UniqueId = $"{exchange}@{dpt.UniqueId}",    // 让主键有含义，可避免重复生成
                        ExchangeId = exchange,

                        ApplyId = applyId,
                        ApplyQty = qty,
                        DeliveryNumber = applyId,
                        DepartmentSourceId = department,
                        DepartmentSource = departs.FirstOrDefault(o => o.UniqueId == department),
                        DepartmentDestinationId = dpt.UniqueId,
                        DepartmentDestination = departs.FirstOrDefault(o => o.UniqueId == dpt.UniqueId),
                        GoodsId = find.GoodsId,
                        Goods = find.Goods,
                        BatchNumber = fill?.BatchNumber ?? string.Empty,
                        ExpiredDate = (fill?.ExpiredDate ?? DateTime.MaxValue).Date,
                        Mode = ExchangeMode.CheckIn,
                        Qty = 0,

                        CustomerId = customer,
                        Computer = dpt.Computer,
                        RecordType = "SFRA 调拨入库",
                        TimeFilter = DateTime.Now.Date,
                    };
                }).Where(a => a != null).ToArray();
            }).ToArray();

            foreach (var item in allocs)
            {
                await mongo.AllocationCollection.FindOneAndReplaceAsync<Allocation>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<Allocation, Allocation> { IsUpsert = true });
            }
            return allocs.Length;
        }


        /// <summary>
        ///     所在的位置是否有药柜
        /// </summary>
        [HttpGet]
        [ActionName("search-if-exist-cabinet")]
        public bool SearchIfExistCabinet(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            return mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Any(c => c.Computer == Terminal);
        }

        /// <summary>
        ///     生成虚拟位置的流转信息, 主要用于录入柜外药品 或者 本地无药柜时的虚拟调拨
        /// </summary>
        [HttpPut]
        [ActionName("modify-exchanges-for-outside-goods")]
        public async Task<string[]> ModifyMedicationsForOutsideGoodsAsync(string hospital, string collection, [FromBody] JObject[] exchanges, string terminal = null)
        {
            exchanges = exchanges ?? new JObject[0];
            var customer = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).Select(c => c.OwnerCode).FirstOrDefault();
            var array = exchanges.Select(e => collection == nameof(Exchange) ? JsonConvert.DeserializeObject<Exchange>(e.ToString()) : collection == nameof(Medication) ? JsonConvert.DeserializeObject<Medication>(e.ToString()) : null).ToArray();
            foreach (var e in array)
            {
                e.Plans = new List<ActionPlan>
                {
                    new ActionPlan
                    {
                        Mode = e.Mode,
                        Box = new BoxDevice
                        {
                            BoxMode = BoxMode.VirtualBox,
                            DisplayText = "虚拟位置",
                            IsControlled = false,
                            Fills = new List<NodeGoodsInfo>
                            {
                                new NodeGoodsInfo
                                {
                                    GoodsId = e.GoodsId,
                                    QtyExisted = e.Qty,
                                    BatchNumber = e.BatchNumber,
                                    ExpiredDate = e.ExpiredDate,
                                    QtyMax = e.Qty,
                                },
                            },
                        },
                        Qty = e.Qty,
                        IsExecuted = true,
                    },
                };
                e.UniqueId = e.UniqueId ?? SfraObject.GenerateId();
                e.CustomerId = customer;
                e.Computer = Terminal;
                if (e is Medication m)
                {
                    m.Doctor = string.IsNullOrEmpty(m.DoctorId) ? null : mongo.EmployeeCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == m.DoctorId);
                    m.Patient = string.IsNullOrEmpty(m.PatientId) ? null : mongo.PatientCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == m.PatientId);
                }
                e.Goods = string.IsNullOrEmpty(e.GoodsId) ? null : mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == e.GoodsId);
                e.BatchNumber = e.BatchNumber ?? string.Empty;
                switch (collection)
                {
                    case nameof(Exchange):
                        await mongo.ExchangeCollection.FindOneAndReplaceAsync<Exchange>(x => x.UniqueId == e.UniqueId, e, new FindOneAndReplaceOptions<Exchange, Exchange> { IsUpsert = true });
                        break;
                    case nameof(Medication):                      
                        await mongo.MedicationCollection.FindOneAndReplaceAsync<Medication>(x => x.UniqueId == ((Medication)e).UniqueId, (Medication)e, new FindOneAndReplaceOptions<Medication, Medication> { IsUpsert = true });
                        break;
                }
            }
            return array.Select(o => o.UniqueId).ToArray();
        }
    }

}

// pharmacy-internal-transfer
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        [HttpGet]
        [ActionName("search-qty-for-internal-transfer")]
        public double SearchQtyForInternalTransfer(string cabinet, string goods, string batch, DateTime expired, double qty, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;

            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).Select(c => new
            {
                Cabinets = c.Cabinets.Where(o => o.Computer == Terminal),
                OutOfCabinets = c.OutOfCabinets.Where(o => o.DepartmentId == department),
            }).ToList();

            var algo = new Algorithm.LocateBoxAlgo();
            // 柜外到柜内 => 查询柜内能存储的最大数量
            // 柜内到柜外 => 查询柜外能存储的最大数量
            var cabinets = cs.SelectMany(c => c.OutOfCabinets).Any(c => c.No == cabinet) ? cs.SelectMany(c => c.Cabinets)
                : cs.SelectMany(c => c.Cabinets).Any(c => c.No == cabinet) ? cs.SelectMany(c => c.OutOfCabinets) : new List<CabinetDevice>();
            var fills = cabinets.SelectMany(v => v.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).Where(f => algo.CanCheckIn(f, goods, batch, expired)).ToList();
            return Math.Min(qty, fills.Sum(o => o.QtyMax - o.QtyExisted));
        }

        /// <summary>
        ///     柜外往柜内调整的内部流转
        /// </summary>
        /// <param name="allocations">CheckIn</param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-internal-allocations-for-virtual-to-cabinet")]
        public async Task<string[]> ModifyInternalAllocationsForVirtualToCabinetAsync([FromBody] InternalAllocation[] allocations, string terminal = null)
        {
            if (allocations.Any(a => a.Mode != ExchangeMode.CheckIn))
            {
                return new string[0];
            }

            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).Select(c => new
            {
                Cabinets = c.Cabinets.Where(o => o.Computer == Terminal),
                OutOfCabinets = c.OutOfCabinets.Where(o => o.DepartmentId == department),
            }).ToList();
            var owner = cs.SelectMany(c => c.Cabinets).Select(f => f.OwnerCode).FirstOrDefault();
            var applyId = DateTime.Now.ToString("yyyyMMddHHmmss");
            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal);

            foreach (var a in allocations)
            {
                a.CustomerId = owner;
                a.Computer = Terminal;
                a.Goods = a.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == a.GoodsId);
                a.ApplyId = applyId;
                a.OperatorId = primary.LoginId;
                a.OperatorName = primary.DisplayName;
            }

            // 去除转出设备
            var nos = allocations.Select(a => a.TurnOutDevice).ToArray();
            var excluded = cs.SelectMany(c =>
            {
                var cabs = c.Cabinets.Concat(c.OutOfCabinets).Where(o => nos.All(n => n != o.No));
                foreach (var cab in cabs)
                {
                    cab.Drawers = cab.Drawers.Where(d => nos.All(n => n != d.No)).ToList();
                    foreach (var dra in cab.Drawers)
                    {
                        dra.Boxes = dra.Boxes.Where(b => !b.IsBreakdown && nos.All(n => n != b.No)).ToList();
                        // 最小硬件单位为药盒，不过滤针剂
                        //foreach (var box in dra.Boxes)
                        //{
                        //    box.Injections = box.Injections.Where(ij => nos.All(n => n != ij.No)).ToList();
                        //}
                    }
                }
                return cabs;
            }).ToList();

            Exchange[] exchanges = allocations;
            var boxNos = SearchPermitBoxes(Terminal);
            var algo = new LocateBoxAlgo();
            var boxes = algo.AlternativeBoxes(boxNos, excluded.Where(e => e.IsControlled), excluded.Where(e => !e.IsControlled));
            algo.LocateCheckIn(ref exchanges, boxes, false, false);

            // InternalAllocation 的含义：从设备 TurnOutDevice 中拿出 Plans 这些物品放到设备 TurnInDevice 中
            var alos = allocations.SelectMany(a => a.Plans.Where(p => !p.IsExecuted).Select((p, index) => new InternalAllocation
            {
                UniqueId = index == 0 ? a.UniqueId : $"{a.UniqueId}@{index}",
                Goods = a.Goods,
                GoodsId = a.GoodsId,
                BatchNumber = a.BatchNumber,
                ExpiredDate = a.ExpiredDate,
                Mode = p.Mode,
                Qty = p.Qty,
                Plans = new List<ActionPlan> { p },
                QtyActual = 0,
                CustomerId = a.CustomerId,
                Computer = a.Computer,
                RecordType = a.RecordType,
                TimeFilter = a.TimeFilter,
                ApplyId = a.ApplyId,
                TurnInDevice = p.Box.No,
                TurnOutDevice = a.TurnOutDevice,
                OperatorId = a.OperatorId,
                OperatorName = a.OperatorName,
            })).ToList();
            foreach (var item in alos)
            {
                await mongo.InternalAllocationCollection.FindOneAndReplaceAsync<InternalAllocation>(x => x.UniqueId == item.UniqueId, item, new FindOneAndReplaceOptions<InternalAllocation, InternalAllocation> { IsUpsert = true });
            }
            return alos.Select(e => e.UniqueId).ToArray();
        }

        /// <summary>
        ///     暂时只支持柜外往柜内调整
        /// </summary>
        /// <param name="allocations"></param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-qty-for-turn-in-device")]
        public async Task<bool> ModifyQtyForTurnInDeviceAsync([FromBody] string[] allocations, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var virtuals = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.OutOfCabinets).ToList();

            var alos = mongo.InternalAllocationCollection.AsQueryable().Where(a => allocations.Contains(a.UniqueId)).ToList();
            foreach (var item in alos)
            {
                for (int i = 0; i < virtuals.Count; i++)
                {
                    for (int j = 0; j < virtuals[i].Drawers.Count; j++)
                    {
                        for (int k = 0; k < virtuals[i].Drawers[j].Boxes.Count; k++)
                        {
                            var box = virtuals[i].Drawers[j].Boxes[k];
                            if (item.TurnOutDevice == box.No)
                            {
                                var index = box.Fills.FindIndex(f => f.GoodsId == item.GoodsId);
                                await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == virtuals[i].OwnerCode, Builders<Customer>.Update
                                    .Inc(x => x.OutOfCabinets[i].Drawers[j].Boxes[k].Fills[index].QtyExisted, -item.QtyActual));
                            }
                        }
                    }
                }
            }
            return alos.Any();
        }
    }
}

// pharmacy-transfer
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        /// <summary>
        ///     删除调拨 —— 柜内未存储指定的药品
        /// </summary>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpDelete]
        [ActionName("delete-allocations-for-none-storage-goods")]
        public async Task<bool> DeleteAllocationsForNoneStorageGoodsAsync(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var goodsIds = mongo.TerminalGoodsCollection.AsQueryable().Where(t => t.Computer == Terminal).Select(t => t.GoodsId).ToList();
            await mongo.AllocationCollection.UpdateManyAsync(p => !p.IsDisabled && p.FinishTime == null && !goodsIds.Contains(p.GoodsId) && p.DepartmentDestinationId == department,
                Builders<Allocation>.Update.Set(p => p.IsDisabled, true));
            return true;
        }

        /// <summary>
        /// 恢复当天被禁用的调拨数据
        /// </summary>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("add-allocations-for-storage-goods")]
        public async Task<bool> AddAllocationsForStorageGoodsAsync(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;
            var date = Convert.ToDateTime(DateTime.Now.ToShortDateString());
            var goodsIds = mongo.TerminalGoodsCollection.AsQueryable().Where(t => t.Computer == Terminal).Select(t => t.GoodsId).ToList();
            await mongo.AllocationCollection.UpdateManyAsync(p => p.IsDisabled && p.TimeFilter >= date && p.TimeFilter < date.AddDays(1) && p.FinishTime == null && goodsIds.Contains(p.GoodsId) && p.DepartmentDestinationId == department,
                Builders<Allocation>.Update.Set(p => p.IsDisabled, false));
            return true;
        }
        private string[] VisitorDepartments(string hospital, string depart)
        {
            // 广济医院，HIS 调拨给中心药房（409）的调拨单填写的是西药房（105）
            var j = ((JArray)Global.AppSettings["DepartmentsGroup"][hospital])?.FirstOrDefault()?[depart ?? string.Empty];
            return j?.Values<string>().ToArray() ?? (depart == null ? new string[0] : new[] { depart });
        }

        public class AllocationProfile
        {
            [JsonProperty("_id")]
            public string UniqueId { get; set; }
            public ExchangeMode Mode { get; set; }
            public GoodsProfile Goods { get; set; }
            public DepartmentProfile Department { get; set; }
            public string ApplyId { get; set; }
            public double ApplyQty { get; set; }
            public double AcceptedQty { get; set; }
            public UserProfile Receiver { get; set; }
            public DateTime? ReceivedTime { get; set; }
            public string ExchangeBarcode { get; set; }
            public DateTime? DeliverTime { get; set; }
            public double Qty { get; set; }
            public double QtyActual { get; set; }
            public List<string> GoodsBarcodes { get; set; }
            public string UnCompleteReason { get; set; }
        }

        /// <summary>
        ///     查询时间范围内的调拨
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="hospital"></param>
        /// <param name="mode">CheckIn or CheckOut</param>
        /// <param name="type">CheckIn: All, NotAccepted, NotIn; CheckOut: All, NotOut</param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-allocations-by-date-range")]
        public AllocationProfile[] SearchAllocationsByDateRange(DateTime start, DateTime end, string hospital, ExchangeMode mode, string type, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var departments = VisitorDepartments(hospital, DepartmentId);
            var allocs = new List<Allocation>();
            var lq = mongo.AllocationCollection.AsQueryable().Where(a => !a.IsDisabled && a.Mode == mode && a.TimeFilter >= start && a.TimeFilter < end);
            if (mode == ExchangeMode.CheckIn)
            {
                allocs = lq.Where(a => departments.Contains(a.DepartmentDestinationId)).ToList();
                switch (type)
                {
                    case "All":         // 顺序 未入库、未验收、已入库
                        allocs = allocs.Where(x => x.AcceptedQty > 0 && x.QtyActual != x.AcceptedQty).Concat(allocs.Where(x => x.AcceptedQty <= 0)).Concat(allocs.Where(x => x.Qty == x.QtyActual && x.QtyActual > 0)).ToList();
                        break;
                    case "NotAccepted": // 未验收
                        allocs = allocs.Where(x => x.AcceptedQty <= 0).ToList();
                        break;
                    case "NotIn":       // 未入库
                        allocs = allocs.Where(x => x.AcceptedQty > 0 && x.QtyActual != x.AcceptedQty).ToList();
                        break;
                    default:
                        allocs = new List<Allocation>();
                        break;
                }
            }
            else if (mode == ExchangeMode.CheckOut)
            {
                allocs = lq.Where(a => departments.Contains(a.DepartmentSourceId)).ToList();
                switch (type)
                {
                    case "All":         // 顺序 未出库、已出库
                        allocs = allocs.Where(x => x.QtyActual < x.ApplyQty).Concat(allocs.Where(x => x.QtyActual >= x.ApplyQty)).ToList();
                        break;
                    case "NotOut":      // 未出库
                        allocs = allocs.Where(x => x.QtyActual < x.ApplyQty).ToList();
                        break;
                    default:
                        allocs = new List<Allocation>();
                        break;
                }
            }
            else
            {
                allocs = new List<Allocation>();
            }

            var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
            return allocs.Select(a =>
            {
                var dpt = mode == ExchangeMode.CheckOut ? a.DepartmentDestinationId : a.DepartmentSourceId;
                var code = mode == ExchangeMode.CheckOut ? a.DepartmentDestination?.Code : a.DepartmentSource?.Code;
                a.Goods = a.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == a.GoodsId) ?? new Goods { UniqueId = a.GoodsId, };
                return new AllocationProfile
                {
                    UniqueId = a.UniqueId,
                    Mode = a.Mode,
                    Goods = a.Goods.ToGoodsProfile(a.BatchNumber, a.ExpiredDate.Date),
                    Department = new DepartmentProfile
                    {
                        UniqueId = dpt,
                        DisplayName = mode == ExchangeMode.CheckOut ? a.DepartmentDestination?.DisplayName : a.DepartmentSource?.DisplayName,
                        Code = code ?? dpt,
                        Computer = customers.SelectMany(o => o.Cabinets).Where(o => o.DepartmentId == dpt).Select(o => o.Computer).FirstOrDefault(),
                    },
                    ApplyId = a.ApplyId,
                    ApplyQty = a.ApplyQty,
                    AcceptedQty = a.AcceptedQty,
                    Receiver = new UserProfile { LoginId = a.Receiver, DisplayName = a.ReceiverName, /* 其余属性不需要 */ },
                    ReceivedTime = a.ReceivedTime,
                    DeliverTime = a.DeliverTime,
                    ExchangeBarcode = a.ExchangeBarcode,
                    Qty = a.Qty,
                    QtyActual = a.QtyActual,
                    UnCompleteReason = a.UnCompleteReason,
                    GoodsBarcodes = a.GoodsBarcodes,
                };
            }).ToArray();
        }

        /// <summary>
        ///     调拨数据验收
        /// </summary>
        /// <param name="allocation"></param>
        /// <param name="qty">验收数量</param>
        /// <param name="reason">未完全入库时，用户输入的原因</param>
        /// <param name="deliverer">复核人</param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-allocation-accepted")]
        public async Task<bool> ModifyAllocationAcceptedAsync(string allocation, int qty, string reason = null, string deliverer = null)
        {
            var alloc = mongo.AllocationCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == allocation);
            if (alloc == null)
            {
                return false;
            }

            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal);
            var name = string.IsNullOrEmpty(deliverer) ? null : mongo.UserCollection.AsQueryable().Where(u => u.LoginId == deliverer).Select(u => u.Employee.DisplayName).FirstOrDefault();
            var builder = Builders<Allocation>.Update
                .Set(x => x.Receiver, primary.LoginId)
                .Set(x => x.ReceiverName, primary.DisplayName)
                .Set(x => x.ReceivedTime, DateTime.Now)
                .Set(x => x.AcceptedQty, qty)
                .Set(x => x.Qty, qty)
                .Set(x => x.UnCompleteReason, reason)
                .Set(x => x.Deliverer, deliverer)
                .Set(x => x.DelivererName, name);
            await mongo.AllocationCollection.UpdateOneAsync(e => e.UniqueId == allocation, builder);
            return true;
        }

        [HttpPut]
        [ActionName("modify-allocation-batch-accepted")]
        public async Task<bool> ModifyAllocationBatchAcceptedAsync(string hospital, [FromBody] string[] allocations)
        {
            // SDEY 新功能:
            // 调拨记录数量都是 1, 选中多条调拨验收之后, 相同的物品进行合并操作便于批量入库
            if (allocations?.Any() == true)
            {
                var primary = ServiceStartup.GetPrimaryAuthorized(Terminal);
                var allocs = mongo.AllocationCollection.AsQueryable().Where(a => !a.IsDisabled && allocations.Contains(a.UniqueId)).ToArray();
                foreach (var item in allocs)
                {
                    var builder = Builders<Allocation>.Update
                        .Set(x => x.Receiver, primary.LoginId)
                        .Set(x => x.ReceiverName, primary.DisplayName)
                        .Set(x => x.ReceivedTime, DateTime.Now)
                        .Set(x => x.AcceptedQty, item.ApplyQty)
                        .Set(x => x.Qty, item.ApplyQty);
                    if (hospital == Hospital.SDEY)
                    {
                        builder = builder.Set(x => x.IsDisabled, true);
                    }
                    await mongo.AllocationCollection.UpdateOneAsync(e => e.UniqueId == item.UniqueId, builder);
                }

                if (hospital == Hospital.SDEY)
                {
                    var grouped = allocs.GroupBy(a => new { a.GoodsId, BatchNumber = a.BatchNumber ?? string.Empty, ExpiredDate = a.ExpiredDate.Date, a.Mode, });
                    var singles = grouped.Where(gp => gp.Count() == 1).Select(gp => gp.First().UniqueId).ToList();
                    if (singles.Any())
                    {
                        await mongo.AllocationCollection.UpdateManyAsync(a => singles.Contains(a.UniqueId), Builders<Allocation>.Update.Set(x => x.IsDisabled, false));
                    }

                    var newObjs = grouped.Where(gp => gp.Count() > 1)
                        .Select(gp =>
                        {
                            var first = gp.First();
                            first.UniqueId = SfraObject.GenerateId();
                            first.ApplyQty = gp.Sum(o => o.ApplyQty);
                            first.BatchNumber = gp.Key.BatchNumber;
                            first.ExpiredDate = gp.Key.ExpiredDate;

                            // 结扎夹一包含有 4 个或 6 个, 取和退操作时都按照整包来操作
                            // 为了便于用户使用, 对于此类物品都新生成一个条码
                            // 生成新条码的规则是 2018051500001,2018051500002,2018051500003,2018051500004 保存为 2018051500001+4
                            // HIS 保证条码是连续的
                            var bars = gp.SelectMany(o => o.GoodsBarcodes).OrderBy(o => o).ToList();
                            var conversion = (int)(first.Goods?.Conversion ?? 1.0);
                            first.GoodsBarcodes = Enumerable.Range(0, bars.Count / conversion).Select(o =>
                            {
                                var tmps = bars.Skip(o * conversion).Take(conversion);
                                return $"{tmps.First()}+{tmps.Count()}";
                            }).ToList();

                            first.Receiver = primary.LoginId;
                            first.ReceiverName = primary.DisplayName;
                            first.ReceivedTime = DateTime.Now;
                            first.AcceptedQty = first.ApplyQty;
                            first.Qty = first.ApplyQty;

                            first.Plans = gp.SelectMany(o => o.Plans ?? new List<ActionPlan>()).ToList();
                            first.QtyActual = gp.Sum(o => o.QtyActual);
                            first.CreatedTime = DateTime.Now;
                            return first;
                        }).ToList();
                    await mongo.AllocationCollection.InsertManyAsync(newObjs);
                }
            }
            return allocations?.Any() ?? false;
        }
    }

}

// storage-transfer
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        /// <summary>
        ///     获取当前登录人（主、次）能操作的药盒（不包括已经标记为故障的药盒）
        /// </summary>
        [ActionName("search-permit-boxes")]
        [HttpGet]
        public List<string> SearchPermitBoxes(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).Select(c => new
            {
                c.Cabinets,
                c.OutOfCabinets,
            }).ToList();
            var boxes = cs.SelectMany(c => c.Cabinets).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes);

            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal);
            var secondary = ServiceStartup.GetSecondaryAuthorized(Terminal);
            return Bxs(primary?.LoginId, primary?.Kernel ?? false).Concat(Bxs(secondary?.LoginId, secondary?.Kernel ?? false))
                .Concat(cs.SelectMany(c => c.OutOfCabinets).SelectMany(v => v.Drawers).SelectMany(d => d.Boxes).Select(b => b.No))
                .Distinct().ToList();

            List<string> Bxs(string user, bool kernel)
            {
                if (user == null)
                {
                    return new List<string>();
                }

                return kernel ? boxes.Select(b => b.No).ToList()
                    : (mongo.UserCollection.AsQueryable().Select(f => new { f.LoginId, f.AvailableStorages }).FirstOrDefault(f => f.LoginId == user)?.AvailableStorages ?? new List<string>()).Join(boxes, a => a, b => b.No, (a, b) => b.No).ToList();
            }
        }

        [HttpGet]
        [ActionName("modify-batch-expired-for-box")]
        public async Task<bool> ModifyBatchExpiredForBoxAsync(string box, string goods, string batch, DateTime expired, string oldBatch, DateTime oldExpired)
        {
            var modified = false;

            var cabinets = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).ToList();
            for (int i = 0; i < cabinets.Count; i++)
            {
                var cabinet = cabinets[i];
                for (int j = 0; j < cabinet.Drawers.Count; j++)
                {
                    var drawer = cabinet.Drawers[j];
                    for (int k = 0; k < drawer.Boxes.Count; k++)
                    {
                        var find = drawer.Boxes[k];
                        if (find.No != box)
                        {
                            continue;
                        }

                        for (int m = 0; m < find.Fills.Count; m++)
                        {
                            var fill = find.Fills[m];
                            if (fill.GoodsId == goods)
                            {
                                fill.BatchNumber = batch;
                                fill.ExpiredDate = expired;
                                await mongo.CustomerCollection.UpdateOneAsync(c => c.UniqueId == cabinet.OwnerCode, Builders<Customer>.Update.
                                    Set(c => c.Cabinets[i].Drawers[j].Boxes[k].Fills[m].BatchNumber, batch).
                                    Set(c => c.Cabinets[i].Drawers[j].Boxes[k].Fills[m].ExpiredDate, expired));
                                modified = true;
                                goto over;
                            }
                        }
                    }
                }
            }

            over:
            return modified;
        }
    }

}

// goods-transfer
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        /// <summary>
        ///     查询终端药品库存    不包括回收箱
        /// </summary>
        [HttpGet]
        [ActionName("search-inventory-detail-for-terminal")]
        public TerminalInventoryDetail[] SearchInventoryDetailForTerminal(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;

            var categories = mongo.GoodsCategoryCollection.AsQueryable().Where(d => !d.IsDisabled).ToList();
            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).Select(c => new
            {
                Cabinets = c.Cabinets.Where(o => o.Computer == Terminal),
                OutOfCabinets = c.OutOfCabinets.Where(o => o.DepartmentId == department),
            }).ToList();

            var currents = cs.SelectMany(c => c.Cabinets).Concat(cs.SelectMany(c => c.OutOfCabinets))
                    .SelectMany(c => c.Drawers).Where(d => !d.IsRecycleBin).SelectMany(d => d.Boxes).SelectMany(b => b.Fills)
                    .GroupBy(f => new { f.GoodsId, BatchNumber = f.BatchNumber ?? string.Empty, ExpiredDate = f.ExpiredDate.Date })
                    .Select(f => (f.Key.GoodsId, f.Key.BatchNumber, f.Key.ExpiredDate, QtyExisted: f.Sum(o => o.QtyExisted), QtyMax: f.Sum(o => o.QtyMax))).ToArray();
            var goodsIds = currents.Select(c => c.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).ToList();

            return currents.Select(c =>
            {
                var g = goods.FirstOrDefault(f => f.UniqueId == c.GoodsId) ?? new Goods { UniqueId = c.GoodsId, };
                var category = categories.FirstOrDefault(f => f.GoodsKeys.Contains(c.GoodsId));
                return new TerminalInventoryDetail
                {
                    Goods = g.ToGoodsProfile(c.BatchNumber, c.ExpiredDate.Date),
                    Category = category?.DisplayName,
                    Background = category?.Background,
                    Foreground = category?.Foreground,
                    QtyExisted = c.QtyExisted,
                    QtyMax = c.QtyMax,
                    QtyWarning = double.NaN,
                };
            }).ToArray();
        }
    }

}

// storage-check
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        //      searchPermitBoxes

        //      searchIntellingentCabinetsForTerminal

        /// <summary>
        ///     提交清点记录
        /// </summary>
        [HttpPut]
        [ActionName("modify-inventory-transfer")]
        public async Task<string> ModifyInventoryTransferAsync([FromBody] Transfer transfer, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            transfer.UniqueId = transfer.UniqueId ?? SfraObject.GenerateId();
            transfer.Computer = Terminal;
            transfer.Login = ServiceStartup.GetPrimaryAuthorized(Terminal).LoginId;
            transfer.Executor = ServiceStartup.GetPrimaryAuthorized(Terminal).LoginId;
            foreach (var record in transfer.TransferRecords)
            {
                record.UniqueId = record.UniqueId ?? SfraObject.GenerateId();
                record.Goods = mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == record.GoodsId);
            }
            await mongo.TransferCollection.InsertOneAsync(transfer);
            updateFills(transfer.TransferRecords);
            return transfer.UniqueId;

            void updateFills(List<Transfer.TransferRecord> records)
            {
                var finds = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
                foreach (var record in records)
                {
                    foreach (var customer in finds)
                    {
                        var cabIdx = customer.Cabinets.FindIndex(c => record.No.StartsWith(c.No));
                        var draIdx = customer.Cabinets[cabIdx].Drawers.FindIndex(d => record.No.StartsWith(d.No));
                        var boxIdx = customer.Cabinets[cabIdx].Drawers[draIdx].Boxes.FindIndex(b => record.No == b.No);

                        var fills = customer.Cabinets[cabIdx].Drawers[draIdx].Boxes[boxIdx].Fills;
                        var filIdx = fills.FindIndex(f => f.GoodsId == record.GoodsId);

                        if (fills[filIdx].BatchNumber != record.BatchNumber || fills[filIdx].ExpiredDate != record.ExpiredDate)
                        {
                            mongo.CustomerCollection.UpdateOne(x => x.UniqueId == customer.UniqueId, Builders<Customer>.Update
                                .Set(o => o.Cabinets[cabIdx].Drawers[draIdx].Boxes[boxIdx].Fills[filIdx].BatchNumber, record.BatchNumber)
                                .Set(o => o.Cabinets[cabIdx].Drawers[draIdx].Boxes[boxIdx].Fills[filIdx].ExpiredDate, record.ExpiredDate)
                            );
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     从上次清点以来, 操作过的硬件位置. 包括抽屉和药盒
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-handled-nos-from-last-transfer")]
        public List<string> SearcHandledhNosFromLastTransfer(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var department = DepartmentId;

            var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).Select(c => new
            {
                Cabinets = c.Cabinets.Where(o => o.Computer == Terminal),
                OutOfCabinets = c.OutOfCabinets.Where(o => o.DepartmentId == department),
            }).ToArray();
            var cabinets = customers.SelectMany(c => c.Cabinets).Concat(customers.SelectMany(c => c.OutOfCabinets)).ToList();

            var handleds = cabinets.SelectMany(c => c.Drawers).SelectMany(d =>
            {
                var boxes = d.Boxes.Select(b => b.No).ToArray();
                var record = mongo.TransferCollection.AsQueryable().SelectMany(t => t.TransferRecords).Where(r => boxes.Contains(r.No)).OrderByDescending(r => r.CreatedTime).FirstOrDefault();
                var last = record?.CreatedTime ?? DateTime.MinValue;
                return mongo.ActionJournalCollection.AsQueryable().Where(a => boxes.Contains(a.No) && a.CreatedTime >= last).Select(a => a.No).Distinct().ToList();
            });

            var cabdras = handleds.SelectMany(o =>
            {
                var pts = o.Split(':');
                return new[] { $"{pts[0]}:{pts[1].Substring(0, "01".Length)}", $"{pts[0]}:{pts[1].Substring(0, "01-0101".Length)}" };
            }).Distinct().ToList();

            return cabdras.OrderBy(o => o).Concat(handleds.OrderBy(o => o)).ToList();
        }
    }
}