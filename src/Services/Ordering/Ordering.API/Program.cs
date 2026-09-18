using MassTransit;
using Ordering.Application.Contracts;
using Ordering.Application.Features.Orders.Consumers;
using Ordering.Infrastructure.Data;
using Ordering.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient; 
using Polly;                     

var builder = WebApplication.CreateBuilder(args);

// Injeção de Dependências da Infraestrutura e Aplicação
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Configuração do EF Core com SQL Server
builder.Services.AddDbContext<OrderContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrderingConn")));

// Configuração do MassTransit + RabbitMQ Consumer (Unificado)
builder.Services.AddMassTransit(config =>
{
    // Registra o consumer
    config.AddConsumer<BasketCheckoutConsumer>();

    config.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["EventBusSettings:HostAddress"] ?? "amqp://guest:guest@localhost:5672");

        // Configura o MassTransit para tentar reprocessar mensagens falhas (3 vezes a cada 5 segundos)
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));

        // Configura o endpoint da fila mapeando para o Consumer
        cfg.ReceiveEndpoint("basket-checkout-queue", c =>
        {
            c.ConfigureConsumer<BasketCheckoutConsumer>(context);
        });
    });
});

builder.Services.AddOpenApi();
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();

// Definição da Política de Retry para o SQL Server (Polly)
var retryPolicy = Policy
    .Handle<SqlException>()
    .Or<InvalidOperationException>()
    .WaitAndRetry(
        retryCount: 5,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            Console.WriteLine($"[Resiliência] Falha ao conectar ao SQL Server. Tentativa {retryCount} em {timeSpan.TotalSeconds} segundos. Erro: {exception.Message}");
        });

// Garante a criação do banco de dados na inicialização aplicando a resiliência
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderContext>();
    
    retryPolicy.Execute(() =>
    {
        dbContext.Database.EnsureCreated();
    });
}

app.Run();
