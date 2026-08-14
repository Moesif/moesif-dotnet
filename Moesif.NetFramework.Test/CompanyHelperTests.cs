using System.Threading.Tasks;
using Moesif.Middleware.Helpers;
using NUnit.Framework;

namespace Moesif.NetFramework.Test
{
    /// <summary>
    /// Signature-level tests for the NET461 build of <see cref="CompanyHelper"/>.
    /// See UserHelperTests for the rationale (async void → async Task).
    /// </summary>
    [TestFixture]
    public class CompanyHelperTests
    {
        [Test]
        public void UpdateCompany_ReturnsTask_NotVoid()
        {
            var m = typeof(CompanyHelper).GetMethod(nameof(CompanyHelper.UpdateCompany));
            Assert.IsNotNull(m);
            Assert.AreEqual(typeof(Task), m.ReturnType);
        }

        [Test]
        public void UpdateCompaniesBatch_ReturnsTask_NotVoid()
        {
            var m = typeof(CompanyHelper).GetMethod(nameof(CompanyHelper.UpdateCompaniesBatch));
            Assert.IsNotNull(m);
            Assert.AreEqual(typeof(Task), m.ReturnType);
        }
    }
}