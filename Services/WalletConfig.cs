namespace SACCOBlockChainSystem.Models
{
    public class WalletConfig
    {
        public int Id { get; set; }

        // Organization / SACCO Identifier
        public string CompanyCode { get; set; } = string.Empty;

        // Wallet Creation Settings
        public bool EnableWallets { get; set; } = false;

        // Automatically create wallet during member registration
        public bool AutoAssignWalletOnRegistration { get; set; } = false;

        // Allow members to access wallets from mobile/web
        public bool AllowMemberWalletAccess { get; set; } = false;

        // Allow wallet-to-wallet transfers
        public bool AllowInternalTransfers { get; set; } = false;

        // Allow deposits into wallet
        public bool AllowDeposits { get; set; } = false;

        // Allow withdrawals from wallet
        public bool AllowWithdrawals { get; set; } = false;

        // Allow loan disbursement into wallet
        public bool AllowLoanDisbursementToWallet { get; set; } = false;

        // Allow loan repayments from wallet balance
        public bool AllowLoanRepaymentFromWallet { get; set; } = false;

        // Require PIN before transaction signing
        public bool RequireTransactionPin { get; set; } = false;

        // Require OTP/MFA for sensitive operations
        public bool RequireOtpVerification { get; set; } = false;

      
        // Whether system stores private keys (custodial mode)
        public bool CustodialWallets { get; set; } = false;

        // Allow members to reset wallets
        public bool AllowWalletReset { get; set; } = false;

        // Maximum transaction amount per transaction
        public decimal MaxTransactionAmount { get; set; } = 100000m;

        // Daily transfer limit
        public decimal DailyTransactionLimit { get; set; } = 500000m;

        // Wallet inactivity timeout in minutes
        public int SessionTimeoutMinutes { get; set; } = 15;

       
        
        // Enable SMS notifications
        public bool EnableSmsNotifications { get; set; } = false;

        // Enable email notifications
        public bool EnableEmailNotifications { get; set; } = true;

        // Enable push notifications
        public bool EnablePushNotifications { get; set; } = true;

        // Whether approval is required for large transfers
        public bool RequireApprovalForLargeTransactions { get; set; } = true;

        // Threshold requiring approval
        public decimal ApprovalThresholdAmount { get; set; } = 200000m;

        // Wallet creation fee
        public decimal WalletCreationFee { get; set; } = 0m;

        // Transaction fee percentage
        public decimal TransactionFeePercent { get; set; } = 0m;

        

        // Audit Fields
        public string? AuditId { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public DateTime? DateModified { get; set; }
    }
}