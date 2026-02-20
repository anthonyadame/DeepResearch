using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DeepResearch.Api.Models.Auth;

namespace DeepResearch.Api.Services.Auth;

/// <summary>
/// Service for user authentication and validation.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>Authenticate user with username and password.</summary>
    Task<(bool success, UserDto? user, string? error)> AuthenticateAsync(
        string username, 
        string password);

    /// <summary>Get user by ID.</summary>
    Task<UserDto?> GetUserByIdAsync(string userId);

    /// <summary>Get user by username.</summary>
    Task<UserDto?> GetUserByUsernameAsync(string username);
}

/// <summary>
/// In-memory authentication service for development/testing.
/// IMPORTANT: In production, replace with database-backed implementation.
/// </summary>
public class InMemoryAuthenticationService : IAuthenticationService
{
    private static readonly Dictionary<string, (string Password, UserDto User)> Users = new()
    {
        {
            "admin",
            (
                "admin123", // Simple password for demo - use proper hashing in production!
                new UserDto
                {
                    Id = "user_001",
                    Username = "admin",
                    Roles = new() { "Administrator", "User" },
                    CreatedAt = DateTime.UtcNow
                }
            )
        },
        {
            "research",
            (
                "research123",
                new UserDto
                {
                    Id = "user_002",
                    Username = "research",
                    Roles = new() { "Researcher", "User" },
                    CreatedAt = DateTime.UtcNow
                }
            )
        },
        {
            "viewer",
            (
                "viewer123",
                new UserDto
                {
                    Id = "user_003",
                    Username = "viewer",
                    Roles = new() { "User" },
                    CreatedAt = DateTime.UtcNow
                }
            )
        }
    };

    public Task<(bool success, UserDto? user, string? error)> AuthenticateAsync(
        string username,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Task.FromResult((false, null as UserDto, "Username and password are required"));

        if (Users.TryGetValue(username.ToLower(), out var userEntry))
        {
            // Simple password comparison - NEVER do this in production!
            // Use bcrypt, PBKDF2, or Argon2 for password hashing
            if (userEntry.Password == password)
            {
                return Task.FromResult((true, userEntry.User, null as string));
            }
        }

        return Task.FromResult((false, null as UserDto, "Invalid username or password"));
    }

    public Task<UserDto?> GetUserByIdAsync(string userId)
    {
        foreach (var (_, userEntry) in Users)
        {
            if (userEntry.User.Id == userId)
                return Task.FromResult<UserDto?>(userEntry.User);
        }
        return Task.FromResult<UserDto?>(null);
    }

    public Task<UserDto?> GetUserByUsernameAsync(string username)
    {
        if (Users.TryGetValue(username.ToLower(), out var userEntry))
            return Task.FromResult<UserDto?>(userEntry.User);
        return Task.FromResult<UserDto?>(null);
    }
}
