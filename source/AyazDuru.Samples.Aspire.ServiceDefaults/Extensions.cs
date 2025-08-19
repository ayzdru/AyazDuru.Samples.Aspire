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

// Bu s�n�f, .NET Aspire projelerinde ortak olarak kullan�lan servisleri ekler.
// Servis ke�fi, dayan�kl�l�k, sa�l�k kontrolleri ve OpenTelemetry gibi modern bulut uygulamalar�nda kritik olan �zellikleri kolayca projeye dahil eder.
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // .NET Aspire servis varsay�lanlar�n� ekler: servis ke�fi, dayan�kl�l�k, sa�l�k kontrolleri ve telemetri.
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry(); // OpenTelemetry ile izleme ve metrik toplama

        builder.AddDefaultHealthChecks(); // Sa�l�k kontrolleri ekleniyor

        builder.Services.AddServiceDiscovery(); // Servis ke�fi (Service Discovery) ekleniyor

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // HTTP isteklerinde dayan�kl�l�k (resilience) varsay�lan olarak aktif
            http.AddStandardResilienceHandler();

            // HTTP isteklerinde servis ke�fi varsay�lan olarak aktif
            http.AddServiceDiscovery();
        });

        // Servis ke�finde izin verilen protokolleri k�s�tlamak i�in a�a��daki sat�r� a�abilirsiniz.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    // OpenTelemetry yap�land�rmas�: �zleme ve metrik toplama i�in kullan�l�r.
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
                    .AddRuntimeInstrumentation(); // .NET �al��ma zaman� metrikleri
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Sa�l�k kontrol� istekleri izlemeye dahil edilmez
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // gRPC istemci izleme i�in a�a��daki sat�r� a�abilirsiniz
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters(); // Telemetri verisini d��a aktaran eklentiler

        return builder;
    }

    // OpenTelemetry d��a aktar�c�lar�n� ekler (�r. OTLP veya Azure Monitor)
    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter(); // OTLP ile telemetri verisi d��a aktar�l�r
        }

        // Azure Monitor d��a aktar�c�y� eklemek i�in a�a��daki sat�r� a�abilirsiniz
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    // Varsay�lan sa�l�k kontrollerini ekler
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Uygulaman�n canl� (responsive) oldu�unu do�rulayan varsay�lan kontrol
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    // Varsay�lan sa�l�k kontrol� u� noktalar�n� haritalar
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Sa�l�k kontrol� u� noktalar�n� geli�tirme ortam�nda eklemek g�venlik a��s�ndan �nemlidir.
        // Detaylar i�in: https://aka.ms/dotnet/aspire/healthchecks
        if (app.Environment.IsDevelopment())
        {
            // T�m sa�l�k kontrolleri ge�erse uygulama trafi�e haz�r kabul edilir
            app.MapHealthChecks(HealthEndpointPath);

            // Sadece "live" etiketiyle i�aretlenen sa�l�k kontrolleri ge�erse uygulama canl� kabul edilir
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
