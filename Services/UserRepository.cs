using Microsoft.EntityFrameworkCore;
using RekovaBE_CSharp.Data;
using RekovaBE_CSharp.Models;

namespace RekovaBE_CSharp.Services
{
    public interface IUserRepository
    {
        Task<User?> GetUserByIdAsync(int id);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserByEmailAsync(string email);
        Task UpdateUserAsync(User user);
    }

    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users
                .Include(u => u.AssignedCustomers)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await _context.Users
                // FIXED: Handle nullable IsActive
                .FirstOrDefaultAsync(u => u.Username == username && (u.IsActive == true));
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users
                // FIXED: Handle nullable IsActive
                .FirstOrDefaultAsync(u => u.Email == email && (u.IsActive == true));
        }

        public async Task UpdateUserAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }
    }
}