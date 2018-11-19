//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable CS1591

namespace HealthCare.CenterService.Algorithm
{
    /// <summary>
    ///     药格定位算法
    /// </summary>
    public class LocateBoxAlgo
    {
        /// <summary>
        ///     定位优先级
        /// </summary>
        public enum LocatePriority
        {
            /// <summary>
            ///     智能柜优先
            /// </summary>
            CabinetFirst = 1,
            /// <summary>
            ///     非智能柜优先
            /// </summary>
            VirtualFirst,
        }

        /// <summary>
        ///     计划操作数量
        /// </summary>
        private void InitSchedule(Exchange[] exchanges)
        {
            foreach (var e in exchanges)
            {
                e.Qty = Math.Abs(e.Qty);
                // 清除未执行的计划
                e.Plans = e.Plans?.Where(p => p.IsExecuted).ToList() ?? new List<ActionPlan>();
            }
        }

        /// <summary>
        ///     从硬件权限中筛选出备选的药盒
        /// </summary>
        internal List<BoxDevice> AlternativeBoxes(IEnumerable<string> permitNos, IEnumerable<CabinetDevice> cabinets, IEnumerable<CabinetDevice> virtuals)
        {
            return cabinets.Concat(virtuals).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).Join(permitNos, a => a.No, b => b, (a, b) => a).ToList();
        }

        /// <summary>
        ///     同类药盒
        /// </summary>
        /// <param name="box1"></param>
        /// <param name="box2"></param>
        /// <param name="cross">跨抽屉或跨柜子</param>
        /// <returns></returns>
        internal bool IsSimilarBox(BoxDevice box1, BoxDevice box2, bool cross)
        {
            var conditions = new List<bool>
            {
                box1.BoxMode == box2.BoxMode,
                box1.Injections?.Count == box2.Injections?.Count,
                boxSize(box1.Location) == boxSize(box2.Location),
                cross || sameDrawer(box1.No, box2.No),
            };
            return conditions.All(c => c);

            int boxSize(Location location)
            {
                var start = location.CellStart;
                var end = location.CellEnd;
                return (end.X - start.X + 1) * (end.Y - start.Y + 1) * (end.Z - start.Z + 1);
            }

            bool sameDrawer(string no1, string no2)
            {
                // 01-0101-0101
                var n1s = no1.Split(':');
                var n2s = no2.Split(':');
                return n1s[0] == n2s[0] && n1s[1].Substring(0, "01-0101".Length) == n2s[1].Substring(0, "01-0101".Length);
            }
        }

