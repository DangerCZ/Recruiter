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
    }
}
