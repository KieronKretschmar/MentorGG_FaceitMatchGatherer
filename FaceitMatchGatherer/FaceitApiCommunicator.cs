using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace FaceitMatchGatherer
{
    public interface IFaceitApiCommunicator
    {
        Task<string> GetDemoUrl(string faceitMatchId);
        Task<long> GetSteamId(string faceItName);
        Task<IEnumerable<FaceitMatchData>> GetPlayerMatches(long steamId, int maxMatches, int maxAgeInDays);
    }

    /// <summary>
    /// Class for communicating with Faceit's Data API
    /// For more information, see https://developers.faceit.com/docs/tools/data-api
    /// 
    /// Requires environment variables: ["FACEIT_API_KEY"]
    /// </summary>
    public class FaceitApiCommunicator : IFaceitApiCommunicator
    {
        private ILogger<FaceitApiCommunicator> _logger;

        private HttpClient Client { get; set; }

        public FaceitApiCommunicator(ILogger<FaceitApiCommunicator> logger, IConfiguration configuration)
        {
            _logger = logger;
            Client = new HttpClient();
            var apikey = configuration.GetValue<string>("FACEIT_API_KEY");
            Client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apikey);
            Client.DefaultRequestHeaders.Add("Accept", "application/json");
        }
        
        public async Task<long> GetSteamId(string faceItName)
        {
            var response = await Client.GetAsync("https://open.faceit.com/data/v4/players?nickname=" + faceItName);
            string responseString = await response.Content.ReadAsStringAsync();
            JObject json = JObject.Parse(responseString);
            var steamId = long.Parse(json["steam_id_64"].ToString());
            return steamId;
        }

        public async Task<IEnumerable<FaceitMatchData>> GetPlayerMatches(long steamId, int maxMatches, int maxAgeInDays)
        {
            List<FaceitMatchData> matches = new List<FaceitMatchData>();

            //Gather Matchlist from API as JSON
            var response = await Client.GetAsync("https://open.faceit.com/data/v4/players/" + steamId + "/history?game=csgo&limit=" + maxMatches + "&from=0&offset=0");
            string responseString = await response.Content.ReadAsStringAsync();
            JObject json = JObject.Parse(responseString);

            if (!json.ContainsKey("items"))
            {
                var errorMessage = $"Invalid Faceit API key. Response: {json.ToString()}";
                _logger.LogError(errorMessage);
                throw new InvalidApiKeyException(errorMessage);
            }

            var jsonMatches = json["items"];
            // Happens when API Limit exceeded
            if (jsonMatches == null)
            {
                var errorMessage = $"No 'items' found in response from Faceit api. Assuming Faceit API limit exceeded. {json.ToString()}";
                _logger.LogError(errorMessage);
                throw new ExceededApiLimitException(errorMessage);
            }

            // Skip matches that lie more than 2 weeks in the past or the date could not be parsed
            // Add the rest
            foreach (var jsonMatch in jsonMatches)
            {
                try
                {
                    var match = new FaceitMatchData(steamId, jsonMatch);

                    //Add or skip FaceitMatchData
                    if (DateTime.Now < match.StartedAt.AddDays(maxAgeInDays))
                    {
                        matches.Add(match);
                    }
                }
                catch (FormatException e)
                {
                    _logger.LogError(e, $"Error reading Faceit match with id .");
                    continue;
                }
            }            

            return matches;
        }


        public async Task<string> GetDemoUrl(string faceitMatchId)
        {
            var response = await Client.GetAsync("https://open.faceit.com/data/v4/matches/" + faceitMatchId);
            string responseString = await response.Content.ReadAsStringAsync();
            JObject json = JObject.Parse(responseString);

            if (json == null)
                return "";

            var demoUrlField = json["demo_url"];
            if (demoUrlField == null || demoUrlField.Count() == 0)
                return "";

            string demoUrl = demoUrlField.First().ToString();
            return demoUrl;
        }

        public class ExceededApiLimitException : Exception
        {
            public ExceededApiLimitException(string message) : base(message)
            {
            }

            public ExceededApiLimitException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }

        public class InvalidApiKeyException : Exception
        {
            public InvalidApiKeyException(string message) : base(message)
            {
            }

            public InvalidApiKeyException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }

    }
}
