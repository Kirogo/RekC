using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using RekovaBE_CSharp.Models;

namespace RekovaBE_CSharp.Services
{
    public class JwtConfigOptions
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
    }

    public interface IAuthService
    {
        string GenerateJwtToken(User user);
        Task<User?> AuthenticateUserAsync(string username, string password);
    }

    public class AuthService : IAuthService
    {
        private readonly JwtConfigOptions _jwtConfig;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IOptions<JwtConfigOptions> jwtConfig, ILogger<AuthService> logger)
        {
            _jwtConfig = jwtConfig.Value;
            _logger = logger;
        }

        public string GenerateJwtToken(User user)
        {
            try
            {
                if (string.IsNullOrEmpty(_jwtConfig.Key) || _jwtConfig.Key.Length < 32)
                {
                    throw new InvalidOperationException($"JWT Key is not properly configured. Length: {_jwtConfig.Key?.Length ?? 0}");
                }

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.Key));

                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var expires = DateTime.UtcNow.AddDays(1);

                // FIXED: Handle null values with null-coalescing operator
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username ?? ""),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim(ClaimTypes.Role, user.Role ?? "officer"),
                    new Claim("firstName", user.FirstName ?? ""),
                    new Claim("lastName", user.LastName ?? ""),
                    new Claim("department", user.Department ?? "Collections")
                };

                var token = new JwtSecurityToken(
                    issuer: _jwtConfig.Issuer,
                    audience: _jwtConfig.Audience,
                    claims: claims,
                    expires: expires,
                    signingCredentials: credentials);

                var encodedToken = new JwtSecurityTokenHandler().WriteToken(token);
                _logger.LogInformation($"JWT token generated for user: {user.Username}");
                return encodedToken;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating JWT token: {ex.Message}");
                throw;
            }
        }

        public Task<User?> AuthenticateUserAsync(string username, string password)
        {
            // This will be implemented in the repository layer
            throw new NotImplementedException("Use UserRepository for authentication");
        }
    }
}