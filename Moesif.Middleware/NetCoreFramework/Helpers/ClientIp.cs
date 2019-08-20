using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;

#if NETCORE
using Microsoft.AspNetCore.Http;
#endif

#if NETCORE
namespace Moesif.Middleware.NetCoreFramework.Helpers
{
    public class ClientIp
    {
        // Check Valid IpAddress
        public static bool IsValidIP(string address)
        {
            IPAddress ip;
            if (!IPAddress.TryParse(address, out ip)) return false;

            switch (ip.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    if (address.Length > 6 && address.Contains("."))
                    {
                        string[] s = address.Split('.');
                        if (s.Length == 4 && s[0].Length > 0 && s[1].Length > 0 && s[2].Length > 0 && s[3].Length > 0)
                            return true;
                    }
                    break;
                case AddressFamily.InterNetworkV6:
                    if (address.Contains(":") && address.Length > 15)
                        return true;
                    break;
            }
            return false;
        }

        // Get Client Ip
        public string GetClientIp(Dictionary<string, string> headers, HttpRequest request)
        {
            try
            {
                string Ip;

                if (headers.ContainsKey("X-Client-Ip") && IsValidIP(headers["X-Client-Ip"]))
                {
                    return headers["X-Client-Ip"];
                }
                if (headers.ContainsKey("X-Forwarded-For"))
                {
                    List<string> ForwardedIps = new List<string>();

                    foreach (string forwardedIp in headers["X-Forwarded-For"].Split(','))
                    {
                        Ip = forwardedIp.Trim();
                        if (Ip.Contains(":"))
                        {
                            ForwardedIps.Add(Ip.Split(':')[0]);
                        }
                        ForwardedIps.Add(Ip);
                    }

                    return ForwardedIps.FirstOrDefault(validIp => IsValidIP(validIp));
                }
                if (headers.ContainsKey("Cf-Connecting-Ip") && IsValidIP(headers["Cf-Connecting-Ip"]))
                {
                    return headers["Cf-Connecting-Ip"];
                }
                if (headers.ContainsKey("True-Client-Ip") && IsValidIP(headers["True-Client-Ip"]))
                {
                    return headers["True-Client-Ip"];
                }
                if (headers.ContainsKey("X-Real-Ip") && IsValidIP(headers["X-Real-Ip"]))
                {
                    return headers["X-Real-Ip"];
                }
                if (headers.ContainsKey("X-Cluster-Client-Ip") && IsValidIP(headers["X-Cluster-Client-Ip"]))
                {
                    return headers["X-Cluster-Client-Ip"];
                }
                if (headers.ContainsKey("X-Forwarded") && IsValidIP(headers["X-Forwarded"]))
                {
                    return headers["X-Forwarded"];
                }
                if (headers.ContainsKey("Forwarded-For") && IsValidIP(headers["Forwarded-For"]))
                {
                    return headers["Forwarded-For"];
                }
                if (headers.ContainsKey("Forwarded") && IsValidIP(headers["Forwarded"]))
                {
                    return headers["Forwarded"];
                }
                return request.HttpContext.Connection.RemoteIpAddress.ToString();
            }
            catch (Exception e)
            {
                return request.HttpContext.Connection.RemoteIpAddress.ToString();
            }
        }
    }
}
#endif