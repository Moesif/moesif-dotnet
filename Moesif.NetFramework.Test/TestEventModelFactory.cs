using System;
using System.Collections.Generic;
using Moesif.Api.Models;

namespace Moesif.NetFramework.Test
{
    /// <summary>
    /// Small helper for building <see cref="EventModel"/> instances in tests.
    /// Mirror of the NetCore version, but the SDK code exercised here is the
    /// NET461 branch (Newtonsoft.Json-based).
    /// </summary>
    internal static class TestEventModelFactory
    {
        public static EventModel NewJsonEvent(object body)
        {
            return new EventModel
            {
                Direction = "Incoming",
                Request = new EventRequestModel
                {
                    Time = DateTime.UtcNow,
                    Uri = "http://localhost/api/strict",
                    Verb = "POST",
                    IpAddress = "127.0.0.1",
                    Headers = new Dictionary<string, string>
                    {
                        { "content-type", "application/json" }
                    },
                    Body = body,
                    TransferEncoding = "json"
                }
            };
        }
    }
}