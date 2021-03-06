﻿using Microsoft.Practices.EnterpriseLibrary.ExceptionHandling;
using System;
using System.Collections.Generic;
using vm.Aspects.Facilities;

namespace vm.Aspects.Wcf.ServicePolicies
{
    /// <summary>
    /// Class ServiceExceptionTransformationHandlingPolicies. Defines a registrar and implements <see cref="IExceptionPolicyProvider"/> which adds a 
    /// single programmatic mapping of exceptions to faults as defined in <see cref="FaultContracts.Fault"/>. <seealso cref="ServiceFaultFromExceptionHandler"/>
    /// These can be used instead of the WCF exception shielding mechanism. 
    /// </summary>
    public partial class ServiceFaultFromExceptionHandlingPolicies : IExceptionPolicyProvider
    {
        /// <summary>
        /// The exception transformation policy name
        /// </summary>
        public const string RegistrationName = nameof(ServiceFaultFromExceptionHandlingPolicies);
        /// <summary>
        /// The exception transformation policy name
        /// </summary>
        public const string PolicyName = "FaultException<> from Exception policy.";
        /// <summary>
        /// The title of the logged exceptions.
        /// </summary>
        public const string LogExceptionTitle = "vm.Aspects.Wcf";

        #region IExceptionPolicyEntries Members
        /// <summary>
        /// Gets a dictionary of exception policy names and respective lists of policy entries.
        /// </summary>
        public IDictionary<string, IEnumerable<ExceptionPolicyEntry>> ExceptionPolicyEntries
            => new SortedList<string, IEnumerable<ExceptionPolicyEntry>>
            {
                [PolicyName] = FaultFromExceptionPolicyEntries(),
            };
        #endregion

        static ExceptionPolicyEntry[] FaultFromExceptionPolicyEntries(
            string logExceptionTitle = LogExceptionTitle)
        {
            int eventId = 3900;

            return new ExceptionPolicyEntry[]
            {
                new ExceptionPolicyEntry(
                        typeof(Exception),
                        PostHandlingAction.ThrowNewException,
                        new IExceptionHandler[]
                        {
                            ExceptionPolicyProvider.CreateLoggingExceptionHandler(
                                    logExceptionTitle,
                                    eventId),

                            new ServiceFaultFromExceptionHandler(),
                        })
            };
        }
    }
}
