//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.CenterService.Algorithm;
using HealthCare.Data;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

#pragma warning disable CS1591

namespace HealthCare.CenterService.Controllers
{
    [UserAuthorize]
    public partial class ngController : BaseController
    {
        public class PositionArg
        {
            public List<string> Exchanges { get; set; } = new List<string>();
            public List<string> Boxes { get; set; } = new List<string>();
            /// <summary>
            ///     使用回收箱
            /// </summary>
            public bool IsRecycle { get; set; }
            /// <summary>
            ///     卸载物品
            /// </summary>
            public bool IsUnload { get; set; }
            /// <summary>
            /// 扩展未占位药盒
            /// </summary>
            public bool Extension { get; set; }
            /// <summary>
            /// 跨抽屉或跨柜子
            /// </summary>
            public bool Cross { get; set; }

            public LocateBoxAlgo.LocatePriority Priority { get; set; } = LocateBoxAlgo.LocatePriority.CabinetFirst;
        }

        public class PositionResult
        {
            public PostionExchange[] Exchanges { get; set; }
            /// <summary>
            ///     取药时为分配的位置的条码，预支退回时为预支时使用的条码，入库时为柜内已有的条码
            /// </summary>
            public string[] GoodsBarcodes { get; set; }
        }

        public class PostionExchange
        {
            public string ExchangeId { get; set; }
            public double QtyActual { get; set; }
            public IdName Doctor { get; set; }
            public IdName Patient { get; set; }
            public PostionPlan[] Plans { get; set; }
        }

        public class PostionPlan
        {
            public string PlanId { get; set; }
            public bool IsExecuted { get; set; }
            public string Box { get; set; }
            public BoxMode BoxMode { get; set; }
            public string DisplayText { get; set; }
            public GoodsProfile Goods { get; set; }
            public ExchangeMode Mode { get; set; }
            public double Qty { get; set; }
            /// <summary>
            ///     该位置所存储的条码
            /// </summary>
            public string[] GoodsBarcodes { get; set; }
        }

        public class GoodsProfile : IdName
        {
            public string Specification { get; set; }
            public string Manufacturer { get; set; }
            public string Trader { get; set; }
            public string GoodsType { get; set; }
            public string UsedUnit { get; set; }
            public string SmallPackageUnit { get; set; }
            public string BatchNumber { get; set; }
            public DateTime ExpiredDate { get; set; }
            public double Conversion { get; set; } = 1.0;
            public string Filter { get; set; }
            public double Price { get; set; }
            public string DosageForm { get; set; }
        }

        /// <summary>
        ///     算法药品定位
        /// </summary>
        [HttpPost]
        [ActionName("search-position")]
        public async Task<PositionResult> SearchPositionAsync(string collection, [FromBody] PositionArg arg, string terminal = null, string department = null)
        {
            Terminal = terminal ?? Terminal;
            department = department ?? DepartmentId;

            var pmtBoxes = SearchPermitBoxes(Terminal);
            var boxNos = arg.Boxes.Any() ? pmtBoxes.Join(arg.Boxes, pb => pb, b => b, (pb, b) => pb).ToList() : pmtBoxes;
            var data = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).Select(cs => new
            {
                Cabinets = cs.Cabinets.Where(c => c.DepartmentId == department),
                OutOfCabinets = cs.OutOfCabinets.Where(v => v.DepartmentId == department),
            }).ToList();

            // 获取流转数据
            Exchange[] dataSource;
            switch (collection)
            {
                case nameof(Exchange): dataSource = mongo.ExchangeCollection.AsQueryable().Where(e => arg.Exchanges.Contains(e.UniqueId)).ToArray(); break;
                case nameof(Prescription): dataSource = mongo.PrescriptionCollection.AsQueryable().Where(p => arg.Exchanges.Contains(p.UniqueId)).ToArray(); break;
                case nameof(Allocation): dataSource = mongo.AllocationCollection.AsQueryable().Where(a => arg.Exchanges.Contains(a.UniqueId)).ToArray(); break;
                case nameof(Medication): dataSource = mongo.MedicationCollection.AsQueryable().Where(m => arg.Exchanges.Contains(m.UniqueId)).ToArray(); break;
                case nameof(InternalAllocation): dataSource = mongo.InternalAllocationCollection.AsQueryable().Where(t => arg.Exchanges.Contains(t.UniqueId)).ToArray(); break;
                default: throw new NotImplementedException(collection);
            }

