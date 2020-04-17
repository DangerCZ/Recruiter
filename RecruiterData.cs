using TaleWorlds.SaveSystem;

namespace Recruiter
{
    [SaveableClass(42069247)]
    public class RecruiterData
    {
        [SaveableProperty(1)]
        public string SettlementId { get; set; }

        [SaveableProperty(2)]
        public bool HasRecruiter { get; set; }
        
        [SaveableProperty(3)]
        public int RecruiterLevel { get; set; }
        
        [SaveableProperty(4)]
        public bool IsRecruiterEnabled { get; set; }
    }
}
