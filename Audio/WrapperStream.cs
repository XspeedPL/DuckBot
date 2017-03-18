using System.IO;

namespace DuckBot.Audio
{
    public class WrapperStream : Stream
    {
        private const int BUFFER_SIZE = 16384;
        private int position;

        public Stream BaseStream { get; private set; }

        public WrapperStream(Stream source)
        {
            BaseStream = new BufferedStream(source, BUFFER_SIZE);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int ret = 0;
            while (ret < count)
            {
                int read = BaseStream.Read(buffer, offset, count);
                ret += read;
                if (read == 0) break;
            }
            if (ret == 0) throw new EndOfStreamException();
            position += ret;
            return ret;
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) { return position; }

        public override void Write(byte[] buffer, int offset, int count) { }

        public override void SetLength(long value) { }

        public override long Length => position;

        public override long Position
        {
            get { return position; }
            set { }
        }

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            BaseStream.Dispose();
        }
    }
}
