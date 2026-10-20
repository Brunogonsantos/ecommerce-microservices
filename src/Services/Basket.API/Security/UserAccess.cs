using System.Security.Claims;

namespace Basket.API.Security
{
    // Regra de acesso por dono do recurso: um usuário comum só mexe nos próprios
    // dados; o papel Admin pode acessar os de qualquer pessoa.
    public static class UserAccess
    {
        // Procura o nome do usuário em mais de um tipo de claim, porque o nome
        // do claim muda conforme o mapeamento de claims do JWT (ex: "unique_name"
        // vs ClaimTypes.Name).
        public static string? GetUserName(this ClaimsPrincipal? user) =>
            user?.FindFirst(ClaimTypes.Name)?.Value
            ?? user?.FindFirst("unique_name")?.Value
            ?? user?.FindFirst("sub")?.Value
            ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        public static bool IsAdmin(this ClaimsPrincipal? user) => user?.IsInRole("Admin") ?? false;

        public static bool CanAccess(this ClaimsPrincipal? user, string? userName)
        {
            if (user is null || string.IsNullOrEmpty(userName))
            {
                return false;
            }

            return user.IsAdmin()
                || string.Equals(user.GetUserName(), userName, StringComparison.Ordinal);
        }
    }
}
