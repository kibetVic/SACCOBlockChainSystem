using Microsoft.AspNetCore.Mvc;

namespace SACCOBlockChainSystem.Controllers
{
    public class LoanCalculatorController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}