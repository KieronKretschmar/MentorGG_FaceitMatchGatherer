﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Database;
using Entities.Models;
using System.IdentityModel.Tokens.Jwt;
using static Entities.Enumerals;
using Microsoft.Extensions.Logging;
using RabbitCommunicationLib.Enums;

namespace FaceitMatchGatherer.Controllers
{
    [Route("users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ILogger<UsersController> _logger;
        private readonly FaceitContext _context;
        private readonly IFaceitOAuthCommunicator _faceitOAuthCommunicator;
        private readonly IFaceitApiCommunicator _faceitApiCommunicator;
        private readonly IFaceitMatchesWorker _faceitMatchesWorker;

        public UsersController(ILogger<UsersController> logger, FaceitContext context, IFaceitOAuthCommunicator faceitOAuthCommunicator, IFaceitApiCommunicator faceitApiCommunicator, IFaceitMatchesWorker faceitMatchesWorker)
        {
            _logger = logger;
            _context = context;
            _faceitOAuthCommunicator = faceitOAuthCommunicator;
            _faceitApiCommunicator = faceitApiCommunicator;
            _faceitMatchesWorker = faceitMatchesWorker;
        }

        /// <summary>
        /// Gets the database entry of the Faceit user with the given steamId.
        /// </summary>
        /// <param name="steamId"></param>
        /// <returns></returns>
        [HttpGet("{steamId}")]
        public async Task<ActionResult<User>> GetUser(long steamId)
        {
            var user = await _context.Users.FindAsync(steamId);

            if (user == null)
            {
                return NotFound();
            }

            return user;
        }

        /// <summary>
        /// Adds user to database and thereby enables faceit automatic-upload for him.
        /// </summary>
        /// <param name="steamId"></param>
        /// <param name="code">Authorization code provided by Faceit OAuth implementation.</param>
        /// <returns></returns>
        [HttpPost("{steamId}")]
        public async Task<ActionResult> CreateUser(long steamId, string code)
        {
            _logger.LogInformation($"CreateUser called with steamId [ {steamId} ] and code [ {code} ]");

            // Query faceit for more data about user
            User user;
            try
            {
                user = await _faceitOAuthCommunicator.CreateUser(steamId, code);
            }
            catch (FaceitOAuthCommunicator.FaceitFailedUserCreationException)
            {
                return BadRequest();
                throw;
            }

            // Check if the faceit account belongs to this steamid
            var faceitSteamId = await _faceitApiCommunicator.GetSteamId(user.FaceitName);
            if (faceitSteamId != user.SteamId)
            {
                var msg = $"Faceit and Steam Account don't match. SteamId of the logged in user: [ {user.SteamId} ]. SteamId of the Faceit account: [ {faceitSteamId} ]";
                _logger.LogWarning(msg);
                return BadRequest(msg);
            }

            // Modify user if he already exists, Add if he is new
            if (_context.Users.Any(x => x.SteamId == steamId))
            {
                _logger.LogInformation($"User with steamId [ {steamId} ] already exists. Updating entry.");
                _context.Entry(user).State = EntityState.Modified;
            }
            else
            {
                _logger.LogInformation($"Created new User with steamId [ {steamId} ].");
                _context.Entry(user).State = EntityState.Added;
            }

            // Update UserDb
            await _context.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Removes User from database and thereby disables faceit automatic upload.
        /// </summary>
        /// <param name="steamId"></param>
        /// <returns></returns>
        [HttpDelete("{steamId}")]
        public async Task<ActionResult> DeleteUser(long steamId)
        {
            _logger.LogInformation($"DeleteUser called with steamId [ {steamId} ]");
            var user = await _context.Users.FindAsync(steamId);
            if (user == null)
            {
                _logger.LogInformation($"User not found for deletion with steamId [ {steamId} ]");
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Removed user with steamId [ {steamId} ]");

            return Ok();
        }

        /// <summary>
        /// Triggers calls to the Faceit API to find new matches of the specified user, and initiates the process of analyzing them.
        /// </summary>
        /// <param name="steamId"></param>
        /// <returns></returns>
        [HttpPost("{steamId}/look-for-matches")]
        public async Task<ActionResult<bool>> LookForMatches(long steamId)
        {
            _logger.LogInformation($"LookForMatches called with steamId [ {steamId} ]");

            var user =  _context.Users.Find(steamId);

            // Workaround while filling up database pre-release of kubernetes backend
            // Can be removed after launch
            if (user == null)
            {
                _logger.LogWarning($"User with SteamId [ {steamId} ] not found in database. Ignoring the request under the assumption that this is because of the pre-release db fill.");
                return false;
            }

            user.LastActivity = DateTime.UtcNow;
            _context.SaveChanges();

            return await _faceitMatchesWorker.WorkUser(steamId, 20, 60);
        }

        private bool UserExists(string id)
        {
            return _context.Users.Any(e => e.FaceitId == id);
        }
    }
}
