using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security;
using System.Security.Cryptography;
//using Chaos.NaCl;

namespace AmbientOS.Net
{
    public class SecurityManager : IDisposable
    {
        private byte[] IdentityExpandedPrivateKey { get; }
        public byte[] IdentityPublicKey { get; }
        public byte[] DomainExpandedPrivateKey { get; }
        public byte[] DomainPublicKey { get; }

        private readonly ICryptoTransform encryptor;
        private readonly ICryptoTransform decryptor;

        /// <summary>
        /// Generates a new security context.
        /// On first lauch, the identity is generated randomly.
        /// After this constructor, Save should be called on the config file.
        /// </summary>
        /// <param name="identity">A 32 byte array that defines the indentity of the local peer.</param>
        public SecurityManager(byte[] identity, string domainName, string domainPassword)
        {
            byte[] publicKey, expandedPrivateKey;

            if (identity == null) {
                identity = new byte[32];
                new RNGCryptoServiceProvider().GetBytes(identity);
            }

            Ed25519.KeyPairFromSeed(out publicKey, out expandedPrivateKey, identity);
            IdentityPublicKey = publicKey;
            IdentityExpandedPrivateKey = expandedPrivateKey;


            var pwd = new Rfc2898DeriveBytes(domainName + "|" + domainPassword, new byte[8]);
            var seed = pwd.GetBytes(32);

            Ed25519.KeyPairFromSeed(out publicKey, out expandedPrivateKey, seed);
            DomainPublicKey = publicKey;
            DomainExpandedPrivateKey = expandedPrivateKey;

            var aes = new AesCryptoServiceProvider() {
                KeySize = 256,
                Key = seed,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7
            };
            aes.GenerateIV();
            encryptor = aes.CreateEncryptor();
            decryptor = aes.CreateDecryptor();
        }

        /// <summary>
        /// Returns a signature for the specified bytes (using the public identity key).
        /// </summary>
        public byte[] Sign(byte[] value)
        {
            return Ed25519.Sign(value, IdentityExpandedPrivateKey);
        }

        /// <summary>
        /// Verifies a signature for the specified bytes (using the originators public identity key).
        /// </summary>
        public bool Verify(byte[] value, byte[] signature, byte[] key)
        {
            return Ed25519.Verify(signature, value, key);
        }

        /// <summary>
        /// Encrypts the given cleartext using AES-256 from System.Security.Cryptography.
        /// </summary>
        /// <param name="clearText">Text to be encrypted</param>
        /// <returns>ciphertext</returns>
        public byte[] Encrypt(byte[] clearText)
        {
            return encryptor.TransformFinalBlock(clearText, 0, clearText.Length);            
        }


        /// <summary>
        /// Decrypts the given ciphertext using the symmetric aes key.
        /// </summary>
        /// <param name="cipherText">Text to be decrypted</param>
        /// <returns>cleartext</returns>
        public byte[] Decrypt(byte[] cipherText)
        {
            return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
        }

        public void Dispose()
        {
            encryptor.Dispose();
            decryptor.Dispose();
        }
    }
}
