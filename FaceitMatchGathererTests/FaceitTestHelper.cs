using Database;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FaceitMatchGathererTests
{
    public static class FaceitTestHelper
    {

        public static DbContextOptions<FaceitContext> GetDatabaseOptions(string databaseName)
        {
            return new DbContextOptionsBuilder<FaceitContext>()
                .UseInMemoryDatabase(databaseName: databaseName)
                .Options;
        }

        public static User GetRandomUser()
        {
            return new User
            {
                SteamId = (long)new Random().Next(1, 99999999),
                FaceitName = "FaceitName",
                FaceitId = "FaceitId",
                FaceitMembership = Entities.Enumerals.FaceitMembership.Free,
                RefreshToken = "RefreshToken",
                Token = "Token",
                LastChecked = DateTime.Now,
                TokenExpires = DateTime.Now,
            };
        }
    }
}
