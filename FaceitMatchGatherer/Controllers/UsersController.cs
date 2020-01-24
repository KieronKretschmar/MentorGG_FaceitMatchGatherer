using System;
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
        ///
        /// GET: users/<steamId>
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
        /// Endpoint for creating a new Faceit user. Has to be used as callback for Faceit's OAuth JS library.
        ///
        /// POST: users/<steamId>
        /// </summary>
        /// <param name="steamId"></param>
        /// <param name="code">Authorization code provided by Faceit OAuth implementation.</param>
        /// <returns></returns>
        [HttpPost("{steamId}")]
        public async Task<ActionResult> CreateUser(long steamId, string code)
        {
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
                return BadRequest();
            }

            // Modify user if he already exists, Add if he is new
            if (_context.Users.Any(x => x.SteamId == steamId))
            {
                _context.Entry(user).State = EntityState.Modified;
            }
            else
            {
                _context.Entry(user).State = EntityState.Added;
            }

            // Update UserDb
            await _context.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Removes User from database.
        ///
        /// DELETE: /users/<steamId>
        /// </summary>
        /// <param name="steamId"></param>
        /// <returns></returns>
        [HttpDelete("{steamId}")]
        public async Task<ActionResult> DeleteUser(long steamId)
        {
            var user = await _context.Users.FindAsync(steamId);
            if (user == null)
            {

                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok();
        }

        /// <summary>
        /// Triggers calls to the Faceit API to find new matches of the specified user, and initiates the process of analyzing them.
        ///
        /// POST /users/<steamId>/look-for-matches
        /// </summary>
        /// <param name="steamId"></param>
        /// <returns></returns>
        [HttpPost("{steamId}/look-for-matches")]
        public async Task<ActionResult<bool>> LookForMatches(long steamId)
        {
            return await _faceitMatchesWorker.WorkUser(steamId, 20, 60);
        }

        private bool UserExists(string id)
        {
            return _context.Users.Any(e => e.FaceitId == id);
        }
    }
}
