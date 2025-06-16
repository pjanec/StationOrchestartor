namespace SiteKeeper.Master
{
    /// <summary>
    /// Configuration settings for the SiteKeeper Master Agent's web application.
    /// </summary>
    public class MasterConfig
    {
        /// <summary>
        /// Gets or sets the port number for the GUI and general API access.
        /// </summary>
        public int GuiPort { get; set; } = 7001;

        /// <summary>
        /// Gets or sets the port number for the Slave Agent communication (SignalR Agent Hub).
        /// </summary>
        public int AgentPort { get; set; } = 7002;

        /// <summary>
        /// Gets or sets a value indicating whether HTTPS should be used for both ports.
        /// </summary>
        public bool UseHttps { get; set; } = false;

        /// <summary>
        /// Gets or sets the path to the master server's SSL/TLS certificate file (e.g., .pfx or .pem).
        /// Required if UseHttps is true.
        /// </summary>
        public string? MasterCertPath { get; set; }

        /// <summary>
        /// Gets or sets the password for the master server's SSL/TLS certificate file.
        /// Required if UseHttps is true and the certificate is password-protected.
        /// </summary>
        public string? MasterCertPassword { get; set; }

        /// <summary>
        /// Gets or sets the path to the CA certificate file used to validate client certificates from Slave Agents.
        /// If provided, client certificate authentication will be required on the Agent port.
        /// </summary>
        public string? MasterCaCertPath { get; set; }

        /// <summary>
        /// Gets or sets the JWT token issuer.
        /// </summary>
        public string JwtIssuer { get; set; } = "SiteKeeperMaster";

        /// <summary>
        /// Gets or sets the JWT token audience.
        /// </summary>
        public string JwtAudience { get; set; } = "SiteKeeperClients";

        /// <summary>
        /// Gets or sets the JWT secret key. Ensure this is strong and kept secret.
        /// Minimum 16 characters for HS256.
        /// </summary>
        public string JwtSecretKey { get; set; } = "DefaultSuperSecretKeyNeedsChanging"; // IMPORTANT: Change in production!

        /// <summary>
        /// Gets or sets the JWT token expiration in minutes.
        /// </summary>
        public int JwtExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// Gets or sets the Refresh token expiration in days.
        /// </summary>
        public int RefreshTokenExpirationDays { get; set; } = 7;

        /// <summary>
        /// Where the journal root folder is located
        /// </summary>
        public string JournalRootPath { get; set; } = "Journal";

		/// <summary>
		///  the subfolder of the journal root where the master stores its own journal entries for given environment.
		/// </summary>
		public string EnvironmentName  { get; set; } = "MyTestEnv";


        public int HeartbeatIntervalSeconds { get; set; } = 10;
	}
} 