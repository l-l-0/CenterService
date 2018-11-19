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
using System.Web.Http;

#pragma warning disable CS1591

// action-details
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        // searchAllCustomerCore

        // searchDepartmentsOwnComputer

        public class CabinetsOperationDetail
        {
            public GoodsProfile Goods { get; set; }
            public string No { get; set; }
            public string TargetType { get; set; }
            public string TargetId { get; set; }
            public ExchangeMode Mode { get; set; }
            public string RecordType { get; set; }
            public double QtyActual { get; set; }
            public DateTime OperateTime { get; set; }
            public string OperatorId { get; set; }
            public string OperatorName { get; set; }
            public string SecondaryUserId { get; set; }
            public string SecondaryUserName { get; set; }
            public string PrimaryUserId { get; set; }
            public string PrimaryUserName { get; set; }
        }

        /// <summary>
        ///     操作记录明细表
        /// </summary>
        [HttpGet]
        [ActionName("search-cabinets-operation-details")]
        public CabinetsOperationDetail[] SearchCabinetsOperationDetails(DateTime start, DateTime end, string department)
        {
            var computers = Computers(department);
            var jours = mongo.ActionJournalCollection.AsQueryable().Where(a => computers.Contains(a.Computer) && a.CreatedTime >= start && a.CreatedTime < end).ToList();
            return old(jours.Where(j => j.GoodsId == null).ToList()).Concat(current(jours.Where(j => j.GoodsId != null).ToList())).ToArray();

            CabinetsOperationDetail[] current(List<ActionJournal> journals)
            {
                var goodsIds = journals.Select(j => j.GoodsId).Distinct().ToList();
                var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).ToList();
                return journals.Select(aj =>
                {
                    aj.Goods = goods.FirstOrDefault(g => g.UniqueId == aj.GoodsId);
                    // 默认主登录人为操作人，报表记 1 人
                    // 当有监督人时，操作人为监督人，报表记 2 人
                    var ope = aj.PrimaryUserId == aj.OperatorUserId ? aj.OperatorUserId : $"{aj.PrimaryUserId},{aj.OperatorUserId}";
                    var opn = aj.OperatorUserName ?? mongo.UserCollection.AsQueryable().Where(u => u.LoginId == aj.OperatorUserId).Select(u => u.Employee.DisplayName).FirstOrDefault();  // 查询数据库是为了兼容前面记录认证人时的错误
                    var usr = aj.PrimaryUserId == aj.OperatorUserId ? opn : $"{aj.PrimaryUserName},{opn}";
                    return new CabinetsOperationDetail
                    {
                        Goods = (aj.Goods ?? new Goods { UniqueId = aj.GoodsId, }).ToGoodsProfile(aj.BatchNumber, aj.ExpiredDate.Date),
                        TargetType = aj.TargetType,
                        TargetId = aj.TargetId,
                        No = aj.No,
                        Mode = aj.Mode,
                        RecordType = aj.RecordType,
                        QtyActual = aj.Qty,
                        OperateTime = aj.CreatedTime,
                        OperatorId = ope,
                        OperatorName = usr,
                        SecondaryUserId = aj.SecondaryUserId,
                        SecondaryUserName = aj.SecondaryUserName,
                        PrimaryUserId = aj.PrimaryUserId,
                        PrimaryUserName = aj.PrimaryUserName
                    };
                }).ToArray();
            }

            CabinetsOperationDetail[] old(List<ActionJournal> journals)
            {
                return journals.GroupBy(j => j.TargetType).SelectMany(gp =>
                {
                    var exchanges = queryExchanges(gp.Key, gp.Select(k => k.TargetId).ToArray());
                    return gp.Select(aj =>
                    {
                        var ex = exchanges.FirstOrDefault(fd => fd.UniqueId == aj.TargetId && fd.Plans[0].Box.No == aj.No);
                        if (ex == null)
                        {
                            return null;
                        }
                        return new CabinetsOperationDetail
                        {
                            Goods = (ex.Goods ?? new Goods { UniqueId = ex.GoodsId, }).ToGoodsProfile(ex.BatchNumber, ex.ExpiredDate.Date),
                            No = aj.No,
                            TargetType = aj.TargetType,
                            TargetId = aj.TargetId,
                            Mode = ex.Mode,
                            RecordType = ex.RecordType,
                            QtyActual = ex.QtyActual,
                            OperateTime = aj.CreatedTime,
                            OperatorId = aj.OperatorUserId,
                            OperatorName = aj.OperatorUserName,
                            SecondaryUserId = aj.SecondaryUserId,
                            SecondaryUserName = aj.SecondaryUserName,
                            PrimaryUserId = aj.PrimaryUserId,
                            PrimaryUserName = aj.PrimaryUserName,
                        };
                    }).Where(e => e?.Goods.UniqueId != null);
                }).OrderBy(x => x.OperateTime).ToArray();
            }

            Exchange[] queryExchanges(string collection, string[] targetIds)
            {
                Exchange[] exchanges = null;
                switch (collection)
                {
                    case nameof(Prescription):
                        exchanges = mongo.PrescriptionCollection.AsQueryable().Where(e => targetIds.Contains(e.UniqueId))
                            .Select(e => new { e.UniqueId, e.GoodsId, e.Goods, e.Mode, e.QtyActual, e.RecordType, e.Plans, })
                            .ToArray().SelectMany(e => e.Plans.Select(p =>
                            {
                                var fill = p.Box.Fills.FirstOrDefault(f => f.GoodsId == e.GoodsId);
                                return new Exchange
                                {
                                    UniqueId = e.UniqueId,
                                    GoodsId = e.GoodsId,
                                    Goods = e.Goods,
                                    Mode = e.Mode,
                                    QtyActual = p.IsExecuted ? p.Qty : 0.0,
                                    RecordType = e.RecordType,
                                    BatchNumber = fill?.BatchNumber,
                                    ExpiredDate = fill?.ExpiredDate ?? DateTime.MaxValue,
                                    Plans = new List<ActionPlan>(new[] { p }),
                                };
                            })).ToArray();
                        break;
                    case nameof(Exchange):
                        exchanges = mongo.ExchangeCollection.AsQueryable().Where(e => targetIds.Contains(e.UniqueId))
                            .Select(e => new { e.UniqueId, e.GoodsId, e.Goods, e.Mode, e.QtyActual, e.RecordType, e.Plans, })
                            .ToArray().SelectMany(e => e.Plans.Select(p =>
                            {
                                var fill = p.Box.Fills.FirstOrDefault(f => f.GoodsId == e.GoodsId);
                                return new Exchange
                                {
                                    UniqueId = e.UniqueId,
                                    GoodsId = e.GoodsId,
                                    Goods = e.Goods,
                                    Mode = e.Mode,
                                    QtyActual = p.IsExecuted ? p.Qty : 0.0,
                                    RecordType = e.RecordType,
                                    BatchNumber = fill?.BatchNumber,
                                    ExpiredDate = fill?.ExpiredDate ?? DateTime.MaxValue,
                                    Plans = new List<ActionPlan>(new[] { p }),
                                };
                            })).ToArray();
                        break;
                    case nameof(Allocation):
                        exchanges = mongo.AllocationCollection.AsQueryable().Where(e => targetIds.Contains(e.UniqueId))
                            .Select(e => new { e.UniqueId, e.GoodsId, e.Goods, e.Mode, e.QtyActual, e.RecordType, e.Plans, e.BatchNumber, e.ExpiredDate, })
                            .ToArray().SelectMany(e => e.Plans.Select(p => new Exchange
                            {
                                UniqueId = e.UniqueId,
                                GoodsId = e.GoodsId,
                                Goods = e.Goods,
                                Mode = e.Mode,
                                QtyActual = p.IsExecuted ? p.Qty : 0.0,
                                RecordType = e.RecordType,
                                BatchNumber = e.BatchNumber,
                                ExpiredDate = e.ExpiredDate,
                                Plans = new List<ActionPlan>(new[] { p }),
                            })).ToArray();
                        break;
                    case nameof(Medication):
                        exchanges = mongo.MedicationCollection.AsQueryable().Where(e => targetIds.Contains(e.UniqueId))
                            .Select(e => new { e.UniqueId, e.GoodsId, e.Goods, e.Mode, e.QtyActual, e.RecordType, e.Plans, })
                            .ToArray().SelectMany(e => e.Plans.Select(p =>
                            {
                                var fill = p.Box.Fills.FirstOrDefault(f => f.GoodsId == e.GoodsId);
                                return new Exchange
                                {
                                    UniqueId = e.UniqueId,
                                    GoodsId = e.GoodsId,
                                    Goods = e.Goods,
                                    Mode = e.Mode,
                                    QtyActual = p.IsExecuted ? p.Qty : 0.0,
                                    RecordType = e.RecordType,
                                    BatchNumber = fill?.BatchNumber,
                                    ExpiredDate = fill?.ExpiredDate ?? DateTime.MaxValue,
                                    Plans = new List<ActionPlan>(new[] { p }),
                                };
                            })).ToArray();
                        break;
                    default: exchanges = new Exchange[0]; break;
                }
                return exchanges;
            }
        }
    }

}

// inventory-details
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        // searchAllCustomerCore

        public class AllocaProfile
        {
            [JsonProperty("_id")]
            public string UniqueId { get; set; }
            public string ApplyId { get; set; }
            public GoodsProfile Goods { get; set; }
            public DepartmentProfile DepartmentDestination { get; set; }
            public DepartmentProfile DepartmentSource { get; set; }
            public double? Qty { get; set; }
            public double? ApplyQty { get; set; }
            public DateTime? Time { get; set; }
            public string State { get; set; }
            public string Person { get; set; }
            public double Balance { get; set; }
            public string Deliverer { get; set; }
        }

        /// <summary>
        ///     药品入库验收记录
        /// </summary>
        [HttpGet]
        [ActionName("search-allocation-details")]
        public AllocaProfile[] SearchAllocationDetails(string hospital, string department, DateTime start, DateTime end, string goods = null, string terminal = null)
        {
            Terminal = terminal ?? Terminal;

            var departs = VisitorDepartments(hospital, department);
            // 查询调拨 => Allocation
            var allocs = mongo.AllocationCollection.AsQueryable().Where(a => departs.Contains(a.DepartmentDestinationId) && (a.ReceivedTime >= start && a.ReceivedTime < end || a.FinishTime >= start && a.FinishTime < end) && (goods == null || a.GoodsId == goods)).ToList();
            allocs.ForEach(a => a.Goods = a.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == a.GoodsId) ?? new Goods { UniqueId = a.GoodsId, });

            var loginIds = allocs.SelectMany(a => new[] { a.Receiver, a.Storager }).Where(x => !string.IsNullOrEmpty(x)).ToList();
            AllocaProfile[] data;
            switch (hospital)
            {
                case Hospital.SDJN:
                case Hospital.SDTZ:
                case Hospital.SHQP:
                default:
                    data = allocs.SelectMany(a => new[]
                    {
                        new AllocaProfile
                        {
                            UniqueId = a.UniqueId,
                            ApplyId = a.ApplyId,
                            Goods =  a.Goods.ToGoodsProfile(a.BatchNumber, a.ExpiredDate.Date),
                            Qty = a.AcceptedQty,
                            ApplyQty=a.ApplyQty,
                            Time = a.ReceivedTime,
                            State = $"验收{(string.IsNullOrEmpty(a.UnCompleteReason) ? string.Empty : $"（{a.UnCompleteReason}）")}",
                            Person = a.ReceiverName,
                            Deliverer = a.DelivererName,
                        },
                        new AllocaProfile
                        {
                            UniqueId = a.UniqueId,
                            ApplyId = a.ApplyId,
                            Goods =  a.Goods.ToGoodsProfile(a.BatchNumber, a.ExpiredDate.Date),
                            Qty = a.QtyActual,
                            ApplyQty=a.ApplyQty,
                            Time = a.FinishTime,
                            State = "入库",
                            Person = a.StoragerName,
                            Deliverer = a.DelivererName,

                        },
                    }).Where(a => a.Time != null).OrderBy(a => a.Time).ToArray();
                    break;
            }
            return data;
        }

        /// <summary>
        ///     山东省立北院药品入库验收记录
        /// </summary>
        [HttpGet]
        [ActionName("search-allocation-details-days")]
        public AllocaProfile[] SearchAllocationDetailsDays(string hospital, string department, DateTime start, DateTime end, string goods)
        {
            var computers = Computers(department);
            var actionJournal = mongo.ActionJournalCollection.AsQueryable().Where(aj => computers.Contains(aj.Computer) && aj.GoodsId == goods && aj.CreatedTime >= start && aj.CreatedTime < end && aj.Qty > 0).ToList();
            var batchExpiredArray = actionJournal.Select(g => g.BatchNumber).Where(b => b != null).Distinct().ToArray();
            // 查询调拨 => Allocation
            var allocs = mongo.AllocationCollection.AsQueryable().Where(a => (a.DepartmentDestinationId == department) && (a.ReceivedTime >= start && a.ReceivedTime < end || a.FinishTime >= start && a.FinishTime < end) && a.GoodsId == goods).ToList();
            allocs.ForEach(a => a.Goods = a.Goods ?? mongo.GoodsCollection.AsQueryable().Where(f => f.UniqueId == a.GoodsId).FirstOrDefault());
            var loginIds = allocs.SelectMany(a => new[] { a.Receiver, a.Storager }).Where(x => !string.IsNullOrEmpty(x)).ToList();
            var dsjs = mongo.InventoryCollection.AsQueryable().Where(j => computers.Contains(j.Computer) && batchExpiredArray.Contains(j.BatchNumber) && j.StatsTime >= start && j.StatsTime < end && j.GoodsId == goods)
               .Select(d => new { d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime, d.GoodsId, d.BatchNumber }).ToList();
            return batchExpiredArray.SelectMany(b =>
            {
                return Enumerable.Range(0, Math.Max(0, (end - start).Days)).Select(d => start.AddDays(d)).SelectMany(date =>
                {
                    (double QtyInitial, double QtyCheckIn, double QtyCheckOut, DateTime StatsTime, string GoodsId, string BatchNumber)[] crnt;
                    if (date >= DateTime.Now.Date)
                    {
                        var css = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(o => o.DepartmentId == department).ToList();
                        var find = css.Select(c => new { c.Computer, c.OwnerCode, }).FirstOrDefault();
                        crnt = BuildInventory(find?.OwnerCode, find?.Computer, date, date.AddDays(1)).Where(d => d.GoodsId == goods && d.BatchNumber == b).Select(d => (d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime, d.GoodsId, d.BatchNumber)).ToArray();
                    }
                    else
                    {
                        crnt = dsjs.Where(d => d.StatsTime >= date && d.StatsTime < date.AddDays(1)).Where(d => d.BatchNumber == b).Select(d => (d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime, d.GoodsId, d.BatchNumber)).ToArray();
                    }
                    var prescriptions = actionJournal.Where(g => g.CreatedTime >= date && g.CreatedTime < date.AddDays(1) && g.TargetType == nameof(Prescription) && g.Mode == ExchangeMode.CheckIn && g.BatchNumber == b).ToArray();
                    var result = allocs.Where(a => (a.ReceivedTime >= date && a.ReceivedTime < DateTime.Parse(date.ToString("yyyy-MM-dd 23:59:59")) || a.FinishTime >= date && a.FinishTime < DateTime.Parse(date.ToString("yyyy-MM-dd 23:59:59")))).ToList();
                    return result.SelectMany(a => new[]
                    {
                        new AllocaProfile
                        {
                            UniqueId = a.UniqueId,
                            ApplyId = a.ApplyId,
                            Goods = (a.Goods ?? new Goods { UniqueId = a.GoodsId, }).ToGoodsProfile( a.BatchNumber ?? string.Empty, a.ExpiredDate.Date),
                            DepartmentDestination = new DepartmentProfile
                            {
                                UniqueId = a.DepartmentDestinationId,
                                DisplayName = a.DepartmentDestination?.DisplayName,
                                Code = a.DepartmentDestination?.Code,
                            },
                            DepartmentSource = new DepartmentProfile
                            {
                                UniqueId = a.DepartmentSourceId,
                                DisplayName = a.DepartmentSource?.DisplayName,
                                Code = a.DepartmentSource?.Code,
                            },
                            Qty = a.QtyActual,
                            Time = a.FinishTime,
                            State = "入库",
                            Person = a.StoragerName,
                            Balance = crnt.Sum(g => g.QtyInitial) + crnt.Sum(g => g.QtyCheckIn) - crnt.Sum(g => g.QtyCheckOut) + prescriptions.Sum(g => g.Qty),
                        },
                    }).Where(a => a.Time != null).OrderBy(a => a.Time).ToArray();
                }).ToArray();
            }).ToArray();
        }

    }

}

