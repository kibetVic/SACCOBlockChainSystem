SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Loans](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LoanNo] [nvarchar](50) NOT NULL,
	[MemberNo] [nvarchar](450) NOT NULL,
	[LoanCode] [nvarchar](50) NOT NULL,
	[CompanyCode] [nvarchar](50) NOT NULL,
	[PrincipalAmount] [decimal](18, 2) NOT NULL,
	[ApprovedAmount] [decimal](18, 2) NOT NULL,
	[DisbursedAmount] [decimal](18, 2) NOT NULL,
	[OutstandingPrincipal] [decimal](18, 2) NOT NULL,
	[OutstandingInterest] [decimal](18, 2) NOT NULL,
	[OutstandingPenalty] [decimal](18, 2) NOT NULL,
	[TotalOutstanding] [decimal](18, 2) NOT NULL,
	[InterestRate] [decimal](5, 4) NOT NULL,
	[RepaymentPeriod] [int] NOT NULL,
	[RepaymentFrequency] [int] NOT NULL,
	[InstallmentAmount] [decimal](18, 2) NOT NULL,
	[ApplicationDate] [datetime2](7) NOT NULL,
	[ApprovalDate] [datetime2](7) NULL,
	[DisbursementDate] [datetime2](7) NULL,
	[FirstPaymentDate] [datetime2](7) NULL,
	[MaturityDate] [datetime2](7) NULL,
	[LoanStatus] [nvarchar](30) NOT NULL,
	[Purpose] [nvarchar](500) NOT NULL,
	[Remarks] [nvarchar](500) NULL,
	[LoanTypeId] [int] NOT NULL,
	[HasGuarantors] [bit] NOT NULL,
	[RequiredGuarantors] [int] NOT NULL,
	[AssignedGuarantors] [int] NOT NULL,
	[GuarantorsApproved] [bit] NOT NULL,
	[AppraisalCompleted] [bit] NOT NULL,
	[AppraisalDate] [datetime2](7) NULL,
	[AppraisedBy] [nvarchar](100) NULL,
	[ProcessingFee] [decimal](18, 2) NOT NULL,
	[InsuranceFee] [decimal](18, 2) NOT NULL,
	[LegalFees] [decimal](18, 2) NOT NULL,
	[OtherFees] [decimal](18, 2) NOT NULL,
	[TotalFees] [decimal](18, 2) NOT NULL,
	[NetDisbursement] [decimal](18, 2) NOT NULL,
	[CreatedBy] [nvarchar](100) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[ModifiedBy] [nvarchar](100) NULL,
	[ModifiedAt] [datetime2](7) NULL,
	[BlockchainTxId] [nvarchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Loans_BlockchainTxId] UNIQUE NONCLUSTERED 
