using System.Collections.Generic;
using Moesif.Middleware.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Moesif.NetFramework.Test
{
    /// <summary>
    /// Tests for the NET461 branch of <see cref="RequestMapHelper"/>.createRequestMap.
    ///
    /// Historically the net461 branch did an unchecked <c>(JObject)model.Request.Body</c>
    /// cast for JSON bodies, which threw for any array/primitive body. Fixed by
    /// using a safe <c>as JObject</c> cast with a null-guard so non-object
    /// bodies simply skip the GraphQL-extraction path instead of blowing up.
    ///
    /// (The GetJsonObject helper only exists in the NETCORE branch, so we only
    /// exercise createRequestMap here — that's where the net461 fix lives.)
    /// </summary>
    [TestFixture]
    public class RequestMapHelperTests
    {
        [Test]
        public void CreateRequestMap_DoesNotThrow_ForArrayJsonBody()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(new JArray());
            Assert.DoesNotThrow(() => RequestMapHelper.createRequestMap(eventModel));
        }

        [Test]
        public void CreateRequestMap_DoesNotThrow_ForNonEmptyArrayJsonBody()
        {
            var body = new JArray { new JObject { { "name", "Thing" } } };
            var eventModel = TestEventModelFactory.NewJsonEvent(body);
            Assert.DoesNotThrow(() => RequestMapHelper.createRequestMap(eventModel));
        }

        [Test]
        public void CreateRequestMap_DoesNotThrow_ForPrimitiveJsonBody()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(new JValue(42));
            Assert.DoesNotThrow(() => RequestMapHelper.createRequestMap(eventModel));
        }

        [Test]
        public void CreateRequestMap_DoesNotThrow_ForNullBody()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(null);
            Assert.DoesNotThrow(() => RequestMapHelper.createRequestMap(eventModel));
        }

        [Test]
        public void CreateRequestMap_PopulatesBasicFields()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(new JObject());
            var map = RequestMapHelper.createRequestMap(eventModel);

            Assert.IsTrue(map.regex_mapping.ContainsKey("request.verb"));
            Assert.IsTrue(map.regex_mapping.ContainsKey("request.ip_address"));
            Assert.IsTrue(map.regex_mapping.ContainsKey("request.route"));
        }

        [Test]
        public void CreateRequestMap_ExtractsGraphQLFields_FromObjectBody()
        {
            var body = new JObject
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

        [Test]
        public void CreateRequestMap_DoesNotAddRequestBody_ForArrayBody()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(new JArray());
            var map = RequestMapHelper.createRequestMap(eventModel);
            Assert.IsFalse(map.regex_mapping.ContainsKey("request.body"));
        }
    }
}