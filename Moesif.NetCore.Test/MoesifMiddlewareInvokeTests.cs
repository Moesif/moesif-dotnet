using System.Reflection;
using System.Threading.Tasks;
using Moesif.Middleware;
using Xunit;
using Assert = NUnit.Framework.Assert;

namespace Moesif.NetCore.Test
{
    /// <summary>
    /// Contract tests for <see cref="MoesifMiddleware"/>.
    ///
    /// Historically the outer <c>Invoke</c> wrapped everything in a bare
    /// <c>try { ... } catch { }</c>, silently swallowing every exception raised
    /// inside the SDK. Combined with other latent bugs (e.g. the AsObject()
    /// throw on array bodies), that produced a silent <c>200 OK, Content-Length: 0</c>
    /// on the client and no event in the Moesif dashboard. The swallowing catch
    /// has been removed so exceptions propagate to the app's
    /// <c>UseExceptionHandler</c> instead of disappearing.
    ///
    /// Behavioural verification for the full request pipeline lives in the
    /// integration suite (<c>MoesifRepro/run-tests.sh</c>).
    /// </summary>
    public class MoesifMiddlewareInvokeTests
    {
        [Fact]
        public void Invoke_ReturnsTask()
        {
            var m = typeof(MoesifMiddleware).GetMethod(
                "Invoke",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(m);
            Assert.AreEqual(typeof(Task), m.ReturnType);
        }
    }
}