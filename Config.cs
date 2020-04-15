using Newtonsoft.Json;

namespace Recruiter
{
    public class Config
    {
        [JsonProperty("recruiterCost")]
        public int RecruiterCost { get; set; }
    }
}