// goods-quota-details
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        // searchAllCustomerCore

        // searchCategoryProfilesByCustomer

        public class GoodsQtyArg
        {
            public List<string> Departments { get; set; }
            public string[] Categories { get; set; }
        }

        public class GoodsQtyDetail
        {
            public DepartmentProfile Department { get; set; }
            public GoodsProfileQty[] Goods { get; set; }
            public IdName Category { get; set; }
            public double CurrentQuota { get; set; }
            public double StorageQuota { get; set; }
            public double SupplyQuota { get; set; }
        }

        /// <summary>
        ///     物品基数记录报表
        /// </summary>
        [HttpPost]
        [ActionName("search-goods-qty-details")]
        public GoodsQtyDetail[] SearchGoodsQtyDetails(string hospital, string customer, [FromBody] GoodsQtyArg arg, string content = null)
        {
            // 查询指定客户下的指定分组的药品库存详情
            arg.Categories = arg.Categories ?? new string[0];
            var categories = mongo.GoodsCategoryCollection.AsQueryable().Where(c => c.CustomerId == customer && (arg.Categories.Length <= 0 || arg.Categories.Contains(c.UniqueId))).ToList();
            var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).Select(cs => new { cs.Cabinets, cs.OutOfCabinets, }).ToList();

            var goodsIds = customers.SelectMany(c => c.Cabinets).Concat(customers.SelectMany(c => c.OutOfCabinets)).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).Select(f => f.GoodsId).Distinct().ToArray();
            var goodsLq = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId));
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                goodsLq = goodsLq.Where(g => g.DisplayName.ToLower().Contains(content) || g.Pinyin.ToLower().Contains(content) || g.PinyinFull.ToLower().Contains(content) || g.Specification.ToLower().Contains(content) || g.Manufacturer.ToLower().Contains(content));
            }
            var goods = goodsLq.ToList();

            var departs = mongo.DepartmentCollection.AsQueryable().Where(d => arg.Departments.Contains(d.UniqueId)).ToList().Select(d => new DepartmentProfile
            {
                UniqueId = d.UniqueId,
                DisplayName = d.DisplayName,
                Code = d.Code,
                Computer = customers.SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == d.UniqueId).Select(c => c.Computer).FirstOrDefault(),
            }).ToList();

            var categoryGoodsIds = arg.Categories.Length > 0 ? categories.SelectMany(c => c.GoodsKeys).Distinct().ToArray() : goodsIds;
            var computers = customers.SelectMany(c => c.Cabinets).OrderBy(c => c.Department?.DisplayOrder ?? byte.MaxValue).Select(c => c.Computer).Distinct().ToList();
            var data = mongo.TerminalGoodsCollection.AsQueryable().Where(t => computers.Contains(t.Computer)).ToList().Where(t => categoryGoodsIds.Contains(t.GoodsId))
                .SelectMany(t =>
                {
                    t.Goods = goods.FirstOrDefault(x => x.UniqueId == t.GoodsId);
                    var depart = departs.FirstOrDefault(f => f.UniqueId == customers.SelectMany(c => c.Cabinets).Where(c => c.Computer == t.Computer).Select(c => c.DepartmentId).FirstOrDefault());
                    if (t.Goods == null || depart == null)
                    {
                        return new GoodsQtyDetail[0];
                    }

                    t.StorageQuota = double.IsNaN(t.StorageQuota) ? 0.0 : t.StorageQuota;
                    t.WarningQuota = double.IsNaN(t.WarningQuota) ? 0.0 : t.WarningQuota;
                    var result = customers.SelectMany(c => c.Cabinets).Concat(customers.SelectMany(c => c.OutOfCabinets)).Where(c => c.DepartmentId == depart.UniqueId)
                        .SelectMany(c => c.Drawers).Where(d => !d.IsRecycleBin).SelectMany(d => d.Boxes).SelectMany(b => b.Fills)
                        .Where(f => f.GoodsId == t.GoodsId).ToList()
                        .GroupBy(f => f.GoodsId).Select(gp =>
                        {
                            var cat = categories.FirstOrDefault(c => c.GoodsKeys.Contains(gp.Key));
                            var sum = gp.Sum(f => f.QtyExisted);
                            return new GoodsQtyDetail
                            {
                                Department = depart,
                                Goods = gp.GroupBy(g => new { BatchNumber = g.BatchNumber ?? string.Empty, ExpiredDate = g.ExpiredDate.Date, })
                                    .Select(g => t.Goods.ToGoodsProfileQty(g.Key.BatchNumber, g.Key.ExpiredDate, g.Sum(o => o.QtyExisted)))
                                    .ToArray(),
                                Category = new IdName { UniqueId = cat?.UniqueId, DisplayName = cat?.DisplayName, },
                                CurrentQuota = sum,
                                StorageQuota = t.StorageQuota,
                                SupplyQuota = t.StorageQuota - sum,
                            };
                        }).ToArray();
                    return result;
                }).Where(t => t.Goods.Length > 0).ToArray();
            return arg.Categories.Length > 0
                ? categories.SelectMany(c => data.Where(d => d.Category.UniqueId == c.UniqueId))
                    .GroupBy(d => new { depart = d.Department.UniqueId, goods = d.Goods[0].UniqueId, })
                    .SelectMany(g => g.Sum(o => o.CurrentQuota) >= g.First().StorageQuota ? g.Where(o => o.CurrentQuota > 0) : g)
                    .OrderBy(d => d.Department.Code).ToArray()
                : data;
        }

        public class GoodsNoQtyDetail
        {
            public GoodsProfile Goods { get; set; }
            public Details[] Details { get; set; }
        }

        public class Details
        {
            public double SumQty { get; set; }
            public GoodsLocation[] GoodsLocations { get; set; }
        }
        public class GoodsLocation
        {
            public double Qty { get; set; }
            public string BatchNumber { get; set; }
            public DateTime ExpiredDate { get; set; }
            public string No { get; set; }
        }

        /// <summary>
        ///     物品位置基数记录报表
        /// </summary>
        [HttpGet]
        [ActionName("search-no-goods-qty-details")]
        public GoodsNoQtyDetail[] SearchNoGoodsQtyDetails(string hospital, string customer, string department, string content = null)
        {
            var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).SelectMany(cs => cs.Cabinets).ToList();
            var computers = customers.Select(c => c.Computer).Distinct().ToList();

            var goodsLq = mongo.TerminalGoodsCollection.AsQueryable().Where(t => computers.Contains(t.Computer)).Select(t => t.Goods);
            if (!string.IsNullOrEmpty(content))
            {
                content = content.ToDBC().ToLower();
                goodsLq = goodsLq.Where(g => g.DisplayName.ToLower().Contains(content) || g.Pinyin.ToLower().Contains(content) || g.PinyinFull.ToLower().Contains(content) || g.Specification.ToLower().Contains(content) || g.Manufacturer.ToLower().Contains(content));
            }
            var goodsIds = goodsLq.Select(g => g.UniqueId).ToList();

            var data = mongo.TerminalGoodsCollection.AsQueryable().Where(t => goodsIds.Contains(t.GoodsId)).ToList()
                .SelectMany(t =>
                {
                    if (t.Goods == null)
                    {
                        return new GoodsNoQtyDetail[0];
                    }

                    return customers.Where(c => c.Computer == t.Computer).SelectMany(c => c.Drawers).Where(d => !d.IsRecycleBin).SelectMany(d => d.Boxes)
                        .SelectMany(b => b.Fills.Where(f => f.GoodsId == t.GoodsId).Select(f => new
                        {
                            Goods = t.Goods.ToGoodsProfile(string.Empty, DateTime.MaxValue.Date),
                            f.BatchNumber,
                            f.ExpiredDate,
                            f.QtyExisted,
                            b.No,
                        }))
                        .GroupBy(r => r.Goods.UniqueId).Select(o => new GoodsNoQtyDetail
                        {
                            Goods = (goodsLq.FirstOrDefault(g => g.UniqueId == o.Key) ?? new Goods { UniqueId = o.Key, }).ToGoodsProfile(string.Empty, DateTime.MaxValue.Date),
                            Details = o.GroupBy(s => new { BatchNumber = s.BatchNumber ?? string.Empty, ExpiredDate = s.ExpiredDate.Date, }).Select(b => new Details
                            {
                                SumQty = b.Sum(x => x.QtyExisted),
                                GoodsLocations = b.Select(l => new GoodsLocation { BatchNumber = l.BatchNumber, ExpiredDate = l.ExpiredDate, No = l.No, Qty = l.QtyExisted }).ToArray(),
                            }).ToArray(),
                        }).ToArray();
                }).ToArray();
            return data;
        }
    }
}

