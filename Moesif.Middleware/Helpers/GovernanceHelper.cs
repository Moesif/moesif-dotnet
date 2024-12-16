//#define MOESIF_INSTRUMENT

using System;
using Moesif.Api;
using Moesif.Api.Models;
using Moesif.Middleware.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System.Data;
using Rule = Moesif.Middleware.Models.Rule;
#if NET6_0_OR_GREATER
    using System.Text.Json;
    using System.Text.Json.Nodes;
#else
    using Newtonsoft.Json.Linq;
#endif

namespace Moesif.Middleware.Helpers
{
    public class GovernanceHelper
    {
        public static async Task<Governance> updateGovernance(MoesifApiClient client, Governance prevGovernance, bool debug, ILogger logger)
        {
            try
            {
#if MOESIF_INSTRUMENT
              logger.LogError($"Called: updateGovernance");
#endif
                var resp = await client.Api.GetGovernanceRuleAsync();
                var etag = resp.Headers.ToDictionary(k => k.Key.ToLower(), k => k.Value)["x-moesif-rules-tag"];
                if (etag != prevGovernance.etag)
                {
                    var governance = Governance.fromJson(resp.Body);
                    governance.etag = etag;
                    logger.LogDebug("governance rule updated with {body} at {time} ", resp.Body, governance.lastUpdatedTime);
                    return governance;
                }
                return prevGovernance;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error getting GovernanceRule");
                return prevGovernance;
            }
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static bool isGovernaceRuleDefined(Governance governace)
        {
            return governace.rules != null && governace.rules.Count > 0 ;
        }

        public static bool enforceGovernaceRule(EventModel eventModel, Governance governace, AppConfig config)
        {
            if (governace.rules == null)
                return false;
            else
            {
                var requestMap = RequestMapHelper.createRequestMap(eventModel);
                var matchingRules = new List<(Rule, GovernanceRule)>();
                var matchingUser = findMatchingEntityRule(eventModel.UserId, "user", governace, config, requestMap);
                matchingRules.AddRange(matchingUser);


                var matchingCompany = findMatchingEntityRule(eventModel.CompanyId, "company", governace, config, requestMap);
                matchingRules.AddRange(matchingCompany);


                var regexRules = new List<GovernanceRule>();
                var matchingRegexRules = new List<(Rule, GovernanceRule)>();
                foreach (GovernanceRule r in governace.rules)
                {
                    if (r.type == "regex")
                    {
                        regexRules.Add(r);
                    }
                }
                foreach (GovernanceRule r in regexRules)
                {
                    if (isRegexMatch(r, requestMap))
                    {
                        matchingRegexRules.Add((null, r));
                    }
                }
                matchingRules.AddRange(matchingRegexRules);
                if(matchingRules.Count > 0)
                {
                    updateEventModel(eventModel, matchingRules);
                    return true;
                }
                return false;
            }
        }

        static void updateEventModel(EventModel eventModel, List<(Rule, GovernanceRule)> matchingRules)
        {
            var (r, govRule) = matchingRules.First();
            var response = govRule.response;
            var body = applyMergeTagtoBody(r, response.body.ToString());
            var headers = new Dictionary<string, string>();
            matchingRules.Reverse();
            foreach (var (rule, gRule) in matchingRules)
            {
                foreach (var header in applyMergeTagtoHeaders(rule, gRule.response.headers))
                {
                    if (headers.ContainsKey(header.Key))
                    {
                        headers[header.Key] = header.Value;
                    }
                    else
                    {
                        headers.Add(header.Key, header.Value);
                    }
                }
            }
            response.body = body;
            response.headers = headers;
            updateEventModel(eventModel, response, govRule._id);
        }

        static string applyMergeTagtoBody(Rule rule, string body)
        {
            var variables = rule?.values?.ToDictionary(kv => $"{{{{{kv.Key}}}}}", kv => kv.Value);
            variables ??= new Dictionary<string, string>();
            foreach (var v in variables)
            {
                body = body.Replace(v.Key, v.Value);
            }
            return body;

        }

        static Dictionary<string, string> applyMergeTagtoHeaders(Rule rule, Dictionary<string, string> headers)
        {
            var variables = rule?.values?.ToDictionary(kv => $"{{{{{kv.Key}}}}}", kv => kv.Value);
            //var variables = rule == null || rule.values == null ? new Dictionary<string, string>()  : rule.values.ToDictionary(kv => $"{{{{{kv.Key}}}}}", kv => kv.Value);
            variables ??= new Dictionary<string, string>();
            return headers.ToDictionary(kv => kv.Key, kv =>
            {
                var value = kv.Value;
                foreach (var v in variables)
                {
                    value = value.Replace(v.Key, v.Value);
                }
                return value;
            });
        }


        static void updateEventModel(EventModel eventModel, Response response, string ruleId)
        {
            eventModel.BlockedBy = ruleId;
            string encoding;
            object body;
            try
            {
                body = ApiHelper.JsonDeserialize<object>(response.body.ToString());
                encoding = "json";

            }
            catch (Exception)
            {
                body = Base64Encode(response.body.ToString());
                encoding = "base64";
            }
            eventModel.Response = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = response.status,
                Headers = response.headers,
                Body = body,
                TransferEncoding = encoding,
            };
        }

