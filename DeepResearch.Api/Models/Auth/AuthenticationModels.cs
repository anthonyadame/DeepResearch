using System;
using System.ComponentModel.DataAnnotations;

namespace DeepResearch.Api.Models.Auth;

/// <summary>
/// Authentication request model for user login.
/// </summary>
public class AuthenticationRequestDto
{
    /// <summary>User identifier or email address.</summary>
    [Required(ErrorMessage = "Username is required")]
    [MinLength(3, ErrorMessage = "Username must be at least 3 characters")]
    public string Username { get; set; } = string.Empty;

    /// <summary>User password.</summary>
    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Authentication response model with JWT token.
/// </summary>
public class AuthenticationResponseDto
{
    /// <summary>JWT bearer token for authenticated requests.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Token type (always "Bearer").</summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>Expiration time in seconds.</summary>
    public int ExpiresIn { get; set; }

    /// <summary>Authenticated user information.</summary>
    public UserDto? User { get; set; }
}

/// <summary>
/// User information model.
/// </summary>
public class UserDto
{
    /// <summary>User ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>User roles.</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>When user was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// JWT token claims information.
/// </summary>
public class TokenClaimsDto
{
    /// <summary>Subject (user ID).</summary>
    public string Sub { get; set; } = string.Empty;

    /// <summary>Username claim.</summary>
    public string Preferred_Username { get; set; } = string.Empty;

    /// <summary>User roles.</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>Token issue time (Unix timestamp).</summary>
    public long Iat { get; set; }

    /// <summary>Token expiration time (Unix timestamp).</summary>
    public long Exp { get; set; }

    /// <summary>Audience (API identifier).</summary>
    public string Aud { get; set; } = "deepresearch-api";
}