// prescription-details
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        // searchAllCustomerCore

        public class PersonProfile : IdName
        {
            public string CertificateCode { get; set; }
            public string CertificateType { get; set; }
        }

        public class PrescriptionDetail
        {
            [JsonProperty("_id")]
            public string UniqueId { get; set; }
            public int DisplayOrder { get; set; }
            public ExchangeMode Mode { get; set; }
            public EmployeeProfile Doctor { get; set; }
            public PatientProfile Patient { get; set; }
            public DepartmentProfile PatientDepartment { get; set; }
            public GoodsProfileQty Goods { get; set; }
            public PlanProfile[] Plans { get; set; }
            public PersonProfile Dispensing { get; set; }
            public DepartmentProfile DispensingDepartment { get; set; }
            public PersonProfile Agent { get; set; }
            public UserProfile Primary { get; set; }
            public UserProfile Secondary { get; set; }
            public UserProfile Operator { get; set; }
            public DateTime? IssuedTime { get; set; }
            public DateTime? DispensingTime { get; set; }
            public DateTime? FinishTime { get; set; }
        }

        public class PlanProfile
        {
            public string BatchNumber { get; set; }
            public DateTime? ExpiredDate { get; set; }
            public double Qty { get; set; }
        }

        /// <summary>
        ///     查询指定时间段内的医嘱信息
        /// </summary>
        /// <param name="hospital"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="department">为 null, 查询所有部门, 其余参数则查询指定的部门</param>
        /// <returns></returns>
        [ActionName("search-prescription-details")]
        [HttpGet]
        public PrescriptionDetail[] SearchPrescriptionDetails(string hospital, DateTime start, DateTime end, string department = null)
        {
            var lq = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && (department == null || p.DepartmentDestinationId == department));
            switch (hospital)
            {
                // SDBZ 根据取药时间来进行筛选
                case Hospital.SDBZ:
                case Hospital.NTSY:
                case Hospital.SDTZ: lq = lq.Where(x => x.FinishTime >= start && x.FinishTime < end); break;
                default: lq = lq.Where(x => x.TimeFilter >= start && x.TimeFilter < end); break;
            }
            var prescriptions = lq.OrderBy(p => p.TimeFilter).ToList();

            var departIds = prescriptions.SelectMany(p => new[]
            {
                p.Patient?.Hospitalization.ResidedAreaId ?? p.Patient?.Hospitalization.RoomId ?? p.Patient?.Hospitalization.AdmittedDepartmentId??p.DepartmentSourceId,
                p.DepartmentDestinationId
            }).Where(o => !string.IsNullOrEmpty(o)).Distinct().ToArray();
            var pIds = prescriptions.Select(p => p.UniqueId).ToList();
            var jors = mongo.ActionJournalCollection.AsQueryable().Where(a => pIds.Contains(a.TargetId) && a.TargetType == nameof(Prescription)).ToList();
            var userIds = jors.Select(a => a.PrimaryUserId).Distinct().ToArray();

            var cabinets = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).ToList();
            var departs = mongo.DepartmentCollection.AsQueryable().Where(d => departIds.Contains(d.UniqueId)).ToList().Select(d => new DepartmentProfile
            {
                UniqueId = d.UniqueId,
                DisplayName = d.DisplayName,
                Code = d.Code,
                Computer = cabinets.Where(c => c.DepartmentId == d.UniqueId).Select(o => o.Computer).FirstOrDefault(),
            }).ToArray();
            var users = mongo.UserCollection.AsQueryable().ToList();

            return prescriptions.Select(p =>
            {
                var jour = jors.LastOrDefault(j => j.TargetId == p.UniqueId);
                var dpt = p.Patient?.Hospitalization.ResidedAreaId ?? p.Patient?.Hospitalization.RoomId ?? p.Patient?.Hospitalization.AdmittedDepartmentId ?? p.DepartmentSourceId;
                p.Doctor = p.Doctor ?? new Employee { UniqueId = p.DoctorId, };
                p.Patient = p.Patient ?? new Patient { UniqueId = p.PatientId, };
                p.Goods = p.Goods ?? new Goods { UniqueId = p.GoodsId, };
                return new PrescriptionDetail
                {
                    UniqueId = p.UniqueId,
                    DisplayOrder = p.DisplayOrder,
                    Mode = p.Mode,
                    Doctor = p.Doctor.ToEmployeeProfile(),
                    Patient = p.Patient.ToPatientProfile(),
                    PatientDepartment = departs.FirstOrDefault(o => o.UniqueId == dpt) ?? new DepartmentProfile { UniqueId = dpt, },
                    Goods = p.Goods.ToGoodsProfileQty(string.Empty, DateTime.MaxValue.Date, p.QtyActual),
                    Plans = p.Plans?.Where(o => o.IsExecuted).Select(o =>
                    {
                        var fill = o.Box.Fills.First(x => x.GoodsId == p.GoodsId);
                        return (BatchNumber: fill.BatchNumber ?? string.Empty, ExpiredDate: fill.ExpiredDate.Date, o.Qty);
                    }).GroupBy(f => new { f.BatchNumber, f.ExpiredDate }).Select(f => new PlanProfile
                    {
                        BatchNumber = f.Key.BatchNumber ?? string.Empty,
                        ExpiredDate = f.Key.ExpiredDate.Date,
                        Qty = f.Sum(o => o.Qty),
                    }).ToArray() ?? new PlanProfile[0],
                    Dispensing = new PersonProfile
                    {
                        UniqueId = p.DispensingId,
                        // TODO 查询数据库
                        DisplayName = users.FirstOrDefault(u => u.UniqueId == p.DispensingId)?.Employee?.DisplayName,
                    },
                    DispensingDepartment = departs.FirstOrDefault(o => o.UniqueId == p.DepartmentDestinationId) ?? new DepartmentProfile { UniqueId = p.DepartmentDestinationId, },
                    DispensingTime = p.DispensingTime,
                    Agent = new PersonProfile
                    {
                        UniqueId = p.AgentId,
                        DisplayName = p.Agent?.DisplayName,
                        CertificateCode = p.Agent?.CertificateCode,
                        CertificateType = p.Agent?.CertificateType,
                    },
                    Operator = new UserProfile
                    {
                        UniqueId = jour?.OperatorUserId,
                        DisplayName = jour?.OperatorUserName,
                    },
                    FinishTime = p.FinishTime,
                    IssuedTime = p.IssuedTime,
                    Primary = new UserProfile
                    {
                        UniqueId = jour?.PrimaryUserId,
                        DisplayName = jour?.PrimaryUserName,
                    },
                    Secondary = new UserProfile
                    {
                        UniqueId = jour?.SecondaryUserId,
                        DisplayName = jour?.SecondaryUserName,
                    },
                };
            }).ToArray();
        }

        /// <summary>
        ///     查询指定时间段内的医嘱信息
        /// </summary>
        /// <param name="hospital"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="department">为 null, 查询所有部门, 其余参数则查询指定的部门</param>
        /// <returns></returns>
        [ActionName("search-special-issue-registration")]
        [HttpGet]
        public PrescriptionDetail[] SearchSpecialIssueRegistration(string hospital, DateTime start, DateTime end, string department = null)
        {
            var prescriptions = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && (department == null || p.DepartmentDestinationId == department))
                .Where(x => x.FinishTime >= start && x.FinishTime < end).OrderBy(p => p.TimeFilter).ToList();

            var departIds = prescriptions.SelectMany(p => new[]
            {
                p.Patient?.Hospitalization.ResidedAreaId ?? p.Patient?.Hospitalization.RoomId ?? p.Patient?.Hospitalization.AdmittedDepartmentId,
                p.DepartmentDestinationId
            }).Where(o => !string.IsNullOrEmpty(o)).Distinct().ToArray();
            var pIds = prescriptions.Select(p => p.UniqueId).ToList();
            var jors = mongo.ActionJournalCollection.AsQueryable().Where(a => pIds.Contains(a.TargetId) && a.TargetType == nameof(Prescription)).ToList();
            var userIds = jors.Select(a => a.PrimaryUserId).Distinct().ToArray();

            var cabinets = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).ToList();
            var departs = mongo.DepartmentCollection.AsQueryable().Where(d => departIds.Contains(d.UniqueId)).ToList().Select(d => new DepartmentProfile
            {
                UniqueId = d.UniqueId,
                DisplayName = d.DisplayName,
                Code = d.Code,
                Computer = cabinets.Where(c => c.DepartmentId == d.UniqueId).Select(o => o.Computer).FirstOrDefault(),
            }).ToArray();
            var users = mongo.UserCollection.AsQueryable().Where(u => userIds.Contains(u.UniqueId)).Select(u => new UserProfile
            {
                UniqueId = u.UniqueId,
                LoginId = u.LoginId,
                DisplayName = u.Employee.DisplayName,
                JobNo = u.Employee.JobNo,
                JobTitle = u.Employee.JobTitle,
            }).ToList();

            return prescriptions.Select(p =>
            {
                var jour = jors.LastOrDefault(j => j.TargetId == p.UniqueId);
                var dpt = p.Patient?.Hospitalization.ResidedAreaId ?? p.Patient?.Hospitalization.RoomId ?? p.Patient?.Hospitalization.AdmittedDepartmentId ?? p.DepartmentDestinationId;
                p.Doctor = p.Doctor ?? new Employee { UniqueId = p.DoctorId, };
                p.Patient = p.Patient ?? new Patient { UniqueId = p.PatientId, };
                p.Goods = p.Goods ?? new Goods { UniqueId = p.GoodsId, };
                return new PrescriptionDetail
                {
                    UniqueId = p.UniqueId,
                    DisplayOrder = p.DisplayOrder,
                    Mode = p.Mode,
                    Doctor = p.Doctor.ToEmployeeProfile(),
                    Patient = p.Patient.ToPatientProfile(),
                    PatientDepartment = departs.FirstOrDefault(o => o.UniqueId == dpt) ?? new DepartmentProfile { UniqueId = dpt, },
                    Goods = p.Goods.ToGoodsProfileQty(string.Empty, DateTime.MaxValue.Date, p.QtyActual),
                    Plans = p.Plans?.Where(o => o.IsExecuted).Select(o =>
                    {
                        var fill = o.Box.Fills.First(x => x.GoodsId == p.GoodsId);
                        return (BatchNumber: fill.BatchNumber ?? string.Empty, ExpiredDate: fill.ExpiredDate.Date, o.Qty);
                    }).GroupBy(f => new { f.BatchNumber, f.ExpiredDate }).Select(f => new PlanProfile
                    {
                        BatchNumber = f.Key.BatchNumber ?? string.Empty,
                        ExpiredDate = f.Key.ExpiredDate.Date,
                        Qty = f.Sum(o => o.Qty),
                    }).ToArray() ?? new PlanProfile[0],
                    Dispensing = new PersonProfile
                    {
                        UniqueId = p.DispensingId,
                        // TODO 查询数据库
                    },
                    DispensingDepartment = departs.FirstOrDefault(o => o.UniqueId == p.DepartmentDestinationId) ?? new DepartmentProfile { UniqueId = p.DepartmentDestinationId, },
                    DispensingTime = p.DispensingTime,
                    Agent = new PersonProfile
                    {
                        UniqueId = p.AgentId,
                        DisplayName = p.Agent?.DisplayName,
                        CertificateCode = p.Agent?.CertificateCode,
                        CertificateType = p.Agent?.CertificateType,
                    },
                    Operator = new UserProfile
                    {
                        UniqueId = jour?.OperatorUserId,
                        DisplayName = jour?.OperatorUserName,
                    },
                    FinishTime = p.FinishTime,
                    IssuedTime = p.IssuedTime,
                    Primary = new UserProfile
                    {
                        UniqueId = jour?.PrimaryUserId,
                        DisplayName = jour?.PrimaryUserName,
                    },
                    Secondary = new UserProfile
                    {
                        UniqueId = jour?.SecondaryUserId,
                        DisplayName = jour?.SecondaryUserName,
                    },
                };
            }).ToArray();
        }

        [HttpGet]
        [ActionName("search-prescriptions-by-patient")]
        public PrescriptionDetail[] SearchPrescriptionDetailsByPatient(string hospital, string patient = null, string certificateCode = null, string department = null)
        {
            var patients = mongo.PatientCollection.AsQueryable().Where(x => x.DisplayName == patient || x.CertificateCode == certificateCode).ToList();
            var patientsId = patients.Select(x => x.UniqueId);
            var prescriptions = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && p.FinishTime != null && patientsId.Contains(p.PatientId) && (department == null || p.DepartmentDestinationId == department)).OrderBy(p => p.FinishTime).ToList();
            var departIds = prescriptions.SelectMany(p => new[]
            {
                p.Patient?.Hospitalization.ResidedAreaId ?? p.Patient?.Hospitalization.RoomId ?? p.Patient?.Hospitalization.AdmittedDepartmentId??p.DepartmentSourceId,
                p.DepartmentDestinationId
            }).Where(o => !string.IsNullOrEmpty(o)).Distinct().ToArray();
            var pIds = prescriptions.Select(p => p.UniqueId).ToList();
            var jors = mongo.ActionJournalCollection.AsQueryable().Where(a => pIds.Contains(a.TargetId) && a.TargetType == nameof(Prescription)).ToList();
            var userIds = jors.Select(a => a.PrimaryUserId).Distinct().ToArray();

            var cabinets = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).ToList();
            var departs = mongo.DepartmentCollection.AsQueryable().Where(d => departIds.Contains(d.UniqueId)).ToList().Select(d => new DepartmentProfile
            {
                UniqueId = d.UniqueId,
                DisplayName = d.DisplayName,
                Code = d.Code,
                Computer = cabinets.Where(c => c.DepartmentId == d.UniqueId).Select(o => o.Computer).FirstOrDefault(),
            }).ToArray();
            var users = mongo.UserCollection.AsQueryable().Where(u => userIds.Contains(u.UniqueId)).Select(u => new UserProfile
            {
                UniqueId = u.UniqueId,
                LoginId = u.LoginId,
                DisplayName = u.Employee.DisplayName,
                JobNo = u.Employee.JobNo,
                JobTitle = u.Employee.JobTitle,
            }).ToList();

            return prescriptions.Select(p =>
            {
                var jour = jors.LastOrDefault(j => j.TargetId == p.UniqueId);
                var dpt = p.Patient?.Hospitalization.ResidedAreaId ?? p.Patient?.Hospitalization.RoomId ?? p.Patient?.Hospitalization.AdmittedDepartmentId ?? p.DepartmentSourceId;
                p.Doctor = p.Doctor ?? new Employee { UniqueId = p.DoctorId, };
                p.Patient = p.Patient ?? new Patient { UniqueId = p.PatientId, };
                p.Goods = p.Goods ?? new Goods { UniqueId = p.GoodsId, };
                return new PrescriptionDetail
                {
                    UniqueId = p.UniqueId,
                    DisplayOrder = p.DisplayOrder,
                    Mode = p.Mode,
                    Doctor = p.Doctor.ToEmployeeProfile(),
                    Patient = p.Patient.ToPatientProfile(),
                    PatientDepartment = departs.FirstOrDefault(o => o.UniqueId == dpt) ?? new DepartmentProfile { UniqueId = dpt, },
                    Goods = p.Goods.ToGoodsProfileQty(string.Empty, DateTime.MaxValue.Date, p.QtyActual),
                    Plans = p.Plans?.Where(o => o.IsExecuted).Select(o =>
                    {
                        var fill = o.Box.Fills.First(x => x.GoodsId == p.GoodsId);
                        return (BatchNumber: fill.BatchNumber ?? string.Empty, ExpiredDate: fill.ExpiredDate.Date, o.Qty);
                    }).GroupBy(f => new { f.BatchNumber, f.ExpiredDate }).Select(f => new PlanProfile
                    {
                        BatchNumber = f.Key.BatchNumber ?? string.Empty,
                        ExpiredDate = f.Key.ExpiredDate.Date,
                        Qty = f.Sum(o => o.Qty),
                    }).ToArray() ?? new PlanProfile[0],
                    Dispensing = new PersonProfile
                    {
                        UniqueId = p.DispensingId,
                        // TODO 查询数据库
                    },
                    DispensingDepartment = departs.FirstOrDefault(o => o.UniqueId == p.DepartmentDestinationId) ?? new DepartmentProfile { UniqueId = p.DepartmentDestinationId, },
                    DispensingTime = p.DispensingTime,
                    Agent = new PersonProfile
                    {
                        UniqueId = p.AgentId,
                        DisplayName = p.Agent?.DisplayName,
                        CertificateCode = p.Agent?.CertificateCode,
                        CertificateType = p.Agent?.CertificateType,
                    },
                    Operator = new UserProfile
                    {
                        UniqueId = jour?.OperatorUserId,
                        DisplayName = jour?.OperatorUserName,
                    },
                    FinishTime = p.FinishTime,
                    IssuedTime = p.IssuedTime,
                    Primary = new UserProfile
                    {
                        UniqueId = jour?.PrimaryUserId,
                        DisplayName = jour?.PrimaryUserName,
                    },
                    Secondary = new UserProfile
                    {
                        UniqueId = jour?.SecondaryUserId,
                        DisplayName = jour?.SecondaryUserName,
                    },
                };
            }).ToArray();
        }

        public class UserDataProfile
        {
            public EmployeeProfile PrimaryUser { get; set; }
            public EmployeeProfile SecondaryUser { get; set; }
        }
        /// <summary>
        /// 查询登陆人签名
        /// </summary>
        /// <param name="ids">登陆人id</param>
        /// <returns></returns>
        [HttpPost]
        [ActionName("search-user-signature")]
        public UserDataProfile SearchUserSignature([FromBody] string[] ids)
        {
            var employees = mongo.EmployeeCollection.AsQueryable().ToList();
            return new UserDataProfile
            {
                PrimaryUser = (employees.FirstOrDefault(e => e.UniqueId == ids[0]) ?? new Employee { UniqueId = ids[0], }).ToEmployeeProfile(),
                SecondaryUser = (employees.FirstOrDefault(e => e.UniqueId == ids[1]) ?? new Employee { UniqueId = ids[1], }).ToEmployeeProfile(),
            };
        }
    }
}

