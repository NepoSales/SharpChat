using System.Globalization;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;
using SharpChat.Application.Diagnostics;

namespace SharpChat.Api.Observability;

internal static class ObservabilitySetup
{
    public const string ServiceName = "sharpchat-api";

    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string SignalRServerSource = "Microsoft.AspNetCore.SignalR.Server";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var instance = builder.Configuration["InstanceName"] ?? Environment.MachineName;
        var otlpEndpoint = builder.Configuration[OtlpEndpointKey];
        var exportEnabled = !string.IsNullOrWhiteSpace(otlpEndpoint);

        builder.Services.AddSerilog((services, logger) =>
        {
            logger.ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);

            if (exportEnabled)
            {
                logger.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = OtlpProtocol.Grpc;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = ServiceName,
                        ["service.instance.id"] = instance,
                    };
                });
            }
        });

        if (!exportEnabled)
        {
            return builder;
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName, serviceInstanceId: instance))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !IsHealthCheck(context))
                .AddHttpClientInstrumentation()
                .AddSource(SignalRServerSource)
                .AddNpgsql()
                .AddRedisInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(ChatMetrics.MeterName))
            .UseOtlpExporter();

        return builder;
    }

    public static WebApplication UseObservability(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options => options.GetLevel = (context, _, exception) =>
            exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
                ? LogEventLevel.Error
                : IsHealthCheck(context) ? LogEventLevel.Verbose : LogEventLevel.Information);

        return app;
    }

    private static bool IsHealthCheck(HttpContext context) => context.Request.Path.StartsWithSegments("/health");
}
