using System;
using System.IO;
using System.Text;
using System.Linq;
using Moesif.Api;
using Moesif.Api.Models;
using Moesif.Api.Exceptions;
using System.Threading.Tasks;
using System.Collections.Generic;
using Moesif.Middleware.Helpers;


#if NETCORE
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Moesif.Middleware.NetCore.Helpers;
#endif

#if NETCORE
namespace Moesif.Middleware.NetCore.Helpers
{
    public class LoggerHelper
    {
        public async static void LogDebugMessage(bool debug, String msg)
        {
            if (debug)
            {
                await Console.Out.WriteLineAsync(msg);
            }
        }

        public static string GetConfigStringValues(Dictionary<string, object> moesifOptions, String configName, string defaultValue)
        {
            var config_out = new object();
            var getConfigOption = moesifOptions.TryGetValue(configName, out config_out);
            return getConfigOption ? Convert.ToString(config_out) : defaultValue;
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

        public static Dictionary<string, object> GetConfigObjectValues(string configName, Dictionary<string, object> moesifOptions, HttpRequest request, HttpResponse response, bool debug)
        {
            var object_out = new object();
            var getObject = moesifOptions.TryGetValue(configName, out object_out);

            Func<HttpRequest, HttpResponse, Dictionary<string, object>> GetObject = null;

            if (getObject)
            {
                GetObject = (Func<HttpRequest, HttpResponse, Dictionary<string, object>>)(object_out);
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

        public static string GetConfigValues(string configName, Dictionary<string, object> moesifOptions, HttpRequest request, HttpResponse response, bool debug, string value = null)
        {
            var string_out = new object();
            var getStringValue = moesifOptions.TryGetValue(configName, out string_out);

            Func<HttpRequest, HttpResponse, string> GetValue = null;
            if (getStringValue)
            {
                GetValue = (Func<HttpRequest, HttpResponse, string>)(string_out);
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

        public static string GetOrCreateTransactionId(IDictionary<string, string> headers, string headerName)
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

        public static Dictionary<string, string> AddTransactionId(string headerName, string transactionId, Dictionary<string, string> headers)
        {
            if (!string.IsNullOrEmpty(transactionId))
            {
                headers[headerName] = transactionId;
            }
            return headers;
        }

        public static Dictionary<string, string> ToHeaders(IHeaderDictionary headers, bool debug)
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

        public static async Task<string> GetRequestContents(string bodyAsText, HttpRequest request, string contentEncoding, int parsedContentLength, bool debug)
        {
            try
            {
                bodyAsText = await Compression.UncompressStream(request.Body, contentEncoding, parsedContentLength);
                request.Body.Position = 0;
            }
            catch (Exception)
            {
                LoggerHelper.LogDebugMessage(debug, "Error encountered while copying request body");
            }
            return bodyAsText;
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static Tuple<object, string> Serialize(string data, string contentType, bool logBody, bool debug)
        {
            if (string.IsNullOrEmpty(data))
            {
                return new Tuple<object, string>(null, null);
            }

            var reqBody = new object();
            string requestTransferEncoding = null;
            if (logBody)
            {
                try
                {
                    // Only try parse if is JSON or looks like JSON
                    if (contentType != null && contentType.ToLower().Contains("json") || data.StartsWith("{") || data.StartsWith("["))
                    {
                        reqBody = ApiHelper.JsonDeserialize<object>(data);
                        requestTransferEncoding = "json";
                    }
                    else
                    {
                        LoggerHelper.LogDebugMessage(debug, "About to parse Request body as Base64 encoding");
                        reqBody = Base64Encode(data);
                        requestTransferEncoding = "base64";
                    }
                }
                catch (Exception)
                {
                    LoggerHelper.LogDebugMessage(debug, "About to parse Request body as Base64 encoding");
                    reqBody = Base64Encode(data);
                    requestTransferEncoding = "base64";
                }
            }

            return new Tuple<object, string>(reqBody, requestTransferEncoding);
        }
    }
}
#endif
