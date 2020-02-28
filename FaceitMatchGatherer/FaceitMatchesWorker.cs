using Database;
using Entities.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitCommunicationLib.Interfaces;
using RabbitCommunicationLib.TransferModels;
using FaceitMatchGatherer.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FaceitMatchGatherer
{
    public interface IFaceitMatchesWorker
    {
        Task<bool> WorkUser(long steamId, int maxMatches, int maxAgeInDays, UserSubscription userSubscription);
    }

    /// <summary>
    /// Class for orchestrating all the work regarding the matchgathering process for a user.
    /// </summary>
    public class FaceitMatchesWorker : IFaceitMatchesWorker
    {
        private ILogger<FaceitMatchesWorker> _logger;
        private readonly FaceitContext _context;
        private readonly IFaceitApiCommunicator _apiCommunicator;
        private readonly IProducer<DemoEntryInstructions> _rabbitProducer;

        public FaceitMatchesWorker(ILogger<FaceitMatchesWorker> logger, FaceitContext context, IFaceitApiCommunicator apiCommunicator, IProducer<DemoEntryInstructions> rabbitProducer)
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
        public async Task<bool> WorkUser(long steamId, int maxMatches, int maxAgeInDays, UserSubscription userSubscription)
        {

            var requestedQuality = QualityPerSubscription.Qualities[userSubscription];

            // Get new matches
            var matches = await GetNewMatches(steamId, maxMatches, maxAgeInDays, requestedQuality);

            foreach (var match in matches)
            {
                var demoInDb = _context.Matches.SingleOrDefault(x => x.FaceitMatchId == match.FaceitMatchId);

                //TODO make a pretty upsert
                if (demoInDb == null)
                    _context.Matches.Add(new Match
                    {
                        FaceitMatchId = match.FaceitMatchId,
                        AnalyzedQuality = requestedQuality
                    });
                else
                    demoInDb.AnalyzedQuality = requestedQuality;
               

                await _context.SaveChangesAsync();

                // Create rabbit transfer model
                var model = match.ToTransferModel();

                // Publish to rabbit queue
                _rabbitProducer.PublishMessage(new Guid().ToString(), model);
            }

            var matchesFound = matches.Any();

            return matchesFound;
        }

        private async Task<List<FaceitMatchData>> GetNewMatches(long steamId, int maxMatches, int maxAgeInDays, RabbitCommunicationLib.Enums.AnalyzerQuality requestedQuality)
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


            // Remove matches already in db with higher or equal quality
            var knownMatchIds = _context.Matches
                .Where(x => recentMatches.Select(y => y.FaceitMatchId).Contains(x.FaceitMatchId)).Where(y => y.AnalyzedQuality >= requestedQuality)
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
