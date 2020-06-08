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
using Moesif.Middleware.Helpers;

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
        public string userId;

        public string companyId;

        public string sessionToken;

        public Dictionary<string, object> metadata;

        public Dictionary<string, object> moesifOptions;

        public MoesifApiClient client;

        public AppConfig appConfig; // Initialize config dictionary

        public UserHelper userHelper; // Initialize user helper

        public CompanyHelper companyHelper; // Initialize company helper
        
        public ClientIp clientIpHelper; // Initialize client ip helper

        public Api.Http.Response.HttpStringResponse config; // Initialize config response

        public int samplingPercentage; // Initialize samplingPercentage

        public string configETag; // Initialize configETag

        public DateTime lastUpdatedTime; // Initialize lastUpdatedTime

        public bool isBatchingEnabled; // Enable Batching

        public int batchSize; // Queue batch size

        public Queue<EventModel> MoesifQueue; // Moesif Queue

        public bool debug;

        public bool logBody;

        public string transactionId;

        public MoesifMiddlewareNetFramework(OwinMiddleware next, Dictionary<string, object> _middleware) : base(next)
        {
            moesifOptions = _middleware;

            try
            {
                // Initialize client
                client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString());
                debug = LoggerHelper.GetConfigBoolValues(moesifOptions, "LocalDebug", false);
                logBody = LoggerHelper.GetConfigBoolValues(moesifOptions, "LogBody", true);
                isBatchingEnabled = LoggerHelper.GetConfigBoolValues(moesifOptions, "EnableBatching", false); // Enable batching
                batchSize = LoggerHelper.GetConfigIntValues(moesifOptions, "BatchSize", 25); // Batch Size
                transactionId = null; // Initialize Transaction Id
                appConfig = new AppConfig(); // Create a new instance of AppConfig
                userHelper = new UserHelper(); // Create a new instance of userHelper
                companyHelper = new CompanyHelper(); // Create a new instane of companyHelper
                clientIpHelper = new ClientIp(); // Create a new instance of client Ip
                samplingPercentage = 100; // Default sampling percentage
                configETag = null; // Default configETag
                lastUpdatedTime = DateTime.UtcNow; // Default lastUpdatedTime

                if (isBatchingEnabled)
                {
                    MoesifQueue = new Queue<EventModel>(); // Initialize queue

                    new Thread(async () => // Create a new thread to read the queue and send event to moesif
                    {
                        Thread.CurrentThread.IsBackground = true;
                        var initConfig = await appConfig.GetAppConfig(configETag, samplingPercentage, lastUpdatedTime, client, debug);
                        (config, configETag, samplingPercentage, lastUpdatedTime) = (initConfig.Item1, initConfig.Item2, initConfig.Item3, initConfig.Item4);

                        try
                        {
                            var startTimeSpan = TimeSpan.Zero;
                            var periodTimeSpan = TimeSpan.FromSeconds(1);
                            Tasks task = new Tasks();

                            var timer = new Timer((e) =>
                            {
                                var updatedConfig = task.AsyncClientCreateEvent(client, MoesifQueue, batchSize, debug, config, configETag, samplingPercentage, lastUpdatedTime, appConfig);
                                (config, configETag, samplingPercentage, lastUpdatedTime) = (updatedConfig.Result.Item1, updatedConfig.Result.Item2, updatedConfig.Result.Item3, updatedConfig.Result.Item4);
                            }, null, startTimeSpan, periodTimeSpan);
                        }
                        catch (Exception ex)
                        {
                            LoggerHelper.LogDebugMessage(debug, "Error while scheduling events batch job every 5 seconds");
                        }
                    }).Start();
                }
                else
                {
                    // Create a new thread to get the application config
                    new Thread(async () =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        var initConfig = await appConfig.GetAppConfig(configETag, samplingPercentage, lastUpdatedTime, client, debug);
                        (config, configETag, samplingPercentage, lastUpdatedTime) = (initConfig.Item1, initConfig.Item2, initConfig.Item3, initConfig.Item4);
                    }).Start();
                }
            }
            catch (Exception e)
            {
                throw new Exception("Please provide the application Id to send events to Moesif");
            }
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
            HttpResponse httpResponse = HttpContext.Current.Response;
            StreamHelper outputCaptureMVC = new StreamHelper(httpResponse.Filter);
            httpResponse.Filter = outputCaptureMVC;

            // Biffering Owin response
            IOwinResponse owinResponse = httpContext.Response;
            StreamHelper outputCaptureOwin = new StreamHelper(owinResponse.Body);
            owinResponse.Body = outputCaptureOwin;

            // Prepare Moeif Event Request Model
            var request = await ToRequest(httpContext.Request);

            // Add Transaction Id to the Response Header
            if (!string.IsNullOrEmpty(transactionId))
            {
                httpContext.Response.Headers.Append("X-Moesif-Transaction-Id", transactionId);
            }

            await Next.Invoke(httpContext);

            // Select stream to use 
            StreamHelper streamToUse = outputCaptureMVC.CopyStream.Length == 0 ? outputCaptureOwin : outputCaptureMVC;
           
            // Prepare Moesif Event Response model
            var response = await ToResponse(httpContext.Response, streamToUse);

            // UserId
            userId = LoggerHelper.GetConfigStringValues("IdentifyUser", moesifOptions, httpContext.Request, httpContext.Response, debug);
            // CompanyId
            companyId = LoggerHelper.GetConfigStringValues("IdentifyCompany", moesifOptions, httpContext.Request, httpContext.Response, debug);
            // SessionToken
            sessionToken = LoggerHelper.GetConfigStringValues("GetSessionToken", moesifOptions, httpContext.Request, httpContext.Response, debug);
            // Metadata
            metadata = LoggerHelper.GetConfigObjectValues("GetMetadata", moesifOptions, httpContext.Request, httpContext.Response, debug);

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
                LoggerHelper.LogDebugMessage(debug, "Calling the API to send the event to Moesif");

                //Send event to Moesif async
                LogEventAsync(request, response);
            }
        }

        private async Task<EventRequestModel> ToRequest(IOwinRequest request)
        {
            // Request headers
            var reqHeaders = LoggerHelper.ToHeaders(request.Headers, debug);

            // RequestBody
            string contentEncoding = "";
            reqHeaders.TryGetValue("Content-Encoding", out contentEncoding);
            var body = LoggerHelper.GetRequestContents(request, contentEncoding);
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

            return eventReq;
        }

        private async Task<EventResponseModel> ToResponse(IOwinResponse response, StreamHelper outputStream)
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

        private async Task LogEventAsync(EventRequestModel event_request, EventResponseModel event_response)
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
                // If available, get sampling percentage based on config else default to 100
                samplingPercentage = appConfig.getSamplingPercentage(config, userId, companyId);

                Random random = new Random();
                double randomPercentage = random.NextDouble() * 100;
                if (samplingPercentage >= randomPercentage)
                {
                    // If available, get weight based on sampling percentage else default to 1
                    eventModel.Weight = appConfig.calculateWeight(samplingPercentage);

                    if (isBatchingEnabled)
                    {
                        LoggerHelper.LogDebugMessage(debug, "Add Event to the batch");
                        // Add event to queue
                        MoesifQueue.Enqueue(eventModel);
                    } else {
                        var createEventResponse = await client.Api.CreateEventAsync(eventModel);
                        var eventResponseConfigETag = createEventResponse["X-Moesif-Config-ETag"];

                        if (!(string.IsNullOrEmpty(eventResponseConfigETag)) &&
                            !(string.IsNullOrEmpty(configETag)) &&
                            configETag != eventResponseConfigETag &&
                            DateTime.UtcNow > lastUpdatedTime.AddMinutes(5))
                        {
                            var updatedConfig = await appConfig.GetAppConfig(configETag, samplingPercentage, lastUpdatedTime, client, debug);
                            (config, configETag, samplingPercentage, lastUpdatedTime) = (updatedConfig.Item1, updatedConfig.Item2, updatedConfig.Item3, updatedConfig.Item4);
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