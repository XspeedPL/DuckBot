using System;
using System.IO;

namespace DuckBot.Audio
{
    public class ReadAheadStream : Stream
    {
        private readonly Stream sourceStream;
        private readonly byte[] readAheadBuffer;
        private long pos;
        private int aheadLength, aheadOffset;

        public ReadAheadStream(Stream sourceStream)
        {
            this.sourceStream = sourceStream;
            readAheadBuffer = new byte[4096];
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => pos;

        public override long Position
        {
            get { return pos; }
            set { throw new InvalidOperationException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            while (bytesRead < count)
            {
                int readAheadAvailableBytes = aheadLength - aheadOffset;
                int bytesRequired = count - bytesRead;
                if (readAheadAvailableBytes > 0)
                {
                    int toCopy = Math.Min(readAheadAvailableBytes, bytesRequired);
                    Array.Copy(readAheadBuffer, aheadOffset, buffer, offset + bytesRead, toCopy);
                    bytesRead += toCopy;
                    aheadOffset += toCopy;
                }
                else
                {
                    aheadOffset = 0;
                    aheadLength = sourceStream.Read(readAheadBuffer, 0, readAheadBuffer.Length);
                    if (aheadLength == 0) break;
                }
            }
            pos += bytesRead;
            return bytesRead;
        }

        public override void Flush()
        {
            throw new InvalidOperationException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        protected override void Dispose(bool disposing)
        {
            sourceStream.Dispose();
        }
    }
}
