using MediatR;
using System.Text.Json;

namespace StargateAPI.Business.Logging
{
    // Wraps every MediatR request (pre-processors included, see Program.cs)
    // and records its outcome in the ProcessLog table.
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IProcessLogger _processLogger;

        public LoggingBehavior(IProcessLogger processLogger)
        {
            _processLogger = processLogger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var requestData = Serialize(request);

            try
            {
                var response = await next();

                await _processLogger.LogSuccessAsync(requestName, requestData, $"{requestName} completed successfully.");

                return response;
            }
            catch (Exception ex)
            {
                await _processLogger.LogErrorAsync(requestName, requestData, ex);
                throw;
            }
        }

        private static string? Serialize(TRequest request)
        {
            try
            {
                return JsonSerializer.Serialize(request);
            }
            catch
            {
                return null;
            }
        }
    }
}
