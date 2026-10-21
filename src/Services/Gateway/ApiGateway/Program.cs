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

// A chave NÃO tem valor padrão no código: um padrão público permitiria forjar
// tokens (inclusive de Admin) caso a configuração faltasse em algum ambiente.
// Em Docker ela vem de Jwt__Key; em desenvolvimento local, do appsettings.Development.json.
// A MESMA chave precisa estar configurada nos serviços que validam o token.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || System.Text.Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key ausente ou curta demais (mínimo de 32 bytes). Defina a variável de ambiente Jwt__Key; " +
        "em desenvolvimento local, ela vem do appsettings.Development.json.");
}

var jwtSettings = new JwtSettings
{
    Key = jwtKey,
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
