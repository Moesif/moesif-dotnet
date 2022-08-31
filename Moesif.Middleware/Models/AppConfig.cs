using System;
using System.Collections.Generic;
using UserId = System.String;
using CompanyId = System.String;
using Name = System.String;
using IpAddress = System.String;
using SampleRate = System.Int32;
using Newtonsoft.Json;

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

        public void copy(AppConfig config)
        {
            lock (this)
            {
                block_bot_traffic = config.block_bot_traffic;
                company_rules = config.company_rules;
                company_sample_rate = config.company_sample_rate;
                ip_addresses_blocked_by_name = config.ip_addresses_blocked_by_name;
                regex_config = config.regex_config;
                sample_rate = config.sample_rate;
                user_rules = config.user_rules;
                user_sample_rate = config.user_sample_rate;
                etag = config.etag;
                lastUpdatedTime = config.lastUpdatedTime;
            }
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

