using System.Collections.Generic;
using Moesif.Api.Models;
using Moesif.Middleware.Helpers;
using Moesif.Middleware.Models;
using Xunit;
using Assert = NUnit.Framework.Assert;

namespace Moesif.NetCore.Test
{
    /// <summary>
    /// Tests for <see cref="GovernanceHelper"/> — evaluates governance rules
    /// against the incoming request/response.
    ///
    /// Historically the rule evaluator used unchecked <c>(JsonObject)</c> /
    /// <c>(JObject)</c> casts on <c>request.body</c>, which threw for any
    /// non-object body. Fixed by using safe <c>as</c> casts with null-guards so
    /// non-object bodies simply skip the rule instead of blowing up.
    /// </summary>
    public class GovernanceHelperTests
    {
        [Fact]
        public void EnforceGovernance_NoRulesDefined_ReturnsFalseWithoutThrowing()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(body: new List<object>());
            eventModel.Response = new EventResponseModel { Status = 400 };
            var governance = Governance.getDefaultGovernance();
            var config = AppConfig.getDefaultAppConfig();

            bool blocked = false;
            Assert.DoesNotThrow(() => blocked = GovernanceHelper.enforceGovernaceRule(eventModel, governance, config));
            Assert.IsFalse(blocked);
        }

        /// <summary>
        /// Governance rule targeting a "request.body.*" path is evaluated over an
        /// array-shaped request body. Before the fix this would throw an
        /// InvalidCastException from the unchecked (JsonObject) cast. After the
        /// fix the rule simply doesn't match (safe cast returns null, path
        /// skipped) and the request is not blocked.
        /// </summary>
        [Fact]
        public void EnforceGovernance_RuleTargetsRequestBody_ArrayBody_DoesNotThrow()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(body: new List<object>());
            eventModel.Response = new EventResponseModel { Status = 400 };

            var governance = new Governance
            {
                rules = new List<GovernanceRule>
                {
                    new GovernanceRule
                    {
                        _id = "rule-1",
                        type = "regex",
                        block = true,
                        applied_to_unidentified = true,
                        applied_to = "matching",
                        response = new Response { status = 403, headers = new Dictionary<string, string>(), body = "blocked" },
                        regex_config = new List<RegexConfig>
                        {
                            new RegexConfig
                            {
                                conditions = new List<Condition>
                                {
                                    new Condition { path = "request.body.query", value = ".*" }
                                }
                            }
                        }
                    }
                }
            };
            var config = AppConfig.getDefaultAppConfig();

            bool blocked = true;
            Assert.DoesNotThrow(() => blocked = GovernanceHelper.enforceGovernaceRule(eventModel, governance, config));
            Assert.IsFalse(blocked);
        }

        /// <summary>
        /// Same rule shape but with a valid GraphQL-style object body. Confirms
        /// the safe-cast fix hasn't broken the happy path — a matching object
        /// body still causes the rule to fire and block the request.
        /// </summary>
        [Fact]
        public void EnforceGovernance_RuleTargetsRequestBody_ObjectBody_MatchesRule()
        {
            var body = new Dictionary<string, object> { { "query", "{ users { id } }" } };
            var eventModel = TestEventModelFactory.NewJsonEvent(body);
            eventModel.Response = new EventResponseModel { Status = 200 };

            var governance = new Governance
            {
                rules = new List<GovernanceRule>
                {
                    new GovernanceRule
                    {
                        _id = "rule-1",
                        type = "regex",
                        block = true,
                        applied_to_unidentified = true,
                        applied_to = "matching",
                        response = new Response { status = 403, headers = new Dictionary<string, string>(), body = "blocked" },
                        regex_config = new List<RegexConfig>
                        {
                            new RegexConfig
                            {
                                conditions = new List<Condition>
                                {
                                    new Condition { path = "request.body.query", value = "users" }
                                }
                            }
                        }
                    }
                }
            };
            var config = AppConfig.getDefaultAppConfig();

            bool blocked = false;
            Assert.DoesNotThrow(() => blocked = GovernanceHelper.enforceGovernaceRule(eventModel, governance, config));
            Assert.IsTrue(blocked);
        }
    }
}