//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable 1591, 1584, 1711, 1572, 1581, 1580

// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable once CheckNamespace

namespace HealthCare.Data
{
    /// <summary>
    ///     SFRA对象,所有可单独存储于mongo的collection中的数据要继承此类
    /// </summary>
    public abstract partial class SfraObject
    {
        /// <summary>
        ///     唯一标识符
        /// </summary>
        [JsonProperty("_id")]
        [BsonElement("_id")]
        [BsonId]
        [MongoMember(Display = "唯一标识符", IsRequired = true, Order = 1)]
        public string UniqueId { get; set; } = GenerateId();
        /// <summary>
        ///     数据对象创建时间,通常是MONGO数据生成的时间
        /// </summary>
        [MongoMember(Order = 8)]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        public static string EmptyId() => ObjectId.Empty.ToString();

        public static string GenerateId() => ObjectId.GenerateNewId().ToString();
    }

    /// <summary>
    ///     支持同步第三方数据 对象基类
    /// </summary>
    public abstract partial class MongoSyncable : SfraObject
    {
        /// <summary>
        ///     对象名称
        /// </summary>
        [MongoMember(Display = "名称", IsRequired = true, Order = 2)]
        public string DisplayName { get; set; }
        /// <summary>
        ///     显示顺序
        /// </summary>
        [MongoMember(Display = "显示顺序", Order = 6)]
        public int DisplayOrder { get; set; } = -1;
        /// <summary>
        ///     数据是否禁用、无效
        /// </summary>
        [MongoMember(Display = "数据是否禁用、无效", Order = 7)]
        public bool IsDisabled { get; set; }
        /// <summary>
        ///     根据 UniqueId 判断两个对象是否相等
        /// </summary>
        public static bool operator ==(MongoSyncable obj1, MongoSyncable obj2) => string.Equals(obj1?.UniqueId, obj2?.UniqueId, StringComparison.OrdinalIgnoreCase);
        /// <summary>
        ///     根据 UniqueId 判断两个对象是否不等
        /// </summary>
        public static bool operator !=(MongoSyncable obj1, MongoSyncable obj2) => !(obj1 == obj2);
        public override bool Equals(object obj) => obj is MongoSyncable && string.Equals(((MongoSyncable)obj).UniqueId, UniqueId, StringComparison.OrdinalIgnoreCase);
        public override int GetHashCode() => UniqueId.GetHashCode();
        public override string ToString() => DisplayName;
    }

    /// <summary>
    ///     可通过拼音检索的对象
    /// </summary>
    public abstract class RetrievableObject : MongoSyncable
    {
        /// <summary>
        ///     检索编码，不同于拼音检索的另一种编码体系    [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "不同于拼音检索的另一种编码体系", Description = "通常和 UniqueId 相等", Order = 3, IsRequired = true)]
        [BsonIgnoreIfNull]
        public string Code { get; set; }
        /// <summary>
        ///     拼音码，简拼，即首字母缩写，大写形式，如SFRA    [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(IsHidden = true, Order = 4)]
        [BsonIgnoreIfNull]
        public string Pinyin { get; set; }
        /// <summary>
        ///     拼音全拼，含声母韵母，且声母大写，韵母小写，如ShengFuRuiAn [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(IsHidden = true, Order = 5)]
        [BsonIgnoreIfNull]
        public string PinyinFull { get; set; }
    }
}

// Customer
namespace HealthCare.Data
{
    /// <summary>
    ///     硬件存储节点
    /// </summary>
    [BsonDiscriminator(nameof(Storage))]
    public partial class Storage
    {
        /// <summary>
        ///     终端计算机IP
        /// </summary>
        public string Computer { get; set; }
        /// <summary>
        ///     显示文本标签，通常是编码的文本描述意义
        /// </summary>
        public string DisplayText { get; set; }
        /// <summary>
        ///     节点编码，例如药柜可控设备采用14位16进制编码 FF(药柜) FFFF(抽屉) FFFF(药盒) FFFF(针剂) 等4层级
        ///     格式：ip:node 如 192.168.3.1:01-0101
        /// </summary>
        public string No { get; set; }
        /// <summary>
        ///     存储位置的信息，即通过行、列描述的位置
        /// </summary>
        public Location Location { get; set; } = new Location();
        /// <summary>
        ///     是否受控的存储节点，'受控'即具有硬件指令执行或信号的存储设备，其余则为'非受控'
        /// </summary>
        public bool IsControlled { get; set; }
        /// <summary>
        ///     节点类型，用于描述子类，如药柜，针剂，玻璃门
        /// </summary>
        public string NodeType { get; set; }
        /// <summary>
        ///     该药盒的唯一使用者（不为空时该盒子被锁定，解锁之后可以被其他人使用）
        /// </summary>
        [BsonIgnoreIfNull]
        public string OwnerId { get; set; }
        public override string ToString() => DisplayText;
    }

    /// <summary>
    ///     客户信息
    /// </summary>
    [Mongo(Collection = nameof(Customer), AllowSync = false)]
    [BsonDiscriminator(nameof(Customer))]
    public partial class Customer : MongoSyncable
    {
        /// <summary>
        ///     智能药柜
        /// </summary>
        public List<CabinetDevice> Cabinets { get; set; } = new List<CabinetDevice>();
        /// <summary>
        ///     柜外存储
        /// </summary>
        public List<CabinetDevice> OutOfCabinets { get; set; } = new List<CabinetDevice>();
        /// <summary>
        ///     上级
        /// </summary>
        [BsonIgnoreIfNull]
        public string ParentId { get; set; }
    }

    /// <summary>
    ///     智能药柜 或 柜外存储
    /// <remarks>
    ///     柜外存储通常是保险柜，木制药柜，冰柜等可二维布局的存储位置    
    /// </remarks>
    /// </summary>
    [BsonDiscriminator(nameof(CabinetDevice))]
    public partial class CabinetDevice : Storage
    {
        /// <summary>
        ///     是否是主柜
        /// </summary>
        public bool IsPrimary { get; set; }
        /// <summary>
        ///     柜子显示顺序
        /// </summary>
        public int DisplayOrder { get; set; }
        /// <summary>
        ///     抽屉，按照层方式存储
        /// </summary>
        public List<DrawerDevice> Drawers { get; set; } = new List<DrawerDevice>();
        /// <summary>
        ///     所属发药科室  [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        public Department Department { get; set; }
        /// <summary>
        ///     所属发药科室
        /// </summary>
        public string DepartmentId { get; set; }
        /// <summary>
        ///     父级关联ID
        /// </summary>
        [BsonIgnoreIfNull]
        public string ParentId { get; set; }
        /// <summary>
        ///     所属客户，即客户的编码
        /// </summary>
        public string OwnerCode { get; set; }
    }

    /// <summary>
    ///     位置模型，通过(start,end)坐标形式表示一个矩形
    /// </summary>
    [BsonDiscriminator(nameof(Location))]
    public partial class Location
    {
        /// <summary>
        ///     单元格起点
        /// </summary>
        public Cell CellStart { get; set; } = new Cell();
        /// <summary>
        ///     单元格终点
        /// </summary>
        public Cell CellEnd { get; set; } = new Cell();
        /// <summary>
        ///     位置备注描述，如主柜第三层第二受控药盒，柜外16号药盒，冷藏冰箱
        /// </summary>
        [BsonIgnoreIfNull]
        public string Remark { get; set; }
        public override string ToString() => $"[{(char)(CellStart.X + 'A' - 1)}{CellStart?.Y},{(char)(CellEnd.X + 'A' - 1)}{CellEnd?.Y}]";
    }

    /// <summary>
    ///     单元格点坐标
    /// </summary>
    [BsonDiscriminator(nameof(Cell))]
    public partial class Cell
    {
        /// <summary>
        ///     列 - X，从 1 开始计数
        /// </summary>
        public int X { get; set; } = 1;
        /// <summary>
        ///     行 - Y，从 1 开始计数
        /// </summary>
        public int Y { get; set; } = 1;
        /// <summary>
        ///     深度 Z，从 1 开始计数
        /// </summary>
        public int Z { get; set; } = 1;
        public override string ToString() => $"[{X},{Y},{Z}]";
    }

    /// <summary>
    ///     药抽屉
    /// </summary>
    [BsonDiscriminator(nameof(DrawerDevice))]
    public partial class DrawerDevice : Storage
    {
        /// <summary>
        ///     最大列数
        /// </summary>
        public int MaxColumn { get; set; }
        /// <summary>
        ///     最大行数
        /// </summary>
        public int MaxRow { get; set; }
        /// <summary>
        ///     抽屉中药盒的上边界距离底边的距离
        /// </summary>
        public int PaddingTop { get; set; } = 35;
        /// <summary>
        ///     抽屉中药盒的下边界距离底边的距离
        /// </summary>
        public int PaddingBottom { get; set; } = 5;
        /// <summary>
        ///     药盒
        /// </summary>
        public List<BoxDevice> Boxes { get; set; } = new List<BoxDevice>();
        /// <summary>
        ///     抽屉的药盒是否已经完全锁定
        /// </summary>
        public bool IsAllLocked { get; set; }
        /// <summary>
        ///     智能柜配置页面是否已经锁定布局
        /// </summary>
        public bool IsLayoutLocked { get; set; }
        /// <summary>
        ///     抽屉是否作为回收箱（如 空安瓿回收）
        /// </summary>
        public bool IsRecycleBin { get; set; }
        [JsonIgnore]
        [BsonIgnore]
        public CabinetDevice Parent { get; set; }
    }

