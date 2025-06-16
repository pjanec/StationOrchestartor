namespace SiteKeeper.Shared.DTOs.Common
{
    /// <summary>
    /// Standard error response structure for API errors across the SiteKeeper system.
    /// </summary>
    /// <remarks>
    /// This DTO is used to provide a consistent format for error messages returned by API endpoints.
    /// It includes a machine-readable error code, a human-readable message, and optional detailed information.
    /// This structure is based on the ErrorResponse schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class ErrorResponse
    {
        /// <summary>
        /// A short, machine-readable error code or type (e.g., "ResourceNotFound", "Unauthorized", "InvalidInput").
        /// Matches the 'error' field in the Swagger definition.
        /// </summary>
        /// <example>"ValidationError"</example>
        public string? Error { get; set; }

        /// <summary>
        /// A human-readable message describing the error.
        /// Matches the 'message' field in the Swagger definition.
        /// </summary>
        /// <example>"One or more validation errors occurred."</example>
        public string Message { get; set; }

        /// <summary>
        /// Optional, more detailed error information. This can be a string, a dictionary of validation errors
        /// (e.g., field name to error message list), or a custom object providing further context about the error.
        /// Matches the 'details' field in the Swagger definition.
        /// </summary>
        /// <example>{"fieldName": ["Error message 1", "Error message 2"]}</example>
        public object? Details { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorResponse"/> class.
        /// </summary>
        public ErrorResponse()
        {
            Error = null;
            Message = string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorResponse"/> class with specified error code and message.
        /// </summary>
        /// <param name="error">The error code or type. Matches the 'error' field in Swagger.</param>
        /// <param name="message">The human-readable error message.</param>
        /// <param name="details">Optional detailed error information.</param>
        public ErrorResponse(string? error, string message, object? details = null)
        {
            Error = error;
            Message = message;
            Details = details;
        }
    }
} 