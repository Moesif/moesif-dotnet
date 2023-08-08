using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using Moesif.Api.Models;
using System.Net.Http;
using Microsoft.Owin;
using System.Web;
using Moesif.Middleware;
using System.Threading.Tasks;
using Moq;
using Microsoft.Extensions.Logging;
using Moesif.Middleware.Helpers;
using NUnit.Framework;
using Moesif.Api;
using Moesif.Middleware.Models;

namespace Moesif.NetFramework.Test
{
    [TestFixture()]
    public class MoesifNetFrameworkTest
    {
        public Dictionary<string, object> moesifOptions = new Dictionary<string, object>();

        public MoesifMiddleware moesifMiddleware;

        public MoesifMiddleware moesifMiddleware_standalone;

        public OwinMiddleware next;

        public static Func<HttpRequestMessage, HttpResponseMessage, Dictionary<string, object>> GetMetadataOutgoing = (HttpRequestMessage req, HttpResponseMessage res) => {
            Dictionary<string, object> metadata = new Dictionary<string, object>
            {
                {"email", "johndoe@acmeinc.com"},
                {"string_field", "value_1"},
                {"number_field", 0},
                {"object_field", new Dictionary<string, string> {
                    {"field_a", "value_a"},
                    {"field_b", "value_b"}
                    }
                }
            };
            return metadata;
        };

        public static Func<IOwinRequest, IOwinResponse, string> IdentifyUser = (IOwinRequest req, IOwinResponse res) => {
            return "my_user_id";
        };

        public static Func<IOwinRequest, IOwinResponse, string> IdentifyCompany = (IOwinRequest req, IOwinResponse res) => {
            return "my_company_id";
        };

        public static Func<IOwinRequest, IOwinResponse, Dictionary<string, object>> GetMetadata = (IOwinRequest req, IOwinResponse res) => {
            Dictionary<string, object> metadata = new Dictionary<string, object>
            {
                {"email", "johndoe@acmeinc.com"},
                {"string_field", "value_1"},
                {"number_field", 0},
                {"object_field", new Dictionary<string, string> {
                    {"field_a", "value_a"},
                    {"field_b", "value_b"}
                    }
                }
            };
            return metadata;
        };

        public static Func<HttpRequestMessage, HttpResponseMessage, string> IdentifyUserOutgoing = (HttpRequestMessage req, HttpResponseMessage res) => {
            return "my_user_id";
        };

        public static Func<HttpRequestMessage, HttpResponseMessage, string> IdentifyCompanyOutgoing = (HttpRequestMessage req, HttpResponseMessage res) => {
            return "my_company_id";
        };

        public static Func<HttpRequestMessage, HttpResponseMessage, string> GetSessionTokenOutgoing = (HttpRequestMessage req, HttpResponseMessage res) => {
            return "23jdf0owekfmcn4u3qypxg09w4d8ayrcdx8nu2ng]s98y18cx98q3yhwmnhcfx43f";
        };

        public static Func<HttpRequestMessage, HttpResponseMessage, bool> SkipOutgoing = (HttpRequestMessage req, HttpResponseMessage res) => {
            return false;
        };

        public static Func<EventModel, EventModel> MaskEventModelOutgoing = (EventModel event_model) => {
            event_model.UserId = "masked_user_id";
            return event_model;
        };

        public MoesifNetFrameworkTest()
        {

            moesifOptions.Add("ApplicationId", "Your Moesif Application Id");
            moesifOptions.Add("LocalDebug", true);
            moesifOptions.Add("LogBody", true);
            moesifOptions.Add("LogBodyOutgoing", true);
            moesifOptions.Add("ApiVersion", "1.0.0");
            moesifOptions.Add("IdentifyUser", IdentifyUser);
            moesifOptions.Add("IdentifyCompany", IdentifyCompany);
            moesifOptions.Add("GetMetadata", GetMetadata);
            moesifOptions.Add("GetMetadataOutgoing", GetMetadataOutgoing);
            moesifOptions.Add("GetSessionTokenOutgoing", GetSessionTokenOutgoing);
            moesifOptions.Add("IdentifyUserOutgoing", IdentifyUserOutgoing);
            moesifOptions.Add("IdentifyCompanyOutgoing", IdentifyCompanyOutgoing);
            moesifOptions.Add("SkipOutgoing", SkipOutgoing);
            moesifOptions.Add("MaskEventModelOutgoing", MaskEventModelOutgoing);

            moesifMiddleware = new MoesifMiddleware(next, moesifOptions);
            moesifMiddleware_standalone = new MoesifMiddleware(moesifOptions);

        }

