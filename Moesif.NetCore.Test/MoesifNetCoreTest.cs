using System;
using System.Collections.Generic;
using Moesif.Middleware;
using Moesif.Middleware.Helpers;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Net;
using Moesif.Api.Models;
using Moesif.Middleware.Models;
using Moesif.Api;
using Assert = NUnit.Framework.Assert;

namespace Moesif.NetCore.Test
{
    public class MoesifNetCoreTest{

        public Dictionary<string, object> moesifOptions = new Dictionary<string, object>();

        public MoesifMiddleware moesifMiddleware;
        public MoesifMiddleware moesifMiddleware_standalone;

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

        public static Func<HttpRequest, HttpResponse, string> IdentifyUser = (HttpRequest req, HttpResponse res) => {
            return "my_user_id";
        };

        public static Func<HttpRequest, HttpResponse, string> IdentifyCompany = (HttpRequest req, HttpResponse res) => {
            return "my_company_id";
        };

        public static Func<HttpRequest, HttpResponse, Dictionary<string, object>> GetMetadata = (HttpRequest req, HttpResponse res) => {
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

        public static Func<HttpRequestMessage, HttpResponseMessage, bool> SkipOutgoing = (HttpRequestMessage req, HttpResponseMessage res) =>
        {
            return false;
        };

        public static Func<EventModel, EventModel> MaskEventModel = (EventModel moesifEvent) =>
        {
            Dictionary<String, String> eventRequestHeaders = moesifEvent.Request.Headers;
            bool keyExists = eventRequestHeaders.ContainsKey("Authorization");
            if (keyExists)
            {
                eventRequestHeaders.Remove("Authorization");
            };

            return moesifEvent;
        };

        public MoesifNetCoreTest() {

            moesifOptions.Add("ApplicationId", "eyJhcHAiOiI1MTk6MzQ1IiwidmVyIjoiMi4wIiwib3JnIjoiNDIwOjMyMCIsImlhdCI6MTY5MDg0ODAwMH0.Deusjx69LTWGaa1dJyJFP9mfFKNwF_plUvWEJfssEMw");
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

            moesifMiddleware = new MoesifMiddleware((innerHttpContext) => Task.FromResult(0), moesifOptions);
            moesifMiddleware_standalone = new MoesifMiddleware(moesifOptions);

        }

        [Fact]
        public async Task It_Should_Log_Event()
        {
            var loggerMock = new Mock<ILogger>();
            var requestMock = new Mock<HttpRequest>();
            requestMock.Setup(x => x.Scheme).Returns("https");
            requestMock.Setup(x => x.Host).Returns(new HostString("acmeinc.com"));
            requestMock.Setup(x => x.Path).Returns(new PathString("/42752/reviews"));
            requestMock.Setup(x => x.PathBase).Returns(new PathString("/items"));
            requestMock.Setup(x => x.Method).Returns("GET");
            requestMock.Setup(x => x.Body).Returns(new MemoryStream());
            requestMock.Setup(x => x.QueryString).Returns(new QueryString("?key=value"));
            requestMock.Setup(x => x.HttpContext.Connection.RemoteIpAddress).Returns(IPAddress.Parse("219.148.232.216"));
            requestMock.Setup(x => x.Headers.Add("Content-Type", "application/json"));

            var responseMock = new Mock<HttpResponse>();
            responseMock.Setup(x => x.StatusCode).Returns(200);
            responseMock.Setup(x => x.ContentType).Returns("application/json");
            responseMock.Setup(x => x.Body).Returns(new MemoryStream(System.Text.Encoding.Unicode.GetBytes("{ \"key\": \"value\"}")));
            responseMock.Setup(x => x.Headers.Add("Content-Type", "application/json"));


            var contextMock = new Mock<HttpContext>();
            contextMock.Setup(x => x.Request).Returns(requestMock.Object);
            contextMock.Setup(x => x.Response).Returns(responseMock.Object);

            await Task.Delay(1000);
            await moesifMiddleware.Invoke(contextMock.Object);

        }

        [Fact]
        public async Task It_Should_Log_Outgoing_Event()
        {
            MoesifCaptureOutgoingRequestHandler handler = new MoesifCaptureOutgoingRequestHandler(new HttpClientHandler(), moesifOptions);
            HttpClient client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "C# App");
            var responseString = await client.GetStringAsync("https://api.github.com");
            Console.WriteLine(responseString);
        }

        [Fact]
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
                {"utm_source", "Newsletter" },
                {"utm_medium", "Email" }
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


        [Fact]
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

        [Fact]
        public void It_Should_Update_Company()
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>
            {
                {"email", "johndoe@acmeinc.com"},
                {"string_field", "value_1"},
                {"number_field", 1},
                {"object_field", new Dictionary<string, string> {
                    {"field_a", "value_a"},
                    {"field_b", "value_b"}
                    }
                }
            };

