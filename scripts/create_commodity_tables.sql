-- Create Commodity Trading Tables
-- Run this script to add commodity tables to existing database

-- 1. Commodities table
CREATE TABLE IF NOT EXISTS "Commodities" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Commodities" PRIMARY KEY AUTOINCREMENT,
    "Symbol" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "CurrentPrice" TEXT NOT NULL,
    "ContractSize" INTEGER NOT NULL,
    "TickSize" TEXT NOT NULL,
    "TickValue" TEXT NOT NULL,
    "MarginRequirement" TEXT NOT NULL,
    "LastUpdated" TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Commodities_Symbol" ON "Commodities" ("Symbol");
CREATE INDEX IF NOT EXISTS "IX_Commodities_Category" ON "Commodities" ("Category");
CREATE INDEX IF NOT EXISTS "IX_Commodities_LastUpdated" ON "Commodities" ("LastUpdated");

-- 2. CommodityHistory table
CREATE TABLE IF NOT EXISTS "CommodityHistory" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_CommodityHistory" PRIMARY KEY AUTOINCREMENT,
    "CommodityId" INTEGER NOT NULL,
    "Symbol" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "Open" TEXT NOT NULL,
    "High" TEXT NOT NULL,
    "Low" TEXT NOT NULL,
    "Close" TEXT NOT NULL,
    "Volume" INTEGER NOT NULL,
    "ATR14" TEXT NULL,
    "Volatility20" TEXT NULL,
    CONSTRAINT "FK_CommodityHistory_Commodities_CommodityId" FOREIGN KEY ("CommodityId") REFERENCES "Commodities" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_CommodityHistory_Symbol" ON "CommodityHistory" ("Symbol");
CREATE INDEX IF NOT EXISTS "IX_CommodityHistory_Date" ON "CommodityHistory" ("Date");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_CommodityHistory_Symbol_Date" ON "CommodityHistory" ("Symbol", "Date");
CREATE INDEX IF NOT EXISTS "IX_CommodityHistory_CommodityId" ON "CommodityHistory" ("CommodityId");

-- 3. CommodityCorrelations table
CREATE TABLE IF NOT EXISTS "CommodityCorrelations" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_CommodityCorrelations" PRIMARY KEY AUTOINCREMENT,
    "Symbol1" TEXT NOT NULL,
    "Symbol2" TEXT NOT NULL,
    "Period" TEXT NOT NULL,
    "PearsonCorrelation" TEXT NOT NULL,
    "CointegrationScore" TEXT NOT NULL,
    "IsStationaryPair" INTEGER NOT NULL,
    "HalfLife" INTEGER NULL,
    "OptimalRatio" TEXT NULL,
    "CalculatedAt" TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_CommodityCorrelations_Symbol1" ON "CommodityCorrelations" ("Symbol1");
CREATE INDEX IF NOT EXISTS "IX_CommodityCorrelations_Symbol2" ON "CommodityCorrelations" ("Symbol2");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_CommodityCorrelations_Symbol1_Symbol2_Period" ON "CommodityCorrelations" ("Symbol1", "Symbol2", "Period");
CREATE INDEX IF NOT EXISTS "IX_CommodityCorrelations_CalculatedAt" ON "CommodityCorrelations" ("CalculatedAt");

-- 4. PairSuggestions table
CREATE TABLE IF NOT EXISTS "PairSuggestions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_PairSuggestions" PRIMARY KEY AUTOINCREMENT,
    "Symbol1" TEXT NOT NULL,
    "Symbol2" TEXT NOT NULL,
    "RecommendationType" TEXT NOT NULL,
    "Score" TEXT NOT NULL,
    "Reasoning" TEXT NOT NULL,
    "OptimalRatio" TEXT NOT NULL,
    "ExpectedReturns" TEXT NULL,
    "RiskLevel" TEXT NOT NULL,
    "GeneratedAt" TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_PairSuggestions_Score" ON "PairSuggestions" ("Score");
CREATE INDEX IF NOT EXISTS "IX_PairSuggestions_Symbol1_Symbol2" ON "PairSuggestions" ("Symbol1", "Symbol2");
CREATE INDEX IF NOT EXISTS "IX_PairSuggestions_GeneratedAt" ON "PairSuggestions" ("GeneratedAt");

