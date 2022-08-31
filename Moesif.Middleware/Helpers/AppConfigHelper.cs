using System;
using System.Linq;
using System.Collections.Generic;
using Moesif.Api;
using Moesif.Api.Exceptions;
using System.Threading.Tasks;
using Moesif.Middleware.Models;
using System.Text.RegularExpressions;
using System.Reflection;


namespace Moesif.Middleware.Helpers
{
    public class AppConfigHelper
    {
        public static AppConfig deepCopy(AppConfig config)
        {
            var json = ApiHelper.JsonSerialize(config);
            var appConfig = ApiHelper.JsonDeserialize<AppConfig>(json);
            appConfig.etag = config.etag;
            appConfig.lastUpdatedTime = config.lastUpdatedTime;
            return appConfig;
        }
       
        public static async Task updateConfig(MoesifApiClient client, AppConfig config, bool debug)
        {

            try
            {
                var appConfigResp = await client.Api.GetAppConfigAsync();
                var etag = appConfigResp.Headers.ToDictionary(k => k.Key.ToLower(), k => k.Value)["x-moesif-config-etag"];
                if (etag != config.etag)
                {
                    var appConfig = ApiHelper.JsonDeserialize<AppConfig>(appConfigResp.Body);
                    appConfig.etag = etag;
                    appConfig.lastUpdatedTime = DateTime.UtcNow;
                    config.copy(appConfig);
                }

            }
            catch (APIException inst)
            {
                if (401 <= inst.ResponseCode && inst.ResponseCode <= 403)
                {
                    Console.WriteLine("Unauthorized access getting application configuration. Please check your Appplication Id.");
                }
                if (debug)
                {
                    Console.WriteLine("Error getting application configuration, with status code:");
                    Console.WriteLine(inst.ResponseCode);
                }
            }
            catch (Exception e)
            {
                if (debug)
                {
                    Console.WriteLine("Error while parsing the configuration object, setting the sample rate to default. " + e.StackTrace);
                }
            }
            
        }


        public static int calculateWeight(int sampleRate)
        {
            return (int)(sampleRate == 0 ? 1 : Math.Floor(100.00 / sampleRate));
        }

        public static int getSamplingPercentage(AppConfig config, RequestMap requestMap)
        {
            AppConfig local;

            lock (config)
            {
                local = deepCopy(config);
            }

            if (local.regex_config != null)
            {
                var rates = new List<int>();
                foreach (RegexConfig rc in local.regex_config)
                {
                    var matched = true;
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
                    }
                    if (matched)
                    {
                        rates.Add(rc.sample_rate);
                    }
                }
                if (rates.Count > 0)
                {
                    return rates.Min();
                }
            }

            if (requestMap.companyId != null && local.company_sample_rate != null)
            {
                if (local.company_sample_rate.TryGetValue(requestMap.companyId, out int v))
                {
                    return v;
                }
            }
            if (requestMap.userId != null && local.user_sample_rate != null)
            {
                if (local.user_sample_rate.TryGetValue(requestMap.userId, out int v))
                {
                    return v;
                }
            }

            return local.sample_rate;
        }

    }
}
