namespace Basket.API.Entities
{
    public class ShoppingCart
    {
        public string UserName { get; set; } = string.Empty;
        public List<ShoppingCartItem> Items { get; set; } = new List<ShoppingCartItem>();

        // Chave de idempotência do checkout, atrelada a ESTE carrinho. É gravada no
        // Redis antes de publicar o evento e só some quando o carrinho é apagado ou
        // alterado. Assim, se o processo cair depois do Publish (e antes do
        // DeleteBasket), repetir o checkout reaproveita o mesmo id e o Ordering.API
        // descarta a duplicata. Nunca deve vir do cliente: o UpdateBasket zera.
        public Guid? PendingCheckoutId { get; set; }

        public ShoppingCart()
        {
        }

        public ShoppingCart(string userName)
        {
            UserName = userName;
        }

        // Propriedade calculada para o preço total do carrinho
        public decimal TotalPrice
        {
            get
            {
                decimal totalprec = 0;
                foreach (var item in Items)
                {
                    totalprec += item.Price * item.Quantity;
                }
                return totalprec;
            }
        }
    }
}