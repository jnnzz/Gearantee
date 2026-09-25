using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Text;

namespace ASI.Basecode.WebApp.Mvc
{
    public class ControllerBase<TController> : Controller
        where TController : class
    {
        protected readonly IConfiguration _configuration;
        protected readonly IHttpContextAccessor _httpContextAccessor;
        protected readonly ILogger _logger;
        protected ISession _session =>
            _httpContextAccessor.HttpContext.Session;

        public ControllerBase(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<TController>();
        }

        public string UserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        public string UserName => User.Identity?.Name;

        public string Role =>
            User.FindFirstValue(ClaimTypes.Role);

        public override void OnActionExecuting(
            ActionExecutingContext context)
        {
        }

        public override void OnActionExecuted(
            ActionExecutedContext context)
        {
        }

        protected void HandleExceptionLog(
            Exception exception,
            string request)
        {
            var controllerName =
                ControllerContext.RouteData.Values["controller"]?.ToString();
            var actionMethod =
                ControllerContext.RouteData.Values["action"]?.ToString();

            var logContent = new StringBuilder();
            logContent.AppendLine(
                "======================================== start ========================================");
            logContent.AppendLine(
                $"Controller: {controllerName}");
            logContent.AppendLine(
                $"Action: {actionMethod}");
            logContent.AppendLine(
                $"Request: {request}");
            logContent.AppendLine(
                $"Exception: {exception}");
            logContent.AppendLine(
                "========================================= end =========================================");

            _logger.LogError(
                exception,
                "{LogContent}",
                logContent.ToString());
        }
    }
}
