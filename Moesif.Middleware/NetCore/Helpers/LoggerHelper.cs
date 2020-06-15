using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

#if NETCORE
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace Moesif.Middleware.NetCore.Helpers
{
    class LoggerHelper
    {
        public async static Task<string> GetRequestContents(HttpRequest request, string contentEncoding)
        {
            string requestBody;
            if (request == null || request.Body == null || !request.Body.CanSeek)
            {
                return string.Empty;
            }

            request.Body.Seek(0L, SeekOrigin.Begin);
            var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream);
            memoryStream.Seek(0L, SeekOrigin.Begin);
            request.Body = memoryStream;
            request.HttpContext.Response.RegisterForDispose(memoryStream);

            if (contentEncoding != null && contentEncoding.ToLower().Contains("gzip"))
            {
                try
                {
                    using (GZipStream decompressedStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                    {
                        using (StreamReader readStream = new StreamReader(decompressedStream))
                        {
                            requestBody = readStream.ReadToEnd();
                        }
                    }
                }
                catch
                {
                    using (StreamReader readStream = new StreamReader(memoryStream))
                    {
                        requestBody = readStream.ReadToEnd();
                    }
                }
            }
            else
            {
                using (StreamReader readStream = new StreamReader(memoryStream))
                {
                    requestBody = readStream.ReadToEnd();
                }
            }

            return requestBody;
        }
    }
}
#endif