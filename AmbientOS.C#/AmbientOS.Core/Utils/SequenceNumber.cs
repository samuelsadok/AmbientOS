using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS
{
    public struct SequenceNumber
    {
        readonly ulong val;

        public bool HasValue { get { return val != ulong.MaxValue; } }

        private SequenceNumber(ulong val)
        {
            this.val = val;
        }

        public SequenceNumber Increment()
        {
            if (val == ulong.MaxValue)
                throw new InvalidOperationException();
            var newVal = val + 1;
            return new SequenceNumber(newVal == ulong.MaxValue ? ulong.MinValue : newVal);
        }

        public static readonly SequenceNumber Zero = new SequenceNumber(ulong.MinValue);
        public static readonly SequenceNumber None = new SequenceNumber(ulong.MaxValue);

        public static bool operator ==(SequenceNumber seqNo1, SequenceNumber seqNo2)
        {
            return seqNo1.val == seqNo2.val;
        }

        public static bool operator !=(SequenceNumber seqNo1, SequenceNumber seqNo2)
        {
            return seqNo1.val != seqNo2.val;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SequenceNumber))
                return false;
            return this == (SequenceNumber)obj;
        }

        public override int GetHashCode()
        {
            return val.GetHashCode();
        }
    }
}
