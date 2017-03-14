﻿using System;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using vm.Aspects.Model.EFRepository;
using vm.Aspects.Model.EFRepository.HiLoIdentity;
using vm.Aspects.Model.Repository;

namespace vm.Aspects.Model.PerCallContextRepositoryCallHandlerTests
{
    public partial class Repository : EFRepositoryBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModelRepository" /> class.
        /// Required by the migrations only.
        /// </summary>
        public Repository()
            : base("TestPCCRCHT")
        {
#if DEBUG
            Database.Log = s => Debug.Write(s);
#endif
        }

        /// <summary>
        /// Initializes the repository.
        /// </summary>
        /// <returns>vm.Aspects.Model.Repository.IRepository.</returns>
        public override IRepository Initialize()
        {
            if (IsInitialized)
                return this;

            base.Initialize();

            Debug.WriteLine(
                "The {0} v{1} was initialized successfully and has {2} entities and {3} values.",
                GetType().Name,
                Assembly.GetAssembly(GetType()).GetCustomAttribute<AssemblyFileVersionAttribute>().Version,
                Entities<Entity>().Count(),
                Values<Value>().Count());

            return this;
        }

        protected override void OnModelCreating(
            DbModelBuilder modelBuilder)
        {
            if (modelBuilder == null)
                throw new ArgumentNullException(nameof(modelBuilder));

            base.OnModelCreating(modelBuilder);

            modelBuilder
                .Configurations

                // TODO TRY THIS: .AddFromAssembly(Assembly.GetAssembly(GetType()))

                .Add(new EntityMap())
                .Add(new ValueMap())
                ;
        }
    }
}
