﻿using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moesif.Api;
using Moesif.Api.Models;
using Moesif.Api.Controllers;
using Moesif.Api.Exceptions;
using System.Threading;
using System.Collections.Concurrent;
using Moesif.Middleware.Helpers;
using Moesif.Middleware.Models;

#if NET45
using Microsoft.Owin;
using System.Web;
using Moesif.Middleware.NetFramework.Helpers;
#endif

#if NET45
namespace Moesif.Middleware.NetFramework
{
    public class MoesifMiddlewareNetFramework : OwinMiddleware
    {
        public Dictionary<string, object> moesifOptions;

        public MoesifApiClient client;

        public UserHelper userHelper; // Initialize user helper

        public CompanyHelper companyHelper; // Initialize company helper
        
        public ClientIp clientIpHelper; // Initialize client ip helper

        public AppConfig config; // The only AppConfig instance shared among threads 

        public bool isBatchingEnabled; // Enable Batching

        public bool disableStreamOverride; // Reset Request Body position

        public int batchSize; // Queue batch size

        public int queueSize; // Event Queue size 

        public int batchMaxTime; // Time in seconds for next batch

        public int appConfigSyncTime; // Time in seconds to sync application configuration

        public ConcurrentQueue<EventModel> MoesifQueue; // Moesif Queue

        public string authorizationHeaderName; // A request header field name used to identify the User

        public string authorizationUserIdField; // A field name used to parse the User from authorization header

        public bool debug;

        public bool logBody;

        public DateTime lastWorkerRun = DateTime.MinValue;

        public DateTime lastAppConfigWorkerRun = DateTime.MinValue;

