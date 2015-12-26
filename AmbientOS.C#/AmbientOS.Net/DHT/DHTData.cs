using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.Net.DHT
{
    /// <summary>
    /// Represents mutable or immutable data stored in the DHT (as specified in BEP44).
    /// None of the members should be considered thread-safe.
    /// </summary>
    public class DHTData
    {
        public BigInt Hash { get; }
        public byte[] PublicKey { get; }
        public byte[] Salt { get; }

        public long? SequenceNumber { get; private set; }
        public byte[] Data { get; private set; }
        public byte[] Signature { get; private set; }

        public event Action DataChangedCallback;

        private Func<byte[], byte[], Tuple<byte[], byte[]>> merge;

        private long? lastCallbackInvokation = null;

        /// <summary>
        /// Creates an immutable data item from existing data.
        /// The data is hashed automatically.
        /// </summary>
        public DHTData(byte[] immutableData)
        {
            Hash = ComputeHash(immutableData);
            Data = immutableData;
        }

        ///// <summary>
        ///// Creates a mutable data item from existing data and key pair.
        ///// The data is signed automatically.
        ///// </summary>
        //public DHTData(byte[] mutableData, byte[] publicKey, byte[] salt, byte[] expandedPrivateKey)
        //{
        //    Hash = ComputeHash(mutableData, salt);
        //    Data = mutableData;
        //    PublicKey = publicKey;
        //    Signature = Sign(mutableData, salt, expandedPrivateKey);
        //    SequenceNumber = null;
        //}

        /// <summary>
        /// Creates a mutable data item from existing data and signature.
        /// </summary>
        public DHTData(byte[] mutableData, byte[] publicKey, byte[] salt, byte[] signature, long? sequenceNumber)
        {
            Hash = ComputeHash(publicKey, salt);
            Data = mutableData;
            PublicKey = publicKey;
            Salt = salt;
            SequenceNumber = sequenceNumber;
            Signature = signature;
        }

        /// <summary>
        /// Creates a mutable data item from existing data.
        /// </summary>
        private static DHTData FromExistingMutableData(byte[] mutableData, byte[] publicKey, byte[] salt, byte[] expandedPrivateKey, long? sequenceNumber)
        {
            var data = new DHTData(mutableData, publicKey, salt, null, sequenceNumber);
            data.Signature = Chaos.NaCl.Ed25519.Sign(data.ComputeSignableValue(), expandedPrivateKey);
            return data;
        }

        /// <summary>
        /// Creates an empty immutable data item from a hash.
        /// Before this data item is usful, you must call Apply.
        /// </summary>
        public DHTData(BigInt hash)
        {
            Hash = hash;
        }

        /// <summary>
        /// Creates an empty mutable data item from a public key and salt.
        /// </summary>
        public DHTData(byte[] publicKey, byte[] salt)
        {
            Hash = ComputeHash(publicKey, salt);
            PublicKey = publicKey;
            Salt = salt;
        }

        /// <summary>
        /// Sets up a merge function for the data.
        /// </summary>
        public void Setup(byte[] expandedPrivateKey, Func<byte[], byte[], byte[]> merge)
        {
            this.merge = (data1, data2) => {
                return new Tuple<byte[], byte[]>(merge(data1, data2), expandedPrivateKey);
            };
        }

        /// <summary>
        /// Applies the new data to the current data after comparing their hashes and sequence numbers.
        /// If the data has a sequence number, the new data is only accepted if it has a newer sequence number.
        /// If the hashes are equal, this implies that the new data is valid.
        /// If the data is applied and the data item was set up with a merge function, the old and new data is merged.
        /// This is not thread-safe and does not trigger the DataChanged event.
        /// </summary>
        public string Apply(DHTData newData)
        {
            if (SequenceNumber.HasValue) {
                if (!newData.SequenceNumber.HasValue)
                    return "cannot disable sequence number";
                else if (newData.SequenceNumber.Value <= SequenceNumber.Value)
                    return "cannot reverse sequence number";
            }

            var verifyError = newData.Verify();
            if (verifyError != null)
                return verifyError;

            if (Hash != newData.Hash)
                return "hash not equal";
            
            if (Data != null && merge != null) {
                var mergeResult = merge(Data, newData.Data);
                SequenceNumber = newData.SequenceNumber + (mergeResult.Item1.SequenceEqual(newData.Data) ? 0 : 1);
                Data = mergeResult.Item1;
                Signature = Chaos.NaCl.Ed25519.Sign(ComputeSignableValue(), mergeResult.Item2);
            } else {
                SequenceNumber = newData.SequenceNumber;
                Data = newData.Data;
                Signature = newData.Signature;
            }

            return null;
        }

        /// <summary>
        /// Applies the specified data to this data item.
        /// </summary>
        /// <param name="lastSequenceNumber">The last known sequence number of this data item. This enables atomic updates. If there is a mismatch, the method does nothing and returns false.</param>
        public bool Apply(byte[] newData, byte[] expandedPrivateKey, long? lastSequenceNumber)
        {
            if (SequenceNumber.HasValue && lastSequenceNumber.HasValue)
                if (lastSequenceNumber != SequenceNumber.Value)
                    return false;

            Apply(FromExistingMutableData(newData, PublicKey, Salt, expandedPrivateKey, (SequenceNumber ?? -1) + 1));

            return true;
        }

        private string Verify()
        {
            if (PublicKey != null && Signature != null && Salt != null) {
                if (ComputeHash(PublicKey, Salt) != Hash)
                    return "invalid hash of mutable DHT value";
                if (!Chaos.NaCl.Ed25519.Verify(Signature, ComputeSignableValue(), PublicKey))
                    return "invalid signature of mutable DHT value";
            } else {
                if (ComputeHash(Data) != Hash)
                    return "invalid hash of immutable DHT value";
            }
            return null;
        }

        /// <summary>
        /// Triggers the event if the data has changed since the last invokation.
        /// </summary>
        public void MaybeInvokeCallback()
        {
            if ((!lastCallbackInvokation.HasValue && Data != null) || lastCallbackInvokation < SequenceNumber) {
                lastCallbackInvokation = SequenceNumber ?? 0;
                DataChangedCallback.SafeInvoke();
            }
        }

        /// <summary>
        /// Computes a signable byte array according to the specification (BEP44)
        /// </summary>
        private byte[] ComputeSignableValue()
        {
            IEnumerable<BEncode> signable = new BEncode[] {
                new BString("seq"), new BInt(SequenceNumber.Value),
                new BString("v"), new BString(Data)
            };

            if (Salt?.Count() > 0)
                signable = new BEncode[] { new BString("salt"), new BString(Salt) }.Concat(signable);

            return signable.SelectMany(val => val.Encode()).ToArray();
        }


        public static BigInt ComputeHash(byte[] value)
        {
            var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider();
            return new BigInt(sha1.ComputeHash(value), Endianness.NetworkByteOrder);
        }

        public static BigInt ComputeHash(byte[] publicKey, byte[] salt)
        {
            return ComputeHash(publicKey.Concat(salt).ToArray());
        }

        //public static bool Validate(byte[] value, Hash hash)
        //{
        //    return hash == ComputeHash(value);
        //}

        //public static bool Validate(byte[] value, byte[] salt, byte[] publicKey, byte[] signature)
        //{
        //    return Chaos.NaCl.Ed25519.Verify(signature, ComputeSignableValue(value, salt), publicKey);
        //}
    }
}
