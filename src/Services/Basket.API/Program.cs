using Basket.API.Repositories;
using MassTransit;
using Scalar.AspNetCore; // <--- Importante para o Scalar funcionar

var builder = WebApplication.CreateBuilder(args);

// Configuração do Redis Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Registro do Repositório do Carrinho
builder.Services.AddScoped<IBasketRepository, BasketRepository>();

// --- Configuração do MassTransit com RabbitMQ ---
builder.Services.AddMassTransit(config =>
{
    config.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["EventBusSettings:HostAddress"] ?? "amqp://guest:guest@localhost:5672");
    });
});

builder.Services.AddOpenApi(); // Reativado nativo do .NET 9
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Mapeia o endpoint JSON do OpenAPI
    app.MapScalarApiReference(); // Ativa a interface visual do Scalar em /scalar
}

app.UseAuthorization();
app.MapControllers();
app.Run();
