﻿using System.IO;
using System.Threading.Tasks;

namespace vm.Aspects.Security.Cryptography.Ciphers
{
    /// <summary>
    /// Interface <c>IHasherAsync</c> extends <see cref="IHasher"/> with 
    /// asynchronous versions of its <see cref="Stream"/> related methods.
    /// </summary>
    public interface IHasherAsync : IHasher
    {
        /// <summary>
        /// Computes the hash of the <paramref name="dataStream" /> asynchronously.
        /// </summary>
        /// <param name="dataStream">
        /// The data stream to compute the hash of.
        /// </param>
        /// <returns>
        /// A <see cref="T:Task{byte[]}"/> object representing the hashing process and the end result -
        /// a hash of the stream, optionally prepended with the generated salt.
        /// If <paramref name="dataStream" /> is <see langword="null" /> returns <see langword="null" />.
        /// </returns>
        /// <exception cref="T:System.ArgumentException">
        /// Thrown when <paramref name="dataStream"/> cannot be read.
        /// </exception>
        /// <exception cref="T:System.Security.Cryptography.CryptographicException">
        /// The hash or the encryption failed.
        /// </exception>
        /// <exception cref="T:System.IO.IOException">
        /// An I/O error occurred.
        /// </exception>
        Task<byte[]> HashAsync(Stream dataStream);

        /// <summary>
        /// Asynchronously verifies that the <paramref name="hash"/> of a <paramref name="dataStream" /> is correct.
        /// </summary>
        /// <param name="dataStream">The data stream.</param>
        /// <param name="hash">The hash to verify, optionally prepended with the salt.</param>
        /// <returns>
        /// A <see cref="T:Task{bool}"/> object representing the process and the verification result:
        /// <see langword="true"/>
        /// if <paramref name="hash"/> is correct or if both <paramref name="dataStream" /> and <paramref name="hash"/> are <see langword="null" />, 
        /// otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown if <paramref name="dataStream" /> is not <see langword="null" /> and <paramref name="hash"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="System.ArgumentException">Thrown if <paramref name="hash"/> has invalid length.</exception>
        /// <exception cref="T:System.Security.Cryptography.CryptographicException">The hash or the encryption failed.</exception>
        Task<bool> TryVerifyHashAsync(Stream dataStream, byte[] hash);
    }
}
