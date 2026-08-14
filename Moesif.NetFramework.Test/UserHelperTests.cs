using System.Threading.Tasks;
using Moesif.Middleware.Helpers;
using NUnit.Framework;

namespace Moesif.NetFramework.Test
{
    /// <summary>
    /// Signature-level tests for the NET461 build of <see cref="UserHelper"/>.
    ///
    /// Historically <c>UpdateUser</c> and <c>UpdateUsersBatch</c> were
    /// <c>async void</c>. Any exception escaping the inner catch would crash
    /// the host process. Fixed by returning <c>Task</c>.
    /// </summary>
    [TestFixture]
    public class UserHelperTests
    {
        [Test]
        public void UpdateUser_ReturnsTask_NotVoid()
        {
            var m = typeof(UserHelper).GetMethod(nameof(UserHelper.UpdateUser));
            Assert.IsNotNull(m);
            Assert.AreEqual(typeof(Task), m.ReturnType);
        }

        [Test]
        public void UpdateUsersBatch_ReturnsTask_NotVoid()
        {
            var m = typeof(UserHelper).GetMethod(nameof(UserHelper.UpdateUsersBatch));
            Assert.IsNotNull(m);
            Assert.AreEqual(typeof(Task), m.ReturnType);
        }
    }
}