CREATE TABLE [Authors] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(150) NOT NULL,
    CONSTRAINT [PK_Authors] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Categories] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(150) NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [FinePolicies] (
    [Id] int NOT NULL IDENTITY,
    [DailyRate] decimal(18,2) NOT NULL,
    [MaxFinePerLoan] decimal(18,2) NOT NULL,
    [DebtThreshold] decimal(18,2) NOT NULL,
    [HoldPeriodDays] int NOT NULL,
    [ClosedOnSundays] bit NOT NULL,
    CONSTRAINT [PK_FinePolicies] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_FinePolicies_Cap] CHECK ([MaxFinePerLoan] >= [DailyRate]),
    CONSTRAINT [CK_FinePolicies_DailyRate] CHECK ([DailyRate] > 0),
    CONSTRAINT [CK_FinePolicies_HoldPeriodDays] CHECK ([HoldPeriodDays] BETWEEN 1 AND 30)
);
GO


CREATE TABLE [Holidays] (
    [Id] int NOT NULL IDENTITY,
    [Date] date NOT NULL,
    [Description] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Holidays] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [MembershipTypes] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(50) NOT NULL,
    [MaxLoans] int NOT NULL,
    [LoanPeriodDays] int NOT NULL,
    [MaxRenewals] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_MembershipTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_MembershipTypes_LoanPeriodDays] CHECK ([LoanPeriodDays] > 0),
    CONSTRAINT [CK_MembershipTypes_MaxLoans] CHECK ([MaxLoans] > 0),
    CONSTRAINT [CK_MembershipTypes_MaxRenewals] CHECK ([MaxRenewals] >= 0)
);
GO


CREATE TABLE [Publishers] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(150) NOT NULL,
    CONSTRAINT [PK_Publishers] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Roles] (
    [Id] int NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Titles] (
    [Id] int NOT NULL IDENTITY,
    [Isbn] nvarchar(13) NULL,
    [Name] nvarchar(300) NOT NULL,
    [PublisherId] int NULL,
    [CategoryId] int NULL,
    [PublicationYear] int NULL,
    [Description] nvarchar(2000) NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Titles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Titles_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Titles_Publishers_PublisherId] FOREIGN KEY ([PublisherId]) REFERENCES [Publishers] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [UserAccounts] (
    [Id] int NOT NULL IDENTITY,
    [Username] nvarchar(50) NOT NULL,
    [PasswordHash] nvarchar(200) NOT NULL,
    [FullName] nvarchar(100) NOT NULL,
    [RoleId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IsLocked] bit NOT NULL,
    [FailedLoginAttempts] int NOT NULL,
    [LockoutEndsAt] datetime2 NULL,
    [LastLoginAt] datetime2 NULL,
    CONSTRAINT [PK_UserAccounts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserAccounts_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Copies] (
    [Id] int NOT NULL IDENTITY,
    [TitleId] int NOT NULL,
    [Barcode] nvarchar(30) NOT NULL,
    [Location] nvarchar(50) NOT NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_Copies] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Copies_Titles_TitleId] FOREIGN KEY ([TitleId]) REFERENCES [Titles] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [EResources] (
    [Id] int NOT NULL IDENTITY,
    [TitleId] int NOT NULL,
    [Label] nvarchar(150) NOT NULL,
    [Url] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_EResources] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EResources_Titles_TitleId] FOREIGN KEY ([TitleId]) REFERENCES [Titles] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [TitleAuthors] (
    [Id] int NOT NULL IDENTITY,
    [TitleId] int NOT NULL,
    [AuthorId] int NOT NULL,
    [Position] int NOT NULL,
    CONSTRAINT [PK_TitleAuthors] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TitleAuthors_Authors_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [Authors] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TitleAuthors_Titles_TitleId] FOREIGN KEY ([TitleId]) REFERENCES [Titles] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Members] (
    [Id] int NOT NULL IDENTITY,
    [MemberCode] nvarchar(20) NOT NULL,
    [FullName] nvarchar(100) NOT NULL,
    [Email] nvarchar(100) NULL,
    [Phone] nvarchar(20) NULL,
    [MembershipTypeId] int NOT NULL,
    [RegisteredOn] date NOT NULL,
    [CardExpiryDate] date NOT NULL,
    [IsActive] bit NOT NULL,
    [UserAccountId] int NULL,
    CONSTRAINT [PK_Members] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Members_MembershipTypes_MembershipTypeId] FOREIGN KEY ([MembershipTypeId]) REFERENCES [MembershipTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Members_UserAccounts_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [UserAccounts] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Loans] (
    [Id] int NOT NULL IDENTITY,
    [CopyId] int NOT NULL,
    [MemberId] int NOT NULL,
    [LoanDate] datetime2 NOT NULL,
    [DueDate] date NOT NULL,
    [ReturnedAt] datetime2 NULL,
    [RenewalCount] int NOT NULL,
    [IssuedById] int NOT NULL,
    [ReturnedById] int NULL,
    CONSTRAINT [PK_Loans] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Loans_RenewalCount] CHECK ([RenewalCount] >= 0),
    CONSTRAINT [FK_Loans_Copies_CopyId] FOREIGN KEY ([CopyId]) REFERENCES [Copies] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Loans_Members_MemberId] FOREIGN KEY ([MemberId]) REFERENCES [Members] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Loans_UserAccounts_IssuedById] FOREIGN KEY ([IssuedById]) REFERENCES [UserAccounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Loans_UserAccounts_ReturnedById] FOREIGN KEY ([ReturnedById]) REFERENCES [UserAccounts] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Reservations] (
    [Id] int NOT NULL IDENTITY,
    [TitleId] int NOT NULL,
    [MemberId] int NOT NULL,
    [ReservedAt] datetime2 NOT NULL,
    [Status] int NOT NULL,
    [HeldCopyId] int NULL,
    [HoldExpiresOn] date NULL,
    [ClosedAt] datetime2 NULL,
    CONSTRAINT [PK_Reservations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Reservations_Copies_HeldCopyId] FOREIGN KEY ([HeldCopyId]) REFERENCES [Copies] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reservations_Members_MemberId] FOREIGN KEY ([MemberId]) REFERENCES [Members] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reservations_Titles_TitleId] FOREIGN KEY ([TitleId]) REFERENCES [Titles] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Fines] (
    [Id] int NOT NULL IDENTITY,
    [LoanId] int NOT NULL,
    [MemberId] int NOT NULL,
    [OverdueDays] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [AmountPaid] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_Fines] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Fines_Amount] CHECK ([Amount] >= 0),
    CONSTRAINT [CK_Fines_AmountPaid] CHECK ([AmountPaid] >= 0 AND [AmountPaid] <= [Amount]),
    CONSTRAINT [CK_Fines_OverdueDays] CHECK ([OverdueDays] > 0),
    CONSTRAINT [FK_Fines_Loans_LoanId] FOREIGN KEY ([LoanId]) REFERENCES [Loans] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Fines_Members_MemberId] FOREIGN KEY ([MemberId]) REFERENCES [Members] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Payments] (
    [Id] int NOT NULL IDENTITY,
    [FineId] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [PaidAt] datetime2 NOT NULL,
    [ReceivedById] int NOT NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [FK_Payments_Fines_FineId] FOREIGN KEY ([FineId]) REFERENCES [Fines] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Payments_UserAccounts_ReceivedById] FOREIGN KEY ([ReceivedById]) REFERENCES [UserAccounts] ([Id]) ON DELETE NO ACTION
);
GO