    /// <summary>
    ///     药盒
    /// </summary>
    [BsonDiscriminator(nameof(BoxDevice))]
    public partial class BoxDevice : Storage
    {
        /// <summary>
        ///     药盒类型
        /// </summary>
        public BoxMode BoxMode { get; set; }
        /// <summary>
        ///     填充的物品的 Id
        /// </summary>
        public List<NodeGoodsInfo> Fills { get; set; } = new List<NodeGoodsInfo>();
        /// <summary>
        ///     针剂
        /// </summary>
        [BsonIgnoreIfNull]
        public List<InjectionDevice> Injections { get; set; }
        /// <summary>
        ///     是否认为 <see cref="Fills" /> 是一个套装
        /// </summary>
        public bool IsKit { get; set; }
        /// <summary>
        ///     套装
        /// </summary>
        [BsonIgnoreIfNull]
        public string KitId { get; set; }
        /// <summary>
        ///     套装名称
        /// </summary>
        [BsonIgnoreIfNull]
        public string KitName { get; set; }
        /// <summary>
        ///     操作该药盒时是否需要双人认证
        /// </summary>
        public bool IsDoubleCertify { get; set; }
        /// <summary>
        ///     是否故障。通常是用户在使用时发现药盒不能打开，标记为故障。已经故障的药盒不能参与后续的位置分配
        /// </summary>
        public bool IsBreakdown { get; set; }
        [JsonIgnore]
        [BsonIgnore]
        public DrawerDevice Parent { get; set; }
    }

    /// <summary>
    ///     <see cref="BoxDevice" />的类型
    /// </summary>
    public enum BoxMode
    {
        /// <summary>
        ///     非控药盒
        /// </summary>
        UnmanagedBox = 0,
        /// <summary>
        ///     受控药盒
        /// </summary>
        ManagedBox = 1,
        /// <summary>
        ///     受控针剂
        /// </summary>
        InjectionBox = 2,
        /// <summary>
        ///     受控 LED 的药盒 (单色灯)
        /// </summary>
        LedBoxVersion1 = 3,
        /// <summary>
        ///     受控 LED 的药盒 (双色灯)
        /// </summary>
        LedBoxVersion2 = 4,

        /// <summary>
        ///     柜外（算法使用。分配的位置不属于智能药柜）
        /// </summary>
        VirtualBox = -1,
    }

    /// <summary>
    ///     药盒和物品信息的关联
    /// </summary>
    [BsonDiscriminator(nameof(NodeGoodsInfo))]
    public partial class NodeGoodsInfo
    {
        /// <summary>
        ///     物品耗材对象 [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        public Goods Goods { get; set; }
        /// <summary>
        ///     物品耗材的ID
        /// </summary>
        public string GoodsId { get; set; }
        /// <summary>
        ///     批号
        /// </summary>
        public string BatchNumber { get; set; } = string.Empty;
        /// <summary>
        ///     有效期 [DateTime.MaxValue]
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime ExpiredDate { get; set; } = DateTime.MaxValue.Date;
        /// <summary>
        ///     物品现存量
        /// </summary>
        public double QtyExisted { get; set; }
        /// <summary>
        ///     该节点最大存储量
        /// </summary>
        public double QtyMax { get; set; } = 1;
        /// <summary>
        ///     按照药格存储时间进行物品推送算法使用（有针剂时，以针剂的最小时间为准）
        ///     第一次补充物品，顺序使用后，若某个药格是针剂且未使用完
        ///     第二次补充物品后，推送的物品地址应该接续  [DateTime.Now]
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime StorageTime { get; set; } = DateTime.Now;
        /// <summary>
        ///     扫描枪条码
        /// </summary>
        public string[] Barcodes { get; set; } = new string[0];
        /// <summary>
        ///     存储节点 No [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        public string StorageNo { get; set; }
        /// <summary>
        ///     存储节点名称 [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        public string StorageDisplay { get; set; }
    }

    /// <summary>
    ///     针剂设备
    /// </summary>
    [BsonDiscriminator(nameof(InjectionDevice))]
    public partial class InjectionDevice : Storage
    {
        [JsonIgnore]
        [BsonIgnore]
        public BoxDevice Parent { get; set; }
    }
}

// Exchange
namespace HealthCare.Data
{
    /// <summary>
    ///     物品流转（虚拟医嘱）
    /// </summary>
    [Mongo(Collection = nameof(Exchange), AllowSync = false)]
    [BsonDiscriminator(nameof(Exchange))]
    public partial class Exchange : MongoSyncable
    {
        /// <summary>
        ///     物品
        /// </summary>
        [MongoMember(IsHidden = true)]
        public Goods Goods { get; set; }
        /// <summary>
        ///     物品唯一标识
        /// </summary>
        [MongoMember(Display = "物品唯一标识", IsRequired = true, Order = 16)]
        public string GoodsId { get; set; }
        /// <summary>
        ///     物品批号，当处方提供批号时，分配药盒位置时则必须使用该批号
        /// </summary>
        [MongoMember(Display = "物品批号", Order = 17)]
        public string BatchNumber { get; set; } = string.Empty;
        /// <summary>
        ///     物品有效期
        /// </summary>
        [MongoMember(Display = "物品有效期", Order = 18)]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime ExpiredDate { get; set; } = DateTime.MaxValue.Date;
        /// <summary>
        ///     流转模式，明确使用该属性表述，在业务中不可使用<see cref="Qty" />的正负数判定
        /// </summary>
        [MongoMember(Display = "执行模式", Description = "CheckIn or CheckOut", IsRequired = true, Order = 19)]
        public ExchangeMode Mode { get; set; }
        /// <summary>
        ///     物品的流转数量
        /// </summary>
        [MongoMember(Display = "流转数量", Description = "物品的取退数量", IsRequired = true, Order = 20)]
        public double Qty { get; set; }

        /// <summary>
        ///     动作计划，指定位置指定物品   [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public List<ActionPlan> Plans { get; set; }
        /// <summary>
        ///     算法指派存储空间是否完成
        /// </summary>
        [BsonIgnore]
        [JsonIgnore]
        [MongoMember(IsHidden = true)]
        public bool IsSpecified => Plans?.Sum(p => p.Qty) == Qty;
        /// <summary>
        ///     实际执行的物品数量
        /// </summary>
        [MongoMember(IsHidden = true)]
        public double QtyActual { get; set; }
        /// <summary>
        ///     物品条码
        /// </summary>
        [MongoMember(Display = "物品条码", Order = 20)]
        public List<string> GoodsBarcodes { get; set; } = new List<string>();
        /// <summary>
        ///     完成时刻
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? FinishTime { get; set; }

        /// <summary>
        ///     所属 Customer
        /// </summary>
        [MongoMember(IsHidden = true)]
        public string CustomerId { get; set; }
        /// <summary>
        ///     终端 IP
        /// </summary>
        [MongoMember(IsHidden = true)]
        public string Computer { get; set; }
        /// <summary>
        ///     处方、调拨、预支条形码 [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "处方、调拨、预支条形码", Order = 11)]
        public string ExchangeBarcode { get; set; }
        /// <summary>
        ///     记录来源，药方记录来源，如HIS、手麻(OIS)、估药(Evaluated)、预支(Medication)、虚拟(Virtual)、HIS调拨(Allocate)、SFRA内部流转(Int-Allocate)
        /// </summary>
        [MongoMember(Display = "记录来源，药方记录来源", Description = "如HIS、手麻(OIS)、估药(Evaluated)、预支(Medication)、虚拟(Virtual)、HIS调拨(Allocate)、SFRA内部流转(Int-Allocate)", Order = 23, IsRequired = true)]
        public string RecordType { get; set; }
        /// <summary>
        ///     时间过滤，SFRA方使用该属性进行按时间条件筛选数据条目
        /// </summary>
        [MongoMember(Display = "时间过滤", Description = "SFRA方使用该属性进行按时间条件筛选数据条目", Order = 13, IsRequired = true)]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime TimeFilter { get; set; } = DateTime.Now;
        /// <summary>
        ///     平账人ID，有平账人  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true, Display = "销账的用户")]
        public string ChargeOffId { get; set; }


        /// <summary>
        ///     是否已完成回收空安瓿废贴
        /// </summary>
        [MongoMember(Display = "已完成回收空安瓿废贴", IsHidden = true)]
        public bool FinishedAmpoule { get; set; }
        /// <summary>
        ///     回收记录    [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "回收记录", IsHidden = true)]
        [BsonIgnoreIfNull]
        public List<AmpouleRecord> AssignAmpouleRecords { get; set; }
        /// <summary>
        ///     处方、调拨、预支打印编号    [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public string PrintNumber { get; set; }
        /// <summary>
        ///     打印次数相关信息    [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public List<PrintRecordInfo> PrintRecords { get; set; }

        [BsonDiscriminator(nameof(AmpouleRecord))]
        public partial class AmpouleRecord : SfraObject
        {
            public string AmpouleId { get; set; }
            public string BatchNumber { get; set; } = string.Empty;
            [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
            public DateTime ExpiredDate { get; set; } = DateTime.MaxValue.Date;
            public double Qty { get; set; }
            /// <summary>
            ///     归还空安瓿的人
            /// </summary>
            public string RepaidPerson { get; set; }
            /// <summary>
            ///     接收空安瓿的人
            /// </summary>
            public string ReceivePerson { get; set; }
            /// <summary>
            ///     流转的 Id
            /// </summary>
            public string OwnerCode { get; set; }
            /// <summary>
            ///     回收类型(空安瓿回收,药品回收)
            /// </summary>
            public string RecycleType { get; set; }
        }
    }

    /// <summary>
    ///     流转模式
    /// </summary>
    public enum ExchangeMode
    {
        /// <summary>
        ///     存、退、还
        /// </summary>
        CheckIn = 1,
        /// <summary>
        ///     卸、取、拿
        /// </summary>
        CheckOut = 2,
        /// <summary>
        ///     药方
        /// </summary>
        [Obsolete]
        Medication = 3,
    }

