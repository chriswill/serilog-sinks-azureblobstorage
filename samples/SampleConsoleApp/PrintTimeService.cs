using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SampleConsoleApp
{
    public class PrintTimeService : BackgroundService
    {
        private readonly ILogger<PrintTimeService> logger;

        public PrintTimeService(ILogger<PrintTimeService> logger)
        {
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("The current time is: {CurrentTime}", DateTimeOffset.UtcNow);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
