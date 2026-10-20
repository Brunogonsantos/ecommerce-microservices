using System.Security.Claims;

namespace Ordering.API.Security
{
    // Regra de acesso por dono do recurso: um usuário comum só vê os próprios
    // pedidos; o papel Admin pode ver os de qualquer pessoa.
    // (Mesma regra do Basket.API: cada serviço mantém sua cópia para não criar
    // um projeto compartilhado só por causa de 3 métodos.)
    public static class UserAccess
    {
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
