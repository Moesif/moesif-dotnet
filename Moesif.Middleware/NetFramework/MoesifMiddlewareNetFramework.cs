using System;
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
using System.ComponentModel.Design;
using Microsoft.Extensions.Logging;
#if NET461
using Microsoft.Owin;
using System.Web;
using Moesif.Middleware.NetFramework.Helpers;

#endif

#if NET461
namespace Moesif.Middleware.NetFramework
{
    public class MoesifMiddlewareNetFramework : OwinMiddleware
    {
        public Dictionary<string, object> moesifOptions;

        public MoesifApiClient client;

        public UserHelper userHelper; // Initialize user helper

        public CompanyHelper companyHelper; // Initialize company helper
        
        public ClientIp clientIpHelper; // Initialize client ip helper

        public volatile AppConfig config; // The only AppConfig instance shared among threads

        public volatile Governance governance;

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

        public string apiVersion;

        public bool logBody;

        private AutoResetEvent configEvent;

        private AutoResetEvent governanceEvent;

        public DateTime lastWorkerRun = DateTime.MinValue;

        public DateTime lastAppConfigWorkerRun = DateTime.MinValue;

        private ILogger _logger;

        private LoggerHelper loggerHelper;

        public MoesifMiddlewareNetFramework(Dictionary<String,object> options) : base(null)
        {
            moesifOptions = options;
            _logger = null;
            loggerHelper = new LoggerHelper(_logger);
            debug = loggerHelper.GetConfigBoolValues(moesifOptions, "LocalDebug", false);
            client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString(), "moesif-netframework/1.4.4", debug);
            userHelper = new UserHelper(); // Create a new instance of userHelper
            companyHelper = new CompanyHelper(); // Create a new instane of companyHelper
            clientIpHelper = new ClientIp(); // Create a new instance of client Ip
        }

        public MoesifMiddlewareNetFramework(OwinMiddleware next, Dictionary<string, object> _middleware, ILoggerFactory logger) : base(next)
        {
            moesifOptions = _middleware;
            _logger = logger.CreateLogger("Moesif.Middleware.NetFramework");
            loggerHelper = new LoggerHelper(_logger);

            try
            {
                // Initialize client
                debug = loggerHelper.GetConfigBoolValues(moesifOptions, "LocalDebug", false);
                client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString(), "moesif-netframework/1.4.4", debug);
                logBody = loggerHelper.GetConfigBoolValues(moesifOptions, "LogBody", true);
                isBatchingEnabled = loggerHelper.GetConfigBoolValues(moesifOptions, "EnableBatching", true); // Enable batching
                disableStreamOverride = loggerHelper.GetConfigBoolValues(moesifOptions, "DisableStreamOverride", false); // Reset Request Body position
                batchSize = loggerHelper.GetConfigIntValues(moesifOptions, "BatchSize", 200); // Batch Size
                queueSize = loggerHelper.GetConfigIntValues(moesifOptions, "QueueSize", 100*1000); // Event Queue Size
                batchMaxTime = loggerHelper.GetConfigIntValues(moesifOptions, "batchMaxTime", 2); // Batch max time in seconds
                appConfigSyncTime = loggerHelper.GetConfigIntValues(moesifOptions, "appConfigSyncTime", 300); // App config sync time in seconds
                config = AppConfig.getDefaultAppConfig();
                governance = Governance.getDefaultGovernance();
                userHelper = new UserHelper(); // Create a new instance of userHelper
                companyHelper = new CompanyHelper(); // Create a new instane of companyHelper
                clientIpHelper = new ClientIp(); // Create a new instance of client Ip
                authorizationHeaderName = loggerHelper.GetConfigStringValues(moesifOptions, "AuthorizationHeaderName", "authorization");
                authorizationUserIdField = loggerHelper.GetConfigStringValues(moesifOptions, "AuthorizationUserIdField", "sub");
                if (moesifOptions.TryGetValue("ApiVersion", out object version))
                {
                    apiVersion = version != null ? version.ToString() : null;
                }
                else
                {
                    apiVersion = null;
                }

                MoesifQueue = new ConcurrentQueue<EventModel>(); // Initialize queue

                configEvent = new System.Threading.AutoResetEvent(false);
                governanceEvent = new System.Threading.AutoResetEvent(false);

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
            _logger.LogDebug("Starting a new thread to sync the application configuration");

            Thread appConfigThread = new Thread(async () => // Create a new thread to fetch the application configuration
            {

                while (true)
                {
                    try
                    {
                        lastAppConfigWorkerRun = DateTime.UtcNow;
                        _logger.LogDebug("Last App Config Worker Run - " + lastAppConfigWorkerRun.ToString() + " for thread Id - " + Thread.CurrentThread.ManagedThreadId.ToString());

                        // update Application config
                        config = await AppConfigHelper.updateConfig(client, config, debug, _logger);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while scheduling appConfig job at {time}", DateTime.UtcNow);
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
                        governance = await GovernanceHelper.updateGovernance(client, governance, debug, _logger);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error while updating Governance rule at {tine}", DateTime.UtcNow);
                    }
                    governanceEvent.WaitOne( 60 * 1000);
                }
            });
            governanceThread.IsBackground = true;
            governanceThread.Start();
        }

