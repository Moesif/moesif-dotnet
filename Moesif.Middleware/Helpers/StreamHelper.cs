using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Moesif.Middleware.Helpers
{
    public class StreamHelper : Stream
    {
        private readonly Stream InnerStream;
        // Using lazy initialization
        // private Lazy<MemoryStream> _lazyCopyStream;
        // public MemoryStream CopyStream => _lazyCopyStream.Value;
        public MemoryStream CopyStream;

        public StreamHelper(Stream inner)
        {
            this.InnerStream = inner;
            this.CopyStream = new MemoryStream();
            // Use lazy initialization
            // this._lazyCopyStream = new Lazy<MemoryStream>(() => new MemoryStream());
        }

        public string ReadStream(string contentEncoding)
        {
            lock (this.InnerStream)
            {
                if (this.CopyStream.Length <= 0L ||
                    !this.CopyStream.CanRead ||
                    !this.CopyStream.CanSeek)
                {
                    return String.Empty;
                }

                long pos = this.CopyStream.Position;
                this.CopyStream.Position = 0L;
                try
                {
                    if (contentEncoding != null && contentEncoding.ToLower().Contains("gzip"))
                    {
                        try
                        {
                            using (GZipStream decompressedStream = new GZipStream(this.CopyStream, CompressionMode.Decompress))
                            {
                                return new StreamReader(decompressedStream).ReadToEnd();
                            }
                        }
                        catch
                        {
                            return new StreamReader(this.CopyStream).ReadToEnd();
                        }
                    }
                    return new StreamReader(this.CopyStream).ReadToEnd();
                }
                finally
                {
                    try
                    {
                        this.CopyStream.Position = pos;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(this.GetType().ToString() + "Cannot read stream " + e.ToString());
                    }
                }
            }
        }


        public override bool CanRead
        {
            get { return this.InnerStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return this.InnerStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return this.InnerStream.CanWrite; }
        }

        public override void Flush()
        {
            try {
                this.InnerStream.Flush();
            } catch (Exception e) {
                Console.WriteLine(this.GetType().ToString() + "Error flushing stream: " + e.ToString()
                + "\n See troubleshooting: https://github.com/Moesif/moesif-dotnet#the-response-body-is-not-logged-or-calls-are-not-recieved-in-moesif");
            }
        }

        public override long Length
        {
            get { return this.InnerStream.Length; }
        }

        public override long Position
        {
            get { return this.InnerStream.Position; }
            set { this.CopyStream.Position = this.InnerStream.Position = value; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.InnerStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            this.CopyStream.Seek(offset, origin);
            return this.InnerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.CopyStream.SetLength(value);
            this.InnerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            try {
                this.CopyStream.Write(buffer, offset, count);
                this.InnerStream.Write(buffer, offset, count);
            } catch (Exception e) {
                Console.WriteLine(this.GetType().ToString() + "Error writing stream: " + e.ToString()
                + "\n See troubleshooting: https://github.com/Moesif/moesif-dotnet#the-response-body-is-not-logged-or-calls-are-not-recieved-in-moesif");
            }
        }
    }
}
