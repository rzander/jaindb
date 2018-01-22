// ************************************************************************************
//          jaindb (c) Copyright 2017 by Roger Zander
// ************************************************************************************

using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Diagnostics;

namespace jaindb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                if (File.Exists("/usr/local/bin/redis-server"))
                {
                    if (!File.Exists("/app/wwwroot/redis.conf"))
                    {
                        File.Copy("/app/redis.conf", "/app/wwwroot/redis.conf", true);
                    }

                    if (!File.Exists("/app/wwwroot/inventory.ps1"))
                    {
                        File.Copy("/app/inventory.ps1", "/app/wwwroot/inventory.ps1", true);
                    }

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        WorkingDirectory = "/app/wwwroot",
                        RedirectStandardOutput = false,
                        FileName = "redis-server",
                        Arguments = "/app/wwwroot/redis.conf"
                    };
                    Console.WriteLine("starting Redis-Server ...");
                    var oInit = Process.Start(psi); //>/dev/null 2>/dev/null

                    int iDelay = 3000;
                    if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("StartDelay")))
                    {
                        iDelay = int.Parse(Environment.GetEnvironmentVariable("StartDelay") ?? "3000");
                    }

                    oInit.WaitForExit(iDelay);
                    Console.WriteLine("... Done.");

                    //Thread.Sleep(2000);
                }

            }
            catch { }

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .UseApplicationInsights()
                .UseUrls("http://*:" + (Environment.GetEnvironmentVariable("WebPort") ?? "5000"))
                .Build();

            host.Run();

        }
    }
}
