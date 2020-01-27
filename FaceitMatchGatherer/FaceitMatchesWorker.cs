using Database;
using Entities.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitTransfer.Interfaces;
using RabbitTransfer.TransferModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FaceitMatchGatherer
{
    public interface IFaceitMatchesWorker
    {
        Task<bool> WorkUser(long steamId, int maxMatches, int maxAgeInDays);
    }

    /// <summary>
    /// Class for orchestrating all the work regarding the matchgathering process for a user.
    /// </summary>
    public class FaceitMatchesWorker : IFaceitMatchesWorker
    {
        private ILogger<FaceitMatchesWorker> _logger;
        private readonly FaceitContext _context;
        private readonly IFaceitApiCommunicator _apiCommunicator;
        private readonly IProducer<GathererTransferModel> _rabbitProducer;

        public FaceitMatchesWorker(ILogger<FaceitMatchesWorker> logger, FaceitContext context, IFaceitApiCommunicator apiCommunicator, IProducer<GathererTransferModel> rabbitProducer)
        {
            _logger = logger;
            _context = context;
            _apiCommunicator = apiCommunicator;
            _rabbitProducer = rabbitProducer;
        }

        /// <summary>
        /// Looks for a new matches for a user, saves them in the database and pushes them to the rabbit queue.
        /// </summary>
        /// <param name="steamId"></param>
        /// <param name="maxMatches"></param>
        /// <param name="maxAgeInDays"></param>
        /// <returns>bool, whether a new match was found</returns>
        public async Task<bool> WorkUser(long steamId, int maxMatches, int maxAgeInDays)
        {
            // Get new matches
            var matches = await GetNewMatches(steamId, maxMatches, maxAgeInDays);

            foreach (var match in matches)
            {
                // Write new matches to database
                _context.Matches.Add(new Match { FaceitMatchId = match.FaceitMatchId });
                await _context.SaveChangesAsync();

                // Create rabbit transfer model
                var model = match.ToTransferModel();

                // Publish to rabbit queue
                _rabbitProducer.PublishMessage(new Guid().ToString(), model);
            }

            var matchesFound = matches.Any();

            return matchesFound;
        }

        private async Task<List<FaceitMatchData>> GetNewMatches(long steamId, int maxMatches, int maxAgeInDays)
        {
            IEnumerable<FaceitMatchData> recentMatches;
            try
            {
                recentMatches = await _apiCommunicator.GetPlayerMatches(steamId, maxMatches, maxAgeInDays);
            }
            catch (Exception e)
            {
                if (e is FaceitApiCommunicator.InvalidApiKeyException || e is FaceitApiCommunicator.ExceededApiLimitException)
                {
                    return new List<FaceitMatchData>();
                }

                throw;
            }


            // Remove matches already in db
            var knownMatchIds = _context.Matches
                .Where(x => recentMatches.Select(y => y.FaceitMatchId).Contains(x.FaceitMatchId))
                .Select(x => x.FaceitMatchId);
            var newMatches = recentMatches.Where(x => !knownMatchIds.Contains(x.FaceitMatchId))
                .ToList();

            // Get DownloadUrls of new matches
            // Only do this for new matches as it requires an API call
            // Iterate backwards through the list to enable removal of error-causing matches
            for (int i = newMatches.Count - 1; i >= 0; i--)
            {
                try
                {
                    var match = newMatches[i];
                    match.DownloadUrl = await _apiCommunicator.GetDemoUrl(match.FaceitMatchId);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error pulling DemoUrl from Faceit API.");
                    newMatches.RemoveAt(i);
                    continue;
                }
            }

            return newMatches;
        }
    }
}
