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
        [TestMethod]
        public async Task WorkUserTest()
        {
            var options = FaceitTestHelper.GetDatabaseOptions("WorkUserTest");

            var steamId = 1;
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
                .Setup(x => x.GetPlayerMatches(It.Is<long>(x => x == steamId), It.Is<int>(x => x == maxMatches), It.Is<int>(x => x == maxAgeInDays)))
                .Returns(Task.FromResult(matches));

            // Call WorkUser and expect it to publish matches to queue and database
            using (var context = new FaceitContext(options))
            {
                var faceitMatchesWorker = new FaceitMatchesWorker(serviceProvider.GetService<ILogger<FaceitMatchesWorker>>(), context, mockFaceitApiCommunicator.Object, mockRabbitProducer.Object);
                var foundMatches = await faceitMatchesWorker.WorkUser(steamId, maxMatches, maxAgeInDays, AnalyzerQuality.High);

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
                var faceitMatchesWorker = new FaceitMatchesWorker(serviceProvider.GetService<ILogger<FaceitMatchesWorker>>(), context, mockFaceitApiCommunicator.Object, mockRabbitProducer.Object);
                var foundMatches = await faceitMatchesWorker.WorkUser(steamId, 20, 60, AnalyzerQuality.High);
                Assert.IsFalse(foundMatches);

                // Verify that no more messages were published
                mockRabbitProducer.Verify(x => x.PublishMessage( It.IsAny<DemoInsertInstruction>(),It.IsAny<string>()), Times.Exactly(matches.Count()));
            }
        }


        /// <summary>
        /// Tests that a match already in the database is not reported back if the new gather-request comes in with a lower quality
        /// </summary>
        [TestMethod]
        public async Task DoNotReturnMatchWithHigherQuality()
        {
            var testOptions = FaceitTestHelper.GetDatabaseOptions("test_DoNotReturnMatchWithHigherQuality");
            bool firstMatchCheck = false;
            bool secondMatchCheck = false;

            using (var context = new FaceitContext(testOptions))
            {
                var testSteamId = 123456789;

                var testFaceitMatch = new FaceitMatchData
                {
                    FaceitMatchId = "testFaceitID",                
                };
                var mockFaceitAPI = new Mock<IFaceitApiCommunicator>();
                mockFaceitAPI.Setup(x => x.GetPlayerMatches(testSteamId, It.IsAny<int>(), It.IsAny<int>())).Returns(Task.FromResult<IEnumerable<FaceitMatchData>>(new List<FaceitMatchData> { testFaceitMatch }));
                mockFaceitAPI.Setup(x => x.GetDemoUrl(It.IsAny<string>())).Returns(Task.FromResult("testDownloadUrl"));

                var mockRabbit = new Mock<IProducer<DemoInsertInstruction>>();

                var test = new FaceitMatchesWorker(serviceProvider.GetRequiredService<ILogger<FaceitMatchesWorker>>(), context, mockFaceitAPI.Object, mockRabbit.Object);

                firstMatchCheck = await test.WorkUser(testSteamId, 5, 5, AnalyzerQuality.High);

                secondMatchCheck = await test.WorkUser(testSteamId, 5, 5, AnalyzerQuality.Low);
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
        /// Tests that a match already in the database is  reported back if the new gather-request comes in with a higher quality
        /// </summary>
        [TestMethod]
        public async Task DoReturnMatchWithLowerQuality()
        {
            var testOptions = FaceitTestHelper.GetDatabaseOptions("test_DoReturnMatchWithLowerQuality");
            bool firstMatchCheck = false;
            bool secondMatchCheck = false;

            using (var context = new FaceitContext(testOptions))
            {
                var testSteamId = 123456789;

                var testFaceitMatch = new FaceitMatchData
                {
                    FaceitMatchId = "testFaceitID",
                };
                var mockFaceitAPI = new Mock<IFaceitApiCommunicator>();
                mockFaceitAPI.Setup(x => x.GetPlayerMatches(testSteamId, It.IsAny<int>(), It.IsAny<int>())).Returns(Task.FromResult<IEnumerable<FaceitMatchData>>(new List<FaceitMatchData> { testFaceitMatch }));
                mockFaceitAPI.Setup(x => x.GetDemoUrl(It.IsAny<string>())).Returns(Task.FromResult("testDownloadUrl"));

                var mockRabbit = new Mock<IProducer<DemoInsertInstruction>>();

                var test = new FaceitMatchesWorker(serviceProvider.GetRequiredService<ILogger<FaceitMatchesWorker>>(), context, mockFaceitAPI.Object, mockRabbit.Object);

                firstMatchCheck = await test.WorkUser(testSteamId, 5, 5, AnalyzerQuality.Low);

                secondMatchCheck = await test.WorkUser(testSteamId, 5, 5, AnalyzerQuality.High);
            }

            using (var context = new FaceitContext(testOptions))
            {
                Assert.IsTrue(firstMatchCheck);
                Assert.IsTrue(secondMatchCheck);
                Assert.AreEqual(context.Matches.Count(), 1);
                Assert.AreEqual(context.Matches.First().AnalyzedQuality, AnalyzerQuality.High);
            }
        }
    }
}