// expired-date-warning-chart
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        // searchAllCustomerCore

        public class DepartmentWithCount : DepartmentProfile
        {
            public int Count { get; set; }
        }

        [HttpGet]
        [ActionName("search-departments-with-expired-qty")]
        public DepartmentWithCount[] SearchDepartmentsWithExpiredQty(string customer, DateTime date, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).Select(c => new { c.Cabinets, c.OutOfCabinets, }).ToList();
            var departIds = cs.SelectMany(c => c.Cabinets).Select(c => c.DepartmentId).Distinct().ToList();
            var data = mongo.DepartmentCollection.AsQueryable().Where(d => departIds.Contains(d.UniqueId)).Select(d => new { d.UniqueId, d.DisplayName, d.Code, }).ToList()
                .Select(dpt => new DepartmentWithCount
                {
                    UniqueId = dpt.UniqueId,
                    DisplayName = dpt.DisplayName,
                    Code = dpt.Code,
                    Computer = cs.SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == dpt.UniqueId).Select(c => c.Computer).FirstOrDefault(),
                    Count = cs.SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == dpt.UniqueId).Concat(cs.SelectMany(v => v.OutOfCabinets).Where(v => v.DepartmentId == dpt.UniqueId))
                        .SelectMany(c => c.Drawers).Where(d => !d.IsRecycleBin).SelectMany(d => d.Boxes).SelectMany(b => b.Fills)
                        .Where(f => f.QtyExisted > 0)
                        .Select(f => new { f.GoodsId, BatchNumber = f.BatchNumber ?? string.Empty, ExpiredDate = f.ExpiredDate.Date, })
                        .GroupBy(g => g)
                        .Select(g => g.Where(o => o.ExpiredDate != DateTime.MaxValue.Date).Any() ? (g.Key.GoodsId, g.Key.ExpiredDate) : (GoodsId: string.Empty, ExpiredDate: DateTime.MaxValue))
                        .Where(g => !string.IsNullOrEmpty(g.GoodsId) && g.ExpiredDate <= date)
                        .Count(),
                }).ToArray();
            return data.Where(d => d.Computer == Terminal).Concat(data.Where(d => d.Computer != Terminal)).ToArray();
        }

        public class ExpiredWarning
        {
            [JsonProperty("_id")]
            public string UniqueId { get; set; }
            public string DisplayName { get; set; }
            public double Days { get; set; }
        }

        [HttpGet]
        [ActionName("search-near-expired-goods-for-department")]
        public ExpiredWarning[] SearchNearExpiredGoodsForDepartment(string customer, string department, DateTime date)
        {
            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).Select(c => new
            {
                Cabinets = c.Cabinets.Where(o => o.DepartmentId == department),
                OutOfCabinets = c.OutOfCabinets.Where(o => o.DepartmentId == department),
            }).ToList();

            var data = cs.SelectMany(c => c.Cabinets).Concat(cs.SelectMany(v => v.OutOfCabinets)).SelectMany(c => c.Drawers).Where(d => !d.IsRecycleBin).SelectMany(d => d.Boxes).SelectMany(b => b.Fills)
                    .Where(f => f.QtyExisted > 0)
                    .Select(f => new { f.GoodsId, BatchNumber = f.BatchNumber ?? string.Empty, ExpiredDate = f.ExpiredDate.Date, })
                    .GroupBy(g => g)
                    .Select(g => g.Where(o => o.ExpiredDate != DateTime.MaxValue.Date).Any() ? (g.Key.GoodsId, g.Key.ExpiredDate) : (GoodsId: string.Empty, ExpiredDate: DateTime.MaxValue))
                    .Where(g => !string.IsNullOrEmpty(g.GoodsId) && g.ExpiredDate <= date)
                    .ToList();
            var goodsIds = data.Select(d => d.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).Select(g => new IdName { UniqueId = g.UniqueId, DisplayName = g.DisplayName, }).ToList();

            return data.Select(d => new ExpiredWarning
            {
                UniqueId = d.GoodsId,
                DisplayName = goods.FirstOrDefault(f => f.UniqueId == d.GoodsId)?.DisplayName,
                Days = Math.Ceiling((d.ExpiredDate - DateTime.Now.Date).TotalDays),
            }).OrderBy(d => d.Days).ToArray();
        }
    }
}

// idle-frequency-chart
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        // searchAllCustomerCore

        /// <summary>
        ///     医嘱和预支，未使用的物品
        /// </summary>
        [HttpGet]
        [ActionName("search-departments-with-idle-frequency-qty")]
        public DepartmentWithCount[] SearchDepartmentsWithIdleFrequencyQty(string hospital, string customer, DateTime start, DateTime end, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var preGoods = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && p.IssuedTime >= start && p.IssuedTime < end).Select(p => new { p.DepartmentDestinationId, p.GoodsId, }).ToList();
            var medGoods = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && m.Mode == ExchangeMode.Medication && m.CreatedTime >= start && m.CreatedTime < end).Select(m => new { m.Computer, m.GoodsId, }).ToList();

            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).Select(c => new { c.Cabinets, c.OutOfCabinets, }).ToList();
            var computers = cs.SelectMany(c => c.Cabinets).Select(d => d.Computer).Distinct().ToList();
            var departsLq = mongo.DepartmentCollection.AsQueryable().Where(d => d.UniqueId != null);
            if (hospital == Hospital.SZGJ)
            {
                departsLq = departsLq.Where(d => d.UniqueId != "409");  // 中心药房不用统计
            }

            var data = departsLq.Select(d => new { d.UniqueId, d.DisplayName, d.Code, }).ToList().Select(dpt =>
            {
                var computer = cs.SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == dpt.UniqueId).Select(c => c.Computer).FirstOrDefault();
                var goodsIds = preGoods.Where(p => p.DepartmentDestinationId == dpt.UniqueId).Select(p => p.GoodsId).Concat(medGoods.Where(m => m.Computer == computer).Select(m => m.GoodsId)).ToList();
                var fills = cs.SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == dpt.UniqueId).Concat(cs.SelectMany(v => v.OutOfCabinets).Where(v => v.DepartmentId == dpt.UniqueId))
                        .SelectMany(c => c.Drawers).Where(d => !d.IsRecycleBin).SelectMany(d => d.Boxes).SelectMany(b => b.Fills)
                        .Where(n => goodsIds.All(g => g != n.GoodsId)).ToList();
                return new DepartmentWithCount
                {
                    UniqueId = dpt.UniqueId,
                    DisplayName = dpt.DisplayName,
                    Code = dpt.Code,
                    Count = fills.Select(f => f.GoodsId).Distinct().Count(),
                    Computer = computer,
                };
            }).ToArray();
            return data.Where(d => d.Computer == Terminal).Concat(data.Where(d => d.Computer != Terminal)).ToArray();
        }

        public class IdleFrequencyGoods : IdName
        {
            public double QtyExisted { get; set; }
        }

        [HttpGet]
        [ActionName("search-idle-frequency-goods-for-department")]
        public IdleFrequencyGoods[] SearchIdleFrequencyGoodsForDepartment(string customer, string department, DateTime start, DateTime end)
        {
            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == customer).Select(c => new
            {
                Cabinets = c.Cabinets.Where(o => o.DepartmentId == department),
                OutOfCabinets = c.OutOfCabinets.Where(o => o.DepartmentId == department),
            }).ToList();
            var data = cs.SelectMany(c => c.Cabinets).Concat(cs.SelectMany(v => v.OutOfCabinets)).SelectMany(c => c.Drawers).Where(d => !d.IsRecycleBin).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).ToList();
            // 统计医嘱和预支中使用的物品
            var presGoods = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && p.DepartmentDestinationId == department && p.IssuedTime >= start && p.IssuedTime < end).Select(p => p.GoodsId).ToList();
            var computers = cs.SelectMany(c => c.Cabinets).Select(c => c.Computer).Distinct().ToList();
            var medsGoods = mongo.MedicationCollection.AsQueryable().Where(m => !m.IsDisabled && m.Mode == ExchangeMode.Medication && computers.Contains(m.Computer) && m.CreatedTime >= start && m.CreatedTime < end).Select(p => p.GoodsId).ToList();

            var goodsIds = presGoods.Concat(medsGoods).Concat(data.Select(d => d.GoodsId)).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).Select(g => new { g.UniqueId, g.DisplayName, }).ToList();

            return data.Where(d => presGoods.Concat(medsGoods).Distinct().All(o => o != d.GoodsId))
                .GroupBy(d => d.GoodsId)
                .Select(g => (g.Key, QtyExisted: g.Sum(o => o.QtyExisted)))
                .Select(p => new IdleFrequencyGoods
                {
                    UniqueId = p.Key,
                    DisplayName = goods.FirstOrDefault(f => f.UniqueId == p.Key)?.DisplayName,
                    QtyExisted = p.QtyExisted,
                }).ToArray();
        }

    }
}

// transfer-details
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        // searchAllCustomerCore

        // searchDepartmentsOwnComputer Self

        public class TransferDetail
        {
            public string No { get; set; }
            public GoodsProfile Goods { get; set; }
            public double EstimateQty { get; set; }
            public double ActualQty { get; set; }
            public EmployeeProfile Executor { get; set; }
        }

        [HttpGet]
        [ActionName("search-transfer-details-by-date-range")]
        public TransferDetail[] SearchTransferDetailsByDateRange(string department, DateTime start, DateTime end)
        {
            var computers = Computers(department);
            var ts = mongo.TransferCollection.AsQueryable().Where(t => !t.IsDisabled && computers.Contains(t.Computer) && t.CreatedTime >= start && t.CreatedTime < end).ToList();
            ts.ForEach(t => t.TransferRecords.ForEach(r => r.Goods = r.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == r.GoodsId) ?? new Goods { UniqueId = r.GoodsId, }));
            var executorIds = ts.Select(t => t.Executor).Distinct().ToList();
            var executors = mongo.UserCollection.AsQueryable().Where(u => executorIds.Contains(u.LoginId)).Select(u => new User { LoginId = u.LoginId, DisplayName = u.DisplayName, Employee = u.Employee, }).ToList()
                .Concat(new[] { new User { UniqueId = SfraObject.EmptyId(), LoginId = ServiceStartup.Kernel, DisplayName = "Kernel User", } }).ToList();
            return ts.SelectMany(t =>
            {
                var executor = executors.FirstOrDefault(e => e.LoginId == t.Executor);
                return t.TransferRecords.Select(r => new TransferDetail
                {
                    No = r.No,
                    Goods = r.Goods.ToGoodsProfile(r.BatchNumber, r.ExpiredDate.Date),
                    ActualQty = r.ActualQty,
                    EstimateQty = r.EstimateQty,
                    Executor = (executor?.Employee ?? new Employee { UniqueId = t.Executor, DisplayName = executor?.Employee?.DisplayName ?? t.DisplayName ?? t.Executor }).ToEmployeeProfile(),
                });
            }).ToArray();
        }
    }
}

