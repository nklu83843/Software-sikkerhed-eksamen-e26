using WebGoatCore.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.FileProviders;
using WebGoatCore.Controllers;
using WebGoatCore.Exceptions;
using Microsoft.AspNetCore.Builder; 
using Microsoft.AspNetCore.Hosting; 
using Microsoft.Extensions.Configuration; 
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.Extensions.Hosting; 
using Microsoft.Extensions.Logging;

namespace WebGoatCore
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            var execDirectory = GetExecDirectory();

            var builder = new ConfigurationBuilder();

            var dic = new Dictionary<string, string>
            {
                {Constants.WEBGOAT_ROOT, execDirectory},
            };
            builder.AddInMemoryCollection(dic);
            builder.AddConfiguration(configuration);
            Configuration = builder.Build();

            env.WebRootFileProvider = new CompositeFileProvider(
                env.WebRootFileProvider, new PhysicalFileProvider(Path.Combine(execDirectory, "wwwroot"))
            );

            NorthwindContext.Initialize(this.Configuration, env);
        }

        private static string GetExecDirectory()
        {
            string? entryDir = null;
            string? entryLocation = Assembly.GetEntryAssembly()?.Location;
            if(!string.IsNullOrEmpty(entryLocation))
            {
                entryDir = Path.GetDirectoryName(entryLocation);
            }
            if(!string.IsNullOrEmpty(entryDir))
            {
                return entryDir;
            }
            throw new WebGoatStartupException("Cannot locate entry assembly location!");
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews()
                .AddRazorRuntimeCompilation();

            services.AddDbContext<NorthwindContext>(options =>
                options.UseSqlite(NorthwindContext.ConnString)
                    .UseLazyLoadingProxies(),
                ServiceLifetime.Scoped);

            services.AddDefaultIdentity<IdentityUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<NorthwindContext>();

            services.Configure<IdentityOptions>(options =>
            {
                // Password settings.
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequiredLength = 2;
                options.Password.RequiredUniqueChars = 0;

                // Lockout settings.
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = false;

                // User settings.
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
            });

            services.ConfigureApplicationCookie(options =>
            {
                // Cookie settings
                options.Cookie.HttpOnly = false;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);

                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.SlidingExpiration = true;
            });

            services.AddDistributedMemoryCache();

            services.AddSession(options =>
            {
                options.Cookie.HttpOnly = false;
                options.IdleTimeout = TimeSpan.FromHours(1);
            });

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };

            services.AddScoped<CustomerRepository>();
            services.AddScoped<ProductRepository>();
            services.AddScoped<BlogEntryRepository>();
            services.AddScoped<BlogResponseRepository>();
            services.AddScoped<ShipperRepository>();
            services.AddScoped<SupplierRepository>();
            services.AddScoped<OrderRepository>();
            services.AddScoped<CategoryRepository>();
        }
/*
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
           
            app.UseDeveloperExceptionPage();
            
            app.UseStatusCodePagesWithRedirects($"/{StatusCodeController.NAME}?code={{0}}");

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseSession();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}");
            });
        }
*/

        /*
        Udbedring af AC-09 (Del 3): Håndhævelse af Fail Secure og Global Fejlhåndtering

        Uhåndterede undtagelser i WebGoat.NET udstiller fulde stack traces, interne stier og kildekodelinjer i browseren. 
        Dette overtræder det grundlæggende princip om Fail Secure og fører til Fail Insecurely
        */
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                // Udviklervisning: Kun tilladt i isoleret lokalt miljø
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // PRODUKTIONSSIKRING (Fail Secure):
                // Fanger alle ubehandlede tekniske undtagelser og omdirigerer til StatusCodeController
                app.UseExceptionHandler("/StatusCode?code=500");

                // HSTS (HTTP Strict Transport Security) til sikring af transportlaget
                app.UseHsts();
            }

            // Central håndtering af HTTP statuskoder (f.eks. 404 Not Found og 403 Forbidden)
            app.UseStatusCodePagesWithReExecute("/StatusCode?code={0}");

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }


        public int startupNotUsed(int a, int b) // Function added to generate same FA Violation
        {
            var c = 1;
            b = 0;
            return a / b;
        }
    }
}
