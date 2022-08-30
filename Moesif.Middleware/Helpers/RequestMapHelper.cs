using Moesif.Api.Models;
using Moesif.Middleware.Models;
using System.Text.RegularExpressions;
namespace Moesif.Middleware.Helpers
{
    public class RequestMapHelper
    {
        static string Base64Decode(string text)
        {
            return System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(text));
        }

        public static RequestMap createRequestMap(EventModel model) 
        {
            RequestMap requestMap = new RequestMap()
            {
                companyId = model.CompanyId,
                userId = model.UserId,
                regex_mapping = new System.Collections.Generic.Dictionary<string, string>()
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
                Match m = Regex.Match(model.Request.Uri, "http[s]?://[^/]+(/[^?]+)", RegexOptions.IgnoreCase);
                var route = "/";
                if (m.Groups.Count == 2) route = m.Groups[1].Value;
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
            }

            return requestMap;
        }
    }
}

