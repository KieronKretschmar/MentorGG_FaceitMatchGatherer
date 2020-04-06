using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FaceitMatchGatherer
{
    public interface IPeriodicMatchLooker
    {
        void Dispose();
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }

    public class PeriodicMatchLooker : IHostedService, IDisposable, IPeriodicMatchLooker
    {
        private readonly TimeSpan _interval;
        private readonly IMatchLooker _matchLooker;
        private readonly ILogger<PeriodicMatchLooker> _logger;
        private Timer _timer;

        public PeriodicMatchLooker(TimeSpan interval, IMatchLooker matchLooker, ILogger<PeriodicMatchLooker> logger)
        {
            _interval = interval;
            _matchLooker = matchLooker;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Starting {GetType().Name} ...  - Interval {_interval.Days} days");
            _timer = new Timer(CallMatchLookerAsync, null, TimeSpan.Zero, _interval);

            return Task.CompletedTask;
        }

        private void CallMatchLookerAsync(object state)
        {
            _logger.LogInformation("Periodic user refresh");
            _matchLooker.RefreshActiveUsersAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Stopped {GetType().Name}");
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

    }
}
