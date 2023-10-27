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
using Microsoft.Extensions.Logging;

#if NET461
using Microsoft.Owin;
using Moesif.Middleware.NetFramework.Helpers;

namespace Moesif.Middleware.NetFramework.Helpers
{
    public class LoggerHelper
    {
        private ILogger _logger;

        public LoggerHelper(ILogger logger)
        {
            _logger = logger;
        }
        public Dictionary<string, string> AddTransactionId(string headerName, string transactionId, Dictionary<string, string> headers)
        {
            if (!string.IsNullOrEmpty(transactionId))
            {
                headers[headerName] = transactionId;
            }
            return headers;
        }

        public  string GetConfigStringValues(Dictionary<string, object> moesifOptions, String configName, string defaultValue)
        {
            var config_out = new object();
            var getConfigOption = moesifOptions.TryGetValue(configName, out config_out);
            return getConfigOption ? Convert.ToString(config_out) : defaultValue;
        }

        public  bool GetConfigBoolValues(Dictionary<string, object> moesifOptions, String configName, bool defaultValue) 
        {
            var config_out = new object();
            var getConfigOption = moesifOptions.TryGetValue(configName, out config_out);
            return getConfigOption ? Convert.ToBoolean(config_out) : defaultValue;
        }

        public int GetConfigIntValues(Dictionary<string, object> moesifOptions, String configName, int defaultValue)
        {
            var config_out = new object();
            var getConfigOption = moesifOptions.TryGetValue(configName, out config_out);
            return getConfigOption ? Convert.ToInt32(config_out) : defaultValue; 
        }

        public Dictionary<string, object> GetConfigObjectValues(string configName, Dictionary<string, object> moesifOptions, IOwinRequest request, IOwinResponse response, bool debug)
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

        public  string GetConfigValues(string configName, Dictionary<string, object> moesifOptions, IOwinRequest request, IOwinResponse response, bool debug)
        {
            var string_out = new object();
            var getStringValue = moesifOptions.TryGetValue(configName, out string_out);

            Func<IOwinRequest, IOwinResponse, string> GetValue = null;
            if (getStringValue)
            {
                GetValue = (Func<IOwinRequest, IOwinResponse, string>)(string_out);
            }
            string value = null;

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

        public  string GetOrCreateTransactionId(IDictionary<string, string> headers, string headerName)
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

        public string GetOutputFilterStreamContents(StreamHelper filter, string contentEncoding)
        {
            if (filter != null)
            {
                return filter.ReadStream(contentEncoding);
            }
            return null;
        }

        public async  Task<string> GetRequestContents(IOwinRequest request, string contentEncoding, int bufferSize, bool disableStreamOverride)
        {
            if (request == null || request.Body == null || !request.Body.CanRead)
            {
                return string.Empty;
            }

            var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream);
            memoryStream.Seek(0L, SeekOrigin.Begin);
            if (disableStreamOverride) {
                request.Body.Position = 0;
            }
            else {
                request.Body = memoryStream;
            }
            return await Compression.UncompressStream(memoryStream, contentEncoding, bufferSize);
        }

        public async  void LogDebugMessage(bool debug, String msg) 
        {
            if (debug)
            {
                await Console.Out.WriteLineAsync(msg);
            }
        }

        public Tuple<object, string> Serialize(string data, string contentType)
        {
            if (string.IsNullOrEmpty(data))
            {
                return new Tuple<object, string>(null, null);
            }

            // Only try parse if is JSON or looks like JSON
            if (contentType != null && contentType.ToLower().Contains("json") || data.StartsWith("{") || data.StartsWith("["))
            {
                try
                {
                    return new Tuple<object, string>(JToken.Parse(data), null);
                }
                catch (Exception)
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

        public  Dictionary<string, string> ToHeaders(IDictionary<string, string[]> headers, bool debug)
        {
            var copyHeaders = new Dictionary<string, string>();
            try
            {
                copyHeaders = headers.ToLookup(k => k.Key, k => string.Join(",", k.Value.Distinct()), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(k => k.Key, k => string.Join(",", k.ToList().Distinct()), StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                LogDebugMessage(debug, "Error encountered while copying header");
            }
            return copyHeaders;
        }
    }
}
#endif