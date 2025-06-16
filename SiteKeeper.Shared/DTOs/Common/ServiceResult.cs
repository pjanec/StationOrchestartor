namespace SiteKeeper.Shared.DTOs.Common
{
    /// <summary>
    /// Represents the outcome of a service operation.
    /// </summary>
    public class ServiceResult
    {
        /// <summary>
        /// Indicates whether the service operation was successful.
        /// </summary>
        public bool IsSuccess { get; protected set; }

        /// <summary>
        /// An optional error code if the operation failed.
        /// </summary>
        public string? ErrorCode { get; protected set; }

        /// <summary>
        /// An optional error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; protected set; }

        protected ServiceResult(bool isSuccess, string? errorCode = null, string? errorMessage = null)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public static ServiceResult Success()
            => new ServiceResult(true);

        public static ServiceResult Failure(string errorCode, string errorMessage)
            => new ServiceResult(false, errorCode, errorMessage);
    }

    /// <summary>
    /// Represents the outcome of a service operation that returns data.
    /// </summary>
    /// <typeparam name="T">The type of the data returned by the operation.</typeparam>
    public class ServiceResult<T> : ServiceResult
    {
        /// <summary>
        /// The data returned by the service operation, if successful.
        /// Will be default(T) if the operation failed.
        /// </summary>
        public T? Data { get; private set; }

        private ServiceResult(T? data, bool isSuccess, string? errorCode = null, string? errorMessage = null)
            : base(isSuccess, errorCode, errorMessage)
        {
            Data = data;
        }

        public static ServiceResult<T> Success(T data)
            => new ServiceResult<T>(data, true);

        public new static ServiceResult<T> Failure(string errorCode, string errorMessage)
            => new ServiceResult<T>(default, false, errorCode, errorMessage);
    }
} 