            Dictionary<string, string> campaign = new Dictionary<string, string>
            {
                {"utm_source", "Adwords" },
                {"utm_medium", "Twitter" }
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

        [Fact]
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

        [Fact]
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
        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
        public void It_Should_not_block_when_governance_rule_is_not_configured()
        {
            Assert.AreEqual(GovernanceHelper.enforceGovernaceRule(new EventModel(), Governance.getDefaultGovernance(), AppConfig.getDefaultAppConfig()), false);
        }

        [Fact]
        public void It_Should_Block_On_User_rule()
        {
            var configJson = "{\"org_id\":\"640:128\",\"app_id\":\"617:473\",\"sample_rate\":99,\"block_bot_traffic\":false,\"user_sample_rate\":{\"azure_user_id\":100,\"tyk-bearer-token\":100,\"basic-auth-test\":100,\"abc\":60,\"masked_user_id\":100,\"keyur@moesif.com\":100,\"385\":99,\"tyk-basic-auth\":100,\"outgoing_user_id\":90,\"tyk-user\":100,\"keyur\":100,\"nginxapiuser\":95,\"Jason\":100,\"8ce866a1-1ba1-47ec-9130-f046bd8e3df5\":99,\"1234\":0,\"tyk-jwt-token\":100,\"jwt-token\":100,\"2d45bf73-bfa2-4b0a-918a-ee4010dfb5a3\":92,\"dev_user_id\":100,\"12345\":100,\"7ab8b13c-866d-4587-b99d-a8166391171b\":94,\"OAuth\":100,\"my_user_id\":97,\"bearer-token\":100,\"deva-1\":70},\"company_sample_rate\":{\"67890\":98,\"34\":100,\"12\":100,\"8\":100,\"678\":100,\"40\":100,\"9\":100,\"26\":100,\"123\":82,\"37\":100,\"13\":100,\"46\":100,\"24\":100,\"16\":100,\"48\":100,\"43\":100,\"32\":100,\"36\":100,\"39\":100,\"47\":100,\"20\":100,\"27\":100,\"2\":100,\"azure_company_id\":100,\"18\":100,\"3\":100,\"undefined\":80},\"user_rules\":{\"masked_user_id\":[{\"rules\":\"5f4910ab5f09092bd0e4ec79\",\"values\":{\"8\":\"body.phone\",\"4\":\"San Francisco\",\"5\":\"body.title\",\"1\":\"company_id\",\"0\":\"masked_user_id\",\"2\":\"name\",\"7\":\"body.age\",\"3\":\"2021-01-08T19:05:38.482Z\"}}]},\"company_rules\":{\"tyk-company\":[{\"rules\":\"5f49118a5f09092bd0e4ec7a\",\"values\":{\"0\":\"tyk-company\",\"1\":\"2020-11-02T20:22:42.845Z\",\"2\":\"body.age\",\"3\":\"campaign.utm_term\"}}],\"azure_company_id\":[{\"rules\":\"5f49118a5f09092bd0e4ec7a\",\"values\":{\"0\":\"azure_company_id\",\"1\":\"2020-08-28T15:11:32.402Z\",\"2\":\"42\",\"3\":\"campaign.utm_term\"}}]},\"ip_addresses_blocked_by_name\":{},\"regex_config\":[],\"billing_config_jsons\":{}}";
            var governaceJson = "[{\"_id\":\"5f4910ab5f09092bd0e4ec79\",\"created_at\":\"2020-08-28T14:11:55.117\",\"org_id\":\"640:128\",\"app_id\":\"617:473\",\"name\":\"First User Rule\",\"block\":true,\"type\":\"user\",\"variables\":[{\"name\":\"0\",\"path\":\"user_id\"},{\"name\":\"1\",\"path\":\"company_id\"},{\"name\":\"2\",\"path\":\"name\"},{\"name\":\"3\",\"path\":\"created\"},{\"name\":\"4\",\"path\":\"geo_ip.city_name\"},{\"name\":\"5\",\"path\":\"body.title\"},{\"name\":\"7\",\"path\":\"body.age\"},{\"name\":\"8\",\"path\":\"body.phone\"}],\"response\":{\"status\":400,\"headers\":{\"X-Company-Id\":\"{{1}}\",\"X-City\":\"{{4}}\",\"X-Created\":\"{{3}}\",\"X-Avg\":\"{{7}} / {{8}}\",\"X-User-Id\":\"{{0}}\"},\"body\":{\"test\":{\"nested\":{\"msg\":\"At {{4}} on {{3}} we {{2}} did this - {{5}}\"}}}}},{\"_id\":\"5f49118a5f09092bd0e4ec7a\",\"created_at\":\"2020-08-28T14:15:38.611\",\"org_id\":\"640:128\",\"app_id\":\"617:473\",\"name\":\"First Company Rule\",\"block\":true,\"type\":\"company\",\"variables\":[{\"name\":\"0\",\"path\":\"company_id\"},{\"name\":\"1\",\"path\":\"created\"},{\"name\":\"2\",\"path\":\"body.age\"},{\"name\":\"3\",\"path\":\"campaign.utm_term\"}],\"response\":{\"status\":500,\"headers\":{\"X-Company-Id\":\"{{0}}\",\"X-Created\":\"{{1}}\",\"X-Age\":\"{{2}}\",\"X-Term\":\"{{3}}\"},\"body\":\"This is a string example for - {{0}}, at {{1}}\"}}]";
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(configJson);
            var governace = Governance.fromJson(governaceJson);
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
            var eventModel = new EventModel()
            {
                Request = eventReq,
                UserId = "masked_user_id",
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            Assert.AreEqual(GovernanceHelper.enforceGovernaceRule(eventModel, governace, appConfig), true);

            Assert.AreEqual(eventModel.BlockedBy, "5f4910ab5f09092bd0e4ec79");
            Assert.AreEqual(eventModel.Response.Status, 400);

        }

        [Fact]
        public void It_Should_Block_On_Company_rule()
        {
            var configJson = "{\"org_id\":\"640:128\",\"app_id\":\"617:473\",\"sample_rate\":99,\"block_bot_traffic\":false,\"user_sample_rate\":{\"azure_user_id\":100,\"tyk-bearer-token\":100,\"basic-auth-test\":100,\"abc\":60,\"masked_user_id\":100,\"keyur@moesif.com\":100,\"385\":99,\"tyk-basic-auth\":100,\"outgoing_user_id\":90,\"tyk-user\":100,\"keyur\":100,\"nginxapiuser\":95,\"Jason\":100,\"8ce866a1-1ba1-47ec-9130-f046bd8e3df5\":99,\"1234\":0,\"tyk-jwt-token\":100,\"jwt-token\":100,\"2d45bf73-bfa2-4b0a-918a-ee4010dfb5a3\":92,\"dev_user_id\":100,\"12345\":100,\"7ab8b13c-866d-4587-b99d-a8166391171b\":94,\"OAuth\":100,\"my_user_id\":97,\"bearer-token\":100,\"deva-1\":70},\"company_sample_rate\":{\"67890\":98,\"34\":100,\"12\":100,\"8\":100,\"678\":100,\"40\":100,\"9\":100,\"26\":100,\"123\":82,\"37\":100,\"13\":100,\"46\":100,\"24\":100,\"16\":100,\"48\":100,\"43\":100,\"32\":100,\"36\":100,\"39\":100,\"47\":100,\"20\":100,\"27\":100,\"2\":100,\"azure_company_id\":100,\"18\":100,\"3\":100,\"undefined\":80},\"user_rules\":{\"masked_user_id\":[{\"rules\":\"5f4910ab5f09092bd0e4ec79\",\"values\":{\"8\":\"body.phone\",\"4\":\"San Francisco\",\"5\":\"body.title\",\"1\":\"company_id\",\"0\":\"masked_user_id\",\"2\":\"name\",\"7\":\"body.age\",\"3\":\"2021-01-08T19:05:38.482Z\"}}]},\"company_rules\":{\"tyk-company\":[{\"rules\":\"5f49118a5f09092bd0e4ec7a\",\"values\":{\"0\":\"tyk-company\",\"1\":\"2020-11-02T20:22:42.845Z\",\"2\":\"body.age\",\"3\":\"campaign.utm_term\"}}],\"azure_company_id\":[{\"rules\":\"5f49118a5f09092bd0e4ec7a\",\"values\":{\"0\":\"azure_company_id\",\"1\":\"2020-08-28T15:11:32.402Z\",\"2\":\"42\",\"3\":\"campaign.utm_term\"}}]},\"ip_addresses_blocked_by_name\":{},\"regex_config\":[],\"billing_config_jsons\":{}}";
            var governaceJson = "[{\"_id\":\"5f4910ab5f09092bd0e4ec79\",\"created_at\":\"2020-08-28T14:11:55.117\",\"org_id\":\"640:128\",\"app_id\":\"617:473\",\"name\":\"First User Rule\",\"block\":true,\"type\":\"user\",\"variables\":[{\"name\":\"0\",\"path\":\"user_id\"},{\"name\":\"1\",\"path\":\"company_id\"},{\"name\":\"2\",\"path\":\"name\"},{\"name\":\"3\",\"path\":\"created\"},{\"name\":\"4\",\"path\":\"geo_ip.city_name\"},{\"name\":\"5\",\"path\":\"body.title\"},{\"name\":\"7\",\"path\":\"body.age\"},{\"name\":\"8\",\"path\":\"body.phone\"}],\"response\":{\"status\":400,\"headers\":{\"X-Company-Id\":\"{{1}}\",\"X-City\":\"{{4}}\",\"X-Created\":\"{{3}}\",\"X-Avg\":\"{{7}} / {{8}}\",\"X-User-Id\":\"{{0}}\"},\"body\":{\"test\":{\"nested\":{\"msg\":\"At {{4}} on {{3}} we {{2}} did this - {{5}}\"}}}}},{\"_id\":\"5f49118a5f09092bd0e4ec7a\",\"created_at\":\"2020-08-28T14:15:38.611\",\"org_id\":\"640:128\",\"app_id\":\"617:473\",\"name\":\"First Company Rule\",\"block\":true,\"type\":\"company\",\"variables\":[{\"name\":\"0\",\"path\":\"company_id\"},{\"name\":\"1\",\"path\":\"created\"},{\"name\":\"2\",\"path\":\"body.age\"},{\"name\":\"3\",\"path\":\"campaign.utm_term\"}],\"response\":{\"status\":500,\"headers\":{\"X-Company-Id\":\"{{0}}\",\"X-Created\":\"{{1}}\",\"X-Age\":\"{{2}}\",\"X-Term\":\"{{3}}\"},\"body\":\"This is a string example for - {{0}}, at {{1}}\"}}]";
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(configJson);
            var governace = Governance.fromJson(governaceJson);
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
            var eventModel = new EventModel()
            {
                Request = eventReq,
                CompanyId = "tyk-company",
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            Assert.AreEqual(GovernanceHelper.enforceGovernaceRule(eventModel, governace, appConfig), true);

            Assert.AreEqual(eventModel.BlockedBy, "5f49118a5f09092bd0e4ec7a");
            Assert.AreEqual(eventModel.Response.Status, 500);
        }

        [Fact]
        public void It_should_block_on_regex_rule()
        {
            var governanceJson = "[{\"_id\":\"62f69205ec701a4f0400a377\",\"created_at\":\"2022-08-12T17:46:45.670\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"my gov\",\"block\":true,\"type\":\"company\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"POST\"}]}],\"response\":{\"status\":203,\"headers\":{},\"body\":{\"ok\":1}}},{\"_id\":\"62f6c661ac3331776950eba1\",\"created_at\":\"2022-08-12T21:30:09.523\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"my regex\",\"block\":true,\"type\":\"regex\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"POST\"}]}],\"response\":{\"status\":203,\"headers\":{},\"body\":{\"ok\":1}}},{\"_id\":\"62fd061e51f905712d73f72d\",\"created_at\":\"2022-08-17T15:15:42.909\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"user\",\"block\":true,\"type\":\"user\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.body.operationName\",\"value\":\"Get.*\"}]}],\"response\":{\"status\":203,\"headers\":{},\"body\":{\"error\":\"this is a test\"}}},{\"_id\":\"62fe6f3bf199ee4cf35762d7\",\"created_at\":\"2022-08-18T16:56:27.767\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"company rule 2\",\"block\":true,\"type\":\"company\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"DELETE\"}]}],\"response\":{\"status\":401,\"headers\":{},\"body\":{\"error\":\"test\"}}},{\"_id\":\"62ffc2e77a9aca1bfdefc3e3\",\"created_at\":\"2022-08-19T17:05:43.321\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"graphql2\",\"block\":true,\"type\":\"regex\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.body.query\",\"value\":\".*Get.*\"}]}],\"response\":{\"status\":401,\"headers\":{},\"body\":{\"test\":\"graph2\"}}},{\"_id\":\"62fff3367a9aca1bfdefc3f1\",\"created_at\":\"2022-08-19T20:31:50.765\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"company rule no regex\",\"block\":true,\"type\":\"company\",\"regex_config\":[],\"response\":{\"status\":402,\"headers\":{},\"body\":{\"status\":\"make payment\"}}},{\"_id\":\"6317c7ba63501c63e3ff4ee0\",\"created_at\":\"2022-09-06T22:20:42.060\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"user rule\",\"block\":true,\"type\":\"user\",\"variables\":[{\"name\":\"0\",\"path\":\"cohort_names\"},{\"name\":\"1\",\"path\":\"created\"},{\"name\":\"2\",\"path\":\"identified_user_id\"},{\"name\":\"3\",\"path\":\"company_id\"}],\"regex_config\":[],\"response\":{\"status\":204,\"headers\":{\"my header\":\"{{0}}\",\"my header2\":\"{{1}}\",\"header3\":\"{{2}}\"},\"body\":{\"yes\":true,\"{{3}}\":\"yes\"}}}]";
            var configJson = "{\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"sample_rate\":100,\"block_bot_traffic\":false,\"user_sample_rate\":{\"sean-user-11\":70,\"sean-user-10\":70,\"sean-user-9\":70},\"company_sample_rate\":{\"sean-company-5\":50,\"67890\":50,\"sean-company-9\":50,\"sean-company-6\":50,\"sean-company-10\":50,\"sean-company-11\":50,\"company_1234\":50},\"user_rules\":{\"sean-user-11\":[{\"rules\":\"6317c7ba63501c63e3ff4ee0\",\"values\":{\"0\":\"cohort_names\",\"1\":\"created\",\"2\":\"identified_user_id\",\"3\":\"sean-company-11\"}}],\"sean-user-10\":[{\"rules\":\"6317c7ba63501c63e3ff4ee0\",\"values\":{\"0\":\"cohort_names\",\"1\":\"created\",\"2\":\"identified_user_id\",\"3\":\"sean-company-10\"}}],\"sean-user-9\":[{\"rules\":\"6317c7ba63501c63e3ff4ee0\",\"values\":{\"0\":\"cohort_names\",\"1\":\"created\",\"2\":\"identified_user_id\",\"3\":\"sean-company-9\"}}]},\"company_rules\":{\"sean-company-5\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"67890\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-9\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-6\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-10\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-11\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"company_1234\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}]},\"ip_addresses_blocked_by_name\":{},\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"post\"}],\"sample_rate\":20}],\"billing_config_jsons\":{}}";
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(configJson);
            var governace = Governance.fromJson(governanceJson);

            var eventReq = new EventRequestModel()
            {
                Time = DateTime.UtcNow,
                Uri = "https://www.google.com/search",
                Verb = "POST",
                ApiVersion = null,
                IpAddress = null,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };
            var eventModel = new EventModel()
            {
                Request = eventReq,
                CompanyId = null,
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            Assert.AreEqual(GovernanceHelper.enforceGovernaceRule(eventModel, governace, appConfig), true);

            Assert.AreEqual(eventModel.BlockedBy, "62f6c661ac3331776950eba1");
            Assert.AreEqual(eventModel.Response.Status, 203);

        }
        [Fact]
        public void It_should_block_on_entity_rule_when_regex_rule_is_not_defined()
        {
            var governanceJson = "[{\"_id\":\"62f69205ec701a4f0400a377\",\"created_at\":\"2022-08-12T17:46:45.670\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"my gov\",\"block\":true,\"type\":\"company\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"POST\"}]}],\"response\":{\"status\":203,\"headers\":{},\"body\":{\"ok\":1}}},{\"_id\":\"62f6c661ac3331776950eba1\",\"created_at\":\"2022-08-12T21:30:09.523\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"my regex\",\"block\":true,\"type\":\"regex\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"POST\"}]}],\"response\":{\"status\":203,\"headers\":{},\"body\":{\"ok\":1}}},{\"_id\":\"62fd061e51f905712d73f72d\",\"created_at\":\"2022-08-17T15:15:42.909\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"user\",\"block\":true,\"type\":\"user\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.body.operationName\",\"value\":\"Get.*\"}]}],\"response\":{\"status\":203,\"headers\":{},\"body\":{\"error\":\"this is a test\"}}},{\"_id\":\"62fe6f3bf199ee4cf35762d7\",\"created_at\":\"2022-08-18T16:56:27.767\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"company rule 2\",\"block\":true,\"type\":\"company\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"DELETE\"}]}],\"response\":{\"status\":401,\"headers\":{},\"body\":{\"error\":\"test\"}}},{\"_id\":\"62ffc2e77a9aca1bfdefc3e3\",\"created_at\":\"2022-08-19T17:05:43.321\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"graphql2\",\"block\":true,\"type\":\"regex\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.body.query\",\"value\":\".*Get.*\"}]}],\"response\":{\"status\":401,\"headers\":{},\"body\":{\"test\":\"graph2\"}}},{\"_id\":\"62fff3367a9aca1bfdefc3f1\",\"created_at\":\"2022-08-19T20:31:50.765\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"company rule no regex\",\"block\":true,\"type\":\"company\",\"regex_config\":[],\"response\":{\"status\":402,\"headers\":{},\"body\":{\"status\":\"make payment\"}}},{\"_id\":\"6317c7ba63501c63e3ff4ee0\",\"created_at\":\"2022-09-06T22:20:42.060\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"user rule\",\"block\":true,\"type\":\"user\",\"variables\":[{\"name\":\"0\",\"path\":\"cohort_names\"},{\"name\":\"1\",\"path\":\"created\"},{\"name\":\"2\",\"path\":\"identified_user_id\"},{\"name\":\"3\",\"path\":\"company_id\"}],\"regex_config\":[],\"response\":{\"status\":204,\"headers\":{\"my header\":\"{{0}}\",\"my header2\":\"{{1}}\",\"header3\":\"{{2}}\"},\"body\":{\"yes\":true,\"{{3}}\":\"yes\"}}}]";
            var configJson = "{\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"sample_rate\":100,\"block_bot_traffic\":false,\"user_sample_rate\":{\"sean-user-11\":70,\"sean-user-10\":70,\"sean-user-9\":70},\"company_sample_rate\":{\"sean-company-5\":50,\"67890\":50,\"sean-company-9\":50,\"sean-company-6\":50,\"sean-company-10\":50,\"sean-company-11\":50,\"company_1234\":50},\"user_rules\":{\"sean-user-11\":[{\"rules\":\"6317c7ba63501c63e3ff4ee0\",\"values\":{\"0\":\"cohort_names\",\"1\":\"created\",\"2\":\"identified_user_id\",\"3\":\"sean-company-11\"}}],\"sean-user-10\":[{\"rules\":\"6317c7ba63501c63e3ff4ee0\",\"values\":{\"0\":\"cohort_names\",\"1\":\"created\",\"2\":\"identified_user_id\",\"3\":\"sean-company-10\"}}],\"sean-user-9\":[{\"rules\":\"6317c7ba63501c63e3ff4ee0\",\"values\":{\"0\":\"cohort_names\",\"1\":\"created\",\"2\":\"identified_user_id\",\"3\":\"sean-company-9\"}}]},\"company_rules\":{\"sean-company-5\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"67890\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-9\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-6\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-10\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-11\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"company_1234\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}]},\"ip_addresses_blocked_by_name\":{},\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"post\"}],\"sample_rate\":20}],\"billing_config_jsons\":{}}";
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(configJson);
            var governace = Governance.fromJson(governanceJson);

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
            var eventModel = new EventModel()
            {
                Request = eventReq,
                CompanyId = "sean-company-11",
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

           Assert.AreEqual(GovernanceHelper.enforceGovernaceRule(eventModel, governace, appConfig), true);

        }

        [Fact]
        public void It_should_block_on_entity_rule_when_regex_rule_also_match()
        {
            var governanceJson = "[{\"_id\":\"62f69205ec701a4f0400a377\",\"created_at\":\"2022-08-12T17:46:45.670\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"my gov\",\"block\":true,\"type\":\"company\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"POST\"}]}],\"response\":{\"status\":203,\"headers\":{},\"body\":{\"ok\":1}}},{\"_id\":\"62f6c661ac3331776950eba1\",\"created_at\":\"2022-08-12T21:30:09.523\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"my regex\",\"block\":true,\"type\":\"regex\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"POST\"}]}],\"response\":{\"status\":203,\"headers\":{},\"body\":{\"ok\":1}}},{\"_id\":\"62fd061e51f905712d73f72d\",\"created_at\":\"2022-08-17T15:15:42.909\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"user\",\"block\":true,\"type\":\"user\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.body.operationName\",\"value\":\"Get.*\"}]}],\"response\":{\"status\":203,\"headers\":{},\"body\":{\"error\":\"this is a test\"}}},{\"_id\":\"62fe6f3bf199ee4cf35762d7\",\"created_at\":\"2022-08-18T16:56:27.767\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"company rule 2\",\"block\":true,\"type\":\"company\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"DELETE\"}]}],\"response\":{\"status\":401,\"headers\":{},\"body\":{\"error\":\"test\"}}},{\"_id\":\"62ffc2e77a9aca1bfdefc3e3\",\"created_at\":\"2022-08-19T17:05:43.321\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"graphql2\",\"block\":true,\"type\":\"regex\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.body.query\",\"value\":\".*Get.*\"}]}],\"response\":{\"status\":401,\"headers\":{},\"body\":{\"test\":\"graph2\"}}},{\"_id\":\"62fff3367a9aca1bfdefc3f1\",\"created_at\":\"2022-08-19T20:31:50.765\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"company rule no regex\",\"block\":true,\"type\":\"company\",\"regex_config\":[],\"response\":{\"status\":402,\"headers\":{},\"body\":{\"status\":\"make payment\"}}},{\"_id\":\"6317c7ba63501c63e3ff4ee0\",\"created_at\":\"2022-09-06T22:20:42.060\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"user rule\",\"block\":true,\"type\":\"user\",\"variables\":[{\"name\":\"0\",\"path\":\"cohort_names\"},{\"name\":\"1\",\"path\":\"created\"},{\"name\":\"2\",\"path\":\"identified_user_id\"},{\"name\":\"3\",\"path\":\"company_id\"}],\"regex_config\":[],\"response\":{\"status\":204,\"headers\":{\"my header\":\"{{0}}\",\"my header2\":\"{{1}}\",\"header3\":\"{{2}}\"},\"body\":{\"yes\":true,\"{{3}}\":\"yes\"}}}]";
            var configJson = "{\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"sample_rate\":100,\"block_bot_traffic\":false,\"user_sample_rate\":{\"sean-user-11\":70,\"sean-user-10\":70,\"sean-user-9\":70},\"company_sample_rate\":{\"sean-company-5\":50,\"67890\":50,\"sean-company-9\":50,\"sean-company-6\":50,\"sean-company-10\":50,\"sean-company-11\":50,\"company_1234\":50},\"user_rules\":{\"sean-user-11\":[{\"rules\":\"6317c7ba63501c63e3ff4ee0\",\"values\":{\"0\":\"cohort_names\",\"1\":\"created\",\"2\":\"identified_user_id\",\"3\":\"sean-company-11\"}}],\"sean-user-10\":[{\"rules\":\"6317c7ba63501c63e3ff4ee0\",\"values\":{\"0\":\"cohort_names\",\"1\":\"created\",\"2\":\"identified_user_id\",\"3\":\"sean-company-10\"}}],\"sean-user-9\":[{\"rules\":\"6317c7ba63501c63e3ff4ee0\",\"values\":{\"0\":\"cohort_names\",\"1\":\"created\",\"2\":\"identified_user_id\",\"3\":\"sean-company-9\"}}]},\"company_rules\":{\"sean-company-5\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"67890\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-9\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-6\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-10\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-11\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"company_1234\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}]},\"ip_addresses_blocked_by_name\":{},\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"post\"}],\"sample_rate\":20}],\"billing_config_jsons\":{}}";
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(configJson);
            var governace = Governance.fromJson(governanceJson);

            var eventReq = new EventRequestModel()
            {
                Time = DateTime.UtcNow,
                Uri = "https://www.google.com/search",
                Verb = "POST",
                ApiVersion = null,
                IpAddress = null,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };
            var eventModel = new EventModel()
            {
                Request = eventReq,
                CompanyId = "sean-company-11",
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            Assert.AreEqual(GovernanceHelper.enforceGovernaceRule(eventModel, governace, appConfig), true);

            Assert.AreEqual(eventModel.BlockedBy, "62fff3367a9aca1bfdefc3f1");

        }

        [Fact]
        public void It_should_block_on_regex_rule_when_no_entity_rule_match()
        {
            var governanceJson = "[{\"_id\":\"62f69205ec701a4f0400a377\",\"created_at\":\"2022-08-12T17:46:45.670\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"my gov\",\"block\":true,\"type\":\"company\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"POST\"}]}],\"response\":{\"status\":203,\"headers\":{},\"body\":{\"ok\":1}}},{\"_id\":\"62f6c661ac3331776950eba1\",\"created_at\":\"2022-08-12T21:30:09.523\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"my regex\",\"block\":true,\"type\":\"regex\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"POST\"}]}],\"response\":{\"status\":203,\"headers\":{},\"body\":{\"ok\":1}}},{\"_id\":\"62fd061e51f905712d73f72d\",\"created_at\":\"2022-08-17T15:15:42.909\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"user\",\"block\":true,\"type\":\"user\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.body.operationName\",\"value\":\"Get.*\"}]}],\"response\":{\"status\":203,\"headers\":{},\"body\":{\"error\":\"this is a test\"}}},{\"_id\":\"62fe6f3bf199ee4cf35762d7\",\"created_at\":\"2022-08-18T16:56:27.767\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"company rule 2\",\"block\":true,\"type\":\"company\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"DELETE\"}]}],\"response\":{\"status\":401,\"headers\":{},\"body\":{\"error\":\"test\"}}},{\"_id\":\"62ffc2e77a9aca1bfdefc3e3\",\"created_at\":\"2022-08-19T17:05:43.321\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"graphql2\",\"block\":true,\"type\":\"regex\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.body.query\",\"value\":\".*Get.*\"}]}],\"response\":{\"status\":401,\"headers\":{},\"body\":{\"test\":\"graph2\"}}},{\"_id\":\"62fff3367a9aca1bfdefc3f1\",\"created_at\":\"2022-08-19T20:31:50.765\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"company rule no regex\",\"block\":true,\"type\":\"company\",\"regex_config\":[],\"response\":{\"status\":402,\"headers\":{},\"body\":{\"status\":\"make payment\"}}},{\"_id\":\"6317c7ba63501c63e3ff4ee0\",\"created_at\":\"2022-09-06T22:20:42.060\",\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"name\":\"user rule\",\"block\":true,\"type\":\"user\",\"variables\":[{\"name\":\"0\",\"path\":\"cohort_names\"},{\"name\":\"1\",\"path\":\"created\"},{\"name\":\"2\",\"path\":\"identified_user_id\"},{\"name\":\"3\",\"path\":\"company_id\"}],\"regex_config\":[],\"response\":{\"status\":204,\"headers\":{\"my header\":\"{{0}}\",\"my header2\":\"{{1}}\",\"header3\":\"{{2}}\"},\"body\":{\"yes\":true,\"{{3}}\":\"yes\"}}}]";
            var configJson = "{\"org_id\":\"421:67\",\"app_id\":\"46:73\",\"sample_rate\":100,\"block_bot_traffic\":false,\"user_sample_rate\":{\"sean-user-11\":70,\"sean-user-10\":70,\"sean-user-9\":70},\"company_sample_rate\":{\"sean-company-5\":50,\"67890\":50,\"sean-company-9\":50,\"sean-company-6\":50,\"sean-company-10\":50,\"sean-company-11\":50,\"company_1234\":50},\"user_rules\":{\"sean-user-11\":[{\"rules\":\"6317c7ba63501c63e3ff4ee0\",\"values\":{\"0\":\"cohort_names\",\"1\":\"created\",\"2\":\"identified_user_id\",\"3\":\"sean-company-11\"}}],\"sean-user-10\":[{\"rules\":\"6317c7ba63501c63e3ff4ee0\",\"values\":{\"0\":\"cohort_names\",\"1\":\"created\",\"2\":\"identified_user_id\",\"3\":\"sean-company-10\"}}],\"sean-user-9\":[{\"rules\":\"6317c7ba63501c63e3ff4ee0\",\"values\":{\"0\":\"cohort_names\",\"1\":\"created\",\"2\":\"identified_user_id\",\"3\":\"sean-company-9\"}}]},\"company_rules\":{\"sean-company-5\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"67890\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-9\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-6\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-10\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"sean-company-11\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}],\"company_1234\":[{\"rules\":\"62fe6f3bf199ee4cf35762d7\"},{\"rules\":\"62fff3367a9aca1bfdefc3f1\"}]},\"ip_addresses_blocked_by_name\":{},\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"post\"}],\"sample_rate\":20}],\"billing_config_jsons\":{}}";
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(configJson);
            var governace = Governance.fromJson(governanceJson);

            var eventReq = new EventRequestModel()
            {
                Time = DateTime.UtcNow,
                Uri = "https://www.google.com/search",
                Verb = "POST",
                ApiVersion = null,
                IpAddress = null,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };
            var eventModel = new EventModel()
            {
                Request = eventReq,
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            Assert.AreEqual(GovernanceHelper.enforceGovernaceRule(eventModel, governace, appConfig), true);

            Assert.AreEqual(eventModel.BlockedBy, "62f6c661ac3331776950eba1");

        }

        [Fact]
        public void It_should_respect_regex_both_and_or_condition()
        {
            var configJson = "{\"org_id\":\"640:128\",\"app_id\":\"487:163\",\"sample_rate\":80,\"block_bot_traffic\":false,\"user_sample_rate\":{},\"company_sample_rate\":{},\"user_rules\":{},\"company_rules\":{},\"ip_addresses_blocked_by_name\":{},\"regex_config\":[{\"conditions\":[{\"path\":\"request.verb\",\"value\":\"GET\"}],\"sample_rate\":90}],\"billing_config_jsons\":{}}";
            var governanceJson = "[{\"_id\":\"631d6d065ef7bb0f43ccd3f8\",\"created_at\":\"2022-09-11T05:07:18.679\",\"org_id\":\"640:128\",\"app_id\":\"487:163\",\"name\":\"regex 1\",\"block\":true,\"type\":\"regex\",\"regex_config\":[{\"conditions\":[{\"path\":\"request.route\",\"value\":\"/api/*\"},{\"path\":\"request.verb\",\"value\":\"POST\"}]},{\"conditions\":[{\"path\":\"request.ip_address\",\"value\":\"120.110.10.11\"}]}],\"response\":{\"status\":401,\"headers\":{},\"body\":{\"test\":1}}}]";
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(configJson);
            var governace = Governance.fromJson(governanceJson);

            var eventReq = new EventRequestModel()
            {
                Time = DateTime.UtcNow,
                Uri = "https://localhost:5001/api/Employee",
                Verb = "GET",
                ApiVersion = null,
                IpAddress = null,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };
            var eventModel = new EventModel()
            {
                Request = eventReq,
                //CompanyId = "sean-company-11",
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            Assert.AreEqual(GovernanceHelper.enforceGovernaceRule(eventModel, governace, appConfig), false);

            eventReq = new EventRequestModel()
            {
                Time = DateTime.UtcNow,
                Uri = "https://localhost:5001/api/Employee",
                Verb = "POST",
                ApiVersion = null,
                IpAddress = null,
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };
            eventModel = new EventModel()
            {
                Request = eventReq,
                //CompanyId = "sean-company-11",
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            Assert.AreEqual(GovernanceHelper.enforceGovernaceRule(eventModel, governace, appConfig), true);

            eventReq = new EventRequestModel()
            {
                Time = DateTime.UtcNow,
                Uri = "https://localhost:5001/api/Employee",
                Verb = "GET",
                ApiVersion = null,
                IpAddress = "120.110.10.11",
                Headers = new Dictionary<string, string>(),
                Body = null,
                TransferEncoding = null
            };
            eventModel = new EventModel()
            {
                Request = eventReq,
                //CompanyId = "sean-company-11",
                SessionToken = "xxxx",
                Metadata = new Dictionary<string, string>(),
                Direction = "Outgoing"
            };

            Assert.AreEqual(GovernanceHelper.enforceGovernaceRule(eventModel, governace, appConfig), true);

        }

    }
}
