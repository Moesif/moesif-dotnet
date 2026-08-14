using System;
using System.Collections.Generic;
using Moesif.Api.Models;

namespace Moesif.NetCore.Test
{
    /// <summary>
    /// Small helper for building <see cref="EventModel"/> instances in tests
    /// without repeating the same boilerplate. Only the fields exercised by
    /// the SDK code under test are populated.
    /// </summary>
    internal static class TestEventModelFactory
    {
        /// <summary>
        /// Builds an incoming JSON POST event with the given deserialized body.
        /// Body may be a Dictionary, List, primitive, or null.
        /// </summary>
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