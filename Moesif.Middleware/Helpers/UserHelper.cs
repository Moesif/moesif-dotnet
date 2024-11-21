using System;
using System.Linq;
using System.Collections.Generic;
using Moesif.Api;
using Moesif.Api.Models;
using Moesif.Api.Exceptions;
using System.Text;
// using System.IdentityModel.Tokens;
// using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;
    
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
            var campaign = new object();
            var campaignHelper = new CampaignHelper();
            var userModel = new UserModel()
            {
                UserId = user["user_id"].ToString(),
                CompanyId = user.TryGetValue("company_id", out companyId) ? companyId.ToString() : null,
                UserAgentString = user.TryGetValue("user_agent_string", out userAgentString) ? userAgentString.ToString() : null,
                Metadata = user.TryGetValue("metadata", out userMetadata) ? userMetadata : null,
                SessionToken = user.TryGetValue("session_token", out userSessionToken) ? userSessionToken.ToString() : null,
                ModifiedTime = user.TryGetValue("modified_time", out modifiedTime) ? (DateTime)modifiedTime : DateTime.UtcNow,
                IpAddress = user.TryGetValue("ip_address", out ipAddress) ? ipAddress.ToString() : null,
                Campaign = user.TryGetValue("campaign", out campaign) ? campaignHelper.createCampaignModel(ApiHelper.JsonDeserialize<Dictionary<string, object>>(ApiHelper.JsonSerialize(campaign))) : null
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

        public static string parseAuthorizationHeader(string token, string field)
        {
            try
            {
                // var newToken = new JwtSecurityToken(jwtEncodedString: token);
                // Initialize the token handler
                var tokenHandler = new JsonWebTokenHandler();
                // Read the token (this does not validate it)
                var newToken = tokenHandler.ReadToken(token) as JsonWebToken;
                if (newToken == null)
                {
                    return null;
                }
                
                bool hasClaim = newToken.Claims.Any(c => c.Type == field);
                return hasClaim ? newToken.Claims.First(c => c.Type == field).Value : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string fetchUserFromAuthorizationHeader(Dictionary<string, string> headers, string authorizationHeaderName, string authorizationUserIdField)
        {
            string userId = null;
            try
            {
                if (!string.IsNullOrEmpty(authorizationHeaderName) && !string.IsNullOrEmpty(authorizationUserIdField))
                {
                    string[] authHeaderNames = authorizationHeaderName.ToLower().Replace(" ", "").Split(',');
                    string token = null;

                    headers = headers.ToDictionary(k => k.Key.ToLower(), k => k.Value);

                    foreach (var authName in authHeaderNames)
                    {
                        if (headers.ContainsKey(authName))
                        {
                            token = headers[authName].Split(',')[0];
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(token))
                    {
                        if (token.Contains("Bearer"))
                        {
                            token = token.Substring("Bearer ".Length).Trim();
                            userId = parseAuthorizationHeader(token, authorizationUserIdField);
                        }
                        else if (token.Contains("Basic"))
                        {
                            token = token.Substring("Basic ".Length).Trim();
                            string decoded_token = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                            int seperatorIndex = decoded_token.IndexOf(':');
                            userId = decoded_token.Substring(0, seperatorIndex);
                        }
                        else
                        {
                            userId = parseAuthorizationHeader(token, authorizationUserIdField);
                        }
                    }
                    else
                    {
                        userId = null;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
            return userId;
        }
    }
}
