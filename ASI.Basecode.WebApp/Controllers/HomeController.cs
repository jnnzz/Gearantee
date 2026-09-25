using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.Controllers
{
    public class HomeController : ControllerBase<HomeController>
    {
        public HomeController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
            : base(
                httpContextAccessor,
                loggerFactory,
                configuration)
        {
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
