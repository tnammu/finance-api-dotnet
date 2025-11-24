import sqlite3
conn = sqlite3.connect('dividends.db')
c = conn.cursor()

print("Data counts per table:")
print("-" * 50)

c.execute("SELECT Symbol, COUNT(*) FROM DividendPayments GROUP BY Symbol")
payments = c.fetchall()
print(f"\nDividendPayments (by symbol):")
if payments:
    for row in payments:
        print(f"  {row[0]}: {row[1]} payments")
else:
    print("  NO DATA - This is why charts are empty!")

c.execute("SELECT Symbol, COUNT(*) FROM YearlyDividends GROUP BY Symbol")
yearly = c.fetchall()
print(f"\nYearlyDividends (by symbol):")
for row in yearly:
    print(f"  {row[0]}: {row[1]} years")

conn.close()