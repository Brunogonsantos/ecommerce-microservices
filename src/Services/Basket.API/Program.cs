using Basket.API.Repositories;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Configuração do Redis Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Registro do Repositório do Carrinho
builder.Services.AddScoped<IBasketRepository, BasketRepository>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
IServiceCollection serviceCollection = builder.Services.AddOpenApi();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Adiciona a interface visual interativa do Scalar
    app.MapScalarApiReference(); // Acessível em /scalar/v1
}

app.UseAuthorization();

app.MapControllers();

app.Run();