    /// <summary>
    ///     动作计划，操作指定位置的物品
    /// </summary>
    [BsonDiscriminator(nameof(ActionPlan))]
    public partial class ActionPlan : SfraObject
    {
        /// <summary>
        ///     取、退
        /// </summary>
        public ExchangeMode Mode { get; set; }
        /// <summary>
        ///     物品所存储的节点
        /// </summary>
        public BoxDevice Box { get; set; }
        /// <summary>
        ///     物品变化量，存储节点所分配的物品数量，无正负之分，增减含义通过<see cref="Mode" />区分
        /// </summary>
        public double Qty { get; set; }
        /// <summary>
        ///     动作是否被执行，执行计划后要根据<see cref="Mode" />和<see cref="Qty" />去修改<see cref="Box" />所指向位置的
        ///     <see cref="NodeGoodsInfo.QtyExisted" />现存数量以及<see cref="NodeGoodsInfo.StorageTime" />存储时间
        /// </summary>
        public bool IsExecuted { get; set; }
    }

    /// <summary>
    ///     物品调配信息（SFRA物品来源），HIS上级库房调配、同级科室调配
    /// </summary>
    [Mongo(Order = 501, Display = "物品调拨", Collection = nameof(Exchange) + "." + nameof(Allocation))]
    [BsonDiscriminator(nameof(Allocation))]
    public partial class Allocation : Exchange
    {
        /// <summary>
        ///     申请号
        /// </summary>
        [MongoMember(Display = "申请号", IsRequired = true, Order = 11)]
        public string ApplyId { get; set; }
        /// <summary>
        ///     申请数量
        /// </summary>
        [MongoMember(Display = "申请数量", IsRequired = true, Order = 11)]
        public double ApplyQty { get; set; }
        /// <summary>
        ///     出库部门/科室
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public Department DepartmentSource { get; set; }
        /// <summary>
        ///     出库部门/科室ID
        /// </summary>
        [MongoMember(Display = "出库部门/科室ID", IsRequired = true, Order = 12)]
        public string DepartmentSourceId { get; set; }
        /// <summary>
        ///     接收部门/科室
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public Department DepartmentDestination { get; set; }
        /// <summary>
        ///     接收部门/科室ID
        /// </summary>
        [MongoMember(Display = "接收部门/科室ID", IsRequired = true, Order = 13)]
        public string DepartmentDestinationId { get; set; }
        /// <summary>
        ///     出库序号
        /// </summary>
        [MongoMember(Display = "出库序号", IsRequired = true, Order = 12)]
        [BsonIgnoreIfNull]
        public string DeliveryNumber { get; set; }
        /// <summary>
        ///     HIS 确认交付的时刻
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "HIS 确认交付的时刻", IsRequired = true, Order = 12)]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? DeliverTime { get; set; }


        /// <summary>
        ///     已经接收的数量   当发送数量<see cref="Exchange.Qty" />大于接收数量<see cref="AcceptedQty" />时，表示未完全入库
        /// </summary>
        [MongoMember(IsHidden = true)]
        public double AcceptedQty { get; set; }
        /// <summary>
        ///     接收操作人（通常为工号）
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public string Receiver { get; set; }
        /// <summary>
        ///     接收操作人姓名
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public string ReceiverName { get; set; }
        /// <summary>
        ///     接收时间
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? ReceivedTime { get; set; }
        /// <summary>
        ///     只验收一部分物品时填写的原因
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public string UnCompleteReason { get; set; }
        /// <summary>
        ///     接收确认人\复核人 Id
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public string Deliverer { get; set; }
        /// <summary>
        ///     接收确认人\复核人姓名
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public string DelivererName { get; set; }
        /// <summary>
        ///     入库人、出库人
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public string Storager { get; set; }
        /// <summary>
        ///     入库人、出库人姓名
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public string StoragerName { get; set; }

        /// <summary>
        ///     当为 SFRA 生成的调拨单时，记录对应的流转 Id
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public string ExchangeId { get; set; }

