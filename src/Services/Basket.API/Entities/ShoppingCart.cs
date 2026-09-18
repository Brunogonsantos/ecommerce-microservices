namespace Basket.API.Entities
{
    public class ShoppingCart
    {
        public string UserName { get; set; } = string.Empty;
        public List<ShoppingCartItem> Items { get; set; } = new List<ShoppingCartItem>();

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