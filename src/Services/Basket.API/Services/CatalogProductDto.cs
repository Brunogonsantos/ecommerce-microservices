namespace Basket.API.Services
{
    // Representa só o que o Basket.API precisa saber sobre um produto.
    // Propositalmente não referencia Catalog.API.Entities.Product: em uma
    // arquitetura de microsserviços, cada serviço deve ter seu próprio
    // contrato de dados, para não acoplar o deploy de um ao do outro.
    public class CatalogProductDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