        public override string ToString() => $"{UniqueId};{DepartmentSourceId},{DepartmentDestinationId};{GoodsId},{BatchNumber},{ExpiredDate:yyyy-MM-dd},{Mode},{ApplyQty};{string.Join(",", GoodsBarcodes)}";
    }

    /// <summary>
    ///     医嘱、处方等药方信息（用药记录凭证）
    /// </summary>
    [Mongo(Order = 301, Display = "医嘱/处方", Collection = nameof(Exchange) + "." + nameof(Prescription))]
    [BsonDiscriminator(nameof(Prescription))]
    public partial class Prescription : Exchange
    {
        /// <summary>
        ///     单号，处方号、请领单号
        /// </summary>
        [MongoMember(Display = "单号", Description = "单号，处方号、请领单号", Order = 11)]
        [BsonIgnoreIfNull]
        public string TrackNumber { get; set; }
        /// <summary>
        ///     开单科室 [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        [MongoMember(IsHidden = true)]
        public Department DepartmentSource { get; set; }
        /// <summary>
        ///     开单科室 Id
        /// </summary>
        [MongoMember(Display = "开单科室", IsRequired = true, Order = 22)]
        public string DepartmentSourceId { get; set; }
        /// <summary>
        ///     医生
        /// </summary>
        [MongoMember(IsHidden = true)]
        public Employee Doctor { get; set; }
        /// <summary>
        ///     医生唯一标识
        /// </summary>
        [MongoMember(Display = "医生唯一标识", Description = "医嘱的开嘱医生", IsRequired = true, Order = 14)]
        public string DoctorId { get; set; }
        /// <summary>
        ///     患者
        /// </summary>
        [MongoMember(IsHidden = true)]
        public Patient Patient { get; set; }
        /// <summary>
        ///     患者唯一标识
        /// </summary>
        [MongoMember(Display = "患者唯一标识", Description = "医嘱的用药患者", IsRequired = true, Order = 15)]
        public string PatientId { get; set; }
        /// <summary>
        ///     医嘱类型，如长期医嘱、临时医嘱，出院带药
        /// </summary>
        [MongoMember(Display = "类型说明", Description = "医嘱类型，如长期医嘱、临时医嘱，出院带药", Order = 12)]
        public string Description { get; set; }
        /// <summary>
        ///     是否是整盒、整瓶取药
        /// </summary>
        [MongoMember(Display = "取药策略", Description = "是否是整盒、整瓶取药", IsRequired = true, Order = 21)]
        public bool IsWhole { get; set; }
        /// <summary>
        ///     开嘱时间
        /// </summary>
        [MongoMember(Display = "开嘱时间", IsRequired = true, Order = 13)]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime IssuedTime { get; set; }
        /// <summary>
        ///     用药频次，
        ///     qd 每日一次，
        ///     bid 每日两次，
        ///     tid 每日三次，
        ///     qid 每日四次，
        ///     qh 每小时一次，
        ///     q2h 每两小时一次，
        ///     q4h 每四小时一次，
        ///     q6h 每六小时一次，
        ///     qn 每晚一次，
        ///     qod 隔日一次，
        ///     biw 每周两次，
        ///     hs 临睡前，
        ///     am 上午pm 下午，
        ///     St 立即，
        ///     DC 停止、取消，
        ///     prn 需要时（长期），
        ///     sos 需要时（限用一次，12小时内有效），
        ///     ac 饭前pc 饭后，
        ///     12n 中午12点，
        ///     12mn午夜12点，
        ///     gtt 滴，
        ///     ID 皮内注射，
        ///     H 皮下注射，
        ///     IM 肌肉注射，
        ///     IV 静脉注射，
        /// </summary>
        [MongoMember(Display = "用药频次", Order = 12)]
        [BsonIgnoreIfNull]
        public string UsedFrequency { get; set; }
        /// <summary>
        ///     途径,
        ///     po  口服
        ///     im  肌内注射
        ///     iv  静脉注射
        ///     ivgtt   静脉注射
        /// </summary>
        [MongoMember(Display = "途径", Order = 12)]
        [BsonIgnoreIfNull]
        public string UsedPurpose { get; set; }
        /// <summary>
        ///     每个频次时的用量
        /// </summary>
        [MongoMember(Display = "使用剂量", Order = 12)]
        [BsonIgnoreIfNull]
        public string UsedDosage { get; set; }

        /// <summary>
        ///     收费员 [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "收费员", Order = 22)]
        [BsonIgnoreIfNull]
        public string FeeCollectorId { get; set; }
        /// <summary>
        ///     收费时间    [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "收费时间", Order = 22)]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        [BsonIgnoreIfNull]
        public DateTime? FeeTime { get; set; }
        /// <summary>
        ///     收费类别，如自费、公费、医保  [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "类型说明", Description = "收费类别，如自费、公费、医保", Order = 22)]
        [BsonIgnoreIfNull]
        public string FeeType { get; set; }
        /// <summary>
        ///     押金  [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public Deposit Deposit { get; set; }


        /// <summary>
        ///     发药科室 [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        [MongoMember(IsHidden = true)]
        public Department DepartmentDestination { get; set; }
        /// <summary>
        ///     发药科室 Id
        /// </summary>
        [MongoMember(Display = "发药科室", IsRequired = true, Order = 23)]
        public string DepartmentDestinationId { get; set; }
        /// <summary>
        ///     代理取药人 Id  [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "代理取药人", IsRequired = true, Order = 24)]
        [BsonIgnoreIfNull]
        public string AgentId { get; set; }
        /// <summary>
        ///     代理取药人  [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public Person Agent { get; set; }
        /// <summary>
        ///     发药确认人\复核人 Id   [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "发药确认人\\复核人", Order = 24)]
        [BsonIgnoreIfNull]
        public string DispensingId { get; set; }
        /// <summary>
        ///     发药确认\复核时间  [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "发药确认\\复核时间", Order = 24)]
        [BsonIgnoreIfNull]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? DispensingTime { get; set; }
        /// <summary>
        ///     发药确认\复核的摆药单号
        /// </summary>
        [MongoMember(Display = "发药确认\\复核的摆药单号", Order = 24)]
        [BsonIgnoreIfNull]
        public string DispensingNumber { get; set; }

        /// <summary>
        ///     是否是先预支，后补录的医嘱
        /// </summary>
        [MongoMember(IsHidden = true)]
        public bool IsAddition { get; set; }
        /// <summary>
        ///     处理执行的状态，如 HIS已审核；HIS已计费；HIS已执行；SFRA已分配(位置); SFRA已执行；SFRA已审核；SFRA已上传； SFRA撤销、恢复、虚拟、终止等；通常处于'撤销'状态的记录不再参与任何统计报告计算
        /// </summary>
        [MongoMember(IsHidden = true)]
        public string FlowState { get; set; }
        /// <summary>
        ///     对<see cref="FlowState" />的详细描述，如 从'HIS已执行' 到 'SFRA已分配(位置)' 时 发生'物品批号不一致，无法提供HIS物品批号取药'
        /// </summary>
        [MongoMember(IsHidden = true)]
        public string FlowRemark { get; set; }
        /// <summary>
        ///     手术唯一标识
        /// </summary>
        public string OperationScheduleId { get; set; }
        /// <summary>
        /// 重试次数
        /// </summary>
        public int RetriesNumber { get; set; }
        /// <summary>
        /// 是否已经计费成功
        /// </summary>
        public bool IsSynchronized { get; set; }

        public override string ToString() => $"{UniqueId};{DepartmentSourceId},{DoctorId};{DepartmentDestinationId},{PatientId};{GoodsId},{Qty},{BatchNumber},{ExpiredDate:yyyy-MM-dd};{Mode}";
    }

    /// <summary>
    ///     押金
    /// </summary>
    [BsonDiscriminator(nameof(Deposit))]
    public partial class Deposit : SfraObject
    {
        /// <summary>
        ///     金额
        /// </summary>
        public double Amount { get; set; }
        /// <summary>
        ///     押金编号
        /// </summary>
        public string Code { get; set; }
        /// <summary>
        ///     打印次数信息
        /// </summary>
        public List<PrintRecordInfo> PrintRecords { get; set; }
    }

    /// <summary>
    ///     数据打印次数相关信息
    /// </summary>
    [BsonDiscriminator(nameof(PrintRecordInfo))]
    public partial class PrintRecordInfo : SfraObject
    {
        /// <summary>
        ///     操作员，打印操作员
        /// </summary>
        public Employee Operator { get; set; }
        /// <summary>
        ///     打印次数
        /// </summary>
        public int PrintCount { get; set; }
        /// <summary>
        ///     打印时刻
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime PrintTime { get; set; }
        /// <summary>
        ///     打印的时刻、打印人、第几次打印，备注信息 
        ///     次数、打印人员、打印时间、备注等
        /// </summary>
        public string Remark { get; set; }
    }

    /// <summary>
    ///     预支记录
    /// </summary>
    [Mongo(Collection = nameof(Exchange) + "." + nameof(Medication), AllowSync = false)]
    [BsonDiscriminator(nameof(Medication))]
    public partial class Medication : Exchange
    {
        /// <summary>
        ///     医生
        /// </summary>
        [MongoMember(IsHidden = true)]
        public Employee Doctor { get; set; }
        /// <summary>
        ///     医生唯一标识
        /// </summary>
        [MongoMember(Display = "医生唯一标识", Description = "医嘱的开嘱医生", IsRequired = true, Order = 14)]
        public string DoctorId { get; set; }
        /// <summary>
        ///     患者
        /// </summary>
        [MongoMember(IsHidden = true)]
        public Patient Patient { get; set; }
        /// <summary>
        ///     患者唯一标识
        /// </summary>
        [MongoMember(Display = "患者唯一标识", Description = "医嘱的用药患者", IsRequired = true, Order = 15)]
        public string PatientId { get; set; }
        /// <summary>
        ///     操作人唯一标识
        /// </summary>
        public string OperatorId { get; set; }
        /// <summary>
        ///     操作人
        /// </summary>
        [BsonIgnoreIfNull]
        public string OperatorName { get; set; }

        /// <summary>
        ///     是否已经和 HIS 同步数据
        /// </summary>
        public bool IsSynchronized { get; set; }
        /// <summary>
        ///     计费时间
        /// </summary>
        public DateTime? FeeTime { get; set; }
        /// <summary>
        ///     关联的医嘱。由本预支记录产生的补录 当 <see cref="Exchange.Mode"/>为<see cref="ExchangeMode.Medication"/>时有效
        /// </summary>
        [BsonIgnoreIfNull]
        public string PrescriptionId { get; set; }
        /// <summary>
        ///     当 <see cref="Exchange.Mode"/>为<see cref="ExchangeMode.Medication"/>时，记录关联的预支取药记录
        /// </summary>
        [BsonIgnoreIfNull]
        [Obsolete]
        public string[] CheckOutIds { get; set; }
        /// <summary>
        ///     当 <see cref="Exchange.Mode"/>为<see cref="ExchangeMode.Medication"/>时，记录关联的预支退回记录
        /// </summary>
        [BsonIgnoreIfNull]
        [Obsolete]
        public string CheckInId { get; set; }
        /// <summary>
        ///     取退平衡。 预支、取退、消耗平衡
        /// </summary>
        [BsonIgnoreIfNull]
        [Obsolete]
        public bool? InOutBalance { get; set; }
        /// <summary>
        ///     急症补录时上传的图片的地址
        /// </summary>
        [BsonIgnoreIfNull]
        public string GoodsSnapshot { get; set; }

        /// <summary>
        ///     手术
        /// </summary>
        [BsonIgnoreIfNull]
        public OperationSchedule OperationSchedule { get; set; }
        /// <summary>
        ///     手术唯一标识
        /// </summary>
        public string OperationScheduleId { get; set; }
    }

    /// <summary>
    ///     内部流转记录
    /// </summary>
    [Mongo(Collection = nameof(Exchange) + "." + nameof(InternalAllocation), AllowSync = false)]
    [BsonDiscriminator(nameof(InternalAllocation))]
    public partial class InternalAllocation : Exchange
    {
        /// <summary>
        ///     申请号
        /// </summary>
        [MongoMember(IsRequired = true)]
        public string ApplyId { get; set; }
        /// <summary>
        ///     转入终端
        /// </summary>
        public string TurnInDevice { get; set; }
        /// <summary>
        ///     转出终端
        /// </summary>
        public string TurnOutDevice { get; set; }
        /// <summary>
        ///     操作人唯一标识
        /// </summary>
        public string OperatorId { get; set; }
        /// <summary>
        ///     操作人
        /// </summary>
        public string OperatorName { get; set; }
    }

    /// <summary>
    ///     物品库存，归档记录
    /// </summary>
    [Mongo(Collection = nameof(Inventory), AllowSync = false)]
    [BsonDiscriminator(nameof(Inventory))]
    public partial class Inventory : MongoSyncable
    {
        /// <summary>
        ///     客户编码
        /// </summary>
        public string CustomerId { get; set; }
        /// <summary>
        ///     终端计算机IP
        /// </summary>
        public string Computer { get; set; }
        /// <summary>
        ///     物品对象 [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        public Goods Goods { get; set; }
        /// <summary>
        ///     物品Id
        /// </summary>
        public string GoodsId { get; set; }
        /// <summary>
        ///     批号
        /// </summary>
        public string BatchNumber { get; set; } = string.Empty;
        /// <summary>
        ///     过期时间
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime ExpiredDate { get; set; } = DateTime.MaxValue.Date;
        /// <summary>
        ///     初始库存（上次计算的结存）
        /// </summary>
        public double QtyInitial { get; set; }
        /// <summary>
        ///     消耗量
        /// </summary>
        public double QtyCheckOut { get; set; }
        /// <summary>
        ///     入库量
        /// </summary>
        public double QtyCheckIn { get; set; }
        /// <summary>
        ///     统计库存的时刻，结存时间
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime StatsTime { get; set; }

        /// <summary>
        ///     位置说明，对<see cref="StorageSpace" />的解释说明
        /// </summary>
        [BsonIgnoreIfNull]
        public string SpaceRemark { get; set; }
        /// <summary>
        ///     存储位置，如药房、柜子
        /// </summary>
        [BsonIgnoreIfNull]
        public string StorageSpace { get; set; }
    }
}

// Journal
namespace HealthCare.Data
{
    /// <summary>
    ///     MONGO日志
    /// </summary>
    public abstract partial class MongoJournal : SfraObject
    {
        /// <summary>
        ///     终端计算机IP
        /// </summary>
        public string Computer { get; set; }
    }

