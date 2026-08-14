using System.Threading.Tasks;
using Moesif.Middleware.Helpers;
using Xunit;
using Assert = NUnit.Framework.Assert;

namespace Moesif.NetCore.Test
{
    /// <summary>
    /// Tests for <see cref="CompanyHelper"/>.
    ///
    /// Historically <c>UpdateCompany</c> and <c>UpdateCompaniesBatch</c> were
    /// declared <c>async void</c>. Any exception that escaped their inner catch
    /// would crash the host process. Fixed by returning <c>Task</c> instead.
    /// </summary>
    public class CompanyHelperTests
    {
        [Fact]
        public void UpdateCompany_ReturnsTask_NotVoid()
        {
            var m = typeof(CompanyHelper).GetMethod(nameof(CompanyHelper.UpdateCompany));
            Assert.IsNotNull(m);
            Assert.AreEqual(typeof(Task), m.ReturnType);
        }

        [Fact]
        public void UpdateCompaniesBatch_ReturnsTask_NotVoid()
        {
            var m = typeof(CompanyHelper).GetMethod(nameof(CompanyHelper.UpdateCompaniesBatch));
            Assert.IsNotNull(m);
            Assert.AreEqual(typeof(Task), m.ReturnType);
        }
    }
}