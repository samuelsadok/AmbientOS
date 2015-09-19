using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace AppInstall.Framework
{
    /// <summary>
    /// Represents a concatenation of several streams.
    /// Do not use a stream directly while it is in use by a SegmentedStream instance.
    /// </summary>
    public class SegmentedStream : Stream
    {
        private long position = 0;
        private int currentSegment = 0;
        private readonly Tuple<Stream, long, long>[] segments; // stream - offset - length

        /// <summary>
        /// Constructs a concatenation of the specified streams, taking only a limited number of bytes from each stream. The Position of each stream at construction time of this instance is considered its beginning.
        /// </summary>
        public SegmentedStream(params Tuple<Stream, long>[] segments)
        {
            this.segments = segments.Select((s) => new Tuple<Stream, long, long>(s.Item1, s.Item1.Position, s.Item2)).ToArray();
        }

        /// <summary>
        /// Constructs a concatenation of the specified streams
        /// </summary>
        public SegmentedStream(params Stream[] segments)
            : this(segments.Select((s) => new Tuple<Stream, long>(s, s.Length)).ToArray())
        {
        }

        public SegmentedStream(Stream segment1, long length1, Stream segment2, long length2)
            : this(new Tuple<Stream, long>(segment1, length1), new Tuple<Stream, long>(segment2, length2))
        {
        }

        public SegmentedStream(Stream segment1, long length1, Stream segment2, long length2, Stream segment3, long length3)
            : this(new Tuple<Stream, long>(segment1, length1), new Tuple<Stream, long>(segment2, length2), new Tuple<Stream, long>(segment3, length3))
        {
        }


        public override bool CanRead { get { return segments.All((s) => s.Item1.CanRead); } }
        public override bool CanSeek { get { return segments.All((s) => s.Item1.CanSeek); } }
        public override bool CanWrite { get { return segments.All((s) => s.Item1.CanWrite); } }
        public override bool CanTimeout { get { return segments.Any((s) => s.Item1.CanTimeout); } }
        public override long Length { get { return (from s in segments select s.Item3).Sum(); } }
        public override long Position
        {
            get { return position; }
            set
            {
                position = value;
                for (currentSegment = 0; value >= segments[currentSegment].Item3; currentSegment++)
                    value -= segments[currentSegment].Item3;
                segments[currentSegment].Item1.Seek(segments[currentSegment].Item2 + (long)value, SeekOrigin.Begin);
            }
        }

        public override void Flush()
        {
            foreach (var s in segments)
                s.Item1.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Current) offset += Position;
            if (origin == SeekOrigin.End) offset += Length;
            return Position = offset;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = segments[currentSegment].Item2 + segments[currentSegment].Item3 - segments[currentSegment].Item1.Position;
            if ((long)count > remaining) count = (int)Math.Min((long)int.MaxValue, remaining);
            segments[currentSegment].Item1.Read(buffer, offset, count);
            if ((long)count == remaining) currentSegment++;
            return count;
        }

        private int WriteEx(byte[] buffer, int offset, int count)
        {
            long remaining = segments[currentSegment].Item2 + segments[currentSegment].Item3 - segments[currentSegment].Item1.Position;
            if ((long)count > remaining) count = (int)Math.Min((long)int.MaxValue, remaining);
            segments[currentSegment].Item1.Write(buffer, offset, count);
            if ((long)count == remaining) currentSegment++;
            return count;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            while (count > 0) {
                int written = WriteEx(buffer, offset, count);
                offset += written;
                count -= written;
            }
        }

        public override int ReadByte()
        {
            var result = segments[currentSegment].Item1.ReadByte();
            if (segments[currentSegment].Item1.Position == segments[currentSegment].Item2 + segments[currentSegment].Item3)
                currentSegment++;
            return result;
        }

        public override void WriteByte(byte value)
        {
            segments[currentSegment].Item1.WriteByte(value);
            if (segments[currentSegment].Item1.Position == segments[currentSegment].Item2 + segments[currentSegment].Item3)
                currentSegment++;
        }
    }
}