    /// <summary>
    ///     系统日志
    /// </summary>
    [Mongo(Collection = nameof(MongoJournal) + "." + nameof(AccessJournal), AllowSync = false)]
    [BsonDiscriminator(nameof(AccessJournal))]
    public partial class AccessJournal : MongoJournal
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
    }

    /// <summary>
    ///     操作日志记录，记录着所有动作
    /// </summary>
    /// <remarks>
    ///     动作为主线，为了什么业务，谁（执行人）操作了什么位置的物品
    /// </remarks>
    [Mongo(Collection = nameof(MongoJournal) + "." + nameof(ActionJournal), AllowSync = false)]
    [BsonDiscriminator(nameof(ActionJournal))]
    public partial class ActionJournal : MongoJournal
    {
        /// <summary>
        ///     主要操作人的登录 Id
        /// </summary>
        public string PrimaryUserId { get; set; }
        /// <summary>
        ///     主要操作人的显示姓名
        /// </summary>
        public string PrimaryUserName { get; set; }
        /// <summary>
        ///     监督人的登录Id
        /// </summary>
        public string SecondaryUserId { get; set; }
        /// <summary>
        ///     监督人的显示姓名
        /// </summary>
        public string SecondaryUserName { get; set; }
        /// <summary>
        ///     实际操作人的Id
        /// </summary>
        public string OperatorUserId { get; set; }
        /// <summary>
        ///     实际操作人的UserName
        /// </summary>
        public string OperatorUserName { get; set; }
        /// <summary>
        ///     关联的业务数据id
        /// </summary>
        public string TargetId { get; set; }
        /// <summary>
        ///     目标类别名，出入库[Exchange]; 调拨出入库[Allocation]; 医嘱取退[Prescription]; 预支退还[Medication];
        /// </summary>
        public string TargetType { get; set; }
        public string RecordType { get; set; }

        /// <summary>
        ///     节点No编号
        /// </summary>
        public string No { get; set; }
        public string GoodsId { get; set; }
        /// <summary>
        ///     [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        public Goods Goods { get; set; }
        public ExchangeMode Mode { get; set; }
        public double Qty { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime ExpiredDate { get; set; } = DateTime.MaxValue.Date;
    }

    /// <summary>
    ///     存储节点日志，即库存状态日志记录
    /// </summary>
    /// <remarks>
    ///     归档存储日志，<see cref="Storage" />的规格变化不影响已归档记录
    /// </remarks>
    [Mongo(Collection = nameof(MongoJournal) + "." + nameof(StorageJournal), AllowSync = false)]
    [BsonDiscriminator(nameof(StorageJournal))]
    public partial class StorageJournal : MongoJournal
    {
        /// <summary>
        ///     节点 No 编号
        /// </summary>
        public string No { get; set; }
        /// <summary>
        ///     存储的物品数据
        /// </summary>
        [BsonIgnoreIfNull]
        public List<NodeGoodsInfo> Fills { get; set; }
    }

}

// Person
namespace HealthCare.Data
{
    /// <summary>
    ///     自然人
    /// </summary>
    [BsonDiscriminator(nameof(Person))]
    public partial class Person : RetrievableObject
    {
        /// <summary>
        ///     年龄  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "年龄", Order = 0x1A)]
        public int? Age { get; set; }
        /// <summary>
        ///     出生日期    [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        [MongoMember(Display = "出生日期", Order = 0x1A)]
        public DateTime? Birthday { get; set; }
        /// <summary>
        ///     性别  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "性别", Order = 0x1B)]
        public string Gender { get; set; }
        /// <summary>
        ///     民族  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "民族", Order = 0x1C)]
        public string Nation { get; set; }
        /// <summary>
        ///     国籍  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "国籍", Order = 0x1C)]
        public string Nationality { get; set; }
        /// <summary>
        ///     证件号码    [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "证件号码", Order = 0x1D)]
        public string CertificateCode { get; set; }
        /// <summary>
        ///     证件类型    [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "证件类型", Order = 0x1D)]
        public string CertificateType { get; set; }
        /// <summary>
        ///     住址  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public string Address { get; set; }
        /// <summary>
        ///     联系电话    [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "联系电话", Order = 0x1D)]
        public string CellPhone { get; set; }
        /// <summary>
        ///     邮箱  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public string Email { get; set; }
        /// <summary>
        ///     邮编  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public string Post { get; set; }
    }

    /// <summary>
    ///     员工信息
    /// </summary>
    [Mongo(Order = 201, Display = "员工信息", Collection = nameof(Person) + "." + nameof(Employee))]
    [BsonDiscriminator(nameof(Employee))]
    public partial class Employee : Person
    {
        /// <summary>
        ///     部门
        /// </summary>
        [MongoMember(IsHidden = true)]
        public Department Department { get; set; }
        /// <summary>
        ///     部门 ID
        /// </summary>
        [MongoMember(Display = "员工所属科室", Description = "科室唯一标识", IsRequired = true, Order = 11)]
        public string DepartmentId { get; set; }
        /// <summary>
        ///     工号
        /// </summary>
        [MongoMember(Display = "工号", IsRequired = true, Order = 12)]
        public string JobNo { get; set; }
        /// <summary>
        ///     在职状态    [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public string JobState { get; set; }
        /// <summary>
        ///     职称
        /// </summary>
        [MongoMember(Display = "职称", IsRequired = true, Order = 13)]
        public string JobTitle { get; set; }
        /// <summary>
        ///     电子签名    [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "电子签名", Order = 14)]
        public string Signature { get; set; }

        public override string ToString() => $"{UniqueId},{DisplayName};{JobTitle},{JobNo},{DepartmentId}";
    }

    /// <summary>
    ///     患者信息（可包括门诊数据、住院数据、手术数据）
    /// </summary>
    [Mongo(Order = 401, Display = "患者信息", Collection = nameof(Person) + "." + nameof(Patient))]
    [BsonDiscriminator(nameof(Patient))]
    public partial class Patient : Person
    {
        /// <summary>
        ///     年龄特征. 如六个月, 一岁零两天
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "年龄特征", Description = "如六个月, 一岁零两天", Order = 0x1A)]
        public string AgeCharacter { get; set; }
        /// <summary>
        ///     患者门诊数据  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "门诊数据", Order = 11)]
        public Clinic Clinic { get; set; }
        /// <summary>
        ///     诊断
        /// </summary>
        [MongoMember(Display = "诊断", Order = 12)]
        public string Diagnostic { get; set; }
        /// <summary>
        ///     患者住院数据
        /// </summary>
        [MongoMember(Display = "住院数据", Order = 13)]
        public Hospitalization Hospitalization { get; set; } = new Hospitalization();
        /// <summary>
        ///     医保号、社保号 [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "医保号、社保号", Order = 14)]
        [BsonIgnoreIfNull]
        public string MedicareNumber { get; set; }
        /// <summary>
        ///     就诊卡号    [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "就诊卡号", Order = 15)]
        [BsonIgnoreIfNull]
        public string RegisterNumber { get; set; }
        /// <summary>
        ///     病历号    [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "病历号", Order = 16)]
        [BsonIgnoreIfNull]
        public string MedicalRecordNo { get; set; }
        public override string ToString() => $"{UniqueId},{DisplayName};{Clinic?.SerialNumber};{Hospitalization.AdmittedDepartmentId},{Hospitalization.HospitalNumber},{Hospitalization.BedNo},{Hospitalization?.ResidedAreaId},{Hospitalization.RoomId}";
    }

    /// <summary>
    ///     患者的门诊信息
    /// </summary>
    [BsonDiscriminator(nameof(Clinic))]
    public partial class Clinic : SfraObject
    {
        /// <summary>
        ///     门诊号
        /// </summary>
        [MongoMember(Display = "门诊号", Order = 11)]
        public string SerialNumber { get; set; }
    }

    /// <summary>
    ///     患者的住院信息
    /// </summary>
    /// <remarks>
    ///     解释：患者会有多个住院记录，而在我方系统，并未使用1:N关系，而是直接将住院记录与患者信息保存在一起
    /// </remarks>
    [BsonDiscriminator(nameof(Hospitalization))]
    public partial class Hospitalization : SfraObject
    {
        /// <summary>
        ///     患者入院科室
        /// </summary>
        [MongoMember(IsHidden = true)]
        public Department AdmittedDepartment { get; set; }
        /// <summary>
        ///     患者入院科室 Id
        /// </summary>
        [MongoMember(Display = "关联科室信息", Description = "患者入院科室", IsRequired = true, Order = 11)]
        public string AdmittedDepartmentId { get; set; }

        /// <summary>
        ///     住院号
        /// </summary>
        [MongoMember(Display = "住院号", Order = 13)]
        public string HospitalNumber { get; set; }
        /// <summary>
        ///     入院时间
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        [MongoMember(Display = "入院时间", Order = 12)]
        public DateTime InitiationTime { get; set; }
        /// <summary>
        ///     入院次数
        /// </summary>
        [MongoMember(Display = "入院次数", Order = 12)]
        public int CumulativeCount { get; set; }

        /// <summary>
        ///     所属病区
        /// </summary>
        [MongoMember(IsHidden = true)]
        public Department ResidedArea { get; set; }
        /// <summary>
        ///     所属病区唯一标识
        /// </summary>
        [MongoMember(Display = "关联病区信息", Description = "关联病区唯一标识", IsRequired = true, Order = 15)]
        public string ResidedAreaId { get; set; }
        /// <summary>
        ///     床号
        /// </summary>
        [MongoMember(IsRequired = true, Display = "床号", Order = 16)]
        public string BedNo { get; set; }
        /// <summary>
        ///     所属手术间   [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public Department Room { get; set; }
        /// <summary>
        ///     所属手术间唯一标识   [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "关联手术间信息", Description = "关联手术间唯一标识", IsRequired = true, Order = 17)]
        public string RoomId { get; set; }
    }
}

namespace HealthCare.Data
{
    /// <summary>
    ///     简单化数据对象，根据<see cref="Filter" />筛选数据类型
    /// </summary>
    [BsonDiscriminator(nameof(Simplified))]
    public partial class Simplified : RetrievableObject
    {
        /// <summary>
        ///     创建对象实例
        /// </summary>
        public Simplified() { }
        /// <summary>
        ///     创建对象实例
        /// </summary>
        protected Simplified(string filter) => Filter = filter;
        /// <summary>
        ///     数据过滤器
        /// </summary>
        [MongoMember(Display = "数据对象", IsReadOnly = false, Description = "Drug, MedicalConsume, OperatingCoat, Slipper", Order = 9)]
        public string Filter { get; set; }
    }