            // 计算可用的硬件设备
            List<CabinetDevice> cabinets, virtuals;
            switch (collection)
            {
                case nameof(InternalAllocation):
                    var nos = ((InternalAllocation[])dataSource).Select(a => a.TurnOutDevice).ToArray();
                    var excluded = data.SelectMany(c =>
                    {
                        var cabs = c.Cabinets.Concat(c.OutOfCabinets).Where(o => nos.All(n => n != o.No));
                        foreach (var cab in cabs)
                        {
                            cab.Drawers = cab.Drawers.Where(d => d.IsRecycleBin == arg.IsRecycle && nos.All(n => n != d.No)).ToList();
                            foreach (var dra in cab.Drawers)
                            {
                                dra.Boxes = dra.Boxes.Where(b => !b.IsBreakdown && nos.All(n => n != b.No)).ToList();
                                // 最小硬件单位为药盒，不过滤针剂
                            }
                        }
                        return cabs;
                    }).ToList();
                    cabinets = excluded.Where(e => e.IsControlled).ToList();
                    virtuals = excluded.Where(e => !e.IsControlled).ToList();
                    break;
                case nameof(Exchange):
                case nameof(Prescription):
                case nameof(Allocation):
                case nameof(Medication):
                default:
                    cabinets = data.SelectMany(x => x.Cabinets.Select(c =>
                    {
                        c.Drawers = c.Drawers.Where(d => d.IsRecycleBin == arg.IsRecycle).ToList();
                        foreach (var dra in c.Drawers)
                        {
                            dra.Boxes = dra.Boxes.Where(b => !b.IsBreakdown).ToList();
                        }
                        return c;
                    }).Where(c => c.Drawers.Any())).OrderBy(c => c.DisplayOrder).ToList();
                    virtuals = data.SelectMany(x => x.OutOfCabinets).OrderBy(v => v.DisplayOrder).ToList();
                    break;
            }

            // 分配计划
            var algo = new LocateBoxAlgo();
            var boxes = algo.AlternativeBoxes(boxNos, cabinets, virtuals);
            var mode = dataSource[0].Mode;
            switch (mode)
            {
                case ExchangeMode.CheckIn: algo.LocateCheckIn(ref dataSource, boxes, arg.Extension, arg.Cross, arg.Priority); break;
                case ExchangeMode.CheckOut: algo.LocateCheckOut(ref dataSource, boxes, arg.Priority, arg.IsUnload); break;
                default: throw new InvalidOperationException(mode.ToString());

            }

            if (collection == nameof(Prescription) && dataSource.Any(d => !d.IsSpecified))
            {
                // 医嘱不可以取一部分
                return new PositionResult { Exchanges = new PostionExchange[0], GoodsBarcodes = new string[0], };
            }

