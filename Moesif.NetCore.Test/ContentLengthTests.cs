using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moesif.Api.Models;
using Moesif.Middleware.NetCore;
using Moesif.Middleware.NetCore.Helpers;
using Xunit;
using Assert = NUnit.Framework.Assert;
using StringAssert = NUnit.Framework.StringAssert;

namespace Moesif.NetCore.Test
{
    /// <summary>Chunked request bodies were dropped when no Content-Length was sent.</summary>
    public class ContentLengthTests
    {
        private const int MaxBodySize = 100000;
        private const string Json = "{\"hello\":\"chunked world\"}";

        // ---------- Layer 1: the helper in isolation ----------

        [Fact]
        public void GetContentLengthAndEncoding_NoContentLengthHeader_FallsBackToDefault()
        {
            // Headers as a chunked client sends them: no Content-Length at all.
            var headers = new Dictionary<string, string>
            {
                { "Transfer-Encoding", "chunked" },
                { "Content-Type", "application/json" },
            };

            MoesifMiddlewareNetCore.GetContentLengthAndEncoding(
                headers, defaultLength: MaxBodySize - 1, out int parsed);

            Assert.AreEqual(MaxBodySize - 1, parsed,
                "Missing Content-Length is an unknown length, not zero.");
        }

        [Fact]
        public void GetContentLengthAndEncoding_ExplicitZero_StaysZero()
        {
            var headers = new Dictionary<string, string> { { "Content-Length", "0" } };

            MoesifMiddlewareNetCore.GetContentLengthAndEncoding(
                headers, defaultLength: MaxBodySize - 1, out int parsed);

            Assert.AreEqual(0, parsed, "An explicit Content-Length: 0 does mean no body.");
        }

        [Fact]
        public void GetContentLengthAndEncoding_ValidContentLength_IsUsed()
        {
            var headers = new Dictionary<string, string> { { "Content-Length", "24" } };

            MoesifMiddlewareNetCore.GetContentLengthAndEncoding(
                headers, defaultLength: MaxBodySize - 1, out int parsed);

            Assert.AreEqual(24, parsed);
        }

        [Fact]
        public void GetContentLengthAndEncoding_GarbageContentLength_FallsBackToDefault()
        {
            var headers = new Dictionary<string, string> { { "Content-Length", "not-a-number" } };

            MoesifMiddlewareNetCore.GetContentLengthAndEncoding(
                headers, defaultLength: MaxBodySize - 1, out int parsed);

            Assert.AreEqual(MaxBodySize - 1, parsed,
                "An unparseable Content-Length is also unknown, not zero.");
        }

        [Fact]
        public void GetContentLengthAndEncoding_StillReadsContentEncoding()
        {
            var headers = new Dictionary<string, string>
            {
                { "Transfer-Encoding", "chunked" },
                { "Content-Encoding", "gzip" },
            };

            var encoding = MoesifMiddlewareNetCore.GetContentLengthAndEncoding(
                headers, defaultLength: MaxBodySize - 1, out int _);

            Assert.AreEqual("gzip", encoding,
                "Content-Encoding must still be read when Content-Length is absent.");
        }

        // ---------- Layer 2: the symptom, through FormatRequest ----------

        [Fact]
        public async Task FormatRequest_ChunkedRequestWithoutContentLength_CapturesBody()
        {
            var (eventReq, _) = await FormatRequest(withContentLength: false);

            Assert.IsNotNull(eventReq.Body, "Chunked request body was dropped.");
        }

        [Fact]
        public async Task FormatRequest_RequestWithContentLength_CapturesBody()
        {
            // Control case: the axios-style request that always worked.
            var (eventReq, _) = await FormatRequest(withContentLength: true);

            Assert.IsNotNull(eventReq.Body, "Control case regressed.");
        }

        [Fact]
        public void GetContentLengthAndEncoding_NullHeaders_ReturnsDefault()
        {
            // Pins the else-branch the fix removed.
            var encoding = MoesifMiddlewareNetCore.GetContentLengthAndEncoding(
                null, defaultLength: MaxBodySize - 1, out int parsed);

            Assert.AreEqual(MaxBodySize - 1, parsed);
            Assert.AreEqual("", encoding);
        }

