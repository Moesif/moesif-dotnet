using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Owin;
using Moesif.Middleware;
using NUnit.Framework;

namespace Moesif.NetFramework.Test
{
    /// <summary>
    /// Contract tests for the NET461/OWIN build of <see cref="MoesifMiddleware"/>.
    ///
    /// Historically the outer <c>Invoke(IOwinContext)</c> wrapped everything in
    /// a bare <c>try { ... } catch { }</c>, silently swallowing every exception
    /// raised inside the SDK. Removed so exceptions reach the caller's error
    /// handler instead of disappearing into a fake success response.
    /// </summary>
    [TestFixture]
    public class MoesifMiddlewareInvokeTests
    {
        [Test]
        public void Invoke_TakesOwinContext_AndReturnsTask()
        {
            var m = typeof(MoesifMiddleware).GetMethod(
                "Invoke",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(IOwinContext) },
                modifiers: null);
            Assert.IsNotNull(m);
            Assert.AreEqual(typeof(Task), m.ReturnType);
        }
    }
}