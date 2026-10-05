using Catalog.API.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Scalar.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Adicionar serviços ao container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Autenticação JWT: a MESMA chave/issuer/audience configuradas no Gateway
// (Jwt:Key / Jwt:Issuer / Jwt:Audience) precisam bater aqui, senão todo
// token emitido pelo login é rejeitado como inválido.
var jwtKey = builder.Configuration["Jwt:Key"] ?? "DevOnly_ChaveSecreta_TrocarEmProducao_2026!!";
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

// Configurando o EF Core com SQL Server
builder.Services.AddDbContext<CatalogContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CatalogConn")));

var app = builder.Build();

// Configurar o pipeline de requisições HTTP.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Adiciona a interface visual interativa do Scalar
    app.MapScalarApiReference(); // Acessível em /scalar/v1
}
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Bloco para garantir que o banco e o seed sejam criados automaticamente ao subir a API.
// Usa retry com backoff, pois em docker-compose o SQL Server costuma demorar
// bem mais para aceitar conexões do que o tempo que a API leva para subir.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    var retryPolicy = Policy
        .Handle<SqlException>()
        .Or<InvalidOperationException>()
        .WaitAndRetry(
            retryCount: 5,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            onRetry: (exception, timeSpan, retryCount, _) =>
            {
                logger.LogWarning(
                    "[Resiliência] Falha ao conectar ao SQL Server. Tentativa {RetryCount} em {Seconds}s. Erro: {Message}",
                    retryCount, timeSpan.TotalSeconds, exception.Message);
            });

    try
    {
        retryPolicy.Execute(() =>
        {
            var context = services.GetRequiredService<CatalogContext>();
            context.Database.EnsureCreated();
        });
    }
    catch (Exception ex)
    {
        // Esgotou as tentativas: loga como erro crítico em vez de deixar a API
        // subir silenciosamente sem banco.
        logger.LogCritical(ex, "Não foi possível criar/conectar ao banco de dados do Catalog.API após múltiplas tentativas.");
    }
}

app.Run();
