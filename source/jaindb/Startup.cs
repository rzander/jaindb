// ************************************************************************************
//          jaindb (c) Copyright 2017 by Roger Zander
// ************************************************************************************

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Formatters;
using System.IO;
using System.Diagnostics;
using Microsoft.Azure.Documents.Client;

namespace jaindb
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();

            services.AddSingleton<IConfiguration>(Configuration);

            // Add framework services.
            //services.AddMvc();
            services.AddMvc(options =>
            {
                options.OutputFormatters.RemoveType<StringOutputFormatter>();
                options.RespectBrowserAcceptHeader = true;
            }
                ).AddJsonOptions(options =>
            {
                options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                options.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
                options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
            });


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IApplicationLifetime applicationLifetime)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            applicationLifetime.ApplicationStopping.Register(OnShutdown);
            applicationLifetime.ApplicationStarted.Register(OnStartup);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        private void OnShutdown()
        {
            Console.WriteLine("Shutdown...");
            if (File.Exists("/usr/local/bin/redis-server"))
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    WorkingDirectory = "/app/wwwroot",
                    RedirectStandardOutput = false,
                    FileName = "redis-cli",
                    Arguments = "shutdown"
                };
                var oInit = Process.Start(psi);
                oInit.WaitForExit(2000);
                Console.WriteLine("... Done.");
            }
        }

        private void OnStartup()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("localURL")))
                Environment.SetEnvironmentVariable("localURL", "http://localhost:5000");

            Console.WriteLine(" ");
            Console.WriteLine("-------------------------------------------------------------------");
            Console.WriteLine("PowerShell Inventory:");
            Console.WriteLine(Environment.ExpandEnvironmentVariables("Invoke-RestMethod -Uri '%localURL%/getps' | iex"));
            Console.WriteLine("-------------------------------------------------------------------");
            Console.WriteLine(" ");

            string sHashType = Environment.GetEnvironmentVariable("HashType");
            if (string.IsNullOrEmpty(sHashType))
                sHashType = Configuration.GetSection("jaindb:HashType").Value ?? Configuration.GetSection("HashType").Value;

            switch (sHashType.ToLower())
            {
                case "md5":
                    Inv.HashType = Inv.hashType.MD5;
                    break;
                case "sha256":
                    Inv.HashType = Inv.hashType.SHA2_256;
                    break;
                case "sha2_256":
                    Inv.HashType = Inv.hashType.SHA2_256;
                    break;
                default:
                    Inv.HashType = Inv.hashType.MD5;
                    break;
            }

            if ((int.Parse(Configuration.GetSection("UseRedis").Value ?? Configuration.GetSection("jaindb:UseRedis").Value) == 1) || (Environment.GetEnvironmentVariable("UseRedis")) == "1")
            {
                try
                {
                    if (Inv.cache0 == null)
                    {
                        Inv.cache0 = RedisConnectorHelper.Connection.GetDatabase(0);
                        Inv.cache1 = RedisConnectorHelper.Connection.GetDatabase(1);
                        Inv.cache2 = RedisConnectorHelper.Connection.GetDatabase(2);
                        Inv.cache3 = RedisConnectorHelper.Connection.GetDatabase(3);
                        Inv.cache4 = RedisConnectorHelper.Connection.GetDatabase(4);
                    }
                    if (Inv.srv == null)
                        Inv.srv = RedisConnectorHelper.Connection.GetServer("127.0.0.1", 6379);

                    Inv.UseRedis = true;

                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.Message);
                    Console.WriteLine("Redis = disabled, FileStore = enabled !!!");
                    Inv.UseRedis = false;
                    Inv.UseFileStore = true;
                }
            }

            if ((int.Parse(Configuration.GetSection("UseCosmosDB").Value ?? Configuration.GetSection("jaindb:UseCosmosDB").Value) == 1) || (Environment.GetEnvironmentVariable("UseCosmosDB") == "1"))
            {
                try
                {
                    Inv.databaseId = "Assets";
                    Inv.endpointUrl = "https://localhost:8081";
                    Inv.authorizationKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
                    Inv.CosmosDB = new DocumentClient(new Uri(Inv.endpointUrl), Inv.authorizationKey);

                    Inv.CosmosDB.OpenAsync();

                    Inv.UseCosmosDB = true;
                }
                catch { }
            }

            if ((int.Parse(Configuration.GetSection("UseFileSystem").Value ?? Configuration.GetSection("jaindb:UseFileSystem").Value) == 1) || (Environment.GetEnvironmentVariable("UseFileSystem") == "1"))
            {
                Inv.UseFileStore = true;
            }

            int iComplexity = 0;
            if (!int.TryParse(Environment.GetEnvironmentVariable("PoWComplexitity"), out iComplexity))
            {
                if (!int.TryParse(Configuration.GetSection("PoWComplexitity").Value, out iComplexity))
                {
                    int.TryParse(Configuration.GetSection("jaindb:PoWComplexitity").Value, out iComplexity);
                }
            }
            Inv.PoWComplexitity = iComplexity;
        }
    }
}
