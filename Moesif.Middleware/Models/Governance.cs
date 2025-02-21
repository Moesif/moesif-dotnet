﻿using System;
using System.Collections.Generic;
using Moesif.Api;
// using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Moesif.Middleware.Models
{
    public class Governance
    {
        public static Governance getDefaultGovernance()
        {
            return new Governance
            {
                lastUpdatedTime = DateTime.UtcNow,
            };
        }

        public static Governance fromJson(string json)
        {
            var governance = new Governance();
            governance.rules = ApiHelper.JsonDeserialize<List<GovernanceRule>>(json);
            governance.lastUpdatedTime = DateTime.UtcNow;
            return governance;
        }

        public List<GovernanceRule> rules { get; set; }
        public DateTime lastUpdatedTime { get; set; }
        public string etag { get; set; }
    }

    public class GovernanceRule
    {
        public string _id { get; set; }
        public string type { get; set; }
        public List<RegexConfig> regex_config { get; set; }
        public List<Variable> variables { get; set; }
        public Response response { get; set; }
        public bool applied_to_unidentified { get; set; }
        public string applied_to { get; set; }
        public bool block { get; set; }

    }

    public class Variable
    {
        public string name { get; set; }
        public string path { get; set; }
    }

    public class Response
    {
        public int status { get; set; }
        public Dictionary<string, string> headers { get; set; }
        public object body { get; set; }
    }
}

