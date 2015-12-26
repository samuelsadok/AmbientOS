using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;

namespace AmbientOS.Net.DHT
{
    /// <summary>
    /// A token is an opaque value that is distributed to and returned by querying hosts.
    /// It serves some security purpose (which?), so it must not be forgeable.
    /// The value is opaque and irrelevant to other nodes, so the implementation is not defined.
    /// Our version of the token is the hash of both the ip address and a periodically renewed secret. This is similar to the token used by BitTorrent.
    /// </summary>
    class Token
    {
        private static HashAlgorithm hashAlgorithm = new SHA256Managed();
        private static RandomNumberGenerator rng = new RNGCryptoServiceProvider();

        /// <summary>
        /// Specifies the duration after which the secret is renewed.
        /// A token is valid for at least this timespan and can be valid up to twice this time span.
        /// BitTorrent uses 5 minutes.
        /// </summary>
        private static readonly TimeSpan UPDATE_INTERVAL = TimeSpan.FromMinutes(5);
        private static object secretLockRef = new object();
        private static DateTime lastSecretUpdate = new DateTime(0);
        private static byte[] acceptSecret = new byte[256];
        private static byte[] currentSecret = new byte[256];

        /// <summary>
        /// Ensures that the accepted secret and current secret are not too old and renews them if neccessary.
        /// This should always be called before using acceptSecret and currentSecret.
        /// </summary>
        private static void UpdateSecret()
        {
            lock (secretLockRef) {
                var age = DateTime.Now - lastSecretUpdate;
                DateTime newUpdateTime;

                if (age < UPDATE_INTERVAL) {
                    // secret is still young - don't update
                    return;
                } else if (age < UPDATE_INTERVAL + UPDATE_INTERVAL) {
                    // secret is too old but can still be accepted, so store it in LastSecret
                    acceptSecret = currentSecret;
                    newUpdateTime = lastSecretUpdate + UPDATE_INTERVAL;
                } else {
                    // secret is very old, renew everything
                    rng.GetBytes(acceptSecret);
                    newUpdateTime = DateTime.Now;
                }
                rng.GetBytes(currentSecret);
                lastSecretUpdate = newUpdateTime;
            }
        }


        /// <summary>
        /// This is what our own tokens look like before being hashed.
        /// </summary>
        [Endianness(Endianness.LittleEndian)]
        private class OwnOpaqueToken
        {
            [FieldSpecs(LengthOf = "ipAddress")]
            protected int ipAddressLength = 0;

            [FieldSpecs(LengthOf = "hash")]
            protected int hashLength = 0;

            [FieldSpecs(LengthOf = "secret")]
            protected int secretLength = 0;

            [FieldSpecs(StringFormat = StringFormat.Unicode)]
            public string ipAddress;

            [FieldSpecs(Length = 20)]
            public byte[] hash;

            public byte[] secret;
        }


        /// <summary>
        /// Generates a token from the specified IP address and secret.
        /// </summary>
        private static byte[] GenerateToken(IPAddress addess, BigInt hash, byte[] secret)
        {
            var buffer = ByteConverter.WriteVal(new OwnOpaqueToken() {
                ipAddress = addess.ToString(),
                hash = hash.GetBytes(20, Endianness.NetworkByteOrder),
                secret = secret
            });
            return hashAlgorithm.ComputeHash(buffer);
        }


        public byte[] Value { get; }
        
        /// <summary>
        /// Generates a token from the specified raw data.
        /// </summary>
        public Token(byte[] value)
        {
            Value = value;
        }

        /// <summary>
        /// Generates a token from the current secret for the specified IP address.
        /// The token may optionally be associated with a hash value.
        /// </summary>
        public Token(IPAddress address, BigInt hash = null)
        {
            if (hash == null)
                hash = BigInt.Zero;

            lock (secretLockRef) {
                UpdateSecret();
                Value = GenerateToken(address, hash, currentSecret);
            }
        }

        /// <summary>
        /// Validates if this token belongs the specified IP address.
        /// </summary>
        public bool Validate(IPAddress address, BigInt hash = null)
        {
            if (hash == null)
                hash = BigInt.Zero;

            lock (secretLockRef) {
                if (Value.SequenceEqual(GenerateToken(address, hash, currentSecret)))
                    return true;
                return Value.SequenceEqual(GenerateToken(address, hash, acceptSecret));
            }
        }
    }
}
