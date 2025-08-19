// Da��t�k uygulama i�in bir builder olu�turuluyor.
var builder = DistributedApplication.CreateBuilder(args);

// Redis �nbellek servisi ekleniyor.
var cache = builder.AddRedis("cache");

// API servis projesi ekleniyor ve sa�l�k kontrol� endpoint'i tan�mlan�yor.
var apiService = builder.AddProject<Projects.AyazDuru_Samples_Aspire_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

// Web frontend projesi ekleniyor, d�� HTTP endpoint'leri a��l�yor ve ba��ml�l�klar tan�mlan�yor.
builder.AddProject<Projects.AyazDuru_Samples_Aspire_Web>("webfrontend")
    .WithExternalHttpEndpoints() // D��ar�ya a��k HTTP endpoint'leri ekleniyor.
    .WithHttpHealthCheck("/health") // Sa�l�k kontrol� endpoint'i ekleniyor.
    .WithReference(cache) // Redis �nbellek servisine referans veriliyor.
    .WaitFor(cache) // Redis servisi haz�r olana kadar bekleniyor.
    .WithReference(apiService) // API servisine referans veriliyor.
    .WaitFor(apiService); // API servisi haz�r olana kadar bekleniyor.

// Uygulama ba�lat�l�yor.
builder.Build().Run();
