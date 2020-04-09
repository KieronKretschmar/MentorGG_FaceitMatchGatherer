using FaceitMatchGatherer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FaceitMatchGathererTests
{
    /// <summary>
    /// Requires environment variables: ["FACEIT_API_KEY"]
    /// </summary>
    [TestClass]
    public class FaceitApiCommunicatorTests
    {
        private readonly IConfiguration config;
        private readonly ServiceProvider serviceProvider;

        public FaceitApiCommunicatorTests()
        {
            var services = new ServiceCollection();
            services.AddLogging(x => x.AddConsole().AddDebug());

            var builder = new ConfigurationBuilder()
                .AddEnvironmentVariables();
            config = builder.Build();

            serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Tests GetSteamId() with real data.
        /// </summary>
        /// <param name="faceitUserId"></param>
        /// <param name="expectedSteamId"></param>
        /// <returns></returns>
        [DataTestMethod]
        [DataRow("bahne222", 76561198033880857)]
        public async Task GetFaceitSteamIdTest(string faceitUserId, long expectedSteamId)
        {
            var apiCommunicator = new FaceitApiCommunicator(serviceProvider.GetService<ILogger<FaceitApiCommunicator>>(), config);
            var steamId = await apiCommunicator.GetSteamId(faceitUserId);
            Assert.AreEqual(expectedSteamId, steamId);
        }

        /// <summary>
        /// Tests GetPlayerMatches() with real data, asserting that not too many and / or too old matches are returned.
        /// </summary>
        /// <param name="steamId"></param>
        /// <param name="maxMatches"></param>
        /// <param name="maxAgeInDays"></param>
        /// <returns></returns>
        [DataTestMethod]
        [DataRow(76561198033880857, "b965f4cf-1bfb-4e13-8b75-66dd7377a319", 100, 10000)]
        [DataRow(76561198033880857, "b965f4cf-1bfb-4e13-8b75-66dd7377a319", 100, 1)]
        [DataRow(76561198033880857, "b965f4cf-1bfb-4e13-8b75-66dd7377a319", 1, 1000)]
        public async Task GetPlayerMatchesTest(long steamId, string faceitPlayerId, int maxMatches, int maxAgeInDays)
        {
            var apiCommunicator = new FaceitApiCommunicator(serviceProvider.GetService<ILogger<FaceitApiCommunicator>>(), config);
            var matches = await apiCommunicator.GetPlayerMatches(steamId, faceitPlayerId, maxMatches, maxAgeInDays);

            // Check if not too many matches were pulled
            Assert.IsTrue(matches.Count() <= maxMatches);

            // Check that there was no match too old
            if (matches.Any())
            {
                var oldestMatchDate = matches.Select(x => x.StartedAt).Min();
                Assert.IsTrue((DateTime.Now - oldestMatchDate).Days <= maxAgeInDays);
            }
        }        
    }
}
