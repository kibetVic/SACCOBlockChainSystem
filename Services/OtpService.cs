// Services/OtpService.cs
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;

namespace SACCOBlockChainSystem.Services
{
    public interface IOtpService
    {
        string GenerateOtp(string userId);
        bool ValidateOtp(string userId, string otp);
        bool IsOtpValid(string userId);
        DateTime? GetOtpExpiry(string userId);
        void ClearOtp(string userId);
        int GetRemainingSeconds(string userId);
        bool HasValidOtp(string userId);
    }

    public class OtpService : IOtpService
    {
        private readonly ILogger<OtpService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Random _random = new();
        private readonly int _otpLifetimeMinutes = 5; // 5 minutes

        public OtpService(ILogger<OtpService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private ISession Session => _httpContextAccessor.HttpContext?.Session;

        private string GetOtpKey(string userId) => $"OtpCode_{userId}";
        private string GetExpiryKey(string userId) => $"OtpExpiry_{userId}";
        private string GetUsedKey(string userId) => $"OtpUsed_{userId}";

        public string GenerateOtp(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("UserId cannot be null or empty", nameof(userId));
            }

            // Clear any existing OTP for this user first
            ClearOtp(userId);

            // Generate a 6-digit OTP
            var otp = _random.Next(100000, 999999).ToString("D6");

            // ✅ FIX: Use DateTime.Now (local time) or ensure proper UTC conversion
            // Using DateTime.Now.AddMinutes for consistency with your other code
            var expiry = DateTime.Now.AddMinutes(_otpLifetimeMinutes);

            // Store in Session
            Session.SetString(GetOtpKey(userId), otp);
            Session.SetString(GetExpiryKey(userId), expiry.ToString("O"));
            Session.SetString(GetUsedKey(userId), "false");

            _logger.LogInformation($"OTP generated for user {userId}. OTP: {otp}, Expires at {expiry:HH:mm:ss} ({_otpLifetimeMinutes} minutes)");
            return otp;
        }

        public bool ValidateOtp(string userId, string otp)
        {
            _logger.LogInformation($"ValidateOtp called: UserId={userId}, Otp={otp}");

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(otp))
            {
                _logger.LogWarning($"ValidateOtp: Invalid input");
                return false;
            }

            // Retrieve from Session
            var storedCode = Session.GetString(GetOtpKey(userId));
            var expiryStr = Session.GetString(GetExpiryKey(userId));
            var isUsed = Session.GetString(GetUsedKey(userId));

            _logger.LogInformation($"ValidateOtp: StoredCode={storedCode}, Expiry={expiryStr}, IsUsed={isUsed}");

            if (string.IsNullOrEmpty(storedCode) || string.IsNullOrEmpty(expiryStr))
            {
                _logger.LogWarning($"ValidateOtp: No OTP found for user {userId}");
                return false;
            }

            // Check if OTP has already been used
            if (isUsed == "true")
            {
                _logger.LogWarning($"ValidateOtp: OTP for user {userId} has already been used");
                return false;
            }

            // ✅ FIX: Use DateTime.Now for consistency
            if (DateTime.TryParse(expiryStr, out var expiry) && expiry < DateTime.Now)
            {
                ClearOtp(userId);
                _logger.LogWarning($"ValidateOtp: OTP for user {userId} has expired at {expiry:HH:mm:ss}");
                return false;
            }

            // Verify code
            if (storedCode != otp)
            {
                _logger.LogWarning($"ValidateOtp: Invalid OTP for user {userId}. Expected: {storedCode}, Got: {otp}");
                return false;
            }

            // Mark as used
            Session.SetString(GetUsedKey(userId), "true");
            _logger.LogInformation($"ValidateOtp: OTP validated SUCCESSFULLY for user {userId}");

            return true;
        }

        public bool IsOtpValid(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            var storedCode = Session.GetString(GetOtpKey(userId));
            var expiryStr = Session.GetString(GetExpiryKey(userId));
            var isUsed = Session.GetString(GetUsedKey(userId));

            if (string.IsNullOrEmpty(storedCode) || string.IsNullOrEmpty(expiryStr))
            {
                return false;
            }

            if (isUsed == "true")
            {
                return false;
            }

            // ✅ FIX: Use DateTime.Now for consistency
            if (DateTime.TryParse(expiryStr, out var expiry) && expiry < DateTime.Now)
            {
                ClearOtp(userId);
                return false;
            }

            return true;
        }

        public bool HasValidOtp(string userId)
        {
            return IsOtpValid(userId);
        }

        public DateTime? GetOtpExpiry(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var expiryStr = Session.GetString(GetExpiryKey(userId));
            if (string.IsNullOrEmpty(expiryStr))
            {
                return null;
            }

            if (DateTime.TryParse(expiryStr, out var expiry))
            {
                return expiry;
            }

            return null;
        }

        public int GetRemainingSeconds(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return 0;
            }

            var expiry = GetOtpExpiry(userId);
            if (!expiry.HasValue)
            {
                return 0;
            }

            var isUsed = Session.GetString(GetUsedKey(userId));
            if (isUsed == "true")
            {
                return 0;
            }

            // ✅ FIX: Calculate remaining seconds using DateTime.Now
            var remaining = (int)(expiry.Value - DateTime.Now).TotalSeconds;
            return Math.Max(0, remaining);
        }

        public void ClearOtp(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                Session.Remove(GetOtpKey(userId));
                Session.Remove(GetExpiryKey(userId));
                Session.Remove(GetUsedKey(userId));
                _logger.LogInformation($"OTP cleared for user {userId}");
            }
        }
    }
}