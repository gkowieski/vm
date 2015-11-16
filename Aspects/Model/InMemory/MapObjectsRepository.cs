﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vm.Aspects.Exceptions;
using vm.Aspects.Model.Repository;
using vm.Aspects.Threading;

namespace vm.Aspects.Model.InMemory
{
    /// <summary>
    /// Class MapObjectsRepository is in-memory repository of <see cref="T:DomainEntity{long, string}"/> objects. 
    /// The internal store is a <see cref="T:IDictionary{type, List{BaseEntity}}"/> map where the entities are placed in lists mapped to their types.
    /// The entities of related types (base and derived) are kept in a common list.
    /// </summary>
    public sealed partial class MapObjectsRepository : IRepositoryAsync
    {
        /// <summary>
        /// Synchronizes multi-threaded access to the underlying objects container
        /// </summary>
        static ReaderWriterLockSlim _sync = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        /// <summary>
        /// Set to <see langword="true"/> if the underlying global structures are initialized.
        /// </summary>
        static bool _isInitialized;
        /// <summary>
        /// The objects container.
        /// </summary>
        internal static Dictionary<Type, List<DomainEntity<long, string>>> _entities;
        /// <summary>
        /// The store ID generator.
        /// </summary>
        static long _longId;

        /// <summary>
        /// The repository unique instance identifier
        /// </summary>
        readonly Guid _instanceId = Guid.NewGuid();

        /// <summary>
        /// Gets the instance identifier.
        /// </summary>
        public Guid InstanceId => _instanceId;

        static void UnsafeReset()
        {
            _entities      = new Dictionary<Type, List<DomainEntity<long, string>>>();
            _longId        = 0L;
            _isInitialized = true;
        }

        /// <summary>
        /// Resets the underlying global structures
        /// </summary>
        public static void Reset()
        {
            using (_sync.WriterLock())
                UnsafeReset();
        }

        /// <summary>
        /// Gets the entity list corresponding to the <typeparamref name="T"/>.
        /// If the map does not have an entry for the concrete type the method searches for base type entry and
        /// if still not found then it searches for a derived type entries. And if still no entry is found a new entry will be added with
        /// key the <typeparamref name="T" /> and value a new empty <see cref="T:List{BaseEntity}" /> which will also be the returned value.
        /// If an entry for base or derived type is found, a new entry will be added with key the <typeparamref name="T" /> and value - the list from
        /// the found entry which will be the returned value. This ensures that the types from the same inheritance sub-tree map to a single list of entities.
        /// The method is not type safe and is only invoked from within write-synchronized context.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <returns>List{BaseEntity}.</returns>
        static List<DomainEntity<long, string>> GetEntityList<T>()
        {
            Contract.Requires<InvalidOperationException>(typeof(DomainEntity<long, string>).IsAssignableFrom(typeof(T)), "The repository does not support this type.");
            Contract.Ensures(Contract.Result<List<DomainEntity<long, string>>>() != null);

            var type = typeof(T);
            List<DomainEntity<long, string>> list;

            if (_entities.TryGetValue(type, out list))
                return list;

            // search for base or derived type entry
            var typeList = _entities.FirstOrDefault(kv => kv.Key.IsAssignableFrom(type) ||
                                                          type.IsAssignableFrom(kv.Key));

            if (typeList.Key != null)
                return typeList.Value;
            else
                return _entities[type] = new List<DomainEntity<long, string>>();
        }

        #region IRepository Members

        /// <summary>
        /// Gets a value indicating whether the instance implementing the interface is initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Initializes the repository.
        /// </summary>
        /// <returns>IRepository.</returns>
        public IRepository Initialize()
        {
            if (IsInitialized)
                return this;

            using (_sync.WriterLock())
            {
                if (IsInitialized)
                    return this;

                UnsafeReset();
            }

            return this;
        }

        /// <summary>
        /// Gets a unique store id for the specified type of objects.
        /// </summary>
        /// <typeparam name="T">The type of object for which to get a unique ID.</typeparam>
        /// <typeparam name="TId">The type of the store identifier.</typeparam>
        /// <returns>Unique ID value.</returns>
        /// <exception cref="System.NotImplementedException">The repository does not support entities with ID of type +typeof(TId)</exception>
        public TId GetStoreId<T, TId>()
            where T : IHasStoreId<TId>
            where TId : IEquatable<TId>
        {
            if (typeof(TId) != typeof(long))
                throw new NotImplementedException("The repository does not support entities with ID of type "+typeof(TId));

            object id = Interlocked.Increment(ref _longId);

            return (TId)id;
        }

