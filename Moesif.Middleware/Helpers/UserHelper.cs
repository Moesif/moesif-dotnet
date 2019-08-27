using System;
using System.Linq;
using System.Collections.Generic;
using Moesif.Api;
using Moesif.Api.Models;
using Moesif.Api.Exceptions;

namespace Moesif.Middleware.Helpers
{
    public class UserHelper
    {
        // Create UserModel
        public UserModel createUserModel(Dictionary<string, object> user)
        {
            var userAgentString = new object();
            var companyId = new object();
            var userMetadata = new object();
            var userSessionToken = new object();
            var modifiedTime = new object();
            var ipAddress = new object();
            var userModel = new UserModel()
            {
                UserId = user["user_id"].ToString(),
                CompanyId = user.TryGetValue("company_id", out companyId) ? companyId.ToString() : null,
                UserAgentString = user.TryGetValue("user_agent_string", out userAgentString) ? userAgentString.ToString() : null,
                Metadata = user.TryGetValue("metadata", out userMetadata) ? userMetadata : null,
                SessionToken = user.TryGetValue("session_token", out userSessionToken) ? userSessionToken.ToString() : null,
                ModifiedTime = user.TryGetValue("modified_time", out modifiedTime) ? (DateTime)modifiedTime : DateTime.UtcNow,
                IpAddress = user.TryGetValue("ip_address", out ipAddress) ? ipAddress.ToString() : null
            };
            return userModel;
        }

        // Function to update user
        public async void UpdateUser(MoesifApiClient client, Dictionary<string, object> userProfile, bool debug)
        {

            if (!userProfile.Any())
            {
                Console.WriteLine("Expecting the input to be of the type - dictionary while updating user");
            }
            else
            {
                if (userProfile.ContainsKey("user_id"))
                {

                    UserModel userModel = createUserModel(userProfile);

                    // Perform API call
                    try
                    {
                        await client.Api.UpdateUserAsync(userModel);
                        if (debug)
                        {
                            Console.WriteLine("User Updated Successfully");
                        }
                    }
                    catch (APIException inst)
                    {
                        if (401 <= inst.ResponseCode && inst.ResponseCode <= 403)
                        {
                            Console.WriteLine("Unauthorized access updating user to Moesif. Please check your Appplication Id.");
                        }
                        if (debug)
                        {
                            Console.WriteLine("Error updating user to Moesif, with status code:");
                            Console.WriteLine(inst.ResponseCode);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("To update an user, an user_id field is required");
                }
            }
        }

        // Function to update users in batch
        public async void UpdateUsersBatch(MoesifApiClient client, List<Dictionary<string, object>> userProfiles, bool debug)
        {
            if (!userProfiles.Any())
            {
                Console.WriteLine("Expecting the input to be of the type - List of dictionary while updating users in batch");
            }
            else
            {
                List<UserModel> userModels = new List<UserModel>();
                foreach (Dictionary<string, object> user in userProfiles)
                {
                    if (user.ContainsKey("user_id"))
                    {
                        UserModel userModel = createUserModel(user);
                        userModels.Add(userModel);
                    }
                    else
                    {
                        Console.WriteLine("To update an user, an user_id field is required");
                    }
                }

                // Perform API call
                try
                {
                    await client.Api.UpdateUsersBatchAsync(userModels);
                    if (debug)
                    {
                        Console.WriteLine("Users Updated Successfully");
                    }
                }
                catch (APIException inst)
                {
                    if (401 <= inst.ResponseCode && inst.ResponseCode <= 403)
                    {
                        Console.WriteLine("Unauthorized access updating users to Moesif. Please check your Appplication Id.");
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
