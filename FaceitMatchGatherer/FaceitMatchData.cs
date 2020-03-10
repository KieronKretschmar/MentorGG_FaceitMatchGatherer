using Newtonsoft.Json.Linq;
using RabbitCommunicationLib.Enums;
using RabbitCommunicationLib.TransferModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FaceitMatchGatherer
{
    public class FaceitMatchData
    {
        public string FaceitMatchId { get; set; }
        public string DownloadUrl { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
        public long UploaderId { get; set; }

        private readonly DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        public FaceitMatchData()
        {

        }

        public FaceitMatchData(long uploaderId, JToken data)
        {
            UploaderId = uploaderId;
            FaceitMatchId = data["match_id"].ToString();

            // Try converting faceit API Time format to DateTime, 
            var startedDateString = data["started_at"].ToString();
            var finishedDateString = data["finished_at"].ToString();
            StartedAt = origin.AddSeconds(long.Parse(startedDateString));
            FinishedAt = origin.AddSeconds(long.Parse(finishedDateString));
        }

        public DemoInsertInstruction ToTransferModel()
        {
            var model = new DemoInsertInstruction
            {
                DownloadUrl = DownloadUrl,
                MatchDate = StartedAt,
                UploaderId = UploaderId, 
                Source = Source.Faceit,
                UploadType = UploadType.FaceitMatchGatherer,
            };

            return model;
        }
    }
}
