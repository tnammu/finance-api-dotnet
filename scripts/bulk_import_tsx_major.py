#!/usr/bin/env python3
"""
Bulk import major TSX stocks for swing trading analysis.
Filters out ETFs, debentures, preferred shares, warrants, leveraged products.
Adds .TO suffix as needed and imports using the existing StockDataUpdater.
"""

import sys
import time
import os

# Add scripts directory to path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from update_stocks_from_yahoo import StockDataUpdater

# Major TSX stocks grouped by sector — curated for swing trading
TSX_MAJOR_STOCKS = [
    # === BANKS ===
    "RY.TO", "TD.TO", "BNS.TO", "BMO.TO", "CM.TO", "NA.TO",

    # === INSURANCE / FINANCIAL ===
    "MFC.TO", "SLF.TO", "IAG.TO", "FFH.TO", "X.TO",

    # === ENERGY - OIL & GAS ===
    "ENB.TO", "TRP.TO", "SU.TO", "CNQ.TO", "CVE.TO",
    "BTE.TO", "ARX.TO", "MEG.TO", "WCP.TO", "PXT.TO",
    "ERF.TO", "BIR.TO", "AAV.TO", "CPG.TO", "FRU.TO",
    "ARC.TO", "NVA.TO", "CJ.TO", "TVE.TO", "VRN.TO",

    # === ENERGY - PIPELINES & UTILITIES ===
    "ALA.TO", "KEY.TO", "PPL.TO",

    # === UTILITIES ===
    "FTS.TO", "EMA.TO", "H.TO", "AQN.TO", "NPI.TO",
    "INE.TO", "CWEN-A.TO", "BEP-UN.TO", "CPX.TO",

    # === TELECOM ===
    "BCE.TO", "T.TO", "RCI-B.TO", "QBR-B.TO",

    # === RAILWAYS ===
    "CNR.TO", "CP.TO",

    # === INDUSTRIALS / TRANSPORT ===
    "WCN.TO", "TFI.TO", "GFL.TO", "TIH.TO", "BDT.TO",
    "STN.TO", "ATRL.TO", "NFI.TO",

    # === RETAIL / CONSUMER ===
    "ATD.TO", "DOL.TO", "L.TO", "MRU.TO", "EMP-A.TO",
    "CTC-A.TO", "GOOS.TO", "ATZ.TO", "GIL.TO",

    # === REAL ESTATE (REITs) ===
    "CAR-UN.TO", "AP-UN.TO", "HR-UN.TO", "REI-UN.TO",
    "SRU-UN.TO", "DIR-UN.TO", "CRT-UN.TO", "NWH-UN.TO",
    "IIP-UN.TO", "CSH-UN.TO",

    # === MINING / GOLD ===
    "ABX.TO", "AEM.TO", "AGI.TO", "K.TO", "B2Gold.TO",
    "FNV.TO", "WPM.TO", "EDR.TO", "IMG.TO", "PVG.TO",
    "OGC.TO", "OR.TO", "MAG.TO", "AYA.TO",

    # === MINING - BASE METALS ===
    "FM.TO", "LUN.TO", "HBM.TO", "CS.TO", "TECK-B.TO",
    "IVN.TO", "ERO.TO",

    # === TECHNOLOGY ===
    "SHOP.TO", "CSU.TO", "KXS.TO", "LSPD.TO", "DSGX.TO",
    "OTEX.TO", "BB.TO", "NVEI.TO", "CIGI.TO", "TOI.TO",

    # === HEALTHCARE ===
    "WELL.TO", "GUD.TO", "NVA.TO", "CURA.TO",

    # === DIVERSIFIED / CONGLOMERATES ===
    "BAM.TO", "BN.TO", "POW.TO", "GWO.TO", "SNC.TO",
    "IFC.TO", "BRO.TO",

    # === CANNABIS ===
    "ACB.TO", "WEED.TO", "CRON.TO",
]

def main():
    db_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'dividends.db')
    updater = StockDataUpdater(db_path)
    if not updater.connect_db():
        print("✗ Failed to connect to database. Aborting.")
        sys.exit(1)

    total = len(TSX_MAJOR_STOCKS)
    success_count = 0
    skip_count = 0
    fail_count = 0

    print(f"\n{'='*60}")
    print(f"TSX Bulk Import — {total} stocks")
    print(f"{'='*60}\n")

    for i, symbol in enumerate(TSX_MAJOR_STOCKS, 1):
        print(f"[{i}/{total}] {symbol}", end=" ... ", flush=True)
        try:
            result = updater.add_or_update_single_stock(symbol)
            if result:
                print("✓")
                success_count += 1
            else:
                print("✗ skipped (no data)")
                skip_count += 1
        except Exception as e:
            print(f"✗ error: {e}")
            fail_count += 1

        # Rate limit: 1 request per second to avoid Yahoo Finance throttling
        if i < total:
            time.sleep(1.0)

    print(f"\n{'='*60}")
    print(f"Import complete:")
    print(f"  ✓ Success: {success_count}")
    print(f"  ✗ Skipped: {skip_count}")
    print(f"  ✗ Failed:  {fail_count}")
    print(f"  Total:     {total}")
    print(f"{'='*60}\n")


if __name__ == "__main__":
    main()