    /// <summary>
    ///     商品物品，如物品、耗材
    /// </summary>
    /// <remarks>
    ///     目前只有物品一个子类，考虑到如果扩展到手术衣高值耗材等，此时暂抽象出'物品'作为基类，但不一定合适或有意义。
    /// </remarks>
    [Mongo(Order = 202, Display = "商品物品", Collection = nameof(Goods))]
    [BsonDiscriminator(nameof(Goods))]
    public partial class Goods : Simplified
    {
        /// <summary>
        ///     小包装单位 如mg、ml、支 、片、个、贴、盒
        /// </summary>
        [MongoMember(Display = "小包装单位", IsRequired = true, Order = 11)]
        public string SmallPackageUnit { get; set; }
        /// <summary>
        ///     包装转换率  默认取值1，由<see cref="SmallPackageUnit" />到<see cref="UsedUnit" />转换倍数，如1盒=6支，此时计做6
        /// </summary>
        [MongoMember(Display = "包装转换率", Description = "转换倍数, 如1盒=6支, 此时计做6", IsRequired = true, Order = 12)]
        public double Conversion { get; set; } = 1;
        /// <summary>
        ///     最小用药单位，如mg、ml、支 、片、个、贴、盒
        /// </summary>
        [MongoMember(Display = "最小用药单位", IsRequired = true, Order = 13)]
        public string UsedUnit { get; set; }
        /// <summary>
        ///     规格，如30mg; 0.01g:1ml
        /// </summary>
        [MongoMember(Display = "规格", Order = 14)]
        public string Specification { get; set; }
        /// <summary>
        ///     生产厂家
        /// </summary>
        [MongoMember(Display = "生产厂家", Order = 15)]
        public string Manufacturer { get; set; }
        /// <summary>
        ///     耗材经销商, 根据经销商移除  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "耗材经销商", Order = 16)]
        public string Trader { get; set; }
        /// <summary>
        ///     单价
        /// </summary>
        [MongoMember(Display = "单价", Order = 17)]
        public double Price { get; set; } = double.NaN;
        /// <summary>
        ///     如麻、精一、精二、毒物、易制毒、耗材(手术衣、拖鞋)
        /// </summary>
        [MongoMember(Display = "类型", Description = "如麻、精一、精二、毒物、易制毒、耗材(手术衣、拖鞋)", Order = 18)]
        public string GoodsType { get; set; }
        /// <summary>
        ///     (单位)剂量 单味药的成人内服一日用量 [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "单位剂量", Description = "成人内服一日用量", Order = 19)]
        public string Dosage { get; set; }
        /// <summary>
        ///     剂型 为适应治疗或预防的需要而制备的药物应用形式    [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "剂型", Description = "药物应用形式", Order = 20)]
        public string DosageForm { get; set; }
        /// <summary>
        ///     麻醉药品空安瓿销毁
        /// </summary>
        [MongoMember(Display = "麻醉药品空安瓿销毁", Order = 21)]
        public bool IsAmpoule { get; set; }
        /// <summary>
        ///     物品通用名   [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "物品通用名", Description = "法定名称", Order = 2)]
        [BsonIgnoreIfNull]
        public string GenericName { get; set; }
        /// <summary>
        ///     物品商品名 [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "物品商品名", Description = "厂家名称", Order = 2)]
        [BsonIgnoreIfNull]
        public string TradeName { get; set; }

        /// <summary>
        ///     打印处方类型 红处方|白处方  [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public string PrescriptionType { get; set; }

        /// <summary>
        ///     标签打印模板
        /// </summary>
        [MongoMember(IsHidden = true)]
        [BsonIgnoreIfNull]
        public string LabelTemplate { get; set; }
        /// <summary>
        ///     医保报销方式，如甲（70%）-医保类，乙（30%）-医保类，自费等   [BsonIgnoreIfNull]
        /// </summary>
        [MongoMember(Display = "报销方式", Description = "如甲（70%）-医保类，乙（30%）-医保类，自费等", Order = 22)]
        [BsonIgnoreIfNull]
        public string Reimburse { get; set; }
        /// <summary>
        ///     是否是同步过来的物品。如 从HIS中同步。同步的物品，不允许修改。
        /// </summary>
        [MongoMember(IsHidden = true)]
        public bool IsSync { get; set; }
        /// <summary>
        ///     价格序号 (SDEY 计费时使用)
        /// </summary>
        [MongoMember(Display = "价格序号", Order = 17)]
        [BsonIgnoreIfNull]
        public string PriceSerialNumber { get; set; }
        public override string ToString() => $"{UniqueId},{DisplayName};{Specification},{Manufacturer},{GoodsType},{Filter};{SmallPackageUnit}={Conversion}*{UsedUnit}";
    }

    /// <summary>
    ///     物品分类 分组信息
    /// </summary>
    [Mongo(Collection = nameof(GoodsCategory), AllowSync = false)]
    [BsonDiscriminator(nameof(GoodsCategory))]
    public partial class GoodsCategory : Simplified
    {
        /// <summary>
        ///     创建对象实例
        /// </summary>
        public GoodsCategory() : base(nameof(GoodsCategory)) { }

        /// <summary>
        ///     客户编码
        /// </summary>
        public string CustomerId { get; set; }
        /// <summary>
        ///     分类所含的物品集合，集合项带有顺序含义 [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        public List<Goods> Goods { get; set; } = new List<Goods>();
        /// <summary>
        ///     物品<see cref="SfraObject.UniqueId" />集合
        /// </summary>
        [BsonIgnoreIfNull]
        public List<string> GoodsKeys { get; set; } = new List<string>();
        /// <summary>
        ///     取该物品时是否需要双人认证
        /// </summary>
        public bool IsDoubleCertify { get; set; }
        /// <summary>
        ///     SFRA 是否可以生成调拨单
        /// </summary>
        public bool PermitAlloc { get; set; }
        /// <summary>
        ///    红处方模板id
        /// </summary>
        public string TemplateId { get; set; }
        /// <summary>
        ///     模板
        /// </summary>
        public DesignerTemplate Template { get; set; }
        /// <summary>
        ///     背景色 - RGB，默认白色(透明色)
        /// </summary>
        public string Background { get; set; } = "#00FFFF";
        /// <summary>
        ///     前景色 - RGB，默认黑色
        /// </summary>
        public string Foreground { get; set; } = "#FF0000";
    }

    /// <summary>
    ///     套装信息,含有多个物品的套装
    /// </summary>
    [Mongo(Collection = nameof(Kit), AllowSync = false)]
    [BsonDiscriminator(nameof(Kit))]
    public partial class Kit : Simplified
    {
        public Kit() : base(nameof(Kit)) { }

        /// <summary>
        ///     该套装的创建人
        /// </summary>
        public string CreatorId { get; set; }
        public string CreatorName { get; set; }
        /// <summary>
        ///     套装内的物品集合
        /// </summary>
        public List<GoodsInGroup> Kits { get; set; } = new List<GoodsInGroup>();
    }

    /// <summary>
    ///     套装内的物品集合 或 估药规则内的物品集合
    /// </summary>
    [BsonDiscriminator(nameof(GoodsInGroup))]
    public partial class GoodsInGroup : SfraObject
    {
        /// <summary>
        ///     物品 [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        public Goods Goods { get; set; }
        /// <summary>
        ///     物品 ID
        /// </summary>
        public string GoodsId { get; set; }
        /// <summary>
        ///     物品的数量
        /// </summary>
        public double Qty { get; set; } = 1;
    }

    /// <summary>
    ///     角色，角色ID需要人工编码
    /// </summary>
    [Mongo(Collection = nameof(Role), AllowSync = false)]
    [BsonDiscriminator(nameof(Role))]
    public partial class Role : Simplified
    {
        public Role() : base(nameof(Role)) { }

        /// <summary>
        ///     角色内成员用户的ID集合
        /// </summary>
        public List<string> Users { get; set; } = new List<string>();
        /// <summary>
        ///     角色所包含的功能页面ID集合
        /// </summary>
        public List<string> Menus { get; set; } = new List<string>();
        /// <summary>
        ///     角色的默认页（拥有该角色的人员，登录之后的默认功能页）
        /// </summary>
        [BsonIgnoreIfNull]
        public string DefaultMenu { get; set; }
        /// <summary>
        ///     允许使用的存储位置。格式：ip:no
        ///     每个节点编码全院唯一，例如药柜可控设备采用14位16进制编码 FF(药柜) FFFF(抽屉) FFFF(药盒) FFFF(针剂) 等4层级
        /// </summary>
        public List<string> AvailableStorages { get; set; } = new List<string>();
        /// <summary>
        ///     数据授权集合，采用硬编码方式，数据项ID集合
        /// </summary>
        public List<string> DataPermissions { get; set; } = new List<string>();
        /// <summary>
        ///     父级角色编号
        /// </summary>
        [BsonIgnoreIfNull]
        public string ParentId { get; set; }
    }

    /// <summary>
    ///     功能菜单，功能ID使用固定GUID
    /// </summary>
    [Mongo(Collection = nameof(Menu), AllowSync = false)]
    [BsonDiscriminator(nameof(Menu))]
    public partial class Menu : Simplified
    {
        public Menu() : base(nameof(Menu)) { }

        /// <summary>
        ///     功能页面地址，类型全名或
        /// </summary>
        public string Uri { get; set; }
        /// <summary>
        ///     font awesome 图标
        /// </summary>
        public string Icon { get; set; }
        /// <summary>
        ///     是否是模块（模块或功能）
        /// </summary>
        public bool IsModule { get; set; }
        /// <summary>
        ///     根模块 ID
        /// </summary>
        [BsonIgnoreIfNull]
        public string ParentId { get; set; }

