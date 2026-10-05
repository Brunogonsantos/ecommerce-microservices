using Catalog.API.Data;
using Catalog.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class CatalogController : ControllerBase
    {
        private readonly CatalogContext _context;
        private readonly ILogger<CatalogController> _logger;

        public CatalogController(CatalogContext context, ILogger<CatalogController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Product>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            var products = await _context.Products.ToListAsync();
            return Ok(products);
        }

        [HttpGet("{id}", Name = "GetProduct")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
        public async Task<ActionResult<Product>> GetProductById(string id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                _logger.LogWarning($"Produto com id {id} não encontrado.");
                return NotFound();
            }
            return Ok(product);
        }

        // Só usuários autenticados com o papel Admin podem criar produto.
        // Antes dessa correção, qualquer requisição anônima conseguia criar
        // produtos no catálogo.
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(Product), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<Product>> CreateProduct([FromBody] Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return CreatedAtRoute("GetProduct", new { id = product.Id }, product);
        }
    }
}
