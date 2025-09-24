using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose
{
    /// <summary>
    /// A stream wrapper for a text writer
    /// </summary>
    public class TextWriterStream : Stream
    {
        private readonly TextWriter writer;
        private readonly Encoding encoding;
        private readonly Decoder decoder;

        private readonly byte[] singleByte = new byte[1];

        public TextWriterStream(TextWriter writer, Encoding? encoding = null)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.encoding = encoding ?? Encoding.UTF8;
            this.decoder = this.encoding.GetDecoder();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            char[] array = new char[encoding.GetMaxCharCount(count)];
            int chars = decoder.GetChars(buffer, offset, count, array, 0, true);
            writer.Write(array, 0, chars);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            char[] array = new char[encoding.GetMaxCharCount(buffer.Length)];
            int chars = decoder.GetChars(buffer, array, true);
            writer.Write(array, 0, chars);
        }

        public override void WriteByte(byte value)
        {
            singleByte[0] = value;
            Write(singleByte, 0, 1);
        }

        public override void Flush() => writer.Flush();

        public override bool CanWrite => true;
        public override bool CanRead => false;
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