            // 把计划写回数据库
            foreach (var item in dataSource)
            {
                if (mode == ExchangeMode.CheckIn && arg.Extension)
                {
                    // 扩容时动态修改占位
                    var find = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled && c.UniqueId == item.CustomerId).SelectMany(c => c.Cabinets).ToList();
                    foreach (var plan in item.Plans)
                    {
                        var cabIdx = find.FindIndex(c => plan.Box.No.StartsWith(c.No));
                        var draIdx = find[cabIdx].Drawers.FindIndex(d => plan.Box.No.StartsWith(d.No));
                        var boxIdx = find[cabIdx].Drawers[draIdx].Boxes.FindIndex(b => plan.Box.No.StartsWith(b.No));
                        if (find[cabIdx].Drawers[draIdx].Boxes[boxIdx].Fills.All(b => b.QtyExisted == 0) && plan.Box.Fills.Any(p => p.QtyExisted > 0))
                        {
                            var fills = plan.Box.Fills.Select(f => { f.QtyExisted = 0; return f; }).ToList();
                            await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == item.CustomerId, Builders<Customer>.Update.Set(x => x.Cabinets[cabIdx].Drawers[draIdx].Boxes[boxIdx].Fills, fills));
                        }
                    }
                }
                switch (collection)
                {
                    case nameof(Exchange): await mongo.ExchangeCollection.UpdateOneAsync(e => e.UniqueId == item.UniqueId, Builders<Exchange>.Update.Set(x => x.Plans, item.Plans)); break;
                    case nameof(Prescription): await mongo.PrescriptionCollection.UpdateOneAsync(e => e.UniqueId == item.UniqueId, Builders<Prescription>.Update.Set(x => x.Plans, item.Plans)); break;
                    case nameof(Allocation): await mongo.AllocationCollection.UpdateOneAsync(e => e.UniqueId == item.UniqueId, Builders<Allocation>.Update.Set(x => x.Plans, item.Plans)); break;
                    case nameof(Medication): await mongo.MedicationCollection.UpdateOneAsync(e => e.UniqueId == item.UniqueId, Builders<Medication>.Update.Set(x => x.Plans, item.Plans)); break;
                    case nameof(InternalAllocation): await mongo.InternalAllocationCollection.UpdateOneAsync(e => e.UniqueId == item.UniqueId, Builders<InternalAllocation>.Update.Set(x => x.Plans, item.Plans)); break;
                }
            }

            string[] barcodes;
            switch (mode)
            {
                case ExchangeMode.CheckIn:
                    // 入库
                    // 预支退回以预支时使用的条码为准
                    // 调拨入库以调拨记录带有的条码为准
                    // 其余情况不能和柜内条码重复
                    if (collection == nameof(Medication) || collection == nameof(Allocation))
                    {
                        barcodes = dataSource.SelectMany(m => m.GoodsBarcodes).OrderBy(b => b).ToArray();
                    }
                    else
                    {
                        barcodes = cabinets.Concat(virtuals).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).SelectMany(f => f.Barcodes).OrderBy(b => b).ToArray();
                    }
                    break;
                case ExchangeMode.CheckOut:
                    // 出库
                    // 只能拿取推送位置的条码
                    barcodes = dataSource.SelectMany(e => e.Plans).SelectMany(p => p.Box.Fills).SelectMany(f => f.Barcodes).OrderBy(b => b).ToArray();
                    break;
                default: barcodes = new string[0]; break;
            }

            return new PositionResult
            {
                Exchanges = dataSource.Select(item => new PostionExchange
                {
                    ExchangeId = item.UniqueId,
                    QtyActual = item.QtyActual,
                    Doctor = item is Prescription pd ? new IdName { UniqueId = pd.DoctorId, DisplayName = pd.Doctor?.DisplayName }
                            : item is Medication md ? new IdName { UniqueId = md.DoctorId, DisplayName = md.Doctor?.DisplayName }
                            : new IdName(),
                    Patient = item is Prescription pp ? new IdName { UniqueId = pp.PatientId, DisplayName = pp.Patient?.DisplayName }
                            : item is Medication mp ? new IdName { UniqueId = mp.PatientId, DisplayName = mp.Patient?.DisplayName }
                            : new IdName(),
                    Plans = item.Plans.Select(p =>
                    {
                        var fill = p.Box.Fills.First(f => f.GoodsId == item.GoodsId);
                        // 取药以位置为准，退药以给定为准
                        var batch = item.Mode == ExchangeMode.CheckOut ? fill.BatchNumber : item.BatchNumber;
                        var expired = item.Mode == ExchangeMode.CheckOut ? fill.ExpiredDate : item.ExpiredDate;
                        item.Goods = item.Goods ?? mongo.GoodsCollection.AsQueryable().FirstOrDefault(o => o.UniqueId == item.GoodsId) ?? new Goods { UniqueId = item.GoodsId, };
                        return new PostionPlan
                        {
                            PlanId = p.UniqueId,
                            IsExecuted = p.IsExecuted,
                            Box = p.Box.No,
                            DisplayText = p.Box.DisplayText,
                            BoxMode = p.Box.BoxMode,
                            Mode = item.Mode,
                            Qty = p.Qty,
                            Goods = item.Goods.ToGoodsProfile(batch, expired.Date),
                            GoodsBarcodes = p.Box.Fills.SelectMany(f => f.Barcodes).OrderBy(b => b).ToArray(),
                        };
                    }).ToArray(),
                }).ToArray(),
                GoodsBarcodes = barcodes,
            };
        }

        /// <summary>
        ///     修改给定药盒的药品的现存量
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="exchange"></param>
        /// <param name="plan"></param>
        /// <param name="qty">取、退数量</param>
        /// <param name="batch"></param>
        /// <param name="expired"></param>
        /// <param name="barcodes">该位置执行完取、退操作后该位置剩下的条码</param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionName("modify-qty-for-cabinet")]
        public async Task<bool> ModifyQtyForCabinetAsync(string collection, string exchange, string plan, double qty, string batch, DateTime expired, [FromBody] string[] barcodes, string terminal = null)
        {
            Terminal = terminal ?? Terminal;

            Exchange rowItem;
            ActionPlan exePlan;
            switch (collection)
            {
                case nameof(Exchange):
                    {
                        var target = mongo.ExchangeCollection.AsQueryable().First(f => f.UniqueId == exchange);
                        var targetPlan = target.Plans.First(f => f.UniqueId == plan);
                        rowItem = target;
                        exePlan = targetPlan;
                        // 流转可以中途停止，最后保证期望值和实际值相等即可
                        target.QtyActual += qty;
                        target.Qty = target.QtyActual;
                        targetPlan.IsExecuted = true;
                        targetPlan.Qty = qty; // 该计划的实际动作量
                        target.FinishTime = DateTime.Now;

                        var builders = Builders<Exchange>.Update.Set(o => o.QtyActual, target.QtyActual).Set(o => o.Qty, target.Qty).Set(o => o.Plans, target.Plans).Set(o => o.FinishTime, target.FinishTime);
                        await mongo.ExchangeCollection.UpdateOneAsync(g => g.UniqueId == target.UniqueId, builders);
                    }
                    break;
                case nameof(Prescription):
                    {
                        var target = mongo.PrescriptionCollection.AsQueryable().First(f => f.UniqueId == exchange);
                        var targetPlan = target.Plans.First(f => f.UniqueId == plan);
                        rowItem = target;
                        exePlan = targetPlan;
                        // 医嘱不可以中途停止，必须每一个计划都执行完毕
                        target.QtyActual += qty;
                        targetPlan.IsExecuted = targetPlan.Qty == qty;

                        var builders = Builders<Prescription>.Update.Set(o => o.QtyActual, target.QtyActual).Set(o => o.Plans, target.Plans);
                        if (target.Plans.All(p => p.IsExecuted))
                        {
                            target.FlowState = "SFRA 已执行";
                            target.FlowRemark = null;
                            target.FinishTime = DateTime.Now;
                            builders = builders.Set(o => o.FlowState, target.FlowState).Set(o => o.FlowRemark, target.FlowRemark).Set(o => o.FinishTime, target.FinishTime);
                        }
                        await mongo.PrescriptionCollection.UpdateOneAsync(g => g.UniqueId == target.UniqueId, builders);
                    }
                    break;
                case nameof(Allocation):
                    {
                        var target = mongo.AllocationCollection.AsQueryable().First(f => f.UniqueId == exchange);
                        var targetPlan = target.Plans.First(f => f.UniqueId == plan);
                        rowItem = target;
                        exePlan = targetPlan;
                        // 调拨不可以中途停止，必须每一个计划都执行完毕
                        target.QtyActual += qty;
                        targetPlan.IsExecuted = targetPlan.Qty == qty;

                        var builders = Builders<Allocation>.Update.Set(o => o.QtyActual, target.QtyActual).Set(o => o.Plans, target.Plans);
                        if (target.Plans.All(p => p.IsExecuted))
                        {
                            var p = ServiceStartup.GetPrimaryAuthorized(Terminal);
                            target.Storager = p.LoginId;
                            target.StoragerName = p.DisplayName;
                            target.FinishTime = DateTime.Now;
                            builders = builders.Set(o => o.Storager, target.Storager).Set(o => o.StoragerName, target.StoragerName).Set(o => o.FinishTime, target.FinishTime);
                        }
                        await mongo.AllocationCollection.UpdateOneAsync(g => g.UniqueId == target.UniqueId, builders);
                    }
                    break;
                case nameof(Medication):
                    {
                        var target = mongo.MedicationCollection.AsQueryable().First(f => f.UniqueId == exchange);
                        var targetPlan = target.Plans.First(f => f.UniqueId == plan);
                        rowItem = target;
                        exePlan = targetPlan;
                        target.QtyActual += qty;
                        // 预支不可以中途停止，必须每一个计划都执行完毕
                        targetPlan.IsExecuted = targetPlan.Qty == qty;

                        var builders = Builders<Medication>.Update.Set(o => o.QtyActual, target.QtyActual).Set(o => o.Plans, target.Plans);
                        if (target.Plans.All(p => p.IsExecuted))
                        {
                            var p = ServiceStartup.GetPrimaryAuthorized(Terminal);
                            target.OperatorId = p.LoginId;
                            target.OperatorName = p.DisplayName;
                            target.FinishTime = DateTime.Now;
                            builders = builders.Set(o => o.OperatorId, target.OperatorId).Set(o => o.OperatorName, target.OperatorName).Set(o => o.FinishTime, target.FinishTime);
                        }
                        await mongo.MedicationCollection.UpdateOneAsync(g => g.UniqueId == target.UniqueId, builders);
                    }
                    break;
                case nameof(InternalAllocation):
                    {
                        var target = mongo.InternalAllocationCollection.AsQueryable().First(f => f.UniqueId == exchange);
                        var targetPlan = target.Plans.First(f => f.UniqueId == plan);
                        rowItem = target;
                        exePlan = targetPlan;
                        target.QtyActual += qty;
                        // 内部调整不可以中途停止，必须每一个计划都执行完毕
                        targetPlan.IsExecuted = targetPlan.Qty == qty;

                        var builders = Builders<InternalAllocation>.Update.Set(o => o.QtyActual, target.QtyActual).Set(o => o.Plans, target.Plans);
                        if (target.Plans.All(p => p.IsExecuted))
                        {
                            target.FinishTime = DateTime.Now;
                            builders = builders.Set(o => o.FinishTime, target.FinishTime);
                        }
                        await mongo.InternalAllocationCollection.UpdateOneAsync(g => g.UniqueId == target.UniqueId, builders);
                    }
                    break;
                // other exchange
                default:
                    throw new NotImplementedException(collection);
            }

            if (qty != 0.0)
            {
                //  更新 NodeGoodsInfo
                var customers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).ToList();
                foreach (var customer in customers)
                {
                    var cabinets = customer.Cabinets.Concat(customer.OutOfCabinets).ToList();
                    var (cabIdx, draIdx, boxIdx, filIdx) = FindIndex(exePlan.Box.No, rowItem.GoodsId, cabinets);
                    if (cabIdx < 0)
                    {
                        continue;
                    }

                    var fills = cabinets[cabIdx].Drawers[draIdx].Boxes[boxIdx].Fills;
                    var fill = fills[filIdx];
                    var oldQty = fill.QtyExisted;
                    ModifyFill(rowItem.Mode, fill);

                    var index = cabIdx - (cabIdx < customer.Cabinets.Count ? 0 : customer.Cabinets.Count);
                    if (cabIdx < customer.Cabinets.Count)
                    {
                        await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == customer.UniqueId, Builders<Customer>.Update
                            .Set(x => x.Cabinets[index].Drawers[draIdx].Boxes[boxIdx].Fills[filIdx].Barcodes, fill.Barcodes)
                            .Inc(x => x.Cabinets[index].Drawers[draIdx].Boxes[boxIdx].Fills[filIdx].QtyExisted, fill.QtyExisted - oldQty)
                            .Set(x => x.Cabinets[index].Drawers[draIdx].Boxes[boxIdx].Fills[filIdx].StorageTime, fill.StorageTime)
                            .Set(x => x.Cabinets[index].Drawers[draIdx].Boxes[boxIdx].Fills[filIdx].BatchNumber, fill.BatchNumber)
                            .Set(x => x.Cabinets[index].Drawers[draIdx].Boxes[boxIdx].Fills[filIdx].ExpiredDate, fill.ExpiredDate));
                    }
                    else
                    {
                        await mongo.CustomerCollection.UpdateOneAsync(x => x.UniqueId == customer.UniqueId, Builders<Customer>.Update
                            .Set(x => x.OutOfCabinets[index].Drawers[draIdx].Boxes[boxIdx].Fills[filIdx].Barcodes, fill.Barcodes)
                            .Inc(x => x.OutOfCabinets[index].Drawers[draIdx].Boxes[boxIdx].Fills[filIdx].QtyExisted, fill.QtyExisted - oldQty)
                            .Set(x => x.OutOfCabinets[index].Drawers[draIdx].Boxes[boxIdx].Fills[filIdx].StorageTime, fill.StorageTime)
                            .Set(x => x.OutOfCabinets[index].Drawers[draIdx].Boxes[boxIdx].Fills[filIdx].BatchNumber, fill.BatchNumber)
                            .Set(x => x.OutOfCabinets[index].Drawers[draIdx].Boxes[boxIdx].Fills[filIdx].ExpiredDate, fill.ExpiredDate));
                    }
                    await mongo.StorageJournalCollection.InsertOneAsync(new StorageJournal { Computer = Terminal, No = exePlan.Box.No, Fills = fills, });
                    break;
                }
            }

            switch (collection)
            {
                case nameof(Exchange): await mongo.ExchangeCollection.UpdateOneAsync(e => e.UniqueId == exchange, Builders<Exchange>.Update.Set(e => e.GoodsBarcodes, rowItem.GoodsBarcodes)); break;
                case nameof(Prescription): await mongo.PrescriptionCollection.UpdateOneAsync(p => p.UniqueId == exchange, Builders<Prescription>.Update.Set(p => p.GoodsBarcodes, rowItem.GoodsBarcodes)); break;
                case nameof(Allocation): await mongo.AllocationCollection.UpdateOneAsync(a => a.UniqueId == exchange, Builders<Allocation>.Update.Set(a => a.GoodsBarcodes, rowItem.GoodsBarcodes)); break;
                case nameof(Medication): await mongo.MedicationCollection.UpdateOneAsync(m => m.UniqueId == exchange, Builders<Medication>.Update.Set(m => m.GoodsBarcodes, rowItem.GoodsBarcodes)); break;
                case nameof(InternalAllocation): await mongo.InternalAllocationCollection.UpdateOneAsync(s => s.UniqueId == exchange, Builders<InternalAllocation>.Update.Set(t => t.GoodsBarcodes, rowItem.GoodsBarcodes)); break;
            }

            var needCertify = mongo.GoodsCategoryCollection.AsQueryable().Where(g => !g.IsDisabled && g.IsDoubleCertify && g.GoodsKeys.Contains(rowItem.GoodsId)).Any();
            // 记录日志
            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal);
            var secondary = ServiceStartup.GetSecondaryAuthorized(Terminal);
            var certify = ServiceStartup.GetCertifyAuthorized(Terminal);
            await mongo.ActionJournalCollection.InsertOneAsync(new ActionJournal
            {
                Computer = Terminal,
                PrimaryUserId = primary.LoginId,
                PrimaryUserName = primary.DisplayName,
                SecondaryUserId = secondary?.LoginId,
                SecondaryUserName = secondary?.DisplayName,
                OperatorUserId = needCertify ? (certify ?? primary).LoginId : primary.LoginId,
                OperatorUserName = needCertify ? (certify ?? primary).DisplayName : primary.DisplayName,   // kernel 用户没有监督人
                TargetType = collection,
                TargetId = exchange,
                RecordType = rowItem.RecordType,
                No = exePlan.Box.No,
                GoodsId = rowItem.GoodsId,
                Mode = rowItem.Mode,
                Qty = qty,
                BatchNumber = batch,
                ExpiredDate = expired,
            });

            return true;

            void ModifyFill(ExchangeMode mode, NodeGoodsInfo fill)
            {
                switch (mode)
                {
                    case ExchangeMode.CheckIn:
                        switch (collection)
                        {
                            case nameof(Prescription):
                            case nameof(Allocation):
                                // 医嘱退药  ** 现在没有需求, 以后再说 **
                                // 调拨入库的条码, 以调拨记录为准
                                break;
                            case nameof(Exchange):
                            case nameof(Medication):
                            case nameof(InternalAllocation):
                                // 位置, 物品入库, 以新增为准
                                // 预支退药的条码, 以新增为准
                                rowItem.GoodsBarcodes = barcodes.Except(fill.Barcodes).ToList();
                                break;
                        }
                        fill.QtyExisted += qty; fill.StorageTime = DateTime.Now; fill.BatchNumber = batch; fill.ExpiredDate = expired;
                        break;
                    case ExchangeMode.CheckOut:
                        // 出库, 现存条码除去剩余条码
                        rowItem.GoodsBarcodes = fill.Barcodes.Except(barcodes).ToList();
                        fill.QtyExisted -= qty;
                        break;
                }
                fill.Barcodes = barcodes;
            }

            (int cabIdx, int draIdx, int boxIdx, int filIdx) FindIndex(string box, string goods, List<CabinetDevice> cabinets)
            {
                var cabIdx = cabinets.FindIndex(c => box.StartsWith(c.No));
                if (cabIdx >= 0)
                {
                    var draIdx = cabinets[cabIdx].Drawers.FindIndex(d => box.StartsWith(d.No));
                    if (draIdx >= 0)
                    {
                        var boxIdx = cabinets[cabIdx].Drawers[draIdx].Boxes.FindIndex(b => box == b.No);
                        if (boxIdx >= 0)
                        {
                            var fills = cabinets[cabIdx].Drawers[draIdx].Boxes[boxIdx].Fills;
                            var filIdx = fills.FindIndex(f => f.GoodsId == goods);
                            if (filIdx >= 0)
                            {
                                return (cabIdx, draIdx, boxIdx, filIdx);
                            }
                        }
                    }
                }
                return (-1, -1, -1, -1);
            }
        }

        /// <summary>
        ///     删除指定的流转。 无实际动作量（<see cref="Exchange.QtyActual"/> 小于等于 0 ）可以删除
        /// </summary>
        [HttpDelete]
        [ActionName("remove-exchange")]
        public async Task<bool> RemoveExchangeAsync(string collection, string exchange, bool force = false)
        {
            var deleted = false;
            switch (collection)
            {
                case nameof(Exchange):
                    if (mongo.ExchangeCollection.AsQueryable().Any(x => x.UniqueId == exchange && x.QtyActual <= 0))
                    {
                        await mongo.ExchangeCollection.DeleteOneAsync(x => x.UniqueId == exchange);
                        deleted = true;
                    }

                    if (force)
                    {
                        await mongo.ExchangeCollection.UpdateOneAsync(x => x.UniqueId == exchange, Builders<Exchange>.Update.Set(x => x.IsDisabled, true));
                    }
                    break;
                case nameof(Medication):
                    if (mongo.MedicationCollection.AsQueryable().Any(x => x.UniqueId == exchange && x.QtyActual <= 0))
                    {
                        await mongo.MedicationCollection.DeleteOneAsync(x => x.UniqueId == exchange);
                        deleted = true;
                    }

                    if (force)
                    {
                        await mongo.MedicationCollection.UpdateOneAsync(x => x.UniqueId == exchange, Builders<Medication>.Update.Set(x => x.IsDisabled, true));
                    }
                    break;
                case nameof(InternalAllocation):
                    if (mongo.InternalAllocationCollection.AsQueryable().Any(x => x.UniqueId == exchange && x.QtyActual <= 0))
                    {
                        await mongo.InternalAllocationCollection.DeleteOneAsync(x => x.UniqueId == exchange);
                        deleted = true;
                    }

                    if (force)
                    {
                        await mongo.InternalAllocationCollection.UpdateOneAsync(x => x.UniqueId == exchange, Builders<InternalAllocation>.Update.Set(x => x.IsDisabled, true));
                    }
                    break;
            }
            return deleted;
        }

        /// <summary>
        ///     修改药盒的是否损坏的状态
        /// </summary>
        [HttpPut]
        [ActionName("modify-box-break-state")]
        public async Task<bool> ModifyBoxBreakStateAsync(string box, bool broken, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var cs = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).Select(c => c.OwnerCode).FirstOrDefault();
            var customer = mongo.CustomerCollection.AsQueryable().Where(c => c.UniqueId == cs).FirstOrDefault();
            if (customer == null)
            {
                return false;
            }

            for (int i = 0; i < customer.Cabinets.Count; i++)
            {
                var cabinet = customer.Cabinets[i];
                for (int j = 0; j < cabinet.Drawers.Count; j++)
                {
                    var drawer = cabinet.Drawers[j];
                    for (int k = 0; k < drawer.Boxes.Count; k++)
                    {
                        var find = drawer.Boxes[k];
                        if (find.No == box)
                        {
                            find.IsBreakdown = broken;
                            await mongo.CustomerCollection.UpdateOneAsync(c => c.UniqueId == cs, Builders<Customer>.Update.Set(c => c.Cabinets[i].Drawers[j].Boxes[k].IsBreakdown, find.IsBreakdown));
                            goto Modified;
                        }
                    }
                }
            }

        Modified:
            return true;
        }

    }
}

