﻿using Microsoft.Practices.Unity;
using System.Runtime.Remoting.Messaging;
using vm.Aspects.Facilities;

namespace vm.Aspects.Wcf
{
#pragma warning disable CS3009 // Base type is not CLS-compliant
    /// <summary>
    /// Class PerCallContextLifetimeManager. Used for objects which lifetime should end with the end of the current 
    /// .NET remoting or WCF call context. The objects are stored in the current <see cref="CallContext"/>.
    /// </summary>
    public class PerCallContextLifetimeManager : LifetimeManager
#pragma warning restore CS3009 // Base type is not CLS-compliant
    {
        /// <summary>
        /// Gets the key of the object stored in the call context.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public string Key => Facility.GuidGenerator.NewGuid().ToString("N");

        /// <summary>
        /// Retrieve a value from the backing store associated with this Lifetime policy.
        /// </summary>
        /// <returns>the object desired, or null if no such object is currently stored.</returns>
        public override object GetValue() => CallContext.LogicalGetData(Key);

        /// <summary>
        /// Stores the given value into backing store for retrieval later.
        /// </summary>
        /// <param name="newValue">The object being stored.</param>
        public override void SetValue(
            object newValue)
        {
            if (newValue == null)
                RemoveValue();
            else
                CallContext.LogicalSetData(Key, newValue);
        }

        /// <summary>
        /// Remove the given object from backing store.
        /// </summary>
        public override void RemoveValue()
        {
            object value = CallContext.LogicalGetData(Key);

            CallContext.LogicalSetData(Key, null);
            CallContext.FreeNamedDataSlot(Key);

            value?.Dispose();
        }
    }
}
