using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using static fnJainDB.fnJainDB;

[assembly: FunctionsStartup(typeof(Startup))]
namespace fnJainDB
{
    public static class fnJainDB
    {
        public class Startup : FunctionsStartup
        {
            //private ILoggerFactory _loggerFactory;

            public override void Configure(IFunctionsHostBuilder builder)
            {
                var config = new ConfigurationBuilder().AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();
                builder.Services.AddLogging();
                ConfigureServices(builder, config);
            }

            public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
            {
                //Add KeyVault by using the VaultUri variable
                //builder.ConfigurationBuilder.AddAzureKeyVault(Environment.GetEnvironmentVariable("VaultUri"), keyVaultClient, new DefaultKeyVaultSecretManager());

                base.ConfigureAppConfiguration(builder);
            }

            public void ConfigureServices(IFunctionsHostBuilder builder, IConfiguration config)
            {
                //_loggerFactory = new LoggerFactory();
                //var logger = _loggerFactory.CreateLogger("Startup");

                var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string ResourcePath = Path.GetFullPath(Path.Combine(binDirectory, ".."));

                Console.WriteLine("loading JainDB Storage-Providers:");
                jaindb.jDB.loadPlugins(ResourcePath);

            }
        }


    }
}
