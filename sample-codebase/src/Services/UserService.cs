using System.Security.Cryptography;
using System.Text;
using SampleApp.Models;

namespace SampleApp.Services;

/// <summary>
/// Service for managing user operations including authentication.
/// </summary>
public class UserService
{
    private readonly List<User> _users = new();
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user with username and password.
    /// </summary>
    /// <param name="username">The username to authenticate</param>
    /// <param name="password">The password to verify</param>
    /// <returns>The authenticated user or null if authentication fails</returns>
    public User? Authenticate(string username, string password)
    {
        _logger.LogInformation("Authenticating user: {Username}", username);

        var user = _users.FirstOrDefault(u =>
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
            u.IsActive);

        if (user == null)
        {
            _logger.LogWarning("User not found or inactive: {Username}", username);
            return null;
        }

        var passwordHash = HashPassword(password);
        if (user.PasswordHash != passwordHash)
        {
            _logger.LogWarning("Invalid password for user: {Username}", username);
            return null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        _logger.LogInformation("User authenticated successfully: {Username}", username);
        return user;
    }

    /// <summary>
    /// Creates a new user account.
    /// </summary>
    public User CreateUser(string username, string email, string password)
    {
        if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Username '{username}' already exists");
        }

        if (_users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Email '{email}' already registered");
        }

        var user = new User
        {
            Id = _users.Count + 1,
            Username = username,
            Email = email,
            PasswordHash = HashPassword(password)
        };

        _users.Add(user);
        _logger.LogInformation("Created new user: {Username} ({Email})", username, email);
        return user;
    }

    /// <summary>
    /// Gets a user by their ID.
    /// </summary>
    public User? GetById(int id)
    {
        return _users.FirstOrDefault(u => u.Id == id);
    }

    /// <summary>
    /// Gets all users in the system.
    /// </summary>
    public IEnumerable<User> GetAll()
    {
        return _users.Where(u => u.IsActive);
    }

    /// <summary>
    /// Updates a user's profile information.
    /// </summary>
    public void UpdateProfile(int userId, string? fullName, string? email)
    {
        var user = GetById(userId);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        if (fullName != null)
            user.FullName = fullName;

        if (email != null && !email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (_users.Any(u => u.Id != userId && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Email '{email}' already registered");
            }
            user.Email = email;
        }

        _logger.LogInformation("Updated profile for user: {UserId}", userId);
    }

    /// <summary>
    /// Deactivates a user account.
    /// </summary>
    public void DeactivateUser(int userId)
    {
        var user = GetById(userId);
        if (user != null)
        {
            user.IsActive = false;
            _logger.LogInformation("Deactivated user: {UserId}", userId);
        }
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
