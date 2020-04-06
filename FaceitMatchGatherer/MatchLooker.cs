using Database;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FaceitMatchGatherer
{
    public interface IMatchLooker
    {
        Task RefreshActiveUserAsync(int maxUsers, TimeSpan lastActivity);
    }

    public class MatchLooker : IMatchLooker
    {
        private readonly ILogger<MatchLooker> _logger;
        private readonly FaceitContext _context;
        private readonly FaceitMatchesWorker _matchesWorker;

        public MatchLooker(ILogger<MatchLooker> logger, FaceitContext context, FaceitMatchesWorker matchesWorker)
        {
            _logger = logger;
            _context = context;
            _matchesWorker = matchesWorker;
        }
        public async Task RefreshActiveUserAsync(int maxUsers, TimeSpan lastActivity)
        {
            _logger.LogInformation($"Refreshing {maxUsers} users who were active in the last {lastActivity.Days} Days");
            var earliestAllowedActivity = DateTime.UtcNow - lastActivity;

            var usersToRefresh = _context.Users.Where(x => x.LastActivity > earliestAllowedActivity).OrderBy(x => x.LastActivity).Take(maxUsers);
            foreach (var user in usersToRefresh)
            {
                //values 
                await _matchesWorker.WorkUser(user.SteamId, 20, 60);
            }
        }
    }
}