        public MoesifMiddlewareNetFramework(OwinMiddleware next, Dictionary<string, object> _middleware) : base(next)
        {
            moesifOptions = _middleware;

            try
            {
                // Initialize client
                debug = LoggerHelper.GetConfigBoolValues(moesifOptions, "LocalDebug", false);
                client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString(), "moesif-netframework/1.3.8", debug);
                logBody = LoggerHelper.GetConfigBoolValues(moesifOptions, "LogBody", true);
                isBatchingEnabled = LoggerHelper.GetConfigBoolValues(moesifOptions, "EnableBatching", true); // Enable batching
                disableStreamOverride = LoggerHelper.GetConfigBoolValues(moesifOptions, "DisableStreamOverride", false); // Reset Request Body position
                batchSize = LoggerHelper.GetConfigIntValues(moesifOptions, "BatchSize", 25); // Batch Size
                queueSize = LoggerHelper.GetConfigIntValues(moesifOptions, "QueueSize", 1000); // Event Queue Size
                batchMaxTime = LoggerHelper.GetConfigIntValues(moesifOptions, "batchMaxTime", 2); // Batch max time in seconds
                appConfigSyncTime = LoggerHelper.GetConfigIntValues(moesifOptions, "appConfigSyncTime", 300); // App config sync time in seconds
                config = AppConfig.getDefaultAppConfig();
                userHelper = new UserHelper(); // Create a new instance of userHelper
                companyHelper = new CompanyHelper(); // Create a new instane of companyHelper
                clientIpHelper = new ClientIp(); // Create a new instance of client Ip
                authorizationHeaderName = LoggerHelper.GetConfigStringValues(moesifOptions, "AuthorizationHeaderName", "authorization");
                authorizationUserIdField = LoggerHelper.GetConfigStringValues(moesifOptions, "AuthorizationUserIdField", "sub");

                MoesifQueue = new ConcurrentQueue<EventModel>(); // Initialize queue

                if (isBatchingEnabled) ScheduleWorker();

                ScheduleAppConfig();
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
                    lastAppConfigWorkerRun = DateTime.UtcNow;
                    LoggerHelper.LogDebugMessage(debug, "Last App Config Worker Run - " + lastAppConfigWorkerRun.ToString() + " for thread Id - " + Thread.CurrentThread.ManagedThreadId.ToString());
                    try
                    {
                        // Get Application config
                        await AppConfigHelper.updateConfig(client, config, debug);

                    }
                    catch (Exception e)
                    {
                        LoggerHelper.LogDebugMessage(debug, "Error getting appConfig " + e.StackTrace);
                    }
                    Thread.Sleep(appConfigSyncTime * 1000);
                }
            });
            appConfigThread.IsBackground = true;
            appConfigThread.Start();
        }

        private void ScheduleWorker() 
        {
            LoggerHelper.LogDebugMessage(debug, "Starting a new thread to read the queue and send event to moesif");
            Thread workerThread = new Thread(async () => // Create a new thread to read the queue and send event to moesif
             {

                 Tasks task = new Tasks();
                 while (true)
                 {
                     Thread.Sleep(batchMaxTime * 1000);
                     try
                     {
                         lastWorkerRun = DateTime.UtcNow;
                         LoggerHelper.LogDebugMessage(debug, "Last Worker Run - " + lastWorkerRun.ToString() + " for thread Id - " + Thread.CurrentThread.ManagedThreadId.ToString());
                         await task.AsyncClientCreateEvent(client, MoesifQueue, config, batchSize, debug);

                     }
                     catch (Exception e)
                     {
                         LoggerHelper.LogDebugMessage(debug, "Error while scheduling events batch job " + e.StackTrace);
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

        public async override Task Invoke(IOwinContext httpContext) 
        {
            // Buffering mvc reponse
            StreamHelper outputCaptureMVC = null;
            HttpResponse httpResponse = HttpContext.Current?.Response;
            if (httpResponse != null)
            {
                outputCaptureMVC = new StreamHelper(httpResponse.Filter);
                httpResponse.Filter = outputCaptureMVC;
            }

            // Buffering Owin response
            IOwinResponse owinResponse = httpContext.Response;
            StreamHelper outputCaptureOwin = new StreamHelper(owinResponse.Body);
            owinResponse.Body = outputCaptureOwin;

            // Initialize Transaction Id
            string transactionId = null;
            EventRequestModel request;

            // Prepare Moeif Event Request Model
            (request, transactionId) = await ToRequest(httpContext.Request, transactionId);

            // Add Transaction Id to the Response Header
            if (!string.IsNullOrEmpty(transactionId))
            {
                httpContext.Response.Headers.Append("X-Moesif-Transaction-Id", transactionId);
            }

            await Next.Invoke(httpContext);

            // Get Skip
            var skip_out = new object();
            var getSkip = moesifOptions.TryGetValue("Skip", out skip_out);

            // Check to see if we need to send event to Moesif
            Func<IOwinRequest, IOwinResponse, bool> ShouldSkip = null;

            if (getSkip)
            {
                ShouldSkip = (Func<IOwinRequest, IOwinResponse, bool>)(skip_out);
            }

            if (ShouldSkip != null && ShouldSkip(httpContext.Request, httpContext.Response))
            {
                LoggerHelper.LogDebugMessage(debug, "Skip sending event to Moesif");
            }
            else
            {
                // Select stream to use 
                StreamHelper streamToUse = (outputCaptureMVC == null || outputCaptureMVC.CopyStream.Length == 0) ? outputCaptureOwin : outputCaptureMVC;
            
                // Prepare Moesif Event Response model
                var response = ToResponse(httpContext.Response, streamToUse, transactionId);

                // UserId
                string userId = httpContext?.Authentication?.User?.Identity?.Name;
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

                LoggerHelper.LogDebugMessage(debug, "Calling the API to send the event to Moesif");
                await Task.Run(async () => await LogEventAsync(request, response, userId, companyId, sessionToken, metadata));
            }
        }

        private async Task<(EventRequestModel, String)> ToRequest(IOwinRequest request, string transactionId)
        {
            // Request headers
            var reqHeaders = LoggerHelper.ToHeaders(request.Headers, debug);

            // RequestBody
            string contentEncoding = "";
            string contentLength = "";
            int parsedContentLength = 100000;

            string body = null;
            reqHeaders.TryGetValue("Content-Encoding", out contentEncoding);
            reqHeaders.TryGetValue("Content-Length", out contentLength);
            int.TryParse(contentLength, out parsedContentLength);
            try
            { 
                body = await LoggerHelper.GetRequestContents(request, contentEncoding, parsedContentLength, disableStreamOverride);
            }
            catch 
            {
                LoggerHelper.LogDebugMessage(debug, "Cannot read request body.");
            }

            var bodyWrapper = LoggerHelper.Serialize(body, request.ContentType);

            // Add Transaction Id to the Request Header
            bool disableTransactionId = LoggerHelper.GetConfigBoolValues(moesifOptions, "DisableTransactionId", false);
            if (!disableTransactionId)
            {
                transactionId = LoggerHelper.GetOrCreateTransactionId(reqHeaders, "X-Moesif-Transaction-Id");
                reqHeaders = LoggerHelper.AddTransactionId("X-Moesif-Transaction-Id", transactionId, reqHeaders);
            }

            string ip = clientIpHelper.GetClientIp(reqHeaders, request);
            var uri = request.Uri.ToString();

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

        private EventResponseModel ToResponse(IOwinResponse response, StreamHelper outputStream, string transactionId)
        {

            var rspHeaders = LoggerHelper.ToHeaders(response.Headers, debug);

            // ResponseBody
            string contentEncoding = "";
            rspHeaders.TryGetValue("Content-Encoding", out contentEncoding);

            var body = LoggerHelper.GetOutputFilterStreamContents(outputStream, contentEncoding);            
            var bodyWrapper = LoggerHelper.Serialize(body, response.ContentType);

            // Add Transaction Id to Response Header
            rspHeaders = LoggerHelper.AddTransactionId("X-Moesif-Transaction-Id", transactionId, rspHeaders);

            var eventRsp = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = response.StatusCode,
                Headers = rspHeaders,
                Body = bodyWrapper.Item1,
                TransferEncoding = bodyWrapper.Item2
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
                RequestMap requestMap = RequestMapHelper.createRequestMap(eventModel);


                // If available, get sampling percentage based on config else default to 100
                var samplingPercentage = AppConfigHelper.getSamplingPercentage(config, requestMap);

                if (debug)
                {
                    Console.WriteLine("sampling rate is ", samplingPercentage);
                 }
                Random random = new Random();
                double randomPercentage = random.NextDouble() * 100;
                if (samplingPercentage >= randomPercentage)
                {
                    // If available, get weight based on sampling percentage else default to 1
                    eventModel.Weight = AppConfigHelper.calculateWeight(samplingPercentage);

                    if (isBatchingEnabled)
                    {
                        LoggerHelper.LogDebugMessage(debug, "Add Event to the batch");
                        // Add event to queue
                        if (MoesifQueue.Count < queueSize) 
                        {
                            MoesifQueue.Enqueue(eventModel);
                            if (DateTime.Compare(lastWorkerRun, DateTime.MinValue) != 0 )
                            {
                                if (lastWorkerRun.AddMinutes(1) < DateTime.UtcNow) {
                                    LoggerHelper.LogDebugMessage(debug, "Scheduling worker thread. lastWorkerRun=" + lastWorkerRun.ToString());
                                    ScheduleWorker();
                                }
                            }
                        } 
                        else {
                            LoggerHelper.LogDebugMessage(debug, "Queue is full, skip adding events ");
                        }
                    } else {
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