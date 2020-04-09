using Database;
using Entities.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitCommunicationLib.Interfaces;
using RabbitCommunicationLib.TransferModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitCommunicationLib.Enums;
using System.Net.Http;

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
        private readonly IProducer<DemoInsertInstruction> _rabbitProducer;
        private readonly IUserIdentityRetriever _userIdentityRetriever;

        public FaceitMatchesWorker(ILogger<FaceitMatchesWorker> logger, FaceitContext context, IFaceitApiCommunicator apiCommunicator, IProducer<DemoInsertInstruction> rabbitProducer, IUserIdentityRetriever userIdentityRetriever)
        {
            _logger = logger;
            _context = context;
            _apiCommunicator = apiCommunicator;
            _rabbitProducer = rabbitProducer;
            _userIdentityRetriever = userIdentityRetriever;
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
            _logger.LogInformation($"Working user with steamId [ {steamId} ], maxMatches [ {maxMatches} ] and maxAgeInDays [ {maxAgeInDays} ]");

            var quality = await _userIdentityRetriever.GetAnalyzerQualityAsync(steamId); 
            // Get new matches
            var matches = await GetNewMatches(steamId, maxMatches, maxAgeInDays, quality);

            foreach (var match in matches)
            {
                var demoInDb = _context.Matches.SingleOrDefault(x => x.FaceitMatchId == match.FaceitMatchId);

                if (demoInDb == null)
                {
                    _logger.LogInformation($"Found new match with FaceitMatchId [ {match.FaceitMatchId} ] for user with steamId [ {steamId} ]");

                    var newMatch = new Match
                    {
                        FaceitMatchId = match.FaceitMatchId,
                        AnalyzedQuality = quality
                    };

                    // Add to database
                    _context.Matches.Add(newMatch);
                    await _context.SaveChangesAsync();

                    // Publish to rabbit queue
                    _rabbitProducer.PublishMessage(match.ToTransferModel());

                    _logger.LogInformation($"Updated and published model with DownloadUrl [ {match.DownloadUrl} ] from uploader#{match.UploaderId} to queue.");
                }
                else if (demoInDb.AnalyzedQuality < quality)
                {
                    _logger.LogInformation($"Found match to re-analyze with FaceitMatchId [ {match.FaceitMatchId} ] for user with steamId [ {steamId} ]. Previous quality [ {demoInDb.AnalyzedQuality} ], new quality [ {quality} ]");

                    // update database
                    demoInDb.AnalyzedQuality = quality;
                    await _context.SaveChangesAsync();

                    // Publish to rabbit queue
                    _rabbitProducer.PublishMessage(match.ToTransferModel());

                    _logger.LogInformation($"Updated and published model with DownloadUrl [ {match.DownloadUrl} ] from uploader#{match.UploaderId} to queue.");
                }
                else
                {
                    // match is already known in at least the same quality
                    continue;
                }
            }

            // Update user.LastChecked
            var user = await _context.Users.FindAsync(steamId);
            user.LastChecked = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var matchesFound = matches.Any();

            _logger.LogInformation($"Finish working user with steamId [ {steamId} ]");
            return matchesFound;
        }

        /// <summary>
        /// Returns matches that need to be updated as they're either new or only stored in lower quality.
        /// </summary>
        /// <param name="steamId"></param>
        /// <param name="maxMatches"></param>
        /// <param name="maxAgeInDays"></param>
        /// <param name="requestedQuality"></param>
        /// <returns></returns>
        private async Task<List<FaceitMatchData>> GetNewMatches(long steamId, int maxMatches, int maxAgeInDays, RabbitCommunicationLib.Enums.AnalyzerQuality requestedQuality)
        {
            var faceitPlayerId = _context.Users.Single(x => x.SteamId == steamId).FaceitId;

            IEnumerable<FaceitMatchData> recentMatches;
            try
            {
                recentMatches = await _apiCommunicator.GetPlayerMatches(steamId, faceitPlayerId, maxMatches, maxAgeInDays);
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

            _logger.LogInformation($" [ {newMatches.Count()}/{recentMatches.Count()} ] matches are new for user [ {steamId} ].");

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
