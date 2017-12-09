using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.Diagnostics;
using System.Threading;

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
                    if(!File.Exists("/app/wwwroot/redis.conf"))
                    {
                        File.Copy("/app/redis.conf", "/app/wwwroot/redis.conf", true);
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
                    oInit.WaitForExit(3000);
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
                .UseUrls("http://*:" + Environment.GetEnvironmentVariable("WebPort") ?? "5000")
                .Build();

            host.Run();

        }
    }
}
