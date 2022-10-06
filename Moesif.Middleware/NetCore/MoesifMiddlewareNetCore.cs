using System;
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
using Moesif.Middleware.Helpers;
using Moesif.Middleware.Models;

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
        private readonly RequestDelegate _next;

        public Dictionary<string, object> moesifOptions;

        public MoesifApiClient client;

        public UserHelper userHelper; // Initialize user helper

        public CompanyHelper companyHelper; // Initialize company helper

        public ClientIp clientIpHelper; // Initialize client ip helper

        public volatile AppConfig config;  // The AppConfig

        public volatile Governance governance; // Governance Rule

        public bool isBatchingEnabled; // Enable Batching

        public int batchSize; // Queue batch size

        public int queueSize; // Event Queue size

        public int batchMaxTime; // Time in seconds for next batch

        public int appConfigSyncTime; // Time in seconds to sync application configuration

        public ConcurrentQueue<EventModel> MoesifQueue; // Moesif Queue

        public string authorizationHeaderName; // A request header field name used to identify the User

        public string authorizationUserIdField; // A field name used to parse the User from authorization header

        public DateTime lastWorkerRun = DateTime.MinValue;

        public DateTime lastAppConfigWorkerRun = DateTime.MinValue;

        public bool debug;

        public bool logBody;

        private AutoResetEvent configEvent ;

        private AutoResetEvent governanceEvent;

        public MoesifMiddlewareNetCore(Dictionary<string, object> _middleware)
        {
            moesifOptions = _middleware;

            try
            {
                // Initialize client
                client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString());
                debug = LoggerHelper.GetConfigBoolValues(moesifOptions, "LocalDebug", false);
            }
            catch (Exception)
            {
                throw new Exception("Please provide the application Id to send events to Moesif");
            }
        }

        public MoesifMiddlewareNetCore(RequestDelegate next, Dictionary<string, object> _middleware)
        {
            moesifOptions = _middleware;

            try
            {
                // Initialize client
                debug = LoggerHelper.GetConfigBoolValues(moesifOptions, "LocalDebug", false);
                client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString(), "moesif-netcore/1.3.20", debug);
                logBody = LoggerHelper.GetConfigBoolValues(moesifOptions, "LogBody", true);
                _next = next;
                config = AppConfig.getDefaultAppConfig();
                userHelper = new UserHelper(); // Create a new instance of userHelper
                companyHelper = new CompanyHelper(); // Create a new instane of companyHelper
                clientIpHelper = new ClientIp(); // Create a new instance of client Ip
                isBatchingEnabled = LoggerHelper.GetConfigBoolValues(moesifOptions, "EnableBatching", true); // Enable batching
                batchSize = LoggerHelper.GetConfigIntValues(moesifOptions, "BatchSize", 25); // Batch Size
                queueSize = LoggerHelper.GetConfigIntValues(moesifOptions, "QueueSize", 1000); // Queue Size
                batchMaxTime = LoggerHelper.GetConfigIntValues(moesifOptions, "batchMaxTime", 2); // Batch max time in seconds
                appConfigSyncTime = LoggerHelper.GetConfigIntValues(moesifOptions, "appConfigSyncTime", 300); // App config sync time in seconds
                authorizationHeaderName = LoggerHelper.GetConfigStringValues(moesifOptions, "AuthorizationHeaderName", "authorization");
                authorizationUserIdField = LoggerHelper.GetConfigStringValues(moesifOptions, "AuthorizationUserIdField", "sub");
                MoesifQueue = new ConcurrentQueue<EventModel>(); // Initialize queue
                governance = Governance.getDefaultGovernance();
                configEvent = new AutoResetEvent(false);
                governanceEvent = new AutoResetEvent(false);

                if (isBatchingEnabled) ScheduleWorker();

                ScheduleAppConfig();
                ScheduleGovernanceRule();

            }
            catch (Exception)
            {
                throw new Exception("Please provide the application Id to send events to Moesif");
            }
        }

        private void ScheduleAppConfig()
        {
            LoggerHelper.LogDebugMessage(debug, "Starting a new thread to sync the application configuration");

            Thread appConfigThread = new Thread(async () => // Create a new thread to fetch the application configuration
            {

                while (true)
                {
                    try
                    {
                        lastAppConfigWorkerRun = DateTime.UtcNow;
                        LoggerHelper.LogDebugMessage(debug, "Last App Config Worker Run - " + lastAppConfigWorkerRun.ToString() + " for thread Id - " + Thread.CurrentThread.ManagedThreadId.ToString());

                        // update Application config
                        config = await AppConfigHelper.updateConfig(client, config, debug);
                    }
                    catch (Exception)
                    {
                        LoggerHelper.LogDebugMessage(debug, "Error while scheduling appConfig job");
                    }
                    // wait for max 1 hour
                    configEvent.WaitOne(60*60*1000);
                }
            });
            appConfigThread.IsBackground = true;
            appConfigThread.Start();
        }

        private void ScheduleGovernanceRule()
        {
            LoggingHelper.LogDebugMessage(debug, "Starting a new thread to sync governance rules");

            Thread governanceThread = new Thread(async () => // Create a new thread to fetch the governance rules
            {

                while (true)
                {
                    try
                    {
                        var lastGovernanceWorkerRun = DateTime.UtcNow;
                        if(debug)
                          LoggingHelper.LogMessage("Last Governance Worker Run - " + lastAppConfigWorkerRun.ToString() + " for thread Id - " + Thread.CurrentThread.ManagedThreadId.ToString());

                        // update Governance Rule
                        governance = await GovernanceHelper.updateGovernance(client, governance, debug);
                    }
                    catch (Exception)
                    {
                        LoggingHelper.LogDebugMessage(debug, "Error while updating Governance rule");
                    }
                    // wait for event for max 1 hour
                    governanceEvent.WaitOne(60 * 60 * 1000);
                }
            });
            governanceThread.IsBackground = true;
            governanceThread.Start();
        }

        private void ScheduleWorker()
        {
            LoggerHelper.LogDebugMessage(debug, "Starting a new thread to read the queue and send event to moesif");

            Thread workerThread = new Thread(async () => // Create a new thread to read the queue and send event to moesif
            {
                while (true)
                {
                    await Task.Delay(batchMaxTime * 1000);
                    try
                    {
                        lastWorkerRun = DateTime.UtcNow;
                        LoggerHelper.LogDebugMessage(debug, "Last Worker Run - " + lastWorkerRun.ToString() + " for thread Id - " + Thread.CurrentThread.ManagedThreadId.ToString());
                        await Tasks.AsyncClientCreateEvent(client, MoesifQueue, config, governance, configEvent, governanceEvent,batchSize, debug);
                    }
                    catch (Exception)
                    {
                        LoggerHelper.LogDebugMessage(debug, "Error while scheduling events batch job");
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

        public async Task Invoke(HttpContext httpContext)
        {
            // Initialize Transaction Id
            string transactionId = null;
            EventRequestModel request;
            (request, transactionId) = await FormatRequest(httpContext.Request, transactionId);

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

            if(GovernanceHelper.isGovernaceRuleDefined(governance))
            {
                eventModel.UserId = getUserId(httpContext, request);
                eventModel.CompanyId = LoggerHelper.GetConfigValues("IdentifyCompany", moesifOptions, httpContext.Request, httpContext.Response, debug);
            }

            if (GovernanceHelper.enforceGovernaceRule(eventModel, governance, config))
            {
                httpContext.Response.StatusCode = eventModel.Response.Status;
                foreach (var kv in eventModel.Response.Headers)
                {
                    httpContext.Response.Headers.Append(kv.Key, kv.Value);
                }
                await httpContext.Response.WriteAsync(eventModel.Response.Body.ToString());

                if(debug)
                {
                    LoggingHelper.LogMessage("Request is blocked by Governance rule " + eventModel.BlockedBy);
                }

                eventModel.Response.Headers["X-Moesif-Transaction-Id"] = transactionId;
                if (!skipLogging)
                   await Task.Run(async () => await LogEventAsync(eventModel));
            }

            else
            {
                var owinResponse = httpContext.Response;
                StreamHelper outputCaptureOwin = new StreamHelper(owinResponse.Body);
                owinResponse.Body = outputCaptureOwin;

                await _next(httpContext);

                if (skipLogging)
                {
                    LoggerHelper.LogDebugMessage(debug, "Skipping the event");
                }
                else
                {

                    eventModel.Response = FormatResponse(httpContext.Response, outputCaptureOwin, transactionId);
                    if(eventModel.CompanyId == null)
                    {
                       
                        eventModel.CompanyId = LoggerHelper.GetConfigValues("IdentifyCompany", moesifOptions, httpContext.Request, httpContext.Response, debug);
                    }
                    if(eventModel.UserId == null)
                    {
                        eventModel.UserId = getUserId(httpContext, eventModel.Request);
                    }
                    eventModel.Metadata = LoggerHelper.GetConfigObjectValues("GetMetadata", moesifOptions, httpContext.Request, httpContext.Response, debug);
                    eventModel.SessionToken = LoggerHelper.GetConfigValues("GetSessionToken", moesifOptions, httpContext.Request, httpContext.Response, debug);

                    LoggerHelper.LogDebugMessage(debug, "Calling the API to send the event to Moesif");
                    //Send event to Moesif async
                    await Task.Run(async () => await LogEventAsync(eventModel));
                }
            }
        }

        private async Task<(EventRequestModel, String)> FormatRequest(HttpRequest request, string transactionId)
        {
            // Request headers
            var reqHeaders = LoggerHelper.ToHeaders(request.Headers, debug);

            // RequestBody
            request.EnableBuffering(bufferThreshold: 1000000);
            string bodyAsText = null;

            string contentEncoding = "";
            string contentLength = "";
            int parsedContentLength = 100000;

            reqHeaders.TryGetValue("Content-Encoding", out contentEncoding);
            reqHeaders.TryGetValue("Content-Length", out contentLength);
            int.TryParse(contentLength, out parsedContentLength);

            bodyAsText = await LoggerHelper.GetRequestContents(bodyAsText, request, contentEncoding, parsedContentLength, debug);

            // Add Transaction Id to the Request Header
            bool disableTransactionId = LoggerHelper.GetConfigBoolValues(moesifOptions, "DisableTransactionId", false);
            if (!disableTransactionId)
            {
                transactionId = LoggerHelper.GetOrCreateTransactionId(reqHeaders, "X-Moesif-Transaction-Id");
                reqHeaders = LoggerHelper.AddTransactionId("X-Moesif-Transaction-Id", transactionId, reqHeaders);
            }

            // Serialize request body
            var bodyWrapper = LoggerHelper.Serialize(bodyAsText, request.ContentType, logBody, debug);

            // Client Ip Address
            string ip = clientIpHelper.GetClientIp(reqHeaders, request);
            var uri = new Uri(request.GetDisplayUrl()).ToString();

            string apiVersion = null;
            var apiVersion_out = new object();
            var getApiVersion = moesifOptions.TryGetValue("ApiVersion", out apiVersion_out);
            if (getApiVersion)
            {
                apiVersion = apiVersion_out.ToString();
            }

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
            return (eventReq, transactionId);
        }

        private EventResponseModel FormatResponse(HttpResponse response, StreamHelper stream, string transactionId)
        {
            // Response headers
            var rspHeaders = LoggerHelper.ToHeaders(response.Headers, debug);

            // ResponseBody
            string contentEncoding = "";
            rspHeaders.TryGetValue("Content-Encoding", out contentEncoding);
            string text = stream.ReadStream(contentEncoding);

            // Add Transaction Id to Response Header
            rspHeaders = LoggerHelper.AddTransactionId("X-Moesif-Transaction-Id", transactionId, rspHeaders);

            // Serialize Response body
            var responseWrapper = LoggerHelper.Serialize(text, response.ContentType, logBody, debug);

            var eventRsp = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = response.StatusCode,
                Headers = rspHeaders,
                Body = responseWrapper.Item1,
                TransferEncoding = responseWrapper.Item2
            };
            return eventRsp;
        }

        private String getUserId(HttpContext httpContext, EventRequestModel request)
        {
            string userId = httpContext?.User?.Identity?.Name;
            userId = LoggerHelper.GetConfigValues("IdentifyUser", moesifOptions, httpContext.Request, httpContext.Response, debug, userId);
            if (string.IsNullOrEmpty(userId))
            {
                // Fetch userId from authorization header
                userId = userHelper.fetchUserFromAuthorizationHeader(request.Headers, authorizationHeaderName, authorizationUserIdField);
            }
            return userId;
        }

        private async Task LogEventAsync(EventModel eventModel)
        {

            // Get Mask Event
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
                    LoggerHelper.LogDebugMessage(debug, "Can not execute MASK_EVENT_MODEL function. Please check moesif settings.");
                }
            }

            // Send Events
            try
            {
                // Get Sampling percentage
                RequestMap requestMap = RequestMapHelper.createRequestMap(eventModel);
                var samplingPercentage = AppConfigHelper.getSamplingPercentage(config, requestMap);

                Random random = new Random();
                double randomPercentage = random.NextDouble() * 100;
                if (samplingPercentage >= randomPercentage)
                {
                    eventModel.Weight = AppConfigHelper.calculateWeight(samplingPercentage);

                    if (isBatchingEnabled)
                    {
                        LoggerHelper.LogDebugMessage(debug, "Add Event to the batch");

                        if (MoesifQueue.Count < queueSize)
                        {
                            // Add event to the batch
                            MoesifQueue.Enqueue(eventModel);
                        }
                        else
                        {
                            LoggerHelper.LogDebugMessage(debug, "Queue is full, skip adding events ");
                        }
                    }
                    else
                    {
                        await client.Api.CreateEventAsync(eventModel);

                        LoggerHelper.LogDebugMessage(debug, "Event sent successfully to Moesif");
                    }
                }
                else
                {
                    LoggerHelper.LogDebugMessage(debug, "Skipped Event due to sampling percentage: " + samplingPercentage.ToString() + " and random percentage: " + randomPercentage.ToString());
                }
            }
            catch (APIException inst)
            {
                if (401 <= inst.ResponseCode && inst.ResponseCode <= 403)
                {
                    Console.WriteLine("Unauthorized access sending event to Moesif. Please check your Appplication Id.");
                }
                if (debug)
                {
                    Console.WriteLine("Error sending event to Moesif, with status code:");
                    Console.WriteLine(inst.ResponseCode);
                }
            }
        }

    }
}
#endif
