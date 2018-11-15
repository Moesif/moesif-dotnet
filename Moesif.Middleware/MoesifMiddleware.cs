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

namespace Moesif.Middleware
{
    public class MoesifMiddleware
    {
        private readonly RequestDelegate _next;

        public string userId;

        public string sessionToken;

        public Dictionary<string, object> metadata;

        public Dictionary<string, object> moesifOptions;

        public bool debug;

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

        public MoesifMiddleware(RequestDelegate next, Dictionary<string, object> _middleware)
        {
            moesifOptions = _middleware;
            debug = Debug();
            _next = next;
        }

        public async Task Invoke(Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            var request = await FormatRequest(httpContext.Request);

            var originalBodyStream = httpContext.Response.Body;

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
                reqHeaders = request.Headers.ToDictionary(k => k.Key, k => k.Value.ToString());
            }
            catch (Exception inst)
            {
                if (debug)
                {
                    Console.WriteLine("error encountered while copying request header");
                    Console.WriteLine(inst);
                }
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
                ip = request.HttpContext.Connection.RemoteIpAddress.ToString();
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
                    var client = new MoesifApiClient(applicationId);

                    await client.Api.CreateEventAsync(eventModel);
                    if (debug)
                    {
                        Console.WriteLine("Event sent successfully to Moesif");
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