// daily-consumption-details
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class DailyConsumption
        {
            public GoodsProfile Goods { get; set; }
            public double InitialQty { get; set; }
            public double CheckInQty { get; set; }
            public double CheckOutQty { get; set; }
            public double PresInQty { get; set; }
            public double AllocOutQty { get; set; }
        }

        public class DailyConsumptionder
        {
            public GoodsProfile Goods { get; set; }
            public double InitialQty { get; set; }
            public double CheckInQty { get; set; }
            public double CheckOutQty { get; set; }
            public double PresInQty { get; set; }
            public double AllocOutQty { get; set; }
            public double Unconsumed { get; set; }
            public double Uninstall { get; set; }
        }

        [HttpGet]
        [ActionName("search-daily-consumption-details-by-date-range")]
        public DailyConsumption[] SearchDailyConsumptionDetailsByDateRange(string hospital, DateTime begin, DateTime end, string department)
        {
            // 时间段查询
            // [max(begin, now), e) -> 临时计算
            // [begin, min(now, end)) -> 查数据库

            var tmpBegin = begin >= DateTime.Now.Date ? begin : DateTime.Now.Date;
            var dbEnd = DateTime.Now.Date <= end ? DateTime.Now.Date : end;

            var css = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(o => o.DepartmentId == department).ToList();
            var tmpIvs = css.Select(x => new { x.OwnerCode, x.Computer }).Distinct().SelectMany(x => BuildInventory(x.OwnerCode, x.Computer, tmpBegin, end)).ToList();
            var computers = Computers(department);
            var dbIvs = mongo.InventoryCollection.AsQueryable().Where(j => computers.Contains(j.Computer) && j.StatsTime >= begin && j.StatsTime < dbEnd).ToList();

            var ivs = tmpIvs.Concat(dbIvs).ToList();
            var goodsIds = ivs.Select(iv => iv.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).ToList().Select(g => g.ToGoodsProfile(string.Empty, DateTime.MaxValue.Date)).ToList();

            var jls = mongo.ActionJournalCollection.AsQueryable().Where(aj => aj.Computer == Terminal && aj.CreatedTime >= begin && aj.CreatedTime < end)
                .Where(aj => (aj.TargetType == nameof(Prescription) && aj.Mode == ExchangeMode.CheckIn) || (aj.TargetType == nameof(Allocation) && aj.Mode == ExchangeMode.CheckOut))
                .Select(aj => new { aj.GoodsId, aj.TargetType, aj.Qty, }).ToList();
            return ivs.GroupBy(d => d.GoodsId).Select(gp => new DailyConsumption
            {
                Goods = goods.FirstOrDefault(f => f.UniqueId == gp.Key) ?? new GoodsProfile { UniqueId = gp.Key },
                InitialQty = gp.First().QtyInitial,
                CheckInQty = gp.Sum(g => g.QtyCheckIn),
                CheckOutQty = gp.Sum(g => g.QtyCheckOut),
                PresInQty = jls.Where(x => x.GoodsId == gp.Key && x.TargetType == nameof(Prescription)).Sum(x => x.Qty),
                AllocOutQty = jls.Where(x => x.GoodsId == gp.Key && x.TargetType == nameof(Allocation)).Sum(x => x.Qty),
            }).ToArray();
        }

        [HttpGet]
        [ActionName("search-daily-consumptionder-details")]
        public DailyConsumptionder[] SearchDailyConsumptionderDetails(string hospital, DateTime date, string department)
        {
            List<Inventory> ivs;
            if (date >= DateTime.Now.Date)
            {
                var css = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(o => o.DepartmentId == department).ToList();
                ivs = css.Select(x => new { x.OwnerCode, x.Computer }).Distinct().SelectMany(x => BuildInventory(x.OwnerCode, x.Computer, date, date.AddDays(1))).ToList();
            }
            else
            {
                var computers = Computers(department);
                ivs = mongo.InventoryCollection.AsQueryable().Where(j => computers.Contains(j.Computer) && j.StatsTime >= date && j.StatsTime < date.AddDays(1)).ToList();
            }

            var goodsIds = ivs.Select(iv => iv.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId)).ToList().Select(g => g.ToGoodsProfile(string.Empty, DateTime.MaxValue.Date)).ToList();

            var jls = mongo.ActionJournalCollection.AsQueryable().Where(aj => aj.Computer == Terminal && aj.CreatedTime >= date && aj.CreatedTime < date.AddDays(1))
                        .Where(aj => (aj.TargetType == nameof(Prescription) && aj.Mode == ExchangeMode.CheckIn) || (aj.TargetType == nameof(Allocation) && aj.Mode == ExchangeMode.CheckOut))
                        .Select(aj => new { aj.GoodsId, aj.TargetType, aj.Qty, }).ToList();

            //卸载数量
            var exc = mongo.ExchangeCollection.AsQueryable().Where(ex => ex.Computer == Terminal && ex.CreatedTime >= date && ex.CreatedTime < date.AddDays(1))
                .Where(ex => ex.Mode == ExchangeMode.CheckOut && ex.RecordType == "位置卸载")
                .Select(ex => new { ex.GoodsId, ex.RecordType, ex.Qty }).ToList();

            //未使用未退回的数量
            var exch = mongo.ExchangeCollection.AsQueryable().Where(e => e.Computer == Terminal && e.CreatedTime >= date && e.CreatedTime < date.AddDays(1))
                .Where(e => e.RecordType == nameof(Prescription) && e.Mode == ExchangeMode.CheckOut)
                .Select(e => new { e.GoodsId, e.RecordType, e.Qty });

            return ivs.GroupBy(d => d.GoodsId).Select(gp => new DailyConsumptionder
            {
                Goods = goods.FirstOrDefault(f => f.UniqueId == gp.Key) ?? new GoodsProfile { UniqueId = gp.Key },
                //原存
                InitialQty = gp.Sum(g => g.QtyInitial),
                //收入
                CheckInQty = gp.Sum(g => g.QtyCheckIn),
                //消耗
                CheckOutQty = gp.Sum(g => g.QtyCheckOut),
                //退药
                PresInQty = jls.Where(x => x.GoodsId == gp.Key && x.TargetType == nameof(Prescription)).Sum(x => x.Qty),
                //未使用未退还
                Unconsumed = exch.Where(ex => ex.GoodsId == gp.Key && ex.RecordType == nameof(Prescription)).Sum(x => x.Qty),
                //卸载
                Uninstall = exc.Where(e => e.GoodsId == gp.Key && e.RecordType == "位置卸载").Sum(e => e.Qty),

            }).ToArray();
        }
    }
}

// daily-consumption-summary-details
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        [HttpGet]
        [ActionName("search-goods-for-daily-consumption")]
        public GoodsProfile[] SearchGoodsForDailyConsumption(bool ampoule, string department)
        {
            var computers = Computers(department);
            var goodsIds = mongo.TerminalGoodsCollection.AsQueryable().Where(t => computers.Contains(t.Computer)).Select(t => t.GoodsId).ToList();
            return mongo.GoodsCollection.AsQueryable().Where(g => goodsIds.Contains(g.UniqueId) && g.IsAmpoule == ampoule).OrderBy(g => g.Pinyin).ToList().Select(g => g.ToGoodsProfile(string.Empty, DateTime.MaxValue.Date)).ToArray();
        }

        [HttpGet]
        [ActionName("search-daily-consumption-summary-details")]
        public DailyConsumption[] SearchDailyConsumptionSummaryDetails(string hospital, DateTime start, DateTime end, string goods, string department)
        {

            var computers = Computers(department);
            var dsjs = mongo.InventoryCollection.AsQueryable().Where(j => computers.Contains(j.Computer) && j.StatsTime >= start && j.StatsTime < end && j.GoodsId == goods)
                .Select(d => new { d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime, }).ToList();
            return Enumerable.Range(0, Math.Max(0, (end - start).Days)).Select(d => start.AddDays(d)).Select(date =>
            {
                (double QtyInitial, double QtyCheckIn, double QtyCheckOut, DateTime StatsTime)[] crnt;
                if (date >= DateTime.Now.Date)
                {
                    var css = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(o => o.DepartmentId == department).ToList();
                    var find = css.Select(c => new { c.Computer, c.OwnerCode, }).FirstOrDefault();
                    crnt = BuildInventory(find?.OwnerCode, find?.Computer, date, date.AddDays(1)).Where(d => d.GoodsId == goods).Select(d => (d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime)).ToArray();
                }
                else
                {
                    crnt = dsjs.Where(d => d.StatsTime >= date && d.StatsTime < date.AddDays(1)).Select(d => (d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime)).ToArray();
                }
                return new DailyConsumption
                {
                    Goods = null,
                    InitialQty = crnt.Sum(c => c.QtyInitial),
                    CheckInQty = crnt.Sum(c => c.QtyCheckIn),
                    CheckOutQty = crnt.Sum(c => c.QtyCheckOut),
                };
            }).ToArray();
        }
    }
}

//search-daily-consumption-recording
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class DailyConsumptionRecord
        {
            public DateTime SearchTime { get; set; }
            public GoodsInventory[] Details { get; set; }
            public string Recorder { get; set; }
        }
        public class GoodsInventory
        {
            public string GoodsId { get; set; }
            public double InitialQty { get; set; }
            public double CheckInQty { get; set; }
            public double CheckOutQty { get; set; }

        }

        [HttpPut]
        [ActionName("search-daily-consumptiond-records")]
        public DailyConsumptionRecord[] SearchDailyConsumptionRecords(string hospital, DateTime start, DateTime end, string department)
        {
            var computers = Computers(department);
            var dsjs = mongo.InventoryCollection.AsQueryable().Where(j => computers.Contains(j.Computer) && j.StatsTime >= start && j.StatsTime < end).ToList().GroupBy(x => new { x.StatsTime.Date, x.GoodsId })
                .Select(x => new { SearchTime = x.Key.Date, x.Key.GoodsId, QtyInitial = x.Sum(y => y.QtyInitial), QtyCheckIn = x.Sum(y => y.QtyCheckIn), QtyCheckOut = x.Sum(y => y.QtyCheckOut) });
            var actionJournal = mongo.ActionJournalCollection.AsQueryable().Where(aj => computers.Contains(aj.Computer) && aj.CreatedTime >= start && aj.CreatedTime < end && aj.Qty > 0).ToList();
            var goodsIds = mongo.TerminalGoodsCollection.AsQueryable().Where(t => computers.Contains(t.Computer)).Select(t => t.GoodsId).OrderBy(x => x).ToList();
            return Enumerable.Range(0, Math.Max(0, (end - start).Days)).Select(d => start.AddDays(d)).Select(date =>
            {
                return new DailyConsumptionRecord
                {
                    SearchTime = date,
                    Details = goodsIds.Select(goods =>
                    {
                        var Inventories = dsjs.Where(x => x.SearchTime.Day == date.Day && x.GoodsId == goods);
                        return new GoodsInventory
                        {
                            GoodsId = goods,
                            InitialQty = Inventories.Sum(c => c.QtyInitial) + Inventories.Sum(c => c.QtyCheckIn) - Inventories.Sum(c => c.QtyCheckOut),
                            CheckInQty = Inventories.Sum(c => c.QtyCheckIn),
                            CheckOutQty = Inventories.Sum(c => c.QtyCheckOut),
                        };
                    }).ToArray(),
                    Recorder = actionJournal.Where(x => x.CreatedTime.Day == date.Day && goodsIds.Contains(x.GoodsId) && x.PrimaryUserName != null).FirstOrDefault()?.PrimaryUserName,
                };
            }).Where(d => d.Details.Sum(i => i.InitialQty) > 0).ToArray();
        }
    }
}

