using System.Net;
using System.Net.Http.Json;

namespace Basket.API.Services
{
    public interface ICatalogService
    {
        // Retorna null se o produto não existir no catálogo (404).
        // Deixa propagar a exceção se a chamada falhar por outro motivo
        // (timeout, circuito aberto, 5xx) — quem chama decide como reagir.
        Task<CatalogProductDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default);
    }

    public class CatalogService : ICatalogService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CatalogService> _logger;

        public CatalogService(HttpClient httpClient, ILogger<CatalogService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CatalogProductDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"api/v1/Catalog/{productId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<CatalogProductDto>(cancellationToken: cancellationToken);
        }
    }
}