        /// <summary>
        ///     存、退、还
        /// </summary>
        /// <param name="exchanges"></param>
        /// <param name="boxes"></param>
        /// <param name="priority"></param>
        /// <param name="extension">扩展未占位药盒</param>
        /// <param name="cross">跨抽屉或跨柜子</param>
        internal void LocateCheckIn(ref Exchange[] exchanges, List<BoxDevice> boxes, bool extension, bool cross, LocatePriority priority = LocatePriority.CabinetFirst)
        {
            InitSchedule(exchanges);
            foreach (var exchange in exchanges)
            {
                List<(BoxDevice Box, NodeGoodsInfo Node)> options;
                if (extension)
                {
                    // 组合
                    // 1. 相同物品[GoodsId]且无现存量，2. 未占位且同类药盒，3. 不同物品[GoodsId]且无现存量（保留一个占位），4. 相同物品[GoodsId 批号 有效期]且有现存量
                    var box2 = new List<BoxDevice>();
                    var box3 = new List<BoxDevice>();

                    var box1 = boxes.Where(b => b.Fills.Any(f => f.GoodsId == exchange.GoodsId && f.QtyExisted == 0)).ToList();
                    var box4 = boxes.Where(b => b.Fills.Any(f => f.QtyExisted > 0 && CanCheckIn(f, exchange.GoodsId, exchange.BatchNumber, exchange.ExpiredDate))).ToList();
                    var goodsbox = box1.Concat(box4).FirstOrDefault();

                    box2 = boxes.Where(b => b.Fills.Count() == 0 && IsSimilarBox(goodsbox, b, cross)).Select(b => { b.Fills = goodsbox?.Fills.Select(f => new NodeGoodsInfo { GoodsId = f.GoodsId, Goods = f.Goods, Barcodes = f.Barcodes, BatchNumber = f.BatchNumber, ExpiredDate = f.ExpiredDate, QtyExisted = 0, QtyMax = f.QtyMax, StorageTime = f.StorageTime }).ToList(); return b; }).ToList();

                    var different = boxes.Where(b => b.Fills.All(f => f.QtyExisted == 0 && f.GoodsId != exchange.GoodsId) && IsSimilarBox(goodsbox, b, cross)).ToList();
                    var exist = boxes.Where(b => b.Fills.Any(f => f.QtyExisted > 0) && IsSimilarBox(goodsbox, b, cross)).ToList();
                    var newdifferent = new List<BoxDevice>();
                    foreach (var item in different)
                    {
                        if (!newdifferent.Where(n => n.Fills.Select(f => new { f.GoodsId, f.BatchNumber, f.ExpiredDate }).OrderBy(o => o.GoodsId).SequenceEqual(item.Fills.Select(f => new { f.GoodsId, f.BatchNumber, f.ExpiredDate }).OrderBy(o => o.GoodsId))).Any())
                        {
                            newdifferent.Add(item);
                        }
                    }

                    foreach (var item in newdifferent)
                    {
                        if (!exist.Where(b => b.Fills.Select(f => new { f.GoodsId, f.BatchNumber, f.ExpiredDate }).OrderBy(o => o.GoodsId).SequenceEqual(item.Fills.Select(f => new { f.GoodsId, f.BatchNumber, f.ExpiredDate }).OrderBy(o => o.GoodsId))).Any())
                        {
                            different.Remove(item);
                        }
                    }
                    box3 = different.Select(b => { b.Fills = goodsbox?.Fills.Select(f => new NodeGoodsInfo { GoodsId = f.GoodsId, Goods = f.Goods, Barcodes = f.Barcodes, BatchNumber = f.BatchNumber, ExpiredDate = f.ExpiredDate, QtyExisted = 0, QtyMax = f.QtyMax, StorageTime = f.StorageTime }).ToList(); return b; }).ToList();

                    options = box1.Concat(box2).Concat(box3).Concat(box4).Select(b => (b, b.Fills.First(f => f.GoodsId == exchange.GoodsId))).ToList();
                }
                else
                {
                    // 优先填充取量少的
                    options = boxes.Where(b => b.Fills.Any(f => CanCheckIn(f, exchange.GoodsId, exchange.BatchNumber, exchange.ExpiredDate)))
                        .Select(b => (Box: b, Node: b.Fills.First(f => f.GoodsId == exchange.GoodsId))).OrderBy(o => o.Node.QtyMax - o.Node.QtyExisted).ToList();
                }

                switch (priority)
                {
                    case LocatePriority.CabinetFirst:
                        options = options.Where(o => o.Box.BoxMode != BoxMode.VirtualBox).Concat(options.Where(o => o.Box.BoxMode == BoxMode.VirtualBox)).ToList();
                        break;
                    case LocatePriority.VirtualFirst:
                        options = options.Where(o => o.Box.BoxMode == BoxMode.VirtualBox).Concat(options.Where(o => o.Box.BoxMode != BoxMode.VirtualBox)).ToList();
                        break;
                }
                foreach (var option in options)
                {
                    var qty = exchange.Qty - exchange.Plans.Sum(p => p.Qty);
                    if (qty > 0)
                    {
                        var plan = new ActionPlan { Box = option.Box, Mode = ExchangeMode.CheckIn, Qty = Math.Min(qty, option.Node.QtyMax - option.Node.QtyExisted), IsExecuted = false, };
                        exchange.Plans.Add(plan);
                        option.Node.QtyExisted += plan.Qty;
                    }
                }
            }
        }

        internal bool CanCheckIn(NodeGoodsInfo fill, string goods, string batchNumber, DateTime expiredDate)
        {
            if (fill.GoodsId != goods || expiredDate.Date <= DateTime.Now.Date)
            {
                // 药品来源已过期，不能存储
                return false;
            }

            // 无现存量 或 有存储空间 √
            // 未指定批号 √ 或 和指定的批号一致 √
            if (fill.QtyExisted <= 0)
            {
                return true;
            }

            // 实际使用时 1. 不指定批号 2. 指定批号不指定有效期 3. 指定批号和有效期 4. 批号相同有效期不同
            return fill.QtyMax > fill.QtyExisted && fill.ExpiredDate.Date > DateTime.Now.Date && (string.IsNullOrEmpty(batchNumber) || (fill.BatchNumber ?? string.Empty).Equals(batchNumber ?? string.Empty, StringComparison.OrdinalIgnoreCase) && (expiredDate.Date == DateTime.MaxValue.Date || fill.ExpiredDate.Date == expiredDate.Date));
        }

