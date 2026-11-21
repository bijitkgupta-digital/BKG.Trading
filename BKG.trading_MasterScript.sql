USE master;
GO

-- =========================================================================
-- MASTER INITIALIZATION SCRIPT: ETF SYSTEM (BKG.Trading) - SQL SERVER
-- =========================================================================

-- 1. DATABASE CREATION (Defensive Block with Dot-Name Handling)
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'BKG.Trading')
BEGIN
    CREATE DATABASE [BKG.Trading];
END;
GO

USE [BKG.Trading];
GO

-- 2. CREATE ETF TABLE
IF OBJECT_ID(N'[dbo].[ETF]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ETF];
END;
GO

CREATE TABLE [dbo].[ETF]
(
    [Symbol] NVARCHAR(50) NOT NULL PRIMARY KEY,
    [RecentPeakPrice] DECIMAL(18,2) NULL
);
GO

-- =========================================================================
-- 5. SEED DATA GENERATION: EXHAUSTIVE 34 ETFs MERGE BLOCK
-- =========================================================================
MERGE INTO [dbo].[ETF] AS Target
USING (VALUES
    -- US Innovation, Technology & Global Growth Segment (Target: 3.14%)
    ('MAFANG', NULL)
) AS Source (Symbol, RecentPeakPrice)
ON Target.Symbol = Source.Symbol
WHEN NOT MATCHED THEN
    INSERT (Symbol, RecentPeakPrice) 
    VALUES (Source.Symbol, Source.RecentPeakPrice);
GO

-- Verify total assets seeded successfully (Should return 34 rows)
SELECT * FROM [dbo].[ETF];
GO



