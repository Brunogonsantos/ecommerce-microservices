namespace EventBus.Messages.Events
{
    public class BasketCheckoutEvent
    {
        // Identificador único da tentativa de checkout, gerado uma única vez
        // pelo Basket.API. É a chave de idempotência: se o MassTransit reentregar
        // essa mesma mensagem (retry de erro transitório), o CheckoutId continua
        // o mesmo, permitindo ao Ordering.API detectar e ignorar duplicatas.
        public Guid CheckoutId { get; set; }

        public string UserName { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        
        // Dados de Entrega / Pagamento
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string AddressLine { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
    }
}