-- 5. CommodityBacktests table
CREATE TABLE IF NOT EXISTS "CommodityBacktests" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_CommodityBacktests" PRIMARY KEY AUTOINCREMENT,
    "Symbol" TEXT NULL,
    "Symbol1" TEXT NULL,
    "Symbol2" TEXT NULL,
    "StrategyType" TEXT NOT NULL,
    "StopLossMethod" TEXT NOT NULL,
    "StopLossValue" TEXT NOT NULL,
    "Period" TEXT NOT NULL,
    "Capital" TEXT NOT NULL,
    "ContractsTraded" INTEGER NOT NULL,
    "FinalValue" TEXT NOT NULL,
    "TotalReturn" TEXT NOT NULL,
    "AnnualReturn" TEXT NOT NULL,
    "MaxDrawdown" TEXT NOT NULL,
    "WinRate" TEXT NOT NULL,
    "TotalTrades" INTEGER NOT NULL,
    "ProfitFactor" TEXT NOT NULL,
    "SharpeRatio" TEXT NOT NULL,
    "SortinoRatio" TEXT NOT NULL,
    "TotalCosts" TEXT NOT NULL,
    "CalculatedAt" TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_CommodityBacktests_Symbol" ON "CommodityBacktests" ("Symbol");
CREATE INDEX IF NOT EXISTS "IX_CommodityBacktests_Symbol1_Symbol2" ON "CommodityBacktests" ("Symbol1", "Symbol2");
CREATE INDEX IF NOT EXISTS "IX_CommodityBacktests_StrategyType" ON "CommodityBacktests" ("StrategyType");
CREATE INDEX IF NOT EXISTS "IX_CommodityBacktests_Period" ON "CommodityBacktests" ("Period");
CREATE INDEX IF NOT EXISTS "IX_CommodityBacktests_CalculatedAt" ON "CommodityBacktests" ("CalculatedAt");

-- 6. BacktestTrades table
CREATE TABLE IF NOT EXISTS "BacktestTrades" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_BacktestTrades" PRIMARY KEY AUTOINCREMENT,
    "BacktestId" INTEGER NOT NULL,
    "Date" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "Price" TEXT NOT NULL,
    "Contracts" INTEGER NOT NULL,
    "Reason" TEXT NOT NULL,
    "StopLossPrice" TEXT NULL,
    "TakeProfitPrice" TEXT NULL,
    "PnL" TEXT NULL,
    "Commission" TEXT NOT NULL,
    "ExchangeFees" TEXT NOT NULL,
    "ClearingFees" TEXT NOT NULL,
    "OvernightFinancing" TEXT NOT NULL,
    CONSTRAINT "FK_BacktestTrades_CommodityBacktests_BacktestId" FOREIGN KEY ("BacktestId") REFERENCES "CommodityBacktests" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_BacktestTrades_BacktestId" ON "BacktestTrades" ("BacktestId");
CREATE INDEX IF NOT EXISTS "IX_BacktestTrades_Date" ON "BacktestTrades" ("Date");

-- 7. CmeCostProfiles table
CREATE TABLE IF NOT EXISTS "CmeCostProfiles" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_CmeCostProfiles" PRIMARY KEY AUTOINCREMENT,
    "BrokerName" TEXT NOT NULL,
    "CommoditySymbol" TEXT NOT NULL,
    "CommissionPerContract" TEXT NOT NULL,
    "ExchangeFeePerContract" TEXT NOT NULL,
    "ClearingFeePerContract" TEXT NOT NULL,
    "OvernightFinancingRate" TEXT NOT NULL,
    "MarginInterestRate" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_CmeCostProfiles_CommoditySymbol" ON "CmeCostProfiles" ("CommoditySymbol");
CREATE INDEX IF NOT EXISTS "IX_CmeCostProfiles_IsActive" ON "CmeCostProfiles" ("IsActive");

-- Insert default CME cost profiles
INSERT OR IGNORE INTO "CmeCostProfiles" ("BrokerName", "CommoditySymbol", "CommissionPerContract", "ExchangeFeePerContract", "ClearingFeePerContract", "OvernightFinancingRate", "MarginInterestRate", "IsActive")
VALUES
    ('Standard CME', 'ALL', '2.50', '1.50', '0.50', '0.000137', '0.05', 1),
    ('Standard CME', 'GC=F', '2.50', '1.50', '0.50', '0.000137', '0.05', 1),
    ('Standard CME', 'SI=F', '2.50', '1.50', '0.50', '0.000137', '0.05', 1),
    ('Standard CME', 'HG=F', '2.50', '1.50', '0.50', '0.000137', '0.05', 1),
    ('Standard CME', 'PL=F', '2.50', '1.50', '0.50', '0.000137', '0.05', 1),
    ('Standard CME', 'CL=F', '2.50', '1.50', '0.50', '0.000137', '0.05', 1),
    ('Standard CME', 'NG=F', '2.50', '1.50', '0.50', '0.000137', '0.05', 1),
    ('Standard CME', 'HO=F', '2.50', '1.50', '0.50', '0.000137', '0.05', 1);