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
using RabbitCommunicationLib.Enums;
using RabbitCommunicationLib.Interfaces;
using RabbitCommunicationLib.TransferModels;

namespace FaceitMatchGatherer.Controllers
{
    [Route("trusted/maintenance")]
    [ApiController]
    public class MaintenanceController : ControllerBase
    {
        private readonly ILogger<MaintenanceController> _logger;
        private readonly FaceitContext _context;
        private readonly IProducer<DemoInsertInstruction> _rabbitProducer;
        private readonly IFaceitApiCommunicator _faceitApi;

        public MaintenanceController(
            ILogger<MaintenanceController> logger,
            FaceitContext context,
            IProducer<DemoInsertInstruction> rabbitProducer,
            IFaceitApiCommunicator faceitApi)
        {
            _logger = logger;
            _context = context;
            _rabbitProducer = rabbitProducer;
            _faceitApi = faceitApi;
        }

        /// <summary>
        /// Scans through previousily stored Matches and
        /// sends a flood of DownloadUrls inside DemoInsertIntrusctrions
        /// to DemoCentral for processing.
        /// </summary>
        /// <param name="amount">Amount of Matches to send.</param>
        /// <returns></returns>
        [HttpGet("flood/{amount}")]
        public async Task Flood(int amount)
        {
            List<Match> matches = _context.Matches
                .TakeLast(amount)
                .ToList();

            if (matches.Count < 1)
            {
                _logger.LogError(
                    "No matches found, cannot produce any DemoInsertInstructions");
            }
                
            foreach (var match in matches)
            {
                string downloadUrl = await _faceitApi.GetDemoUrl(match.FaceitMatchId);

                if (string.IsNullOrEmpty(downloadUrl))
                    continue;

                var message = new DemoInsertInstruction
                {
                    DownloadUrl = downloadUrl,
                    MatchDate = DateTime.Now.ToUniversalTime(),
                    UploaderId = 0,
                    Source = Source.Unknown,
                    UploadType = UploadType.FaceitMatchGatherer
                };

                _logger.LogInformation(
                    $"Maintenance: Sending DemoInsertInstruction with DownloadUrl [{message.DownloadUrl} ].");

                _rabbitProducer.PublishMessage(message);
                
            }
        }
    }
}
