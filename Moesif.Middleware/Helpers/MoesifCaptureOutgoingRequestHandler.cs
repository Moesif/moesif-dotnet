using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Moesif.Api;
using Moesif.Api.Exceptions;
using Moesif.Api.Models;
using Moesif.Middleware.Models;
using System.Linq;
using System.ComponentModel.Design;
using Microsoft.Extensions.Logging;


namespace Moesif.Middleware.Helpers
{
    public class MoesifCaptureOutgoingRequestHandler: DelegatingHandler
    {
        public MoesifApiClient client;

        public Dictionary<string, object> metadataOutgoing;

        public string sessionTokenOutgoing;

        public string userIdOutgoing;

        public string companyIdOutgoing;

        public Dictionary<string, object> moesifConfigOptions = new Dictionary<string, object>();

        public bool debug;

        public bool logBodyOutgoing;

        public AppConfig config;

        public Governance governance;

        private ILogger _logger;
       
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public bool Debug()
        {
            bool localDebug;
            var debug_out = new object();
            var getDebug = moesifConfigOptions.TryGetValue("LocalDebug", out debug_out);
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

        public bool LogBodyOutgoing()
        {
            bool localLogBody;
            var log_body_out = new object();
            var getLogBody = moesifConfigOptions.TryGetValue("LogBodyOutgoing", out log_body_out);
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

        public MoesifCaptureOutgoingRequestHandler(HttpMessageHandler innerHandler, Dictionary<string, object> moesifOptions, ILoggerFactory logger) : base(innerHandler)
        {
            moesifConfigOptions = moesifOptions;
            _logger = logger.CreateLogger("Moesif.Middleware.Helpers.MoesifCaptureOutgoingRequestHandler");


            try {
                client = new MoesifApiClient(moesifConfigOptions["ApplicationId"].ToString());
                debug = Debug(); 
                logBodyOutgoing = LogBodyOutgoing();
                // Create a new instance of AppConfig
                config = AppConfig.getDefaultAppConfig();
                governance = Governance.getDefaultGovernance();

                // Create a new thread to get the application config
                new Thread(async () =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    try
                    {
                        // Get Application config
                        config = await AppConfigHelper.updateConfig(client, config, debug, _logger);
                        governance = await GovernanceHelper.updateGovernance(client, governance, debug, _logger);
                  
                    }
                    catch (Exception)
                    {
                        if (debug)
                        {
                            Console.WriteLine("Error while parsing application configuration on initialization");
                        }
                    }
                }).Start();

            }
            catch (Exception)
            {
                throw new Exception("Please provide the application Id to send events to Moesif");
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Request Time
            DateTime reqTime = DateTime.UtcNow;

            // Get Response
            var response = await base.SendAsync(request, cancellationToken);

            // Response Time
            DateTime respTime = DateTime.UtcNow;

            // Get Skip
            var skip_out = new object();
            var getSkip = moesifConfigOptions.TryGetValue("SkipOutgoing", out skip_out);

            // Check to see if we need to send event to Moesif
            Func<HttpRequestMessage, HttpResponseMessage, bool> ShouldSkip = null;

            if (getSkip)
            {
                ShouldSkip = (Func<HttpRequestMessage, HttpResponseMessage, bool>)(skip_out);
            }

            if (ShouldSkip != null)
            {
                if (ShouldSkip(request, response))
                {
                    if (debug)
                    {
                        Console.WriteLine("Skipping the event");
                    }
                    return response;
                }
                else
                {
                    if (debug)
                    {
                        Console.WriteLine("Sending Event to Moesif");
                    }
                    return await SendEvent(request, reqTime, response, respTime);
                }
            }
            else
            {
                if (debug)
                {
                    Console.WriteLine("Sending Event to Moesif");
                }
                return await SendEvent(request, reqTime, response, respTime);

            }
        }

        public async Task<HttpResponseMessage> SendEvent(HttpRequestMessage request, DateTime reqTime, HttpResponseMessage response, DateTime respTime) {
            if (debug)
            {
                Console.WriteLine("Calling the API to send the event to Moesif");
            }

            // Request Body Encoding
            var reqBody = new object();
            reqBody = null;
            string requestTransferEncoding;
            requestTransferEncoding = null;
            if (logBodyOutgoing && request.Content != null) {
                try
                {
                    if (debug)
                    {
                        Console.WriteLine("About to parse Request body as json");
                    }
                    // Get Request Body
                    string requestBody = await request.Content.ReadAsStringAsync();
                    reqBody = ApiHelper.JsonDeserialize<object>(requestBody);
                    requestTransferEncoding = "json";
                }
                catch (Exception)
                {
                    if (debug)
                    {
                        Console.WriteLine("About to parse Request body as Base64 encoding");
                    }
                    // Get Request Body
                    string requestBody = await request.Content.ReadAsStringAsync();
                    reqBody = Base64Encode(requestBody);
                    requestTransferEncoding = "base64";
                }    
            }

            // Prepare Moesif EventRequest Model
            Dictionary<string, string> reqHeaders = request.Headers.ToDictionary(a => a.Key, a => string.Join(";", a.Value));

            var eventReq = new EventRequestModel()
            {
                Time = reqTime,
                Uri = request.RequestUri.AbsoluteUri,
                Verb = request.Method.ToString(),
                ApiVersion = null,
                IpAddress = null,
                Headers = reqHeaders,
                Body = reqBody,
                TransferEncoding = requestTransferEncoding
            };

            // Response Body Encoding
            var rspBody = new object();
            rspBody = null;
            string responseTransferEncoding;
            responseTransferEncoding = null;
            if (logBodyOutgoing && response.Content != null) {
                try
                {
                    if (debug) {
                        Console.WriteLine("About to parse Response body as json");
                    }
                    // Get Response body
                    string responseBody = await response.Content.ReadAsStringAsync();
                    rspBody = ApiHelper.JsonDeserialize<object>(responseBody);
                    responseTransferEncoding = "json";
                }
                catch (Exception)
                {
                    if (debug)
                    {
                        Console.WriteLine("About to parse Response body as Base64 encoding");
                    }
                    // Get Response body
                    string responseBody = await response.Content.ReadAsStringAsync();
                    rspBody = Base64Encode(responseBody);
                    responseTransferEncoding = "base64";
                }    
            }

            // Response header
            Dictionary<string, string> respHeaders = response.Headers.ToDictionary(a => a.Key, a => string.Join(";", a.Value));

            // Prepare Moesif EventResponse Model
            var eventRsp = new EventResponseModel()
            {
                Time = respTime,
                Status = (int)response.StatusCode,
                Headers = respHeaders,
                Body = rspBody,
                TransferEncoding = responseTransferEncoding 
            };

            // Get Outgoing Metadata
            var metadata_out = new object();
            var getMetadata = moesifConfigOptions.TryGetValue("GetMetadataOutgoing", out metadata_out);

            Func<HttpRequestMessage, HttpResponseMessage, Dictionary<string, object>> GetMetadata = null;

            if (getMetadata)
            {
                GetMetadata = (Func<HttpRequestMessage, HttpResponseMessage, Dictionary<string, object>>)(metadata_out);
            }

            // Metadata
            metadataOutgoing = null;
            if (GetMetadata != null)
            {
                try 
                {
                    metadataOutgoing = GetMetadata(request, response);
                }
                catch
                {
                    Console.WriteLine("Can not execute GetMetadataOutgoing function. Please check moesif settings.");
                }
            }

            // Get Outgoing SessionToken
            var token_out = new object();
            var getSessionToken = moesifConfigOptions.TryGetValue("GetSessionTokenOutgoing", out token_out);

            Func<HttpRequestMessage, HttpResponseMessage, string> GetSessionToken = null;
            if (getSessionToken)
            {
                GetSessionToken = (Func<HttpRequestMessage, HttpResponseMessage, string>)(token_out);
            }

            // Session Token
            sessionTokenOutgoing = null;
            if (GetSessionToken != null)
            {
                try 
                {
                    sessionTokenOutgoing = GetSessionToken(request, response);   
                }
                catch {
                    Console.WriteLine("Can not execute GetSessionTokenOutgoing function. Please check moesif settings.");   
                }
            }

            // Get UserId outgoing
            var user_out = new object();
            var getUserId = moesifConfigOptions.TryGetValue("IdentifyUserOutgoing", out user_out);

            Func<HttpRequestMessage, HttpResponseMessage, string> IdentifyUser = null;
            if (getUserId)
            {
                IdentifyUser = (Func<HttpRequestMessage, HttpResponseMessage, string>)(user_out);
            }

            // UserId
            userIdOutgoing = null;
            if (IdentifyUser != null)
            {
                try 
                {
                    userIdOutgoing = IdentifyUser(request, response);    
                } 
                catch {
                    Console.WriteLine("Can not execute IdentifyUserOutgoing function. Please check moesif settings.");
                }
            }

            // Get CompanyId outgoing
            var company_out = new object();
            var getCompanyId = moesifConfigOptions.TryGetValue("IdentifyCompanyOutgoing", out company_out);

            Func<HttpRequestMessage, HttpResponseMessage, string> IdentifyCompany = null;
            if (getCompanyId)
            {
                IdentifyCompany = (Func<HttpRequestMessage, HttpResponseMessage, string>)(company_out);
            }

            // CompanyId
            companyIdOutgoing = null;
            if (IdentifyCompany != null)
            {
                try
                {
                    companyIdOutgoing = IdentifyCompany(request, response);
                }
                catch
                {
                    Console.WriteLine("Can not execute IdentifyCompanyOutgoing function. Please check moesif settings.");
                }
            }

            // Prepare Moesif Event Model
            var eventModel = new EventModel()
            {
                Request = eventReq,
                Response = eventRsp,
                UserId = userIdOutgoing,
                CompanyId = companyIdOutgoing,
                SessionToken = sessionTokenOutgoing,
                Metadata = metadataOutgoing,
                Direction = "Outgoing"
            };

            // Mask Outgoing Event Model
            var maskEvent_out = new object();
            var getMaskEvent = moesifConfigOptions.TryGetValue("MaskEventModelOutgoing", out maskEvent_out);

            // Check to see if we need to send event to Moesif
            Func<EventModel, EventModel> MaskEventOutgoing = null;

            if (getMaskEvent)
            {
                MaskEventOutgoing = (Func<EventModel, EventModel>)(maskEvent_out);
            }

            // Mask event
            if (MaskEventOutgoing != null)
            {
                try
                {
                    eventModel = MaskEventOutgoing(eventModel);
                }
                catch
                {
                    Console.WriteLine("Can not execute MaskEventModelOutgoing function. Please check moesif settings.");
                }
            }

            // Send Event
            try
            {
                RequestMap requestMap = RequestMapHelper.createRequestMap(eventModel);

                // Get Sampling percentage
                var samplingPercentage = AppConfigHelper.getSamplingPercentage(config, requestMap);

                Random random = new Random();
                double randomPercentage = random.NextDouble() * 100;
                if (samplingPercentage >= randomPercentage)
                {
                    eventModel.Weight = AppConfigHelper.calculateWeight(samplingPercentage);

                    var createEventResponse = await client.Api.CreateEventAsync(eventModel);
                    var eventResponseConfigETag = createEventResponse.ToDictionary(k => k.Key.ToLower(), k => k.Value)["x-moesif-config-etag"];

                    if (!(string.IsNullOrEmpty(eventResponseConfigETag)) &&
                        config.etag != eventResponseConfigETag &&
                        DateTime.UtcNow > config.lastUpdatedTime.AddMinutes(5))
                    {
                        try
                        {
                            // Get Application config
                            AppConfigHelper.updateConfig(client, config, debug, _logger);
                      
                        }
                        catch (Exception)
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

            // Return Response back to the client
            return response;
        }
    }
}
