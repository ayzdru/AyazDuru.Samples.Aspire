using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Bu sýnýf, .NET Aspire projelerinde ortak olarak kullanýlan servisleri ekler.
// Servis keþfi, dayanýklýlýk, saðlýk kontrolleri ve OpenTelemetry gibi modern bulut uygulamalarýnda kritik olan özellikleri kolayca projeye dahil eder.
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // .NET Aspire servis varsayýlanlarýný ekler: servis keþfi, dayanýklýlýk, saðlýk kontrolleri ve telemetri.
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry(); // OpenTelemetry ile izleme ve metrik toplama

        builder.AddDefaultHealthChecks(); // Saðlýk kontrolleri ekleniyor

        builder.Services.AddServiceDiscovery(); // Servis keþfi (Service Discovery) ekleniyor

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // HTTP isteklerinde dayanýklýlýk (resilience) varsayýlan olarak aktif
            http.AddStandardResilienceHandler();

            // HTTP isteklerinde servis keþfi varsayýlan olarak aktif
            http.AddServiceDiscovery();
        });

        // Servis keþfinde izin verilen protokolleri kýsýtlamak için aþaðýdaki satýrý açabilirsiniz.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    // OpenTelemetry yapýlandýrmasý: Ýzleme ve metrik toplama için kullanýlýr.
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation() // ASP.NET Core metrikleri
                    .AddHttpClientInstrumentation() // HTTP istemci metrikleri
                    .AddRuntimeInstrumentation(); // .NET çalýþma zamaný metrikleri
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Saðlýk kontrolü istekleri izlemeye dahil edilmez
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // gRPC istemci izleme için aþaðýdaki satýrý açabilirsiniz
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters(); // Telemetri verisini dýþa aktaran eklentiler

        return builder;
    }

    // OpenTelemetry dýþa aktarýcýlarýný ekler (ör. OTLP veya Azure Monitor)
    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter(); // OTLP ile telemetri verisi dýþa aktarýlýr
        }

        // Azure Monitor dýþa aktarýcýyý eklemek için aþaðýdaki satýrý açabilirsiniz
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    // Varsayýlan saðlýk kontrollerini ekler
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Uygulamanýn canlý (responsive) olduðunu doðrulayan varsayýlan kontrol
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    // Varsayýlan saðlýk kontrolü uç noktalarýný haritalar
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Saðlýk kontrolü uç noktalarýný geliþtirme ortamýnda eklemek güvenlik açýsýndan önemlidir.
        // Detaylar için: https://aka.ms/dotnet/aspire/healthchecks
        if (app.Environment.IsDevelopment())
        {
            // Tüm saðlýk kontrolleri geçerse uygulama trafiðe hazýr kabul edilir
            app.MapHealthChecks(HealthEndpointPath);

            // Sadece "live" etiketiyle iþaretlenen saðlýk kontrolleri geçerse uygulama canlý kabul edilir
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
