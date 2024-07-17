using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace SampleConsoleApp
{
    internal class Program
    {
        private const string OutputTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:u1}] {Message:lj}{NewLine}{Exception}";

        static async Task<int> Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: OutputTemplate)
                .CreateBootstrapLogger();

            try
            {
                Log.Information("Starting Console Host");

                var app = Host
                    .CreateDefaultBuilder(args)
                    .ConfigureServices(services => services.AddHostedService<PrintTimeService>())
                    .UseSerilog((context, services, configuration) => configuration
                        .ReadFrom.Configuration(context.Configuration)
                    )
                    .Build();

                Serilog.Debugging.SelfLog.Enable(Console.Error);

                await app.RunAsync();

                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }

        }
    }
}
