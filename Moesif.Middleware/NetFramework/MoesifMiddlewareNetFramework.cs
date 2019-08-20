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
        public string userId;

        public string companyId;

        public string sessionToken;

        public Dictionary<string, object> metadata;

        public Dictionary<string, object> moesifOptions;

        public MoesifApiClient client;

        // Initialize config dictionary
        public Dictionary<string, object> confDict = new Dictionary<string, object>();

        // Initialize cached_config_etag
        public string cachedConfigETag = null;

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

        // Get application configuration
        public async Task<Dictionary<string, object>> GetConfig(MoesifApiClient client, Dictionary<string, object> appConfigDict, string cachedETag)
        {
            // Remove cached ETag if exist
            if (!(string.IsNullOrEmpty(cachedETag)) && confDict.ContainsKey(cachedETag))
            {
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
            else
            {
                var rspBody = ApiHelper.JsonDeserialize<Dictionary<string, object>>(appConfig.Body);
                appConfigDict["ETag"] = appConfig.Headers["X-Moesif-Config-ETag"];
                appConfigDict["sample_rate"] = rspBody["sample_rate"];
                appConfigDict["last_updated_time"] = DateTime.UtcNow;
            }

            // Return config dictionary
            return appConfigDict;
        }

        public MoesifMiddlewareNetFramework(OwinMiddleware next, Dictionary<string, object> _middleware) : base(next)
        {
            moesifOptions = _middleware;

            try
            {
                // Initialize client
                client = new MoesifApiClient(moesifOptions["ApplicationId"].ToString());
                debug = Debug();
                logBody = LogBody();
                // Initialize Transaction Id
                transactionId = null;
                // Create a new thread to get the application config
                new Thread(async () =>
                {
                    confDict = await GetConfig(client, confDict, cachedConfigETag);
                }).Start();

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

        public async override Task Invoke(IOwinContext httpContext)
        {
            // Buffering mvc reponse
            HttpResponse httpResponse = HttpContext.Current.Response;
            StreamHelper outputCapture = new StreamHelper(httpResponse.Filter);
            httpResponse.Filter = outputCapture;

            // Prepare Moeif Event Request Model
            var request = await FormatRequest(httpContext.Request);

            // Original ResponseBody stream
            Stream originalBodyStream = httpContext.Response.Body;

            // Add Transaction Id to the Response Header
            if (!string.IsNullOrEmpty(transactionId))
            {
                httpContext.Response.Headers.Append("X-Moesif-Transaction-Id", transactionId);
            }

            using (var responseBody = new MemoryStream())
            {
                httpContext.Response.Body = responseBody;

                await Next.Invoke(httpContext);

                if (outputCapture.CapturedData.Length == 0)
                {
                    httpContext.Response.Body.Position = 0;
                    await httpContext.Response.Body.CopyToAsync(responseBody);
                }
                else
                {
                    // in case we have captured data from  mvc response copy it into owinResponse
                    outputCapture.CapturedData.Position = 0;
                    outputCapture.CapturedData.CopyTo(httpContext.Response.Body);
                }

                // Prepare Moesif Event Response model
                var response = await FormatResponse(httpContext.Response);

                // User Id 
                var user_out = new object();
                var getUserId = moesifOptions.TryGetValue("IdentifyUser", out user_out);

                Func<IOwinRequest, IOwinResponse, string> IdentifyUser = null;
                if (getUserId)
                {
                    IdentifyUser = (Func<IOwinRequest, IOwinResponse, string>)(user_out);
                }

                // SessionToken
                var token_out = new object();
                var getSessionToken = moesifOptions.TryGetValue("GetSessionToken", out token_out);

                Func<IOwinRequest, IOwinResponse, string> GetSessionToken = null;
                if (getSessionToken)
                {
                    GetSessionToken = (Func<IOwinRequest, IOwinResponse, string>)(token_out);
                }

                // Get Metadata
                var metadata_out = new object();
                var getMetadata = moesifOptions.TryGetValue("GetMetadata", out metadata_out);

                Func<IOwinRequest, IOwinResponse, Dictionary<string, object>> GetMetadata = null;

                if (getMetadata)
                {
                    GetMetadata = (Func<IOwinRequest, IOwinResponse, Dictionary<string, object>>)(metadata_out);
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

                Func<IOwinRequest, IOwinResponse, string> IdentifyCompany = null;
                if (getCompanyId)
                {
                    IdentifyCompany = (Func<IOwinRequest, IOwinResponse, string>)(company_out);
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
                Func<IOwinRequest, IOwinResponse, bool> ShouldSkip = null;

                if (getSkip)
                {
                    ShouldSkip = (Func<IOwinRequest, IOwinResponse, bool>)(skip_out);
                }

                if (ShouldSkip != null)
                {
                    if (ShouldSkip(httpContext.Request, httpContext.Response))
                    {
                        if (debug)
                        {
                            Console.WriteLine("Skipping the event");
                        }

                        if (responseBody.Length > 0)
                        {
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

                        if (responseBody.Length > 0)
                        {
                            await responseBody.CopyToAsync(originalBodyStream);
                        }

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

                    if (responseBody.Length > 0)
                    {
                        await responseBody.CopyToAsync(originalBodyStream);
                    }
                }
            }
        }

        private async Task<EventRequestModel> FormatRequest(IOwinRequest request)
        {
            var body = request.Body;

            string requestBody = await new StreamReader(request.Body).ReadToEndAsync();

            // Read request Body
            byte[] requestData = Encoding.UTF8.GetBytes(requestBody);
            request.Body = new MemoryStream(requestData);

            var reqHeaders = new Dictionary<string, string>();
            try
            {
                reqHeaders = request.Headers.ToDictionary(k => k.Key, k => k.Value.First(), StringComparer.OrdinalIgnoreCase);
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
                    reqBody = ApiHelper.JsonDeserialize<object>(requestBody);
                }
                catch (Exception inst)
                {
                    if (debug)
                    {
                        Console.WriteLine("About to parse Request body as Base64 encoding");
                    }
                    // Get Request Body
                    reqBody = Base64Encode(requestBody);
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
                    ip = request.RemoteIpAddress.ToString();
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
                Body = reqBody,
                TransferEncoding = requestTransferEncoding
            };

            return eventReq;
        }

        private async Task<EventResponseModel> FormatResponse(IOwinResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);

            // read the response body stream
            var responseBody = new StreamReader(response.Body).ReadToEnd();

            var rspHeaders = new Dictionary<string, string>();
            try
            {
                rspHeaders = response.Headers.ToDictionary(k => k.Key, k => k.Value.First());
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
                    rspBody = ApiHelper.JsonDeserialize<object>(responseBody);
                }
                catch (Exception inst)
                {
                    if (debug)
                    {
                        Console.WriteLine("About to parse Response body as Base64 encoding");
                    }
                    // Get Response body
                    rspBody = Base64Encode(responseBody);
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
                Metadata = metadata
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
                double samplingPercentage = Convert.ToDouble(confDict["sample_rate"]);

                Random random = new Random();
                double randomPercentage = random.NextDouble() * 100;
                if (samplingPercentage >= randomPercentage)
                {
                    var createEventResponse = await client.Api.CreateEventAsync(eventModel);
                    var eventResponseConfigETag = createEventResponse["X-Moesif-Config-ETag"];
                    cachedConfigETag = confDict["ETag"].ToString();

                    if (!(string.IsNullOrEmpty(eventResponseConfigETag)) &&
                        cachedConfigETag != eventResponseConfigETag &&
                        DateTime.UtcNow > ((DateTime)confDict["last_updated_time"]).AddMinutes(5))
                    {
                        confDict = await GetConfig(client, confDict, eventResponseConfigETag);
                    }
                    if (debug)
                    {
                        Console.WriteLine("Event sent successfully to Moesif");
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