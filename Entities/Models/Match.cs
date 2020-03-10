using System;
using RabbitCommunicationLib.Enums;

namespace Entities.Models
{
    public class Match
    {
        public string FaceitMatchId { get; set; }
        public AnalyzerQuality AnalyzedQuality { get; set; }
    }
}
