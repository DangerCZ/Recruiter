using Newtonsoft.Json;

namespace Recruiter
{
    public class Config
    {
        [JsonProperty("isEnabledForGarrisonInTowns")]
        public bool IsEnabledForGarrisonInTowns { get; set; }
        
        [JsonProperty("isEnabledForGarrisonInCastles")]
        public bool IsEnabledForGarrisonInCastles { get; set; }
        
        [JsonProperty("recruiterCost")]
        public int RecruiterCost { get; set; }
        
        [JsonProperty("recruitsPerDay")]
        public int RecruitsPerDay { get; set; }
        
        [JsonProperty("eliteRecruiterCost")]
        public int EliteRecruiterCost { get; set; }
        
        [JsonProperty("eliteRecruitsPerDay")]
        public int EliteRecruitsPerDay { get; set; }
        
        [JsonProperty("masterRecruiterCost")]
        public int MasterRecruiterCost { get; set; }
        
        [JsonProperty("masterRecruitsPerDay")]
        public int MasterRecruitsPerDay { get; set; }
        
        [JsonProperty("grandmasterRecruiterCost")]
        public int GrandmasterRecruiterCost { get; set; }
        
        [JsonProperty("grandmasterRecruitsPerDay")]
        public int GrandmasterRecruitsPerDay { get; set; }
    }
}