        private void ScheduleWorker() 
        {
            _logger.LogDebug("Starting a new thread to read the queue and send event to moesif");
            Thread workerThread = new Thread(async () => // Create a new thread to read the queue and send event to moesif
             {

                 while (true)
                 {
                     await Task.Delay(batchMaxTime * 1000);
                     try
                     {
                         lastWorkerRun = DateTime.UtcNow;
                         _logger.LogDebug("Last Worker Run - " + lastWorkerRun.ToString() + " for thread Id - " + Thread.CurrentThread.ManagedThreadId.ToString());
                         await Tasks.AsyncClientCreateEvent(client, MoesifQueue, config, governance, configEvent, governanceEvent,batchSize, debug, _logger);

                     }
                     catch (Exception e)
                     {
                         _logger.LogDebug("Error while scheduling events batch job:{Error} ",e.Message);
                         _logger.LogError(e, "Error while scheduling events batch job");
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

            var eventModel = new EventModel()
            {
                Request = request,
                Response = null,
                Direction = "Incoming"
            };

            if (GovernanceHelper.isGovernaceRuleDefined(governance))
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
                eventModel.Response.Headers["X-Moesif-Transaction-Id"] = transactionId;
                _logger.LogDebug("Request is blocked by Governance rule {ruleId} " , eventModel.BlockedBy);
                await Task.Run(async () => await LogEventAsync(eventModel));
            }
            else
            {

                await Next.Invoke(httpContext);

                // Get Skip
                var getSkip = moesifOptions.TryGetValue("Skip", out object skip_out);

                // Check to see if we need to send event to Moesif
                Func<IOwinRequest, IOwinResponse, bool> ShouldSkip = null;

                if (getSkip)
                {
                    ShouldSkip = (Func<IOwinRequest, IOwinResponse, bool>)(skip_out);
                }

                if (ShouldSkip != null && ShouldSkip(httpContext.Request, httpContext.Response))
                {
                    _logger.LogDebug("Skip sending event to Moesif");
                }
                else
                {
                    // Select stream to use 
                    StreamHelper streamToUse = (outputCaptureMVC == null || outputCaptureMVC.CopyStream.Length == 0) ? outputCaptureOwin : outputCaptureMVC;

                    // Prepare Moesif Event Response model
                    eventModel.Response = ToResponse(httpContext.Response, streamToUse, transactionId);
                    if (eventModel.CompanyId == null)
                    {
                        eventModel.CompanyId = loggerHelper.GetConfigValues("IdentifyCompany", moesifOptions, httpContext.Request, httpContext.Response, debug);
                    }
                    if (eventModel.UserId == null)
                    {
                        eventModel.UserId = getUserId(httpContext, eventModel.Request);
                    }
                    eventModel.Metadata = loggerHelper.GetConfigObjectValues("GetMetadata", moesifOptions, httpContext.Request, httpContext.Response, debug);
                    eventModel.SessionToken = loggerHelper.GetConfigValues("GetSessionToken", moesifOptions, httpContext.Request, httpContext.Response, debug);

                    loggerHelper.LogDebugMessage(debug, "Calling the API to send the event to Moesif");
                    await Task.Run(async () => await LogEventAsync(eventModel));
                }
            }
        }

        private String getUserId(IOwinContext httpContext, EventRequestModel request)
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
                    userId = httpContext?.Authentication?.User?.Identity?.Name;
                }
                return userId;
            }
        }