        static Boolean isRegexMatch(GovernanceRule govRule, RequestMap requestMap)
        {
            if (govRule.regex_config == null || govRule.regex_config.Count == 0)
                return true;

            foreach (RegexConfig rc in govRule.regex_config)
            {
                var matched = false;
                if (rc.conditions != null)
                {
                    foreach (Condition c in rc.conditions)
                    {
                        if (c.path.StartsWith("request.body.") && requestMap.regex_mapping.ContainsKey("request.body"))
                        {
#if NET6_0_OR_GREATER
                            var body = (JsonObject)requestMap.regex_mapping["request.body"];
#else
                            var body = (Newtonsoft.Json.Linq.JObject)requestMap.regex_mapping["request.body"];
#endif
                            var fieldName = c.path.Split('.')[2];
#if NET6_0_OR_GREATER
                            var fieldlValue = body[fieldName];
#else
                            var fieldlValue = body.GetValue(fieldName);
#endif
                            if (fieldlValue != null)
                            {
                                Match m = Regex.Match(fieldlValue.ToString(), c.value, RegexOptions.IgnoreCase);
                                if (!m.Success)
                                {
                                    return false;
                                }
                                else
                                {
                                    matched = true;
                                }
                            }
                            else
                            {
                                return false;
                            }

                        }
                        else if (requestMap.regex_mapping.ContainsKey(c.path))
                        {
                            Match m = Regex.Match((string)requestMap.regex_mapping[c.path], c.value, RegexOptions.IgnoreCase);
                            if (!m.Success)
                            {
                                return false;
                            }
                            else
                            {
                                matched = true;
                            }
                        }
                        else
                        {
                            return false;
                        }

                    }
                }
                if (matched) return true;
            }
            return false;

        }


        static List<(Rule, GovernanceRule)> findMatchingEntityRule(string entity, string type, Governance governace, AppConfig config, RequestMap requestMap)
        {
            var matching = new List<(Rule, GovernanceRule)>();
            if (entity == null)
            {
                if (governace.rules != null)
                {
                    foreach (GovernanceRule govRule in governace.rules)
                    {
                        if (govRule.block && govRule.applied_to_unidentified && govRule.type == type )
                        {
                            if (isRegexMatch(govRule, requestMap))
                                matching.Add((null, govRule));
                        }
                    }
                }
                return matching;
            }
            Dictionary<string, List<Rule>> entityRules;
            if (type == "user")
                entityRules = config.user_rules;
            else if (type == "company")
                entityRules = config.company_rules;
            else
                entityRules = null;

            List<Rule> rules = new List<Rule>();

            if (entityRules != null)
            {
                if (entityRules.ContainsKey(entity ?? ""))
                {
                    rules.AddRange(entityRules[entity]);
                    foreach (Rule rule in rules)
                    {
                        if (governace.rules != null)
                        {
                            foreach (GovernanceRule govRule in governace.rules)
                            {
                                if (govRule.block && rule.rules == govRule._id && (govRule.applied_to == null || govRule.applied_to == "matching"))
                                {
                                    if(isRegexMatch(govRule, requestMap))
                                      matching.Add((rule, govRule));
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (governace.rules != null)
                    {
                        foreach (GovernanceRule govRule in governace.rules)
                        {
                            if (govRule.block && govRule.type == type && (govRule.applied_to != null && govRule.applied_to == "not_matching"))
                            {
                                if(isRegexMatch(govRule, requestMap))
                                    matching.Add((null, govRule));
                            }
                        }
                    }
                }

            }
            return matching;
        }

    }
}

