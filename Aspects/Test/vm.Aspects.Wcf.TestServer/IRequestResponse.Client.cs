using System;
using System.Collections.Generic;
using System.IdentityModel.Claims;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Security;
using Microsoft.ApplicationInsights.Wcf;
using vm.Aspects.Security;
using vm.Aspects.Wcf.Bindings;
using vm.Aspects.Wcf.Clients;

namespace vm.Aspects.Wcf.TestServer
{
    /// <summary>
    /// WCF channel factory based client (proxy) for services implementing the contract IRequestResponse.
    /// </summary>
    /// <seealso cref="LightClient{IRequestResponse}" />
    /// <seealso cref="IRequestResponse" />
    [ClientTelemetry]
    public class RequestResponseClient : LightClient<IRequestResponse>, IRequestResponse
    {
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestResponseClient" /> class (creates the channel factory).
        /// </summary>
        /// <param name="binding">A binding instance.</param>
        /// <param name="remoteAddress">The remote address of the service.</param>
        /// <param name="identityType">
        /// Type of the identity: can be <see cref="ServiceIdentity.Dns" />, <see cref="ServiceIdentity.Spn" />, <see cref="ServiceIdentity.Upn" />, or 
        /// <see cref="ServiceIdentity.Rsa" />.
        /// </param>
        /// <param name="identity">
        /// The identifier in the case of <see cref="ServiceIdentity.Dns" /> should be the DNS name of specified by the service's certificate or machine.
        /// If the identity type is <see cref="ServiceIdentity.Upn" /> - use the UPN of the service identity; if <see cref="ServiceIdentity.Spn" /> - use the SPN and if
        /// <see cref="ServiceIdentity.Rsa" /> - use the RSA key.
        /// </param>
        /// <param name="messagingPattern">
        /// The messaging pattern defining the configuration of the connection. If <see langword="null"/>, empty or whitespace characters only, 
        /// the constructor will try to resolve the pattern from the interface's attribute <see cref="MessagingPatternAttribute"/> if present,
        /// otherwise will apply the default messaging pattern fro the transport.
        /// </param>
        public RequestResponseClient(
            Binding binding,
            string remoteAddress,
            ServiceIdentity identityType,
            string identity,
            string messagingPattern = null)
            : base(binding, remoteAddress, identityType, identity, messagingPattern)
        {
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));
            if (remoteAddress.IsNullOrWhiteSpace())
                throw new ArgumentException("The argument cannot be null, empty string or consist of whitespace characters only.", nameof(remoteAddress));
            if (identityType != ServiceIdentity.None         &&
                identityType != ServiceIdentity.Certificate  &&
                identity.IsNullOrWhiteSpace())
                throw new ArgumentException("Invalid combination of identity parameters.");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:RequestResponseClient{TContract}" /> class.
        /// </summary>
        /// <param name="binding">A binding instance.</param>
        /// <param name="remoteAddress">The remote address of the service.</param>
        /// <param name="identityType">
        /// Type of the identity: can be <see cref="ServiceIdentity.Certificate" /> or <see cref="ServiceIdentity.Rsa" />.
        /// </param>
        /// <param name="certificate">The identifying certificate.</param>
        /// <param name="messagingPattern">
        /// The messaging pattern defining the configuration of the connection. If <see langword="null"/>, empty or whitespace characters only, 
        /// the constructor will try to resolve the pattern from the interface's attribute <see cref="MessagingPatternAttribute"/> if present,
        /// otherwise will apply the default messaging pattern fro the transport.
        /// </param>
        public RequestResponseClient(
            Binding binding,
            string remoteAddress,
            ServiceIdentity identityType,
            X509Certificate2 certificate,
            string messagingPattern = null)
            : base(binding, remoteAddress, identityType, certificate, messagingPattern)
        {
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));
            if (remoteAddress.IsNullOrWhiteSpace())
                throw new ArgumentException("The argument cannot be null, empty string or consist of whitespace characters only.", nameof(remoteAddress));
            if (identityType != ServiceIdentity.None  &&
                (identityType != ServiceIdentity.Dns  && identityType != ServiceIdentity.Rsa && identityType != ServiceIdentity.Certificate) || certificate==null)
                throw new ArgumentException("Invalid combination of identity parameters.");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestResponseClient" /> class.
        /// </summary>
        /// <param name="binding">A binding instance.</param>
        /// <param name="remoteAddress">The remote address of the service.</param>
        /// <param name="identityClaim">The identity claim.</param>
        /// <param name="messagingPattern">
        /// The messaging pattern defining the configuration of the connection. If <see langword="null"/>, empty or whitespace characters only, 
        /// the constructor will try to resolve the pattern from the interface's attribute <see cref="MessagingPatternAttribute"/> if present,
        /// otherwise will apply the default messaging pattern fro the transport.
        /// </param>
        public RequestResponseClient(
            Binding binding,
            string remoteAddress,
            Claim identityClaim,
            string messagingPattern = null)
            : base(binding, remoteAddress, identityClaim, messagingPattern)
        {
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));
            if (remoteAddress.IsNullOrWhiteSpace())
                throw new ArgumentException("The argument cannot be null, empty string or consist of whitespace characters only.", nameof(remoteAddress));
        }
        #endregion

        protected override void ConfigureChannelFactory(
            string messagingPattern)
        {
#if !RELEASE
            if (new[]
                {
                    RequestResponseTransportConfigurator.PatternName,
                    RequestResponseMessageConfigurator.PatternName,

                    RequestResponseTransportClientCertificateAuthenticationConfigurator.PatternName,
                    RequestResponseMessageClientCertificateAuthenticationConfigurator.PatternName,

                    RequestResponseMessageClientWindowsAuthenticationConfigurator.PatternName,
                }.Contains(messagingPattern)  &&
                ChannelFactory.Endpoint.Binding is NetTcpBinding  ||
                ChannelFactory.Endpoint.Binding is WSHttpBinding  ||
                ChannelFactory.Endpoint.Binding is BasicHttpBinding)
                ChannelFactory.Credentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.None;
#endif
            if (new[]
                {
                    RequestResponseTransportClientCertificateAuthenticationConfigurator.PatternName,
                    RequestResponseMessageClientCertificateAuthenticationConfigurator.PatternName,
                }.Contains(messagingPattern))
                ChannelFactory.Credentials.ClientCertificate.Certificate = CertificateFactory.GetCertificate(StoreLocation.LocalMachine, StoreName.My, X509FindType.FindByThumbprint, "2b31465567e15c96d81cae74482aefaead75e4d1");
            // NOTE: the client's certificate root must be in the "Trusted Root Certification Authority"

            if (RequestResponseMessageClientCertificateAuthenticationConfigurator.PatternName == messagingPattern)
                ChannelFactory.Credentials.ServiceCertificate.DefaultCertificate = CertificateFactory.GetCertificate(StoreLocation.LocalMachine, StoreName.My, X509FindType.FindByThumbprint, "351ae52cb2c3ac15ec12a3bdce838554fc63da95");

            if (ChannelFactory.Endpoint.EndpointBehaviors.FirstOrDefault(b => b is ClientTelemetryEndpointBehavior) == null)
            {
                var telemetryAttribute = GetType().GetCustomAttribute<ClientTelemetryAttribute>();

                if (telemetryAttribute != null)
                    ChannelFactory.Endpoint.EndpointBehaviors.Add(new ClientTelemetryEndpointBehavior());
            }
        }

        #region IRequestResponse implementation
        public ICollection<string> GetStrings(int numberOfStrings) => Proxy.GetStrings(numberOfStrings);
        #endregion
    }
}