        private async Task<(EventRequestModel, String)> ToRequest(IOwinRequest request, string transactionId)
        {
            // Request headers
            var reqHeaders = loggerHelper.ToHeaders(request.Headers, debug);

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
                body = await loggerHelper.GetRequestContents(request, contentEncoding, parsedContentLength, disableStreamOverride);
            }
            catch 
            {
                _logger.LogDebug("Cannot read request body.");
            }

            var bodyWrapper = loggerHelper.Serialize(body, request.ContentType, logBody);

            // Add Transaction Id to the Request Header
            bool disableTransactionId = loggerHelper.GetConfigBoolValues(moesifOptions, "DisableTransactionId", false);
            if (!disableTransactionId)
            {
                transactionId = loggerHelper.GetOrCreateTransactionId(reqHeaders, "X-Moesif-Transaction-Id");
                reqHeaders = loggerHelper.AddTransactionId("X-Moesif-Transaction-Id", transactionId, reqHeaders);
            }

            string ip = clientIpHelper.GetClientIp(reqHeaders, request);
            var uri = request.Uri.ToString();

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

            var rspHeaders = loggerHelper.ToHeaders(response.Headers, debug);

            // ResponseBody
            string contentEncoding = "";
            rspHeaders.TryGetValue("Content-Encoding", out contentEncoding);

            var body = loggerHelper.GetOutputFilterStreamContents(outputStream, contentEncoding);            
            var bodyWrapper = loggerHelper.Serialize(body, response.ContentType, logBody);

            // Add Transaction Id to Response Header
            rspHeaders = loggerHelper.AddTransactionId("X-Moesif-Transaction-Id", transactionId, rspHeaders);

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
                catch(Exception e)
                {
                    _logger.LogError(e ,"Can not execute MASK_EVENT_MODEL function. Please check moesif settings.");
                }
            }

            // Send Events
            try
            {
                RequestMap requestMap = RequestMapHelper.createRequestMap(eventModel);


                // If available, get sampling percentage based on config else default to 100
                var samplingPercentage = AppConfigHelper.getSamplingPercentage(config, requestMap);

                _logger.LogDebug("sampling rate is {samplingRate} ", samplingPercentage);
                Random random = new Random();
                double randomPercentage = random.NextDouble() * 100;
                if (samplingPercentage >= randomPercentage)
                {
                    // If available, get weight based on sampling percentage else default to 1
                    eventModel.Weight = AppConfigHelper.calculateWeight(samplingPercentage);

                    if (isBatchingEnabled)
                    {
                        _logger.LogDebug("Add Event to the batch");
                        // Add event to queue
                        if (MoesifQueue.Count < queueSize)
                        {
                            MoesifQueue.Enqueue(eventModel);
                        }
                        else
                        {
                            _logger.LogWarning("Queue is full, skip adding events ");
                        }
                    }
                    else
                    {
                        await client.Api.CreateEventAsync(eventModel);

                        _logger.LogDebug("Event sent successfully to Moesif at {time}", DateTime.UtcNow);
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
                _logger.LogWarning("Error sending event to Moesif, with status code:{errorCode}", inst.ResponseCode);
            }
        }

        
    }
}
#endif