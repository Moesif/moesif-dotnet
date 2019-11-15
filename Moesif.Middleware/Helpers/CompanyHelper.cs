using System;
using System.Linq;
using System.Collections.Generic;
using Moesif.Api;
using Moesif.Api.Models;
using Moesif.Api.Exceptions;

namespace Moesif.Middleware.Helpers
{
    public class CompanyHelper
    {
        // Create CompanyModel
        public CompanyModel createCompanyModel(Dictionary<string, object> company)
        {
            var companyDomain = new object();
            var userMetadata = new object();
            var userSessionToken = new object();
            var modifiedTime = new object();
            var ipAddress = new object();
            var campaign = new object();
            var campaignHelper = new CampaignHelper();
            var companyModel = new CompanyModel()
            {
                CompanyId = company["company_id"].ToString(),
                CompanyDomain = company.TryGetValue("company_domain", out companyDomain) ? companyDomain.ToString() : null,
                Metadata = company.TryGetValue("metadata", out userMetadata) ? userMetadata : null,
                SessionToken = company.TryGetValue("session_token", out userSessionToken) ? userSessionToken.ToString() : null,
                ModifiedTime = company.TryGetValue("modified_time", out modifiedTime) ? (DateTime)modifiedTime : DateTime.UtcNow,
                IpAddress = company.TryGetValue("ip_address", out ipAddress) ? ipAddress.ToString() : null,
                Campaign = company.TryGetValue("campaign", out campaign) ? campaignHelper.createCampaignModel(ApiHelper.JsonDeserialize<Dictionary<string, object>>(ApiHelper.JsonSerialize(campaign))) : null
            };
            return companyModel;
        }

        // Function to update company
        public async void UpdateCompany(MoesifApiClient client, Dictionary<string, object> companyProfile, bool debug)
        {

            if (!companyProfile.Any())
            {
                Console.WriteLine("Expecting the input to be of the type - dictionary while updating company");
            }
            else
            {
                if (companyProfile.ContainsKey("company_id"))
                {

                    CompanyModel companyModel = createCompanyModel(companyProfile);

                    // Perform API call
                    try
                    {
                        await client.Api.UpdateCompanyAsync(companyModel);
                        if (debug)
                        {
                            Console.WriteLine("Company Updated Successfully");
                        }
                    }
                    catch (APIException inst)
                    {
                        if (401 <= inst.ResponseCode && inst.ResponseCode <= 403)
                        {
                            Console.WriteLine("Unauthorized access updating company to Moesif. Please check your Appplication Id.");
                        }
                        if (debug)
                        {
                            Console.WriteLine("Error updating company to Moesif, with status code:");
                            Console.WriteLine(inst.ResponseCode);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("To update a comapny, an company_id field is required");
                }
            }
        }

        // Function to update companies in batch
        public async void UpdateCompaniesBatch(MoesifApiClient client, List<Dictionary<string, object>> companyProfiles, bool debug)
        {
            if (!companyProfiles.Any())
            {
                Console.WriteLine("Expecting the input to be of the type - List of dictionary while updating companies in batch");
            }
            else
            {
                List<CompanyModel> companyModels = new List<CompanyModel>();
                foreach (Dictionary<string, object> company in companyProfiles)
                {
                    if (company.ContainsKey("company_id"))
                    {
                        CompanyModel companyModel = createCompanyModel(company);
                        companyModels.Add(companyModel);
                    }
                    else
                    {
                        Console.WriteLine("To update a company, an company_id field is required");
                    }
                }

                // Perform API call
                try
                {
                    await client.Api.UpdateCompaniesBatchAsync(companyModels);
                    if (debug)
                    {
                        Console.WriteLine("Companies Updated Successfully");
                    }
                }
                catch (APIException inst)
                {
                    if (401 <= inst.ResponseCode && inst.ResponseCode <= 403)
                    {
                        Console.WriteLine("Unauthorized access updating companies to Moesif. Please check your Appplication Id.");
                    }
                    if (debug)
                    {
                        Console.WriteLine("Error updating users to Moesif, with status code:");
                        Console.WriteLine(inst.ResponseCode);
                    }
                }
            }
        }
    }
}
