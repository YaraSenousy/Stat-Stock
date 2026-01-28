using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StatStock.Domain.Entities;
using StatStock.Domain.Enums;
using StatStock.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace StatStock.Infrastructure.Services;

/// <summary>
/// Custom user authentication service to replace ASP.NET Identity.
/// Handles user management, password hashing, and validation.
/// </summary>
public interface ICustomUserService
{
    Task<ApplicationUser?> AuthenticateAsync(string email, string password);
    Task<(bool success, string? message)> CreateUserAsync(string email, string fullName, string password, string role);
    Task<ApplicationUser?> GetUserByEmailAsync(string email);
    Task<ApplicationUser?> GetUserByIdAsync(string id);
    Task<(bool success, string? message)> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
    Task<IEnumerable<ApplicationUser>> GetAllUsersAsync();
    Task<(bool success, string? message)> DeleteUserAsync(string userId);
    Task<(bool success, string? message)> UpdateUserAsync(ApplicationUser user);
}

public class CustomUserService : ICustomUserService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<CustomUserService> _logger;
    private const int SaltSize = 16;
    private const int HashSize = 20;
    private const int Iterations = 10000;

    public CustomUserService(ApplicationDbContext dbContext, ILogger<CustomUserService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApplicationUser?> AuthenticateAsync(string email, string password)
    {
        try
        {
            var user = await _dbContext.ApplicationUsers.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return null;

            if (!VerifyPassword(password, user.PasswordHash!))
                return null;

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating user {Email}", email);
            return null;
        }
    }

    public async Task<(bool success, string? message)> CreateUserAsync(string email, string fullName, string password, string role)
    {
        try
        {
            // Validate password
            if (!IsPasswordValid(password))
                return (false, "Password must be at least 6 characters and contain uppercase, lowercase, digit, and special character");

            // Check if user exists
            var existingUser = await _dbContext.ApplicationUsers.FirstOrDefaultAsync(u => u.Email == email);
            if (existingUser != null)
                return (false, "User with this email already exists");

            var nameParts = fullName.Split(' ', 2);
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                UserName = email,
                FirstName = nameParts[0],
                LastName = nameParts.Length > 1 ? nameParts[1] : "",
                Role = Enum.Parse<UserRole>(role),
                Area = "Default",
                PasswordHash = HashPassword(password),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ApplicationUsers.Add(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User {Email} created successfully with role {Role}", email, role);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Email}", email);
            return (false, "Error creating user: " + ex.Message);
        }
    }

    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        return await _dbContext.ApplicationUsers.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(string id)
    {
        return await _dbContext.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<(bool success, string? message)> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null)
                return (false, "User not found");

            if (!VerifyPassword(currentPassword, user.PasswordHash!))
                return (false, "Current password is incorrect");

            if (!IsPasswordValid(newPassword))
                return (false, "New password doesn't meet requirements");

            user.PasswordHash = HashPassword(newPassword);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Password changed for user {UserId}", userId);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            return (false, "Error changing password: " + ex.Message);
        }
    }

    public async Task<IEnumerable<ApplicationUser>> GetAllUsersAsync()
    {
        return await _dbContext.ApplicationUsers.ToListAsync();
    }

    public async Task<(bool success, string? message)> DeleteUserAsync(string userId)
    {
        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null)
                return (false, "User not found");

            _dbContext.ApplicationUsers.Remove(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deleted", userId);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", userId);
            return (false, "Error deleting user: " + ex.Message);
        }
    }

    public async Task<(bool success, string? message)> UpdateUserAsync(ApplicationUser user)
    {
        try
        {
            _dbContext.ApplicationUsers.Update(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User {UserId} updated", user.Id);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", user.Id);
            return (false, "Error updating user: " + ex.Message);
        }
    }

    /// <summary>
    /// Hashes a password using PBKDF2 with SHA-256.
    /// </summary>
    private string HashPassword(string password)
    {
        using (var rng = new RNGCryptoServiceProvider())
        {
            byte[] salt = new byte[SaltSize];
            rng.GetBytes(salt);

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(HashSize);
                byte[] hashWithSalt = new byte[SaltSize + HashSize];
                Array.Copy(salt, 0, hashWithSalt, 0, SaltSize);
                Array.Copy(hash, 0, hashWithSalt, SaltSize, HashSize);

                return Convert.ToBase64String(hashWithSalt);
            }
        }
    }

    /// <summary>
    /// Verifies a password against a hash.
    /// </summary>
    private bool VerifyPassword(string password, string hash)
    {
        byte[] hashWithSalt = Convert.FromBase64String(hash);
        byte[] salt = new byte[SaltSize];
        Array.Copy(hashWithSalt, 0, salt, 0, SaltSize);

        using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
        {
            byte[] computedHash = pbkdf2.GetBytes(HashSize);
            for (int i = 0; i < HashSize; i++)
            {
                if (hashWithSalt[i + SaltSize] != computedHash[i])
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates password requirements:
    /// - Minimum 6 characters
    /// - At least one uppercase letter
    /// - At least one lowercase letter
    /// - At least one digit
    /// - At least one special character
    /// </summary>
    private bool IsPasswordValid(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 6)
            return false;

        bool hasUppercase = password.Any(char.IsUpper);
        bool hasLowercase = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

        return hasUppercase && hasLowercase && hasDigit && hasSpecial;
    }
}
