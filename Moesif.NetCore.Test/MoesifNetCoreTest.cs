using System;
using NUnit.Framework;
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

namespace Moesif.NetCore.Test
{
    public class MoesifNetCoreTest{

        public Dictionary<string, object> moesifOptions = new Dictionary<string, object>();

        public MoesifMiddleware moesifMiddleware;

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

        public static Func<HttpRequestMessage, HttpResponseMessage, bool> SkipOutgoing = (HttpRequestMessage req, HttpResponseMessage res) => {
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

            moesifMiddleware = new MoesifMiddleware((innerHttpContext) => Task.FromResult(0), moesifOptions);
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
        public void It_Should_Update_User() {
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

            moesifMiddleware.UpdateUser(user);
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

            moesifMiddleware.UpdateUsersBatch(usersBatch);
        }

        [Fact]
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

            moesifMiddleware.UpdateCompany(company);
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

            moesifMiddleware.UpdateCompaniesBatch(companiesBatch);
        }
    }
}
