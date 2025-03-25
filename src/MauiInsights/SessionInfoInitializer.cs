using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace MauiInsights;

internal class SessionInfoInitializer : ITelemetryInitializer
{
    private readonly SessionId? _sessionId;

    public SessionInfoInitializer(SessionId? sessionId)
    {
        _sessionId = sessionId;
    }
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is ExceptionTelemetry) return;
        if (string.IsNullOrEmpty(telemetry.Context.Session.Id))
        {
            telemetry.Context.Session.Id = _sessionId?.Value;
            telemetry.Context.Operation.Id = _sessionId?.OperationId;
        }
    }
}