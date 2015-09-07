﻿using System;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
//using System.Data;
//using System.Data.Metadata.Edm;
//using System.Data.SqlClient;
using System.Globalization;
using System.Linq.Expressions;
using System.Security;
using System.Threading.Tasks;
using vm.Aspects.Diagnostics.ExternalMetadata;
//using Microsoft.Practices.EnterpriseLibrary.Validation;
//using Microsoft.Practices.EnterpriseLibrary.Validation.PolicyInjection;

namespace vm.Aspects.Diagnostics
{
    /// <summary>
    /// Class ClassMetadataRegistrar - helper for registering external dump metadata and type related <see cref="DumpAttribute"/>-s in a fluent API style.
    /// </summary>
    public class ClassMetadataRegistrar
    {
        /// <summary>
        /// Registers the metadata defined in <see cref="N:Experian.Ems.Asap.Aspects.Diagnostics.ExternalMetadata"/>. 
        /// Allows for chaining further registering more dump metadata.
        /// </summary>
        /// <returns>ClassMetadataRegistrar.</returns>
        public static ClassMetadataRegistrar RegisterMetadata()
        {
            Contract.Ensures(Contract.Result<ClassMetadataRegistrar>() != null);

            return new ClassMetadataRegistrar()
                .Register<Type, TypeDumpMetadata>()
                .Register<Exception, ExceptionDumpMetadata>()
                .Register<ArgumentException, ArgumentExceptionDumpMetadata>()
                .Register<SecurityException, SecurityExceptionDumpMetadata>()
                .Register<CultureInfo, CultureInfoDumpMetadata>()
                .Register<Task, TaskDumpMetadata>()

                .Register<Expression, ExpressionDumpMetadata>()
                .Register<LambdaExpression, LambdaExpressionDumpMetadata>()
                .Register<ParameterExpression, ParameterExpressionDumpMetadata>()
                .Register<BinaryExpression, BinaryExpressionDumpMetadata>()
                .Register<ConstantExpression, ConstantExpressionDumpMetadata>()

                //.Register<SqlException, SqlExceptionDumpMetadata>()
                //.Register<SqlError, SqlErrorDumpMetadata>()
                //.Register<ArgumentValidationException, ArgumentValidationExceptionDumpMetadata>()
                //.Register<MetadataItem, MetadataItemDumpMetadata>()
                //.Register<UpdateException, UpdateExceptionDumpMetadata>()
                //.Register<ValidationResult, ValidationResultDumpMetadata>()
                //.Register<ValidationResults, ValidationResultsDumpMetadata>()
                //.Register<ConfigurationErrorsException, ConfigurationErrorsExceptionDumpMetadata>()
                ;
        }

        /// <summary>
        /// Registers the dump metadata and <see cref="DumpAttribute"/> instance related to the specified type.
        /// </summary>
        /// <param name="type">The type for which the metadata is being registered.</param>
        /// <param name="metadataType">The dump metadata type.</param>
        /// <param name="dumpAttribute">The dump attribute.</param>
        /// <returns>The current instance of ClassMetadataRegistrar.</returns>
        /// <exception cref="T:System.ArgumentNullException">
        /// Thrown if <paramref name="type"/> is <see langword="null"/>.
        /// </exception>
        public ClassMetadataRegistrar Register(
            Type type,
            Type metadataType,
            DumpAttribute dumpAttribute = null)
        {
            Contract.Requires<ArgumentNullException>(type != null, "type");
            Contract.Ensures(Contract.Result<ClassMetadataRegistrar>() != null);

            ClassMetadataResolver.SetClassDumpData(type, metadataType, dumpAttribute);
            return this;
        }

        /// <summary>
        /// Registers the dump metadata and <see cref="DumpAttribute"/> instance related to the specified type.
        /// </summary>
        /// <typeparam name="T">The type for which the metadata is being registered.</typeparam>
        /// <typeparam name="TMetadata">The dump metadata type.</typeparam>
        /// <param name="dumpAttribute">The dump attribute.</param>
        /// <returns>The current instance of ClassMetadataRegistrar.</returns>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
        public ClassMetadataRegistrar Register<T, TMetadata>(
            DumpAttribute dumpAttribute = null)
        {
            Contract.Ensures(Contract.Result<ClassMetadataRegistrar>() != null);

            return Register(typeof(T), typeof(TMetadata), dumpAttribute);
        }

        /// <summary>
        /// Registers the specified dump attribute.
        /// </summary>
        /// <typeparam name="T">The type for which the dump attribute is being registered.</typeparam>
        /// <param name="dumpAttribute">The dump attribute.</param>
        /// <returns>The current instance of ClassMetadataRegistrar.</returns>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
        public ClassMetadataRegistrar Register<T>(
            DumpAttribute dumpAttribute)
        {
            Contract.Ensures(Contract.Result<ClassMetadataRegistrar>() != null);

            return Register(typeof(T), null, dumpAttribute);
        }
    }
}