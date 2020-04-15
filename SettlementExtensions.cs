using TaleWorlds.CampaignSystem;

namespace Recruiter
{
    internal static class SettlementExtensions
    {
        public static SettlementComponent GetSettlementComponent(this Settlement settlement)
        {
            return settlement.GetComponent<SettlementComponent>();
        }
    }
}
