using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitCommunicationLib.Enums;
using System.Net.Http;
using System.Threading.Tasks;

namespace FaceitMatchGatherer
{
    public interface IUserIdentityRetriever
    {
        public Task<AnalyzerQuality> GetAnalyzerQualityAsync(long steamId);
    }

    /// <summary>
    /// Responsible for retreiving UserIdentity Information.
    /// </summary>
    public class UserIdentityRetriever : IUserIdentityRetriever
    {
        private readonly ILogger<UserIdentityRetriever> _logger;
        private readonly HttpClient Client;


        public UserIdentityRetriever(IHttpClientFactory httpClientFactory, ILogger<UserIdentityRetriever> logger)
        {
            _logger = logger;
            Client = httpClientFactory.CreateClient("mentor-interface");
        }

        /// <summary>
        /// Gets the analyzer quality associated with a users subscription plan
        /// </summary>
        /// <remarks>defaults to low if user could not be queried</remarks>
        /// <exception cref="HttpRequestException"></exception>
        /// <param name="steamId"></param>
        /// <returns></returns>
        public async Task<AnalyzerQuality> GetAnalyzerQualityAsync(long steamId)
        {
            var response = await Client.GetAsync($"/identity/{steamId}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    $"Getting UserIdentity for SteamId [ {steamId} ]. Response: {response}. Returning AnalyzerQuality.Low");

                return AnalyzerQuality.Low;
            }

            var reponseContent = await response.Content.ReadAsStringAsync();
            var userIdentity = JsonConvert.DeserializeObject<UserIdentity>(reponseContent);

            switch (userIdentity.SubscriptionType)
            {
                case SubscriptionType.Free:
                    return AnalyzerQuality.Low;
                case SubscriptionType.Premium:
                    return AnalyzerQuality.Medium;
                case SubscriptionType.Ultimate:
                    return AnalyzerQuality.High;
                default:
                    _logger.LogWarning($"Defaulting to AnalyzerQuality.Low for unknown UserSubsription of user [ {steamId} ]");
                    return AnalyzerQuality.Low;
            }
        }


        private class UserIdentity
        {
            public int ApplicationUserId { get; set; }
            public long SteamId { get; set; }
            public SubscriptionType SubscriptionType { get; set; }
            public int DailyMatchesLimit { get; set; }
        }

        private enum SubscriptionType
        {
            Free = 1,
            Premium = 2,
            Ultimate = 3,
        }

    }
}
