﻿using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using PayBot.Configuration;
using Sender.Quartz;
using Sender.Services;
using Utils;
using Utils.Logger;

namespace Sender
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        private const string _configPath = "../conf/config.json";
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            services.AddScoped<IPhoneHelper, PhoneHelper>();
            services.AddScoped<ISenderService, SenderService>();
            services.AddScoped<IBotLogger, ToGoogleTableBotLogger>();
            services.AddScoped<ISheetsServiceProvider, SheetsServiceProvider>();

            services.AddScoped<SendPaymentsInfoJob>();
            services.AddSingleton(p =>
            {
                var json = System.IO.File.ReadAllText(_configPath);
                return JsonConvert.DeserializeObject<Config>(json);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure
            (IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime lifetime,
            IServiceProvider serviceProvider)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseMvc();

            var quartz = new QuartzStartup(serviceProvider);
            lifetime.ApplicationStarted.Register(quartz.Start);
            lifetime.ApplicationStopping.Register(quartz.Stop);
        }
    }
}