CREATE UNIQUE INDEX [IX_Authors_Name] ON [Authors] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Categories_Name] ON [Categories] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Copies_Barcode] ON [Copies] ([Barcode]);
GO


CREATE INDEX [IX_Copies_TitleId] ON [Copies] ([TitleId]);
GO


CREATE INDEX [IX_EResources_TitleId] ON [EResources] ([TitleId]);
GO


CREATE UNIQUE INDEX [IX_Fines_LoanId] ON [Fines] ([LoanId]);
GO


CREATE INDEX [IX_Fines_MemberId] ON [Fines] ([MemberId]);
GO


CREATE UNIQUE INDEX [IX_Holidays_Date] ON [Holidays] ([Date]);
GO


CREATE INDEX [IX_Loans_DueDate] ON [Loans] ([DueDate]);
GO


CREATE INDEX [IX_Loans_IssuedById] ON [Loans] ([IssuedById]);
GO


CREATE INDEX [IX_Loans_MemberId_ReturnedAt] ON [Loans] ([MemberId], [ReturnedAt]);
GO


CREATE INDEX [IX_Loans_ReturnedById] ON [Loans] ([ReturnedById]);
GO


CREATE UNIQUE INDEX [UX_Loans_OpenLoanPerCopy] ON [Loans] ([CopyId]) WHERE [ReturnedAt] IS NULL;
GO


CREATE UNIQUE INDEX [IX_Members_MemberCode] ON [Members] ([MemberCode]);
GO


CREATE INDEX [IX_Members_MembershipTypeId] ON [Members] ([MembershipTypeId]);
GO


CREATE UNIQUE INDEX [IX_Members_UserAccountId] ON [Members] ([UserAccountId]) WHERE [UserAccountId] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_MembershipTypes_Name] ON [MembershipTypes] ([Name]);
GO


CREATE INDEX [IX_Payments_FineId] ON [Payments] ([FineId]);
GO


CREATE INDEX [IX_Payments_ReceivedById] ON [Payments] ([ReceivedById]);
GO


CREATE UNIQUE INDEX [IX_Publishers_Name] ON [Publishers] ([Name]);
GO


CREATE INDEX [IX_Reservations_HeldCopyId] ON [Reservations] ([HeldCopyId]);
GO


CREATE INDEX [IX_Reservations_MemberId] ON [Reservations] ([MemberId]);
GO


CREATE INDEX [IX_Reservations_TitleId_Status_ReservedAt] ON [Reservations] ([TitleId], [Status], [ReservedAt]);
GO


CREATE UNIQUE INDEX [UX_Reservations_ActivePerMemberAndTitle] ON [Reservations] ([TitleId], [MemberId]) WHERE [Status] IN (1, 2);
GO


CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
GO


CREATE INDEX [IX_TitleAuthors_AuthorId] ON [TitleAuthors] ([AuthorId]);
GO


CREATE UNIQUE INDEX [IX_TitleAuthors_TitleId_AuthorId] ON [TitleAuthors] ([TitleId], [AuthorId]);
GO


CREATE INDEX [IX_Titles_CategoryId] ON [Titles] ([CategoryId]);
GO


CREATE UNIQUE INDEX [IX_Titles_Isbn] ON [Titles] ([Isbn]) WHERE [Isbn] IS NOT NULL;
GO


CREATE INDEX [IX_Titles_Name] ON [Titles] ([Name]);
GO


CREATE INDEX [IX_Titles_PublisherId] ON [Titles] ([PublisherId]);
GO


CREATE INDEX [IX_UserAccounts_RoleId] ON [UserAccounts] ([RoleId]);
GO


CREATE UNIQUE INDEX [IX_UserAccounts_Username] ON [UserAccounts] ([Username]);
GO


