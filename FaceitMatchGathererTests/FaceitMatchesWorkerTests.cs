using Database;
using FaceitMatchGatherer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RabbitCommunicationLib.Interfaces;
using RabbitCommunicationLib.TransferModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitCommunicationLib.Enums;
using Entities.Models;

namespace FaceitMatchGathererTests
{
    [TestClass]
    public class FaceitMatchesWorkerTests
    {
        private readonly ServiceProvider serviceProvider;

        public FaceitMatchesWorkerTests()
        {
            var services = new ServiceCollection();
            services.AddLogging(x => x.AddConsole().AddDebug());

            serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Tests WorkUser() mocking 2 matches to be found, expecting matches to be published to the queue, and written to the database. 
        /// Also checks that trying to add those matches again once more does not lead have an effect on the queue and database.
        /// </summary>
        /// <returns></returns>
        [DataTestMethod]
        [DataRow(76561198033880857, "b965f4cf-1bfb-4e13-8b75-66dd7377a319")]
        public async Task WorkUserTest(long steamId, string faceitPlayerId)
        {
            var options = FaceitTestHelper.GetDatabaseOptions("WorkUserTest");

            var user = new User
            {
                FaceitId = faceitPlayerId,
                SteamId = steamId,
                LastActivity = DateTime.MinValue
            };

            var maxMatches = 20;
            var maxAgeInDays = 60;

            IEnumerable<FaceitMatchData> matches = new List<FaceitMatchData>
            {
                new FaceitMatchData{ FaceitMatchId = "First"},
                new FaceitMatchData{ FaceitMatchId = "Second"},
            };

            // Setup mocks
            var mockFaceitOAuthCommunicator = new Mock<IFaceitOAuthCommunicator>();
            var mockRabbitProducer = new Mock<IProducer<DemoInsertInstruction>>();
            // Setup mockFaceitApiCommunicator such that GetPlayerMatches returns matches
            var mockFaceitApiCommunicator = new Mock<IFaceitApiCommunicator>();
            mockFaceitApiCommunicator
                .Setup(x => x.GetPlayerMatches(It.Is<long>(x => x == steamId), It.IsAny<string>(), It.Is<int>(x => x == maxMatches), It.Is<int>(x => x == maxAgeInDays)))
                .Returns(Task.FromResult(matches));

            var mockIUserIdentityRetriever = new Mock<IUserIdentityRetriever>();
            mockIUserIdentityRetriever.Setup(x => x.GetAnalyzerQualityAsync(It.IsAny<long>())).Returns(Task.FromResult(AnalyzerQuality.High));

            // Call WorkUser and expect it to publish matches to queue and database
            using (var context = new FaceitContext(options))
            {
                context.Add(user);
                await context.SaveChangesAsync();

                var faceitMatchesWorker = new FaceitMatchesWorker(serviceProvider.GetService<ILogger<FaceitMatchesWorker>>(), context, mockFaceitApiCommunicator.Object, mockRabbitProducer.Object, mockIUserIdentityRetriever.Object);
                var foundMatches = await faceitMatchesWorker.WorkUser(steamId, maxMatches, maxAgeInDays);

                Assert.IsTrue(foundMatches);
            }

            // Verify that all matches were written to database and queue
            using (var context = new FaceitContext(options))
            {
                var allMatchesInDb = matches.All(x => context.Matches.Any(y => y.FaceitMatchId == x.FaceitMatchId));
                Assert.IsTrue(allMatchesInDb);

                mockRabbitProducer.Verify(x => x.PublishMessage(It.IsAny<DemoInsertInstruction>(), It.IsAny<string>()), Times.Exactly(matches.Count()));
            }

            // Call endpoint again, expecting no more matches to be added to happen
            using (var context = new FaceitContext(options))
            {
                var faceitMatchesWorker = new FaceitMatchesWorker(serviceProvider.GetService<ILogger<FaceitMatchesWorker>>(), context, mockFaceitApiCommunicator.Object, mockRabbitProducer.Object, mockIUserIdentityRetriever.Object);
                var foundMatches = await faceitMatchesWorker.WorkUser(steamId, 20, 60);
                Assert.IsFalse(foundMatches);

                // Verify that no more messages were published
                mockRabbitProducer.Verify(x => x.PublishMessage(It.IsAny<DemoInsertInstruction>(), It.IsAny<string>()), Times.Exactly(matches.Count()));
            }
        }


        /// <summary>
        /// Tests that a match already in the database is not reported back if the new gather-request comes in with a lower quality
        /// </summary>
        [DataTestMethod]
        [DataRow(76561198033880857, "b965f4cf-1bfb-4e13-8b75-66dd7377a319")]
        public async Task DoNotReturnMatchWithLowerQuality(long steamId, string faceitPlayerId)
        {
            var testOptions = FaceitTestHelper.GetDatabaseOptions("test_DoNotReturnMatchWithHigherQuality");
            bool firstMatchCheck = false;
            bool secondMatchCheck = false;

            using (var context = new FaceitContext(testOptions))
            {
                var user = new User
                {
                    FaceitId = faceitPlayerId,
                    SteamId = steamId,
                    LastActivity = DateTime.MinValue
                };
                context.Add(user);
                await context.SaveChangesAsync();

                var testFaceitMatch = new FaceitMatchData
                {
                    FaceitMatchId = "testFaceitID",
                };
                var mockFaceitAPI = new Mock<IFaceitApiCommunicator>();
                mockFaceitAPI.Setup(x => x.GetPlayerMatches(user.SteamId, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).Returns(Task.FromResult<IEnumerable<FaceitMatchData>>(new List<FaceitMatchData> { testFaceitMatch }));
                mockFaceitAPI.Setup(x => x.GetDemoUrl(It.IsAny<string>())).Returns(Task.FromResult("testDownloadUrl"));

                var mockRabbit = new Mock<IProducer<DemoInsertInstruction>>();
                var mockUserIdentityRetriever = new Mock<IUserIdentityRetriever>();
                mockUserIdentityRetriever.Setup(x => x.GetAnalyzerQualityAsync(It.IsAny<long>())).Returns(Task.FromResult(AnalyzerQuality.High));

                var test = new FaceitMatchesWorker(serviceProvider.GetRequiredService<ILogger<FaceitMatchesWorker>>(), context, mockFaceitAPI.Object, mockRabbit.Object,mockUserIdentityRetriever.Object);

                firstMatchCheck = await test.WorkUser(user.SteamId, 5, 5);
                mockUserIdentityRetriever.Setup(x => x.GetAnalyzerQualityAsync(It.IsAny<long>())).Returns(Task.FromResult(AnalyzerQuality.Low));

                secondMatchCheck = await test.WorkUser(user.SteamId, 5, 5);
            }

            using (var context = new FaceitContext(testOptions))
            {
                Assert.IsTrue(firstMatchCheck);
                Assert.IsFalse(secondMatchCheck);
                Assert.AreEqual(context.Matches.Count(), 1);
                Assert.AreEqual(context.Matches.First().AnalyzedQuality, AnalyzerQuality.High);
            }
        }

        /// <summary>
        /// Tests that a match already in the database is reported back if the new gather-request comes in with a higher quality
        /// </summary>
        [DataTestMethod]
        [DataRow(76561198033880857, "b965f4cf-1bfb-4e13-8b75-66dd7377a319")]
        public async Task DoReturnMatchWithHigherQuality(long steamId, string faceitPlayerId)
        {
            var testOptions = FaceitTestHelper.GetDatabaseOptions("test_DoReturnMatchWithLowerQuality");
            bool firstMatchCheck = false;
            bool secondMatchCheck = false;

            using (var context = new FaceitContext(testOptions))
            {
                var user = new User
                {
                    FaceitId = faceitPlayerId,
                    SteamId = steamId,
                    LastActivity = DateTime.MinValue
                };
                context.Add(user);
                await context.SaveChangesAsync();

                var testFaceitMatch = new FaceitMatchData
                {
                    FaceitMatchId = "testFaceitID",
                };
                var mockFaceitAPI = new Mock<IFaceitApiCommunicator>();
                mockFaceitAPI.Setup(x => x.GetPlayerMatches(user.SteamId, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).Returns(Task.FromResult<IEnumerable<FaceitMatchData>>(new List<FaceitMatchData> { testFaceitMatch }));
                mockFaceitAPI.Setup(x => x.GetDemoUrl(It.IsAny<string>())).Returns(Task.FromResult("testDownloadUrl"));

                var mockRabbit = new Mock<IProducer<DemoInsertInstruction>>();
                var mockUserRetriever = new Mock<IUserIdentityRetriever>();
                mockUserRetriever.Setup(x => x.GetAnalyzerQualityAsync(It.IsAny<long>())).Returns(Task.FromResult(AnalyzerQuality.Low));

                var test = new FaceitMatchesWorker(serviceProvider.GetRequiredService<ILogger<FaceitMatchesWorker>>(), context, mockFaceitAPI.Object, mockRabbit.Object, mockUserRetriever.Object);

                firstMatchCheck = await test.WorkUser(user.SteamId, 5, 5);
                mockUserRetriever.Setup(x => x.GetAnalyzerQualityAsync(It.IsAny<long>())).Returns(Task.FromResult(AnalyzerQuality.High));


                secondMatchCheck = await test.WorkUser(user.SteamId, 5, 5);
            }

            using (var context = new FaceitContext(testOptions))
            {
                Assert.IsTrue(firstMatchCheck);
                Assert.IsTrue(secondMatchCheck);
                Assert.AreEqual(context.Matches.Count(), 1);
                Assert.AreEqual(context.Matches.First().AnalyzedQuality, AnalyzerQuality.High);
            }
        }

        //This is WIP, as it is a cluster fuck to test
        [Ignore]
        [TestMethod]
        public async Task WorkUserSetsLastCheckedAsync()
        {
            var testOptions = FaceitTestHelper.GetDatabaseOptions("test_WorkUserSersLastChecked");
            var testSteamId = 123456789;
            var testFaceitId = "123456789";
            var testMaxMatches = 1;
            var testMaxAgeDays = 1;

            var mockFaceitAPI = new Mock<IFaceitApiCommunicator>();
            var mockLogger = new Mock<ILogger<FaceitMatchesWorker>>();
            var mockRabbitProducer = new Mock<IProducer<DemoInsertInstruction>>();
            var mockUserRetriever = new Mock<IUserIdentityRetriever>();

            mockFaceitAPI.Setup(x => x.GetPlayerMatches(testSteamId, testFaceitId, testMaxMatches, testMaxAgeDays)).Returns(Task.FromResult<IEnumerable<FaceitMatchData>>(new List<FaceitMatchData>()));

            using (var context = new FaceitContext(testOptions))
            {
                context.Users.Add(new User { SteamId = testSteamId, FaceitId = testFaceitId });

                var test = new FaceitMatchesWorker(mockLogger.Object, context, mockFaceitAPI.Object, mockRabbitProducer.Object, mockUserRetriever.Object);
                await test.WorkUser(testSteamId,testMaxMatches,testMaxAgeDays);
            }

            using(var confirmContext = new FaceitContext(testOptions))
            {
                var user = confirmContext.Users.Single(x => x.SteamId == testSteamId);
                Assert.IsTrue(user.LastChecked != default);
                Assert.IsTrue((user.LastChecked - DateTime.UtcNow) < TimeSpan.FromMinutes(5));
            }
        }
    }
}
