using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#if NETCORE
using Microsoft.AspNetCore.Http;
using Moesif.Middleware.NetCore;
#endif

#if NET461
using Microsoft.Owin;
using Moesif.Middleware.NetFramework;
#endif

#if NETCORE
namespace Moesif.Middleware
{
    public class MoesifMiddleware
    {
        MoesifMiddlewareNetCore netCoreMoesifMiddleware;

        public MoesifMiddleware(RequestDelegate next, Dictionary<string, object> _middleware)
        {
            netCoreMoesifMiddleware = new MoesifMiddlewareNetCore(next, _middleware);
        }

        public MoesifMiddleware(Dictionary<string, object> _middleware) => netCoreMoesifMiddleware = new MoesifMiddlewareNetCore(_middleware);

        public async Task Invoke(HttpContext httpContext) {
            await netCoreMoesifMiddleware.Invoke(httpContext);
        }

        // Function to update user
        public void UpdateUser(Dictionary<string, object> userProfile)
        {
            netCoreMoesifMiddleware.UpdateUser(userProfile);
        }

        // Function to update users in batch
        public void UpdateUsersBatch(List<Dictionary<string, object>> userProfiles)
        {
            netCoreMoesifMiddleware.UpdateUsersBatch(userProfiles);
        }

        // Function to update company
        public void UpdateCompany(Dictionary<string, object> companyProfile)
        {
            netCoreMoesifMiddleware.UpdateCompany(companyProfile);
        }

        // Function to update companies in batch
        public void UpdateCompaniesBatch(List<Dictionary<string, object>> companyProfiles)
        {
            netCoreMoesifMiddleware.UpdateCompaniesBatch(companyProfiles);
        }
    }
}
#endif

#if NET461
namespace Moesif.Middleware
{
    public class MoesifMiddleware: OwinMiddleware
    {
        MoesifMiddlewareNetFramework netFrameworkMoesifMiddleware;

        public MoesifMiddleware(OwinMiddleware next, Dictionary<string, object> _middleware) : base(next)
        {
            netFrameworkMoesifMiddleware = new MoesifMiddlewareNetFramework(next, _middleware);
        }

        public async override Task Invoke(IOwinContext httpContext)
        {
            await netFrameworkMoesifMiddleware.Invoke(httpContext);
        }

        // Function to update user
        public void UpdateUser(Dictionary<string, object> userProfile)
        {
            netFrameworkMoesifMiddleware.UpdateUser(userProfile);
        }

        // Function to update users in batch
        public void UpdateUsersBatch(List<Dictionary<string, object>> userProfiles)
        {
            netFrameworkMoesifMiddleware.UpdateUsersBatch(userProfiles);
        }

        // Function to update company
        public void UpdateCompany(Dictionary<string, object> companyProfile)
        {
            netFrameworkMoesifMiddleware.UpdateCompany(companyProfile);
        }

        // Function to update companies in batch
        public void UpdateCompaniesBatch(List<Dictionary<string, object>> companyProfiles)
        {
            netFrameworkMoesifMiddleware.UpdateCompaniesBatch(companyProfiles);
        }
    }
}
#endif