namespace HealthCare.CenterService.Controllers
{
    public partial class ngController
    {
        /// <summary>
        ///     重构日库存（删除旧归档记录）, 从指定的开始日期(包括)计算到当前时间(不包括)
        /// </summary>
        /// <remarks>
        ///     日库存的计算以前一日的结存和当日操作日志为依据，需要计算的药品以 <see cref="TerminalGoods"/> 中设置的为准
        /// </remarks>
        /// <param name="department">部门的唯一标志</param>
        /// <param name="start">开始日期, 如 2018-05-20</param>
        /// <returns></returns>
        [HttpGet]
        [ActionName("rebuild-inventory")]
        public async Task<int> RebuildInventoryAsync(string department, DateTime start)
        {
            var cabinets = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.DepartmentId == department).ToList();
            var find = cabinets.Select(c => new { c.Computer, c.OwnerCode, }).FirstOrDefault();

            var customer = find?.OwnerCode;
            var computer = find?.Computer;
            await mongo.InventoryCollection.DeleteManyAsync(iv => iv.CustomerId == customer && iv.Computer == computer && iv.StatsTime >= start && iv.StatsTime < DateTime.Now.Date);

            var ivs = BuildInventory(customer, computer, start, DateTime.Now.Date);
            if (ivs.Any())
            {
                await mongo.InventoryCollection.InsertManyAsync(ivs);
            }
            return ivs.Count;
        }

