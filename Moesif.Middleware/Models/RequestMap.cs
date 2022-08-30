using System;
using System.Collections.Generic;
using Path = System.String;
using Value = System.String;

namespace Moesif.Middleware.Models
{
    public class RequestMap
    {
        public String companyId { get; set; }
        public String userId { get; set; }
        public Dictionary<Path, Value> regex_mapping;
    }
}

