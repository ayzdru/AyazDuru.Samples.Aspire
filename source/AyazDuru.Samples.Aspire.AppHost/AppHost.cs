// Daðýtýk uygulama için bir builder oluþturuluyor.
var builder = DistributedApplication.CreateBuilder(args);

// Redis önbellek servisi ekleniyor.
var cache = builder.AddRedis("cache");

// API servis projesi ekleniyor ve saðlýk kontrolü endpoint'i tanýmlanýyor.
var apiService = builder.AddProject<Projects.AyazDuru_Samples_Aspire_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

// Web frontend projesi ekleniyor, dýþ HTTP endpoint'leri açýlýyor ve baðýmlýlýklar tanýmlanýyor.
builder.AddProject<Projects.AyazDuru_Samples_Aspire_Web>("webfrontend")
    .WithExternalHttpEndpoints() // Dýþarýya açýk HTTP endpoint'leri ekleniyor.
    .WithHttpHealthCheck("/health") // Saðlýk kontrolü endpoint'i ekleniyor.
    .WithReference(cache) // Redis önbellek servisine referans veriliyor.
    .WaitFor(cache) // Redis servisi hazýr olana kadar bekleniyor.
    .WithReference(apiService) // API servisine referans veriliyor.
    .WaitFor(apiService); // API servisi hazýr olana kadar bekleniyor.

// Uygulama baþlatýlýyor.
builder.Build().Run();