// search-daily-consumption-statistics
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class DailyConsumptionDetails
        {
            public DateTime SeachTime { get; set; }
            public string CheckInNumber { get; set; }
            public string CheckInBatchNumber { get; set; }
            public DateTime? CheckInExpiredDate { get; set; }
            public ExchangeMode? Mode { get; set; }
            public string DepartmentDestination { get; set; }
            public string BatchNumber { get; set; }
            public DateTime? ExpiredDate { get; set; }
            public double InitialQty { get; set; }
            public double CheckInQty { get; set; }
            public double CheckOutQty { get; set; }
            public double PresOutQty { get; set; }
            public double PresInQty { get; set; }
            public string Booker { get; set; }
            public AllocaProfile Allocation { get; set; }
            public UserProfile Primary { get; set; }
            public UserProfile Secondary { get; set; }
        }

        [HttpGet]
        [ActionName("search-daily-consumption-statistics")]
        public DailyConsumptionDetails[] SearchDailyConsumptionStatistics(string hospital, DateTime start, DateTime end, string goods, string department)
        {
            //获取操作记录里batchNumber
            var computers = Computers(department);
            var departments = VisitorDepartments(hospital, department);
            var actionJournal = mongo.ActionJournalCollection.AsQueryable().Where(aj => computers.Contains(aj.Computer) && aj.GoodsId == goods && aj.CreatedTime >= start && aj.CreatedTime < end && aj.Qty > 0).ToList();
            //获取清单
            var dsjs = mongo.InventoryCollection.AsQueryable().Where(j => computers.Contains(j.Computer) && j.StatsTime >= start && j.StatsTime < end && j.GoodsId == goods).ToList();
            return dsjs.GroupBy(d => new { d.BatchNumber, d.ExpiredDate, }).SelectMany(b =>
            {
                return Enumerable.Range(0, Math.Max(0, (end - start).Days)).Select(d => start.AddDays(d)).Select(date =>
                {
                    (double QtyInitial, double QtyCheckIn, double QtyCheckOut, DateTime StatsTime, string GoodsId, string BatchNumber)[] crnt;
                    if (date >= DateTime.Now.Date)
                    {
                        var css = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(o => o.DepartmentId == department).ToList();
                        var find = css.Select(c => new { c.Computer, c.OwnerCode, }).FirstOrDefault();
                        crnt = BuildInventory(find?.OwnerCode, find?.Computer, date, date.AddDays(1)).Where(d => d.GoodsId == goods && d.BatchNumber == b.Key.BatchNumber && d.ExpiredDate == b.Key.ExpiredDate).Select(d => (d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime, d.GoodsId, d.BatchNumber)).ToArray();
                    }
                    else
                    {
                        crnt = dsjs.Where(d => d.StatsTime >= date && d.StatsTime < date.AddDays(1)).Where(d => d.BatchNumber == b.Key.BatchNumber && d.ExpiredDate == b.Key.ExpiredDate).Select(d => (d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime, d.GoodsId, d.BatchNumber)).ToArray();
                    }
                    //获取 操作记录、调拨、取药医嘱
                    var action = actionJournal.FirstOrDefault(g => g.CreatedTime >= date && g.CreatedTime < date.AddDays(1) && g.TargetType == nameof(Allocation) && g.BatchNumber == b.Key.BatchNumber && g.ExpiredDate == b.Key.ExpiredDate) ?? new ActionJournal { };
                    var alloc = mongo.AllocationCollection.AsQueryable().FirstOrDefault(g => g.UniqueId == action.TargetId);
                    var prescriptions = mongo.PrescriptionCollection.AsQueryable()
                       .Where(d => !d.IsDisabled && !d.IsAddition && d.TimeFilter >= date && d.TimeFilter < date.AddDays(1) && d.Mode == ExchangeMode.CheckOut && departments.Contains(d.DepartmentDestinationId)).ToList()
                       .Where(d => d.QtyActual < d.Qty && d.BatchNumber == b.Key.BatchNumber && d.GoodsId == goods).ToList();
                    return new DailyConsumptionDetails
                    {
                        SeachTime = date,
                        CheckInNumber = alloc?.ApplyId,
                        CheckInBatchNumber = alloc?.BatchNumber,
                        CheckInExpiredDate = alloc?.ExpiredDate,
                        Mode = alloc?.Mode,
                        DepartmentDestination = alloc?.DepartmentDestination.DisplayName,
                        BatchNumber = b.Key.BatchNumber,
                        ExpiredDate = b.Key.ExpiredDate,
                        InitialQty = crnt.Sum(g => g.QtyInitial),
                        CheckInQty = crnt.Sum(g => g.QtyCheckIn),
                        CheckOutQty = crnt.Sum(g => g.QtyCheckOut),
                        PresOutQty = prescriptions.Sum(g => g.QtyActual)
                    };
                }).Where(o => o.InitialQty > 0 || o.CheckInQty > 0 || o.CheckOutQty > 0).ToArray();
            }).OrderBy(d => d.SeachTime).ToArray();
        }
        /// <summary>
        ///  上海青浦特殊药品专用账册
        /// </summary>
        /// <param name="hospital"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="goods"></param>
        /// <param name="department"></param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-daily-consumption-depart-group")]
        public DailyConsumptionDetails[] SearchDailyConsumptionDepartGroup(string hospital, DateTime start, DateTime end, string goods, string department)
        {
            //获取操作记录里batchNumber
            var computers = Computers(department);
            var departments = VisitorDepartments(hospital, department);
            var actionJournal = mongo.ActionJournalCollection.AsQueryable().Where(aj => computers.Contains(aj.Computer) && aj.GoodsId == goods && aj.CreatedTime >= start && aj.CreatedTime < end && aj.Qty > 0).ToList();
            var batchNumArray = actionJournal.Select(g => new { BatchNumber = g.BatchNumber ?? string.Empty, ExpiredDate = g.ExpiredDate.Date }).Distinct().ToArray();
            //获取清单
            var dsjs = mongo.InventoryCollection.AsQueryable().Where(j => computers.Contains(j.Computer) && j.StatsTime >= start && j.StatsTime < end && j.GoodsId == goods)
                .Select(d => new { d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime, d.GoodsId, d.BatchNumber, d.ExpiredDate }).ToList();
            var batchNumber = string.Empty;
            var expiredDate = DateTime.MaxValue;
            var allocations = mongo.AllocationCollection.AsQueryable().Where(a => !a.IsDisabled && a.GoodsId == goods).ToArray();
            var prescriptions = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && p.GoodsId == goods).ToArray();
            var departs = mongo.DepartmentCollection.AsQueryable();
            return batchNumArray.SelectMany(b =>
            {
                return Enumerable.Range(0, Math.Max(0, (end - start).Days)).Select(d => start.AddDays(d)).Select(date =>
                 {
                     (double QtyInitial, double QtyCheckIn, double QtyCheckOut, DateTime StatsTime, string GoodsId, string BatchNumber)[] crnt;
                     if (date >= DateTime.Now.Date)
                     {
                         var css = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(o => o.DepartmentId == department).ToList();
                         var find = css.Select(c => new { c.Computer, c.OwnerCode, }).FirstOrDefault();
                         crnt = BuildInventory(find?.OwnerCode, find?.Computer, date, date.AddDays(1)).Where(d => d.GoodsId == goods && d.BatchNumber == b.BatchNumber && d.ExpiredDate == b.ExpiredDate).Select(d => (d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime, d.GoodsId, d.BatchNumber)).ToArray();
                     }
                     else
                     {
                         crnt = dsjs.Where(d => d.StatsTime >= date && d.StatsTime < date.AddDays(1)).Where(d => d.BatchNumber == b.BatchNumber && d.ExpiredDate == b.ExpiredDate).Select(d => (d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime, d.GoodsId, d.BatchNumber)).ToArray();
                     }
                     //获取 操作记录、调拨、取药医嘱
                     var action = actionJournal.FirstOrDefault(g => g.CreatedTime >= date && g.CreatedTime < date.AddDays(1) && g.TargetType == nameof(Allocation) && g.BatchNumber == b.BatchNumber && g.ExpiredDate == b.ExpiredDate) ?? new ActionJournal { };
                     if (action.BatchNumber != string.Empty)
                     {
                         batchNumber = action.BatchNumber;
                         expiredDate = action.ExpiredDate;
                     }
                     var jour = actionJournal.Where(g => g.CreatedTime >= date && g.CreatedTime < date.AddDays(1) && g.TargetType == nameof(Prescription) && g.BatchNumber == b.BatchNumber && g.ExpiredDate == b.ExpiredDate);
                     var alloc = allocations.FirstOrDefault(g => g.UniqueId == action.TargetId);
                     if (jour.Count() > 0)
                     {
                         var itial = crnt.Sum(g => g.QtyInitial);
                         return jour.Join(prescriptions, a => a.TargetId, d => d.UniqueId, (a, d) => new { a, d.DepartmentSourceId }).GroupBy(d => d.DepartmentSourceId)
                         .Select(j =>
                         {
                             var sourceId = j.FirstOrDefault()?.DepartmentSourceId;
                             var sourceName = departs.Where(d => d.UniqueId == sourceId).FirstOrDefault()?.DisplayName;
                             itial = itial - j.Sum(g => g.a.Qty);
                             return new DailyConsumptionDetails
                             {
                                 SeachTime = date,
                                 CheckInNumber = alloc?.ApplyId,
                                 CheckInBatchNumber = alloc?.BatchNumber,
                                 CheckInExpiredDate = alloc?.ExpiredDate,
                                 Mode = alloc?.Mode,
                                 DepartmentDestination = sourceName ?? alloc?.DepartmentDestination.DisplayName,
                                 BatchNumber = batchNumber,
                                 ExpiredDate = expiredDate,
                                 InitialQty = itial + crnt.Sum(g => g.QtyCheckIn),
                                 CheckInQty = crnt.Sum(g => g.QtyCheckIn),
                                 CheckOutQty = j.Sum(g => g.a.Qty),
                                 Primary = new UserProfile
                                 {
                                     UniqueId = j.FirstOrDefault()?.a.PrimaryUserId,
                                     DisplayName = j.FirstOrDefault()?.a.PrimaryUserName,
                                 },
                                 Secondary = new UserProfile
                                 {
                                     UniqueId = j.FirstOrDefault()?.a.SecondaryUserId,
                                     DisplayName = j.FirstOrDefault()?.a.SecondaryUserName,
                                 },
                             };
                         }).ToArray();
                     }
                     else
                     {
                         return new List<DailyConsumptionDetails>(){
                             new DailyConsumptionDetails{
                             SeachTime = date,
                             CheckInNumber = alloc?.ApplyId,
                             CheckInBatchNumber = alloc?.BatchNumber,
                             CheckInExpiredDate = alloc?.ExpiredDate,
                             Mode = alloc?.Mode,
                             DepartmentDestination = alloc?.DepartmentDestination.DisplayName,
                             BatchNumber = batchNumber,
                             ExpiredDate = expiredDate,
                             InitialQty =crnt.Sum(g => g.QtyCheckIn) + crnt.Sum(g => g.QtyInitial)-crnt.Sum(g => g.QtyCheckOut),
                             CheckInQty = crnt.Sum(g => g.QtyCheckIn),
                             CheckOutQty = crnt.Sum(g => g.QtyCheckOut),
                             Primary = new UserProfile
                             {
                                UniqueId = action?.PrimaryUserId,
                                DisplayName = action?.PrimaryUserName,
                             },
                             Secondary = new UserProfile
                             {
                                UniqueId = action?.SecondaryUserId,
                                DisplayName = action?.SecondaryUserName,
                             },
                             }
                         }.ToArray();
                     }
                 }).SelectMany(d => d).ToArray();
            }).OrderBy(d => d.SeachTime).ToArray();
        }

        [HttpGet]
        [ActionName("search-daily-consumption")]
        public DailyConsumptionDetails[] SearchDailyConsumption(string hospital, DateTime start, DateTime end, string goods, string department)
        {
            var computers = Computers(department);
            //获取批号
            var actionJournal = mongo.ActionJournalCollection.AsQueryable().Where(aj => computers.Contains(aj.Computer) && aj.GoodsId == goods && aj.CreatedTime >= start && aj.CreatedTime < end && aj.Qty > 0).ToList();
            var inventory = mongo.InventoryCollection.AsQueryable().Where(i => computers.Contains(i.Computer) && i.GoodsId == goods && i.StatsTime >= start && i.StatsTime < end).Select(x => x.BatchNumber).ToList();
            var batchExpiredArray = actionJournal.Select(g => g.BatchNumber).Union(inventory).Where(b => b != null).Distinct().ToArray();
            //获取清单
            var dsjs = mongo.InventoryCollection.AsQueryable().Where(j => computers.Contains(j.Computer) && batchExpiredArray.Contains(j.BatchNumber) && j.StatsTime >= start && j.StatsTime < end && j.GoodsId == goods)
                 .Select(d => new { d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime, d.GoodsId, d.BatchNumber }).ToList();
            var batchNumber = string.Empty;
            var expiredDate = DateTime.MaxValue;
            return batchExpiredArray.SelectMany(b =>
            {
                return Enumerable.Range(0, Math.Max(0, (end - start).Days)).Select(d => start.AddDays(d)).Select(date =>
                {
                    (double QtyInitial, double QtyCheckIn, double QtyCheckOut, DateTime StatsTime, string GoodsId, string BatchNumber)[] crnt;
                    if (date >= DateTime.Now.Date)
                    {
                        var css = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(o => o.DepartmentId == department).ToList();
                        var find = css.Select(c => new { c.Computer, c.OwnerCode, }).FirstOrDefault();
                        crnt = BuildInventory(find?.OwnerCode, find?.Computer, date, date.AddDays(1)).Where(d => d.GoodsId == goods && d.BatchNumber == b).Select(d => (d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime, d.GoodsId, d.BatchNumber)).ToArray();
                    }
                    else
                    {
                        crnt = dsjs.Where(d => d.StatsTime >= date && d.StatsTime < date.AddDays(1)).Where(d => d.BatchNumber == b).Select(d => (d.QtyInitial, d.QtyCheckIn, d.QtyCheckOut, d.StatsTime, d.GoodsId, d.BatchNumber)).ToArray();
                    }
                    // 获取登记人
                    var booker = mongo.AccessJournalCollection.AsQueryable().Where(a => computers.Contains(a.Computer)).ToList().FirstOrDefault(a =>
                    {
                        return a.CreatedTime.ToShortDateString() == date.ToShortDateString();
                    });
                    // 获取 调拨、退药医嘱
                    var allocAction = actionJournal.FirstOrDefault(g => g.CreatedTime >= date && g.CreatedTime < date.AddDays(1) && g.TargetType == nameof(Allocation) && g.BatchNumber == b) ?? new ActionJournal { };
                    if (allocAction.BatchNumber != string.Empty)
                    {
                        batchNumber = allocAction.BatchNumber;
                        expiredDate = allocAction.ExpiredDate;
                    }
                    else
                    {
                        batchNumber = b;
                    }
                    var alloc = mongo.AllocationCollection.AsQueryable().FirstOrDefault(g => g.UniqueId == allocAction.TargetId);
                    var prescriptions = actionJournal.Where(g => g.CreatedTime >= date && g.CreatedTime < date.AddDays(1) && g.TargetType == nameof(Prescription) && g.Mode == ExchangeMode.CheckIn && g.BatchNumber == b).ToArray();
                    return new DailyConsumptionDetails
                    {
                        SeachTime = date,
                        BatchNumber = batchNumber,
                        ExpiredDate = expiredDate,
                        InitialQty = crnt.Sum(g => g.QtyInitial),
                        CheckInQty = crnt.Sum(g => g.QtyCheckIn),
                        CheckOutQty = crnt.Sum(g => g.QtyCheckOut),
                        PresInQty = prescriptions.Sum(g => g.Qty),
                        Booker = booker?.UserName,
                        Allocation = new AllocaProfile
                        {
                            UniqueId = alloc?.UniqueId,
                            Goods = alloc?.Goods.ToGoodsProfile(alloc.BatchNumber, alloc.ExpiredDate.Date),
                            Qty = alloc?.QtyActual,
                            Time = alloc?.ReceivedTime,
                            ApplyId = alloc?.ApplyId,
                            Person = alloc?.ReceiverName,
                            Deliverer = alloc?.DelivererName,
                        }
                    };
                }).ToArray();
            }).OrderBy(d => d.SeachTime).ToArray();
        }

        /// <summary>
        /// 滕州门诊出入库登记
        /// </summary>
        /// <param name="hospital"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="goods"></param>
        /// <param name="department"></param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-clinic-consumption")]
        public DailyConsumptionDetails[] SearchClinicConsumption(string hospital, DateTime start, DateTime end, string goods, string department)
        {
            var computers = Computers(department);
            var actionJournal = mongo.ActionJournalCollection.AsQueryable().Where(x => computers.Contains(x.Computer) && x.GoodsId == goods && x.CreatedTime >= start && x.CreatedTime < end).ToList();
            var allocations = mongo.AllocationCollection.AsQueryable().Where(x => computers.Contains(x.Computer) && x.GoodsId == goods && x.FinishTime >= start && x.FinishTime < end).ToList();
            var pres = actionJournal.Where(x => x.TargetType == nameof(Prescription));
            var alloRange = allocations.GroupBy(x => (DateTime)x.FinishTime).Select(x => x.Key).OrderBy(x => x).ToList();

            var monthRange = Enumerable.Range(1, end.Month).Select(d => new DateTime(start.Year, d, 1));
            var dateRange = monthRange.Union(alloRange).Where(x => x >= start).OrderBy(x => x).ToList();
            var inventoryDate = dateRange.FirstOrDefault();

            var inventories = mongo.InventoryCollection.AsQueryable().Where(x => x.StatsTime >= start && x.StatsTime < end && x.GoodsId == goods).ToList();
            var initialInventory = inventories.Where(x => x.StatsTime.Date == inventoryDate);
            var allocationFromActions = mongo.ActionJournalCollection.AsQueryable().Where(x => x.TargetType == nameof(Allocation) && computers.Contains(x.Computer) && x.GoodsId == goods && x.CreatedTime >= inventoryDate && x.CreatedTime < end).ToList();
            var dataRangeConsumption = dateRange.SelectMany(endDateTime =>
            {
                var startDateTime = new DateTime();
                var currIndex = dateRange.IndexOf(endDateTime);
                if (currIndex != 0)
                {
                    startDateTime = dateRange[currIndex - 1];
                }
                var batchExpiredCheckOut = new List<DailyConsumptionDetails>();
                var batchExpiredCheckIn = new List<DailyConsumptionDetails>();
                if (currIndex != 0)
                {
                    batchExpiredCheckOut = pres.Where(x => x.CreatedTime >= startDateTime && x.CreatedTime <= endDateTime).GroupBy(x => new { x.BatchNumber, ExpiredDate = x.ExpiredDate })
                        .Select(x => new DailyConsumptionDetails
                        {
                            BatchNumber = x.Key.BatchNumber,
                            ExpiredDate = x.Key.ExpiredDate,
                            CheckOutQty = x.Sum(y => y.Qty)
                        }).ToList();
                }
                var allocation = allocations.FirstOrDefault(x => x.FinishTime == endDateTime);
                if (allocation != null)
                {
                    var actions = allocationFromActions.Where(x => x.TargetId == allocation.UniqueId).ToList();
                    batchExpiredCheckIn = actions.GroupBy(x => new { x.BatchNumber, ExpiredDate = x.ExpiredDate.Date }).Select(x =>
                    {
                        return new DailyConsumptionDetails
                        {
                            SeachTime = endDateTime,
                            BatchNumber = x.Key.BatchNumber,
                            ExpiredDate = x.Key.ExpiredDate,
                            CheckInQty = x.Sum(y => y.Qty),
                            Allocation = new AllocaProfile
                            {
                                UniqueId = allocation?.UniqueId,
                                Goods = allocation?.Goods.ToGoodsProfile(allocation.BatchNumber, allocation.ExpiredDate.Date),
                                Qty = allocation?.QtyActual,
                                Time = allocation?.ReceivedTime,
                                ApplyId = allocation?.ApplyId,
                                Person = allocation?.ReceiverName,
                                Deliverer = allocation?.DelivererName,
                            }
                        };
                    }).ToList();
                }
                else
                {
                    var batchs = batchExpiredCheckOut.Select(x => new { x.BatchNumber, x.ExpiredDate });
                    batchExpiredCheckIn = allocationFromActions.Where(x => x.CreatedTime >= startDateTime && x.CreatedTime <= endDateTime).GroupBy(x => new { x.BatchNumber, ExpiredDate = x.ExpiredDate })
                        .Select(x => new DailyConsumptionDetails
                        {
                            SeachTime = endDateTime,
                            BatchNumber = x.Key.BatchNumber,
                            ExpiredDate = x.Key.ExpiredDate
                        }).ToList();

                }
                var inibatchExpired = inventories.Where(x => x.StatsTime == endDateTime.Date).Select(x => new DailyConsumptionDetails
                {
                    SeachTime = endDateTime,
                    BatchNumber = x.BatchNumber,
                    ExpiredDate = x.ExpiredDate,
                    CheckOutQty = 0,
                    CheckInQty = 0
                });
                return batchExpiredCheckOut.Union(batchExpiredCheckIn).Union(inibatchExpired).Where(x => !string.IsNullOrEmpty(x.BatchNumber)).GroupBy(x => new { x.BatchNumber, ExpiredDate = (x.ExpiredDate ?? DateTime.MaxValue).Date, })
                    .Select(x =>
                    {
                        return new DailyConsumptionDetails
                        {
                            SeachTime = endDateTime,
                            BatchNumber = x.Key.BatchNumber,
                            ExpiredDate = x.Key.ExpiredDate,
                            CheckOutQty = x.Sum(y => y.CheckOutQty),
                            CheckInQty = x.Sum(y => y.CheckInQty),
                            Allocation = x.FirstOrDefault(y => y.Allocation != null)?.Allocation
                        };
                    });
            }).ToArray();

            var dataRangeConsumptionGroupByTime = dataRangeConsumption.GroupBy(x => x.SeachTime).ToList();
            for (int i = 0; i < dataRangeConsumptionGroupByTime.Count(); i++)
            {
                var dailyConsumptionDetails = dataRangeConsumptionGroupByTime[i].ToList();
                if (i > 0)
                {
                    dailyConsumptionDetails.ForEach(x =>
                    {
                        var crtIndex = dailyConsumptionDetails.IndexOf(x);
                        var lant = dataRangeConsumptionGroupByTime[i - 1].FirstOrDefault(y => y.BatchNumber == x.BatchNumber && y.ExpiredDate == x.ExpiredDate);
                        x.InitialQty = lant != null ? lant.PresOutQty : 0;
                        x.PresOutQty = x.InitialQty + x.CheckInQty - x.CheckOutQty;
                        if (x.SeachTime == new DateTime(x.SeachTime.Year, x.SeachTime.Month, x.SeachTime.Day, 0, 0, 0))
                        {
                            if (dailyConsumptionDetails.Count > 1 && crtIndex > 0)
                            {
                                if (dailyConsumptionDetails[crtIndex - 1].SeachTime == new DateTime(x.SeachTime.Year, x.SeachTime.Month, x.SeachTime.Day, 0, 0, 0))
                                {
                                    x.CheckInQty = 0;
                                    x.CheckOutQty = 0;
                                    x.InitialQty = 0;
                                }
                            }
                            if (x.PresOutQty != 0 && crtIndex == 0)
                            {
                                var crnt = dataRangeConsumption.Where(t => t.SeachTime.ToString("yyyy-MM") == x.SeachTime.AddMonths(-1).ToString("yyyy-MM")).OrderBy(o => o.SeachTime);
                                x.CheckInQty = crnt != null ? crnt.Where(t => t.Allocation != null).Sum(s => s.CheckInQty) : 0;
                                x.CheckOutQty = crnt != null ? crnt.Where(t => t.SeachTime.ToString("HHmmss") != "000000").Sum(s => s.CheckOutQty) + (x.InitialQty - x.PresOutQty) : 0;
                                x.InitialQty = 0;
                            }
                        }
                    });
                }
                else
                {
                    dailyConsumptionDetails.ForEach(x =>
                    {
                        var firstInventory = initialInventory.FirstOrDefault(y => y.BatchNumber == x.BatchNumber && y.ExpiredDate == x.ExpiredDate && y.StatsTime == x.SeachTime.Date);
                        double firstRangeInventory = 0;
                        if (firstInventory != null)
                        {
                            firstRangeInventory = pres.Where(y => y.CreatedTime >= firstInventory.StatsTime && y.CreatedTime < x.SeachTime).Sum(y => y.Mode == ExchangeMode.CheckIn ? -y.Qty : y.Qty);
                        }

                        x.InitialQty = firstInventory != null ? firstInventory.QtyInitial : 0 - firstRangeInventory;
                        x.PresOutQty = x.InitialQty + x.CheckInQty;
                    });
                }

            }
            // 获取登记人
            var accesses = mongo.AccessJournalCollection.AsQueryable().Where(a => computers.Contains(a.Computer)).ToList();
            return dataRangeConsumption.Select(x =>
             {
                 var access = accesses.FirstOrDefault(a => a.CreatedTime.ToShortDateString() == x.SeachTime.ToShortDateString());
                 string booker = access?.UserName;
                 if (x.Allocation != null)
                 {
                     booker = x.Allocation.Person;
                 }
                 return new DailyConsumptionDetails
                 {
                     SeachTime = (x.SeachTime.Day == 1 && x.Allocation == null) == true ? x.SeachTime.AddSeconds(-1).Date : x.SeachTime,
                     BatchNumber = x.BatchNumber,
                     ExpiredDate = x.ExpiredDate,
                     InitialQty = x.InitialQty,
                     CheckOutQty = x.CheckOutQty,
                     CheckInQty = x.CheckInQty,
                     PresOutQty = (x.SeachTime.Day == 1 && x.Allocation == null) == true ? x.PresOutQty : (x.InitialQty + x.CheckInQty - x.CheckOutQty),
                     Booker = booker,
                     Allocation = x.Allocation
                 };
             }).Where(x => x.CheckInQty != 0 || x.CheckOutQty != 0 || x.PresOutQty != 0).ToArray();
        }
        /// <summary>
        /// 滕州住院出入库登记
        /// </summary>
        /// <param name="hospital"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="goods"></param>
        /// <param name="department"></param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-hospitalization-consumption")]
        public DailyConsumptionDetails[] SearchHospitalizationConsumption(string hospital, DateTime start, DateTime end, string goods, string department)
        {
            var computers = Computers(department);
            var actionJournal = mongo.ActionJournalCollection.AsQueryable().Where(x => computers.Contains(x.Computer) && x.GoodsId == goods && x.CreatedTime >= start && x.CreatedTime < end).ToList();
            var allocations = mongo.AllocationCollection.AsQueryable().Where(x => computers.Contains(x.Computer) && x.GoodsId == goods && x.FinishTime >= start && x.FinishTime < end);
            var pres = actionJournal.Where(x => x.TargetType == nameof(Prescription));
            var alloRange = allocations.GroupBy(x => (DateTime)x.FinishTime).Select(x => x.Key).OrderBy(x => x).ToList();
            var monthRange = Enumerable.Range(1, end.AddMonths(-1).Month).Select(d => new DateTime(start.Year, d, 1, 23, 59, 59).AddMonths(1).AddDays(-1));
            var dateRange = monthRange.Union(alloRange).Where(x => x >= alloRange.FirstOrDefault()).OrderBy(x => x).ToList();
            var inventoryDate = alloRange.FirstOrDefault().Date;
            var inventories = mongo.InventoryCollection.AsQueryable().Where(x => x.StatsTime >= start && x.StatsTime < end && x.GoodsId == goods).ToList();
            var initialInventory = inventories.Where(X => X.StatsTime.Date == inventoryDate);
            var allocationFromActions = mongo.ActionJournalCollection.AsQueryable().Where(x => x.TargetType == nameof(Allocation) && computers.Contains(x.Computer) && x.GoodsId == goods && x.CreatedTime >= inventoryDate && x.CreatedTime < end).ToList();
            var dataRangeConsumption = dateRange.SelectMany(endDateTime =>
            {
                var startDateTime = new DateTime();
                var currIndex = dateRange.IndexOf(endDateTime);
                if (currIndex != 0)
                {
                    startDateTime = dateRange[currIndex - 1];
                }
                var batchExpiredCheckIn = new List<DailyConsumptionDetails>();

                var allocation = allocations.FirstOrDefault(x => x.FinishTime == endDateTime);
                if (allocation != null)
                {
                    var actions = allocationFromActions.Where(x => x.TargetId == allocation.UniqueId).ToList();
                    batchExpiredCheckIn = actions.GroupBy(x => new { x.BatchNumber, ExpiredDate = new DateTime(x.ExpiredDate.Year, x.ExpiredDate.Month, 1) }).Select(x =>
                        {
                            return new DailyConsumptionDetails
                            {
                                BatchNumber = x.Key.BatchNumber,
                                ExpiredDate = x.Key.ExpiredDate,
                                CheckInQty = x.Sum(y => y.Qty),
                                Allocation = new AllocaProfile
                                {
                                    UniqueId = allocation?.UniqueId,
                                    Goods = allocation?.Goods.ToGoodsProfile(allocation.BatchNumber, allocation.ExpiredDate.Date),
                                    Qty = allocation?.QtyActual,
                                    Time = allocation?.ReceivedTime,
                                    ApplyId = allocation?.ApplyId,
                                    Person = allocation?.ReceiverName,
                                    Deliverer = allocation?.DelivererName,
                                }
                            };
                        }).ToList();
                }
                var inibatchExpired = new List<DailyConsumptionDetails>();
                if (currIndex == 0 || endDateTime.ToString("HHmmss").Equals("235959"))
                {
                    inibatchExpired = inventories.Where(x => x.StatsTime == endDateTime.Date).Select(x => new DailyConsumptionDetails
                    {
                        SeachTime = endDateTime,
                        BatchNumber = x.BatchNumber,
                        ExpiredDate = new DateTime(x.ExpiredDate.Year, x.ExpiredDate.Month, 1),
                        CheckOutQty = 0,
                        CheckInQty = 0
                    }).ToList();
                }

                return batchExpiredCheckIn.Union(inibatchExpired).Where(x => !string.IsNullOrEmpty(x.BatchNumber)).GroupBy(x => new { x.BatchNumber, x.ExpiredDate }).Select(x =>
                {
                    double initialQty = 0;
                    if (currIndex == 0)
                    {
                        var firstInventory = initialInventory.FirstOrDefault(y => y.BatchNumber == x.Key.BatchNumber && y.ExpiredDate.ToString("yyyy-MM") == x.Key.ExpiredDate.Value.ToString("yyyy-MM") && y.StatsTime == endDateTime.Date);
                        double firstRangeInventory = 0;
                        if (firstInventory != null)
                        {
                            firstRangeInventory = pres.Where(y => y.CreatedTime >= firstInventory.StatsTime && y.CreatedTime < endDateTime).Sum(y => y.Mode == ExchangeMode.CheckIn ? -y.Qty : y.Qty);
                        }
                        initialQty = firstInventory != null ? firstInventory.QtyInitial : 0 - firstRangeInventory;
                    }
                    return new DailyConsumptionDetails
                    {
                        SeachTime = endDateTime,
                        BatchNumber = x.Key.BatchNumber,
                        ExpiredDate = x.Key.ExpiredDate,
                        InitialQty = initialQty,
                        CheckOutQty = x.Sum(y => y.CheckOutQty),
                        CheckInQty = x.Sum(y => y.CheckInQty),
                        Allocation = x.FirstOrDefault()?.Allocation
                    };
                });
            }).ToArray();

            var dataRangeConsumptionGroupByTime = dataRangeConsumption.Where(x => x.SeachTime.ToString("HHmmss").Equals("235959")).GroupBy(x => x.SeachTime).ToList();
            for (int i = 0; i < dataRangeConsumptionGroupByTime.Count(); i++)
            {
                var dailyConsumptionDetails = dataRangeConsumptionGroupByTime[i];
                dailyConsumptionDetails.ToList().ForEach(x =>
                {
                    var startDateTime = new DateTime(dailyConsumptionDetails.Key.Year, dailyConsumptionDetails.Key.Month, 1);
                    var allsQty = dataRangeConsumption.Where(y => y.SeachTime >= startDateTime && y.SeachTime < dailyConsumptionDetails.Key && y.BatchNumber == x.BatchNumber && y.ExpiredDate == x.ExpiredDate).Sum(y => y.CheckInQty);
                    var presQty = pres.Where(y => y.CreatedTime >= startDateTime && y.CreatedTime < dailyConsumptionDetails.Key && y.BatchNumber == x.BatchNumber && y.ExpiredDate == x.ExpiredDate).Sum(y => y.Qty);
                    x.CheckInQty = allsQty;
                    x.CheckOutQty = presQty;
                    if (i == 0)
                    {
                        var firstDateTime = dataRangeConsumption.FirstOrDefault().SeachTime;
                        var lant = dataRangeConsumption.FirstOrDefault(y => x.SeachTime == firstDateTime && y.BatchNumber == x.BatchNumber && y.ExpiredDate == x.ExpiredDate);
                        x.PresOutQty = lant != null ? lant.InitialQty : 0 + x.CheckInQty - x.CheckOutQty;
                    }
                    else
                    {
                        var lant = dataRangeConsumptionGroupByTime[i - 1].FirstOrDefault(y => y.BatchNumber == x.BatchNumber && y.ExpiredDate == x.ExpiredDate);
                        x.PresOutQty = lant != null ? lant.PresOutQty : 0 + x.CheckInQty - x.CheckOutQty;
                    }

                });
            }
            return dataRangeConsumption.Select(x =>
                new DailyConsumptionDetails
                {
                    SeachTime = x.SeachTime,
                    BatchNumber = x.BatchNumber,
                    ExpiredDate = x.ExpiredDate,
                    InitialQty = x.InitialQty,
                    CheckOutQty = x.CheckOutQty,
                    CheckInQty = x.CheckInQty,
                    PresOutQty = x.InitialQty + x.CheckInQty - x.CheckOutQty,
                    Allocation = x.Allocation
                }).ToArray();
        }
    }
}

