using Microsoft.AspNetCore.Identity;

namespace ApiGateway.Auth
{
    public class AppUser
    {
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    // ATENÇÃO: lista fixa em memória, só para demonstrar o fluxo de login/JWT
    // ponta a ponta. Isso NÃO é um substituto para um sistema de usuários real.
    // Antes de qualquer uso além de portfólio/estudo, troque por uma tabela de
    // usuários persistida (SQL Server/Postgres) com um serviço de cadastro,
    // reset de senha, etc. — o hashing de senha (PasswordHasher) já está
    // correto e pode ser reaproveitado nessa migração.
    public static class InMemoryUserStore
    {
        private static readonly PasswordHasher<AppUser> _hasher = new();

        public static readonly List<AppUser> Users = BuildUsers();

        private static List<AppUser> BuildUsers()
        {
            var admin = new AppUser { UserName = "admin", Role = "Admin" };
            admin.PasswordHash = _hasher.HashPassword(admin, "Admin@123");

            var cliente = new AppUser { UserName = "cliente", Role = "Customer" };
            cliente.PasswordHash = _hasher.HashPassword(cliente, "Cliente@123");

            return new List<AppUser> { admin, cliente };
        }

        public static AppUser? ValidateCredentials(string userName, string password)
        {
            var user = Users.FirstOrDefault(u => u.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                return null;
            }

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            return result == PasswordVerificationResult.Success ? user : null;
        }
    }
}