        /// <summary>
        /// Creates an <see cref="BaseDomainEntity" />-derived object of type <typeparamref name="T" />.
        /// Also extends the object with repository/ORM specific qualities like proxy, properties change tracking, etc.
        /// </summary>
        /// <typeparam name="T">The type of the entity to be created.</typeparam>
        /// <returns>The created entity.</returns>
        /// <exception cref="System.InvalidOperationException">The repository does not support type +typeof(T).FullName</exception>
        public T CreateEntity<T>() where T : BaseDomainEntity, new() => ObjectsRepositorySpecifics.CreateEntity<T>();

        /// <summary>
        /// Creates a <see cref="T:Value" /> derived object of type <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">The type of the object to be created.</typeparam>
        /// <returns>The created entity.</returns>
        public T CreateValue<T>() where T : BaseDomainValue, new() => ObjectsRepositorySpecifics.CreateValue<T>();

        /// <summary>
        /// Adds a new entity of <typeparamref name="T" /> to the repository.
        /// </summary>
        /// <typeparam name="T">The type of the entity to be added.</typeparam>
        /// <param name="entity">The entity to be added.</param>
        /// <returns><c>this</c></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", MessageId="0#")]
        public IRepository Add<T>(
            T entity) where T : BaseDomainEntity
        {
            var entityWith = entity as DomainEntity<long, string>;

            if (entityWith == null)
                throw new InvalidOperationException("The repository does not support type "+typeof(T).FullName);

            using (_sync.WriterLock())
            {
                var list = GetEntityList<T>();

                // ensure the uniqueness of the entity
                if (list.OfType<T>()
                        .Any(e => e.Equals(entityWith)))
                    throw new ObjectIdentifierNotUniqueException("An object with this identity already exists in the store.");

                // if it doesn't have store ID - assign one
                if (entityWith.Id == 0L)
                    entityWith.Id = Interlocked.Increment(ref _longId);

                // ensure uniqueness of the store ID
                if (list.Any(e => e.Id == entityWith.Id))
                    throw new ObjectIdentifierNotUniqueException("An object with this store unique identifier already exists in the store.");

                list.Add(entityWith);
            }

            return this;
        }

        /// <summary>
        /// Gets an entity of type <typeparamref name="T" /> from the repository where the entity is referred to by repository ID.
        /// </summary>
        /// <typeparam name="T">The type of the entity to be fetched.</typeparam>
        /// <typeparam name="TId">The type of the store identifier.</typeparam>
        /// <param name="id">The repository's related unique identifier (primary key for a DB) of the entity to be fetched.</param>
        /// <returns>An entity with the specified <paramref name="id" /> or <see langword="null" />.</returns>
        /// <exception cref="System.InvalidOperationException">The repository does not support type +typeof(T).FullName</exception>
        public T GetByStoreId<T, TId>(
            TId id)
            where T : BaseDomainEntity, IHasStoreId<TId>
            where TId : IEquatable<TId>
        {
            using (_sync.WriterLock())
                return (T)(object)GetEntityList<T>().FirstOrDefault(e => e.Id.Equals(id));
        }

        /// <summary>
        /// Gets an entity of type <typeparamref name="T" /> from the repository where the entity is referred to by repository ID.
        /// </summary>
        /// <typeparam name="T">The type of the entity to be fetched.</typeparam>
        /// <typeparam name="TKey">The type of the business key.</typeparam>
        /// <param name="key">The business key of the entity to be fetched.</param>
        /// <returns>An entity with the specified <paramref name="key" /> or <see langword="null" />.</returns>
        public T GetByKey<T, TKey>(
            TKey key)
            where T : BaseDomainEntity, IHasBusinessKey<TKey>
            where TKey : IEquatable<TKey>
        {
            using (_sync.WriterLock())
                return (T)(object)GetEntityList<T>().FirstOrDefault(e => e.Key.Equals(key));
        }

