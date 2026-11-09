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

-- 2. CREATE INSTRUMENTS TABLE
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Instruments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Instruments] (
        [SYMBOL] VARCHAR(30) PRIMARY KEY,
        [SellTarget] DECIMAL(5, 4) NOT NULL            -- 0.0470 for Domestic/Commodity, 0.0640 for US Tech
    );
END;
GO

-- 3. CREATE TIME-SERIES OHLCV & INDICATORS TABLE
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OHLCV_Data]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OHLCV_Data] (
        [SYMBOL] VARCHAR(30),
        [Date] DATE,
        [Open] DECIMAL(10, 2) NOT NULL,
        [High] DECIMAL(10, 2) NOT NULL,
        [Low] DECIMAL(10, 2) NOT NULL,
        [Close] DECIMAL(10, 2) NOT NULL,               -- Reserved keyword protected by bracket downstream
        [Volume] BIGINT NOT NULL,
        [RSI14] DECIMAL(5, 2) NULL,                     -- Automatically stores RSI_T per row
        [Volume_SMA20] BIGINT NULL,                     -- Unified column tracking 20-period EOD Volume average
        [OpenMinusLow] DECIMAL(10, 2) NULL,      -- 14-period daily average of (Low - Close)
        PRIMARY KEY CLUSTERED ([SYMBOL] ASC, [Date] ASC),
        FOREIGN KEY ([SYMBOL]) REFERENCES [dbo].[Instruments]([SYMBOL]) ON DELETE CASCADE
    );
END;
GO

-- 4. VIEW CREATION: PUBLISH TOP 5 NEXT-DAY INSTRUMENTS (With SellTarget Included)
IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[dbo].[v_Top5_Momentum_Pivots]'))
BEGIN
    DROP VIEW [dbo].[v_Top5_Momentum_Pivots];
END;
GO

CREATE VIEW [dbo].[v_Top5_Momentum_Pivots] AS
WITH RowHistory AS (
    SELECT 
        o1.SYMBOL,
        o1.Date,
        o1.[Close] AS Today_Close,                     -- Protected system reserved keyword
        o1.RSI14 AS RSI_Today,
        o1.Volume AS Volume_Today,
        o1.Volume_SMA20,
        o1.OpenMinusLow,
        -- Window Function to cleanly pull the immediate prior row for RSI_T-1
        LAG(o1.RSI14, 1) OVER (PARTITION BY o1.SYMBOL ORDER BY o1.Date ASC) AS RSI_Yesterday,
        -- Row numbering forces selection of only the absolute newest data sequence per asset
        ROW_NUMBER() OVER (PARTITION BY o1.SYMBOL ORDER BY o1.Date DESC) AS rn
    FROM [dbo].[OHLCV_Data] o1
)
SELECT TOP 5
    rh.SYMBOL,
    rh.Date AS Signal_Date,
    rh.Today_Close,
    rh.RSI_Today,
    rh.RSI_Yesterday,
    rh.Volume_Today,
    rh.Volume_SMA20,
    i.SellTarget,
    rh.OpenMinusLow
FROM RowHistory rh
INNER JOIN [dbo].[Instruments] i ON rh.SYMBOL = i.SYMBOL -- Linked master table to fetch static rules
WHERE rh.rn = 1                                      -- Isolates the latest trading day (EOD Sync)
  AND rh.RSI_Today > rh.RSI_Yesterday                   -- Core Condition 1: Momentum Turning Up (RSI_T > RSI_T-1)
  AND rh.Volume_Today > rh.Volume_SMA20                 -- Core Condition 2: Institutional Influx
ORDER BY 
    rh.RSI_Today ASC,                                -- Priority 1: Focus on the deepest oversold value assets
    (CAST(rh.Volume_Today AS FLOAT) / NULLIF(rh.Volume_SMA20, 0)) DESC; -- Priority 2: Highest relative volume accumulation
GO

-- =========================================================================
-- 5. SEED DATA GENERATION: EXHAUSTIVE 34 ETFs MERGE BLOCK
-- =========================================================================
MERGE INTO [dbo].[Instruments] AS Target
USING (VALUES
    -- US Innovation, Technology & Global Growth Segment (Target: 6.4%)
    ('NSE:MAFANG', 0.0640),
    ('NSE:MON100', 0.0640),
    ('NSE:MONQ50', 0.0640),
    ('NSE:MAHKTECH', 0.0640),

    -- Broad Core Market & Domestic Index Segment (Target: 4.7%)
    ('NSE:NIFTYBEES', 0.0470),
    ('NSE:JUNIORBEES', 0.0470),
    ('NSE:MID150BEES', 0.0470),
    ('NSE:MOSMALL250', 0.0470),
    ('NSE:EQUAL50ADD', 0.0470),

    -- Domestic Sectoral Verticals (Target: 4.7%)
    ('NSE:BANKBEES', 0.0470),
    ('NSE:PVTBANIETF', 0.0470),
    ('NSE:PSUBNKBEES', 0.0470),
    ('NSE:ITBEES', 0.0470),
    ('NSE:AUTOBEES', 0.0470),
    ('NSE:PHARMABEES', 0.0470),
    ('NSE:FMCGIETF', 0.0470),
    ('NSE:HEALTHIETF', 0.0470),
    ('NSE:METALIETF', 0.0470),
    ('NSE:ENERGY', 0.0470),
    ('NSE:OILIETF', 0.0470),
    ('NSE:MOREALTY', 0.0470),
    ('NSE:CONSUMBEES', 0.0470),

    -- Government Policies & Thematic Engines (Target: 4.7%)
    ('NSE:CPSEETF', 0.0470),
    ('NSE:ABSLPSE', 0.0470),
    ('NSE:MODEFENCE', 0.0470),
    ('NSE:MAKEINDIA', 0.0470),
    ('NSE:GROWWEV', 0.0470),

    -- Smart-Beta & Quantitative Factor Anchors (Target: 4.7%)
    ('NSE:ALPHA', 0.0470),
    ('NSE:ALPHAETF', 0.0470),
    ('NSE:LOWVOLIETF', 0.0470),
    ('NSE:MOM30IETF', 0.0470),
    ('NSE:MOMENTUM50', 0.0470),
    ('NSE:NV20IETF', 0.0470),

    -- Commodity Tracker (Target: 4.7%)
    ('NSE:SILVERBEES', 0.0470)
) AS Source (SYMBOL, SellTarget)
ON Target.SYMBOL = Source.SYMBOL
WHEN NOT MATCHED THEN
    INSERT (SYMBOL, SellTarget) 
    VALUES (Source.SYMBOL, Source.SellTarget);
GO

-- Verify total assets seeded successfully (Should return 34 rows)
SELECT * FROM [dbo].[Instruments];
GO



