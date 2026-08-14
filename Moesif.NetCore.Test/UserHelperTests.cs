using System.Threading.Tasks;
using Moesif.Middleware.Helpers;
using Xunit;
using Assert = NUnit.Framework.Assert;

namespace Moesif.NetCore.Test
{
    /// <summary>
    /// Tests for <see cref="UserHelper"/>.
    ///
    /// Historically <c>UpdateUser</c> and <c>UpdateUsersBatch</c> were declared
    /// <c>async void</c>. Any exception that escaped their inner catch would
    /// crash the host process because <c>async void</c> exceptions aren't
    /// captured on a Task and get raised on the current SynchronizationContext.
    /// Fixed by returning <c>Task</c> instead.
    /// </summary>
    public class UserHelperTests
    {
        [Fact]
        public void UpdateUser_ReturnsTask_NotVoid()
        {
            var m = typeof(UserHelper).GetMethod(nameof(UserHelper.UpdateUser));
            Assert.IsNotNull(m);
            Assert.AreEqual(typeof(Task), m.ReturnType);
        }

        [Fact]
        public void UpdateUsersBatch_ReturnsTask_NotVoid()
        {
            var m = typeof(UserHelper).GetMethod(nameof(UserHelper.UpdateUsersBatch));
            Assert.IsNotNull(m);
            Assert.AreEqual(typeof(Task), m.ReturnType);
        }
    }
}