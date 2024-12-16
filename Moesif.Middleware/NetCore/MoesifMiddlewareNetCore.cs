// #define MOESIF_INSTRUMENT

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moesif.Api;
using Moesif.Api.Models;
using Moesif.Api.Exceptions;
using System.Threading;
using System.Collections.Concurrent;
//using System.Net.Http;
using Moesif.Middleware.Helpers;
using Moesif.Middleware.Models;
using Microsoft.Extensions.Logging;
// using System.Reflection.PortableExecutable;

//using Newtonsoft.Json;

#if NETCORE
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Moesif.Middleware.NetCore.Helpers;
#endif

#if NETCORE
namespace Moesif.Middleware.NetCore
{
    public class MoesifMiddlewareNetCore
    {
        public static string APP_VERSION = "moesif-netcore/3.1.3";
        private readonly RequestDelegate _next;

        public Dictionary<string, object> moesifOptions;

        public MoesifApiClient client;

        //public UserHelper userHelper; // Initialize user helper
        public static UserHelper userHelper = new UserHelper(); // Initialize user helper

        //public CompanyHelper companyHelper; // Initialize company helper
        public static CompanyHelper companyHelper = new CompanyHelper(); // Initialize company helper

        //public ClientIp clientIpHelper; // Initialize client ip helper
        public static ClientIp clientIpHelper = new ClientIp(); // Initialize client ip helper

        //public volatile AppConfig config;  // The AppConfig
        public static volatile AppConfig config = AppConfig.getDefaultAppConfig();  // The AppConfig
        
        //public volatile Governance governance; // Governance Rule
        public static volatile Governance governance = Governance.getDefaultGovernance(); // Governance Rule

        public bool isBatchingEnabled; // Enable Batching

        public int batchSize; // Queue batch size

        public int queueSize; // Event Queue size

        public int batchMaxTime; // Time in seconds for next batch

        public int appConfigSyncTime; // Time in seconds to sync application configuration

        public string apiVersion;

        //public ConcurrentQueue<EventModel> MoesifQueue; // Moesif Queue
        public static ConcurrentQueue<EventModel> MoesifQueue = new ConcurrentQueue<EventModel>(); // Moesif Queue

        public string authorizationHeaderName; // A request header field name used to identify the User

        public string authorizationUserIdField; // A field name used to parse the User from authorization header

        public DateTime lastWorkerRun = DateTime.MinValue;

        public DateTime lastAppConfigWorkerRun = DateTime.MinValue;

        public bool debug = false;

        public bool logBody;
        public int requestMaxBodySize = 100000;
        public int responseMaxBodySize = 100000;

        public bool isLambda;

        //private AutoResetEvent configEvent ;
        private static AutoResetEvent configEvent = new AutoResetEvent(false);

        //private AutoResetEvent governanceEvent;
        private static AutoResetEvent governanceEvent = new AutoResetEvent(false);

        private ILogger _logger;

        private LoggerHelper loggerHelper;

        public MoesifMiddlewareNetCore(Dictionary<string, object> _middleware)
        {
            moesifOptions = _middleware;
            _logger = null;
            loggerHelper = new LoggerHelper(_logger);

            try
            {
                // Initialize client
                debug = loggerHelper.GetConfigBoolValues(moesifOptions, "LocalDebug", false);
                client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString(), APP_VERSION, debug);
                //companyHelper = new CompanyHelper();
                //userHelper = new UserHelper();
            }
            catch (Exception)
            {
                throw new Exception("Please provide the application Id to send events to Moesif");
            }
        }