        public override string ToString() => $"{DisplayName} {Uri}";
    }

    /// <summary>
    ///     部门、科室、手术室、病区等信息
    /// </summary>
    [Mongo(Order = 101, Display = "部门信息", Collection = nameof(Department))]
    [BsonDiscriminator(nameof(Department))]
    public partial class Department : Simplified
    {
        public Department() : base(nameof(Department))
        {

        }
        public override string ToString() => $"{UniqueId},{DisplayName}";
    }

    /// <summary>
    ///     物品清点记录
    /// </summary>
    [Mongo(Collection = nameof(Transfer), AllowSync = false)]
    [BsonDiscriminator(nameof(Transfer))]
    public partial class Transfer : Simplified
    {
        /// <summary>
        ///     终端 IP 地址
        /// </summary>
        public string Computer { get; set; }
        /// <summary>
        ///     执行清点人
        /// </summary>
        [BsonIgnoreIfNull]
        public string Executor { get; set; }
        /// <summary>
        ///     登录人
        /// </summary>
        [BsonIgnoreIfNull]
        public string Login { get; set; }
        /// <summary>
        ///     清点集合信息
        /// </summary>
        [BsonIgnoreIfNull]
        public List<TransferRecord> TransferRecords { get; set; }

        /// <summary>
        ///     清点记录
        /// </summary>
        [BsonDiscriminator(nameof(TransferRecord))]
        public partial class TransferRecord : SfraObject
        {
            /// <summary>
            ///     位置
            /// </summary>
            public string No { get; set; }
            public Goods Goods { get; set; }
            public string GoodsId { get; set; }
            /// <summary>
            ///     预计数量
            /// </summary>
            public double EstimateQty { get; set; }
            /// <summary>
            ///     实际数量
            /// </summary>
            public double ActualQty { get; set; }
            public string BatchNumber { get; set; } = string.Empty;
            [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
            public DateTime ExpiredDate { get; set; } = DateTime.MaxValue.Date;
        }
    }

    /// <summary>
    ///     估药规则
    /// </summary>
    /// <remarks>
    ///     设置关键字和药品的关联, 以后按照该规则来快速预支药品. 通常在患者的诊断信息中匹配关键字
    /// </remarks>
    [Mongo(Collection = nameof(Evaluate), AllowSync = false)]
    [BsonDiscriminator(nameof(Evaluate))]
    public partial class Evaluate : Simplified
    {
        /// <summary>
        ///     激活状态需要按照关键字分配药品
        /// </summary>
        public bool IsActived { get; set; }
        /// <summary>
        ///     关键词, 按照半角逗号分隔
        /// </summary>
        public string Keywords { get; set; }
        /// <summary>
        ///     估药规则内的物品集合
        /// </summary>
        public List<GoodsInGroup> Evaluates { get; set; } = new List<GoodsInGroup>();
    }
}

namespace HealthCare.Data
{
    /// <summary>
    ///     系统用户，不针对用户授权，所有权限均来自所属角色
    /// </summary>
    [Mongo(Collection = nameof(User), AllowSync = false)]
    [BsonDiscriminator(nameof(User))]
    public partial class User : MongoSyncable
    {
        /// <summary>
        ///     登录名
        /// </summary>
        public string LoginId { get; set; }
        /// <summary>
        ///     员工信息
        /// </summary>
        [BsonIgnoreIfNull]
        public Employee Employee { get; set; }
        /// <summary>
        ///     是否允许密码登录认证
        /// </summary>
        public bool CanPasswordAuth { get; set; } = true;
        /// <summary>
        ///     登录密码，非明文存储，采用 MD5 存储，密码不具有恢复功能，只提供重置
        /// </summary>
        public string Password { get; set; }
        /// <summary>
        ///     是否允许打卡认证
        /// </summary>
        public bool CanCardAuth { get; set; }
        /// <summary>
        ///     员工卡号
        /// </summary>
        [BsonIgnoreIfNull]
        public string CardNumber { get; set; }
        /// <summary>
        ///     是否允许虹膜认证
        /// </summary>
        public bool CanIrisAuth { get; set; }
        /// <summary>
        ///     虹膜特征，长度为 2，每一项代表一只眼睛，每只眼睛最多允许保存 3 个 base64 编码的模板（以';'分割）
        ///     顺序自定义
        /// </summary>
        public string[] Iris { get; set; } = Enumerable.Range(0, 2).Select(o => string.Empty).ToArray();
        /// <summary>
        ///     是否允许指纹认证
        /// </summary>
        public bool CanPrintfingerAuth { get; set; }
        /// <summary>
        ///     指纹特征，长度为 10，每一项代表一个手指，每个手指最多允许保存 3 个 base64 编码的模板（以';'分割）
        ///     顺序自定义
        /// </summary>
        public string[] Fingerprint { get; set; } = Enumerable.Range(0, 10).Select(o => string.Empty).ToArray();
        /// <summary>
        ///     是否允许人脸认证
        /// </summary>
        public bool CanFaceAuth { get; set; }
        /// <summary>
        ///     人脸特征，最多允许保存 3 个 base64 编码的模板（以';'分割）
        /// </summary>
        public string Face { get; set; } = string.Empty;
        /// <summary>
        ///     自动过期时间，即有效期内允许登录，过期后自动拒绝登录
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime? AutoExpiredTime { get; set; }
        /// <summary>
        ///     是否未过期，即未超出<see cref="AutoExpiredTime" />期限
        /// </summary>
        public bool IsNotExpired => DateTime.Now <= AutoExpiredTime;

        /// <summary>
        ///     所属角色ID集合
        /// </summary>
        public List<string> Roles { get; set; } = new List<string>();
        /// <summary>
        ///     已经授权访问的功能页面集合
        /// </summary>
        public List<string> Menus { get; set; } = new List<string>();
        /// <summary>
        ///     允许使用的存储位置。格式：ip:no
        ///     每个节点编码全院唯一，例如药柜可控设备采用 14 位 16 进制编码 FF(药柜) FFFF(抽屉) FFFF(药盒) FFFF(针剂) 等4层级
        /// </summary>
        public List<string> AvailableStorages { get; set; } = new List<string>();
        /// <summary>
        ///     数据授权集合，采用硬编码方式，数据项ID集合
        /// </summary>
        public List<string> DataPermissions { get; set; } = new List<string>();

        public override string ToString() => $"{LoginId} {Employee?.DisplayName}";
    }

    /// <summary>
    ///     系统参数
    /// </summary>
    [Mongo(Collection = nameof(SystemConfig), AllowSync = false)]
    [BsonDiscriminator(nameof(SystemConfig))]
    public partial class SystemConfig : SfraObject
    {
        public string Key { get; set; }
        /// <summary>
        ///     JSON对象
        /// </summary>
        public string JObject { get; set; }

        public override string ToString() => $"{Key} {JObject}";
    }

    /// <summary>
    ///     终端物品
    /// </summary>
    [Mongo(Collection = nameof(TerminalGoods), AllowSync = false)]
    [BsonDiscriminator(nameof(TerminalGoods))]
    public partial class TerminalGoods : SfraObject
    {
        /// <summary>
        ///     终端的IP地址
        /// </summary>
        public string Computer { get; set; }
        /// <summary>
        ///     Goods
        /// </summary>
        public Goods Goods { get; set; }
        /// <summary>
        ///     GoodsId
        /// </summary>
        public string GoodsId { get; set; }

        /// <summary>
        ///     最大存储基数
        /// </summary>
        public double StorageQuota { get; set; } = double.NaN;
        /// <summary>
        ///     预警基数
        /// </summary>
        public double WarningQuota { get; set; } = double.NaN;
        /// <summary>
        ///     当前存储数量 (无效冗余字段, 数据库并未存储药品当前数据)
        /// </summary>
        public double CurrentQuota { get; set; } = double.NaN;
        /// <summary>
        ///     前景色-ARGB，默认黑色
        /// </summary>
        public string Foreground { get; set; } = "#000000";
        /// <summary>
        ///     背景色-ARGB，默认白色(透明色)
        /// </summary>
        public string Background { get; set; } = "#FFFFFF";

        public override string ToString() => $"{Computer} {GoodsId}\t{StorageQuota} {WarningQuota}";
    }

    /// <summary>
    ///     调拨验收标准
    /// </summary>
    [BsonDiscriminator(nameof(AcceptanceStandard))]
    public partial class AcceptanceStandard : SfraObject
    {
        /// <summary>
        ///     默认值
        /// </summary>
        public bool? DefaultValue { get; set; }
        /// <summary>
        ///     显示内容
        /// </summary>
        public string DisplayContent { get; set; }
        /// <summary>
        ///     是否启用
        /// </summary>
        public bool IsEnabled { get; set; }
        /// <summary>
        ///     合格值
        /// </summary>
        public bool? QualifiedValue { get; set; }
    }

