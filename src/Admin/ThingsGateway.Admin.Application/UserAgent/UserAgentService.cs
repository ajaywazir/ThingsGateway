using ThingsGateway.NewLife.Caching;

namespace ThingsGateway.Admin.Application
{
    /// <summary>
    /// The UserAgent service
    /// </summary>
    /// <seealso cref="ThingsGateway.Admin.Application.IUserAgentService" />
    public class UserAgentService : IUserAgentService
    {
        /// <summary>
        /// Gets or sets the settings.
        /// </summary>
        public UserAgentSettings Settings { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserAgentService"/> class.
        /// </summary>
        public UserAgentService()
        {
            Settings = new UserAgentSettings();
        }

        private MemoryCache MemoryCache { get; set; } = new();

        /// <summary>
        /// Parses the specified user agent string.
        /// </summary>
        /// <param name="userAgentString">The user agent string.</param>
        /// <returns>
        /// An UserAgent object
        /// </returns>
        public UserAgent? Parse(string? userAgentString)
        {
            userAgentString = ((userAgentString?.Length > Settings.UaStringSizeLimit) ? userAgentString?.Trim().Substring(0, Settings.UaStringSizeLimit) : userAgentString?.Trim()) ?? "";
            return MemoryCache.GetOrAdd(userAgentString, entry =>
            {
                return new UserAgent(Settings, userAgentString);
            });
        }

    }
}