(
	[BlockchainTxId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Loans_LoanNo] UNIQUE NONCLUSTERED 
(
	[LoanNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [PrincipalAmount]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [ApprovedAmount]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [DisbursedAmount]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [OutstandingPrincipal]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [OutstandingInterest]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [OutstandingPenalty]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [TotalOutstanding]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [InterestRate]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [RepaymentPeriod]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((1)) FOR [RepaymentFrequency]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [InstallmentAmount]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ('Draft') FOR [LoanStatus]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [LoanTypeId]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [HasGuarantors]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [RequiredGuarantors]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [AssignedGuarantors]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [GuarantorsApproved]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [AppraisalCompleted]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [ProcessingFee]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [InsuranceFee]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [LegalFees]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [OtherFees]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [TotalFees]
GO

ALTER TABLE [dbo].[Loans] ADD  DEFAULT ((0)) FOR [NetDisbursement]
GO

ALTER TABLE [dbo].[Loans]  WITH CHECK ADD  CONSTRAINT [FK_Loans_LoanTypes] FOREIGN KEY([LoanTypeId])
REFERENCES [dbo].[Loantypes] ([Id])
GO

ALTER TABLE [dbo].[Loans] CHECK CONSTRAINT [FK_Loans_LoanTypes]
GO

ALTER TABLE [dbo].[Loans]  WITH CHECK ADD  CONSTRAINT [FK_Loans_Members] FOREIGN KEY([MemberNo])
REFERENCES [dbo].[Members] ([MemberNo])
GO

ALTER TABLE [dbo].[Loans] CHECK CONSTRAINT [FK_Loans_Members]
GO













SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[LoanGuarantors](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LoanNo] [nvarchar](50) NOT NULL,
	[GuarantorMemberNo] [nvarchar](450) NOT NULL,
	[CompanyCode] [nvarchar](50) NOT NULL,
	[GuaranteeAmount] [decimal](18, 2) NOT NULL,
	[AvailableShares] [decimal](18, 2) NOT NULL,
	[LockedAmount] [decimal](18, 2) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[AssignedDate] [datetime2](7) NOT NULL,
	[ApprovedDate] [datetime2](7) NULL,
	[Remarks] [nvarchar](500) NULL,
	[ApprovedBy] [nvarchar](100) NULL,
	[IsActive] [bit] NOT NULL,
	[ReleasedDate] [datetime2](7) NULL,
	[ReleasedBy] [nvarchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_LoanGuarantors] UNIQUE NONCLUSTERED 
(
	[LoanNo] ASC,
	[GuarantorMemberNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[LoanGuarantors] ADD  DEFAULT ((0)) FOR [GuaranteeAmount]
GO

ALTER TABLE [dbo].[LoanGuarantors] ADD  DEFAULT ((0)) FOR [AvailableShares]
GO

ALTER TABLE [dbo].[LoanGuarantors] ADD  DEFAULT ((0)) FOR [LockedAmount]
GO

ALTER TABLE [dbo].[LoanGuarantors] ADD  DEFAULT ('Pending') FOR [Status]
GO

ALTER TABLE [dbo].[LoanGuarantors] ADD  DEFAULT ((1)) FOR [IsActive]
GO

ALTER TABLE [dbo].[LoanGuarantors]  WITH CHECK ADD  CONSTRAINT [FK_LoanGuarantors_Loans] FOREIGN KEY([LoanNo])
REFERENCES [dbo].[Loans] ([LoanNo])
GO

ALTER TABLE [dbo].[LoanGuarantors] CHECK CONSTRAINT [FK_LoanGuarantors_Loans]
GO

ALTER TABLE [dbo].[LoanGuarantors]  WITH CHECK ADD  CONSTRAINT [FK_LoanGuarantors_Members] FOREIGN KEY([GuarantorMemberNo])
REFERENCES [dbo].[Members] ([MemberNo])
GO

ALTER TABLE [dbo].[LoanGuarantors] CHECK CONSTRAINT [FK_LoanGuarantors_Members]
GO













SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[LoanSchedules](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LoanNo] [nvarchar](50) NOT NULL,
	[CompanyCode] [nvarchar](50) NOT NULL,
	[InstallmentNo] [int] NOT NULL,
	[DueDate] [datetime2](7) NOT NULL,
	[PrincipalAmount] [decimal](18, 2) NOT NULL,
	[InterestAmount] [decimal](18, 2) NOT NULL,
	[TotalInstallment] [decimal](18, 2) NOT NULL,
	[BalancePrincipal] [decimal](18, 2) NOT NULL,
	[BalanceInterest] [decimal](18, 2) NOT NULL,
	[BalanceTotal] [decimal](18, 2) NOT NULL,
	[PaidPrincipal] [decimal](18, 2) NOT NULL,
	[PaidInterest] [decimal](18, 2) NOT NULL,
	[PaidTotal] [decimal](18, 2) NOT NULL,
	[OutstandingPrincipal] [decimal](18, 2) NOT NULL,
	[OutstandingInterest] [decimal](18, 2) NOT NULL,
	[OutstandingTotal] [decimal](18, 2) NOT NULL,
	[PenaltyAmount] [decimal](18, 2) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[PaidDate] [datetime2](7) NULL,
	[PaymentReference] [nvarchar](50) NULL,
	[DaysOverdue] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_LoanSchedules] UNIQUE NONCLUSTERED 
(
	[LoanNo] ASC,
	[InstallmentNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [PrincipalAmount]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [InterestAmount]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [TotalInstallment]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [BalancePrincipal]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [BalanceInterest]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [BalanceTotal]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [PaidPrincipal]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [PaidInterest]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [PaidTotal]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [OutstandingPrincipal]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [OutstandingInterest]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [OutstandingTotal]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [PenaltyAmount]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ('Pending') FOR [Status]
GO

ALTER TABLE [dbo].[LoanSchedules] ADD  DEFAULT ((0)) FOR [DaysOverdue]
GO

ALTER TABLE [dbo].[LoanSchedules]  WITH CHECK ADD  CONSTRAINT [FK_LoanSchedules_Loans] FOREIGN KEY([LoanNo])
REFERENCES [dbo].[Loans] ([LoanNo])
GO

ALTER TABLE [dbo].[LoanSchedules] CHECK CONSTRAINT [FK_LoanSchedules_Loans]
GO













SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[LoanDocuments](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LoanNo] [nvarchar](50) NOT NULL,
	[CompanyCode] [nvarchar](50) NOT NULL,
	[DocumentName] [nvarchar](100) NOT NULL,
	[DocumentType] [nvarchar](50) NOT NULL,
	[Description] [nvarchar](500) NULL,
	[FilePath] [nvarchar](max) NOT NULL,
	[FileName] [nvarchar](255) NULL,
	[FileSize] [bigint] NOT NULL,
	[ContentType] [nvarchar](50) NOT NULL,
	[UploadedDate] [datetime2](7) NOT NULL,
	[UploadedBy] [nvarchar](100) NOT NULL,
	[IsVerified] [bit] NOT NULL,
	[VerifiedDate] [datetime2](7) NULL,
	[VerifiedBy] [nvarchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[LoanDocuments] ADD  DEFAULT ((0)) FOR [FileSize]
GO

ALTER TABLE [dbo].[LoanDocuments] ADD  DEFAULT ((0)) FOR [IsVerified]
GO

ALTER TABLE [dbo].[LoanDocuments]  WITH CHECK ADD  CONSTRAINT [FK_LoanDocuments_Loans] FOREIGN KEY([LoanNo])
REFERENCES [dbo].[Loans] ([LoanNo])
GO

ALTER TABLE [dbo].[LoanDocuments] CHECK CONSTRAINT [FK_LoanDocuments_Loans]
GO












SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[LoanDisbursements](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LoanNo] [nvarchar](50) NOT NULL,
	[MemberNo] [nvarchar](450) NOT NULL,
	[CompanyCode] [nvarchar](50) NOT NULL,
	[DisbursementNo] [nvarchar](50) NOT NULL,
	[DisbursedAmount] [decimal](18, 2) NOT NULL,
	[ProcessingFee] [decimal](18, 2) NOT NULL,
	[InsuranceFee] [decimal](18, 2) NOT NULL,
	[LegalFees] [decimal](18, 2) NOT NULL,
	[OtherFees] [decimal](18, 2) NOT NULL,
	[TotalDeductions] [decimal](18, 2) NOT NULL,
	[NetAmount] [decimal](18, 2) NOT NULL,
	[DisbursementDate] [datetime2](7) NOT NULL,
	[DisbursementMethod] [nvarchar](50) NULL,
	[BankName] [nvarchar](100) NULL,
	[BankAccountNo] [nvarchar](50) NULL,
	[ChequeNo] [nvarchar](50) NULL,
	[MobileNo] [nvarchar](100) NULL,
	[DisbursedBy] [nvarchar](200) NULL,
	[AuthorizedBy] [nvarchar](200) NULL,
	[AuthorizationDate] [datetime2](7) NULL,
	[VoucherNo] [nvarchar](50) NULL,
	[Remarks] [nvarchar](500) NULL,
	[Status] [nvarchar](20) NOT NULL,
	[BlockchainTxId] [nvarchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Disbursement_Loan] UNIQUE NONCLUSTERED 
(
	[LoanNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_DisbursementNo] UNIQUE NONCLUSTERED 
(
	[DisbursementNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_LoanDisbursements_BlockchainTxId] UNIQUE NONCLUSTERED 
(
	[BlockchainTxId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[LoanDisbursements] ADD  DEFAULT ((0)) FOR [DisbursedAmount]
GO

ALTER TABLE [dbo].[LoanDisbursements] ADD  DEFAULT ((0)) FOR [ProcessingFee]
GO

ALTER TABLE [dbo].[LoanDisbursements] ADD  DEFAULT ((0)) FOR [InsuranceFee]
GO

ALTER TABLE [dbo].[LoanDisbursements] ADD  DEFAULT ((0)) FOR [LegalFees]
GO

ALTER TABLE [dbo].[LoanDisbursements] ADD  DEFAULT ((0)) FOR [OtherFees]
GO

ALTER TABLE [dbo].[LoanDisbursements] ADD  DEFAULT ((0)) FOR [TotalDeductions]
GO

ALTER TABLE [dbo].[LoanDisbursements] ADD  DEFAULT ((0)) FOR [NetAmount]
GO

ALTER TABLE [dbo].[LoanDisbursements] ADD  DEFAULT ('Pending') FOR [Status]
GO

ALTER TABLE [dbo].[LoanDisbursements]  WITH CHECK ADD  CONSTRAINT [FK_LoanDisbursements_Loans] FOREIGN KEY([LoanNo])
REFERENCES [dbo].[Loans] ([LoanNo])
GO

ALTER TABLE [dbo].[LoanDisbursements] CHECK CONSTRAINT [FK_LoanDisbursements_Loans]
GO

ALTER TABLE [dbo].[LoanDisbursements]  WITH CHECK ADD  CONSTRAINT [FK_LoanDisbursements_Members] FOREIGN KEY([MemberNo])
REFERENCES [dbo].[Members] ([MemberNo])
GO

ALTER TABLE [dbo].[LoanDisbursements] CHECK CONSTRAINT [FK_LoanDisbursements_Members]
GO
















SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[LoanAppraisals](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LoanNo] [nvarchar](50) NOT NULL,
	[CompanyCode] [nvarchar](50) NOT NULL,
	[AppliedAmount] [decimal](18, 2) NOT NULL,
	[RecommendedAmount] [decimal](18, 2) NOT NULL,
	[RecommendedInterestRate] [decimal](5, 4) NOT NULL,
	[RecommendedPeriod] [int] NOT NULL,
	[MemberSharesValue] [decimal](18, 2) NOT NULL,
	[MemberDepositsValue] [decimal](18, 2) NOT NULL,
	[MonthlyIncome] [decimal](18, 2) NOT NULL,
	[ExistingLoanObligations] [decimal](18, 2) NOT NULL,
	[DisposableIncome] [decimal](18, 2) NOT NULL,
	[DebtToIncomeRatio] [decimal](5, 2) NOT NULL,
	[CreditScore] [int] NOT NULL,
	[ExistingLoanDefault] [bit] NOT NULL,
	[LoanHistoryRating] [int] NOT NULL,
	[AppraisalDecision] [nvarchar](20) NOT NULL,
	[AppraisalNotes] [nvarchar](1000) NOT NULL,
	[RiskFactors] [nvarchar](500) NULL,
	[MitigationFactors] [nvarchar](500) NULL,
	[AppraisedBy] [nvarchar](100) NOT NULL,
	[AppraisalDate] [datetime2](7) NOT NULL,
	[VerifiedBy] [nvarchar](100) NULL,
	[VerifiedDate] [datetime2](7) NULL,
	[IsFinal] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_LoanAppraisals_LoanNo] UNIQUE NONCLUSTERED 
(
	[LoanNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [AppliedAmount]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [RecommendedAmount]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [RecommendedInterestRate]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [RecommendedPeriod]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [MemberSharesValue]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [MemberDepositsValue]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [MonthlyIncome]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [ExistingLoanObligations]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [DisposableIncome]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [DebtToIncomeRatio]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [CreditScore]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [ExistingLoanDefault]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [LoanHistoryRating]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ('Pending') FOR [AppraisalDecision]
GO

ALTER TABLE [dbo].[LoanAppraisals] ADD  DEFAULT ((0)) FOR [IsFinal]
GO

ALTER TABLE [dbo].[LoanAppraisals]  WITH CHECK ADD  CONSTRAINT [FK_LoanAppraisals_Loans] FOREIGN KEY([LoanNo])
REFERENCES [dbo].[Loans] ([LoanNo])
GO

ALTER TABLE [dbo].[LoanAppraisals] CHECK CONSTRAINT [FK_LoanAppraisals_Loans]
GO
















SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[LoanApprovals](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LoanNo] [nvarchar](50) NOT NULL,
	[CompanyCode] [nvarchar](50) NOT NULL,
	[ApprovalLevel] [int] NOT NULL,
	[ApprovalStatus] [nvarchar](20) NOT NULL,
	[ApprovedAmount] [decimal](18, 2) NOT NULL,
	[ApprovedInterestRate] [decimal](5, 4) NOT NULL,
	[ApprovedPeriod] [int] NOT NULL,
	[ApprovalComments] [nvarchar](500) NULL,
	[ApprovedBy] [nvarchar](100) NOT NULL,
	[ApprovalDate] [datetime2](7) NOT NULL,
	[ApprovalRole] [nvarchar](50) NULL,
	[RejectedBy] [nvarchar](100) NULL,
	[RejectedDate] [datetime2](7) NULL,
	[RejectionReason] [nvarchar](500) NULL,
	[IsFinalApproval] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[LoanApprovals] ADD  DEFAULT ((0)) FOR [ApprovedAmount]
GO

ALTER TABLE [dbo].[LoanApprovals] ADD  DEFAULT ((0)) FOR [ApprovedInterestRate]
GO

ALTER TABLE [dbo].[LoanApprovals] ADD  DEFAULT ((0)) FOR [ApprovedPeriod]
GO

ALTER TABLE [dbo].[LoanApprovals] ADD  DEFAULT ((0)) FOR [IsFinalApproval]
GO

ALTER TABLE [dbo].[LoanApprovals]  WITH CHECK ADD  CONSTRAINT [FK_LoanApprovals_Loans] FOREIGN KEY([LoanNo])
REFERENCES [dbo].[Loans] ([LoanNo])
GO

ALTER TABLE [dbo].[LoanApprovals] CHECK CONSTRAINT [FK_LoanApprovals_Loans]
GO













SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[LoanAuditTrails](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LoanNo] [nvarchar](50) NOT NULL,
	[MemberNo] [nvarchar](450) NULL,
	[CompanyCode] [nvarchar](50) NOT NULL,
	[Action] [nvarchar](50) NOT NULL,
	[PreviousStatus] [nvarchar](30) NOT NULL,
	[NewStatus] [nvarchar](30) NOT NULL,
	[Description] [nvarchar](1000) NULL,
	[Changes] [nvarchar](500) NULL,
	[PerformedBy] [nvarchar](100) NOT NULL,
	[PerformedByRole] [nvarchar](50) NULL,
	[PerformedDate] [datetime2](7) NOT NULL,
	[IpAddress] [nvarchar](50) NULL,
	[BlockchainTxId] [nvarchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[LoanAuditTrails]  WITH CHECK ADD  CONSTRAINT [FK_LoanAuditTrails_Loans] FOREIGN KEY([LoanNo])
REFERENCES [dbo].[Loans] ([LoanNo])
GO

ALTER TABLE [dbo].[LoanAuditTrails] CHECK CONSTRAINT [FK_LoanAuditTrails_Loans]
GO















SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[LoanRepayments](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LoanNo] [nvarchar](50) NOT NULL,
	[MemberNo] [nvarchar](450) NOT NULL,
	[CompanyCode] [nvarchar](50) NOT NULL,
	[ReceiptNo] [nvarchar](50) NOT NULL,
	[PaymentDate] [datetime2](7) NOT NULL,
	[AmountPaid] [decimal](18, 2) NOT NULL,
	[PenaltyAllocated] [decimal](18, 2) NOT NULL,
	[InterestAllocated] [decimal](18, 2) NOT NULL,
	[PrincipalAllocated] [decimal](18, 2) NOT NULL,
	[OverpaymentAmount] [decimal](18, 2) NOT NULL,
	[BalanceAfterPayment] [decimal](18, 2) NOT NULL,
	[PaymentMethod] [nvarchar](50) NULL,
	[ReferenceNo] [nvarchar](100) NULL,
	[ReceivedBy] [nvarchar](200) NULL,
	[Remarks] [nvarchar](500) NULL,
	[Status] [nvarchar](20) NOT NULL,
	[ReversedDate] [datetime2](7) NULL,
	[ReversedBy] [nvarchar](100) NULL,
	[ReversalReason] [nvarchar](500) NULL,
	[BlockchainTxId] [nvarchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_LoanRepayments_BlockchainTxId] UNIQUE NONCLUSTERED 
(
	[BlockchainTxId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_LoanRepayments_ReceiptNo] UNIQUE NONCLUSTERED 
(
	[ReceiptNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[LoanRepayments] ADD  DEFAULT ((0)) FOR [AmountPaid]
GO

ALTER TABLE [dbo].[LoanRepayments] ADD  DEFAULT ((0)) FOR [PenaltyAllocated]
GO

ALTER TABLE [dbo].[LoanRepayments] ADD  DEFAULT ((0)) FOR [InterestAllocated]
GO

ALTER TABLE [dbo].[LoanRepayments] ADD  DEFAULT ((0)) FOR [PrincipalAllocated]
GO

ALTER TABLE [dbo].[LoanRepayments] ADD  DEFAULT ((0)) FOR [OverpaymentAmount]
GO

ALTER TABLE [dbo].[LoanRepayments] ADD  DEFAULT ((0)) FOR [BalanceAfterPayment]
GO

ALTER TABLE [dbo].[LoanRepayments] ADD  DEFAULT ('Completed') FOR [Status]
GO

ALTER TABLE [dbo].[LoanRepayments]  WITH CHECK ADD  CONSTRAINT [FK_LoanRepayments_Loans] FOREIGN KEY([LoanNo])
REFERENCES [dbo].[Loans] ([LoanNo])
GO

ALTER TABLE [dbo].[LoanRepayments] CHECK CONSTRAINT [FK_LoanRepayments_Loans]
GO

ALTER TABLE [dbo].[LoanRepayments]  WITH CHECK ADD  CONSTRAINT [FK_LoanRepayments_Members] FOREIGN KEY([MemberNo])
REFERENCES [dbo].[Members] ([MemberNo])
GO

ALTER TABLE [dbo].[LoanRepayments] CHECK CONSTRAINT [FK_LoanRepayments_Members]
GO
