// Services/UserSessionService.cs
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using System;

namespace SACCOBlockChainSystem.Services
{
    public interface IUserSessionService
    {
        Task<UserSession> CreateSessionAsync(string userId, string userName, string companyCode, string companyName, string userGroup);
        Task<UserSession> UpdateSessionActivityAsync(long sessionId);
        Task<UserSession> UpdateSessionLogoutAsync(long sessionId, string logoutReason = "Manual");
        Task<UserSession> GetActiveSessionAsync(string userId);
        Task<bool> CloseExpiredSessionsAsync(int timeoutMinutes = 30);
        Task<UserSession> GetSessionByIdAsync(long sessionId);
        Task<List<UserSession>> GetUserSessionsAsync(string userId, int days = 30);
    }
    public class UserSessionService : IUserSessionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UserSessionService> _logger;
        private readonly IIpAddressHelper _ipAddressHelper;

        public UserSessionService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IIpAddressHelper ipAddressHelper,
            ILogger<UserSessionService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _ipAddressHelper = ipAddressHelper;
        }

        public async Task<UserSession> CreateSessionAsync(
            string userId,
            string userName,
            string companyCode,
            string companyName,
            string userGroup)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;

                // ============================================================
                // ✅ USE THE IP HELPER TO GET THE CORRECT IP ADDRESS
                // ============================================================
                var ipAddress = _ipAddressHelper.GetClientIpAddress();
                _logger.LogInformation($"User {userName} login from IP: {ipAddress}");

                var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString() ?? "Unknown";
                var sessionCookie = httpContext?.Request?.Cookies[".AspNetCore.Cookies"] ?? "Unknown";
                var sessionId = httpContext?.Session?.Id ?? Guid.NewGuid().ToString();

                // Close any active sessions for this user
                await CloseActiveSessionsForUserAsync(userId);

                var session = new UserSession
                {
                    UserId = userId,
                    UserName = userName,
                    CompanyCode = companyCode,
                    CompanyName = companyName,
                    UserGroup = userGroup,
                    IpAddress = ipAddress, 
                    LoginTime = DateTime.Now,
                    LastActivityTime = DateTime.Now,
                    SessionId_ = sessionId,
                    BrowserAgent = userAgent,
                    IsActive = true,
                    SessionCookie = sessionCookie,
                    DeviceInfo = GetDeviceInfo(userAgent),
                    CreatedAt = DateTime.Now
                };

                _context.UserSessions.Add(session);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Session created for user {userName} (ID: {userId}) from IP: {ipAddress}");

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating session for user {userId}");
                throw;
            }
        }


        public async Task<UserSession> UpdateSessionActivityAsync(long sessionId)
        {
            try
            {
                var session = await _context.UserSessions
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session != null)
                {
                    session.LastActivityTime = DateTime.Now;
                    await _context.SaveChangesAsync();
                }

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating session activity for session {sessionId}");
                return null;
            }
        }

        public async Task<UserSession> UpdateSessionLogoutAsync(long sessionId, string logoutReason = "Manual")
        {
            try
            {
                var session = await _context.UserSessions
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session != null)
                {
                    session.LogoutTime = DateTime.Now;
                    session.IsActive = false;
                    session.LogoutReason = logoutReason;

                    if (session.LoginTime.HasValue)
                    {
                        session.SessionDurationSeconds = (int)(DateTime.Now - session.LoginTime.Value).TotalSeconds;
                    }

                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Session {sessionId} closed for user {session.UserName}, Reason: {logoutReason}");
                }

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating session logout for session {sessionId}");
                return null;
            }
        }

        public async Task<UserSession> GetActiveSessionAsync(string userId)
        {
            try
            {
                return await _context.UserSessions
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting active session for user {userId}");
                return null;
            }
        }

        public async Task<bool> CloseExpiredSessionsAsync(int timeoutMinutes = 30)
        {
            try
            {
                var cutoffTime = DateTime.Now.AddMinutes(-timeoutMinutes);

                var expiredSessions = await _context.UserSessions
                    .Where(s => s.IsActive &&
                                s.LastActivityTime.HasValue &&
                                s.LastActivityTime.Value < cutoffTime)
                    .ToListAsync();

                foreach (var session in expiredSessions)
                {
                    session.IsActive = false;
                    session.LogoutTime = cutoffTime;
                    session.LogoutReason = "Timeout";
                    if (session.LoginTime.HasValue)
                    {
                        session.SessionDurationSeconds = (int)(cutoffTime - session.LoginTime.Value).TotalSeconds;
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Closed {expiredSessions.Count} expired sessions");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing expired sessions");
                return false;
            }
        }

        public async Task<UserSession> GetSessionByIdAsync(long sessionId)
        {
            return await _context.UserSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        }

        public async Task<List<UserSession>> GetUserSessionsAsync(string userId, int days = 30)
        {
            var cutoffDate = DateTime.Now.AddDays(-days);

            return await _context.UserSessions
                .Where(s => s.UserId == userId && s.CreatedAt >= cutoffDate)
                .OrderByDescending(s => s.LoginTime)
                .ToListAsync();
        }

        private async Task CloseActiveSessionsForUserAsync(string userId)
        {
            try
            {
                var activeSessions = await _context.UserSessions
                    .Where(s => s.UserId == userId && s.IsActive)
                    .ToListAsync();

                foreach (var session in activeSessions)
                {
                    session.IsActive = false;
                    session.LogoutTime = DateTime.Now;
                    session.LogoutReason = "NewLogin";

                    if (session.LoginTime.HasValue)
                    {
                        session.SessionDurationSeconds = (int)(DateTime.Now - session.LoginTime.Value).TotalSeconds;
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error closing active sessions for user {userId}");
            }
        }

        private string GetDeviceInfo(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
                return "Unknown";

            userAgent = userAgent.ToLower();

            if (userAgent.Contains("windows"))
                return "Windows";
            if (userAgent.Contains("macintosh") || userAgent.Contains("mac os x"))
                return "MacOS";
            if (userAgent.Contains("linux"))
                return "Linux";
            if (userAgent.Contains("iphone") || userAgent.Contains("ipad"))
                return "iOS";
            if (userAgent.Contains("android"))
                return "Android";
            if (userAgent.Contains("mobile") || userAgent.Contains("phone"))
                return "Mobile";

            return "Desktop";
        }
    }
}