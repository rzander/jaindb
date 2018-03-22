// ************************************************************************************
//          jaindb (c) Copyright 2018 by Roger Zander
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
using System.Net.Sockets;
using System.Linq;
using System.Net.NetworkInformation;
using Moon.AspNetCore.Authentication.Basic;
using System.Threading.Tasks;
using System.Security.Claims;
using jaindb;
using Microsoft.AspNetCore.Mvc.Authorization;

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
            services.AddMemoryCache();

            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });

            //services.AddAuthorization();
            services.AddAuthentication("Basic").AddBasic(o =>
            {
                o.Realm = "Password: password";

                o.Events = new BasicAuthenticationEvents
                {
                    OnSignIn = OnSignIn
                };
            });

            //Custom Authentication Provider
            /*services.AddAuthentication(options =>
            {
                // the scheme name has to match the value we're going to use in AuthenticationBuilder.AddScheme(...)
                options.DefaultAuthenticateScheme = "Custom Scheme";
                options.DefaultChallengeScheme = "Custom Scheme";
            }).AddCustomAuth(o => { });*/

            // Add framework services.
            //services.AddMvc();
            services.AddMvc(options =>
            {
                options.OutputFormatters.RemoveType<StringOutputFormatter>();
                options.RespectBrowserAcceptHeader = true;

                //disable Authentication ?
                if(int.Parse(Environment.GetEnvironmentVariable("DisableAuth") ?? "0") > 0)
                    options.Filters.Add(new AllowAnonymousFilter());
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

            app.UseAuthentication();
            app.UseResponseCompression();
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
            string sIP = "localhost";

            try
            {
                foreach (NetworkInterface f in NetworkInterface.GetAllNetworkInterfaces().Where(t => t.OperationalStatus == OperationalStatus.Up))
                    foreach (GatewayIPAddressInformation d in f.GetIPProperties().GatewayAddresses.Where(t => t.Address.AddressFamily == AddressFamily.InterNetwork))
                    {
                        sIP = f.GetIPProperties().UnicastAddresses.Where(t => t.Address.AddressFamily == AddressFamily.InterNetwork).First().Address.ToString();
                    }

                /*IPAddress ip = Dns.GetHostAddresses(Dns.GetHostName()).Where(address =>
                address.AddressFamily == AddressFamily.InterNetwork).First();*/

                //sIP = ip.ToString();
            }
            catch { }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WebPort")))
                Environment.SetEnvironmentVariable("WebPort", "");
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("localURL")))
                Environment.SetEnvironmentVariable("localURL", "http://" + sIP);

            Console.WriteLine(" ");
            Console.WriteLine("-------------------------------------------------------------------");
            Console.WriteLine("PowerShell Inventory:");
            if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WebPort")))
                Console.WriteLine(Environment.ExpandEnvironmentVariables("Invoke-RestMethod -Uri '%localURL%/getps' | iex"));
            else
                Console.WriteLine(Environment.ExpandEnvironmentVariables("Invoke-RestMethod -Uri '%localURL%:%WebPort%/getps' | iex"));

            Console.WriteLine("-------------------------------------------------------------------");
            Console.WriteLine(" ");

            string sHashType = Environment.GetEnvironmentVariable("HashType");
            if (string.IsNullOrEmpty(sHashType))
                sHashType = Configuration.GetSection("jaindb:HashType").Value ?? Configuration.GetSection("HashType").Value;

            switch (sHashType.ToLower())
            {
                case "md5":
                    jDB.HashType = jDB.hashType.MD5;
                    break;
                case "sha256":
                    jDB.HashType = jDB.hashType.SHA2_256;
                    break;
                case "sha2_256":
                    jDB.HashType = jDB.hashType.SHA2_256;
                    break;
                default:
                    jDB.HashType = jDB.hashType.MD5;
                    break;
            }

            if ((int.Parse(Configuration.GetSection("UseRedis").Value ?? Configuration.GetSection("jaindb:UseRedis").Value) == 1) || (Environment.GetEnvironmentVariable("UseRedis")) == "1")
            {
                try
                {
                    if (jDB.cache0 == null)
                    {
                        jDB.cache0 = RedisConnectorHelper.Connection.GetDatabase(0);
                        jDB.cache1 = RedisConnectorHelper.Connection.GetDatabase(1);
                        jDB.cache2 = RedisConnectorHelper.Connection.GetDatabase(2);
                        jDB.cache3 = RedisConnectorHelper.Connection.GetDatabase(3);
                        jDB.cache4 = RedisConnectorHelper.Connection.GetDatabase(4);
                    }

                    //Get RedisServer from EnvironmentVariable
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RedisServer")))
                        Environment.SetEnvironmentVariable("RedisServer", "localhost");
                    string sRedisServer = Environment.GetEnvironmentVariable("RedisServer") ?? "localhost";
                    RedisConnectorHelper.RedisServer = sRedisServer;

                    //Get RedisPort from EnvironmentVariable
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RedisPort")))
                        Environment.SetEnvironmentVariable("RedisPort", "6379");
                    int iRedisPort = int.Parse(Environment.GetEnvironmentVariable("RedisPort") ?? "6379");
                    RedisConnectorHelper.RedisPort = iRedisPort;

                    Console.WriteLine("RedisServer: " + sRedisServer + " on Port: " + iRedisPort.ToString());
                    if (jDB.srv == null)
                        jDB.srv = RedisConnectorHelper.Connection.GetServer(sRedisServer, iRedisPort);

                    jDB.UseRedis = true;

                }
                catch (Exception ex)
                {
                    Console.WriteLine("RedisServer: " + RedisConnectorHelper.RedisServer + " on Port: " + RedisConnectorHelper.RedisPort.ToString());
                    Console.WriteLine("ERROR: " + ex.Message);
                    Console.WriteLine("Redis = disabled, FileStore = enabled !!!");
                    jDB.UseRedis = false;
                    jDB.UseFileStore = true;
                }
            }

            if ((int.Parse(Configuration.GetSection("UseCosmosDB").Value ?? Configuration.GetSection("jaindb:UseCosmosDB").Value) == 1) || (Environment.GetEnvironmentVariable("UseCosmosDB") == "1"))
            {
                try
                {
                    jDB.databaseId = "Assets";
                    jDB.endpointUrl = "https://localhost:8081";
                    jDB.authorizationKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
                    jDB.CosmosDB = new DocumentClient(new Uri(jDB.endpointUrl), jDB.authorizationKey);

                    jDB.CosmosDB.OpenAsync();

                    jDB.UseCosmosDB = true;
                }
                catch { }
            }

            if ((int.Parse(Configuration.GetSection("UseFileSystem").Value ?? Configuration.GetSection("jaindb:UseFileSystem").Value) == 1) || (Environment.GetEnvironmentVariable("UseFileSystem") == "1"))
            {
                jDB.UseFileStore = true;
            }

            int iComplexity = 0;
            if (!int.TryParse(Environment.GetEnvironmentVariable("PoWComplexitity"), out iComplexity))
            {
                if (!int.TryParse(Configuration.GetSection("PoWComplexitity").Value, out iComplexity))
                {
                    int.TryParse(Configuration.GetSection("jaindb:PoWComplexitity").Value, out iComplexity);
                }
            }
            jDB.PoWComplexitity = iComplexity;
        }


        private Task OnSignIn(BasicSignInContext context)
        {
            if ((context.Password == Environment.GetEnvironmentVariable("ReportPW")) && (context.UserName == Environment.GetEnvironmentVariable("ReportUser")))
            {
                var claims = new[] { new Claim(ClaimsIdentity.DefaultNameClaimType, context.UserName) };
                
                var identity = new ClaimsIdentity(claims, context.Scheme.Name);
                identity.AddClaim(new Claim(ClaimsIdentity.DefaultRoleClaimType, "All")); //Role = All
                context.Principal = new ClaimsPrincipal(identity);
            }

            return Task.CompletedTask;
        }
    }
}
