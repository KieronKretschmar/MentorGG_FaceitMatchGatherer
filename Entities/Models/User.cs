using System;
using System.Collections.Generic;
using System.Text;
using static Entities.Enumerals;

namespace Entities.Models
{
    public class User
    {
        public long SteamId { get; set; }
        public string FaceitId { get; set; }
        public string FaceitName { get; set; }
        public FaceitMembership FaceitMembership { get; set; }
        public DateTime LastChecked { get; set; }
        public DateTime LastActivity { get; set; }
        public string Token { get; set; }
        public DateTime TokenExpires { get; set; }
        public string RefreshToken { get; set; }
    }
}
