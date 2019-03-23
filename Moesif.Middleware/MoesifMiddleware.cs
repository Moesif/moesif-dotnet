using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Moesif.Api;
using Moesif.Api.Models;
using Moesif.Api.Exceptions;
using Moesif.Api.Http.Client;
using Microsoft.AspNetCore.Http.Extensions;
using System.Threading;
using Moesif.Middleware.Helpers;

namespace Moesif.Middleware
{
    public class MoesifMiddleware
    {
        private readonly RequestDelegate _next;

        public string userId;

        public string sessionToken;

        public Dictionary<string, object> metadata;

        public Dictionary<string, object> moesifOptions;

        public MoesifApiClient client;

        // Initialize config dictionary
        public Dictionary<string, object> confDict = new Dictionary<string, object>();

        // Initialize cached_config_etag
        public string cachedConfigETag = null;

        public bool debug;

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

        // Get application configuration
        public async Task<Dictionary<string, object>> GetConfig(MoesifApiClient client, Dictionary<string, object> appConfigDict, string cachedETag)
        {
            // Remove cached ETag if exist
            if (!(string.IsNullOrEmpty(cachedETag)) && confDict.ContainsKey(cachedETag)) {
                confDict.Remove(cachedETag);
            }

            // Call the api to get the application config
            var appConfig = await client.Api.GetAppConfigAsync();

            // Set default config
            if (string.IsNullOrEmpty(appConfig.Body))
            {
                appConfigDict["ETag"] = null;
                appConfigDict["sample_rate"] = 100;
                appConfigDict["last_updated_time"] = DateTime.UtcNow;
            }
            // Set application config
            else {
                var rspBody = ApiHelper.JsonDeserialize<Dictionary<string, object>>(appConfig.Body);
                appConfigDict["ETag"] = appConfig.Headers["X-Moesif-Config-ETag"];
                appConfigDict["sample_rate"] = rspBody["sample_rate"];
                appConfigDict["last_updated_time"] = DateTime.UtcNow;    
            }

            // Return config dictionary
            return appConfigDict;
        }

        public MoesifMiddleware(RequestDelegate next, Dictionary<string, object> _middleware)
        {
            moesifOptions = _middleware;
            // Initialize client
            client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString());
            debug = Debug();
            _next = next;
            // Initialize Transaction Id
            transactionId = null;
            // Create a new thread to get the application config
            new Thread(async () =>
            {
                confDict = await GetConfig(client, confDict, cachedConfigETag);
            }).Start();
        }

        // Create UserModel
        public UserModel createUserModel(Dictionary<string, object> user) {
            var userAgentString = new object();
            var user_metadata = new object();
            var user_sessionToken = new object();
            var modifiedTime = new object();
            var ipAddress = new object();
            var userModel = new UserModel()
            {
                UserId = user["user_id"].ToString(),
                UserAgentString = user.TryGetValue("user_agent_string", out userAgentString) ? userAgentString.ToString() : null,
                Metadata = user.TryGetValue("metadata", out user_metadata) ? user_metadata : null,
                SessionToken = user.TryGetValue("session_token", out user_sessionToken) ? user_sessionToken.ToString() : null,
                ModifiedTime = user.TryGetValue("modified_time", out modifiedTime) ? (DateTime)modifiedTime : DateTime.UtcNow,
                IpAddress = user.TryGetValue("ip_address", out ipAddress) ? ipAddress.ToString() : null
            };
            return userModel;
        }

        // Function to update user
        public async void UpdateUser(Dictionary<string, object> userProfile){

            if(!userProfile.Any()) {
                Console.WriteLine("Expecting the input to be of the type - dictionary while updating user");
            }
            else {
                if(userProfile.ContainsKey("user_id")) {
                    
                    UserModel userModel = createUserModel(userProfile);

                    // Perform API call
                    try
                    {
                        await client.Api.UpdateUserAsync(userModel);
                        if (debug)
                        {
                            Console.WriteLine("User Updated Successfully");
                        }  
                    }
                    catch (APIException inst)
                    {
                        if (401 <= inst.ResponseCode && inst.ResponseCode <= 403)
                        {
                            Console.WriteLine("Unauthorized access updating user to Moesif. Please check your Appplication Id.");
                        }
                        if (debug)
                        {
                            Console.WriteLine("Error updating user to Moesif, with status code:");
                            Console.WriteLine(inst.ResponseCode);
                        }
                    }
                }
                else {
                    Console.WriteLine("To update an user, an user_id field is required");
                }
            }
        }

