//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System.Configuration;
using System.Reflection;

#pragma warning disable 1591
// ReSharper disable once CheckNamespace
namespace HealthCare.Data
{
    public sealed class MongoContext
    {
        static MongoContext()
        {
            ConventionRegistry.Register("camelCase", new ConventionPack { new CamelCaseElementNameConvention(), }, t => true);
            Database = new MongoClient(ConfigurationManager.AppSettings["sframed:Connection"]).GetDatabase(ConfigurationManager.AppSettings["sframed:Database"]);
        }

        public IMongoCollection<Customer> CustomerCollection => Database.GetCollection<Customer>(CollectionName<Customer>());

        public IMongoCollection<Exchange> ExchangeCollection => Database.GetCollection<Exchange>(CollectionName<Exchange>());
        public IMongoCollection<Allocation> AllocationCollection => Database.GetCollection<Allocation>(CollectionName<Allocation>());
        public IMongoCollection<Prescription> PrescriptionCollection => Database.GetCollection<Prescription>(CollectionName<Prescription>());
        public IMongoCollection<Medication> MedicationCollection => Database.GetCollection<Medication>(CollectionName<Medication>());
        public IMongoCollection<InternalAllocation> InternalAllocationCollection => Database.GetCollection<InternalAllocation>(CollectionName<InternalAllocation>());
        public IMongoCollection<Inventory> InventoryCollection => Database.GetCollection<Inventory>(CollectionName<Inventory>());

        public IMongoCollection<AccessJournal> AccessJournalCollection => Database.GetCollection<AccessJournal>(CollectionName<AccessJournal>());
        public IMongoCollection<ActionJournal> ActionJournalCollection => Database.GetCollection<ActionJournal>(CollectionName<ActionJournal>());
        public IMongoCollection<StorageJournal> StorageJournalCollection => Database.GetCollection<StorageJournal>(CollectionName<StorageJournal>());

        public IMongoCollection<Employee> EmployeeCollection => Database.GetCollection<Employee>(CollectionName<Employee>());
        public IMongoCollection<Patient> PatientCollection => Database.GetCollection<Patient>(CollectionName<Patient>());

        public IMongoCollection<Goods> GoodsCollection => Database.GetCollection<Goods>(CollectionName<Goods>());
        public IMongoCollection<GoodsCategory> GoodsCategoryCollection => Database.GetCollection<GoodsCategory>(CollectionName<GoodsCategory>());
        public IMongoCollection<Kit> KitCollection => Database.GetCollection<Kit>(CollectionName<Kit>());
        public IMongoCollection<Role> RoleCollection => Database.GetCollection<Role>(CollectionName<Role>());
        public IMongoCollection<Menu> MenuCollection => Database.GetCollection<Menu>(CollectionName<Menu>());
        public IMongoCollection<Department> DepartmentCollection => Database.GetCollection<Department>(CollectionName<Department>());
        public IMongoCollection<Transfer> TransferCollection => Database.GetCollection<Transfer>(CollectionName<Transfer>());
        public IMongoCollection<Evaluate> EvaluateCollection => Database.GetCollection<Evaluate>(CollectionName<Evaluate>());

        public IMongoCollection<User> UserCollection => Database.GetCollection<User>(CollectionName<User>());
        public IMongoCollection<SystemConfig> SystemConfigCollection => Database.GetCollection<SystemConfig>(CollectionName<SystemConfig>());
        public IMongoCollection<TerminalGoods> TerminalGoodsCollection => Database.GetCollection<TerminalGoods>(CollectionName<TerminalGoods>());
        public IMongoCollection<OperationSchedule> OperationScheduleCollection => Database.GetCollection<OperationSchedule>(CollectionName<OperationSchedule>());
        public IMongoCollection<Ampoule> AmpouleCollection => Database.GetCollection<Ampoule>(CollectionName<Ampoule>());
        public IMongoCollection<Destory> DestoryCollection => Database.GetCollection<Destory>(CollectionName<Destory>());
        public IMongoCollection<DesignerTemplate> DesignerTemplateCollection => Database.GetCollection<DesignerTemplate>(CollectionName<DesignerTemplate>());
        internal static IMongoDatabase Database { get; }
        private static string CollectionName<T>() => typeof(T).GetTypeInfo().GetCustomAttribute<MongoAttribute>(false).Collection;
        internal IMongoCollection<T> AnyCollection<T>(string collectionName) => Database.GetCollection<T>(collectionName);
    }
}