        /// <summary>
        /// Attaches the specified entity to the context of the repository.
        /// </summary>
        /// <typeparam name="T">The type of the entity.</typeparam>
        /// <param name="entity">The entity to attach.</param>
        /// <returns><c>this</c></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", MessageId="0#")]
        public IRepository Attach<T>(
            T entity) where T : BaseDomainEntity
        {
            var entityWith = entity as DomainEntity<long, string>;

            if (entityWith == null)
                throw new InvalidOperationException("The repository does not support type "+typeof(T).FullName);

            using (_sync.WriterLock())
            {
                var list = GetEntityList<T>();

                var existing = list.Where(e => e.Id == entityWith.Id)
                                   .FirstOrDefault();

                if (existing == null)
                    throw new InvalidOperationException(
                        "Only entities that are known to exist in the store should be attached. Otherwise they will not be stored."+
                        "Note that repositories based on Entity Framework or NHibernate would not be able to catch this situation and data may be lost.");

                if (ReferenceEquals(existing, entity))
                    return this;

                list.Remove(existing);
                list.Add(entityWith);
            }

            return this;
        }

        /// <summary>
        /// Attaches the specified entity to the context of the repository and marks the entire entity or the specified properties as modified.
        /// </summary>
        /// <typeparam name="T">The type of the entity.</typeparam>
        /// <param name="entity">The entity to attach and mark as modified.</param>
        /// <param name="state">The repository related state of the object.</param>
        /// <param name="modifiedProperties">The names of the properties that actually changed their values.
        /// If the array is empty, the entire entity will be marked as modified and updated in the store
        /// otherwise, only the modified properties will be updated in the store.</param>
        /// <returns><c>this</c></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", MessageId="0#")]
        public IRepository Attach<T>(
            T entity,
            EntityState state,
            params string[] modifiedProperties) where T : BaseDomainEntity
        {
            var entityWith = entity as DomainEntity<long, string>;

            if (entityWith == null)
                throw new InvalidOperationException("The repository does not support type "+typeof(T).FullName);

            using (_sync.WriterLock())
            {
                var list = GetEntityList<T>();
                var existing = list.Where(e => e.Id == entityWith.Id)
                                   .FirstOrDefault();

                switch (state)
                {
                case EntityState.Added:
                    if (existing != null)
                        throw new InvalidOperationException(
                            "Only entities that do not exist in the underlying store can be attached in EntityState.Added state. "+
                            "Note that repositories based on Entity Framework or NHibernate would not be able to catch this situation early "+
                            "and most likely will throw an exception at commit time.");
                    list.Add(entityWith);
                    break;

                case EntityState.Modified:
                    if (existing == null)
                        throw new InvalidOperationException(
                            "Only entities that exist in the underlying store can be attached in EntityState.Modified state. "+
                            "Note that repositories based on Entity Framework or NHibernate would not be able to catch this situation early "+
                            "and most likely will throw an exception at commit time.");
                    if (!ReferenceEquals(existing, entity))
                    {
                        list.Remove(existing);
                        list.Add(entityWith);
                    }
                    break;

                case EntityState.Deleted:
                    if (existing != null)
                        list.Remove(existing);
                    break;
                }
            }

            return this;
        }

        /// <summary>
        /// Detaches the specified entity from the context of the repository.
        /// </summary>
        /// <typeparam name="T">The type of the entity.</typeparam>
        /// <param name="entity">The entity to attach.</param>
        /// <returns><c>this</c></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", MessageId="0#")]
        public IRepository Detach<T>(
            T entity) where T : BaseDomainEntity
        {
            throw new NotImplementedException("The MapObjectsRepository does not implement the method Detach.");
        }

        /// <summary>
        /// Deletes an instance from the repository.
        /// </summary>
        /// <typeparam name="T">The type of the instance to be deleted.</typeparam>
        /// <param name="instance">The instance to be deleted.</param>
        /// <returns><c>this</c></returns>
        /// <remarks>Consider if <paramref name="instance" /> is <see langword="null" /> or not found in the repository, the method to silently succeed.</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", MessageId = "0#")]
        public IRepository Delete<T>(
            T instance) where T : BaseDomainEntity
        {
            var entityWith = instance as DomainEntity<long, string>;

            if (entityWith == null)
                throw new InvalidOperationException("The repository does not support type "+typeof(T).FullName);

            using (_sync.WriterLock())
                GetEntityList<T>().Remove(entityWith);

            return this;
        }

