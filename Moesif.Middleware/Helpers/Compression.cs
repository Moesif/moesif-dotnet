using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Moesif.Middleware.Helpers
{
    class Compression
    {
       public static string UncompressStream(Stream memoryStream, string contentEncoding, int bufferSize)
        {
            if (!memoryStream.CanRead)
            {
                return null;
            }
            string bodyString;
            bufferSize = Math.Max(bufferSize, 1000);
            if (contentEncoding != null && contentEncoding.ToLower().Contains("gzip"))
            {
                try
                {
                    using (GZipStream decompressedStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                    {
                        using (StreamReader readStream = new StreamReader(decompressedStream,
                            encoding: Encoding.UTF8,
                            detectEncodingFromByteOrderMarks: false,
                            bufferSize: bufferSize,
                            leaveOpen: true))
                        {
                            bodyString = readStream.ReadToEnd();
                        }
                    }
                }
                catch
                {
                    using (StreamReader readStream = new StreamReader(memoryStream,
                            encoding: Encoding.UTF8,
                            detectEncodingFromByteOrderMarks: false,
                            bufferSize: bufferSize,
                            leaveOpen: true))
                    {
                        bodyString = readStream.ReadToEnd();
                    }
                }
            }
            else
            {
                using (StreamReader readStream = new StreamReader(memoryStream,
                            encoding: Encoding.UTF8,
                            detectEncodingFromByteOrderMarks: false,
                            bufferSize: bufferSize,
                            leaveOpen: true))
                {
                    bodyString = readStream.ReadToEnd();
                }
            }

            return bodyString;
        }
    }
}
