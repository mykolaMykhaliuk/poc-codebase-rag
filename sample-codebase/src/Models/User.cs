namespace SampleApp.Models;

/// <summary>
/// Represents a user in the system.
/// </summary>
public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public UserRole Role { get; set; } = UserRole.User;

    // Password hash - never store plain text passwords!
    public string? PasswordHash { get; set; }
}

public enum UserRole
{
    User = 0,
    Admin = 1,
    SuperAdmin = 2
}
