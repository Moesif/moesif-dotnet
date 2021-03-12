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
using Moesif.Middleware.Helpers;

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

        public AppConfig appConfig; // Initialize config dictionary

        public UserHelper userHelper; // Initialize user helper

        public CompanyHelper companyHelper; // Initialize company helper

        public ClientIp clientIpHelper; // Initialize client ip helper

        public Api.Http.Response.HttpStringResponse config; // Initialized config response

        public int samplingPercentage; // App Config samplingPercentage

        public string configETag; // App Config configETag

        public DateTime lastUpdatedTime; // App Config lastUpdatedTime

        public bool isBatchingEnabled; // Enable Batching

        public int batchSize; // Queue batch size

        public int queueSize; // Event Queue size

        public int batchMaxTime; // Time in seconds for next batch

        public Queue<EventModel> MoesifQueue; // Moesif Queue

        public string authorizationHeaderName; // A request header field name used to identify the User

        public string authorizationUserIdField; // A field name used to parse the User from authorization header

        public DateTime lastWorkerRun = DateTime.MinValue;

        public bool debug;

        public bool logBody;

        public MoesifMiddlewareNetCore(Dictionary<string, object> _middleware)
        {
            moesifOptions = _middleware;

            try
            {
                // Initialize client
                client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString());
                debug = LoggerHelper.GetConfigBoolValues(moesifOptions, "LocalDebug", false);
            }
            catch (Exception e)
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
                client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString(), "moesif-netcore/1.3.8", debug);
                logBody = LoggerHelper.GetConfigBoolValues(moesifOptions, "LogBody", true);
                _next = next;
                appConfig = new AppConfig(); // Create a new instance of AppConfig
                userHelper = new UserHelper(); // Create a new instance of userHelper
                companyHelper = new CompanyHelper(); // Create a new instane of companyHelper
                clientIpHelper = new ClientIp(); // Create a new instance of client Ip
                isBatchingEnabled = LoggerHelper.GetConfigBoolValues(moesifOptions, "EnableBatching", true); // Enable batching
                batchSize = LoggerHelper.GetConfigIntValues(moesifOptions, "BatchSize", 25); // Batch Size
                queueSize = LoggerHelper.GetConfigIntValues(moesifOptions, "QueueSize", 1000); // Queue Size
                batchMaxTime = LoggerHelper.GetConfigIntValues(moesifOptions, "batchMaxTime", 2); // Batch max time in seconds
                authorizationHeaderName = LoggerHelper.GetConfigStringValues(moesifOptions, "AuthorizationHeaderName", "authorization");
                authorizationUserIdField = LoggerHelper.GetConfigStringValues(moesifOptions, "AuthorizationUserIdField", "sub");
                samplingPercentage = 100; // Default sampling percentage
                configETag = null; // Default configETag
                lastUpdatedTime = DateTime.UtcNow; // Default lastUpdatedTime
                MoesifQueue = new Queue<EventModel>(); // Initialize queue

                new Thread(async () => // Create a new thread to read the queue and send event to moesif
                {
                    Thread.CurrentThread.IsBackground = true;
                    try
                    {
                        // Get Application config
                        config = await appConfig.getConfig(client, debug);
                        if (!string.IsNullOrEmpty(config.ToString()))
                        {
                            (configETag, samplingPercentage, lastUpdatedTime) = appConfig.parseConfiguration(config, debug);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerHelper.LogDebugMessage(debug, "Error while parsing application configuration on initialization");
                    }
                    if (isBatchingEnabled)
                    {
                        ScheduleWorker();
                    }
                }).Start();
            }
            catch (Exception e)
            {
                throw new Exception("Please provide the application Id to send events to Moesif");
            }
        }

        private void ScheduleWorker()
        {
            LoggerHelper.LogDebugMessage(debug, "Starting a new thread to read the queue and send event to moesif");

            new Thread(async () => // Create a new thread to read the queue and send event to moesif
            {

                Tasks task = new Tasks();
                while (true)
                {
                    Thread.Sleep(batchMaxTime * 1000);
                    try
                    {
                        lastWorkerRun = DateTime.UtcNow;
                        LoggerHelper.LogDebugMessage(debug, "Last Worker Run - " + lastWorkerRun.ToString() + " for thread Id - " + Thread.CurrentThread.ManagedThreadId.ToString());
                        var updatedConfig = await task.AsyncClientCreateEvent(client, MoesifQueue, batchSize, debug, config, configETag, samplingPercentage, lastUpdatedTime, appConfig);
                        (config, configETag, samplingPercentage, lastUpdatedTime) = (updatedConfig.Item1, updatedConfig.Item2, updatedConfig.Item3, updatedConfig.Item4);
                    }
                    catch (Exception ex)
                    {
                        LoggerHelper.LogDebugMessage(debug, "Error while scheduling events batch job");
                    }
                }
            }).Start();
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

            // Buffering Owin response
            var owinResponse = httpContext.Response;
            StreamHelper outputCaptureOwin = new StreamHelper(owinResponse.Body);
            owinResponse.Body = outputCaptureOwin;

            await _next(httpContext);

            var response = FormatResponse(httpContext.Response, outputCaptureOwin, transactionId);

            // UserId
            string userId = httpContext?.User?.Identity?.Name;
            userId = LoggerHelper.GetConfigValues("IdentifyUser", moesifOptions, httpContext.Request, httpContext.Response, debug, userId);
            if (string.IsNullOrEmpty(userId))
            {
                // Fetch userId from authorization header
                userId = userHelper.fetchUserFromAuthorizationHeader(request.Headers, authorizationHeaderName, authorizationUserIdField);
            }

            // CompanyId
            string companyId = LoggerHelper.GetConfigValues("IdentifyCompany", moesifOptions, httpContext.Request, httpContext.Response, debug);
            // SessionToken
            string sessionToken = LoggerHelper.GetConfigValues("GetSessionToken", moesifOptions, httpContext.Request, httpContext.Response, debug);
            // Metadata
            Dictionary<string, object> metadata = LoggerHelper.GetConfigObjectValues("GetMetadata", moesifOptions, httpContext.Request, httpContext.Response, debug);

            // Get Skip
            var skip_out = new object();
            var getSkip = moesifOptions.TryGetValue("Skip", out skip_out);

            // Check to see if we need to send event to Moesif
            Func<HttpRequest, HttpResponse, bool> ShouldSkip = null;

            if (getSkip)
            {
                ShouldSkip = (Func<HttpRequest, HttpResponse, bool>)(skip_out);
            }

            if (ShouldSkip != null)
            {
                if (ShouldSkip(httpContext.Request, httpContext.Response))
                {
                    LoggerHelper.LogDebugMessage(debug, "Skipping the event");
                }
                else
                {
                    LoggerHelper.LogDebugMessage(debug, "Calling the API to send the event to Moesif");
                    //Send event to Moesif async
                    await Task.Run(async () => await LogEventAsync(request, response, userId, companyId, sessionToken, metadata));
                }
            }
            else
            {
                LoggerHelper.LogDebugMessage(debug, "Calling the API to send the event to Moesif");
                //Send event to Moesif async
                await Task.Run(async () => await LogEventAsync(request, response, userId, companyId, sessionToken, metadata));
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

            bodyAsText = LoggerHelper.GetRequestContents(bodyAsText, request, contentEncoding, parsedContentLength, debug);

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

        private async Task LogEventAsync(EventRequestModel event_request, EventResponseModel event_response, string userId, string companyId, string sessionToken, Dictionary<string, object> metadata)
        {
            var eventModel = new EventModel()
            {
                Request = event_request,
                Response = event_response,
                UserId = userId,
                CompanyId = companyId,
                SessionToken = sessionToken,
                Metadata = metadata,
                Direction = "Incoming"
            };

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
                samplingPercentage = appConfig.getSamplingPercentage(config, userId, companyId);

                Random random = new Random();
                double randomPercentage = random.NextDouble() * 100;
                if (samplingPercentage >= randomPercentage)
                {
                    eventModel.Weight = appConfig.calculateWeight(samplingPercentage);

                    if (isBatchingEnabled)
                    {
                        LoggerHelper.LogDebugMessage(debug, "Add Event to the batch");

                        if (MoesifQueue.Count < queueSize)
                        {
                            // Add event to the batch
                            MoesifQueue.Enqueue(eventModel);

                            if (DateTime.Compare(lastWorkerRun, DateTime.MinValue) != 0)
                            {
                                if (lastWorkerRun.AddMinutes(1) < DateTime.UtcNow)
                                {
                                    LoggerHelper.LogDebugMessage(debug, "Scheduling worker thread. lastWorkerRun=" + lastWorkerRun.ToString());
                                    ScheduleWorker();
                                }
                            }
                        }
                        else
                        {
                            LoggerHelper.LogDebugMessage(debug, "Queue is full, skip adding events ");
                        }
                    }
                    else
                    {
                        var createEventResponse = await client.Api.CreateEventAsync(eventModel);
                        var eventResponseConfigETag = createEventResponse.ToDictionary(k => k.Key.ToLower(), k => k.Value)["x-moesif-config-etag"];

                        if (!(string.IsNullOrEmpty(eventResponseConfigETag)) &&
                            !(string.IsNullOrEmpty(configETag)) &&
                            configETag != eventResponseConfigETag &&
                            DateTime.UtcNow > lastUpdatedTime.AddMinutes(5))
                        {
                            try
                            {
                                // Get Application config
                                config = await appConfig.getConfig(client, debug);
                                if (!string.IsNullOrEmpty(config.ToString()))
                                {
                                    (configETag, samplingPercentage, lastUpdatedTime) = appConfig.parseConfiguration(config, debug);
                                }
                            }
                            catch (Exception ex)
                            {
                                LoggerHelper.LogDebugMessage(debug, "Error while updating the application configuration");
                            }
                        }
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
