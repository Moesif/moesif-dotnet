using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Serialization;
using Moesif.Middleware.Helpers;

#if NET45
using Microsoft.Owin;
using Moesif.Middleware.NetFramework.Helpers;

namespace Moesif.Middleware.NetFramework.Helpers
{
    public static class LoggerHelper
    {
        public static Dictionary<string, string> AddTransactionId(string headerName, string transactionId, Dictionary<string, string> headers)
        {
            if (!string.IsNullOrEmpty(transactionId))
            {
                headers[headerName] = transactionId;
            }
            return headers;
        }

        public static bool GetConfigBoolValues(Dictionary<string, object> moesifOptions, String configName, bool defaultValue) 
        {
            var config_out = new object();
            var getConfigOption = moesifOptions.TryGetValue(configName, out config_out);
            return getConfigOption ? Convert.ToBoolean(config_out) : defaultValue;
        }

        public static int GetConfigIntValues(Dictionary<string, object> moesifOptions, String configName, int defaultValue)
        {
            var config_out = new object();
            var getConfigOption = moesifOptions.TryGetValue(configName, out config_out);
            return getConfigOption ? Convert.ToInt32(config_out) : defaultValue; 
        }

        public static Dictionary<string, object> GetConfigObjectValues(string configName, Dictionary<string, object> moesifOptions, IOwinRequest request, IOwinResponse response, bool debug)
        {
            var object_out = new object();
            var getObject = moesifOptions.TryGetValue(configName, out object_out);

            Func<IOwinRequest, IOwinResponse, Dictionary<string, object>> GetObject = null;

            if (getObject)
            {
                GetObject = (Func<IOwinRequest, IOwinResponse, Dictionary<string, object>>)(object_out);
            }

            Dictionary<string, object> objectValue = null;
            if (GetObject != null)
            {
                try
                {
                    objectValue = GetObject(request, response);
                }
                catch
                {
                    LogDebugMessage(debug, "Can not execute" + configName + "function. Please check moesif settings.");
                }
            }
            return objectValue;
        }

        public static string GetConfigStringValues(string configName, Dictionary<string, object> moesifOptions, IOwinRequest request, IOwinResponse response, bool debug, string value = null)
        {
            var string_out = new object();
            var getStringValue = moesifOptions.TryGetValue(configName, out string_out);

            Func<IOwinRequest, IOwinResponse, string> GetValue = null;
            if (getStringValue)
            {
                GetValue = (Func<IOwinRequest, IOwinResponse, string>)(string_out);
            }

            if (GetValue != null)
            {
                try
                {
                    value = GetValue(request, response);
                }
                catch
                {
                    LogDebugMessage(debug, "Can not execute" + configName + "function. Please check moesif settings.");
                }
            }
            return value;
        }

        public static string GetOrCreateTransactionId(IDictionary<string, string> headers, string headerName)
        {
            string transactionId;
            if (headers.ContainsKey(headerName))
            {
                string reqTransId = headers[headerName];
                if (!string.IsNullOrEmpty(reqTransId))
                {
                    transactionId = reqTransId;
                }
                else
                {
                    transactionId = Guid.NewGuid().ToString();
                }
            }
            else
            {
                transactionId = Guid.NewGuid().ToString();
            }
            return transactionId;
        }

        public static string GetOutputFilterStreamContents(StreamHelper filter, string contentEncoding)
        {
            if (filter != null)
            {
                return filter.ReadStream(contentEncoding);
            }
            return null;
        }

        public async static Task<string> GetRequestContents(IOwinRequest request, string contentEncoding)
        {
            string requestBody;
            if (request == null || request.Body == null || !request.Body.CanRead)
            {
                return string.Empty;
            }

            var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream);
            memoryStream.Seek(0L, SeekOrigin.Begin);
            request.Body = memoryStream;

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

        public static void LogDebugMessage(bool debug, String msg) 
        {
            if (debug)
            {
                Console.WriteLine(msg);
            }
        }

        public static Tuple<object, string> Serialize(string data, string contentType)
        {
            if (string.IsNullOrEmpty(data))
            {
                return new Tuple<object, string>(null, null);
            }

            // Only try parse if is JSON or looks like JSON
            if (contentType.ToLower().Contains("json") || data.StartsWith("{") || data.StartsWith("["))
            {
                try
                {
                    return new Tuple<object, string>(JToken.Parse(data), null);
                }
                catch (Exception e)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(data);
                    var base64 = System.Convert.ToBase64String(bytes);
                    return new Tuple<object, string>(base64, "base64");
                }
            }
            else
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(data);
                var base64 = System.Convert.ToBase64String(bytes);
                return new Tuple<object, string>(base64, "base64");
            }
        }

        public static Dictionary<string, string> ToHeaders(IDictionary<string, string[]> headers, bool debug)
        {
            var copyHeaders = new Dictionary<string, string>();
            try
            {
                copyHeaders = headers.ToLookup(k => k.Key, k => string.Join(",", k.Value.Distinct()), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(k => k.Key, k => string.Join(",", k.ToList().Distinct()), StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception inst)
            {
                LogDebugMessage(debug, "Error encountered while copying header");
            }
            return copyHeaders;
        }
    }
}
#endif