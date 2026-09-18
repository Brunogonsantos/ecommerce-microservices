using Basket.API.Repositories;
using MassTransit;
using Polly;
using Polly.Extensions.Http;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Configuração do Redis Cache ---
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// --- Registro do Repositório do Carrinho ---
builder.Services.AddScoped<IBasketRepository, BasketRepository>();

// --- Configuração do MassTransit com RabbitMQ ---
builder.Services.AddMassTransit(config =>
{
    config.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["EventBusSettings:HostAddress"] ?? "amqp://guest:guest@localhost:5672");
    });
});

// --- Configuração das Políticas de Resiliência (Polly) ---

// Política de Retry: Tenta 3 vezes com intervalo exponencial (2s, 4s, 8s)
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// Política de Circuit Breaker: Abre o circuito por 30 segundos após 5 falhas consecutivas
var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

// Exemplo de aplicação das políticas em um HttpClient (Ajuste as classes conforme seu projeto)
builder.Services.AddHttpClient<ICatalogService, CatalogService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:CatalogUrl"] ?? "http://localhost:5070");
})
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(circuitBreakerPolicy);

// --- Configurações do ASP.NET Core ---
builder.Services.AddOpenApi(); // Nativo do .NET 9
builder.Services.AddControllers();

var app = builder.Build();

// --- Configuração do Pipeline de Requisições HTTP ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();            // Mapeia o endpoint JSON do OpenAPI
    app.MapScalarApiReference(); // Ativa a interface visual do Scalar em /scalar
}

app.UseAuthorization();
app.MapControllers();
app.Run();


// --- Interfaces e Classes de Exemplo (Remova ou mova para arquivos próprios se já existirem) ---
public interface ICatalogService { }
public class CatalogService : ICatalogService 
{ 
    public CatalogService(HttpClient client) { } 
}