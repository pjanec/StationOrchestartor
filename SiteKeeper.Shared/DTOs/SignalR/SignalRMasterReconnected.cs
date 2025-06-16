using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// DTO for the SignalR message sent by the server after it (re)starts.
    /// Corresponds to the 'MasterReconnected' server-to-client message.
    /// As defined in swagger: #/components/schemas/SignalRMasterReconnected
    /// </summary>
    public class SignalRMasterReconnected
    {
        /// <summary>
        /// A message confirming the master node has reconnected.
        /// </summary>
        /// <example>"Master node reconnected."</example>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
} 