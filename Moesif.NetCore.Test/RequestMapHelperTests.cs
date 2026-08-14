using System;
using System.Collections.Generic;
using Moesif.Api.Models;
using Moesif.Middleware.Helpers;
using Xunit;
using Assert = NUnit.Framework.Assert;

namespace Moesif.NetCore.Test
{
    /// <summary>
    /// Tests for <see cref="RequestMapHelper"/> — builds the regex-mapping bag
    /// used by governance rules from an incoming <see cref="EventModel"/>.
    ///
    /// Historically <c>GetJsonObject</c> called <c>JsonNode.AsObject()</c>, which
    /// throws for any JSON body that isn't an object (arrays, primitives, null).
    /// That threw inside Moesif's own middleware and — combined with the outer
    /// bare-catch in <c>MoesifMiddleware.Invoke</c> — surfaced as a silent
    /// <c>200 OK, Content-Length: 0</c> from the server. Fixed by using a safe
    /// <c>as JsonObject</c> cast.
    /// </summary>
    public class RequestMapHelperTests
    {
        // ---------- GetJsonObject: safe cast for non-object bodies ----------

        [Fact]
        public void GetJsonObject_ReturnsObject_ForObjectBody()
        {
            var body = new Dictionary<string, object> { { "name", "Thing" } };
            var result = RequestMapHelper.GetJsonObject(body);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ContainsKey("name"));
            Assert.AreEqual("Thing", result["name"].GetValue<string>());
        }

        [Fact]
        public void GetJsonObject_ReturnsNull_ForEmptyArrayBody()
        {
            var result = RequestMapHelper.GetJsonObject(new List<object>());
            Assert.IsNull(result);
        }

        [Fact]
        public void GetJsonObject_ReturnsNull_ForNonEmptyArrayBody()
        {
            var body = new List<object> { new { name = "Thing" } };
            var result = RequestMapHelper.GetJsonObject(body);
            Assert.IsNull(result);
        }

        [Fact]
        public void GetJsonObject_ReturnsNull_ForPrimitiveIntBody()
        {
            Assert.IsNull(RequestMapHelper.GetJsonObject(42));
        }

        [Fact]
        public void GetJsonObject_ReturnsNull_ForPrimitiveStringBody()
        {
            Assert.IsNull(RequestMapHelper.GetJsonObject("just a string"));
        }

        [Fact]
        public void GetJsonObject_ReturnsNull_ForPrimitiveBoolBody()
        {
            Assert.IsNull(RequestMapHelper.GetJsonObject(true));
        }

        // ---------- createRequestMap: no throw on non-object bodies ----------

        [Fact]
        public void CreateRequestMap_DoesNotThrow_ForArrayJsonBody()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(body: new List<object>());
            Assert.DoesNotThrow(() => RequestMapHelper.createRequestMap(eventModel));
        }

        [Fact]
        public void CreateRequestMap_DoesNotThrow_ForPrimitiveJsonBody()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(body: 42);
            Assert.DoesNotThrow(() => RequestMapHelper.createRequestMap(eventModel));
        }

        [Fact]
        public void CreateRequestMap_DoesNotThrow_ForNullBody()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(body: null);
            Assert.DoesNotThrow(() => RequestMapHelper.createRequestMap(eventModel));
        }

        [Fact]
        public void CreateRequestMap_PopulatesBasicFields()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(body: new Dictionary<string, object>());
            var map = RequestMapHelper.createRequestMap(eventModel);

            Assert.IsTrue(map.regex_mapping.ContainsKey("request.verb"));
            Assert.IsTrue(map.regex_mapping.ContainsKey("request.ip_address"));
            Assert.IsTrue(map.regex_mapping.ContainsKey("request.route"));
        }

        [Fact]
        public void CreateRequestMap_ExtractsGraphQLFields_FromObjectBody()
        {
            var body = new Dictionary<string, object>
            {
                { "operationName", "GetThing" },
                { "query", "{ thing { id } }" }
            };
            var eventModel = TestEventModelFactory.NewJsonEvent(body);

            var map = RequestMapHelper.createRequestMap(eventModel);

            Assert.IsTrue(map.regex_mapping.ContainsKey("request.body.operationName"));
            Assert.AreEqual("GetThing", map.regex_mapping["request.body.operationName"]);
            Assert.IsTrue(map.regex_mapping.ContainsKey("request.body.query"));
            Assert.IsTrue(map.regex_mapping.ContainsKey("request.body"));
        }

        [Fact]
        public void CreateRequestMap_DoesNotAddRequestBody_ForArrayBody()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(body: new List<object>());
            var map = RequestMapHelper.createRequestMap(eventModel);
            Assert.IsFalse(map.regex_mapping.ContainsKey("request.body"));
        }
    }
}