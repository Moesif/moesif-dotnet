using System;
using System.Collections.Generic;
using UserId = System.String;
using CompanyId = System.String;
using Name = System.String;
using IpAddress = System.String;
using SampleRate = System.Int32;
// using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Moesif.Middleware.Models
{
    public class AppConfig
    {
        public static AppConfig getDefaultAppConfig()
        {
            return new AppConfig()
            {
                sample_rate = 100,
                lastUpdatedTime = DateTime.UtcNow,
            };
        }

        // start json fields
        public bool block_bot_traffic { get; set; }
        public SampleRate sample_rate { get; set; }
        public Dictionary<CompanyId, SampleRate> company_sample_rate { get; set; }
        public Dictionary<UserId, SampleRate> user_sample_rate { get; set; }
        public List<RegexConfig> regex_config { get; set; }
        public Dictionary<Name, IpAddress> ip_addresses_blocked_by_name { get; set; }
        public Dictionary<UserId, List<Rule>> user_rules { get; set; }
        public Dictionary<CompanyId, List<Rule>> company_rules { get; set; }
        // end json fields

        // house keeping stuff
        [JsonIgnore]
        public string etag { get; set; }
        [JsonIgnore]
        public DateTime lastUpdatedTime { get; set; }

    }

    public class RegexConfig
    {
        public List<Condition> conditions { get; set; }
        public SampleRate sample_rate { get; set; }

    }

    public class Condition
    {
        public string path { get; set; }
        public string value { get; set; }
    }

    public class Rule
    {
        public string rules { get; set; }
        public Dictionary<string, string> values { get; set; }
    }

}

