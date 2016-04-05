﻿using System;

namespace vm.Aspects
{
    /// <summary>
    /// Supplies objects with conditional 'Dispose' method (if they implement <see cref="IDisposable"/>).
    /// </summary>
    public static class DisposableExtensions
    {
        /// <summary>
        /// Disposes the specified lazily instantiated object, if it supports <see cref="IDisposable"/>.
        /// </summary>
        /// <typeparam name="T">The type of the lazy instantiated object.</typeparam>
        /// <param name="lazy">The lazy instantiated object.</param>
        public static void Dispose<T>(this Lazy<T> lazy)
        {
            if (!lazy.IsValueCreated)
                return;

            var disposable = lazy.Value as IDisposable;

            if (disposable != null)
                disposable.Dispose();
        }

        /// <summary>
        /// Disposes the specified object if it supports <see cref="IDisposable"/>.
        /// </summary>
        /// <param name="obj">The object.</param>
        public static void Dispose(this object obj)
        {
            var disposable = obj as IDisposable;

            if (disposable != null)
                disposable.Dispose();
        }
    }
}