        // Function to update users in batch
        public async void UpdateUsersBatch(List<Dictionary<string, object>> userProfiles)
        {
            if (!userProfiles.Any())
            {
                Console.WriteLine("Expecting the input to be of the type - List of dictionary while updating users in batch");
            }
            else
            {
                List<UserModel> userModels = new List<UserModel>();
                foreach (Dictionary<string, object> user in userProfiles)
                {
                    if (user.ContainsKey("user_id"))
                    {
                        UserModel userModel = createUserModel(user);
                        userModels.Add(userModel);
                    }
                    else
                    {
                        Console.WriteLine("To update an user, an user_id field is required");
                    }
                }

                // Perform API call
                try
                {
                    await client.Api.UpdateUsersBatchAsync(userModels);
                    if (debug)
                    {
                        Console.WriteLine("Users Updated Successfully");
                    }
                }
                catch (APIException inst)
                {
                    if (401 <= inst.ResponseCode && inst.ResponseCode <= 403)
                    {
                        Console.WriteLine("Unauthorized access updating users to Moesif. Please check your Appplication Id.");
                    }
                    if (debug)
                    {
                        Console.WriteLine("Error updating users to Moesif, with status code:");
                        Console.WriteLine(inst.ResponseCode);
                    }
                }
            }
        }

        public async Task Invoke(Microsoft.AspNetCore.Http.HttpContext httpContext)
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
                    userId = IdentifyUser(httpContext.Request, httpContext.Response);
                }

                // Session Token
                sessionToken = null;
                if (GetSessionToken != null)
                {
                    sessionToken = GetSessionToken(httpContext.Request, httpContext.Response);
                }

                // Metadata
                metadata = null;
                if (GetMetadata != null)
                {
                    metadata = GetMetadata(httpContext.Request, httpContext.Response);
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

            request.EnableRewind();

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

            if (!GetCaptureTransactionId) {
                if (reqHeaders.ContainsKey("X-Moesif-Transaction-Id"))
                {
                    string reqTransId = reqHeaders["X-Moesif-Transaction-Id"];
                    if (!string.IsNullOrEmpty(reqTransId))
                    {
                        transactionId = reqTransId; 
                        if (string.IsNullOrEmpty(transactionId)) {
                            transactionId = Guid.NewGuid().ToString();
                        }
                    }
                    else {
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
            try
            {
                reqBody = ApiHelper.JsonDeserialize<object>(bodyAsText);
            }
            catch (Exception inst)
            {
                Console.WriteLine("error encountered while trying to serialize request body");
                Console.WriteLine(inst);
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
                else {
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
                Body = reqBody
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
            try
            {
                rspBody = ApiHelper.JsonDeserialize<object>(text);
            }
            catch (Exception inst)
            {
                Console.WriteLine("error encountered while trying to serialize response body");
                Console.WriteLine(inst);
            }

            var eventRsp = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = response.StatusCode,
                Headers = rspHeaders,
                Body = rspBody
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
                SessionToken = sessionToken,
                Metadata = metadata
            };

            string applicationId = null;
            var applicationId_out = new object();
            var getApplicationId = moesifOptions.TryGetValue("ApplicationId", out applicationId_out);
            if (getApplicationId)
            {
                applicationId = applicationId_out.ToString();
            }

            if (applicationId_out == null || applicationId == null)
            {
                Console.WriteLine("Please provide the application Id to send events to Moesif");
            }
            else
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
                        Console.WriteLine("Can not execute MASK_EVENT_MODEL function. Please check moesif settings.");
                    }
                }

                // Send Events
                try
                {
                    double samplingPercentage = Convert.ToDouble(confDict["sample_rate"]);

                    Random random = new Random();
                    double randomPercentage = random.NextDouble() * 100;
                    if (samplingPercentage >= randomPercentage) {
                        var createEventResponse = await client.Api.CreateEventAsync(eventModel);
                        var eventResponseConfigETag = createEventResponse["X-Moesif-Config-ETag"];
                        cachedConfigETag = confDict["ETag"].ToString();

                        if (!(string.IsNullOrEmpty(eventResponseConfigETag)) && 
                            cachedConfigETag != eventResponseConfigETag &&
                            DateTime.UtcNow > ((DateTime)confDict["last_updated_time"]).AddMinutes(5)) {
                            confDict = await GetConfig(client, confDict, eventResponseConfigETag);
                        }
                        if (debug)
                        {
                            Console.WriteLine("Event sent successfully to Moesif");
                        }    
                    }
                    else {
                        if (debug) {
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
}
