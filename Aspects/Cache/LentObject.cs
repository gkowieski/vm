﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace vm.Aspects.Cache
{
    /// <summary>
    /// Class LentObject wraps an object lent from an object pool. When the object is lent the pool returns an instance of this class.
    /// The actual object can be accessed from the property <see cref="Instance"/>. The easiest way to return the object to the pool
    /// is to dispose the instance of <c>LentObject</c>.
    /// </summary>
    /// <typeparam name="T">The type of the objects in the object pool.</typeparam>
    public class LentObject<T> : IDisposable, IIsDisposed where T : class
    {
        T _instance;
        ObjectPool<T> _pool;

        /// <summary>
        /// Initializes a new instance of the <see cref="LentObject{T}"/> class.
        /// </summary>
        /// <param name="instance">The object.</param>
        /// <param name="pool">The pool from which the object is lent.</param>
        internal LentObject(
            T instance,
            ObjectPool<T> pool)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
            _pool     = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        /// <summary>
        /// Gets the lent object.
        /// </summary>
        public T Instance => _instance;

        /// <summary>
        /// Gets the object pool from which the object was lent.
        /// </summary>
        internal ObjectPool<T> Pool => _pool;

        #region IDisposable pattern implementation
        /// <summary>
        /// The flag will be set just before the object is disposed.
        /// </summary>
        /// <value>0 - if the object is not disposed yet, any other value - the object is already disposed.</value>
        int _disposed;

        /// <summary>
        /// Returns <c>true</c> if the object has already been disposed, otherwise <c>false</c>.
        /// </summary>
        public bool IsDisposed => Interlocked.CompareExchange(ref _disposed, 1, 1) == 1;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <remarks>Invokes the protected virtual <see cref="Dispose(bool)"/>.</remarks>
        [SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Justification = "It is correct.")]
        public void Dispose()
        {
            // these will be called only if the instance is not disposed and is not in a process of disposing.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs the actual job of disposing the object.
        /// </summary>
        /// <param name="disposing">
        /// Passes the information whether this method is called by <see cref="Dispose()"/> (explicitly or
        /// implicitly at the end of a <c>using</c> statement), or by the finalizer.
        /// </param>
        /// <remarks>
        /// If the method is called with <paramref name="disposing"/><c>==true</c>, i.e. from <see cref="Dispose()"/>, 
        /// it will try to release all managed resources (usually aggregated objects which implement <see cref="IDisposable"/> as well) 
        /// and then it will release all unmanaged resources if any. If the parameter is <c>false</c> then 
        /// the method will only try to release the unmanaged resources.
        /// </remarks>
        protected virtual void Dispose(
            bool disposing)
        {
            // if it is disposed or in a process of disposing - return.
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (disposing)
                _pool.ReturnObject(this);
        }
        #endregion
    }
}
