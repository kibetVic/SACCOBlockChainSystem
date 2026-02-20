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
        public DbSet<Loan> Loans { get; set; }
        public DbSet<Loanbal> Loanbals { get; set; }
        public DbSet<Loanguar> Loanguars { get; set; }
        public DbSet<Loantype> Loantypes { get; set; }
        public DbSet<Member> Members { get; set; }
        public DbSet<Repay> Repays { get; set; }
        public DbSet<Share> Shares { get; set; }
        public DbSet<Sharetype> Sharetypes { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<TransactionDetail> TransactionDetails { get; set; }
        public DbSet<Transactions2> Transactions2 { get; set; }
        public DbSet<UserAccounts1> UserAccounts1 { get; set; }
        public DbSet<WicciClient> WicciClients { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<GlSetup> GlSetup { get; set; }
        public DbSet<Journal> Journals { get; set; }
        //public DbSet<BudgetHeader> BudgetHeader { get; set; }

        //public DbSet<BudgetEntry> BudgetEntries { get; set; }
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
            modelBuilder.Entity<Loanguar>().HasKey(l => l.Id);
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


            // Configure Loan-Member relationship
            modelBuilder.Entity<Loan>()
                .HasOne(l => l.Member)
                .WithMany() // If Member doesn't have Loans collection, use WithMany()
                .HasForeignKey(l => l.MemberNo)
                .HasPrincipalKey(m => m.MemberNo);

            // Configure Loan-Loanbals relationship
            modelBuilder.Entity<Loan>()
                .HasMany(l => l.Loanbals)
                .WithOne() // If Loanbal doesn't have Loan navigation property
                .HasForeignKey(lb => lb.LoanNo)
                .HasPrincipalKey(l => l.LoanNo);

            // Configure Loan-Repays relationship
            modelBuilder.Entity<Loan>()
                .HasMany(l => l.Repays)
                .WithOne() // If Repay doesn't have Loan navigation property
                .HasForeignKey(r => r.LoanNo)
                .HasPrincipalKey(l => l.LoanNo);

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

            modelBuilder.Entity<Loan>().HasIndex(l => l.LoanNo).IsUnique();
            modelBuilder.Entity<Loan>().HasIndex(l => l.MemberNo);
            modelBuilder.Entity<Loan>().HasIndex(l => l.BlockchainTxId);

            modelBuilder.Entity<ContribShare>().HasIndex(c => c.MemberNo);
            modelBuilder.Entity<ContribShare>().HasIndex(c => c.BlockchainTxId);

            modelBuilder.Entity<Repay>().HasIndex(r => r.LoanNo);
            modelBuilder.Entity<Repay>().HasIndex(r => r.MemberNo);
            modelBuilder.Entity<Repay>().HasIndex(r => r.BlockchainTxId);

            modelBuilder.Entity<Transactions2>().HasIndex(t => t.MemberNo);
            modelBuilder.Entity<Transactions2>().HasIndex(t => t.BlockchainTxId);
        }
    }
}
