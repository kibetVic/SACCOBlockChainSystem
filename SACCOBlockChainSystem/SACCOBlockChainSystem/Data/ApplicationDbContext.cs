using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Models;
using static SACCOBlockChainSystem.Services.MemberService;

namespace SACCOBlockChainSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Existing tables from your models
        public DbSet<Client> Clients { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Contrib> Contribs { get; set; }
        public DbSet<ContribShare> ContribShares { get; set; }
        public DbSet<CoopTransaction> CoopTransactions { get; set; }
        public DbSet<GeneralLedger> GeneralLedgers { get; set; }
        public DbSet<Gltransaction> Gltransactions { get; set; }
        public DbSet<Loantype> Loantypes { get; set; }
        public DbSet<Loan> Loans { get; set; }
        public DbSet<LoanGuarantor> LoanGuarantors { get; set; }
        public DbSet<LoanAppraisal> LoanAppraisals { get; set; }
        public DbSet<LoanApproval> LoanApprovals { get; set; }
        public DbSet<LoanDisbursement> LoanDisbursements { get; set; }
        public DbSet<LoanRepayment> LoanRepayments { get; set; }
        public DbSet<LoanSchedule> LoanSchedules { get; set; }
        public DbSet<LoanDocument> LoanDocuments { get; set; }
        public DbSet<LoanAuditTrail> LoanAuditTrails { get; set; }
        public DbSet<Member> Members { get; set; }
        public DbSet<Share> Shares { get; set; }
        public DbSet<Sharetype> Sharetypes { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<TransactionDetail> TransactionDetails { get; set; }
        public DbSet<Transactions2> Transactions2 { get; set; }
        public DbSet<UserAccounts1> UserAccounts1 { get; set; }
        public DbSet<WicciClient> WicciClients { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<GlSetup> GlSetup { get; set; }
        public DbSet<MemberNumberCounter> MemberNumberCounters { get; set; }

        // NEW BLOCKCHAIN TABLES
        public DbSet<Block> Blocks { get; set; }
        public DbSet<BlockchainTransaction> BlockchainTransactions { get; set; }

        // Optional: Audit Trail table
        public DbSet<AuditLog> AuditLogs { get; set; }
        public List<dynamic>? Banks { get; internal set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure primary keys for tables without explicit [Key] attribute
            modelBuilder.Entity<Loantype>().HasKey(l => l.Id);
            modelBuilder.Entity<Share>().HasKey(s => new { s.MemberNo, s.Sharescode });
            modelBuilder.Entity<Transaction>().HasKey(t => t.Id);
            modelBuilder.Entity<TransactionDetail>().HasKey(t => t.Id);
            modelBuilder.Entity<Transactions2>().HasKey(t => t.Id);

            // Configure relationships
            modelBuilder.Entity<Contrib>()
                .HasOne(c => c.SharescodeNavigation)
                .WithMany(s => s.Contribs)
                .HasForeignKey(c => c.Sharescode);

            modelBuilder.Entity<ContribShare>()
                .HasOne(c => c.SharescodeNavigation)
                .WithMany(s => s.ContribShares)
                .HasForeignKey(c => c.Sharescode);

            modelBuilder.Entity<MemberNumberCounter>(entity =>
            {
                entity.HasKey(e => e.CompanyCode);
                entity.Property(e => e.CompanyCode)
                    .HasMaxLength(10)
                    .IsRequired();
                entity.Property(e => e.LastNumber)
                    .IsRequired();
                entity.Property(e => e.LastUpdated)
                    .IsRequired();
            });

            modelBuilder.Entity<Member>(entity =>
            {
                entity.Property(e => e.BlockchainTxId)
                    .HasMaxLength(255)
                    .HasColumnName("BlockchainTxId");
            });

            modelBuilder.Entity<Loan>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.LoanNo).IsUnique();
                entity.HasIndex(e => e.MemberNo);
                entity.HasIndex(e => e.LoanStatus);
                entity.HasIndex(e => e.CompanyCode);
                entity.HasIndex(e => e.BlockchainTxId);

                entity.Property(e => e.LoanNo).HasMaxLength(50).IsRequired();
                entity.Property(e => e.InterestRate).HasPrecision(5, 4);

                entity.HasOne(d => d.Member)
                    .WithMany()
                    .HasForeignKey(d => d.MemberNo)
                    .HasPrincipalKey(d => d.MemberNo) // Important: Use MemberNo as principal key
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.LoanType)
                    .WithMany()
                    .HasForeignKey(d => d.LoanTypeId) // Use LoanTypeId instead of LoanCode
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Loan>()
                .ToTable(tb => tb.UseSqlOutputClause(false));

            modelBuilder.Entity<LoanGuarantor>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.LoanNo);
                entity.HasIndex(e => e.GuarantorMemberNo);
                entity.HasIndex(e => new { e.LoanNo, e.GuarantorMemberNo }).IsUnique();

                entity.HasOne(d => d.Loan)
                    .WithMany(p => p.LoanGuarantors)
                    .HasForeignKey(d => d.LoanNo)
                    .HasPrincipalKey(d => d.LoanNo) // Important: Use LoanNo as principal key
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.GuarantorMember)
                    .WithMany()
                    .HasForeignKey(d => d.GuarantorMemberNo)
                    .HasPrincipalKey(d => d.MemberNo) // Important: Use MemberNo as principal key
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LoanAppraisal>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.LoanNo).IsUnique();

                entity.HasOne(d => d.Loan)
                    .WithMany(p => p.LoanAppraisals)
                    .HasForeignKey(d => d.LoanNo)
                    .HasPrincipalKey(d => d.LoanNo) // Important: Use LoanNo as principal key
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LoanApproval>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.LoanNo);

                entity.HasOne(d => d.Loan)
                    .WithMany(p => p.LoanApprovals)
                    .HasForeignKey(d => d.LoanNo)
                    .HasPrincipalKey(d => d.LoanNo) // Important: Use LoanNo as principal key
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LoanDisbursement>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.DisbursementNo).IsUnique();
                entity.HasIndex(e => e.LoanNo).IsUnique();

                entity.HasOne(d => d.Loan)
                    .WithMany(p => p.LoanDisbursements)
                    .HasForeignKey(d => d.LoanNo)
                    .HasPrincipalKey(d => d.LoanNo) // Important: Use LoanNo as principal key
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.Member)
                    .WithMany()
                    .HasForeignKey(d => d.MemberNo)
                    .HasPrincipalKey(d => d.MemberNo) // Important: Use MemberNo as principal key
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LoanSchedule>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.LoanNo, e.InstallmentNo }).IsUnique();

                entity.HasOne(d => d.Loan)
                    .WithMany(p => p.LoanSchedules)
                    .HasForeignKey(d => d.LoanNo)
                    .HasPrincipalKey(d => d.LoanNo) // Important: Use LoanNo as principal key
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LoanRepayment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ReceiptNo).IsUnique();
                entity.HasIndex(e => e.LoanNo);

                entity.HasOne(d => d.Loan)
                    .WithMany(p => p.LoanRepayments)
                    .HasForeignKey(d => d.LoanNo)
                    .HasPrincipalKey(d => d.LoanNo) // Important: Use LoanNo as principal key
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.Member)
                    .WithMany()
                    .HasForeignKey(d => d.MemberNo)
                    .HasPrincipalKey(d => d.MemberNo) // Important: Use MemberNo as principal key
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LoanDocument>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.LoanNo);

                entity.HasOne(d => d.Loan)
                    .WithMany(p => p.LoanDocuments)
                    .HasForeignKey(d => d.LoanNo)
                    .HasPrincipalKey(d => d.LoanNo) // Important: Use LoanNo as principal key
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LoanAuditTrail>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.LoanNo);

                entity.HasOne(d => d.Loan)
                    .WithMany(p => p.LoanAuditTrails)
                    .HasForeignKey(d => d.LoanNo)
                    .HasPrincipalKey(d => d.LoanNo) // Important: Use LoanNo as principal key
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Block - BlockchainTransaction relationship
            modelBuilder.Entity<BlockchainTransaction>()
                .HasOne(t => t.Block)
                .WithMany(b => b.Transactions)
                .HasForeignKey(t => t.BlockHash)
                .HasPrincipalKey(b => b.BlockHash);

            // Configure default for all decimals
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,2)");
            }

            // Override for specific properties that need different precision
            modelBuilder.Entity<Sharetype>(entity =>
            {
                entity.Property(e => e.Interest).HasPrecision(5, 4); // For percentages like 0.1250
                entity.Property(e => e.ElseRatio).HasPrecision(5, 4);
            });

            // Add indexes for performance
            modelBuilder.Entity<Member>().HasIndex(m => m.MemberNo).IsUnique();
            modelBuilder.Entity<Member>().HasIndex(m => m.Idno);
            modelBuilder.Entity<Member>().HasIndex(m => m.PhoneNo);
            modelBuilder.Entity<Member>().HasIndex(m => m.BlockchainTxId);

            modelBuilder.Entity<BlockchainTransaction>().HasIndex(t => t.TransactionId).IsUnique();
            modelBuilder.Entity<BlockchainTransaction>().HasIndex(t => t.MemberNo);
            modelBuilder.Entity<BlockchainTransaction>().HasIndex(t => t.TransactionType);
            modelBuilder.Entity<BlockchainTransaction>().HasIndex(t => t.Timestamp);

            modelBuilder.Entity<Block>().HasIndex(b => b.BlockHash).IsUnique();
            modelBuilder.Entity<Block>().HasIndex(b => b.PreviousHash);

            modelBuilder.Entity<Contrib>().HasIndex(c => c.MemberNo);
            modelBuilder.Entity<Contrib>().HasIndex(c => c.BlockchainTxId);

            modelBuilder.Entity<ContribShare>().HasIndex(c => c.MemberNo);
            modelBuilder.Entity<ContribShare>().HasIndex(c => c.BlockchainTxId);

            modelBuilder.Entity<Transactions2>().HasIndex(t => t.MemberNo);
            modelBuilder.Entity<Transactions2>().HasIndex(t => t.BlockchainTxId);
        }
    }
}