﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.ServiceLocation;

namespace vm.Aspects.Security.Cryptography.Ciphers
{
    /// <summary>
    /// The class <c>KeyedHasher</c> computes and verifies the cryptographic hash of data for maintaining its integrity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Crypto package contents:
    ///     <list type="number">
    ///         <item><description>The bytes of the hash.</description></item>
    ///     </list>
    /// </para>
    /// </remarks>
    public class KeyedHasher : IHasherAsync, IKeyManagement
    {
        #region Fields
        /// <summary>
        /// The public key used for encrypting the hash key.
        /// </summary>
        RSACryptoServiceProvider _publicKey;
        /// <summary>
        /// The private key used for decrypting the hash key.
        /// </summary>
        RSACryptoServiceProvider _privateKey;
        /// <summary>
        /// The object which is responsible for storing and retrieving the encrypted hash key 
        /// to and from the store with the determined store location name (e.g file I/O).
        /// </summary>
        IKeyStorageAsync _keyStorage;
        /// <summary>
        /// Caches the hash algorithm factory.
        /// </summary>
        readonly IHashAlgorithmFactory _hashAlgorithmFactory;
        /// <summary>
        /// The hash key.
        /// </summary>
        byte[] _hashKey;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="KeyedHasher" /> class.
        /// </summary>
        /// <param name="certificate">
        /// The certificate containing the public and optionally the private key for encryption and decryption of the hash key.
        /// If the parameter is <see langword="null"/> the method will try to resolve its value from the Common Service Locator with resolve name &quot;EncryptingHashKeyCertificate&quot;.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// The keyed hash algorithm name. You can use any of the constants from <see cref="Algorithms.KeyedHash" /> or
        /// <see langword="null" />, empty or whitespace characters only - it will default to <see cref="Algorithms.KeyedHash.Default" />.
        /// Also a string instance with name "DefaultKeyedHash" can be defined in a Common Service Locator compatible dependency injection container.
        /// </param>
        /// <param name="keyLocation">
        /// Seeding name of store location name of the encrypted symmetric key (e.g. relative or absolute path).
        /// Can be <see langword="null"/>, empty or whitespace characters only.
        /// The parameter will be passed to the <paramref name="keyLocationStrategy"/> to determine the final store location name path (e.g. relative or absolute path).
        /// </param>
        /// <param name="keyLocationStrategy">
        /// Object which implements the strategy for determining the store location name (e.g. path and filename) of the encrypted symmetric key.
        /// If <see langword="null"/> it defaults to a new instance of the class <see cref="KeyLocationStrategy"/>.
        /// </param>
        /// <param name="keyStorage">
        /// Object which implements the storing and retrieving of the the encrypted symmetric key to and from the store with the determined location name.
        /// If <see langword="null"/> it defaults to a new instance of the class <see cref="KeyFile"/>.
        /// </param>
        public KeyedHasher(
            X509Certificate2 certificate = null,
            string hashAlgorithmName = null,
            string keyLocation = null,
            IKeyLocationStrategy keyLocationStrategy = null,
            IKeyStorageAsync keyStorage = null)
        {
            _hashAlgorithmFactory = ServiceLocatorWrapper.Default.GetInstance<IHashAlgorithmFactory>(Algorithms.KeyedHash.ResolveName);
            _hashAlgorithmFactory.Initialize(hashAlgorithmName);
            ResolveKeyStorage(keyLocation, keyLocationStrategy, keyStorage);
            InitializeAsymmetricKeys(certificate);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the name of the hash algorithm.
        /// </summary>
        /// <value>The name of the hash algorithm.</value>
        public string HashAlgorithmName
        {
            get { return _hashAlgorithmFactory.HashAlgorithmName; }
        }
        #endregion

        #region IHasher Members
        /// <summary>
        /// Gets or sets the length of the salt in bytes. Here it is not used and always returns 0.
        /// </summary>
        /// <value>0</value>
        public virtual int SaltLength
        {
            get
            {
                Contract.Ensures(Contract.Result<int>() == 0);

                return 0;
            }
            set { }
        }

        /// <summary>
        /// Computes the hash of a <paramref name="dataStream" /> stream.
        /// </summary>
        /// <param name="dataStream">The data stream.</param>
        /// <returns>
        /// The hash of the stream optionally prepended with the generated salt or <see langword="null"/> if <paramref name="dataStream"/> is <see langword="null"/>.
        /// </returns>
        /// <exception cref="System.ArgumentException">The data stream cannot be read.</exception>
        public virtual byte[] Hash(
            Stream dataStream)
        {
            if (dataStream == null)
                return null;
            if (!dataStream.CanRead)
                throw new ArgumentException("The data stream cannot be read.", "dataStream");

            InitializeHashKey();

            using (var hashAlgorithm = _hashAlgorithmFactory.Create() as KeyedHashAlgorithm)
            {
                Contract.Assert(hashAlgorithm != null);
                hashAlgorithm.Key = _hashKey;
                using (var hashStream = CreateHashStream(hashAlgorithm))
                {
                    dataStream.CopyTo(hashStream);
                    return FinalizeHashing(hashStream, hashAlgorithm);
                }
            }
        }

        /// <summary>
        /// Verifies that the <paramref name="hash" /> of a <paramref name="dataStream" /> is correct.
        /// </summary>
        /// <param name="dataStream">The data stream.</param>
        /// <param name="hash">The hash to verify, optionally prepended with salt.</param>
        /// <returns>
        /// <see langword="true" /> if <paramref name="hash" /> is correct or <paramref name="hash" /> and <paramref name="dataStream"/> are both <see langword="null"/>, 
        /// otherwise <see langword="false" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="hash"/> is <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the hash has an invalid size.</exception>
        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "1")]
        public virtual bool TryVerifyHash(
            Stream dataStream,
            byte[] hash)
        {
            if (dataStream == null)
                return hash==null;

            InitializeHashKey();

            using (var hashAlgorithm = _hashAlgorithmFactory.Create() as KeyedHashAlgorithm)
            {
                Contract.Assert(hashAlgorithm != null);

                // the parameter hash must have the length of the expected product from this algorithm
                if (hash.Length != hashAlgorithm.HashSize/8)
                    return false;

                hashAlgorithm.Key = _hashKey;
                using (var hashStream = CreateHashStream(hashAlgorithm))
                {
                    dataStream.CopyTo(hashStream);

                    byte[] computedHash = FinalizeHashing(hashStream, hashAlgorithm);

                    return computedHash.ConstantTimeEquals(hash);
                }
            }
        }

