using Catalog.API.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Adicionar serviços ao container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();


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
app.UseAuthorization();

app.MapControllers();

// Bloco para garantir que o banco e o seed sejam criados automaticamente ao subir a API
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<CatalogContext>();
        context.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocorreu um erro ao criar o banco de dados.");
    }
}

app.Run();