// Services/IpAddressHelper.cs
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace SACCOBlockChainSystem.Services
{
    public interface IIpAddressHelper
    {
        string GetClientIpAddress();
        string GetClientIpAddress(HttpContext context);
        string GetRealIpAddress();
    }

    public class IpAddressHelper : IIpAddressHelper
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<IpAddressHelper> _logger;

        public IpAddressHelper(IHttpContextAccessor httpContextAccessor, ILogger<IpAddressHelper> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <summary>
        /// Gets the client IP address from the current HTTP context.
        /// Handles proxies, load balancers, and X-Forwarded-For headers.
        /// </summary>
        public string GetClientIpAddress()
        {
            var context = _httpContextAccessor.HttpContext;
            return GetClientIpAddress(context);
        }

        /// <summary>
        /// Gets the client IP address from a specific HTTP context.
        /// Handles proxies, load balancers, and X-Forwarded-For headers.
        /// </summary>
        public string GetClientIpAddress(HttpContext context)
        {
            if (context == null)
                return "Unknown";

            string ip = null;

            try
            {
                // 1. Check X-Forwarded-For header (most reliable for proxied requests)
                var forwardedHeaders = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedHeaders))
                {
                    // Get the first IP from the chain (client IP)
                    var ips = forwardedHeaders.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (ips.Length > 0)
                    {
                        ip = ips[0].Trim();
                        _logger.LogDebug($"X-Forwarded-For: {ip}");
                    }
                }

                // 2. Check X-Real-IP header (common with nginx)
                if (string.IsNullOrEmpty(ip))
                {
                    ip = context.Request.Headers["X-Real-IP"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(ip))
                    {
                        _logger.LogDebug($"X-Real-IP: {ip}");
                    }
                }

                // 3. Check Cloudflare's CF-Connecting-IP header
                if (string.IsNullOrEmpty(ip))
                {
                    ip = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(ip))
                    {
                        _logger.LogDebug($"CF-Connecting-IP: {ip}");
                    }
                }

                // 4. Fallback to RemoteIpAddress
                if (string.IsNullOrEmpty(ip))
                {
                    var remoteIp = context.Connection.RemoteIpAddress;
                    if (remoteIp != null)
                    {
                        ip = remoteIp.ToString();
                        _logger.LogDebug($"RemoteIpAddress: {ip}");
                    }
                }

                // 5. Handle localhost IPs (::1, 127.0.0.1)
                if (string.IsNullOrEmpty(ip) || ip == "::1" || ip == "127.0.0.1")
                {
                    // Check if this is a local request
                    var remoteIp = context.Connection.RemoteIpAddress;
                    bool isLocal = false;

                    // Properly check if IP is loopback
                    if (remoteIp != null)
                    {
                        // For IPv4: Check if it's 127.0.0.1 or starts with 127.
                        // For IPv6: Check if it's ::1
                        var ipStr = remoteIp.ToString();
                        isLocal = ipStr == "::1" || ipStr == "127.0.0.1" || ipStr.StartsWith("127.") ||
                                  IPAddress.IsLoopback(remoteIp);
                    }

                    if (isLocal || string.IsNullOrEmpty(ip) || ip == "::1" || ip == "127.0.0.1")
                    {
                        // For local development, get the actual local IP
                        try
                        {
                            var hostName = Dns.GetHostName();
                            var hostEntry = Dns.GetHostEntry(hostName);
                            var localIp = hostEntry.AddressList
                                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                            if (localIp != null)
                            {
                                ip = localIp.ToString();
                                _logger.LogDebug($"Local IP: {ip}");
                            }
                            else
                            {
                                ip = "127.0.0.1";
                            }
                        }
                        catch
                        {
                            // Fallback to localhost
                            ip = "127.0.0.1";
                        }
                    }
                }

                // Clean up the IP address (remove port if present)
                if (!string.IsNullOrEmpty(ip))
                {
                    // Remove port number if present (e.g., "192.168.1.1:54321" -> "192.168.1.1")
                    var portIndex = ip.LastIndexOf(':');
                    if (portIndex > 0 && ip.Count(c => c == ':') == 1)
                    {
                        ip = ip.Substring(0, portIndex);
                    }

                    // Remove brackets from IPv6 addresses
                    if (ip.StartsWith("[") && ip.EndsWith("]"))
                    {
                        ip = ip.Substring(1, ip.Length - 2);
                    }
                }

                // Final fallback
                if (string.IsNullOrEmpty(ip))
                {
                    ip = "Unknown";
                }

                return ip;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting client IP");
                return "Unknown";
            }
        }

        /// <summary>
        /// Gets the real IP address, specifically trying to avoid localhost IPs.
        /// </summary>
        public string GetRealIpAddress()
        {
            var ip = GetClientIpAddress();

            // If the IP is localhost, try to get the actual local IP
            if (ip == "::1" || ip == "127.0.0.1" || ip == "Unknown")
            {
                try
                {
                    var hostName = Dns.GetHostName();
                    var hostEntry = Dns.GetHostEntry(hostName);
                    var localIp = hostEntry.AddressList
                        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                    if (localIp != null)
                    {
                        ip = localIp.ToString();
                    }
                }
                catch
                {
                    // Keep the original IP
                }
            }

            return ip;
        }

        // Helper method to check if an IP address is local
        private bool IsLocalIpAddress(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return true;

            // Check for localhost
            if (ip == "::1" || ip == "127.0.0.1" || ip.StartsWith("127."))
                return true;

            // Check for private network IPs
            if (ip.StartsWith("10.") || ip.StartsWith("172.16.") ||
                ip.StartsWith("192.168.") || ip.StartsWith("169.254."))
                return true;

            return false;
        }
    }
}