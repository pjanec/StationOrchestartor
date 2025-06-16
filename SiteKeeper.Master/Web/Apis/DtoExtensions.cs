using System.Collections.Generic;
using System.Text.Json;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Provides extension methods for DTOs, e.g., for converting to a dictionary for audit logging.
    /// </summary>
    public static class DtoExtensions
    {
        /// <summary>
        /// Converts an object to a dictionary of its public properties and their values.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="dto">The object to convert.</param>
        /// <returns>A dictionary representation of the object, or an empty dictionary if the object is null.</returns>
        /// <remarks>
        /// This is a basic reflection-based example. For production, consider a more robust serializer 
        /// or manual mapping for performance and control, especially for complex objects or sensitive data.
        /// </remarks>
        public static Dictionary<string, object> ToDictionary<T>(this T dto) where T : class
        {
            if (dto == null)
            {
                return new Dictionary<string, object>();
            }

            var json = JsonSerializer.Serialize(dto);
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            return dictionary ?? new Dictionary<string, object>();
        }
    }
} 