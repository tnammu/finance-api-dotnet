import sqlite3
conn = sqlite3.connect('dividends.db')
c = conn.cursor()
c.execute("SELECT Year, TotalDividend, AnnualEPS FROM YearlyDividends WHERE Symbol='BMO' ORDER BY Year DESC LIMIT 5")
print('Year | Dividend | EPS')
print('-' * 30)
for r in c.fetchall():
    print(f'{r[0]} | ${r[1]:.2f} | ${r[2] if r[2] else 0:.2f}')
conn.close()
