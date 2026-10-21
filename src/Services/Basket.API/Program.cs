using Basket.API.Repositories;
using Basket.API.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;
using Scalar.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- Configuração do Redis Cache ---
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// --- Registro do Repositório do Carrinho ---
builder.Services.AddScoped<IBasketRepository, BasketRepository>();

// --- Autenticação JWT ---
// A MESMA chave/issuer/audience do Gateway (que emite o token) e do Catalog.API.
// A chave NÃO tem valor padrão no código: um padrão público permitiria forjar
// tokens (inclusive de Admin) caso a configuração faltasse em algum ambiente.
// Em Docker ela vem de Jwt__Key; em desenvolvimento local, do appsettings.Development.json.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key ausente ou curta demais (mínimo de 32 bytes). Defina a variável de ambiente Jwt__Key; " +
        "em desenvolvimento local, ela vem do appsettings.Development.json.");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ecommerce-microservices";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ecommerce-microservices-clients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

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

// Aplica retry + circuit breaker ao ICatalogService: toda chamada do Basket.API
// ao Catalog.API (validar produto/preço no carrinho) passa por essas políticas.
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

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