// destory-details
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class DestoryDetail
        {
            public string ExecutorName { get; set; }
            public GoodsProfile Goods { get; set; }
            public double Qty { get; set; }
        }

        [HttpGet]
        [ActionName("search-destory-details")]
        public DestoryDetail[] SearchDestoryDetails(string hospital, DateTime start, DateTime end, string department)
        {
            var computers = Computers(department);
            var records = mongo.DestoryCollection.AsQueryable().Where(d => computers.Contains(d.Computer) && d.CreatedTime >= start && d.CreatedTime < end).ToList();
            var goodsId = records.Select(r => r.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(g => goodsId.Contains(g.UniqueId)).ToList().Select(g => g.ToGoodsProfile(string.Empty, DateTime.MaxValue.Date)).ToList();

            return records.GroupBy(r => new { r.GoodsId, BatchNumber = r.BatchNumber ?? string.Empty, ExpiredDate = r.ExpiredDate.Date, r.ExecutorName })
                .Select(g =>
                {
                    var find = goods.FirstOrDefault(f => f.UniqueId == g.Key.GoodsId) ?? new GoodsProfile { UniqueId = g.Key.GoodsId, };
                    find.BatchNumber = g.Key.BatchNumber;
                    find.ExpiredDate = g.Key.ExpiredDate;
                    return new DestoryDetail
                    {
                        Goods = find,
                        Qty = g.Sum(o => o.DestoryQty),
                        ExecutorName = g.Key.ExecutorName
                    };
                }).ToArray();
        }
    }
}

