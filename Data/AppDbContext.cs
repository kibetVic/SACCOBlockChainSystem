using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Models;

namespace SACCOBlockChainSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSet properties for your entities
        public DbSet<ApiTable> ApiTable { get; set; }
        public DbSet<UserAccounts1> UserAccounts1 { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Transactions2> Transactions2 { get; set; }
        public DbSet<Company> Company { get; set; }
        public DbSet<ApiTransaction> ApiTransactions { get; set; }
        public DbSet<TransactionDetail> Transaction_detail { get; set; }
        public DbSet<CoopTransaction> CoopTransactions { get; set; }
        public DbSet<Loanbal> Loanbal { get; set; }

        public void ExecuteSqlRaw(string sqlQuery, params object[] parameters)
        {
            this.Database.ExecuteSqlRaw(sqlQuery, parameters);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TransactionDetail>(entity =>
            {
                entity.ToTable("Transaction_detail");

                //modelBuilder.Entity<SaccoMembers>()
                //   .HasNoKey();
            });



            // Add more table configurations as needed
        }

    }
}