        /// <summary>
        /// Gets a collection of all entities of type <typeparamref name="T" /> from the repository.
        /// </summary>
        /// <typeparam name="T">The type of the entities to be returned.</typeparam>
        /// <returns><see cref="IQueryable{T}" /> sequence.</returns>
        /// <remarks>This returns the DDD's aggregate source for the given type. If you modify any of the returned entities
        /// their state will most likely be saved in the underlying storage.
        /// Hint: The result of this method can participate in the <c>from</c> clause of a LINQ expression.</remarks>
        public IQueryable<T> Entities<T>() where T : BaseDomainEntity
        {
            if (!typeof(DomainEntity<long, string>).IsAssignableFrom(typeof(T)))
                throw new InvalidOperationException("The repository does not support type "+typeof(T).FullName);

            using (_sync.WriterLock())
                return GetEntityList<T>().OfType<T>()
                                         .AsQueryable<T>();
        }

        /// <summary>
        /// Gets from the repository a collection of detached from the current context/session entities of type <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">The type of the entities to be returned.</typeparam>
        /// <returns><see cref="IQueryable{T}" /> sequence.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>This method returns the DDD's aggregate source for the given type. The objects however are supposed to be "detached"
        /// from the repository's context and any modifications on them will not be saved unless they are re-attached.
        /// This method gives a chance for performance optimization for some ORM-s in fetching read-only objects from the repository.
        /// The implementing class may however delegate this implementation to <see cref="Entities&lt;T&gt;" />.
        /// <para>
        /// Hint: The result of this method can participate in the <c>from</c> clause of a LINQ expression.
        /// </para></remarks>
        public IQueryable<T> DetachedEntities<T>() where T : BaseDomainEntity => Entities<T>();

        /// <summary>
        /// Saves the changes buffered in the repository's context.
        /// </summary>
        /// <returns><c>this</c></returns>
        public IRepository CommitChanges() => this;
        #endregion

        #region IDisposable Members
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() => GC.SuppressFinalize(this);
        #endregion

        #region IRepositoryAsync Members
        /// <summary>
        /// Initializes the repository asynchronously.
        /// </summary>
        /// <returns>this</returns>
        public Task<IRepository> InitializeAsync() => Task.FromResult(Initialize());

        /// <summary>
        /// Gets asynchronously an entity of type <typeparamref name="T" /> from the repository where the entity is referred to by repository ID.
        /// </summary>
        /// <typeparam name="T">The type of the entity to be fetched.</typeparam>
        /// <typeparam name="TId">The type of the store identifier.</typeparam>
        /// <param name="id">The repository's related unique identifier (primary key for a DB) of the entity to be fetched.</param>
        /// <returns>A <see cref="Task{T}" /> object which represents the asynchronous process of fetching the object.</returns>
        public Task<T> GetByStoreIdAsync<T, TId>(
            TId id)
            where T : BaseDomainEntity, IHasStoreId<TId>
            where TId : IEquatable<TId> => Task.FromResult(GetByStoreId<T, TId>(id));

        /// <summary>
        /// Gets an entity of type <typeparamref name="T" /> from the repository where the entity is referred to by repository ID.
        /// </summary>
        /// <typeparam name="T">The type of the entity to be fetched.</typeparam>
        /// <typeparam name="TKey">The type of the business key.</typeparam>
        /// <param name="key">The business key of the entity to be fetched.</param>
        /// <returns>A <see cref="Task{T}" /> object which represents the asynchronous process of fetching the object.</returns>
        public Task<T> GetByKeyAsync<T, TKey>(
            TKey key)
            where T : BaseDomainEntity, IHasBusinessKey<TKey>
            where TKey : IEquatable<TKey> => Task.FromResult(GetByKey<T, TKey>(key));

        /// <summary>
        /// Asynchronously saves the changes buffered in the repository's context/session.
        /// </summary>
        /// <returns><c>this</c></returns>
        public Task<IRepository> CommitChangesAsync() => Task.FromResult(CommitChanges());
        #endregion
    }
}