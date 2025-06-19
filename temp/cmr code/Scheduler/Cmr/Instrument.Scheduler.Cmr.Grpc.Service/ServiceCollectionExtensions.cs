namespace Instrument.Scheduler.Cmr.Grpc.Service;

using Instrument.Scheduler.Cmr.Parser;
using Instrument.Scheduler.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSchedulerConfiguration (
      this IServiceCollection services,
      IConfiguration configuration)
    {
        return services
                .Configure<SchedulerConfiguration>(
                    nameof(SchedulerConfiguration),
                    configuration.GetSection($"{nameof(SchedulerConfiguration)}"))
                .AddTransient<ICmrFileParser, CmrFileParser>()
                .AddSingleton<ICmrService, CmrService>();
    }
}
