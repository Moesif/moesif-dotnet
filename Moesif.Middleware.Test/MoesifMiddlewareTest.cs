using System;
using NUnit.Framework;
using System.Collections.Generic;
using Moesif.Middleware;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Net;

namespace Moesif.Middleware.Test
{
    public class MoesifMiddlewareTest{

        public Dictionary<string, object> moesifOptions = new Dictionary<string, object>();

        public MoesifMiddleware moesifMiddleware;

        public MoesifMiddlewareTest() {

            moesifOptions.Add("ApplicationId", "Your Application Id");
            moesifOptions.Add("LocalDebug", true);
            moesifOptions.Add("ApiVersion", "1.0.0");

            moesifMiddleware = new MoesifMiddleware(next: (innerHttpContext) => Task.FromResult(0), _middleware: moesifOptions);
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

            await moesifMiddleware.Invoke(contextMock.Object);

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

            Dictionary<string, object> user = new Dictionary<string, object>
            {
                {"user_id", "csharpapiuser"},
                {"metadata", metadata},
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
    }
}
