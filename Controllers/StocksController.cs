using FinanceApi.Data;
using FinanceApi.Model;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StocksController : ControllerBase
    {
        private readonly FinanceContext _context;
        public StocksController(FinanceContext context)
        {
            _context = context;
        }

        [HttpGet]
        public ActionResult<IEnumerable<Stock>> GetStocks()
        {
            return _context.Stocks.ToList();
        }

        [HttpGet("{id}")]
        public ActionResult<Stock> GetStock(int id)
        {
            var stock = _context.Stocks.Find(id);
            if (stock == null) return NotFound();
            return stock;
        }
    }

}
