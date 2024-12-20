﻿using Moesif.Api.Models;
using Moesif.Middleware.Models;
using System;
using System.Text.RegularExpressions;

#if NET6_0_OR_GREATER
    using System.Text.Json;
    using System.Text.Json.Nodes;
#else
    using Newtonsoft.Json.Linq;
#endif

namespace Moesif.Middleware.Helpers
{
    public class RequestMapHelper
    {
        static string Base64Decode(string text)
        {
            return System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(text));
        }

#if NET6_0_OR_GREATER
        /**
         * Function to convert object to JsonObject. Returns null if not possible.
         */
        public static JsonObject GetJsonObject(object obj)
        {
            JsonObject objJson = null;
            
            // Serialize the object to string
            var objStr = JsonSerializer.Serialize(obj);
            
            // Parse the JSON string to JsonNode
            JsonNode objNode = JsonNode.Parse(objStr);
            if (objNode != null)
            {
                // Get JsonObject
                objJson = objNode.AsObject();   
            }

            return objJson;
        }
#endif

        public static RequestMap createRequestMap(EventModel model) 
        {
            RequestMap requestMap = new RequestMap()
            {
                companyId = model.CompanyId,
                userId = model.UserId,
                regex_mapping = new System.Collections.Generic.Dictionary<string, object>()
            };
            if (model.Request.Verb != null)
            {
                requestMap.regex_mapping.Add("request.verb", model.Request.Verb);
            }
            if (model.Request.IpAddress != null)
            {
                requestMap.regex_mapping.Add("request.ip_address", model.Request.IpAddress);
            }
            if (model.Request.Uri != null)
            {
                var route = "/";
                try
                {
                    route = new System.Uri(model.Request.Uri).LocalPath;
                }
                catch (Exception) { }
                requestMap.regex_mapping.Add("request.route", route);
            }
            if (model.Response != null)
                requestMap.regex_mapping.Add("response.status", model.Response.Status.ToString());

            if (model.Request.Headers.ContainsKey("content-type") && model.Request.Headers["content-type"] == "application/graphql" && model.Request.TransferEncoding == "base64")
            {
                var body = Base64Decode((string)model.Request.Body);
                requestMap.regex_mapping.Add("request.body.query", body);
            }

            if (model.Request.TransferEncoding == "json")
            {
                // Newtonsoft.Json.Linq.JObject body = (Newtonsoft.Json.Linq.JObject)model.Request.Body;
#if NET6_0_OR_GREATER
                JsonObject body = GetJsonObject(model.Request.Body);
                if (body != null)
                {
                    if (body.ContainsKey("operationName"))
                    {
                        var operationName = body["operationName"];
                        if (operationName != null)
                        {
                            requestMap.regex_mapping.Add("request.body.operationName", operationName.ToString());
                        }
                    }

                    if (body.ContainsKey("query"))
                    {
                        var query = body["query"];
                        if (query != null)
                        {
                            requestMap.regex_mapping.Add("request.body.query", query.ToString());
                        }

                        requestMap.regex_mapping.Add("request.body", body);
                    }
                }
#else
                Newtonsoft.Json.Linq.JObject body = (Newtonsoft.Json.Linq.JObject)model.Request.Body;
                var operationName = body.GetValue("operationName");
                if (operationName != null)
                {
                    requestMap.regex_mapping.Add("request.body.operationName", operationName.ToString());
                }
                var query = body.GetValue("query");
                if (query != null)
                {
                    requestMap.regex_mapping.Add("request.body.query", query.ToString());
                }
                requestMap.regex_mapping.Add("request.body", body);
#endif
            }

            return requestMap;
        }
    }
}