        [NonAction]
        public List<Inventory> BuildInventory(string customer, string computer, DateTime start, DateTime end)
        {
            if (start > end)
            {
                return new List<Inventory>();
            }

            var tgs = mongo.TerminalGoodsCollection.AsQueryable().Where(tg => tg.Computer == computer).ToList();
            var ajs = mongo.ActionJournalCollection.AsQueryable().Where(aj => aj.Computer == computer && aj.CreatedTime >= start && aj.CreatedTime < end && aj.Qty > 0 && aj.TargetType != nameof(InternalAllocation)).ToList();
            var ivs = mongo.InventoryCollection.AsQueryable().Where(d => d.Computer == computer && d.StatsTime >= start.AddDays(-1) && d.StatsTime < start).ToList();

            var cache = new[] { (-1, ivs), }.Concat(Enumerable.Range(0, (end - start).Days).Select(x => (x, new List<Inventory>()))).ToArray();
            foreach (var offset in Enumerable.Range(0, (end - start).Days))
            {
                var date = start.AddDays(offset);
                var tAjs = ajs.Where(aj => aj.CreatedTime >= date && aj.CreatedTime < date.AddDays(1)).Select(aj =>
                {
                    aj.BatchNumber = aj.BatchNumber ?? string.Empty;
                    aj.ExpiredDate = aj.ExpiredDate.Date;
                    return aj;
                }).ToList();
                var lst = cache[offset].Item2.Select(c =>
                {
                    c.BatchNumber = c.BatchNumber ?? string.Empty;
                    c.ExpiredDate = c.ExpiredDate.Date;
                    return c;
                }).ToList();
                cache[offset + 1].Item2 = Run(tgs, tAjs, lst, (customer, computer, date));
            }
            return cache.Where(c => c.Item1 >= 0).SelectMany(c => c.Item2).ToList();

            List<Inventory> Run(List<TerminalGoods> tGds, List<ActionJournal> ajrs, List<Inventory> invs, (string customer, string computer, DateTime date) ext)
            {
                return tGds.SelectMany(tg =>
                {
                    if (ajrs.Where(aj => aj.GoodsId == tg.GoodsId).Any())
                    {
                        // 有使用记录
                        // 统计该药品的所有批号、有效期，分别计算结存
                        return ajrs.Where(aj => aj.GoodsId == tg.GoodsId).Select(aj => new { aj.GoodsId, aj.BatchNumber, aj.ExpiredDate })
                            .Concat(invs.Where(iv => iv.GoodsId == tg.GoodsId).Select(iv => new { iv.GoodsId, iv.BatchNumber, iv.ExpiredDate })).Distinct()
                            .Select(o => new Inventory
                            {
                                GoodsId = o.GoodsId,
                                BatchNumber = o.BatchNumber,
                                ExpiredDate = o.ExpiredDate,
                                QtyInitial = invs.Where(lt => lt.GoodsId == tg.GoodsId && lt.BatchNumber == o.BatchNumber && lt.ExpiredDate == o.ExpiredDate).Sum(ls => ls.QtyInitial + ls.QtyCheckIn - ls.QtyCheckOut),
                                QtyCheckIn = ajrs.Where(aj => aj.Mode == ExchangeMode.CheckIn && aj.GoodsId == tg.GoodsId && aj.BatchNumber == o.BatchNumber && aj.ExpiredDate == o.ExpiredDate).Sum(aj => aj.Qty),
                                QtyCheckOut = ajrs.Where(aj => aj.Mode == ExchangeMode.CheckOut && aj.GoodsId == tg.GoodsId && aj.BatchNumber == o.BatchNumber && aj.ExpiredDate == o.ExpiredDate).Sum(aj => aj.Qty),
                                SpaceRemark = null,
                                StorageSpace = null,
                                Computer = ext.computer,
                                CustomerId = ext.customer,
                                StatsTime = ext.date,
                            })
                            .Where(o => !(o.QtyInitial == 0 && o.QtyCheckIn == 0 && o.QtyCheckOut == 0))
                            .ToArray();
                    }

                    // 无使用记录，保留上一次计算时有结存的物品
                    var dIvs = invs.Where(o => o.GoodsId == tg.GoodsId && (o.QtyInitial + o.QtyCheckIn - o.QtyCheckOut) != 0)
                        .GroupBy(o => new { o.GoodsId, o.BatchNumber, o.ExpiredDate })
                        .Select(o => new Inventory
                        {
                            GoodsId = o.Key.GoodsId,
                            BatchNumber = o.Key.BatchNumber,
                            ExpiredDate = o.Key.ExpiredDate,
                            QtyInitial = o.Sum(x => x.QtyInitial + x.QtyCheckIn - x.QtyCheckOut),
                            QtyCheckIn = 0,
                            QtyCheckOut = 0,
                            SpaceRemark = null,
                            StorageSpace = null,
                            Computer = ext.computer,
                            CustomerId = ext.customer,
                            StatsTime = ext.date,
                        }).ToArray();
                    if (dIvs.Any())
                    {
                        return dIvs;
                    }

                    // 如果该药品已经用完，记录一条空记录
                    return new[]
                    {
                        new Inventory
                        {
                            GoodsId = tg.GoodsId,
                            BatchNumber = string.Empty,
                            ExpiredDate = DateTime.MaxValue.Date,
                            QtyInitial = 0,
                            QtyCheckIn = 0,
                            QtyCheckOut = 0,
                            SpaceRemark = null,
                            StorageSpace = null,
                            Computer = ext.computer,
                            CustomerId = ext.customer,
                            StatsTime = ext.date,
                        },
                    };
                }).ToList();
            }
        }
    }
}