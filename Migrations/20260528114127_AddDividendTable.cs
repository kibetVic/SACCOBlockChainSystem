using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SACCOBlockChainSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDividendTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    IdNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecruitementAgents = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Names = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    staffcode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Occupation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LandPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MobileNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Branchname = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HomeAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Town = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Recruitdate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuditId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PIN = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.IdNo);
                });

            migrationBuilder.CreateTable(
                name: "ApiTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Passkey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiPassword = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsumerKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsumerSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShortCode = table.Column<int>(type: "int", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    total_transactions = table.Column<int>(type: "int", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShortCode = table.Column<int>(type: "int", nullable: true),
                    TransactionCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheckoutId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConversationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Recipient = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    Request = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LoanNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "APPRAISAL",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AppraisDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Salary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Allowances = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RepayMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CoopShares = table.Column<decimal>(name: "Co-opShares", type: "decimal(18,2)", nullable: true),
                    CoopLoans = table.Column<decimal>(name: "Co-opLoans", type: "decimal(18,2)", nullable: true),
                    Shares = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Loans = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Deductions = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AmtRecommended = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuditID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    memberno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Repayrate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Tinterest = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NoOfLoans = table.Column<int>(type: "int", nullable: true),
                    LoanGuarantor = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetMonthsalary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SocietyPayment = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Interest = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Principal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalInterest = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BankLoan = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Nssf = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CopLoanded = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherDed = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    officernames = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    transactionNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExpectedNetsalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DeductionToGross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StatutoryDed = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StatutoryDedTogross = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalDedNewLoanToGross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetSalaryToGross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalLoanToGross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCoopDedToGross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDedToGrossLessstatutory = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APPRAISAL", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AssetsRegisters",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Class = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AssetType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TagNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SerialNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    ActualValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MarketValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DateOfManufacture = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DatePurchased = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    posted = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetsRegisters", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IPAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.AuditLogId);
                });

            migrationBuilder.CreateTable(
                name: "AuditTrail",
                columns: table => new
                {
                    AuditId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActionDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecordId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    BrowserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Module = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtraData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    HostName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTrail", x => x.AuditId);
                });

            migrationBuilder.CreateTable(
                name: "Banks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Branch = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SwiftCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SortCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GlAccountNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GlAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Blocks",
                columns: table => new
                {
                    BlockId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BlockHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PreviousHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MerkleRoot = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Nonce = table.Column<int>(type: "int", nullable: false),
                    Confirmed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocks", x => x.BlockId);
                    table.UniqueConstraint("AK_Blocks_BlockHash", x => x.BlockHash);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    ClientId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Surname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Othername = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Idno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PinStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecretWord = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unsubscribe = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Acs = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subscription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserRole = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubscriptionPlan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.ClientId);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Contactperson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NoEmployees = table.Column<int>(type: "int", nullable: true),
                    County = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubCounty = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ward = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Village = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Cigcode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CountyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Unitcode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccountNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NoYears = table.Column<int>(type: "int", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Capital = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Project = table.Column<bool>(type: "bit", nullable: false),
                    AuditId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                    table.UniqueConstraint("AK_Companies_CompanyCode", x => x.CompanyCode);
                });

            migrationBuilder.CreateTable(
                name: "CoopTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultCode = table.Column<int>(type: "int", nullable: false),
                    ResultMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConversationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShortCode = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MemberNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginatorConversationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MerchantRequestId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheckoutRequestId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoopTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Counties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CountyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Headquarters = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dividends",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SavingsAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DividendAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DividendType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalDividendPool = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProcessDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Paid = table.Column<bool>(type: "bit", nullable: false),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dividends", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Endmain",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MinuteNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MeetingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AmtApproved = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Accepted = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ChairSigned = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecSigned = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MembSigned = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reasons = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Endmain", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "GeneralLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Transdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Debits = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Credits = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AccBal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Chequeno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Glname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralLedgers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GLSETUP",
                columns: table => new
                {
                    GlId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Glcode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Glaccname = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Glacctype = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GlAccMainGroup = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Glaccgroup = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Normalbal = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Glaccstatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Bal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrCode = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AuditOrg = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AuditDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Curr = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Actuals = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Budgetted = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TransDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSubLedger = table.Column<bool>(type: "bit", nullable: true),
                    AccCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TrialBalance = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GlOrder = table.Column<int>(type: "int", nullable: true),
                    Used = table.Column<int>(type: "int", nullable: true),
                    PrintOrder = table.Column<int>(type: "int", nullable: true),
                    BalanceSheet = table.Column<int>(type: "int", nullable: true),
                    GlType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CurrentBal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EoyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EoyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Main = table.Column<bool>(type: "bit", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OpeningBal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewGlOpeningBal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewGlOpeningBalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<bool>(type: "bit", nullable: false),
                    IsSuspense = table.Column<bool>(type: "bit", nullable: false),
                    IsREarning = table.Column<bool>(type: "bit", nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GLSETUP", x => x.GlId);
                });

            migrationBuilder.CreateTable(
                name: "Gltransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DrAccNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CrAccNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Temp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransDescript = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cash = table.Column<int>(type: "int", nullable: false),
                    DocPosted = table.Column<int>(type: "int", nullable: false),
                    ChequeNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dregard = table.Column<bool>(type: "bit", nullable: true),
                    Recon = table.Column<bool>(type: "bit", nullable: true),
                    TransactionNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Module = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReconId = table.Column<int>(type: "int", nullable: false),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gltransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Journals",
                columns: table => new
                {
                    JVID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VNO = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ACCNO = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    NAME = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    NARATION = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    MEMBERNO = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SHARETYPE = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Loanno = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AMOUNT = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TRANSTYPE = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    AUDITID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TRANSDATE = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AUDITDATE = table.Column<DateTime>(type: "datetime2", nullable: false),
                    POSTED = table.Column<bool>(type: "bit", nullable: false),
                    POSTEDDATE = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Transactionno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journals", x => x.JVID);
                });

            migrationBuilder.CreateTable(
                name: "JournalsListing",
                columns: table => new
                {
                    JLID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VNO = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ACCNO = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    NAME = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    NARATION = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    MEMBERNO = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SHARETYPE = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Loanno = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AMOUNT_DR = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AMOUNT_CR = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AMOUNT = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TRANSTYPE = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    AUDITID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TRANSDATE = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AUDITDATE = table.Column<DateTime>(type: "datetime2", nullable: false),
                    POSTED = table.Column<bool>(type: "bit", nullable: false),
                    POSTEDDATE = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Transactionno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalsListing", x => x.JLID);
                });

            migrationBuilder.CreateTable(
                name: "Loanbal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LoanCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MemberNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IntrOwed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Installments = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IntrOwed2 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FirstDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RepayRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Duedate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IntrCharged = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Interest = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Companycode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Penalty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RepayRate2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RepayMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cleared = table.Column<bool>(type: "bit", nullable: false),
                    AutoCalc = table.Column<bool>(type: "bit", nullable: false),
                    IntrAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RepayPeriod = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IntBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InterestAccrued = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Defaulter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Processdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Receiptno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cease = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nextduedate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Year = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Month = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RepayMode = table.Column<short>(type: "smallint", nullable: false),
                    Gperiod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Run = table.Column<long>(type: "bigint", nullable: true),
                    SerialNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loanbal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Loanguar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LoanNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Collateral = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Transfered = table.Column<bool>(type: "bit", nullable: false),
                    Transdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FullNames = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tguaranto = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loanguar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Loans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LoanCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApplicDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LoanAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RepayPeriod = table.Column<int>(type: "int", nullable: true),
                    PremiumPayable = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Phcf = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalPremium = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JobGrp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WitMemberNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WitSigned = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupMemberNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupSigned = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreparedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddSecurity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Insurance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InsPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InsCalcType = table.Column<int>(type: "int", nullable: true),
                    Posted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aamount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Guaranteed = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cshares = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxLoanamt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Grosspay = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Refinancing = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Loancount = table.Column<long>(type: "bigint", nullable: true),
                    Bridging = table.Column<bool>(type: "bit", nullable: true),
                    RepayMethod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Interest = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TransactionNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Sourceofrepayment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Repayrate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Sharecapital = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Rescheduledate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Gperiod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Run = table.Column<int>(type: "int", nullable: true),
                    Run2 = table.Column<int>(type: "int", nullable: true),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loans", x => x.Id);
                    table.UniqueConstraint("AK_Loans_LoanNo", x => x.LoanNo);
                });

            migrationBuilder.CreateTable(
                name: "LOANSCHD",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Period = table.Column<int>(type: "int", nullable: true),
                    Principal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Interest = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FmtPer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    contrib = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Sharebalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOANSCHD", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Loantypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoanType1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueChain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LoanProduct = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MinimumPaidForBridging = table.Column<short>(type: "smallint", nullable: false),
                    MinimumPaidForTopup = table.Column<short>(type: "smallint", nullable: false),
                    LoanAcc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InterestAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverpaymentAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LoanOverpaymentAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PenaltyAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SchemeCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RepayPeriod = table.Column<int>(type: "int", nullable: true),
                    Interest = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Guarantor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UseintRange = table.Column<bool>(type: "bit", nullable: true),
                    Accno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IntAccno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EarningRation = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Penalty = table.Column<int>(type: "int", nullable: false),
                    DefaultLoanno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nssf = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Bankloan = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherDeduct = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: true),
                    MaxLoans = table.Column<int>(type: "int", nullable: true),
                    ContraAccount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bridging = table.Column<int>(type: "int", nullable: false),
                    Processingfee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ContraAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GracePeriod = table.Column<int>(type: "int", nullable: false),
                    Repaymethod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PremiumAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PremiumContraAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bridgefees = table.Column<double>(type: "float", nullable: true),
                    Periodrepaid = table.Column<int>(type: "int", nullable: true),
                    WaitingPeriod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccruedAcc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ppacc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Mdtei = table.Column<short>(type: "smallint", nullable: false),
                    Intrecovery = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsMain = table.Column<bool>(type: "bit", nullable: false),
                    SelfGuarantee = table.Column<bool>(type: "bit", nullable: true),
                    ReceivableAcc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsFimg = table.Column<bool>(type: "bit", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MobileLoan = table.Column<bool>(type: "bit", nullable: true),
                    MobileCreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MobileCreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loantypes", x => x.Id);
                    table.UniqueConstraint("AK_Loantypes_LoanCode", x => x.LoanCode);
                });

            migrationBuilder.CreateTable(
                name: "MemberNumberCounters",
                columns: table => new
                {
                    CompanyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberNumberCounters", x => x.CompanyCode);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    MemberNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StaffNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Idno = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Surname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OtherNames = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sex = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dob = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Employer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dept = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Terms = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PresentAddr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OfficeTelNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HomeAddr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HomeTelNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegFee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InitShares = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AsAtDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MonthlyContr = table.Column<double>(type: "float", nullable: true),
                    ApplicDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Signed = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Accepted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Archived = table.Column<bool>(type: "bit", nullable: true),
                    Withdrawn = table.Column<bool>(type: "bit", nullable: true),
                    Province = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    District = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Station = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cigcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Photo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShareCap = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BankCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Posted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitsharesTransfered = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Transferdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LoanBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InterestBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FormFilled = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Accno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Memberwitrawaldate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Dormant = table.Column<int>(type: "int", nullable: true),
                    MemberDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MobileNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgentId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNo = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Entrance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    Mstatus = table.Column<bool>(type: "bit", nullable: true),
                    MembershipType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Age = table.Column<int>(type: "int", nullable: true),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Run = table.Column<int>(type: "int", nullable: true),
                    ProfilePicture = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ProfileString = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => new { x.MemberNo, x.CompanyCode });
                });

            migrationBuilder.CreateTable(
                name: "PaymentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    DefaultExpenseAccountNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Privilages",
                columns: table => new
                {
                    PrivilageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrganizationCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Privilages", x => x.PrivilageId);
                });

            migrationBuilder.CreateTable(
                name: "Repay",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MemberNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateReceived = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentNo = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Principal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Interest = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IntrCharged = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IntrOwed = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IntrAccrued = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Penalty = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LoanBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ReceiptNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Chequeno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RepayRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Locked = table.Column<bool>(type: "bit", nullable: true),
                    Posted = table.Column<bool>(type: "bit", nullable: true),
                    Accrued = table.Column<bool>(type: "bit", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ch = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nextduedate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RepayId = table.Column<long>(type: "bigint", nullable: true),
                    Transby = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IntBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Loancode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Interestaccrued = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Mrno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Mrcleared = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Transno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BridgeInterest = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BrgLoan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Statementdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LoanAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InterestAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContraAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cash = table.Column<int>(type: "int", nullable: true),
                    CashBookDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Dregard = table.Column<int>(type: "int", nullable: false),
                    Offs = table.Column<int>(type: "int", nullable: false),
                    TransactionNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Run = table.Column<long>(type: "bigint", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repay", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaccoParram",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaccoName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoOfEmployees = table.Column<int>(type: "int", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Town = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhysicalAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheckOffDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MembershipMaturityMonths = table.Column<int>(type: "int", nullable: false),
                    WithdrawalNoticeDays = table.Column<int>(type: "int", nullable: false),
                    DividendProcessingDays = table.Column<int>(type: "int", nullable: false),
                    MaxGuarantor = table.Column<int>(type: "int", nullable: false),
                    MinGuarantor = table.Column<int>(type: "int", nullable: true),
                    DefaultCurrency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultRounding = table.Column<int>(type: "int", nullable: true),
                    SignificantLoanBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActionOnDefaultedInterest = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Suspense = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetainedEarnings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Creditors = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaccoParram", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shares",
                columns: table => new
                {
                    MemberNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Sharescode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TotalShares = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TransDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastDivDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Loanbal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Statementshares = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Initshares = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shares", x => new { x.MemberNo, x.Sharescode });
                });

            migrationBuilder.CreateTable(
                name: "Sharetypes",
                columns: table => new
                {
                    SharesCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SharesType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SharesAcc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlacePeriod = table.Column<int>(type: "int", nullable: true),
                    LoanToShareRatio = table.Column<float>(type: "real", nullable: true),
                    Issharecapital = table.Column<int>(type: "int", nullable: true),
                    Interest = table.Column<decimal>(type: "decimal(18,2)", precision: 5, scale: 4, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Guarantor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Accno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Shareboost = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsMainShares = table.Column<bool>(type: "bit", nullable: false),
                    UsedToGuarantee = table.Column<bool>(type: "bit", nullable: false),
                    ContraAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsedToOffset = table.Column<bool>(type: "bit", nullable: false),
                    Withdrawable = table.Column<bool>(type: "bit", nullable: false),
                    Loanquaranto = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    MinAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Ppacc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LowerLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ElseRatio = table.Column<decimal>(type: "decimal(18,2)", precision: 5, scale: 4, nullable: false),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sharetypes", x => x.SharesCode);
                    table.UniqueConstraint("AK_Sharetypes_SharesCode_CompanyCode", x => new { x.SharesCode, x.CompanyCode });
                });

            migrationBuilder.CreateTable(
                name: "SmsMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MessageContent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApiSecret = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SenderId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ShortCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SendOnRegistration = table.Column<bool>(type: "bit", nullable: false),
                    SendOnWithdrawal = table.Column<bool>(type: "bit", nullable: false),
                    SendOnLoanApproval = table.Column<bool>(type: "bit", nullable: false),
                    SendOnShareTransfer = table.Column<bool>(type: "bit", nullable: false),
                    SendOnContribution = table.Column<bool>(type: "bit", nullable: false),
                    SendOnLoanRepayment = table.Column<bool>(type: "bit", nullable: false),
                    SendOnAGM = table.Column<bool>(type: "bit", nullable: false),
                    SendOnDeposits = table.Column<bool>(type: "bit", nullable: false),
                    CostPerSms = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ApiEndpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TemplateContent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transaction_Detail",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultCode = table.Column<int>(type: "int", nullable: false),
                    ResultMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConversationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShortCode = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MemberNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginatorConversationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MerchantRequestId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheckoutRequestId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transaction_Detail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions2",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Companycode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceiptNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentMode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ContributionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DepositedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RunE = table.Column<int>(type: "int", nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions2", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts1",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserLoginId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserGroup = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cigcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PassExpire = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Superuser = table.Column<long>(type: "bigint", nullable: true),
                    MemberNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignGl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DepCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Levels = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Authorize = table.Column<bool>(type: "bit", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubCounty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ward = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sign = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Expirydate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Userstatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PasswordStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Euser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VendorId = table.Column<long>(type: "bigint", nullable: true),
                    Count = table.Column<long>(type: "bigint", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Branchcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailedAttempts = table.Column<int>(type: "int", nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts1", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "UserGroups",
                columns: table => new
                {
                    UserGroupId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrganizationCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroups", x => x.UserGroupId);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    Address = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PublicKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrivateKeyEncrypted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastActivity = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Address);
                });

            migrationBuilder.CreateTable(
                name: "WicciClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Surname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Othername = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Idno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PinStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecretWord = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unsubscribe = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Acs = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subscription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserRole = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WicciClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlockchainTransactions",
                columns: table => new
                {
                    TransactionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BlockHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MemberNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OffChainReferenceId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockchainTransactions", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_BlockchainTransactions_Blocks_BlockHash",
                        column: x => x.BlockHash,
                        principalTable: "Blocks",
                        principalColumn: "BlockHash");
                });

            migrationBuilder.CreateTable(
                name: "CIGs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GigCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GigName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Chairperson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalMembers = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CIGs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CIGs_Companies_CompanyCode",
                        column: x => x.CompanyCode,
                        principalTable: "Companies",
                        principalColumn: "CompanyCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubCounties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubCountyCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubCountyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CountyId = table.Column<int>(type: "int", nullable: false),
                    Headquarters = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubCounties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubCounties_Counties_CountyId",
                        column: x => x.CountyId,
                        principalTable: "Counties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoanSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InstallmentNo = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InterestAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalInstallment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalancePrincipal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceInterest = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidPrincipal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidInterest = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingPrincipal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingInterest = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PenaltyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MinimumPayment = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsFlexible = table.Column<bool>(type: "bit", nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DaysOverdue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanSchedules_Loans_LoanNo",
                        column: x => x.LoanNo,
                        principalTable: "Loans",
                        principalColumn: "LoanNo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Penalty",
                columns: table => new
                {
                    LoanCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Fixed"),
                    Rate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Monthly"),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ChargeItem = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Penalty = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Penalty", x => x.LoanCode);
                    table.ForeignKey(
                        name: "FK_Penalty_Loantypes_LoanCode",
                        column: x => x.LoanCode,
                        principalTable: "Loantypes",
                        principalColumn: "LoanCode",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CHEQUES",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MemberNo = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ChequeNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BalForward = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ProcessingFee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IntAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IntrOwed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CollectorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CollectorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateIssued = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClerkStaffNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClerkName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reasons = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AmountIssued = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Firstdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Premium = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Offsetamount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amountinword = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Voucherno = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Voucheramount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Refloan = table.Column<bool>(type: "bit", nullable: false),
                    Paymethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TransactionNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrgAmt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LoanAcc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContraAcc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PremiumAcc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Dregard = table.Column<int>(type: "int", nullable: false),
                    PaidBf = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CHEQUES", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CHEQUES_Members_MemberNo_CompanyCode",
                        columns: x => new { x.MemberNo, x.CompanyCode },
                        principalTable: "Members",
                        principalColumns: new[] { "MemberNo", "CompanyCode" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberWithdrawals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WithdrawalNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MemberNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WithdrawalDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    WithdrawalType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalSharesValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDeposits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingLoans = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetPayableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PenaltiesAndDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankAccountNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccountName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChequeNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MobileNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalComments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProcessedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GlAccountNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    GlAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MemberId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberWithdrawals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberWithdrawals_Members_MemberNo_CompanyCode",
                        columns: x => new { x.MemberNo, x.CompanyCode },
                        principalTable: "Members",
                        principalColumns: new[] { "MemberNo", "CompanyCode" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NextOfKeens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PhoneNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhysicalAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IdNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PassportNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Employer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Occupation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BenefitPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PriorityOrder = table.Column<int>(type: "int", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NextOfKeens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NextOfKeens_Members_MemberNo_CompanyCode",
                        columns: x => new { x.MemberNo, x.CompanyCode },
                        principalTable: "Members",
                        principalColumns: new[] { "MemberNo", "CompanyCode" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Contribs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StaffNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContrDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepositedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ShareBal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TransBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ChequeNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiptNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Locked = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Posted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Schemecode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransferDesc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MrCleared = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Mrno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Offset = table.Column<bool>(type: "bit", nullable: true),
                    TransDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SharesAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContraAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CashBookdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Dregard = table.Column<int>(type: "int", nullable: true),
                    Offs = table.Column<int>(type: "int", nullable: true),
                    Sharescode = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TransactionNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Run = table.Column<long>(type: "bigint", nullable: true),
                    Run2 = table.Column<int>(type: "int", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contribs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contribs_Members_MemberNo_CompanyCode",
                        columns: x => new { x.MemberNo, x.CompanyCode },
                        principalTable: "Members",
                        principalColumns: new[] { "MemberNo", "CompanyCode" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contribs_Sharetypes_Sharescode",
                        column: x => x.Sharescode,
                        principalTable: "Sharetypes",
                        principalColumn: "SharesCode");
                });

            migrationBuilder.CreateTable(
                name: "ContribShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocalId = table.Column<int>(type: "int", nullable: true),
                    MemberNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoanNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContrDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepositedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShareCapitalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DepositsAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PassBookAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Donor = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LoanAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RegFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiptNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Sharescode = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TransactionNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContribShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContribShares_Sharetypes_Sharescode",
                        column: x => x.Sharescode,
                        principalTable: "Sharetypes",
                        principalColumn: "SharesCode");
                });

            migrationBuilder.CreateTable(
                name: "ShareTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransferNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransferorMemberNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransfereeMemberNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SharesCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NumberOfShares = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PricePerShare = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransferDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransferType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransferorBalanceBefore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransferorBalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransfereeBalanceBefore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransfereeBalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaymentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransferFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StampDuty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OtherCharges = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCharges = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FeesGlAccountNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalComments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TransferDocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShareTransfers_Members_TransfereeMemberNo_CompanyCode",
                        columns: x => new { x.TransfereeMemberNo, x.CompanyCode },
                        principalTable: "Members",
                        principalColumns: new[] { "MemberNo", "CompanyCode" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShareTransfers_Members_TransferorMemberNo_CompanyCode",
                        columns: x => new { x.TransferorMemberNo, x.CompanyCode },
                        principalTable: "Members",
                        principalColumns: new[] { "MemberNo", "CompanyCode" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShareTransfers_Sharetypes_SharesCode_CompanyCode",
                        columns: x => new { x.SharesCode, x.CompanyCode },
                        principalTable: "Sharetypes",
                        principalColumns: new[] { "SharesCode", "CompanyCode" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePrivileges",
                columns: table => new
                {
                    UserGroupId = table.Column<int>(type: "int", nullable: false),
                    PrivilageId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePrivileges", x => new { x.UserGroupId, x.PrivilageId });
                    table.ForeignKey(
                        name: "FK_RolePrivileges_Privilages_PrivilageId",
                        column: x => x.PrivilageId,
                        principalTable: "Privilages",
                        principalColumn: "PrivilageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePrivileges_UserGroups_UserGroupId",
                        column: x => x.UserGroupId,
                        principalTable: "UserGroups",
                        principalColumn: "UserGroupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "COLLATERALS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Coldescription = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Percentage = table.Column<double>(type: "float", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COLLATERALS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COLLATERALS_BlockchainTransactions_BlockchainTxId",
                        column: x => x.BlockchainTxId,
                        principalTable: "BlockchainTransactions",
                        principalColumn: "TransactionId");
                });

            migrationBuilder.CreateTable(
                name: "COLLOANGUAR",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MemberNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DocNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Mktvalue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LoanNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AuditId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COLLOANGUAR", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COLLOANGUAR_BlockchainTransactions_BlockchainTxId",
                        column: x => x.BlockchainTxId,
                        principalTable: "BlockchainTransactions",
                        principalColumn: "TransactionId");
                });

            migrationBuilder.CreateTable(
                name: "Wards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WardCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WardName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubCountyId = table.Column<int>(type: "int", nullable: false),
                    Constituency = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockchainTxId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Wards_SubCounties_SubCountyId",
                        column: x => x.SubCountyId,
                        principalTable: "SubCounties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WithdrawalApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WithdrawalId = table.Column<int>(type: "int", nullable: false),
                    WithdrawalNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApprovalLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithdrawalApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WithdrawalApprovals_MemberWithdrawals_WithdrawalId",
                        column: x => x.WithdrawalId,
                        principalTable: "MemberWithdrawals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WithdrawalDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WithdrawalId = table.Column<int>(type: "int", nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithdrawalDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WithdrawalDocuments_MemberWithdrawals_WithdrawalId",
                        column: x => x.WithdrawalId,
                        principalTable: "MemberWithdrawals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShareTransferApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransferId = table.Column<int>(type: "int", nullable: false),
                    TransferNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApprovalLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareTransferApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShareTransferApprovals_ShareTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "ShareTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShareTransferDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransferId = table.Column<int>(type: "int", nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareTransferDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShareTransferDocuments_ShareTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "ShareTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockchainTransactions_BlockHash",
                table: "BlockchainTransactions",
                column: "BlockHash");

            migrationBuilder.CreateIndex(
                name: "IX_BlockchainTransactions_MemberNo",
                table: "BlockchainTransactions",
                column: "MemberNo");

            migrationBuilder.CreateIndex(
                name: "IX_BlockchainTransactions_Timestamp",
                table: "BlockchainTransactions",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_BlockchainTransactions_TransactionId",
                table: "BlockchainTransactions",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlockchainTransactions_TransactionType",
                table: "BlockchainTransactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_BlockHash",
                table: "Blocks",
                column: "BlockHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_PreviousHash",
                table: "Blocks",
                column: "PreviousHash");

            migrationBuilder.CreateIndex(
                name: "IX_CHEQUES_MemberNo_CompanyCode",
                table: "CHEQUES",
                columns: new[] { "MemberNo", "CompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_CIGs_CompanyCode",
                table: "CIGs",
                column: "CompanyCode");

            migrationBuilder.CreateIndex(
                name: "IX_CIGs_GigCode",
                table: "CIGs",
                column: "GigCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CIGs_Status",
                table: "CIGs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_COLLATERALS_BlockchainTxId",
                table: "COLLATERALS",
                column: "BlockchainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_COLLOANGUAR_BlockchainTxId",
                table: "COLLOANGUAR",
                column: "BlockchainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_Contribs_BlockchainTxId",
                table: "Contribs",
                column: "BlockchainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_Contribs_MemberNo",
                table: "Contribs",
                column: "MemberNo");

            migrationBuilder.CreateIndex(
                name: "IX_Contribs_MemberNo_CompanyCode",
                table: "Contribs",
                columns: new[] { "MemberNo", "CompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Contribs_Sharescode",
                table: "Contribs",
                column: "Sharescode");

            migrationBuilder.CreateIndex(
                name: "IX_ContribShares_BlockchainTxId",
                table: "ContribShares",
                column: "BlockchainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_ContribShares_MemberNo",
                table: "ContribShares",
                column: "MemberNo");

            migrationBuilder.CreateIndex(
                name: "IX_ContribShares_Sharescode",
                table: "ContribShares",
                column: "Sharescode");

            migrationBuilder.CreateIndex(
                name: "IX_LoanSchedules_LoanNo",
                table: "LoanSchedules",
                column: "LoanNo");

            migrationBuilder.CreateIndex(
                name: "IX_Members_BlockchainTxId",
                table: "Members",
                column: "BlockchainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_Idno",
                table: "Members",
                column: "Idno");

            migrationBuilder.CreateIndex(
                name: "IX_Members_MemberNo",
                table: "Members",
                column: "MemberNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_PhoneNo",
                table: "Members",
                column: "PhoneNo");

            migrationBuilder.CreateIndex(
                name: "IX_MemberWithdrawals_MemberNo_CompanyCode",
                table: "MemberWithdrawals",
                columns: new[] { "MemberNo", "CompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_NextOfKeens_MemberNo_CompanyCode",
                table: "NextOfKeens",
                columns: new[] { "MemberNo", "CompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePrivileges_PrivilageId",
                table: "RolePrivileges",
                column: "PrivilageId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareTransferApprovals_TransferId",
                table: "ShareTransferApprovals",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareTransferDocuments_TransferId",
                table: "ShareTransferDocuments",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareTransfers_SharesCode_CompanyCode",
                table: "ShareTransfers",
                columns: new[] { "SharesCode", "CompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ShareTransfers_TransfereeMemberNo_CompanyCode",
                table: "ShareTransfers",
                columns: new[] { "TransfereeMemberNo", "CompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ShareTransfers_TransferorMemberNo_CompanyCode",
                table: "ShareTransfers",
                columns: new[] { "TransferorMemberNo", "CompanyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Sharetypes_SharesCode_CompanyCode",
                table: "Sharetypes",
                columns: new[] { "SharesCode", "CompanyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubCounties_CountyId",
                table: "SubCounties",
                column: "CountyId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions2_BlockchainTxId",
                table: "Transactions2",
                column: "BlockchainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions2_MemberNo",
                table: "Transactions2",
                column: "MemberNo");

            migrationBuilder.CreateIndex(
                name: "IX_Wards_SubCountyId",
                table: "Wards",
                column: "SubCountyId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalApprovals_WithdrawalId",
                table: "WithdrawalApprovals",
                column: "WithdrawalId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalDocuments_WithdrawalId",
                table: "WithdrawalDocuments",
                column: "WithdrawalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "ApiTables");

            migrationBuilder.DropTable(
                name: "ApiTransactions");

            migrationBuilder.DropTable(
                name: "APPRAISAL");

            migrationBuilder.DropTable(
                name: "AssetsRegisters");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "AuditTrail");

            migrationBuilder.DropTable(
                name: "Banks");

            migrationBuilder.DropTable(
                name: "CHEQUES");

            migrationBuilder.DropTable(
                name: "CIGs");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "COLLATERALS");

            migrationBuilder.DropTable(
                name: "COLLOANGUAR");

            migrationBuilder.DropTable(
                name: "Contribs");

            migrationBuilder.DropTable(
                name: "ContribShares");

            migrationBuilder.DropTable(
                name: "CoopTransactions");

            migrationBuilder.DropTable(
                name: "Dividends");

            migrationBuilder.DropTable(
                name: "Endmain");

            migrationBuilder.DropTable(
                name: "GeneralLedgers");

            migrationBuilder.DropTable(
                name: "GLSETUP");

            migrationBuilder.DropTable(
                name: "Gltransactions");

            migrationBuilder.DropTable(
                name: "Journals");

            migrationBuilder.DropTable(
                name: "JournalsListing");

            migrationBuilder.DropTable(
                name: "Loanbal");

            migrationBuilder.DropTable(
                name: "Loanguar");

            migrationBuilder.DropTable(
                name: "LOANSCHD");

            migrationBuilder.DropTable(
                name: "LoanSchedules");

            migrationBuilder.DropTable(
                name: "MemberNumberCounters");

            migrationBuilder.DropTable(
                name: "NextOfKeens");

            migrationBuilder.DropTable(
                name: "PaymentTypes");

            migrationBuilder.DropTable(
                name: "Penalty");

            migrationBuilder.DropTable(
                name: "Repay");

            migrationBuilder.DropTable(
                name: "RolePrivileges");

            migrationBuilder.DropTable(
                name: "SaccoParram");

            migrationBuilder.DropTable(
                name: "Shares");

            migrationBuilder.DropTable(
                name: "ShareTransferApprovals");

            migrationBuilder.DropTable(
                name: "ShareTransferDocuments");

            migrationBuilder.DropTable(
                name: "SmsMessages");

            migrationBuilder.DropTable(
                name: "SmsSettings");

            migrationBuilder.DropTable(
                name: "SmsTemplates");

            migrationBuilder.DropTable(
                name: "Transaction_Detail");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Transactions2");

            migrationBuilder.DropTable(
                name: "UserAccounts1");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.DropTable(
                name: "Wards");

            migrationBuilder.DropTable(
                name: "WicciClients");

            migrationBuilder.DropTable(
                name: "WithdrawalApprovals");

            migrationBuilder.DropTable(
                name: "WithdrawalDocuments");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "BlockchainTransactions");

            migrationBuilder.DropTable(
                name: "Loans");

            migrationBuilder.DropTable(
                name: "Loantypes");

            migrationBuilder.DropTable(
                name: "Privilages");

            migrationBuilder.DropTable(
                name: "UserGroups");

            migrationBuilder.DropTable(
                name: "ShareTransfers");

            migrationBuilder.DropTable(
                name: "SubCounties");

            migrationBuilder.DropTable(
                name: "MemberWithdrawals");

            migrationBuilder.DropTable(
                name: "Blocks");

            migrationBuilder.DropTable(
                name: "Sharetypes");

            migrationBuilder.DropTable(
                name: "Counties");

            migrationBuilder.DropTable(
                name: "Members");
        }
    }
}
