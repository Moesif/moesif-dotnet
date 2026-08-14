using System.Collections.Generic;
using Moesif.Api.Models;
using Moesif.Middleware.Helpers;
using Moesif.Middleware.Models;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Moesif.NetFramework.Test
{
    /// <summary>
    /// Tests for the NET461 branch of <see cref="GovernanceHelper"/>.
    ///
    /// Historically the rule evaluator did an unchecked <c>(JObject)</c> cast on
    /// <c>request.body</c>, which threw for non-object bodies. Fixed by using a
    /// safe <c>as JObject</c> cast with null-guard.
    /// </summary>
    [TestFixture]
    public class GovernanceHelperTests
    {
        [Test]
        public void EnforceGovernance_NoRulesDefined_ReturnsFalseWithoutThrowing()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(new JArray());
            eventModel.Response = new EventResponseModel { Status = 400 };
            var governance = Governance.getDefaultGovernance();
            var config = AppConfig.getDefaultAppConfig();

            bool blocked = false;
            Assert.DoesNotThrow(() => blocked = GovernanceHelper.enforceGovernaceRule(eventModel, governance, config));
            Assert.IsFalse(blocked);
        }

        [Test]
        public void EnforceGovernance_RuleTargetsRequestBody_ArrayBody_DoesNotThrow()
        {
            var eventModel = TestEventModelFactory.NewJsonEvent(new JArray());
            eventModel.Response = new EventResponseModel { Status = 400 };

            var governance = BuildRegexRuleGovernance(path: "request.body.query", pattern: ".*");
            var config = AppConfig.getDefaultAppConfig();

            bool blocked = true;
            Assert.DoesNotThrow(() => blocked = GovernanceHelper.enforceGovernaceRule(eventModel, governance, config));
            Assert.IsFalse(blocked);
        }

        [Test]
        public void EnforceGovernance_RuleTargetsRequestBody_ObjectBody_MatchesRule()
        {
            var body = new JObject { { "query", "{ users { id } }" } };
            var eventModel = TestEventModelFactory.NewJsonEvent(body);
            eventModel.Response = new EventResponseModel { Status = 200 };

            var governance = BuildRegexRuleGovernance(path: "request.body.query", pattern: "users");
            var config = AppConfig.getDefaultAppConfig();

            bool blocked = false;
            Assert.DoesNotThrow(() => blocked = GovernanceHelper.enforceGovernaceRule(eventModel, governance, config));
            Assert.IsTrue(blocked);
        }

        private static Governance BuildRegexRuleGovernance(string path, string pattern)
        {
            return new Governance
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
                        response = new Response
                        {
                            status = 403,
                            headers = new Dictionary<string, string>(),
                            body = "blocked"
                        },
                        regex_config = new List<RegexConfig>
                        {
                            new RegexConfig
                            {
                                conditions = new List<Condition>
                                {
                                    new Condition { path = path, value = pattern }
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}