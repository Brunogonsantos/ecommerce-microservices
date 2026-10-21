using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ordering.Application.Contracts;
using Ordering.Application.Features.Orders.Consumers;
using Ordering.Infrastructure.Data;
using Ordering.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient; 
using Polly;                     
using System.Text;

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

// Autenticação JWT: a MESMA chave/issuer/audience do Gateway (que emite o token),
// do Catalog.API e do Basket.API.
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

builder.Services.AddOpenApi();
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
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
