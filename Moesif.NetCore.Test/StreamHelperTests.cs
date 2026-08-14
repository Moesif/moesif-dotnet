using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moesif.Middleware.Helpers;
using Xunit;
using Assert = NUnit.Framework.Assert;

namespace Moesif.NetCore.Test
{
    /// <summary>
    /// Tests for <see cref="StreamHelper"/> — the wrapper around the outgoing
    /// response stream that mirrors bytes into a memory buffer for Moesif logging.
    ///
    /// Historically it only overrode the sync Write/Flush methods, which caused the
    /// base Stream.WriteAsync path to fall back to sync Write and throw on Kestrel
    /// (which disallows sync IO by default). It also silently swallowed sync-write
    /// failures, turning "write failed" into "empty body sent to the client".
    ///
    /// These tests pin down:
    ///   1. Async overrides copy to CopyStream AND forward to InnerStream async.
    ///   2. Async overrides never touch the sync Write path (Kestrel-safe).
    ///   3. Sync Write / Flush rethrow inner-stream exceptions (no silent swallow).
    ///   4. ReadStream returns exactly what was captured.
    /// </summary>
    public class StreamHelperTests
    {
        [Fact]
        public async Task WriteAsync_ByteArray_MirrorsToCopyStreamAndInner()
        {
            var inner = new MemoryStream();
            var helper = new StreamHelper(inner);
            var payload = Encoding.UTF8.GetBytes("hello");

            await helper.WriteAsync(payload, 0, payload.Length, CancellationToken.None);

            Assert.AreEqual(payload, inner.ToArray());
            Assert.AreEqual(payload, helper.CopyStream.ToArray());
        }

        [Fact]
        public async Task WriteAsync_ReadOnlyMemory_MirrorsToCopyStreamAndInner()
        {
            var inner = new MemoryStream();
            var helper = new StreamHelper(inner);
            var payload = Encoding.UTF8.GetBytes("world");

            await helper.WriteAsync(new ReadOnlyMemory<byte>(payload), CancellationToken.None);

            Assert.AreEqual(payload, inner.ToArray());
            Assert.AreEqual(payload, helper.CopyStream.ToArray());
        }

        [Fact]
        public async Task FlushAsync_DelegatesToInner()
        {
            var inner = new FlushTrackingStream();
            var helper = new StreamHelper(inner);

            await helper.FlushAsync(CancellationToken.None);

            Assert.AreEqual(1, inner.AsyncFlushCount);
            Assert.AreEqual(0, inner.SyncFlushCount);
        }

        /// <summary>
        /// Regression check for the original silent-200-empty bug: uses an inner
        /// stream that throws on sync Write (like Kestrel) and asserts that
        /// StreamHelper.WriteAsync completes without ever touching the sync path.
        /// </summary>
        [Fact]
        public async Task WriteAsync_UsesAsyncPath_NotSyncFallback()
        {
            var inner = new SyncWriteRejectingStream();
            var helper = new StreamHelper(inner);
            var payload = Encoding.UTF8.GetBytes("kestrel-safe");

            await helper.WriteAsync(payload, 0, payload.Length, CancellationToken.None);
            await helper.WriteAsync(new ReadOnlyMemory<byte>(payload), CancellationToken.None);

            Assert.AreEqual(0, inner.SyncWriteCallCount);
            Assert.IsTrue(inner.AsyncWriteCallCount >= 2);
        }

        [Fact]
        public async Task FlushAsync_UsesAsyncPath_NotSyncFallback()
        {
            var inner = new SyncFlushRejectingStream();
            var helper = new StreamHelper(inner);

            await helper.FlushAsync(CancellationToken.None);

            Assert.AreEqual(0, inner.SyncFlushCallCount);
            Assert.AreEqual(1, inner.AsyncFlushCallCount);
        }

        [Fact]
        public void SyncWrite_Rethrows_WhenInnerThrows()
        {
            var inner = new SyncWriteRejectingStream();
            var helper = new StreamHelper(inner);
            var payload = Encoding.UTF8.GetBytes("x");

            Assert.Throws<InvalidOperationException>(() => helper.Write(payload, 0, payload.Length));
        }

        [Fact]
        public void SyncFlush_Rethrows_WhenInnerThrows()
        {
            var inner = new SyncFlushRejectingStream();
            var helper = new StreamHelper(inner);

            Assert.Throws<InvalidOperationException>(() => helper.Flush());
        }

        [Fact]
        public async Task ReadStream_ReturnsCapturedBytes()
        {
            var inner = new MemoryStream();
            var helper = new StreamHelper(inner);
            var payload = Encoding.UTF8.GetBytes("captured-body");

            await helper.WriteAsync(payload, 0, payload.Length, CancellationToken.None);

            var read = helper.ReadStream(contentEncoding: null);
            Assert.AreEqual("captured-body", read);
        }

        // ---------- Fake streams used to model Kestrel-style constraints ----------

        /// <summary>Mimics Kestrel: sync Write throws, async WriteAsync succeeds.</summary>
        private sealed class SyncWriteRejectingStream : Stream
        {
            private readonly MemoryStream _inner = new MemoryStream();
            public int SyncWriteCallCount { get; private set; }
            public int AsyncWriteCallCount { get; private set; }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => true;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);

            public override void Write(byte[] buffer, int offset, int count)
            {
                SyncWriteCallCount++;
                throw new InvalidOperationException(
                    "Synchronous operations are disallowed. Call WriteAsync or set AllowSynchronousIO to true instead.");
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                AsyncWriteCallCount++;
                _inner.Write(buffer, offset, count);
                return Task.CompletedTask;
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                AsyncWriteCallCount++;
                _inner.Write(buffer.Span);
                return ValueTask.CompletedTask;
            }
        }

        private sealed class SyncFlushRejectingStream : Stream
        {
            private readonly MemoryStream _inner = new MemoryStream();
            public int SyncFlushCallCount { get; private set; }
            public int AsyncFlushCallCount { get; private set; }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => true;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

            public override void Flush()
            {
                SyncFlushCallCount++;
                throw new InvalidOperationException("Synchronous operations are disallowed.");
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                AsyncFlushCallCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class FlushTrackingStream : Stream
        {
            private readonly MemoryStream _inner = new MemoryStream();
            public int SyncFlushCount { get; private set; }
            public int AsyncFlushCount { get; private set; }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => true;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
            public override void Flush() { SyncFlushCount++; }
            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                AsyncFlushCount++;
                return Task.CompletedTask;
            }
        }
    }
}