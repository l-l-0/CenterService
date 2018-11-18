//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using MongoDB.Driver;
using System;
using System.Linq;
using System.Web.Services;

#pragma warning disable CS1591

namespace HealthCare.WebService.WebService
{
    /// <summary>
    /// Summary description for SDBZWebService
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]
    public class SDBZWebService : BaseWebService
    {
        public class GoodsProfile
        {
            public string UniqueId { get; set; }
            public string DisplayName { get; set; }
            public string Specification { get; set; }
            public string Manufacturer { get; set; }
            public string UsedUnit { get; set; }
            public string SmallPackageUnit { get; set; }
            public double Conversion { get; set; } = 1.0;
            public double QtyExisted { get; set; }
            public string BatchNumber { get; set; }
            public DateTime? ExpiredDate { get; set; }
        }

        [WebMethod(Description = "获取药房库存药品信息")]
        public GoodsProfile[] PharmacyInventory(string department)
        {
            var cabs = mongo.CustomerCollection.AsQueryable().Select(c => new
            {
                Cabinets = c.Cabinets.Where(o => o.DepartmentId == department),
                OutOfCabinets = c.OutOfCabinets.Where(v => v.DepartmentId == department),
            }).ToList();
            var fills = cabs.SelectMany(c => c.Cabinets).Concat(cabs.SelectMany(c => c.OutOfCabinets)).SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).SelectMany(b => b.Fills).ToList();
            var goodsIds = fills.Select(f => f.GoodsId).Distinct().ToList();
            var goods = mongo.GoodsCollection.AsQueryable().Where(o => goodsIds.Contains(o.UniqueId)).ToList();

            return fills.GroupBy(f => new { f.GoodsId, BatchNumber = f.BatchNumber ?? string.Empty, ExpiredDate = f.ExpiredDate.Date, })
                .Select(o =>
                {
                    var f = o.First();
                    f.Goods = goods.FirstOrDefault(x => x.UniqueId == f.GoodsId);
                    return new GoodsProfile
                    {
                        UniqueId = o.Key.GoodsId,
                        DisplayName = f.Goods?.DisplayName ?? string.Empty,
                        Specification = f.Goods?.Specification ?? string.Empty,
                        Manufacturer = f.Goods?.Manufacturer ?? string.Empty,
                        UsedUnit = f.Goods?.UsedUnit ?? string.Empty,
                        SmallPackageUnit = f.Goods?.SmallPackageUnit ?? string.Empty,
                        Conversion = f.Goods?.Conversion ?? 1.0,
                        QtyExisted = o.Sum(x => x.QtyExisted),
                        BatchNumber = o.Key.BatchNumber,
                        ExpiredDate = o.Key.ExpiredDate == DateTime.MaxValue.Date ? default(DateTime?) : o.Key.ExpiredDate,
                    };
                }).ToArray();
        }
    }
}
