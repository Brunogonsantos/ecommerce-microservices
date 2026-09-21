namespace Ordering.Domain.Entities
{
    public class Order
    {
        public int Id { get; set; }

        // Chave de idempotência: mesma origem do CheckoutId gerado no Basket.API.
        // Um índice único no banco (ver OrderContext) garante, mesmo sob concorrência,
        // que a mesma tentativa de checkout nunca gere dois pedidos.
        public Guid CheckoutId { get; set; }

        public string UserName { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }

        // Endereço de Entrega
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string AddressLine { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
    }
}