        /// <summary>
        /// Computes the hash of a specified <paramref name="data" />.
        /// </summary>
        /// <param name="data">The data to be hashed.</param>
        /// <returns>The hash of the <paramref name="data" /> optionally prepended with the generated salt or <see langword="null" /> if <paramref name="data" /> is <see langword="null" />.
        /// </returns>
        public virtual byte[] Hash(
            byte[] data)
        {
            if (data == null)
                return null;

            InitializeHashKey();

            using (var hashAlgorithm = _hashAlgorithmFactory.Create() as KeyedHashAlgorithm)
            {
                Contract.Assert(hashAlgorithm != null);
                hashAlgorithm.Key = _hashKey;
                using (var hashStream = CreateHashStream(hashAlgorithm))
                {
                    hashStream.Write(data, 0, data.Length);
                    return FinalizeHashing(hashStream, hashAlgorithm);
                }
            }
        }

        /// <summary>
        /// Verifies the hash of the specified <paramref name="data" />.
        /// </summary>
        /// <param name="data">The data which hash needs to be verified.</param>
        /// <param name="hash">The hash with optionally prepended salt to be verified.</param>
        /// <returns>
        /// <see langword="true" /> if the hash is correct or <paramref name="hash" /> and <paramref name="data"/> are both <see langword="null"/>, otherwise <see langword="false" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="hash"/> is <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the hash is invalid.</exception>
        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "1")]
        public virtual bool TryVerifyHash(
            byte[] data,
            byte[] hash)
        {
            if (data == null)
                return hash==null;

            InitializeHashKey();

            using (var hashAlgorithm = _hashAlgorithmFactory.Create() as KeyedHashAlgorithm)
            {
                Contract.Assert(hashAlgorithm != null);

                // the parameter hash must have the length of the expected product from this algorithm
                if (hash.Length != hashAlgorithm.HashSize/8)
                    return false;

                hashAlgorithm.Key = _hashKey;
                using (var hashStream = CreateHashStream(hashAlgorithm))
                {
                    hashStream.Write(data, 0, data.Length);

                    byte[] computedHash = FinalizeHashing(hashStream, hashAlgorithm);

                    return computedHash.ConstantTimeEquals(hash);
                }
            }
        }
        #endregion

        #region IHasherAsync members
        /// <summary>
        /// Computes the hash of a <paramref name="dataStream" /> stream.
        /// </summary>
        /// <param name="dataStream">The data stream.</param>
        /// <returns>
        /// The hash of the stream optionally prepended with the generated salt or <see langword="null"/> if <paramref name="dataStream"/> is <see langword="null"/>.
        /// </returns>
        /// <exception cref="System.ArgumentException">The data stream cannot be read.</exception>
        public virtual async Task<byte[]> HashAsync(
            Stream dataStream)
        {
            if (dataStream == null)
                return null;

            await InitializeHashKeyAsync();

            using (var hashAlgorithm = _hashAlgorithmFactory.Create() as KeyedHashAlgorithm)
            {
                Contract.Assert(hashAlgorithm != null);
                hashAlgorithm.Key = _hashKey;
                using (var hashStream = CreateHashStream(hashAlgorithm))
                {
                    await dataStream.CopyToAsync(hashStream);
                    return FinalizeHashing(hashStream, hashAlgorithm);
                }
            }
        }

        /// <summary>
        /// Verifies that the <paramref name="hash" /> of a <paramref name="dataStream" /> is correct.
        /// </summary>
        /// <param name="dataStream">The data stream.</param>
        /// <param name="hash">The hash to verify, optionally prepended with salt.</param>
        /// <returns>
        /// <see langword="true" /> if <paramref name="hash" /> is correct or <paramref name="hash" /> and <paramref name="dataStream"/> are both <see langword="null"/>, 
        /// otherwise <see langword="false" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="hash"/> is <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the hash has an invalid size.</exception>
        public virtual async Task<bool> TryVerifyHashAsync(
            Stream dataStream,
            byte[] hash)
        {
            if (dataStream == null)
                return hash==null;

            await InitializeHashKeyAsync();

            using (var hashAlgorithm = _hashAlgorithmFactory.Create() as KeyedHashAlgorithm)
            {
                Contract.Assert(hashAlgorithm != null);

                // the hash has the same length as the length of the key - there is no salt
                if (hash.Length != hashAlgorithm.HashSize/8)
                    return false;

                hashAlgorithm.Key = _hashKey;
                using (var hashStream = CreateHashStream(hashAlgorithm))
                {
                    await dataStream.CopyToAsync(hashStream);

                    byte[] computedHash = FinalizeHashing(hashStream, hashAlgorithm);

                    return computedHash.ConstantTimeEquals(hash);
                }
            }
        }
        #endregion

        #region IKeyManagement Members
        /// <summary>
        /// Gets the physical storage location name of a symmetric key, e.g. the path and filename of a file.
        /// </summary>
        public string KeyLocation { get; protected set; }

        /// <summary>
        /// Imports the symmetric key as a clear text.
        /// </summary>
        /// <param name="key">The key.</param>
        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        public void ImportSymmetricKey(
            byte[] key)
        {
            _hashKey = key;
            _keyStorage.PutKey(EncryptHashKey(), KeyLocation);
        }

        /// <summary>
        /// Exports the symmetric key as a clear text.
        /// </summary>
        /// <returns>Array of bytes of the symmetric key or <see langword="null"/> if the cipher does not have a symmetric key.</returns>
        public byte[] ExportSymmetricKey()
        {
            InitializeHashKey();
            return _hashKey;
        }

        /// <summary>
        /// Asynchronously imports the symmetric key as a clear text.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="T:Task"/> object representing the process of asynchronously importing the symmetric key.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        public async Task ImportSymmetricKeyAsync(
            byte[] key)
        {
            _hashKey = key;
            await _keyStorage.PutKeyAsync(EncryptHashKey(), KeyLocation);
        }

        /// <summary>
        /// Asynchronously exports the symmetric key as a clear text.
        /// </summary>
        /// <returns>
        /// A <see cref="T:Task"/> object representing the process of asynchronously exporting the symmetric key including the result -
        /// array of bytes of the symmetric key or <see langword="null"/> if the cipher does not have a symmetric key.
        /// </returns>
        public async Task<byte[]> ExportSymmetricKeyAsync()
        {
            await InitializeHashKeyAsync();
            return _hashKey;
        }
        #endregion

        /// <summary>
        /// Initializes the hash key storage by executing the key location strategy.
        /// </summary>
        /// <param name="keyLocation">The name of the hash key location.</param>
        /// <param name="keyLocationStrategy">The hash key location strategy.</param>
        /// <param name="keyStorage">The hash key storage.</param>
        protected void ResolveKeyStorage(
            string keyLocation,
            IKeyLocationStrategy keyLocationStrategy,
            IKeyStorageAsync keyStorage)
        {
            Contract.Ensures(KeyLocation != null, "Could not determine the key's physical location.");
            Contract.Ensures(_keyStorage != null, "Could not resolve the IKeyStorageAsync object.");

            try
            {
                if (keyLocationStrategy == null)
                    keyLocationStrategy = ServiceLocatorWrapper.Default.GetInstance<IKeyLocationStrategy>();
            }
            catch (ActivationException)
            {
                keyLocationStrategy = new KeyLocationStrategy();
            }

            KeyLocation = keyLocationStrategy.GetKeyLocation(keyLocation);

            try
            {
                if (keyStorage == null)
                    keyStorage = ServiceLocatorWrapper.Default.GetInstance<IKeyStorageAsync>();
            }
            catch (ActivationException)
            {
                keyStorage = new KeyFile();
            }

            _keyStorage = keyStorage;
        }

        void InitializeAsymmetricKeys(
            X509Certificate2 certificate)
        {
            if (certificate == null)
                try
                {
                    certificate = ServiceLocatorWrapper.Default.GetInstance<X509Certificate2>(Algorithms.KeyedHash.CertificateResolveName);
                }
                catch (ActivationException x)
                {
                    throw new ArgumentNullException("The parameter "+nameof(certificate)+" was null and could not be resolved from the Common Service Locator.", x);
                }

            _publicKey = (RSACryptoServiceProvider)certificate.PublicKey.Key;

            if (certificate.HasPrivateKey)
                _privateKey = (RSACryptoServiceProvider)certificate.PrivateKey;
        }

        bool IsHashKeyInitialized
        {
            get { return _hashKey?.Length > 0; }
        }

        /// <summary>
        /// Initializes the asymmetric key.
        /// </summary>
        void InitializeHashKey()
        {
            if (IsHashKeyInitialized)
                return;

            byte[] encryptedKey;

            try
            {
                encryptedKey = _keyStorage.GetKey(KeyLocation);
            }
            catch (FileNotFoundException)
            {
                encryptedKey = null;
            }

            if (encryptedKey == null)
            {
                var hash = _hashAlgorithmFactory.Create() as KeyedHashAlgorithm;

                Contract.Assert(hash != null);
                ImportSymmetricKey(hash.Key);
            }
            else
                DecryptHashKey(encryptedKey);
        }

        /// <summary>
        /// Asynchronously initializes the asymmetric key.
        /// </summary>
        async Task InitializeHashKeyAsync()
        {
            if (IsHashKeyInitialized)
                return;

            byte[] encryptedKey;

            try
            {
                encryptedKey = await _keyStorage.GetKeyAsync(KeyLocation);
            }
            catch (FileNotFoundException)
            {
                encryptedKey = null;
            }

            if (encryptedKey == null)
            {
                var hash = _hashAlgorithmFactory.Create() as KeyedHashAlgorithm;

                Contract.Assert(hash != null);
                await ImportSymmetricKeyAsync(hash.Key);
            }
            else
                DecryptHashKey(encryptedKey);
        }

        /// <summary>
        /// Encrypts the hash key using the public key.
        /// </summary>
        /// <returns>The key bytes.</returns>
        byte[] EncryptHashKey()
        {
            return _publicKey.Encrypt(_hashKey, true);
        }

        /// <summary>
        /// Decrypts the hash key using the private key.
        /// </summary>
        /// <param name="encryptedKey">The encrypted key.</param>
        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        void DecryptHashKey(
            byte[] encryptedKey)
        {
            if (_privateKey == null)
                throw new InvalidOperationException("The certificate does not contain a private key.");

            _hashKey = _privateKey.Decrypt(encryptedKey, true);
        }

        /// <summary>
        /// Creates the crypto stream.
        /// </summary>
        /// <returns>CryptoStream.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "It will be disposed by the calling code.")]
        protected virtual CryptoStream CreateHashStream(
            HashAlgorithm hashAlgorithm)
        {
            Contract.Requires<ArgumentNullException>(hashAlgorithm != null, "hashAlgorithm");

            return new CryptoStream(new NullStream(), hashAlgorithm, CryptoStreamMode.Write);
        }

        /// <summary>
        /// Finalizes the hashing.
        /// </summary>
        /// <param name="hashStream">The hash stream.</param>
        /// <param name="hashAlgorithm">The hash algorithm.</param>
        /// <returns>The hash.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="hashStream" /> is <see langword="null" />.</exception>
        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "1")]
        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "2", Justification = "salt is conditionally validated.")]
        protected virtual byte[] FinalizeHashing(
            CryptoStream hashStream,
            HashAlgorithm hashAlgorithm)
        {
            Contract.Requires<ArgumentNullException>(hashStream != null, "hashStream");
            Contract.Requires<ArgumentException>(hashStream.CanWrite, "The argument \"hashStream\" cannot be written to.");
            Contract.Requires<ArgumentNullException>(hashAlgorithm != null, "hashAlgorithm");

            if (!hashStream.HasFlushedFinalBlock)
                hashStream.FlushFinalBlock();

            var hash = new byte[hashAlgorithm.HashSize/8];

            hashAlgorithm.Hash.CopyTo(hash, 0);

            return hash;
        }

        #region IDisposable pattern implementation
        /// <summary>
        /// The flag will be set just before the object is disposed.
        /// </summary>
        /// <value>0 - if the object is not disposed yet, any other value - the object is already disposed.</value>
        /// <remarks>
        /// Do not test or manipulate this flag outside of the property <see cref="IsDisposed"/> or the method <see cref="M:Dispose()"/>.
        /// The type of this field is Int32 so that it can be easily passed to the members of the class <see cref="Interlocked"/>.
        /// </remarks>
        int _disposed;

        /// <summary>
        /// Returns <c>true</c> if the object has already been disposed, otherwise <c>false</c>.
        /// </summary>
        public bool IsDisposed
        {
            get { return Volatile.Read(ref _disposed) != 0; }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <remarks>Invokes the protected virtual <see cref="M:Dispose(true)"/>.</remarks>
        [SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Justification = "It is correct.")]
        public void Dispose()
        {
            // if it is disposed or in a process of disposing - return.
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            // these will be called only if the instance is not disposed and is not in a process of disposing.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Allows the object to attempt to free resources and perform other cleanup operations before it is reclaimed by garbage collection. 
        /// </summary>
        /// <remarks>Invokes the protected virtual <see cref="M:Dispose(false)"/>.</remarks>
        ~KeyedHasher()
        {
            Dispose(false);
        }

        /// <summary>
        /// Performs the actual job of disposing the object.
        /// </summary>
        /// <param name="disposing">
        /// Passes the information whether this method is called by <see cref="M:Dispose()"/> (explicitly or
        /// implicitly at the end of a <c>using</c> statement), or by the <see cref="M:~Hasher"/>.
        /// </param>
        /// <remarks>
        /// If the method is called with <paramref name="disposing"/><c>==true</c>, i.e. from <see cref="M:Dispose()"/>, 
        /// it will try to release all managed resources (usually aggregated objects which implement <see cref="T:IDisposable"/> as well) 
        /// and then it will release all unmanaged resources if any. If the parameter is <c>false</c> then 
        /// the method will only try to release the unmanaged resources.
        /// </remarks>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                _hashAlgorithmFactory.Dispose();
        }
        #endregion

        [ContractInvariantMethod]
        void Invariant()
        {
            Contract.Invariant(_hashAlgorithmFactory != null, "The hash algorithm factory cannot be null.");
        }
    }
}