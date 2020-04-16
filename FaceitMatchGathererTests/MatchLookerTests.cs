using Database;
using FaceitMatchGatherer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FaceitMatchGathererTests
{
    [TestClass]
    public class MatchLookerTests
    {
        public DbContextOptions<FaceitContext> test_config = new DbContextOptionsBuilder<FaceitContext>().UseInMemoryDatabase(databaseName: "FaceItGatherer_TestDB").Options;
        [TestMethod]
        public async Task AmountOfPeriodicCallsIsAsExpected()
        {
            //Keep this high so that other computational delays to not matter
            var tenMSTimeSpan = TimeSpan.FromMilliseconds(50);
            var mockIlogger = new Mock<ILogger<PeriodicMatchLooker>>();

            var expectedMethodCalls = 4;

            var mockMatchlooker = new Mock<IMatchLooker>();

            var test = new PeriodicMatchLooker(tenMSTimeSpan, mockMatchlooker.Object, mockIlogger.Object);

            //TODO OPTIONAL create a stopwatch and measure time
            await test.StartAsync(new CancellationToken());
            //method is called once at the start and then directly every interval after, so it's called 1 more time than expected
            await Task.Delay((expectedMethodCalls - 1) * (int) tenMSTimeSpan.TotalMilliseconds);
            await test.StopAsync(new CancellationToken());

            mockMatchlooker.Verify(x => x.RefreshActiveUsersAsync(), Times.Exactly(expectedMethodCalls));
        }


        [TestMethod]
        public async Task CorrectUserIsFound()
        {
            var mockILogger = new Mock<ILogger<MatchLooker>>();
            var mockFaceitWorker = new Mock<IFaceitMatchesWorker>();
            int maxUsers = 1;
            TimeSpan fiveMin = TimeSpan.FromMinutes(5);
            int testSteamId = 1234;
            var testUser = new Entities.Models.User { SteamId = testSteamId, LastActivity = DateTime.UtcNow };

            using (var context = new FaceitContext(test_config))
            {
                context.Users.Add(testUser);
                context.SaveChanges();

                var test = new MatchLooker(fiveMin, maxUsers, mockILogger.Object, context, mockFaceitWorker.Object);
                await test.RefreshActiveUsersAsync();
            }

            mockFaceitWorker.Verify(x => x.WorkUser(testUser.SteamId, It.IsAny<int>(), It.IsAny<int>()));
        }


        [TestCleanup]
        public void Cleanup()
        {
            using (var context = new FaceitContext(test_config))
            {
                context.Database.EnsureDeleted();
            }
        }
    }
}