    /// <summary>
    ///     手术排班信息
    /// </summary>
    [Mongo(Order = 402, Display = "手术排班", Collection = nameof(OperationSchedule))]
    [BsonDiscriminator(nameof(OperationSchedule))]
    public partial class OperationSchedule : MongoSyncable
    {
        [MongoMember(IsHidden = true)]
        public Patient Patient { get; set; }
        /// <summary>
        ///     患者
        /// </summary>
        [MongoMember(Display = "患者", IsRequired = true, Order = 11)]
        public string PatientId { get; set; }
        /// <summary>
        ///     手术申请时间
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        [MongoMember(Display = "手术申请时间", IsRequired = true, Order = 12)]
        public DateTime ApplyTime { get; set; }
        /// <summary>
        ///     手术等级
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public string OperationLevel { get; set; }
        /// <summary>
        ///     手术类别，择期、急诊
        /// </summary>
        [MongoMember(Display = "手术类别", Description = "择期、急诊", Order = 14)]
        public string OperationType { get; set; }
        /// <summary>
        ///     手术状态，术前、术中、术后、回病房等
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "手术状态", Description = "术前、术中、术后、回病房等", Order = 13)]
        public string OperationState { get; set; }
        /// <summary>
        ///     患者身份条码 通常为 进手术室时, 扫描患者手腕的条码确认患者信息
        /// </summary>
        [MongoMember(Display = "患者身份条码", Order = 14)]
        public string IdentityBarcode { get; set; }
        /// <summary>
        ///     手术室
        /// </summary>
        [MongoMember(Display = "手术室", IsRequired = true, Order = 15)]
        public string RoomId { get; set; }
        /// <summary>
        ///     台次（手术不能一次完成，分成几个不同的时间做手术，每一次都是一个台次）
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public string StageTime { get; set; }

        /// <summary>
        ///     手术开始时间
        /// </summary>
        [BsonIgnoreIfNull]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        [MongoMember(Display = "手术开始时间", Order = 16)]
        public DateTime? ExecutionBeginTime { get; set; }
        /// <summary>
        ///     麻醉方式，局麻、全麻
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "麻醉方式", Description = "局麻、全麻", Order = 20)]
        public string AnesthesiaMode { get; set; }
        /// <summary>
        ///     麻醉医生
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public Employee Anesthetist { get; set; }
        /// <summary>
        ///     麻醉医生
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "麻醉医生", Order = 20)]
        public string AnesthetistId { get; set; }
        /// <summary>
        ///     主刀医生    [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public Employee PrimaryDoctor { get; set; }
        /// <summary>
        ///     主刀医生
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "主刀医生", Order = 21)]
        public string PrimaryDoctorId { get; set; }
        /// <summary>
        ///     第一助手    [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public Employee PrimaryAssistant { get; set; }
        /// <summary>
        ///     第一助手
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "第一助手", Order = 22)]
        public string PrimaryAssistantId { get; set; }
        /// <summary>
        ///     第一洗手护士  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public Employee PrimaryHandwashingNurse { get; set; }
        /// <summary>
        ///     第一洗手护士
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "第一洗手护士", Order = 23)]
        public string PrimaryHandwashingNurseId { get; set; }
        /// <summary>
        ///     第一巡回护士  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public Employee PrimaryTourNurse { get; set; }
        /// <summary>
        ///     第一巡回护士
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "第一巡回护士", Order = 24)]
        public string PrimaryTourNurseId { get; set; }
        /// <summary>
        ///     第二助手    [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public Employee SecondaryAssistant { get; set; }
        /// <summary>
        ///     第二助手
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "第二助手", Order = 25)]
        public string SecondaryAssistantId { get; set; }
        /// <summary>
        ///     第二洗手护士  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public Employee SecondaryHandwashingNurse { get; set; }
        /// <summary>
        ///     第二洗手护士
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "第二洗手护士", Order = 26)]
        public string SecondaryHandwashingNurseId { get; set; }
        /// <summary>
        ///     第二巡回护士  [BsonIgnoreIfNull]
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(IsHidden = true)]
        public Employee SecondaryTourNurse { get; set; }
        /// <summary>
        ///     第二巡回护士
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "第二巡回护士", Order = 27)]
        public string SecondaryTourNurseId { get; set; }
        /// <summary>
        ///     手术结束时间
        /// </summary>
        [BsonIgnoreIfNull]
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        [MongoMember(Display = "手术结束时间", IsHidden = true, Order = 17)]
        public DateTime? ExecutionEndTime { get; set; }

        /// <summary>
        ///     手术是否被取消
        /// </summary>
        [MongoMember(Display = "手术是否被取消", IsRequired = true, Order = 18)]
        public bool IsCancelled { get; set; }
        /// <summary>
        ///     备注
        /// </summary>
        [BsonIgnoreIfNull]
        [MongoMember(Display = "手术是否被取消的判断依据", IsRequired = true, Order = 19)]
        public string Remark { get; set; }

        public override string ToString() => $"{PatientId},{RoomId},{OperationType};{OperationState};{Remark}";
    }

    /// <summary>
    ///     [ mongo = <see cref="Ampoule" /> ]安瓿回收信息
    /// </summary>
    [Mongo(Collection = nameof(Ampoule), AllowSync = false)]
    [BsonDiscriminator(nameof(Ampoule))]
    public partial class Ampoule : MongoSyncable
    {
        /// <summary>
        ///     终端IP
        /// </summary>
        public string Computer { get; set; }
        /// <summary>
        ///     退回科室 [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        public Department Department { get; set; }
        /// <summary>
        ///     归还部门/科室
        /// </summary>
        public string DepartmentId { get; set; }
        /// <summary>
        ///     物品对象 [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        public Goods Goods { get; set; }
        /// <summary>
        ///     物品唯一标识
        /// </summary>
        public string GoodsId { get; set; }
        /// <summary>
        ///     批号
        /// </summary>
        public string BatchNumber { get; set; } = string.Empty;
        /// <summary>
        ///     过期时间
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime ExpiredDate { get; set; } = DateTime.MaxValue.Date;

        /// <summary>
        ///     应回收数量
        /// </summary>
        public double ExpectedQty { get; set; }
        /// <summary>
        ///     归还数量
        /// </summary>
        public double ActualQty { get; set; }
        /// <summary>
        ///     已完成销毁
        /// </summary>
        public bool FinishedDestory { get; set; }
        /// <summary>
        ///     归还空安瓿的人
        /// </summary>
        public string RepaidPerson { get; set; }
        /// <summary>
        ///     接收空安瓿的人
        /// </summary>
        public string ReceivePerson { get; set; }
    }

    /// <summary>
    ///     空安瓿废贴销毁
    /// </summary>
    [Mongo(Collection = nameof(Destory), AllowSync = false)]
    [BsonDiscriminator(nameof(Destory))]
    public partial class Destory : SfraObject
    {
        /// <summary>
        ///     销毁地点
        /// </summary>
        public string Address { get; set; }
        /// <summary>
        ///     销毁的回收记录的ID集合
        /// </summary>
        public List<string> Ampoules { get; set; } = new List<string>();
        /// <summary>
        ///     批号
        /// </summary>
        public string BatchNumber { get; set; } = string.Empty;
        /// <summary>
        ///     过期时间
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime ExpiredDate { get; set; } = DateTime.MaxValue.Date;
        /// <summary>
        ///     终端IP
        /// </summary>
        public string Computer { get; set; }
        /// <summary>
        ///     销毁数量
        /// </summary>
        public double DestoryQty { get; set; }
        /// <summary>
        ///     销毁方式
        /// </summary>
        public string DestroyMode { get; set; }
        /// <summary>
        ///     执行人的 ID
        /// </summary>
        [BsonIgnoreIfNull]
        public string ExecutorId { get; set; }
        /// <summary>
        ///     执行人的名字
        /// </summary>
        public string ExecutorName { get; set; }
        /// <summary>
        ///     物品对象 [BsonIgnore]
        /// </summary>
        [BsonIgnore]
        public Goods Goods { get; set; }
        /// <summary>
        ///     物品标识 ID
        /// </summary>
        public string GoodsId { get; set; }
        /// <summary>
        ///     监督人的 ID
        /// </summary>
        public string SupervisorId { get; set; }
        /// <summary>
        ///     监督人的名字
        /// </summary>
        public string SupervisorName { get; set; }
    }
}

namespace HealthCare.Data
{
    /// <summary>
    ///     设计器保存的模板。如红处方打印模板，条码
    /// </summary>
    [Mongo(Collection = nameof(DesignerTemplate), AllowSync = false)]
    [BsonDiscriminator(nameof(DesignerTemplate))]
    public partial class DesignerTemplate : MongoSyncable
    {
        /// <summary>
        ///     模板，记录所有的版本，数组下标即版本号
        /// </summary>
        public List<RenderDetail> RenderDetails { get; set; } = new List<RenderDetail>();
    }

    [BsonDiscriminator(nameof(RenderDetail))]
    public partial class RenderDetail
    {
        public double Height { get; set; }
        public double Width { get; set; }
        public string Rendering { get; set; }
    }
}

// Attribute
namespace HealthCare.Data
{
    /// <summary>
    ///     MONGO类型特性
    /// </summary>
    public partial class MongoAttribute : Attribute
    {
        /// <summary>
        ///     允许使用数据同步服务
        /// </summary>
        public bool AllowSync { get; set; } = true;
        /// <summary>
        ///     mongo集合名称
        /// </summary>
        public string Collection { get; set; }
        /// <summary>
        ///     显示名称
        /// </summary>
        public string Display { get; set; }
        /// <summary>
        ///     显示顺序
        /// </summary>
        public int Order { get; set; } = -1;
    }

    /// <summary>
    ///     MONGO成员特性
    /// </summary>
    public partial class MongoMemberAttribute : Attribute
    {
        /// <summary>
        ///     详细描述
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        ///     显示名
        /// </summary>
        public string Display { get; set; }
        /// <summary>
        ///     隐藏字段，即不同步该字段数据
        /// </summary>
        public bool IsHidden { get; set; }
        /// <summary>
        ///     是否只读
        /// </summary>
        public bool IsReadOnly { get; set; }
        /// <summary>
        ///     是否必填
        /// </summary>
        public bool IsRequired { get; set; }
        /// <summary>
        ///     显示顺序
        /// </summary>
        public int Order { get; set; }
    }
}