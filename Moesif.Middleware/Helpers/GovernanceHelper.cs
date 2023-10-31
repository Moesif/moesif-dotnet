using System;
using Moesif.Api;
using Moesif.Api.Models;
using Moesif.Middleware.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

namespace Moesif.Middleware.Helpers
{
    public class GovernanceHelper
    {
        public static async Task<Governance> updateGovernance(MoesifApiClient client, Governance prevGovernance, bool debug, ILogger logger)
        {
            try
            {
                var resp = await client.Api.GetGovernanceRuleAsync();
                var etag = resp.Headers.ToDictionary(k => k.Key.ToLower(), k => k.Value)["x-moesif-rules-tag"];
                if (etag != prevGovernance.etag)
                {
                    var governance = Governance.fromJson(resp.Body);
                    governance.etag = etag;
                    logger.LogDebug("governance rule updated with {body} ", resp.Body);
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
                var matchingUser = findMatchingEntityRule(eventModel.UserId, "user", governace, config);
                foreach ((Rule rule, GovernanceRule governanceRule) t in matchingUser)
                {
                    // return the first match
                    if (isRegexMatch(t.governanceRule, requestMap))
                    {
                        updateEventModel(eventModel, t.rule, t.governanceRule);
                        return true;
                    }
                }
                var matchingCompany = findMatchingEntityRule(eventModel.CompanyId, "company", governace, config);
                foreach ((Rule rule, GovernanceRule governanceRule) t in matchingCompany)
                {
                    // return the first match
                    if (isRegexMatch(t.governanceRule, requestMap))
                    {
                        updateEventModel(eventModel, t.rule, t.governanceRule);
                        return true;
                    }
                }

                var regexRules = new List<GovernanceRule>();
                foreach (GovernanceRule r in governace.rules)
                {
                    if (r.type == "regex")
                    {
                        regexRules.Add(r);
                    }
                }
                foreach (GovernanceRule r in regexRules)
                {
                    // return the first match
                    if (isRegexMatch(r, requestMap))
                    {
                        updateEventModel(eventModel, r.response, r._id);
                        return true;
                    }
                }
                return false;
            }
        }

        static void updateEventModel(EventModel eventModel, Rule rule, GovernanceRule governanceRule)
        {
            var response = governanceRule.response;
            var headers = response.headers;
            headers ??= new Dictionary<string, string>();
            var variables = rule.values?.ToDictionary(kv => $"{{{{{kv.Key}}}}}", kv => kv.Value);
            variables ??= new Dictionary<string, string>();
            response.headers = headers.ToDictionary(kv => kv.Key, kv =>
            {
                var value = kv.Value;
                foreach (var v in variables)
                {
                    value = value.Replace(v.Key, v.Value);
                }
                return value;
            });

            var body = response.body.ToString();
            foreach (var v in variables)
            {
                body = body.Replace(v.Key, v.Value);
            }
            response.body = body;

            updateEventModel(eventModel, response, governanceRule._id);

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
            if (govRule.regex_config != null && govRule.regex_config.Count > 0)
            {
                foreach (RegexConfig rc in govRule.regex_config)
                {
                    var matched = true;
                    if (rc.conditions != null)
                    {
                        foreach (Condition c in rc.conditions)
                        {
                            if (requestMap.regex_mapping.ContainsKey(c.path))
                            {
                                Match m = Regex.Match(requestMap.regex_mapping[c.path], c.value, RegexOptions.IgnoreCase);
                                if (!m.Success)
                                {
                                    matched = false;
                                    break;
                                }
                            }
                            else
                            {
                                matched = false;
                                break;
                            }

                        }
                    }
                    if (matched) return true;
                }
                return false;

            }
            else
            return true;
        }

        static List<(Rule, GovernanceRule)> findMatchingEntityRule(string entity, string type, Governance governace, AppConfig config)
        {
            var matching = new List<(Rule, GovernanceRule)>();
            if (entity == null) return matching;
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
                }
            }

            foreach (Rule rule in rules)
            {
                if (governace.rules != null)
                {
                    foreach (GovernanceRule govRule in governace.rules)
                    {
                        if (rule.rules == govRule._id)
                        {
                            matching.Add((rule, govRule));
                        }
                    }
                }
            }
            return matching;

        }
    }
}

