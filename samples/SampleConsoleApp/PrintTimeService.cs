using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SampleConsoleApp
{
    public class PrintTimeService : BackgroundService
    {
        private readonly ILogger<PrintTimeService> logger;
        private readonly Random random;

        public PrintTimeService(ILogger<PrintTimeService> logger)
        {
            this.logger = logger;
            random = new Random();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                LogLevel level = (LogLevel)random.Next(0, 5);
                logger.Log(level, "The current time is: {CurrentTime}", DateTimeOffset.UtcNow);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
