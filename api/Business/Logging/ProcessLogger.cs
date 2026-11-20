using StargateAPI.Business.Data;

namespace StargateAPI.Business.Logging
{
    public interface IProcessLogger
    {
        Task LogSuccessAsync(string requestName, string? requestData, string message);

        Task LogErrorAsync(string requestName, string? requestData, Exception exception);
    }

    public class ProcessLogger : IProcessLogger
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ProcessLogger> _logger;

        public ProcessLogger(IServiceScopeFactory scopeFactory, ILogger<ProcessLogger> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public Task LogSuccessAsync(string requestName, string? requestData, string message) =>
            WriteAsync(new ProcessLog
            {
                CreatedAtUtc = DateTime.UtcNow,
                Level = "Information",
                RequestName = requestName,
                RequestData = requestData,
                Message = message
            });

        public Task LogErrorAsync(string requestName, string? requestData, Exception exception) =>
            WriteAsync(new ProcessLog
            {
                CreatedAtUtc = DateTime.UtcNow,
                Level = "Error",
                RequestName = requestName,
                RequestData = requestData,
                Message = exception.Message,
                Exception = exception.ToString()
            });

        private async Task WriteAsync(ProcessLog log)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<StargateContext>();

                await context.ProcessLogs.AddAsync(log);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write process log for {RequestName}: {Message}", log.RequestName, log.Message);
            }
        }
    }
}