        [Fact]
        public async Task FormatRequest_ChunkedBodyOverMaxSize_ReplacedWithExceededMessage()
        {
            // Without Content-Length the pre-read size check cannot fire.
            var big = Encoding.UTF8.GetBytes(new string('x', 500));

            var (eventReq, _) = await FormatRequest(
                withContentLength: false, bodyBytes: big, maxBodySize: 100);

            Assert.IsNotNull(eventReq.Body);
            StringAssert.Contains("exceeded", eventReq.Body.ToString(),
                "An oversize chunked body must be swapped for the exceeded message.");
        }

        [Fact]
        public async Task FormatRequest_EmptyChunkedBody_CapturesNothing()
        {
            var (eventReq, _) = await FormatRequest(
                withContentLength: false, bodyBytes: new byte[0]);

            Assert.IsNull(eventReq.Body, "An empty chunked body should capture nothing.");
        }

        [Fact]
        public async Task FormatRequest_ChunkedWithLogBodyDisabled_CapturesNothing()
        {
            var (eventReq, _) = await FormatRequest(withContentLength: false, logBody: false);

            Assert.IsNull(eventReq.Body, "LogBody: false must still be honoured for chunked.");
        }

        // Pre-existing bug, not the chunked fix: GZipStream without leaveOpen closes request.Body.
        [Fact(Skip = "Blocked by gzip request-body bug in Helpers/Compression.cs")]
        public async Task FormatRequest_GzippedChunkedBody_IsDecompressed()
        {
            byte[] gzipped;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
                {
                    var raw = Encoding.UTF8.GetBytes(Json);
                    gz.Write(raw, 0, raw.Length);
                }
                gzipped = ms.ToArray();
            }

            var (eventReq, _) = await FormatRequest(
                withContentLength: false, bodyBytes: gzipped, contentEncoding: "gzip");

            Assert.IsNotNull(eventReq.Body, "Gzipped chunked body was dropped.");
            StringAssert.Contains("chunked world", eventReq.Body.ToString());
        }

        private static async Task<(EventRequestModel, string)> FormatRequest(
            bool withContentLength,
            byte[] bodyBytes = null,
            string contentEncoding = null,
            int maxBodySize = MaxBodySize,
            bool logBody = true)
        {
            // Skip the ctor: it needs an ApplicationId and spawns API-calling threads.
            var mw = (MoesifMiddlewareNetCore)RuntimeHelpers.GetUninitializedObject(
                typeof(MoesifMiddlewareNetCore));

            typeof(MoesifMiddlewareNetCore)
                .GetField("loggerHelper", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mw, new LoggerHelper(NullLogger.Instance));

            mw.moesifOptions = new Dictionary<string, object>();
            mw.logBody = logBody;
            mw.debug = false;
            mw.requestMaxBodySize = maxBodySize;
            mw.responseMaxBodySize = maxBodySize;

            var bytes = bodyBytes ?? Encoding.UTF8.GetBytes(Json);

            var ctx = new DefaultHttpContext();
            // ClientIp.GetClientIp dereferences this in both the try and the catch.
            ctx.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
            var req = ctx.Request;
            req.Method = "POST";
            req.Scheme = "http";
            req.Host = new HostString("localhost");
            req.Path = "/api/test";
            req.ContentType = "application/json";
            req.Body = new MemoryStream(bytes);

            if (withContentLength)
            {
                req.ContentLength = bytes.Length;
            }
            else
            {
                // What Java HttpURLConnection sends in chunked mode.
                req.Headers["Transfer-Encoding"] = "chunked";
            }

            if (contentEncoding != null)
            {
                req.Headers["Content-Encoding"] = contentEncoding;
            }

            var formatRequest = typeof(MoesifMiddlewareNetCore)
                .GetMethod("FormatRequest", BindingFlags.NonPublic | BindingFlags.Instance);

            var task = (Task<(EventRequestModel, string)>)formatRequest.Invoke(
                mw, new object[] { req, null });

            return await task;
        }
    }
}
