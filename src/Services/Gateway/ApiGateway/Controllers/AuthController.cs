using ApiGateway.Auth;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly JwtTokenGenerator _tokenGenerator;

        public AuthController(JwtTokenGenerator tokenGenerator)
        {
            _tokenGenerator = tokenGenerator;
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var user = InMemoryUserStore.ValidateCredentials(request.UserName, request.Password);
            if (user is null)
            {
                return Unauthorized(new { message = "Usuário ou senha inválidos." });
            }

            var token = _tokenGenerator.GenerateToken(user);

            return Ok(new
            {
                token,
                userName = user.UserName,
                role = user.Role
            });
        }
    }
}
