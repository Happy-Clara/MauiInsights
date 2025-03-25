using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Http;

namespace MauiInsights
{
    public class DependencyTrackingHandler : DelegatingHandler
    {
        private readonly TelemetryClient _client;

        public DependencyTrackingHandler(TelemetryClient client)
        {
            _client = client;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid().ToString().Substring(0,8);;
            var telemetry = GetTelemetry(request, requestId);
            SetOpenTelemetryHeaders(request, telemetry, requestId);
            telemetry.Start();
            HttpResponseMessage? response = null;
            try
            {
                response = await base.SendAsync(request, cancellationToken);
                return response;
            }
            finally
            {
                telemetry.Stop();
                Enrich(telemetry, response);
                _client.TrackDependency(telemetry);
            }
        }

        private DependencyTelemetry GetTelemetry(HttpRequestMessage request, string requestId)
        {
            var host = request.RequestUri?.Host ?? "Unknown url";
            var call = request.RequestUri?.AbsolutePath ?? "Unknown url";
            var operationId = _client.Context.Operation.Id;
            var telemetry = new DependencyTelemetry(
                "Fetch", 
                host, 
                call, 
                "");
            telemetry.Id = $"|{operationId}.{requestId}.";
            telemetry.Name = $"{request.Method} {request.RequestUri}";
            telemetry.Data = telemetry.Name;
            telemetry.Context.Operation.Id = operationId;
            telemetry.Context.Session.Id = _client.Context.Session.Id;
            telemetry.Properties.Add("HttpMethod", request.Method.ToString());
            return telemetry;
        }

        private void Enrich(DependencyTelemetry telemetry, HttpResponseMessage? response)
        {
            telemetry.Success = response != null;
            telemetry.ResultCode = response?.StatusCode.ToString();
        }

        private void SetOpenTelemetryHeaders(HttpRequestMessage request, DependencyTelemetry telemetry, string requestId)
        {
            var parentId = telemetry.Context.Operation.Id;
            var version = "00";
            var flags = 0x01;
            var headerValue = $"{version}-{parentId}-{requestId}-{flags:00}";
            request.Headers.Add("Request-Id", telemetry.Id);
            request.Headers.Add("traceparent", headerValue);
        }
    }

    internal class DependencyTrackingHandlerFilter : IHttpMessageHandlerBuilderFilter
    {
        private readonly IServiceProvider _serviceProvider;
        public DependencyTrackingHandlerFilter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
        {
            return (builder) =>
            {
                next(builder);

                if (_serviceProvider.GetService(typeof(DependencyTrackingHandler)) is DependencyTrackingHandler handler)
                {
                    builder.AdditionalHandlers.Add(handler);
                }
            };
        }
    }
}
