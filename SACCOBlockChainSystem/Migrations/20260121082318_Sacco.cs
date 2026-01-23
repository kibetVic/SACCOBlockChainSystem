using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SACCOBlockChainSystem.Migrations
{
    /// <inheritdoc />
    public partial class Sacco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cigcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CountyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    County = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubCounty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ward = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Village = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unitcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contactperson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoYears = table.Column<int>(type: "int", nullable: true),
                    NoEmployees = table.Column<int>(type: "int", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Capital = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Project = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
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
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoopTransactions", x => x.Id);
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
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralLedgers", x => x.Id);
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
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gltransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Loanguars",
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
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loanguars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Loantypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    Penalty = table.Column<bool>(type: "bit", nullable: false),
                    DefaultLoanno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nssf = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Bankloan = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherDeduct = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: true),
                    MaxLoans = table.Column<int>(type: "int", nullable: true),
                    ContraAccount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Bridging = table.Column<bool>(type: "bit", nullable: false),
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
                    Ppacc = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loantypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Members", x => x.Id);
                    table.UniqueConstraint("AK_Members_MemberNo", x => x.MemberNo);
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
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    Ppacc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LowerLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ElseRatio = table.Column<decimal>(type: "decimal(18,2)", precision: 5, scale: 4, nullable: false),
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sharetypes", x => x.SharesCode);
                });

            migrationBuilder.CreateTable(
                name: "TransactionDetails",
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
                    table.PrimaryKey("PK_TransactionDetails", x => x.Id);
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
                name: "Loans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                    BlockchainTxId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loans", x => x.Id);
                    table.UniqueConstraint("AK_Loans_LoanNo", x => x.LoanNo);
                    table.ForeignKey(
                        name: "FK_Loans_Members_MemberNo",
                        column: x => x.MemberNo,
                        principalTable: "Members",
                        principalColumn: "MemberNo",
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
                    CompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                name: "Loanbals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                    Companycode = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    AuditDateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loanbals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Loanbals_Loans_LoanNo",
                        column: x => x.LoanNo,
                        principalTable: "Loans",
                        principalColumn: "LoanNo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Repays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberNo = table.Column<string>(type: "nvarchar(450)", nullable: true),
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
                    BlockchainTxId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repays_Loans_LoanNo",
                        column: x => x.LoanNo,
                        principalTable: "Loans",
                        principalColumn: "LoanNo",
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
                name: "IX_Contribs_BlockchainTxId",
                table: "Contribs",
                column: "BlockchainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_Contribs_MemberNo",
                table: "Contribs",
                column: "MemberNo");

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
                name: "IX_Loanbals_LoanNo",
                table: "Loanbals",
                column: "LoanNo");

            migrationBuilder.CreateIndex(
                name: "IX_Loans_BlockchainTxId",
                table: "Loans",
                column: "BlockchainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_Loans_LoanNo",
                table: "Loans",
                column: "LoanNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_MemberNo",
                table: "Loans",
                column: "MemberNo");

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
                name: "IX_Repays_BlockchainTxId",
                table: "Repays",
                column: "BlockchainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_Repays_LoanNo",
                table: "Repays",
                column: "LoanNo");

            migrationBuilder.CreateIndex(
                name: "IX_Repays_MemberNo",
                table: "Repays",
                column: "MemberNo");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions2_BlockchainTxId",
                table: "Transactions2",
                column: "BlockchainTxId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions2_MemberNo",
                table: "Transactions2",
                column: "MemberNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BlockchainTransactions");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "Contribs");

            migrationBuilder.DropTable(
                name: "ContribShares");

            migrationBuilder.DropTable(
                name: "CoopTransactions");

            migrationBuilder.DropTable(
                name: "GeneralLedgers");

            migrationBuilder.DropTable(
                name: "Gltransactions");

            migrationBuilder.DropTable(
                name: "Loanbals");

            migrationBuilder.DropTable(
                name: "Loanguars");

            migrationBuilder.DropTable(
                name: "Loantypes");

            migrationBuilder.DropTable(
                name: "Repays");

            migrationBuilder.DropTable(
                name: "Shares");

            migrationBuilder.DropTable(
                name: "TransactionDetails");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Transactions2");

            migrationBuilder.DropTable(
                name: "UserAccounts1");

            migrationBuilder.DropTable(
                name: "WicciClients");

            migrationBuilder.DropTable(
                name: "Blocks");

            migrationBuilder.DropTable(
                name: "Sharetypes");

            migrationBuilder.DropTable(
                name: "Loans");

            migrationBuilder.DropTable(
                name: "Members");
        }
    }
}
