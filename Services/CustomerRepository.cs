// Services/CustomerRepository.cs
using Microsoft.EntityFrameworkCore;
using RekovaBE_CSharp.Data;
using RekovaBE_CSharp.Models;

namespace RekovaBE_CSharp.Services
{
    public interface ICustomerRepository
    {
        Task<Customer?> GetCustomerByIdAsync(int id);
        Task<Customer?> GetCustomerByPhoneAsync(string phoneNumber);
        Task<List<Customer>> GetAllCustomersAsync(int page, int pageSize);
        Task<List<Customer>> GetCustomersByOfficerAsync(int officerId);
        Task<int> GetTotalCustomersCountAsync();
        Task<Customer> CreateCustomerAsync(Customer customer);
        Task UpdateCustomerAsync(Customer customer);
    }

    public class CustomerRepository : ICustomerRepository
    {
        private readonly ApplicationDbContext _context;

        public CustomerRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Customer?> GetCustomerByIdAsync(int id)
        {
            return await _context.Customers
                .Include(c => c.AssignedToUser)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Customer?> GetCustomerByPhoneAsync(string phoneNumber)
        {
            return await _context.Customers
                .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);
        }

        public async Task<List<Customer>> GetAllCustomersAsync(int page, int pageSize)
        {
            return await _context.Customers
                .Include(c => c.AssignedToUser)
                .Where(c => c.IsActive == true)  // FIXED: Compare with true for nullable bool
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<List<Customer>> GetCustomersByOfficerAsync(int officerId)
        {
            return await _context.Customers
                .Include(c => c.AssignedToUser)
                .Where(c => c.AssignedToUserId == officerId && c.IsActive == true)  // FIXED
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<int> GetTotalCustomersCountAsync()
        {
            return await _context.Customers
                .Where(c => c.IsActive == true)  // FIXED
                .CountAsync();
        }

        public async Task<Customer> CreateCustomerAsync(Customer customer)
        {
            customer.CreatedAt = DateTime.UtcNow;
            customer.UpdatedAt = DateTime.UtcNow;
            customer.IsActive = true;
            
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task UpdateCustomerAsync(Customer customer)
        {
            customer.UpdatedAt = DateTime.UtcNow;
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
        }
    }
}