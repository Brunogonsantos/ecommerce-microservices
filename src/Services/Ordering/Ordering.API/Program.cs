using MassTransit;
using Ordering.Application.Contracts;
using Ordering.Application.Features.Orders.Consumers;
using Ordering.Infrastructure.Data;
using Ordering.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Injeção de Dependências da Infraestrutura e Aplicação
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Configuração do EF Core com SQL Server (compartilhando a mesma instância do Docker)
builder.Services.AddDbContext<OrderContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrderingConn")));

// Configuração do MassTransit + RabbitMQ Consumer
builder.Services.AddMassTransit(config =>
{
    // Registra o consumer que criamos
    config.AddConsumer<BasketCheckoutConsumer>();

    config.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["EventBusSettings:HostAddress"] ?? "amqp://guest:guest@localhost:5672");

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

// Garante a criação do banco de dados na inicialização
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderContext>();
    dbContext.Database.EnsureCreated();
}

app.Run();