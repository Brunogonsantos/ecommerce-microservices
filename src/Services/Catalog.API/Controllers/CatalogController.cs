using Catalog.API.Data;
using Catalog.API.Entities;
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

        [HttpGet("{id:length(24)}", Name = "GetProduct")]
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

        [HttpPost]
        [ProducesResponseType(typeof(Product), StatusCodes.Status201Created)]
        public async Task<ActionResult<Product>> CreateProduct([FromBody] Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return CreatedAtRoute("GetProduct", new { id = product.Id }, product);
        }
    }
}