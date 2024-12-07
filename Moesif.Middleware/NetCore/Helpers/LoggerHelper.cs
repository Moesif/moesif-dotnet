//#define MOESIF_INSTRUMENT

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
using Microsoft.Extensions.Logging;
using System.Diagnostics;

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
        ILogger _logger;
        public LoggerHelper(ILogger logger)
        {
            _logger = logger;
        }
        public void LogDebugMessage(bool debug, String msg)
        {
            _logger.LogDebug(msg);
        }

        public string GetConfigStringValues(Dictionary<string, object> moesifOptions, String configName, string defaultValue)
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

        public  int GetConfigIntValues(Dictionary<string, object> moesifOptions, String configName, int defaultValue)
        {
            var config_out = new object();
            var getConfigOption = moesifOptions.TryGetValue(configName, out config_out);
            return getConfigOption ? Convert.ToInt32(config_out) : defaultValue;
        }

        public  Dictionary<string, object> GetConfigObjectValues(string configName, Dictionary<string, object> moesifOptions, HttpRequest request, HttpResponse response, bool debug)
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

        public  string GetConfigValues(string configName, Dictionary<string, object> moesifOptions, HttpRequest request, HttpResponse response, bool debug)
        {
            var string_out = new object();
            var getStringValue = moesifOptions.TryGetValue(configName, out string_out);

            Func<HttpRequest, HttpResponse, string> GetValue = null;
            if (getStringValue)
            {
                GetValue = (Func<HttpRequest, HttpResponse, string>)(string_out);
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

        public string GetOrCreateTransactionId(IDictionary<string, string> headers, string headerName)
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

        public  Dictionary<string, string> AddTransactionId(string headerName, string transactionId, Dictionary<string, string> headers)
        {
            if (!string.IsNullOrEmpty(transactionId))
            {
                headers[headerName] = transactionId;
            }
            return headers;
        }

        public  Dictionary<string, string> ToHeaders(IHeaderDictionary headers, bool debug)
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

        public async Task<string> GetRequestContents(string bodyAsText, HttpRequest request, string contentEncoding, int parsedContentLength, bool debug, bool logBody)
        {
            if (!logBody)
            {
                return null;
            }

            try
            {
                bodyAsText = await Compression.UncompressStream(request.Body, contentEncoding, parsedContentLength);
                request.Body.Position = 0;
            }
            catch (Exception)
            {
                LogDebugMessage(debug, "Error encountered while copying request body");
            }
            return bodyAsText;
        }

        public string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public  Tuple<object, string> Serialize(string data, string contentType, bool logBody, bool debug)
        {
#if MOESIF_INSTRUMENT
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            _logger.LogError($@"
                                Serialize Body: {data}");

            long checkForEmptyDataAndInitObj = 0;
            long parseJsonData = 0;
            long parseBase64Data = 0;
            long exceptionBase64Data = 0;
#endif

            if (string.IsNullOrEmpty(data))
            {
#if MOESIF_INSTRUMENT
                stopwatch.Stop();
                _logger.LogError($@"
                                Exiting Serialize Body as empty data with time: {stopwatch.ElapsedMilliseconds} ms");
#endif
                return new Tuple<object, string>(null, null);
            }

            var reqBody = new object();
            string requestTransferEncoding = null;
#if MOESIF_INSTRUMENT
            checkForEmptyDataAndInitObj = stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
#endif
            if (logBody && contentType != null && !(contentType.ToLower().StartsWith("multipart/form-data")))
            {
                try
                {
                    // Only try parse if is JSON or looks like JSON
                    if (contentType != null && contentType.ToLower().Contains("json") || data.StartsWith("{") || data.StartsWith("["))
                    {
                        reqBody = ApiHelper.JsonDeserialize<object>(data);
                        requestTransferEncoding = "json";
#if MOESIF_INSTRUMENT
                        parseJsonData = stopwatch.ElapsedMilliseconds;
                        stopwatch.Restart();
#endif
                    }
                    else
                    {
                        LogDebugMessage(debug, "About to parse Request body as Base64 encoding");
                        reqBody = Base64Encode(data);
                        requestTransferEncoding = "base64";
#if MOESIF_INSTRUMENT
                        parseBase64Data = stopwatch.ElapsedMilliseconds;
                        stopwatch.Restart();
#endif
                    }
                }
                catch (Exception)
                {
                    LogDebugMessage(debug, "About to parse Request body as Base64 encoding");
                    reqBody = Base64Encode(data);
                    requestTransferEncoding = "base64";
#if MOESIF_INSTRUMENT
                    exceptionBase64Data = stopwatch.ElapsedMilliseconds;
                    stopwatch.Restart();
#endif
                }
            }
#if MOESIF_INSTRUMENT
            stopwatch.Stop();
            var strHeader = string.Concat(
                "ExitingSerializeBody,",
                "checkForEmptyDataAndInitObj,",
                "parseJsonData,",
                "parseBase64Data,",
                "exceptionBase64Data"
            );
            var strTimes = string.Concat(
                $"{checkForEmptyDataAndInitObj + parseJsonData + parseBase64Data + exceptionBase64Data + stopwatch.ElapsedMilliseconds},",
                $"{checkForEmptyDataAndInitObj},",
                $"{parseJsonData},",
                $"{parseBase64Data},",
                $"{exceptionBase64Data}"
            );
            _logger.LogError($@"
                    {strHeader}
                    {strTimes}
                ");
            // _logger.LogError($@"
            //                     Exiting Serialize Body with time: {checkForEmptyDataAndInitObj + parseJsonData + parseBase64Data + exceptionBase64Data + stopwatch.ElapsedMilliseconds} ms
            //                     checkForEmptyDataAndInitObj took: {checkForEmptyDataAndInitObj} ms
            //                     parseJsonData took: {parseJsonData} ms
            //                     parseBase64Data took: {parseBase64Data} ms
            //                     exceptionBase64Data took: {exceptionBase64Data} ms");
#endif

            return new Tuple<object, string>(reqBody, requestTransferEncoding);
        }
    }
}
#endif