// recycle-ampoules
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class PatientRecycleAmpoules
        {
            public Patient Patient { get; set; }
            public Department Department { get; set; }
            public Goods Goods { get; set; }
            public double Qty { get; set; }
            public string BatchNumber { get; set; }
            public string RepaidPerson { get; set; }
            public string ReceivePerson { get; set; }
            public string DispensingName { get; set; }
            public string PrimaryUserName { get; set; }
            public string SecondaryUserName { get; set; }
            public DateTime CreatedTime { get; set; }
            public DateTime? IssuedTime { get; set; }
        }

        /// <summary>
        ///     空安瓿/药品回收登记
        /// </summary>
        /// <param name="hospital"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="department"></param>
        /// <param name="recycleType">回收类型(空安瓿回收，药品回收)</param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("search-recycle-ampoules-by-date-range")]
        public PatientRecycleAmpoules[] SearchRecycleAmpoulesByDateRange(string hospital, DateTime start, DateTime end, string department, string recycleType)
        {
            var departments = mongo.DepartmentCollection.AsQueryable().ToList();
            var ampoules = mongo.AmpouleCollection.AsQueryable().Where(x => x.CreatedTime >= start && x.CreatedTime < end && x.DepartmentId == department).ToList();
            var goodsIds = ampoules.Select(x => x.GoodsId).Distinct().ToList();
            var ampouleIds = ampoules.Select(x => x.UniqueId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(x => goodsIds.Contains(x.UniqueId)).ToList();

            var records = mongo.PrescriptionCollection.AsQueryable().Where(x => x.DepartmentDestinationId == department).SelectMany(x => x.AssignAmpouleRecords).Where(y => ampouleIds.Contains(y.AmpouleId) && y.RecycleType == recycleType).ToList();
            var prescriptionsId = records.Select(x => x.OwnerCode).ToList();
            var prescriptions = mongo.PrescriptionCollection.AsQueryable().Where(x => prescriptionsId.Contains(x.UniqueId)).ToList();
            var patientIds = prescriptions.Select(x => x.PatientId).Distinct().ToList();
            var patients = mongo.PatientCollection.AsQueryable().Where(x => patientIds.Contains(x.UniqueId)).ToList();

            var personIds = ampoules.Select(x => x.ReceivePerson).Distinct().ToList();
            var persons = mongo.UserCollection.AsQueryable().Where(x => personIds.Contains(x.UniqueId)).ToList().Concat(new[] { new User { UniqueId = SfraObject.EmptyId(), LoginId = ServiceStartup.Kernel, DisplayName = "Kernel User", } });

            return ampoules.Where(a => records.Select(r => r.AmpouleId).Contains(a.UniqueId)).Select(x =>
           {
               var prescription = prescriptions.FirstOrDefault(p => p.UniqueId == records.FirstOrDefault(r => r.AmpouleId == x.UniqueId)?.OwnerCode);
               var id = prescription?.UniqueId;
               var action = mongo.ActionJournalCollection.AsQueryable().Where(d => d.TargetType == nameof(Prescription) && d.TargetId == id).OrderByDescending(d => d.CreatedTime).FirstOrDefault();
               return new { CreatedTime = x.CreatedTime.Date, x.ActualQty, x.BatchNumber, x.GoodsId, x.ReceivePerson, x.RepaidPerson, prescription?.DepartmentSourceId, prescription?.PatientId, prescription?.Patient, prescription?.TimeFilter, prescription?.DispensingId, action?.PrimaryUserName, action?.SecondaryUserName };
           })
            .GroupBy(x => new { x.CreatedTime, x.PatientId, x.DepartmentSourceId, x.GoodsId, x.BatchNumber, x.ReceivePerson, x.RepaidPerson, x.TimeFilter, x.DispensingId, x.PrimaryUserName, x.SecondaryUserName })
            .Select(x =>
            {
                var person = persons.FirstOrDefault(y => y.LoginId == x.Key.ReceivePerson);
                return new PatientRecycleAmpoules
                {
                    CreatedTime = x.Key.CreatedTime,
                    Patient = x.FirstOrDefault().Patient ?? patients.FirstOrDefault(y => y.UniqueId == x.Key.PatientId),
                    Department = departments.FirstOrDefault(d => d.UniqueId == x.Key.DepartmentSourceId),
                    Goods = goods.FirstOrDefault(y => y.UniqueId == x.Key.GoodsId),
                    BatchNumber = x.Key.BatchNumber,
                    ReceivePerson = person.DisplayName ?? person.Employee?.DisplayName,
                    RepaidPerson = x.Key.RepaidPerson,
                    PrimaryUserName = x.Key.PrimaryUserName,
                    SecondaryUserName = x.Key.SecondaryUserName,
                    IssuedTime = x.Key.TimeFilter,
                    DispensingName = x.Key.DispensingId != null ? persons.Where(u => u.LoginId == x.Key.DispensingId).FirstOrDefault()?.Employee.DisplayName : "",
                    Qty = x.Sum(y => y.ActualQty)
                };
            }).ToArray();
        }
    }
}
namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        public class GoodsTransfer
        {
            public string GoodsId { get; set; }
            public double CheckedOutQty { get; set; }
            public double Balance { get; set; }
        }
        public class DailyTransferRecords
        {
            public DateTime SearchDateTime { get; set; }
            public GoodsTransfer[] Details { get; set; }
        }

        [HttpGet]
        [ActionName("search-daily-transfer-records-by-date-range")]
        public DailyTransferRecords[] SearchDailyTransferRecordsByDateRange(string hospital, DateTime start, DateTime end, string department)
        {
            var prescriptions = mongo.PrescriptionCollection.AsQueryable().Where(p => !p.IsDisabled && (department == null || p.DepartmentDestinationId == department))
         .Where(x => x.FinishTime >= start && x.FinishTime < end).OrderBy(p => p.TimeFilter).ToList().GroupBy(x => new { x.FinishTime.Value.Date, x.GoodsId })
         .Select(x => { return new { SearchTime = x.Key.Date, x.Key.GoodsId, CheckedOut = x.Sum(y => y.Mode == ExchangeMode.CheckIn ? -y.Qty : y.Qty) }; });
            var computers = Computers(department);
            var categories = mongo.GoodsCategoryCollection.AsQueryable().Where(x => !x.IsDisabled && (x.DisplayName.Equals("麻醉药品") || x.DisplayName.Equals("精神一类"))).SelectMany(x => x.GoodsKeys).ToList();
            var goodsIds = mongo.TerminalGoodsCollection.AsQueryable().Where(t => computers.Contains(t.Computer) && categories.Contains(t.GoodsId)).Select(t => t.GoodsId).OrderBy(x => x).ToList();
            var actionJournal = mongo.ActionJournalCollection.AsQueryable().Where(x => computers.Contains(x.Computer) && x.CreatedTime >= start && x.CreatedTime < end).ToList();
            var inventories = mongo.InventoryCollection.AsQueryable().Where(x => computers.Contains(x.Computer) && x.StatsTime >= start && x.StatsTime < end).ToList().GroupBy(x => new { x.StatsTime.Date, x.GoodsId })
                 .Select(x => new { SearchTime = x.Key.Date, x.Key.GoodsId, QtyInitial = x.Sum(y => y.QtyInitial) });

            return Enumerable.Range(0, Math.Max(0, (end - start).Days)).Select(d => start.AddDays(d)).Select(date =>
            {
                return new DailyTransferRecords
                {
                    SearchDateTime = date,
                    Details = goodsIds.Select(goods =>
                     {
                         var currentInventories = inventories.FirstOrDefault(x => x.SearchTime.Day == date.Day && x.GoodsId == goods);
                         var currentActionJournal = actionJournal.Where(x => x.CreatedTime.Day == date.Day && x.GoodsId == goods).Sum(x => x.Mode == ExchangeMode.CheckIn ? x.Qty : -x.Qty);
                         var currentPrescription = prescriptions.FirstOrDefault(x => x.GoodsId == goods && x.SearchTime.Day == date.Day);
                         return new GoodsTransfer
                         {
                             GoodsId = goods,
                             CheckedOutQty = currentPrescription != null ? currentPrescription.CheckedOut : 0,
                             Balance = currentInventories != null ? currentInventories.QtyInitial : 0 + currentActionJournal
                         };
                     }).ToArray()
                };
            }).ToArray();
        }
    }
}