        /// <summary>
        ///     卸、取、拿
        /// </summary>
        /// <param name="exchanges"></param>
        /// <param name="boxes"></param>
        /// <param name="priority"></param>
        /// <param name="isUnload">卸载</param>
        internal void LocateCheckOut(ref Exchange[] exchanges, List<BoxDevice> boxes, LocatePriority priority = LocatePriority.CabinetFirst, bool isUnload = false)
        {
            InitSchedule(exchanges);
            foreach (var exchange in exchanges)
            {
                // 取药按照过期时间、存储时间升序排序
                var options = boxes.Where(b => b.Fills.Any(f => CanCheckOut(f, exchange.GoodsId, exchange.BatchNumber, exchange.ExpiredDate, isUnload)))
                    .Select(b => new { Box = b, Node = b.Fills.First(f => f.GoodsId == exchange.GoodsId), }).OrderBy(o => o.Node.ExpiredDate).ThenBy(o => o.Node.StorageTime).ToList();
                switch (priority)
                {
                    case LocatePriority.CabinetFirst:
                        options = options.Where(o => o.Box.BoxMode != BoxMode.VirtualBox).Concat(options.Where(o => o.Box.BoxMode == BoxMode.VirtualBox)).ToList();
                        break;
                    case LocatePriority.VirtualFirst:
                        options = options.Where(o => o.Box.BoxMode == BoxMode.VirtualBox).Concat(options.Where(o => o.Box.BoxMode != BoxMode.VirtualBox)).ToList();
                        break;
                }

                foreach (var option in options)
                {
                    var box = JsonConvert.DeserializeObject<BoxDevice>(JsonConvert.SerializeObject(option.Box));
                    if (exchange is Prescription pre && pre.IsWhole)
                    {
                        // 医嘱，出院带药
                        var exists = (int)(option.Node.QtyExisted / exchange.Goods.Conversion);
                        var expect = (int)((exchange.Qty - exchange.Plans.Sum(p => p.Qty)) / exchange.Goods.Conversion);
                        if (exists > 0 && expect > 0)
                        {
                            var plan = new ActionPlan { Box = box, Mode = ExchangeMode.CheckOut, Qty = Math.Min(exists, expect) * exchange.Goods.Conversion, IsExecuted = false, };
                            exchange.Plans.Add(plan);
                            option.Node.QtyExisted -= plan.Qty;
                        }
                    }
                    else
                    {
                        var qty = exchange.Qty - exchange.Plans.Sum(p => p.Qty);
                        if (qty > 0)
                        {
                            var plan = new ActionPlan { Box = box, Mode = ExchangeMode.CheckOut, Qty = Math.Min(qty, option.Node.QtyExisted), IsExecuted = false, };
                            exchange.Plans.Add(plan);
                            option.Node.QtyExisted -= plan.Qty;
                        }
                    }
                }
            }
        }

        internal bool CanCheckOut(NodeGoodsInfo fill, string goods, string batchNumber, DateTime expiredDate, bool isUnload = false)
        {
            if (fill.GoodsId != goods)
            {
                return false;
            }

            if (isUnload && fill.QtyExisted > 0.0)
            {
                // 卸载物品，不用考虑批号有效期问题
                return true;
            }

            // 批号和有效期通常成对出现，如果没有批号则有效期通常不用考虑
            // 未过期且有现存量
            // 批号未指定或和指定的批号相等
            //
            // 实际使用时 1. 不指定批号 2. 指定批号不指定有效期 3. 指定批号和有效期 4. 批号相同有效期不同
            return fill.QtyExisted > 0.0 && fill.ExpiredDate.Date > DateTime.Now.Date && (string.IsNullOrEmpty(batchNumber) || (fill.BatchNumber ?? string.Empty).Equals(batchNumber ?? string.Empty, StringComparison.OrdinalIgnoreCase) && (expiredDate.Date == DateTime.MaxValue.Date || fill.ExpiredDate.Date == expiredDate.Date));
        }
    }
}
