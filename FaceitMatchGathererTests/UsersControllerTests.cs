using Database;
using FaceitMatchGatherer;
using FaceitMatchGatherer.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Linq;
using System;
using Moq;
using System.ComponentModel;
using Entities.Models;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FaceitMatchGathererTests
{
    [TestClass]
    public class UsersControllerTests
    {
        private readonly ServiceProvider serviceProvider;

        public UsersControllerTests()
        {
            var services = new ServiceCollection();
            services.AddLogging(x => x.AddConsole().AddDebug());

            serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Tests POST: api/Users/<steamId> with (mocked) data of a valid user and verifies that 
        /// HTTP 200 is returned and the user was stored in the database.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task CreateUserTest()
        {
            var options = FaceitTestHelper.GetDatabaseOptions("NonMatchingSteamAndFaceitAccounts");
            var user = FaceitTestHelper.GetRandomUser();

            // Create User by calling PostUser
            using (var context = new FaceitContext(options))
            {
                // Setup mocks
                var mockFaceitMatchesWorker = new Mock<IFaceitMatchesWorker>();
                // Setup mockFaceitOAuthCommunicator to return user
                var mockFaceitOAuthCommunicator = new Mock<IFaceitOAuthCommunicator>();
                mockFaceitOAuthCommunicator.Setup(x => x.CreateUser(user.SteamId, It.IsAny<string>())).Returns(Task.FromResult<User>(user));
                // Setup mockFaceitApiCommunicator, making sure it returns the same SteamId for this user
                var mockFaceitApiCommunicator = new Mock<IFaceitApiCommunicator>();
                mockFaceitApiCommunicator.Setup(x => x.GetSteamId(It.Is<string>(x => x == user.FaceitName))).Returns(Task.FromResult<long>(user.SteamId));

                // Create UsersController and call PostUser
                var controller = new UsersController(serviceProvider.GetService<ILogger<UsersController>>(), context, mockFaceitOAuthCommunicator.Object, mockFaceitApiCommunicator.Object, mockFaceitMatchesWorker.Object);
                var result = await controller.PostUser(user.SteamId, "");

                // Verify 200 response
                Assert.IsInstanceOfType(result, typeof(Microsoft.AspNetCore.Mvc.OkResult));
            }

            // Verify that a single user with the given steamid was saved to database
            using (var context = new FaceitContext(options))
            {
                var steamIdFromDb = context.Users.Single().SteamId;
                Assert.IsTrue(steamIdFromDb == user.SteamId);
            }
        }

        /// <summary>
        /// Tests POST: api/Users/<steamId> with (mocked) updated data of an existing user and verifies that 
        /// HTTP 200 is returned and the user was updated in the database.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task UpdateUserTest()
        {
            var options = FaceitTestHelper.GetDatabaseOptions("UpdateUserTest");
            var user = FaceitTestHelper.GetRandomUser();

            // Create User
            using (var context = new FaceitContext(options))
            {
                context.Users.Add(user);
                await context.SaveChangesAsync();
            }

            // Define updatedUser with updated properties
            var updatedUser = new User
            {
                SteamId = user.SteamId,
                FaceitId = user.FaceitId,
                FaceitName = user.FaceitName,
                LastChecked = user.LastChecked,
                RefreshToken = "UpdatedRefreshToken",
                Token = "UpdatedToken",
                FaceitMembership = Entities.Enumerals.FaceitMembership.Unlimited,
                TokenExpires = DateTime.Now,
            };

            // Create the user mocking updated data by faceit 
            using (var context = new FaceitContext(options))
            {
                // Setup mocks
                var mockFaceitMatchesWorker = new Mock<IFaceitMatchesWorker>();
                // Setup mockFaceitOAuthCommunicator to return updatedUser
                var mockFaceitOAuthCommunicator = new Mock<IFaceitOAuthCommunicator>();
                mockFaceitOAuthCommunicator.Setup(x => x.CreateUser(user.SteamId, It.IsAny<string>())).Returns(Task.FromResult<User>(updatedUser));
                // Setup mockFaceitApiCommunicator, making sure it returns the same SteamId for this user
                var mockFaceitApiCommunicator = new Mock<IFaceitApiCommunicator>();
                mockFaceitApiCommunicator.Setup(x => x.GetSteamId(It.Is<string>(x => x == user.FaceitName))).Returns(Task.FromResult<long>(user.SteamId));

                // Create UsersController and call PostUser
                var controller = new UsersController(serviceProvider.GetService<ILogger<UsersController>>(), context, mockFaceitOAuthCommunicator.Object, mockFaceitApiCommunicator.Object, mockFaceitMatchesWorker.Object);
                var result = await controller.PostUser(user.SteamId, "");

                // Verify 200 response
                Assert.IsInstanceOfType(result, typeof(Microsoft.AspNetCore.Mvc.OkResult));
            }

            // Use a separate instance of the context to verify that a single user with the updated properties was saved to database
            using (var context = new FaceitContext(options))
            {
                var userFromDb = context.Users.Single();

                // Test equality by serializing
                var userFromDbJson = JsonConvert.SerializeObject(userFromDb);
                var updatedUserJson = JsonConvert.SerializeObject(updatedUser);

                Assert.AreEqual(userFromDbJson, updatedUserJson);
            }
        }

        /// <summary>
        /// Tests POST: api/Users/<steamId> with (mocked) data of an invalid user who tries to link a faceit account that 
        /// isn't his and verifies that HTTP 400 is returned and that no user was not stored in database.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task NonMatchingSteamAndFaceitAccounts()
        {
            var options = FaceitTestHelper.GetDatabaseOptions("NonMatchingSteamAndFaceitAccounts");
            var user = FaceitTestHelper.GetRandomUser();

            // Create User with Mismatching SteamId and FaceitSteamId and expect BadRequest
            using (var context = new FaceitContext(options))
            {
                // Setup mocks
                var mockFaceitMatchesWorker = new Mock<IFaceitMatchesWorker>();
                // Setup mockFaceitOAuthCommunicator to return user
                var mockFaceitOAuthCommunicator = new Mock<IFaceitOAuthCommunicator>();
                mockFaceitOAuthCommunicator.Setup(x => x.CreateUser(user.SteamId, It.IsAny<string>())).Returns(Task.FromResult<User>(user));
                // Setup mockFaceitApiCommunicator, making sure it returns a different SteamId
                var mockFaceitApiCommunicator = new Mock<IFaceitApiCommunicator>();
                mockFaceitApiCommunicator.Setup(x => x.GetSteamId(It.Is<string>(x => x == user.FaceitName))).Returns(Task.FromResult<long>(user.SteamId + 1));


                // Create UsersController and call PostUser
                var controller = new UsersController(serviceProvider.GetService<ILogger<UsersController>>(), context, mockFaceitOAuthCommunicator.Object, mockFaceitApiCommunicator.Object, mockFaceitMatchesWorker.Object);
                var result = await controller.PostUser(user.SteamId, "");

                // Verify 400 response
                Assert.IsInstanceOfType(result, typeof(Microsoft.AspNetCore.Mvc.BadRequestResult));
            }

            //Verify that no user is in database
            using (var context = new FaceitContext(options))
            {
                var userInDb = context.Users.Any();
                Assert.IsFalse(userInDb);
            }
        }

        /// <summary>
        /// Tests POST: api/Users/<steamId> with invalid data and verifies that HTTP 400 is returned and that no user was not stored in database
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task FailedUserCreationTest()
        {
            var options = FaceitTestHelper.GetDatabaseOptions("FailedUserCreationTest");
            var steamId = (long)new Random().Next(1, 99999999);

            // Call with (mocked) invalid code and expect BadRequest
            using (var context = new FaceitContext(options))
            {
                // Setup mocks
                var mockFaceitApiCommunicator = new Mock<IFaceitApiCommunicator>();
                var mockFaceitMatchesWorker = new Mock<IFaceitMatchesWorker>();
                // Setup mockFaceitOAuthCommunicator and make its CreateUser() method throw a FaceitFailedUserCreationException
                var mockFaceitOAuthCommunicator = new Mock<IFaceitOAuthCommunicator>();
                mockFaceitOAuthCommunicator
                    .Setup(x => x.CreateUser(It.Is<long>(x=> x == steamId), It.IsAny<string>()))
                    .Throws(new FaceitOAuthCommunicator.FaceitFailedUserCreationException(""));

                // Create UsersController and call PostUser
                var controller = new UsersController(serviceProvider.GetService<ILogger<UsersController>>(), context, mockFaceitOAuthCommunicator.Object, mockFaceitApiCommunicator.Object, mockFaceitMatchesWorker.Object);
                var result = await controller.PostUser(steamId, "");

                // Verify 400 response
                Assert.IsInstanceOfType(result, typeof(Microsoft.AspNetCore.Mvc.BadRequestResult));
            }

            //Verify that no user is in database
            using (var context = new FaceitContext(options))
            {
                var userInDb = context.Users.Any();
                Assert.IsFalse(userInDb);
            }
        }


        /// <summary>
        /// Tests DELETE: api/Users/<steamId> with valid user and verifies that HTTP 200 is returned and that the user was deleted from database
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task DeleteUserTest()
        {
            var options = FaceitTestHelper.GetDatabaseOptions("DeleteUserTest");
            var user = new User { SteamId = (long)new Random().Next(1, 99999999), FaceitName = "MyFaceitName" };

            // Create User
            using (var context = new FaceitContext(options))
            {
                context.Users.Add(user);
                await context.SaveChangesAsync();
            }

            // Call Delete User
            using (var context = new FaceitContext(options))
            {
                // Setup mocks
                var mockFaceitOAuthCommunicator = new Mock<IFaceitOAuthCommunicator>();
                var mockFaceitApiCommunicator = new Mock<IFaceitApiCommunicator>();
                var mockFaceitMatchesWorker = new Mock<IFaceitMatchesWorker>();

                // Create UsersController and call DeleteUser
                var controller = new UsersController(serviceProvider.GetService<ILogger<UsersController>>(), context, mockFaceitOAuthCommunicator.Object, mockFaceitApiCommunicator.Object, mockFaceitMatchesWorker.Object);
                var result = await controller.DeleteUser(user.SteamId);

                // Verify 200 response
                Assert.IsInstanceOfType(result, typeof(Microsoft.AspNetCore.Mvc.OkResult));
            }

            //Verify that no user is in database
            using (var context = new FaceitContext(options))
            {
                var userInDb = context.Users.Any();
                Assert.IsFalse(userInDb);
            }
        }

        /// <summary>
        /// Tests GET: api/Users/<steamId>/LookForMatches with a valid user and verifies that WorkUser() was called on him.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task LookForMatchesTest()
        {
            var options = FaceitTestHelper.GetDatabaseOptions("LookForMatchesTest");
            var user = new User { SteamId = (long)new Random().Next(1, 99999999), FaceitName = "MyFaceitName" };

            // Create User
            using (var context = new FaceitContext(options))
            {
                context.Users.Add(user);
                await context.SaveChangesAsync();
            }


            // Call LookForMatches on user and verify that WorkUser was called
            using (var context = new FaceitContext(options))
            {
                // Setup mocks
                var mockFaceitOAuthCommunicator = new Mock<IFaceitOAuthCommunicator>();
                var mockFaceitApiCommunicator = new Mock<IFaceitApiCommunicator>();
                var mockFaceitMatchesWorker = new Mock<IFaceitMatchesWorker>();

                // Create UsersController and call LookForMatches
                var usersController = new UsersController(serviceProvider.GetService<ILogger<UsersController>>(), context, mockFaceitOAuthCommunicator.Object, mockFaceitApiCommunicator.Object, mockFaceitMatchesWorker.Object);
                var lfmResponse = await usersController.PostLookForMatches(user.SteamId);

                // Verify that WorkUser was called
                mockFaceitMatchesWorker.Verify(x => x.WorkUser(user.SteamId, It.IsAny<int>(), It.IsAny<int>()), Times.Once);
            }
        }
    }
}
