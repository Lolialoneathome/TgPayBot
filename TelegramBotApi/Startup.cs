﻿using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using PayBot.Configuration;
using Sqllite;
using Sqllite.Logger;
using Telegram.Bot;
using TelegramBotApi.Services;
using Utils;
using Utils.DbLogger;
using Utils.Logger;

namespace TelegramBotApi
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
            services.AddMvc();

            services.AddSingleton<ILoggerFactory, LoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddLogging((builder) => builder.SetMinimumLevel(LogLevel.Warning));
            services.AddSingleton<Bot>();
            services.AddScoped<ITelegramBotClient, TelegramBotClient>();

            services.AddDbContext<SqlliteDbContext>(options => options.UseSqlite(Configuration.GetConnectionString("users")));
            services.AddDbContext<LogDbContext>(options => options.UseSqlite(Configuration.GetConnectionString("logs")));

            services.AddScoped<IPhoneHelper, PhoneHelper>();
            services.AddScoped<IBotLogger, ToGoogleTableBotLogger>();
            services.AddScoped<INewBotLogger, NewBotLogger>();
            services.AddScoped<ISheetsServiceProvider, SheetsServiceProvider>(p => new SheetsServiceProvider(
                p.GetService<IConfigService>(),
                Configuration.GetSection("Google").GetValue<string>("ClientSecretPath"),
                Configuration.GetSection("Google").GetValue<string>("CredentialsPath"))
            );

            services.AddScoped<IConfigService, FromFileConfigService>(p => new FromFileConfigService(Configuration.GetValue<string>("ConfigPath")));
            services.AddScoped<IMessageRoutingService, MessageRoutingService>();
            services.AddScoped<IUserMessageService, UserMessageService>();
            services.AddScoped<IAdminMessageService, AdminMessageService>();
            services.AddScoped<IPhoneNumberVerifier, TwilloPhoneNumberVerifier>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env,
            IApplicationLifetime lifetime,
            IServiceProvider serviceProvider)
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            //configure NLog
            loggerFactory.AddNLog();
            loggerFactory.ConfigureNLog(Path.Combine(env.ContentRootPath, @"NLog.config"));

            app.UseMvc();
            var bot = new Bot(serviceProvider.GetService<IConfigService>());
            lifetime.ApplicationStarted.Register(setWebhook);
            lifetime.ApplicationStopping.Register(deleteWebHook);
            

        }

        private void deleteWebHook()
        {
            Bot.Api.DeleteWebhookAsync().Wait();
        }

        private void setWebhook()
        {
            Bot.Api.SetWebhookAsync($"{Configuration.GetValue<string>("WebHook")}/webhook").Wait();
        }
    }
}
