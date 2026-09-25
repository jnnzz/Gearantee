using ASI.Basecode.Data;
using ASI.Basecode.WebApp.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.IO;

namespace ASI.Basecode.WebApp
{
    internal partial class StartupConfigurer
    {
        private IConfiguration Configuration { get; }
        private IApplicationBuilder _app;
        private IWebHostEnvironment _environment;
        private IServiceCollection _services;

        public StartupConfigurer(IConfiguration configuration)
        {
            Configuration = configuration;
            PathManager.Setup(Configuration.GetSetupRootDirectoryPath());
        }

        public void ConfigureServices(IServiceCollection services)
        {
            _services = services;

            services.AddMemoryCache();
            services.AddDbContext<AsiBasecodeDBContext>(options =>
            {
                options.UseSqlServer(
                    Configuration.GetConnectionString("DefaultConnection"),
                    sqlServerOptions => sqlServerOptions.CommandTimeout(120));
            });

            ConfigureIdentityAndAuthorization();

            services.AddControllersWithViews()
                .AddRazorRuntimeCompilation();

            services.AddSession(options =>
            {
                options.Cookie.Name = "Gearantee.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            ConfigureOtherServices();

            services.Configure<FormOptions>(options =>
            {
                options.ValueLengthLimit = 1024 * 1024 * 100;
            });

            services.AddSingleton<IFileProvider>(
                new PhysicalFileProvider(
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")));
        }

        public void ConfigureApp(IApplicationBuilder app, IWebHostEnvironment env)
        {
            _app = app;
            _environment = env;

            if (!_environment.IsDevelopment())
            {
                _app.UseHsts();
            }

            ConfigureLogger();

            _app.UseHttpsRedirection();
            _app.UseStaticFiles();

            var options = _app.ApplicationServices
                .GetService<IOptions<RequestLocalizationOptions>>();
            if (options != null)
            {
                _app.UseRequestLocalization(options.Value);
            }

            _app.UseSession();
            _app.UseRouting();
            _app.UseAuthentication();
            _app.UseAuthorization();
        }
    }
}
