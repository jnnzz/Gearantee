using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ASI.Basecode.WebApp
{
    internal partial class StartupConfigurer
    {
        private void ConfigureOtherServices()
        {
            _services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            _services.TryAddSingleton<IActionContextAccessor, ActionContextAccessor>();
            _services.AddScoped<IUnitOfWork, UnitOfWork>();
            _services.AddHttpClient();
        }
    }
}
