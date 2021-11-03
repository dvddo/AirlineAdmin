using AirlineDB.Data;
using AirlineWeb.Shared;
using AirlineWeb.Shared.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using MediatR;
using AirlineCRS.Queries;
using Microsoft.AspNetCore.Authentication;
using AirlineWeb.Server.Models;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using AirlineWeb.Server.Data;

namespace AirlineAdmin
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<AirlineContext>(options => options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    Configuration.GetConnectionString("DefaultConnection")));
            services.AddDatabaseDeveloperPageExceptionFilter();

            services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddControllersWithViews();

            services.AddSingleton<ITempDataProvider, CookieTempDataProvider>();
            services.AddSession();

            services.AddMemoryCache();
            services.AddScoped<BWACacheRepository>();
            services.AddScoped<BWAFactory>();
            services.AddScoped<BWAService>();

            services.AddMediatR(typeof(GetCountriesQuery).Assembly);

            services.AddSingleton(_ =>
            {
                return Configuration.GetSection("MachineSettings")
                             .Get<ServerSettings>();
            });

            services.AddSingleton(_ =>
            {
                return Configuration.GetSection("ClientSettings")
                             .Get<ClientSettings>();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseSession();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });
        }
    }
}
