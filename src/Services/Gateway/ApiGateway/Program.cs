var builder = WebApplication.CreateBuilder(args);

// Adiciona os serviços do YARP lendo as configurações do appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Mapeia o proxy reverso para interceptar as requisições
app.MapReverseProxy();

app.Run();