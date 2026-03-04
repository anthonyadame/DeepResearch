namespace DeepResearch.Api.DTOs.Common;

/// <summary>
/// Generic API response wrapper for all endpoints.
/// </summary>
/// <typeparam name="T">Response data type</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Whether the request was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Response data if successful.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Error information if failed.
    /// </summary>
    public ApiError? Error { get; set; }

    /// <summary>
    /// Session ID for stateful operations.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Correlation ID for request tracking.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Operation metadata.
    /// </summary>
    public ApiMetadata? Metadata { get; set; }

    /// <summary>
    /// Message describing the response.
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Generic API response wrapper without data.
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public ApiError? Error { get; set; }
    public string? CorrelationId { get; set; }
}

/// <summary>
/// API error response structure.
/// </summary>
public class ApiError
{
    /// <summary>
    /// Error code for categorization.
    /// </summary>
    public string Code { get; set; } = "UNKNOWN_ERROR";

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional error details.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Stack trace for debugging.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Inner exception message if available.
    /// </summary>
    public string? InnerException { get; set; }

    /// <summary>
    /// HTTP status code.
    /// </summary>
    public int StatusCode { get; set; } = 500;

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Timestamp when error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Operation metadata included in responses.
/// </summary>
public class ApiMetadata
{
    /// <summary>
    /// Operation duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>
    /// Number of iterations completed.
    /// </summary>
    public int? IterationsCompleted { get; set; }

    /// <summary>
    /// Quality score if applicable (0.0-1.0).
    /// </summary>
    public double? QualityScore { get; set; }

    /// <summary>
    /// Number of items processed.
    /// </summary>
    public int? ItemsProcessed { get; set; }

    /// <summary>
    /// Request timestamp.
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Response timestamp.
    /// </summary>
    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// API version used.
    /// </summary>
    public string ApiVersion { get; set; } = "v1.0";
}

/// <summary>
/// Session context for stateful operations.
/// </summary>
public class SessionContextDto
{
    /// <summary>
    /// Unique session identifier.
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User identifier if available.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Session creation time.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Session expiration time.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Custom session metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Configuration DTO for per-request overrides.
/// </summary>
public class ConfigurationDto
{
    /// <summary>
    /// Configuration key-value pairs.
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new();

    /// <summary>
    /// Feature flags.
    /// </summary>
    public Dictionary<string, bool> Features { get; set; } = new();

    /// <summary>
    /// Service-specific overrides.
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> ServiceOverrides { get; set; } = new();
}

/// <summary>
/// Pagination parameters.
/// </summary>
public class PaginationDto
{
    /// <summary>
    /// Page number (1-based).
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Items per page.
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Sort field.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort direction: asc or desc.
    /// </summary>
    public string SortDirection { get; set; } = "asc";
}

/// <summary>
/// Paginated response wrapper.
/// </summary>
/// <typeparam name="T">Item type</typeparam>
public class PaginatedResponse<T>
{
    /// <summary>
    /// Items in current page.
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Total number of items across all pages.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

    /// <summary>
    /// Whether there are more pages.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// Whether there are previous pages.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;
}

/// <summary>
/// Async operation response for background tasks.
/// </summary>
public class AsyncOperationResponse
{
    /// <summary>
    /// Unique job identifier.
    /// </summary>
    public string JobId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Current status: Pending, Processing, Completed, Failed, Cancelled.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int ProgressPercentage { get; set; }

    /// <summary>
    /// Status message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// URL to check status.
    /// </summary>
    public string? StatusUrl { get; set; }

    /// <summary>
    /// URL to get results when complete.
    /// </summary>
    public string? ResultsUrl { get; set; }

    /// <summary>
    /// When the job was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the job was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Estimated completion time.
    /// </summary>
    public DateTime? EstimatedCompletionTime { get; set; }

    /// <summary>
    /// Error information if failed.
    /// </summary>
    public ApiError? Error { get; set; }
}