        [Test()]
        public async Task It_Should_Log_Outgoing_Event()
        {
            MoesifCaptureOutgoingRequestHandler handler = new MoesifCaptureOutgoingRequestHandler(new HttpClientHandler(), moesifOptions);
            HttpClient client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "C# App");
            var responseString = await client.GetStringAsync("https://api.github.com");
            Console.WriteLine(responseString);
        }

        [Test()]
        public void It_Should_Update_User()
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>
            {
                {"email", "johndoe@acmeinc.com"},
                {"string_field", "value_1"},
                {"number_field", 0},
                {"object_field", new Dictionary<string, string> {
                    {"field_a", "value_a"},
                    {"field_b", "value_b"}
                    }
                }
            };

            Dictionary<string, string> campaign = new Dictionary<string, string>
            {
                {"utm_source", "NewsletterNet" },
                {"utm_medium", "EmailNet" }
            };

            Dictionary<string, object> user = new Dictionary<string, object>
            {
                {"user_id", "12345"},
                {"company_id", "67890"},
                {"metadata", metadata},
                {"campaign", campaign},
            };

            moesifMiddleware_standalone.UpdateUser(user);
        }

        [Test()]
        public void It_Should_Update_Users_Batch()
        {
            List<Dictionary<string, object>> usersBatch = new List<Dictionary<string, object>>();
            Dictionary<string, object> metadata = new Dictionary<string, object>
            {
                {"email", "johndoe@acmeinc.com"},
                {"string_field", "value_1"},
                {"number_field", 0},
                {"object_field", new Dictionary<string, string> {
                    {"field_a", "value_a"},
                    {"field_b", "value_b"}
                    }
                }
            };

            Dictionary<string, object> userA = new Dictionary<string, object>
            {
                {"user_id", "12345"},
                {"company_id", "67890"},
                {"metadata", metadata},
            };

            Dictionary<string, object> userB = new Dictionary<string, object>
            {
                {"user_id", "1234"},
                {"company_id", "6789"},
                {"modified_time", DateTime.UtcNow},
                {"metadata", metadata},
            };

            usersBatch.Add(userA);
            usersBatch.Add(userB);

            moesifMiddleware_standalone.UpdateUsersBatch(usersBatch);
        }

        [Test()]
        public void It_Should_Update_Company()
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>
            {
                {"email", "johndoe@acmeinc.com"},
                {"string_field", "value_1"},
                {"number_field", 0},
                {"object_field", new Dictionary<string, string> {
                    {"field_a", "value_a"},
                    {"field_b", "value_b"}
                    }
                }
            };

            Dictionary<string, string> campaign = new Dictionary<string, string>
            {
                {"utm_source", "AdwordsNet" },
                {"utm_medium", "TwitterNet" }
            };

            Dictionary<string, object> company = new Dictionary<string, object>
            {
                {"company_id", "12345"},
                {"company_domain", "acmeinc.com"},
                {"metadata", metadata},
                {"campaign", campaign},
            };

            moesifMiddleware_standalone.UpdateCompany(company);
        }

        [Test()]
        public void It_Should_Update_Companies_Batch()
        {
            List<Dictionary<string, object>> companiesBatch = new List<Dictionary<string, object>>();
            Dictionary<string, object> metadata = new Dictionary<string, object>
            {
                {"email", "johndoe@acmeinc.com"},
                {"string_field", "value_1"},
                {"number_field", 0},
                {"object_field", new Dictionary<string, string> {
                    {"field_a", "value_a"},
                    {"field_b", "value_b"}
                    }
                }
            };

            Dictionary<string, object> companyA = new Dictionary<string, object>
            {
                {"company_id", "12345"},
                {"company_domain", "acmeinc.com"},
                {"metadata", metadata},
            };

            Dictionary<string, object> companyB = new Dictionary<string, object>
            {
                {"company_id", "67890"},
                {"company_domain", "nowhere.com"},
                {"metadata", metadata},
            };

            companiesBatch.Add(companyA);
            companiesBatch.Add(companyB);

            moesifMiddleware_standalone.UpdateCompaniesBatch(companiesBatch);
        }

        [Test()]
        public void It_Should_Return_Sampling_rate_From_Regex()
        {
            var appConfigJson = "{'org_id':'421:67','app_id':'46:73','sample_rate':95,'block_bot_traffic':false,'user_sample_rate':{'user_1234': 80 },'company_sample_rate':{},'user_rules':{'12345':[{'rules':'62fd061e51f905712d73f72d'}]},'company_rules':{'sean-company-6':[{'rules':'62fe6f3bf199ee4cf35762d7'}],'67890':[{'rules':'62fe6f3bf199ee4cf35762d7'}],'sean-company-5':[{'rules':'62fe6f3bf199ee4cf35762d7'}]},'ip_addresses_blocked_by_name':{},'regex_config':[{'conditions':[{'path':'request.route','value':'/.*'}],'sample_rate':80}],'billing_config_jsons':{}}";
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(appConfigJson);
            var eventReq = new EventRequestModel()
            {
                Time = DateTime.UtcNow,
                Uri = "https://www.google.com/search",
                Verb = "GET",
                ApiVersion = null,
                IpAddress = null,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };

            var eventRsp = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = 200,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };

            var eventModel = new EventModel()
            {
                Request = eventReq,
                Response = eventRsp,
                UserId = "user_1234",
                CompanyId = "company_1234",
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            var requestMap = RequestMapHelper.createRequestMap(eventModel);
            int sample_rate = AppConfigHelper.getSamplingPercentage(appConfig, requestMap);
            Assert.AreEqual(sample_rate, 80);
        
        }

        [Test()]
        public void It_Should_Return_Smallest_Sampling_rate_From_multi_Regex_Match()
        {
            var appConfigJson = "{'org_id':'421:67','app_id':'46:73','sample_rate':95,'block_bot_traffic':false,'user_sample_rate':{'user_1234': 80 },'company_sample_rate':{},'user_rules':{'12345':[{'rules':'62fd061e51f905712d73f72d'}]},'company_rules':{'sean-company-6':[{'rules':'62fe6f3bf199ee4cf35762d7'}],'67890':[{'rules':'62fe6f3bf199ee4cf35762d7'}],'sean-company-5':[{'rules':'62fe6f3bf199ee4cf35762d7'}]},'ip_addresses_blocked_by_name':{},'regex_config':[{'conditions':[{'path':'request.route','value':'/.*'}],'sample_rate':80},{\"conditions\":[{\"path\":\"response.status\",\"value\":\"200\"}],\"sample_rate\":50}],'billing_config_jsons':{}}";
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(appConfigJson);
            var eventReq = new EventRequestModel()
            {
                Time = DateTime.UtcNow,
                Uri = "https://www.google.com/search",
                Verb = "GET",
                ApiVersion = null,
                IpAddress = null,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };

            var eventRsp = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = 200,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };

            var eventModel = new EventModel()
            {
                Request = eventReq,
                Response = eventRsp,
                UserId = "user_1234",
                CompanyId = "company_1234",
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            var requestMap = RequestMapHelper.createRequestMap(eventModel);
            int sample_rate = AppConfigHelper.getSamplingPercentage(appConfig, requestMap);
            Assert.AreEqual(sample_rate, 50);

        }

        [Test()]
        public void It_Should_Return_Sampling_rate_From_User()
        {
            var appConfigJson = "{'org_id':'421:67','app_id':'46:73','sample_rate':95,'block_bot_traffic':false,'user_sample_rate':{'user_1234': 70 },'company_sample_rate':{},'user_rules':{'12345':[{'rules':'62fd061e51f905712d73f72d'}]},'company_rules':{'sean-company-6':[{'rules':'62fe6f3bf199ee4cf35762d7'}],'67890':[{'rules':'62fe6f3bf199ee4cf35762d7'}],'sean-company-5':[{'rules':'62fe6f3bf199ee4cf35762d7'}]},'ip_addresses_blocked_by_name':{},'regex_config':[],'billing_config_jsons':{}}";
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(appConfigJson);
            var eventReq = new EventRequestModel()
            {
                Time = DateTime.UtcNow,
                Uri = "https://www.google.com/search",
                Verb = "GET",
                ApiVersion = null,
                IpAddress = null,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };

            var eventRsp = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = 200,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };

            var eventModel = new EventModel()
            {
                Request = eventReq,
                Response = eventRsp,
                UserId = "user_1234",
                CompanyId = "company_1234",
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            var requestMap = RequestMapHelper.createRequestMap(eventModel);
            int sample_rate = AppConfigHelper.getSamplingPercentage(appConfig, requestMap);
            Assert.AreEqual(sample_rate, 70);

        }

        [Test()]
        public void It_Should_Return_Sampling_rate_From_Company()
        {
            var appConfigJson = "{'org_id':'421:67','app_id':'46:73','sample_rate':95,'block_bot_traffic':false,'user_sample_rate':{},'company_sample_rate':{'company_1234': 60},'user_rules':{'12345':[{'rules':'62fd061e51f905712d73f72d'}]},'company_rules':{'sean-company-6':[{'rules':'62fe6f3bf199ee4cf35762d7'}],'67890':[{'rules':'62fe6f3bf199ee4cf35762d7'}],'sean-company-5':[{'rules':'62fe6f3bf199ee4cf35762d7'}]},'ip_addresses_blocked_by_name':{},'regex_config':[],'billing_config_jsons':{}}";
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(appConfigJson);
            var eventReq = new EventRequestModel()
            {
                Time = DateTime.UtcNow,
                Uri = "https://www.google.com/search",
                Verb = "GET",
                ApiVersion = null,
                IpAddress = null,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };

            var eventRsp = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = 200,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };

            var eventModel = new EventModel()
            {
                Request = eventReq,
                Response = eventRsp,
                UserId = "user_1234",
                CompanyId = "company_1234",
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            var requestMap = RequestMapHelper.createRequestMap(eventModel);
            int sample_rate = AppConfigHelper.getSamplingPercentage(appConfig, requestMap);
            Assert.AreEqual(sample_rate, 60);

        }

        [Test()]
        public void It_Should_Return_Sampling_rate_From_Global()
        {
            var appConfigJson = "{'org_id':'421:67','app_id':'46:73','sample_rate':95,'block_bot_traffic':false,'user_sample_rate':{},'company_sample_rate':{},'user_rules':{'12345':[{'rules':'62fd061e51f905712d73f72d'}]},'company_rules':{'sean-company-6':[{'rules':'62fe6f3bf199ee4cf35762d7'}],'67890':[{'rules':'62fe6f3bf199ee4cf35762d7'}],'sean-company-5':[{'rules':'62fe6f3bf199ee4cf35762d7'}]},'ip_addresses_blocked_by_name':{},'regex_config':[],'billing_config_jsons':{}}";
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(appConfigJson);
            var eventReq = new EventRequestModel()
            {
                Time = DateTime.UtcNow,
                Uri = "https://www.google.com/search",
                Verb = "GET",
                ApiVersion = null,
                IpAddress = null,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };

            var eventRsp = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = 200,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };

            var eventModel = new EventModel()
            {
                Request = eventReq,
                Response = eventRsp,
                UserId = "user_1234",
                CompanyId = "company_1234",
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            var requestMap = RequestMapHelper.createRequestMap(eventModel);
            int sample_rate = AppConfigHelper.getSamplingPercentage(appConfig, requestMap);
            Assert.AreEqual(sample_rate, 95);

        }
    }

  
}
