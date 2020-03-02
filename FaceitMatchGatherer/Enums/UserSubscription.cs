using RabbitCommunicationLib.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FaceitMatchGatherer.Enums
{
    //Keep in sync with DemoCentrals Usersubscription enum
    public enum UserSubscription
    {
        

        Free = 1,
        Premium = 2,
        Ultimate = 3,
    }
    //TODO Keep in sync with DemoCentrals Usersubscription 
    public static class QualityPerSubscription
    {
        public static Dictionary<UserSubscription, AnalyzerQuality> Qualities =
            new Dictionary<UserSubscription, AnalyzerQuality> {
                {UserSubscription.Free,AnalyzerQuality.Low},
                {UserSubscription.Premium,AnalyzerQuality.Medium},
                {UserSubscription.Ultimate,AnalyzerQuality.High},
            };
    }
}
