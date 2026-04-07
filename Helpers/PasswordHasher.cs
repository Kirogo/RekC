using BCrypt.Net;

namespace RekovaBE_CSharp.Helpers
{
    public static class PasswordHasher
    {
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(10));
        }

        public static bool VerifyPassword(string password, string passwordHash)
        {
            try
            {
                // Check if it's a BCrypt hash (starts with $2a$, $2b$, or $2y$)
                if (passwordHash.StartsWith("$2"))
                {
                    return BCrypt.Net.BCrypt.Verify(password, passwordHash);
                }
                
                // For testing - allow plain text comparison
                if (password == passwordHash)
                {
                    return true;
                }
                
                // Fallback to your custom format if needed
                var parts = passwordHash.Split('.', 3);
                if (parts.Length != 3)
                    return false;

                var iterations = Convert.ToInt32(parts[0]);
                var salt = Convert.FromBase64String(parts[1]);
                var key = Convert.FromBase64String(parts[2]);

                using (var algorithm = new System.Security.Cryptography.Rfc2898DeriveBytes(
                    password,
                    salt,
                    iterations,
                    System.Security.Cryptography.HashAlgorithmName.SHA256))
                {
                    var keyToCheck = algorithm.GetBytes(32);
                    return keyToCheck.SequenceEqual(key);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}