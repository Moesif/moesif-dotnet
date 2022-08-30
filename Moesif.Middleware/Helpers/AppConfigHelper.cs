﻿using System;
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
        public static async Task<AppConfig> getConfig(MoesifApiClient client, AppConfig prevAppConfig, bool debug)
        {
            AppConfig appConfig = prevAppConfig;

            try
            {
                var appConfigResp = await client.Api.GetAppConfigAsync();
                appConfig = ApiHelper.JsonDeserialize<AppConfig>(appConfigResp.Body);
                appConfig.etag = appConfigResp.Headers.ToDictionary(k => k.Key.ToLower(), k => k.Value)["x-moesif-config-etag"];
                appConfig.lastUpdatedTime = DateTime.UtcNow;

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
            catch (Exception)
            {
                if (debug)
                {
                    Console.WriteLine("Error while parsing the configuration object, setting the sample rate to default.");
                }
            }
            return appConfig;
        }


        public static int calculateWeight(int sampleRate)
        {
            return (int)(sampleRate == 0 ? 1 : Math.Floor(100.00 / sampleRate));
        }

        public static int getSamplingPercentage(AppConfig config, RequestMap requestMap)
        {

            if (config.regex_config != null)
            {
                var rates = new List<int>();
                foreach (RegexConfig rc in config.regex_config)
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

            if (requestMap.companyId != null && config.company_sample_rate != null)
            {
                if (config.company_sample_rate.TryGetValue(requestMap.companyId, out int v))
                {
                    return v;
                }
            }
            if (requestMap.userId != null && config.user_sample_rate != null)
            {
                if (config.user_sample_rate.TryGetValue(requestMap.userId, out int v))
                {
                    return v;
                }
            }

            return config.sample_rate;

        }
    }
}
