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

namespace Moesif.NetFramework.Test
{
    [TestFixture()]
    public class MoesifNetFrameworkTest
    {
        public Dictionary<string, object> moesifOptions = new Dictionary<string, object>();

        public MoesifMiddleware moesifMiddleware;

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

            Dictionary<string, object> user = new Dictionary<string, object>
            {
                {"user_id", "csharpapiuser"},
                {"metadata", metadata},
            };

            moesifMiddleware.UpdateUser(user);
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
                {"user_id", "csharpapiuser"},
                {"metadata", metadata},
            };

            Dictionary<string, object> userB = new Dictionary<string, object>
            {
                {"user_id", "csharpapiuser1"},
                {"modified_time", DateTime.UtcNow},
                {"metadata", metadata},
            };

            usersBatch.Add(userA);
            usersBatch.Add(userB);

            moesifMiddleware.UpdateUsersBatch(usersBatch);
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

            Dictionary<string, object> company = new Dictionary<string, object>
            {
                {"company_id", "csharpapicompany"},
                {"company_domain", "acmeinc.com"},
                {"metadata", metadata},
            };

            moesifMiddleware.UpdateCompany(company);
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
                {"company_id", "csharpapicompany"},
                {"company_domain", "acmeinc.com"},
                {"metadata", metadata},
            };

            Dictionary<string, object> companyB = new Dictionary<string, object>
            {
                {"company_id", "csharpapicompany1"},
                {"company_domain", "nowhere.com"},
                {"metadata", metadata},
            };

            companiesBatch.Add(companyA);
            companiesBatch.Add(companyB);

            moesifMiddleware.UpdateCompaniesBatch(companiesBatch);
        }
    }
}
