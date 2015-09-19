using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.Networking
{
    public interface INetContent {
        /// <summary>
        /// Adjusts the header to reflect special attributes this content type may have.
        /// Normally this will set attributes such as Content-Length, Content-Type or Content-Range
        /// </summary>
        void AdjustHeader(Dictionary<string, string> header);
        /// <summary>
        /// Writes the content to the network stream.
        /// </summary>
        Task WriteToStream(Stream stream, CancellationToken cancellationToken);
        /// <summary>
        /// Writes part of the content to the network stream.
        /// </summary>
        Task WriteToStream(Stream stream, long offset, long length, CancellationToken cancellationToken);
        /// <summary>
        /// Reads the content from a stream while respecting header attributes that are specific to this content type.
        /// Not all content types support this.
        /// </summary>
        Task ReadFromStream(Stream stream, Dictionary<string, string> header, CancellationToken cancellationToken);
    }

    public class BinaryContent : INetContent
    {
        public byte[] Content { get; set; }

        public BinaryContent()
        {
        }

        public BinaryContent(byte[] content)
        {
            Content = content;
        }

        /// <summary>
        /// Converts the input string to a HTML encoded byte array
        /// </summary>
        public BinaryContent(string content)
            : this(System.Text.Encoding.ASCII.GetBytes(content.EscapeForHTML()))
        {
        }

        public void AdjustHeader(Dictionary<string, string> header)
        {
            header["Content-Length"] = Content.Count().ToString();
        }

        public Task WriteToStream(Stream stream, CancellationToken cancellationToken)
        {
            return stream.Write(Content, cancellationToken);
        }

        public Task WriteToStream(Stream stream, long offset, long length, CancellationToken cancellationToken)
        {
            return stream.WriteAsync(Content, (int)offset, (int)length);
        }

        public async Task ReadFromStream(Stream stream, Dictionary<string, string> header, CancellationToken cancellationToken)
        {
            long expectedLength = long.Parse(header.GetValueOrDefault("Content-Length", "0"));
            Content = await stream.ReadBytes((int)expectedLength, cancellationToken);
            if (Content.Count() != expectedLength)
                throw new FormatException("did not receive the correct number of bytes as indicated by the Content-Length field (expected: " + expectedLength + ", got: " + Content.Count() + ")");
        }
    }

    /// <summary>
    /// Prevents the inner content from being written to the stream. The header is still altered by the inner content.
    /// </summary>
    public class SkippedContent : INetContent
    {
        public INetContent Content { get; set; }

        public SkippedContent(INetContent content)
        {
            Content = content;
        }

        public void AdjustHeader(Dictionary<string, string> header)
        {
            Content.AdjustHeader(header);
        }

        public Task WriteToStream(Stream stream, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() => { });
        }

        public Task WriteToStream(Stream stream, long offset, long length, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() => { });
        }

        public Task ReadFromStream(Stream stream, Dictionary<string, string> header, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() => {
                throw new NotImplementedException();
            });
        }
    }

    /// <summary>
    /// Writes a subrange of the inner content.
    /// </summary>
    public class PartialContent : INetContent
    {
        public INetContent Content { get; set; }
        public long Start { get; set; }
        public long Length { get; set; }

        public PartialContent(INetContent content)
        {
            Content = content;
        }

        public void AdjustHeader(Dictionary<string, string> header)
        {
            Content.AdjustHeader(header);
            header["Content-Range"] = Start + "-" + (Start + Length - 1) + "/" + header["Content-Length"];
            header["Content-Length"] = Length.ToString();
            header["Accept-Ranges"] = "bytes";
        }

        public Task WriteToStream(Stream stream, CancellationToken cancellationToken)
        {
            return Content.WriteToStream(stream, Start, Length, cancellationToken);
        }

        public Task WriteToStream(Stream stream, long offset, long length, CancellationToken cancellationToken)
        {
            return Content.WriteToStream(stream, Start + offset, length, cancellationToken);
        }

        public Task ReadFromStream(Stream stream, Dictionary<string, string> header, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() => {
                throw new NotImplementedException();
            });
        }
    }

}
