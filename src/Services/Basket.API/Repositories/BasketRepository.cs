using Basket.API.Entities;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Basket.API.Repositories
{
    public class BasketRepository : IBasketRepository
    {
        private readonly IDistributedCache _redisCache;

        public BasketRepository(IDistributedCache redisCache)
        {
            _redisCache = redisCache ?? throw new ArgumentNullException(nameof(redisCache));
        }

        public async Task<ShoppingCart?> GetBasket(string userName)
        {
            var basketjson = await _redisCache.GetStringAsync(userName);

            if (string.IsNullOrEmpty(basketjson))
                return null;

            return JsonSerializer.Deserialize<ShoppingCart>(basketjson);
        }

        public async Task<ShoppingCart> UpdateBasket(ShoppingCart basket)
        {
            // Serializa o objeto carrinho para JSON e salva no Redis usando o UserName como chave
            await _redisCache.SetStringAsync(basket.UserName, JsonSerializer.Serialize(basket));

            return await GetBasket(basket.UserName) ?? basket;
        }

        public async Task DeleteBasket(string userName)
        {
            await _redisCache.RemoveAsync(userName);
        }
    }
}