        public MoesifMiddlewareNetCore(RequestDelegate next, Dictionary<string, object> _middleware, ILoggerFactory logger)
        {
#if MOESIF_INSTRUMENT
            var logStage = false;
            var perfMetrics = new PerformanceMetrics("MiddlewareInit", logStage);
            perfMetrics.Start("createLoggerTime");
#endif

            moesifOptions = _middleware;
            _logger = logger.CreateLogger("Moesif.Middleware.NetCore");
            loggerHelper = new LoggerHelper(_logger);

#if MOESIF_INSTRUMENT
            perfMetrics.StopPreviousStartNew("initClientAndOptions");
#endif
            try
            {
                // Initialize client
                debug = loggerHelper.GetConfigBoolValues(moesifOptions, "LocalDebug", false);
                client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString(), APP_VERSION, debug);
                logBody = loggerHelper.GetConfigBoolValues(moesifOptions, "LogBody", true);
                requestMaxBodySize = loggerHelper.GetConfigIntValues(moesifOptions, "RequestMaxBodySize", 100000);
                responseMaxBodySize = loggerHelper.GetConfigIntValues(moesifOptions, "ResponseMaxBodySize", 100000);
                isLambda = loggerHelper.GetConfigBoolValues(moesifOptions, "IsLambda", false);
                _next = next;
                //config = AppConfig.getDefaultAppConfig();
                //userHelper = new UserHelper(); // Create a new instance of userHelper
                //companyHelper = new CompanyHelper(); // Create a new instane of companyHelper
                //clientIpHelper = new ClientIp(); // Create a new instance of client Ip
                //isBatchingEnabled = loggerHelper.GetConfigBoolValues(moesifOptions, "EnableBatching", true); // Enable batching
                // Force isBatchingEnabled to false if isLambda is true
                if (isLambda)
                {
                    isBatchingEnabled = false;
                }
                else
                {
                    isBatchingEnabled = loggerHelper.GetConfigBoolValues(moesifOptions, "EnableBatching", true); // Enable batching, defaults to true if not lambda
                }

                batchSize = loggerHelper.GetConfigIntValues(moesifOptions, "BatchSize", 200); // Batch Size
                queueSize = loggerHelper.GetConfigIntValues(moesifOptions, "QueueSize", 100 * 1000); // Queue Size
                batchMaxTime = loggerHelper.GetConfigIntValues(moesifOptions, "batchMaxTime", 2); // Batch max time in seconds
                appConfigSyncTime = loggerHelper.GetConfigIntValues(moesifOptions, "appConfigSyncTime", 300); // App config sync time in seconds
                authorizationHeaderName = loggerHelper.GetConfigStringValues(moesifOptions, "AuthorizationHeaderName", "authorization");
                authorizationUserIdField = loggerHelper.GetConfigStringValues(moesifOptions, "AuthorizationUserIdField", "sub");

                //_logger.LogError($"Init isLambda: {isLambda} debug: {debug} isBatchingEnabled {isBatchingEnabled} and logBody: {logBody}");

                if ( moesifOptions.TryGetValue("ApiVersion", out object version))
                {
                    apiVersion = version!= null ? version.ToString() : null;
                } else
                {
                    apiVersion = null;
                }

#if MOESIF_INSTRUMENT
                perfMetrics.StopPreviousStartNew("fetchAppConfig");
#endif
                //MoesifQueue = new ConcurrentQueue<EventModel>(); // Initialize queue
                //governance = Governance.getDefaultGovernance();
                //configEvent = new AutoResetEvent(false);
                //governanceEvent = new AutoResetEvent(false);

                //if (isLambda)
                //{
                //    _logger.LogError("Skip calling schedule function because isLambda true");
                //    // update Application config
                //    if (debug)
                //    {
                //        stopwatch.Reset(); // Reset the stopwatch to 0
                //        stopwatch.Start();
                //    }
                //    config = AppConfigHelper.updateConfig(client, config, debug, _logger).Result;
                //    _logger.LogInformation(config.sample_rate.ToString());

                //    if (debug)
                //    {
                //        _logger.LogInformation($"Fetching app config took time: {stopwatch.ElapsedMilliseconds} milliseconds");
                //        stopwatch.Reset(); // Reset the stopwatch to 0
                //        stopwatch.Start();
                //    }
                //    governance = GovernanceHelper.updateGovernance(client, governance, debug, _logger).Result;
                //    _logger.LogInformation(governance.rules.ToString());

                //    if (debug)
                //    {
                //        _logger.LogInformation($"Fetching gov rules took time: {stopwatch.ElapsedMilliseconds} milliseconds");
                //        stopwatch.Stop();
                //    }
                //} else
                {
                    if (isBatchingEnabled) ScheduleWorker();

                    ScheduleAppConfig();
#if MOESIF_INSTRUMENT
                    _logger.LogError($"Sampling percentage in Init is - {config.sample_rate.ToString()} ");
                    perfMetrics.StopPreviousStartNew("fetchGovRule");
#endif

                    ScheduleGovernanceRule();

#if MOESIF_INSTRUMENT
                    perfMetrics.Stop();
#endif
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, "MoesifMiddlewareNetCore initialization error");
                throw new Exception("Please provide the application Id to send events to Moesif");
            }

#if MOESIF_INSTRUMENT
            perfMetrics.PrintMetrics(Console.WriteLine);
#endif
        }

        private void ScheduleAppConfig()
        {
            _logger.LogDebug("Starting a new thread to sync the application configuration");

            Thread appConfigThread = new Thread(async () => // Create a new thread to fetch the application configuration
            {

                while (true)
                {
                    try
                    {
                        lastAppConfigWorkerRun = DateTime.UtcNow;
                        _logger.LogDebug("Last App Config Worker Run - {time}  for thread Id - {thread}" , lastAppConfigWorkerRun, Thread.CurrentThread.ManagedThreadId);

                        // update Application config
                        //Task.Run(async () => config = await AppConfigHelper.updateConfig(client, config, debug, _logger) );
                        config = await AppConfigHelper.updateConfig(client, config, debug, _logger);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while updating appConfig");
                    }
                    
                    configEvent.WaitOne(60 * 1000);
                }
            });
            appConfigThread.IsBackground = true;
            appConfigThread.Start();
        }

        private void ScheduleGovernanceRule()
        {
            _logger.LogDebug("Starting a new thread to sync governance rules");

            Thread governanceThread = new Thread(async () => // Create a new thread to fetch the governance rules
            {

                while (true)
                {
                    try
                    {
                        var lastGovernanceWorkerRun = DateTime.UtcNow;
                        _logger.LogDebug("Last Governance Worker Run - " + lastAppConfigWorkerRun.ToString() + " for thread Id - " + Thread.CurrentThread.ManagedThreadId.ToString());

                        // update Governance Rule
                        //Task.Run(async () => governance = await GovernanceHelper.updateGovernance(client, governance, debug, _logger));
                        governance = await GovernanceHelper.updateGovernance(client, governance, debug, _logger);
                    }
                    catch (Exception e)
                    {
                         _logger.LogError(e, "Error while updating Governance rule");
                    }
                    governanceEvent.WaitOne(60 * 1000);
                }
            });
            governanceThread.IsBackground = true;
            governanceThread.Start();
        }

        private void ScheduleWorker()
        {
            //LoggerHelper.LogDebugMessage(debug, "Starting a new thread to read the queue and send event to moesif");
            _logger.LogDebug("Starting a new thread to read the queue and send event to moesif");

            Thread workerThread = new Thread(async () => // Create a new thread to read the queue and send event to moesif
            {
                while (true)
                {
                    await Task.Delay(batchMaxTime * 1000);
                    try
                    {
                        lastWorkerRun = DateTime.UtcNow;
                        _logger.LogDebug( "Last Worker Run {time}  for thread Id {threadId} ", lastWorkerRun, Thread.CurrentThread.ManagedThreadId);
                        await Tasks.AsyncClientCreateEvent(client, MoesifQueue, config, governance, configEvent, governanceEvent,batchSize, debug, _logger);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError( e, "Error while scheduling events batch job");
                    }
                }
            });
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        // Function to update user
        public void UpdateUser(Dictionary<string, object> userProfile)
        {
            userHelper.UpdateUser(client, userProfile, debug);
        }

        // Function to update users in batch
        public void UpdateUsersBatch(List<Dictionary<string, object>> userProfiles)
        {
            userHelper.UpdateUsersBatch(client, userProfiles, debug);
        }

        // Function to update company
        public void UpdateCompany(Dictionary<string, object> companyProfile)
        {
            companyHelper.UpdateCompany(client, companyProfile, debug);
        }

        // Function to update companies in batch
        public void UpdateCompaniesBatch(List<Dictionary<string, object>> companyProfiles)
        {
            companyHelper.UpdateCompaniesBatch(client, companyProfiles, debug);
        }

        public void CreateStreamHelpers(HttpContext httpContext, out StreamHelper outputCaptureOwin)
        {
            outputCaptureOwin = null;  // Buffering Owin response

            // Check if we need to create memory stream., only if
            //  - logBody is enabled &&
            //  - response's content-length is less than maxBodySize
            var resHeaders = loggerHelper.ToHeaders(httpContext.Response.Headers, debug);
            int parsedRespContentLength = responseMaxBodySize - 1;
            GetContentLengthAndEncoding(resHeaders, parsedRespContentLength, out parsedRespContentLength);  // Get the content-length from response header if possible.
            bool needToCreateStream = (logBody && parsedRespContentLength <= responseMaxBodySize) ;

            // Create stream to Buffer Owin response
            if (needToCreateStream)
            {
                var owinResponse = httpContext.Response;
                outputCaptureOwin = new StreamHelper(owinResponse.Body);
                owinResponse.Body = outputCaptureOwin;
            }
        }

        public async Task Invoke(HttpContext httpContext)
        {
#if MOESIF_INSTRUMENT
            var logStage = false;
            var perfMetrics = new PerformanceMetrics("Invoke", logStage);
            perfMetrics.Start("formatLambdaRequest");
#endif

            // Initialize Transaction Id
            string transactionId = null;
            EventRequestModel request;
            (request, transactionId) = await FormatRequest(httpContext.Request, transactionId);

#if MOESIF_INSTRUMENT
            perfMetrics.StopPreviousStartNew("eventModelInit");
#endif

            // Add Transaction Id to the Response Header
            if (!string.IsNullOrEmpty(transactionId))
            {
                httpContext.Response.Headers.Add("X-Moesif-Transaction-Id", transactionId);
            }

            var eventModel = new EventModel()
            {
                Request = request,
                Response = null,
                Direction = "Incoming"
            };

            var skipLogging = false;

            // Get Skip
            var getSkip = moesifOptions.TryGetValue("Skip", out object skip_out);

            // Check to see if we need to send event to Moesif
            Func<HttpRequest, HttpResponse, bool> ShouldSkip = null;

            if (getSkip)
            {
                ShouldSkip = (Func<HttpRequest, HttpResponse, bool>)(skip_out);
            }

            if (ShouldSkip != null && ShouldSkip(httpContext.Request, httpContext.Response))
            {
                skipLogging = true;
            }
#if MOESIF_INSTRUMENT
            perfMetrics.StopPreviousStartNew("upstreamResponse");
#endif
            if(GovernanceHelper.isGovernaceRuleDefined(governance))
            {
                eventModel.UserId = getUserId(httpContext, request);
                eventModel.CompanyId = loggerHelper.GetConfigValues("IdentifyCompany", moesifOptions, httpContext.Request, httpContext.Response, debug);
            }

            if (GovernanceHelper.enforceGovernaceRule(eventModel, governance, config))
            {
                httpContext.Response.StatusCode = eventModel.Response.Status;
                foreach (var kv in eventModel.Response.Headers)
                {
                    httpContext.Response.Headers.Append(kv.Key, kv.Value);
                }
                await httpContext.Response.WriteAsync(eventModel.Response.Body.ToString());

                _logger.LogDebug("Request is blocked by Governance rule {ruleId}", eventModel.BlockedBy);

                eventModel.Response.Headers["X-Moesif-Transaction-Id"] = transactionId;
                if (!skipLogging)
                {
                    // REVIEW 
                    // await Task.Run(async () => await LogEventAsync(eventModel));
                    await LogEventAsync(eventModel);
                }
            }
            else
            {
                // Create memory stream
                StreamHelper outputCaptureOwin = null;  // For buffering Owin response
                CreateStreamHelpers(httpContext, out outputCaptureOwin);
#if MOESIF_INSTRUMENT
                perfMetrics.StopPreviousStartNew("nextMiddleware");
#endif 
                await _next(httpContext);
#if MOESIF_INSTRUMENT
                perfMetrics.StopPreviousStartNew("formatResponse");
#endif 

                if (skipLogging)
                {
                    _logger.LogDebug("Skipping the event");
                }
                else
                {
                    if (isLambda)
                    {
                        eventModel.Response = await FormatLambdaResponse(httpContext, transactionId);
                    } else
                    {
                        eventModel.Response = FormatResponse(httpContext.Response, outputCaptureOwin, transactionId);
                    }

#if MOESIF_INSTRUMENT
                    perfMetrics.StopPreviousStartNew("getCompanyId");
#endif

                    if (eventModel.CompanyId == null)
                    {
                       eventModel.CompanyId = loggerHelper.GetConfigValues("IdentifyCompany", moesifOptions, httpContext.Request, httpContext.Response, debug);
                    }

#if MOESIF_INSTRUMENT
                    perfMetrics.StopPreviousStartNew("getUserId");
#endif

                    if (eventModel.UserId == null)
                    {
                        eventModel.UserId = getUserId(httpContext, eventModel.Request);
                    }

#if MOESIF_INSTRUMENT
                    perfMetrics.StopPreviousStartNew("getMetadata");
#endif

                    eventModel.Metadata = loggerHelper.GetConfigObjectValues("GetMetadata", moesifOptions, httpContext.Request, httpContext.Response, debug);

#if MOESIF_INSTRUMENT
                    perfMetrics.StopPreviousStartNew("getSessionToken");
#endif

                    eventModel.SessionToken = loggerHelper.GetConfigValues("GetSessionToken", moesifOptions, httpContext.Request, httpContext.Response, debug);

#if MOESIF_INSTRUMENT
                    perfMetrics.StopPreviousStartNew("LogEventAsync");
                    _logger.LogError("Calling the API to send the event to Moesif");
#endif
                    _logger.LogDebug("Calling the API to send the event to Moesif");
                    //Send event to Moesif async
                    if (isLambda)
                    {
                        await LogEventAsync(eventModel);
                        //Task.Run(async () => await LogEventAsync(eventModel));
                    }
                    else
                    {
                        // REVIEW : Fire & Forget
                        // Task.Run(async () => await LogEventAsync(eventModel));
                        LogEventAsync(eventModel);
                    }

#if MOESIF_INSTRUMENT
                    perfMetrics.Stop();
#endif
                }
            }

#if MOESIF_INSTRUMENT
            perfMetrics.PrintMetrics(Console.WriteLine);
#endif
        }

        public static string GetContentLengthAndEncoding(Dictionary<string, string> headers, int defaultLength, out int parsedContentLength)
        {
            string contentEncoding = "";

            if (headers != null)
            {
                string contentLength = "";
                headers.TryGetValue("Content-Length", out contentLength);
                headers.TryGetValue("Content-Encoding", out contentEncoding);
                int.TryParse(contentLength, out parsedContentLength);
            }
            else
            {
                parsedContentLength = defaultLength;
            }

            return contentEncoding;
        }

        public static string GetExceededBodyForBodySize(string prefix, int curBodySize, int maxBodySize)
        {
            object payload = new { msg = $"{prefix}.body.length {curBodySize} exceeded {prefix}MaxBodySize of {maxBodySize}" };
            string bodyPayload = ApiHelper.JsonSerialize(payload);

            return bodyPayload;
        }

        private async Task<(EventRequestModel, String)> FormatRequest(HttpRequest request, string transactionId)
        {
#if MOESIF_INSTRUMENT
            var logStage = false;
            var perfMetrics = new PerformanceMetrics("FormatRequest", logStage);
            perfMetrics.Start("convertToHeadersTime");
#endif
            // Request headers
            var reqHeaders = loggerHelper.ToHeaders(request.Headers, debug);
#if MOESIF_INSTRUMENT
            perfMetrics.StopPreviousStartNew("setHeaderEnableBuffering");
#endif

            // Get Content-Length and Content-Encoding
            // string contentEncoding = "";
            // string contentLength = "";
            int parsedContentLength = requestMaxBodySize -1 ;
            // reqHeaders.TryGetValue("Content-Encoding", out contentEncoding);
            // reqHeaders.TryGetValue("Content-Length", out contentLength);
            // int.TryParse(contentLength, out parsedContentLength);
            string contentEncoding = GetContentLengthAndEncoding(reqHeaders, parsedContentLength, out parsedContentLength);

            // RequestBody
            request.EnableBuffering(bufferThreshold: 1000000);
            string bodyAsText = null;

#if MOESIF_INSTRUMENT
            perfMetrics.StopPreviousStartNew("getRequestContent");
#endif
            // Check if body exceeded max size supported or no body content
            if (parsedContentLength == 0)
            {
                // no body content
                bodyAsText = null;
            }
            else if (parsedContentLength > requestMaxBodySize)
            {
                // Body size exceeds max limit. Use an info message body.
                bodyAsText = GetExceededBodyForBodySize("request", parsedContentLength, requestMaxBodySize);
            }
            else
            {
                // Body size is within allowed max limit or unknown. Read the body.
                bodyAsText = await loggerHelper.GetRequestContents(bodyAsText, request, contentEncoding, parsedContentLength, debug, logBody);
            }

            // Check if body exceeded max size supported
            if (!string.IsNullOrWhiteSpace(bodyAsText) && bodyAsText.Length > requestMaxBodySize)
            {
                // Read body's size exceeds max limit. Use an info message body.
                bodyAsText = GetExceededBodyForBodySize("request", bodyAsText.Length, requestMaxBodySize);
            }
#if MOESIF_INSTRUMENT
            perfMetrics.StopPreviousStartNew("addTxnId");
#endif

            // Add Transaction Id to the Request Header
            bool disableTransactionId = loggerHelper.GetConfigBoolValues(moesifOptions, "DisableTransactionId", false);
            if (!disableTransactionId)
            {
                transactionId = loggerHelper.GetOrCreateTransactionId(reqHeaders, "X-Moesif-Transaction-Id");
                reqHeaders = loggerHelper.AddTransactionId("X-Moesif-Transaction-Id", transactionId, reqHeaders);
            }
#if MOESIF_INSTRUMENT
            perfMetrics.StopPreviousStartNew("serializeReqBody");
#endif
            // Serialize request body
            var bodyWrapper = loggerHelper.Serialize(bodyAsText, request.ContentType, logBody, debug);
#if MOESIF_INSTRUMENT
            perfMetrics.StopPreviousStartNew("getIpAndInitReqModel");
#endif
            // Client Ip Address
            string ip = clientIpHelper.GetClientIp(reqHeaders, request);
            var uri = new Uri(request.GetDisplayUrl()).ToString();


            var eventReq = new EventRequestModel()
            {
                Time = DateTime.UtcNow,
                Uri = uri,
                Verb = request.Method,
                ApiVersion = apiVersion,
                IpAddress = ip,
                Headers = reqHeaders,
                Body = bodyWrapper.Item1,
                TransferEncoding = bodyWrapper.Item2
            };

#if MOESIF_INSTRUMENT
            perfMetrics.PrintMetrics(Console.WriteLine);
#endif

            return (eventReq, transactionId);
        }

        private EventResponseModel FormatResponse(HttpResponse response, StreamHelper stream, string transactionId)
        {
#if MOESIF_INSTRUMENT
            var logStage = false;
            var perfMetrics = new PerformanceMetrics("FormatResponse", logStage);
            perfMetrics.Start("addTxnId");
#endif
            // Response headers
            var rspHeaders = loggerHelper.ToHeaders(response.Headers, debug);

            // Add Transaction Id to Response Header
            rspHeaders = loggerHelper.AddTransactionId("X-Moesif-Transaction-Id", transactionId, rspHeaders);
#if MOESIF_INSTRUMENT
            perfMetrics.StopPreviousStartNew("serializeRespBody");
#endif

            var responseWrapper = new Tuple<object, string>(null, null);
            if (logBody && stream != null)
            {
                // ResponseBody
                string contentEncoding = "";
                rspHeaders.TryGetValue("Content-Encoding", out contentEncoding);
                string text = stream.ReadStream(contentEncoding);

                // Check if response body exceeded max size supported
                if (!string.IsNullOrWhiteSpace(text) && text.Length > responseMaxBodySize)
                {
                    // Body size exceeds max limit. Use an info message body.
                    text = GetExceededBodyForBodySize("response", text.Length, responseMaxBodySize);
                }

                // Serialize Response body
                responseWrapper = loggerHelper.Serialize(text, response.ContentType, logBody, debug);
            }
#if MOESIF_INSTRUMENT
            perfMetrics.StopPreviousStartNew("initRespModel");
#endif
            var eventRsp = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = response.StatusCode,
                Headers = rspHeaders,
                Body = responseWrapper.Item1,
                TransferEncoding = responseWrapper.Item2
            };
#if MOESIF_INSTRUMENT
            perfMetrics.PrintMetrics(Console.WriteLine);
#endif
            return eventRsp;
        }

        private async Task<EventResponseModel> FormatLambdaResponse(HttpContext httpContext, string transactionId)
        {
            // Response headers
            var rspHeaders = loggerHelper.ToHeaders(httpContext.Response.Headers, debug);

            // Add Transaction Id to Response Header
            rspHeaders = loggerHelper.AddTransactionId("X-Moesif-Transaction-Id", transactionId, rspHeaders);

            var responseWrapper = new Tuple<object, string>(null, null);
            if (logBody)
            {
                // ResponseBody
                string contentEncoding = "";
                rspHeaders.TryGetValue("Content-Encoding", out contentEncoding);

                var originalResponseBodyStream = httpContext.Response.Body;
                string responseBody = string.Empty;
                // TODO : why new memory stream here???
                using (var responseBodyStream = new MemoryStream())
                {
                    httpContext.Response.Body = responseBodyStream; // Use the memory stream for the response

                    // Capture the response
                    httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
                    responseBody = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
                    // Check if response body exceeded max size supported
                    if (!string.IsNullOrWhiteSpace(responseBody) && responseBody.Length > responseMaxBodySize)
                    {
                        // Body size exceeds max limit. Use an info message body.
                        responseBody = GetExceededBodyForBodySize("response", responseBody.Length, responseMaxBodySize);
                    }
                    httpContext.Response.Body.Seek(0, SeekOrigin.Begin); // Reset the position for the response to be sent

                    // Copy the response body back to the original stream
                    await responseBodyStream.CopyToAsync(originalResponseBodyStream);
                }

                // Serialize Response body
                responseWrapper = loggerHelper.Serialize(responseBody, httpContext.Response.ContentType, logBody, debug);
            }
            var eventRsp = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = httpContext.Response.StatusCode,
                Headers = rspHeaders,
                Body = responseWrapper.Item1,
                TransferEncoding = responseWrapper.Item2
            };
            return eventRsp;
        }

        private String getUserId(HttpContext httpContext, EventRequestModel request)
        {
            Object iu;
            var getFunctionValue = moesifOptions.TryGetValue("IdentifyUser", out iu);
            if (getFunctionValue)
            {
                return loggerHelper.GetConfigValues("IdentifyUser", moesifOptions, httpContext.Request, httpContext.Response, debug);
            }
            else
            {
                var userId = userHelper.fetchUserFromAuthorizationHeader(request.Headers, authorizationHeaderName, authorizationUserIdField);

                if (string.IsNullOrEmpty(userId))
                {
                    userId = httpContext?.User?.Identity?.Name;
                }
                return userId;
            }
        }

        private async Task LogEventAsync(EventModel eventModel)
        {
#if MOESIF_INSTRUMENT
            var logStage = false;
            var perfMetrics = new PerformanceMetrics("LogEventAsync", logStage);
            perfMetrics.Start("getMaskEvent");
#endif

            // Get Mask Event : REVIEW can it be created early?
            var maskEvent_out = new object();
            var getMaskEvent = moesifOptions.TryGetValue("MaskEventModel", out maskEvent_out);

            // Check to see if we need to send event to Moesif
            Func<EventModel, EventModel> MaskEvent = null;

            if (getMaskEvent)
            {
                MaskEvent = (Func<EventModel, EventModel>)(maskEvent_out);
            }

            // Mask event
            if (MaskEvent != null)
            {
                try
                {
                    eventModel = MaskEvent(eventModel);
                }
                catch
                {
                    _logger.LogWarning("Can not execute MASK_EVENT_MODEL function. Please check moesif settings.");
                }
            }

#if MOESIF_INSTRUMENT
            perfMetrics.StopPreviousStartNew("createRequestMap");
#endif

            // Send Events
            try
            {
                // Get Sampling percentage
                RequestMap requestMap = RequestMapHelper.createRequestMap(eventModel);

#if MOESIF_INSTRUMENT
                perfMetrics.StopPreviousStartNew("getSamplingPercentage");
#endif
                var samplingPercentage = AppConfigHelper.getSamplingPercentage(config, requestMap);
                //_logger.LogDebug($"Sampling percentage in LogEventAsync is - { samplingPercentage} ");

#if MOESIF_INSTRUMENT
                _logger.LogError($"Sampling percentage in LogEventAsync is - { samplingPercentage} ");
                perfMetrics.StopPreviousStartNew("getRandomPercentage");
#endif

                Random random = new Random();
                double randomPercentage = random.NextDouble() * 100;

#if MOESIF_INSTRUMENT
                perfMetrics.Stop();
#endif
                if (samplingPercentage >= randomPercentage)
                {
#if MOESIF_INSTRUMENT
                    perfMetrics.Start("calculateWeight");
#endif
                    eventModel.Weight = AppConfigHelper.calculateWeight(samplingPercentage);

#if MOESIF_INSTRUMENT
                    perfMetrics.Stop();
#endif

                    if (isBatchingEnabled)
                    {
                        _logger.LogDebug("Add Event to the batch");

                        if (MoesifQueue.Count < queueSize)
                        {
                            // Add event to the batch
                            MoesifQueue.Enqueue(eventModel);
                        }
                        else
                        {
                            _logger.LogWarning("SKIPPING Events: Queue is full.");
                        }
                    }
                    else
                    {
#if MOESIF_INSTRUMENT
                        _logger.LogError("Current UTC time BEFORE CreateEventAsync call : " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                        perfMetrics.Start("CreateEventAsync");
#endif
                        await client.Api.CreateEventAsync(eventModel, !isLambda);
                        _logger.LogDebug("Event sent successfully to Moesif");

#if MOESIF_INSTRUMENT
                        _logger.LogError("Current UTC time AFTER CreateEventAsync call: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                        _logger.LogError("Event sent successfully to Moesif");
                        perfMetrics.Stop();
#endif
                    }
                }
                else
                {
                    _logger.LogDebug("Skipped Event due to sampling percentage: " + samplingPercentage.ToString() + " and random percentage: " + randomPercentage.ToString());
                }
            }
            catch (APIException inst)
            {
                if (401 <= inst.ResponseCode && inst.ResponseCode <= 403)
                {
                    _logger.LogWarning("Unauthorized access sending event to Moesif. Please check your Appplication Id.");
                }
                _logger.LogWarning("Error sending event to Moesif, with status code: {statusCode}", inst.ResponseCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in sending event");
            }

#if MOESIF_INSTRUMENT
            perfMetrics.PrintMetrics(Console.WriteLine);
#endif
        }

    }
}
#endif
