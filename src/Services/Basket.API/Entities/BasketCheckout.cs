namespace Basket.API.Entities
{
    public class BasketCheckout
    {
        public string UserName { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        
        // Dados de Entrega / Pagamento para o Checkout
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string AddressLine { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
    }
}