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

        public string userId;

        public string companyId;

        public string sessionToken;

        public Dictionary<string, object> metadata;

        public Dictionary<string, object> moesifOptions;

        public MoesifApiClient client;

        // Initialize config dictionary
        public AppConfig appConfig;

        // Initialized config response
        public Api.Http.Response.HttpStringResponse config;

        // App Config samplingPercentage
        public int samplingPercentage;

        // App Config configETag
        public string configETag;

        // App Config lastUpdatedTime
        public DateTime lastUpdatedTime;

        // Enable Batching
        public bool isBatchingEnabled;

        // Queue batch size
        public int batchSize;

        // Moesif Queue
        public Queue<EventModel> MoesifQueue;

        public bool debug;

        public bool logBody;

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public string transactionId;

        public bool Debug()
        {
            bool localDebug;
            var debug_out = new object();
            var getDebug = moesifOptions.TryGetValue("LocalDebug", out debug_out);
            if (getDebug)
            {
                localDebug = Convert.ToBoolean(debug_out);
            }
            else
            {
                localDebug = false;
            }
            return localDebug;
        }

        public bool LogBody()
        {
            bool localLogBody;
            var log_body_out = new object();
            var getLogBody = moesifOptions.TryGetValue("LogBody", out log_body_out);
            if (getLogBody)
            {
                localLogBody = Convert.ToBoolean(log_body_out);
            }
            else
            {
                localLogBody = true;
            }
            return localLogBody;
        }

        public bool EnableBatching()
        {
            bool localEnableBatching;
            var batching_out = new object();
            var getEnableBatching = moesifOptions.TryGetValue("EnableBatching", out batching_out);
            if (getEnableBatching)
            {
                localEnableBatching = Convert.ToBoolean(batching_out);
            }
            else
            {
                localEnableBatching = false;
            }
            return localEnableBatching;
        }

        public int BatchSize()
        {
            int localBatchSize;
            var batch_size_out = new object();
            var getBatchSize = moesifOptions.TryGetValue("BatchSize", out batch_size_out);
            if (getBatchSize)
            {
                localBatchSize = Convert.ToInt32(batch_size_out);
            }
            else
            {
                localBatchSize = 25;
            }
            return localBatchSize;
        }

        public MoesifMiddlewareNetCore(Dictionary<string, object> _middleware)
        {
            moesifOptions = _middleware;

            try
            {
                // Initialize client
                client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString());
                debug = Debug();
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
                client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString());
                debug = Debug();
                logBody = LogBody();
                _next = next;
                // Initialize Transaction Id
                transactionId = null;
                // Create a new instance of AppConfig
                appConfig = new AppConfig();

                // Enable batching
                isBatchingEnabled = EnableBatching();

                // Batch Size
                batchSize = BatchSize();

                // Default configuration values
                samplingPercentage = 100;
                configETag = null;
                lastUpdatedTime = DateTime.UtcNow;

                // Check if batching is enabled
                if (isBatchingEnabled)
                {
                    // Initialize queue
                    MoesifQueue = new Queue<EventModel>();

                    // Create a new thread to read the queue and send event to moesif
                    new Thread(async () =>
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
                            if (debug)
                            {
                                Console.WriteLine("Error while parsing application configuration on initialization");
                            }
                        }
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
                            if (debug)
                            {
                                Console.WriteLine("Error while scheduling events batch job every 5 seconds");
                            }
                        }
                    }).Start();
                }
                else
                {
                    // Create a new thread to get the application config
                    new Thread(async () =>
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
                            if (debug)
                            {
                                Console.WriteLine("Error while parsing application configuration on initialization");
                            }
                        }
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
            UserHelper user = new UserHelper();
            user.UpdateUser(client, userProfile, debug);
        }

        // Function to update users in batch
        public void UpdateUsersBatch(List<Dictionary<string, object>> userProfiles)
        {
            UserHelper user = new UserHelper();
            user.UpdateUsersBatch(client, userProfiles, debug);
        }

        // Function to update company
        public void UpdateCompany(Dictionary<string, object> companyProfile)
        {
            CompanyHelper company = new CompanyHelper();
            company.UpdateCompany(client, companyProfile, debug);
        }

        // Function to update companies in batch
        public void UpdateCompaniesBatch(List<Dictionary<string, object>> companyProfiles)
        {
            CompanyHelper company = new CompanyHelper();
            company.UpdateCompaniesBatch(client, companyProfiles, debug);
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var request = await FormatRequest(httpContext.Request);

            var originalBodyStream = httpContext.Response.Body;

            // Add Transaction Id to the Response Header
            if (!string.IsNullOrEmpty(transactionId))
            {
                httpContext.Response.Headers.Add("X-Moesif-Transaction-Id", transactionId);
            }

            using (var responseBody = new MemoryStream())
            {
                httpContext.Response.Body = responseBody;

                await _next(httpContext);

                var response = await FormatResponse(httpContext.Response);

                // User Id 
                var user_out = new object();
                var getUserId = moesifOptions.TryGetValue("IdentifyUser", out user_out);

                Func<HttpRequest, HttpResponse, string> IdentifyUser = null;
                if (getUserId)
                {
                    IdentifyUser = (Func<HttpRequest, HttpResponse, string>)(user_out);
                }

                // SessionToken
                var token_out = new object();
                var getSessionToken = moesifOptions.TryGetValue("GetSessionToken", out token_out);

                Func<HttpRequest, HttpResponse, string> GetSessionToken = null;
                if (getSessionToken)
                {
                    GetSessionToken = (Func<HttpRequest, HttpResponse, string>)(token_out);
                }

                // Get Metadata
                var metadata_out = new object();
                var getMetadata = moesifOptions.TryGetValue("GetMetadata", out metadata_out);

                Func<HttpRequest, HttpResponse, Dictionary<string, object>> GetMetadata = null;

                if (getMetadata)
                {
                    GetMetadata = (Func<HttpRequest, HttpResponse, Dictionary<string, object>>)(metadata_out);
                }

                // UserId
                userId = null;
                if (IdentifyUser != null)
                {
                    try
                    {
                        userId = IdentifyUser(httpContext.Request, httpContext.Response);
                    }
                    catch
                    {
                        Console.WriteLine("Can not execute IdentifyUser function. Please check moesif settings.");
                    }
                }

                // CompanyId
                var company_out = new object();
                var getCompanyId = moesifOptions.TryGetValue("IdentifyCompany", out company_out);

                Func<HttpRequest, HttpResponse, string> IdentifyCompany = null;
                if (getCompanyId)
                {
                    IdentifyCompany = (Func<HttpRequest, HttpResponse, string>)(company_out);
                }

                companyId = null;
                if (IdentifyCompany != null)
                {
                    try
                    {
                        companyId = IdentifyCompany(httpContext.Request, httpContext.Response);
                    }
                    catch
                    {
                        Console.WriteLine("Can not execute IdentifyCompany function. Please check moesif settings.");
                    }
                }

                // Session Token
                sessionToken = null;
                if (GetSessionToken != null)
                {
                    try
                    {
                        sessionToken = GetSessionToken(httpContext.Request, httpContext.Response);
                    }
                    catch
                    {
                        Console.WriteLine("Can not execute GetSessionToken function. Please check moesif settings.");
                    }
                }

                // Metadata
                metadata = null;
                if (GetMetadata != null)
                {
                    try
                    {
                        metadata = GetMetadata(httpContext.Request, httpContext.Response);
                    }
                    catch
                    {
                        Console.WriteLine("Can not execute GetMetadata function. Please check moesif settings.");
                    }
                }

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
                        if (debug)
                        {
                            Console.WriteLine("Skipping the event");
                        }

                        await responseBody.CopyToAsync(originalBodyStream);
                    }
                    else
                    {
                        if (debug)
                        {
                            Console.WriteLine("Calling the API to send the event to Moesif");
                        }

                        //Send event to Moesif async
                        Middleware(request, response);

                        await responseBody.CopyToAsync(originalBodyStream);
                    }
                }
                else
                {
                    if (debug)
                    {
                        Console.WriteLine("Calling the API to send the event to Moesif");
                    }

                    //Send event to Moesif async
                    Middleware(request, response);

                    await responseBody.CopyToAsync(originalBodyStream);
                }
            }
        }

        private async Task<EventRequestModel> FormatRequest(HttpRequest request)
        {
            var body = request.Body;

            request.EnableBuffering();

            var buffer = new byte[Convert.ToInt32(request.ContentLength)];

            await request.Body.ReadAsync(buffer, 0, buffer.Length);

            var bodyAsText = Encoding.UTF8.GetString(buffer);

            request.Body = body;

            var reqHeaders = new Dictionary<string, string>();
            try
            {
                reqHeaders = request.Headers.ToDictionary(k => k.Key, k => k.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception inst)
            {
                if (debug)
                {
                    Console.WriteLine("error encountered while copying request header");
                    Console.WriteLine(inst);
                }
            }

            // Add Transaction Id to the Request Header
            var transation_id_out = new Object();
            var captureTransactionId = moesifOptions.TryGetValue("DisableTransactionId", out transation_id_out);

            bool GetCaptureTransactionId = false;
            if (captureTransactionId)
            {
                GetCaptureTransactionId = (bool)(transation_id_out);
            }

            if (!GetCaptureTransactionId)
            {
                if (reqHeaders.ContainsKey("X-Moesif-Transaction-Id"))
                {
                    string reqTransId = reqHeaders["X-Moesif-Transaction-Id"];
                    if (!string.IsNullOrEmpty(reqTransId))
                    {
                        transactionId = reqTransId;
                        if (string.IsNullOrEmpty(transactionId))
                        {
                            transactionId = Guid.NewGuid().ToString();
                        }
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
                // Add Transaction Id to the Request Header
                reqHeaders["X-Moesif-Transaction-Id"] = transactionId;
            }

            var reqBody = new object();
            reqBody = null;
            string requestTransferEncoding;
            requestTransferEncoding = null;
            if (logBody)
            {
                try
                {
                    reqBody = ApiHelper.JsonDeserialize<object>(bodyAsText);
                }
                catch (Exception inst)
                {
                    if (debug)
                    {
                        Console.WriteLine("About to parse Request body as Base64 encoding");
                    }
                    // Get Request Body
                    reqBody = Base64Encode(bodyAsText);
                    requestTransferEncoding = "base64";
                }
            }

            string ip = null;
            try
            {
                List<string> proxyHeaders = new List<string> { "X-Client-Ip", "X-Forwarded-For","Cf-Connecting-Ip", "True-Client-Ip",
                "X-Real-Ip", "X-Cluster-Client-Ip", "X-Forwarded", "Forwarded-For", "Forwarded"};

                if (!proxyHeaders.Intersect(reqHeaders.Keys.ToList(), StringComparer.OrdinalIgnoreCase).Any())
                {
                    ip = request.HttpContext.Connection.RemoteIpAddress.ToString();
                }
                else
                {
                    ClientIp helpers = new ClientIp();
                    ip = helpers.GetClientIp(reqHeaders, request);
                }
            }
            catch (Exception inst)
            {
                Console.WriteLine("error encountered while trying to get the client IP address");
                Console.WriteLine(inst);
            }

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
                Body = reqBody,
                TransferEncoding = requestTransferEncoding
            };

            return eventReq;
        }

        private async Task<EventResponseModel> FormatResponse(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);

            string text = await new StreamReader(response.Body).ReadToEndAsync();

            response.Body.Seek(0, SeekOrigin.Begin);

            var rspHeaders = new Dictionary<string, string>();
            try
            {
                rspHeaders = response.Headers.ToDictionary(k => k.Key, k => k.Value.ToString());
            }
            catch (Exception inst)
            {
                if (debug)
                {
                    Console.WriteLine("error encountered while copying response header");
                    Console.WriteLine(inst);
                }
            }

            // Add Transaction Id to Response Header
            if (!string.IsNullOrEmpty(transactionId))
            {
                rspHeaders["X-Moesif-Transaction-Id"] = transactionId;
            }

            var rspBody = new object();
            rspBody = null;
            string responseTransferEncoding;
            responseTransferEncoding = null;
            if (logBody)
            {
                try
                {
                    rspBody = ApiHelper.JsonDeserialize<object>(text);
                }
                catch (Exception inst)
                {
                    if (debug)
                    {
                        Console.WriteLine("About to parse Response body as Base64 encoding");
                    }
                    // Get Response body
                    rspBody = Base64Encode(text);
                    responseTransferEncoding = "base64";
                }
            }

            var eventRsp = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = response.StatusCode,
                Headers = rspHeaders,
                Body = rspBody,
                TransferEncoding = responseTransferEncoding
            };

            return eventRsp;
        }

        private async Task Middleware(EventRequestModel event_request, EventResponseModel event_response)
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
                    Console.WriteLine("Can not execute MASK_EVENT_MODEL function. Please check moesif settings.");
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
                        if (debug)
                        {
                            Console.WriteLine("Add Event to the batch");
                        }
                        // Add event to the batch
                        MoesifQueue.Enqueue(eventModel);
                    }
                    else
                    {
                        var createEventResponse = await client.Api.CreateEventAsync(eventModel);
                        var eventResponseConfigETag = createEventResponse["X-Moesif-Config-ETag"];

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
                                if (debug)
                                {
                                    Console.WriteLine("Error while updating the application configuration");
                                }
                            }
                        }
                        if (debug)
                        {
                            Console.WriteLine("Event sent successfully to Moesif");
                        }
                    }
                }
                else
                {
                    if (debug)
                    {
                        Console.WriteLine("Skipped Event due to sampling percentage: " + samplingPercentage.ToString() + " and random percentage: " + randomPercentage.ToString());
                    }
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
