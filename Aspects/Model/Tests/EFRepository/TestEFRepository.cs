﻿using System;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using vm.Aspects.Facilities;
using vm.Aspects.Model.Repository;
using vm.Aspects.Model.Tests;

//[assembly: DbMappingViewCacheType(typeof(TestEFRepository), typeof(EFRepositoryMappingViewCache<TestEFRepository>))]

namespace vm.Aspects.Model.EFRepository.Tests
{
    public partial class TestEFRepository : EFRepositoryBase
    {
        public long GetStoreId<T>() where T : IHasStoreId<long> => GetStoreId<T, long>();

        #region Constructors
        public TestEFRepository()
        {
        }

        /// <summary>
        /// Initializes a new instance of the repository with a connection string and by default will use the HiLo PK generator.
        /// </summary>
        /// <param name="connectionString">
        /// The DB connection string.
        /// </param>
        public TestEFRepository(
            string connectionString)
            : base(connectionString)
        {
            if (connectionString.IsNullOrWhiteSpace())
                throw new ArgumentException("The argument cannot be null, empty string or consist of whitespace characters only.", nameof(connectionString));
        }

        /// <summary>
        /// Initializes a new instance of the repository with a connection string and by default will use the HiLo PK generator.
        /// </summary>
        /// <param name="connectionString">
        /// The DB connection string.
        /// </param>
        public TestEFRepository(
            string connectionString,
            IStoreIdProvider storeIdProvider = null)
            : base(connectionString, storeIdProvider)
        {
            if (connectionString.IsNullOrWhiteSpace())
                throw new ArgumentException("The argument cannot be null, empty string or consist of whitespace characters only.", nameof(connectionString));
        }
        #endregion

        protected override void PreprocessEntries()
        {
            Lazy<DateTime> now = new Lazy<DateTime>(() => Facility.Clock.UtcNow);

            ObjectContext.ObjectStateManager
                         .GetObjectStateEntries(System.Data.Entity.EntityState.Modified |
                                                System.Data.Entity.EntityState.Unchanged)
                         .Where(e => e.Entity is BaseDomainEntity &&
                                     (e.State == System.Data.Entity.EntityState.Modified || e.GetModifiedProperties().Any()))
                         .Select(e =>
                             {
                                 if (e.Entity is TestXEntity xe)
                                 {
                                     xe.SetUpdated(now.Value);
                                     return e;
                                 }

                                 if (e.Entity is TestEntity te)
                                 {
                                     te.SetUpdated(now.Value);
                                     return e;
                                 }

                                 return e;
                             })
                         .Count();
        }

        protected override void OnModelCreating(
            DbModelBuilder modelBuilder)
        {
            if (modelBuilder == null)
                throw new ArgumentNullException(nameof(modelBuilder));

            base.OnModelCreating(modelBuilder);

            modelBuilder.Configurations
                        .Add(new TestValueConfiguration())
                        .Add(new TestXEntityConfiguration())
                        .Add(new TestEntityConfiguration())
                        .Add(new TestEntity1Configuration())
                        .Add(new TestEntity2Configuration())
                        ;
        }

        void EnsureChangeTrackingProxyEntities()
        {
            if (!IsChangeTrackingProxy<TestXEntity>(e => { e.Name = "TestXEntity.IsChangeTrackingProxy"; }) |
                !IsChangeTrackingProxy<TestEntity>(e => { e.Name = "TestEntity.IsChangeTrackingProxy"; })  |
                !IsChangeTrackingProxy<TestEntity1>(e => { e.Name = "TestEntity1.IsChangeTrackingProxy"; }) |
                !IsChangeTrackingProxy<TestEntity2>(e => { e.Name = "TestEntity2.IsChangeTrackingProxy"; }) |
                !IsChangeTrackingProxy<TestValue>(v => { v.Name = "TestValue.IsChangeTrackingProxy"; }))
                Debug.WriteLine(
@"Requirements for a type to be change tracking proxy:
    1. The class must be public and not sealed.
    2. Each mapped property of the class must be virtual.
    3. Each mapped property of the class must have a public getter and setter.
    3. Any mapped collection navigation property of the class must be typed as ICollection<T>.");
        }

        public override IRepository Initialize(Action query)
            => base.Initialize(query ?? InitializationQuery);

        void InitializationQuery()
        {
            EnsureChangeTrackingProxyEntities();
            Facility.LogWriter.EventLogInfo(
                "The {0} v{1} was initialized successfully and has {2} test entities.",
                GetType().Name,
                Assembly.GetAssembly(GetType()).GetCustomAttribute<AssemblyFileVersionAttribute>().Version,
                Entities<TestEntity>().Count());
        }
    }
}
