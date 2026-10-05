using ApiGateway.Auth;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Adiciona os serviços do YARP lendo as configurações do appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Habilita o AuthController (login), separado do proxy reverso das outras rotas
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var jwtSettings = new JwtSettings
{
    // Fallback só para rodar localmente fora do Docker sem configurar nada.
    // Em Docker, isso vem sempre sobrescrito pelas variáveis de ambiente
    // Jwt__Key/Jwt__Issuer/Jwt__Audience (ver docker-compose.yml), e a MESMA
    // chave precisa estar configurada no Catalog.API para a validação bater.
    Key = builder.Configuration["Jwt:Key"] ?? "DevOnly_ChaveSecreta_TrocarEmProducao_2026!!",
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "ecommerce-microservices",
    Audience = builder.Configuration["Jwt:Audience"] ?? "ecommerce-microservices-clients",
    ExpiryMinutes = int.TryParse(builder.Configuration["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 60
};
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<JwtTokenGenerator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();

// Mapeia o proxy reverso para interceptar as demais requisições
app.MapReverseProxy();

app.Run();
