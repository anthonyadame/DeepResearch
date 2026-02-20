using System;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Model.Api;
using DeepResearch.Api.Models.Auth;
using DeepResearch.Api.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DeepResearch.Api.Controllers;

/// <summary>
/// Authentication controller for user login and token generation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthenticationService authService,
        ITokenService tokenService,
        ILogger<AuthController> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticate user and return JWT token.
    /// </summary>
    /// <param name="request">Authentication credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JWT token and user information.</returns>
    /// <response code="200">Authentication successful.</response>
    /// <response code="400">Invalid credentials or request format.</response>
    /// <response code="401">Invalid username or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthenticationResponseDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 400)]
    [ProducesResponseType(typeof(ErrorResponseDto), 401)]
    public async Task<IActionResult> Login(
        [FromBody] AuthenticationRequestDto request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var (success, user, error) = await _authService.AuthenticateAsync(
                request.Username,
                request.Password);

            if (!success || user == null)
            {
                _logger.LogWarning("Authentication failed for username: {Username}", request.Username);
                return Unauthorized(new ErrorResponseDto
                {
                    StatusCode = 401,
                    Message = error ?? "Invalid username or password"
                });
            }

            // Generate JWT token
            var token = _tokenService.GenerateToken(user.Id, user.Username, user.Roles);
            var expirationTime = _tokenService.GetTokenExpiration(token);

            _logger.LogInformation("User authenticated successfully: {Username} ({UserId})", 
                user.Username, user.Id);

            return Ok(new AuthenticationResponseDto
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = (int)(expirationTime - DateTime.UtcNow).TotalSeconds,
                User = user
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            return StatusCode(500, new ErrorResponseDto
            {
                StatusCode = 500,
                Message = "An error occurred during authentication",
                Details = new() { { "error", ex.Message } }
            });
        }
    }

    /// <summary>
    /// Refresh an expired token (if refresh tokens are implemented).
    /// </summary>
    /// <remarks>
    /// This is a placeholder for refresh token functionality.
    /// Currently not implemented - tokens expire after configured duration.
    /// </remarks>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthenticationResponseDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 401)]
    public async Task<IActionResult> Refresh(CancellationToken ct = default)
    {
        // TODO: Implement refresh token functionality
        // For now, users must log in again when token expires
        return Unauthorized(new ErrorResponseDto
        {
            StatusCode = 401,
            Message = "Token refresh not yet implemented. Please login again."
        });
    }

    /// <summary>
    /// Logout (client-side operation).
    /// </summary>
    /// <remarks>
    /// Stateless JWT tokens cannot be invalidated server-side.
    /// Client should discard the token.
    /// </remarks>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> Logout(CancellationToken ct = default)
    {
        _logger.LogInformation("User logout requested");
        return Ok(new { message = "Please discard your token. It will expire after the configured duration." });
    }
}
