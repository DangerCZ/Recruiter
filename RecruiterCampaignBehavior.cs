using System;
using System.Collections.Generic;
using System.Linq;
using NetworkMessages.FromServer;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Overlay;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Recruiter
{
    public class RecruiterCampaignBehavior : CampaignBehaviorBase
    {
        private Dictionary<string, RecruiterData> _settlementRecruiterDataBySettlementId = new Dictionary<string, RecruiterData>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData(nameof(_settlementRecruiterDataBySettlementId), ref _settlementRecruiterDataBySettlementId);
            }
            catch
            {
            }
        }

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            AddMenus(campaignGameStarter);
        }

        private void OnDailyTick()
        {
            UpdateRecruiter();
        }

        private void UpdateRecruiter()
        {
            
            var message = "Recruiter Garrison Report: ";
            var shouldAddSeparator = false;
            
            foreach (var settlementId in _settlementRecruiterDataBySettlementId.Keys)
            {
                var settlement = Settlement.Find(settlementId);
                var recruiterData = GetRecruiterDataAtSettlement(settlement);

                if(recruiterData.HasRecruiter)
                {
                    // get number of troops to be recruited
                    var count = GetNumberOfDailyRecruitsAtSettlement(settlement);
                    var currentGarrisonCount = settlement.Town.GarrisonParty?.Party.NumberOfAllMembers ?? 0;
                    var maxGarrisonCount = settlement.Town.GarrisonParty?.Party.PartySizeLimit ?? 0;
                    
                    // make sure we don't recruit over the limit
                    if (currentGarrisonCount + count >= maxGarrisonCount)
                    {
                        count = maxGarrisonCount - currentGarrisonCount;
                    }

                    // add information to log message (only if there is a change)
                    if (shouldAddSeparator) message += ", ";
                    
                    if (count > 0)
                    {
                        message += $"{settlement.Name} ({currentGarrisonCount+count}/{maxGarrisonCount})";
                    }
                    else
                    {
                        message += $"{settlement.Name} ({currentGarrisonCount+count})";
                    }

                    shouldAddSeparator = true;

                    // create roster of troops
                    var party = new FlattenedTroopRoster();
                    for (int i = 0; i < count; i++)
                    {
                        party.Add(settlement.Culture.BasicTroop);
                    }

                    // if there is no party, make one
                    if (settlement.Town.GarrisonParty == null)
                    {
                        settlement.AddGarrisonParty();
                    }
                    
                    // add roster to garrison
                    settlement.Town.GarrisonParty.Party.AddMembers(party);
                }
            }

            // log message (only if there are any settlements)
            if (_settlementRecruiterDataBySettlementId.Count > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(message));
            }

        }

        private void AddMenus(CampaignGameStarter campaignGameStarter)
        {
            // Town Menu
            campaignGameStarter.AddGameMenuOption(
                "town",
                "town_go_to_recruiter",
                "{=town_recruiter}Go to the recruiter",
                args => 
                {
                    var (canPlayerAccessRecruiter, reasonMessage) = CanPlayerAccessRecruiterAtSettlement(Settlement.CurrentSettlement);
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    args.IsEnabled = canPlayerAccessRecruiter;
                    args.Tooltip = canPlayerAccessRecruiter ? TextObject.Empty : new TextObject(reasonMessage);
                    return true;
                },
                args => GameMenu.SwitchToMenu("recruiter"),
                false,
                1
            );
            
            // Castle Menu
            campaignGameStarter.AddGameMenuOption(
                "castle",
                "castle_go_to_recruiter",
                "{=castle_recruiter}Go to the recruiter",
                args => 
                {
                    var (canPlayerAccessRecruiter, reasonMessage) = CanPlayerAccessRecruiterAtSettlement(Settlement.CurrentSettlement);
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    args.IsEnabled = canPlayerAccessRecruiter;
                    args.Tooltip = canPlayerAccessRecruiter ? TextObject.Empty : new TextObject(reasonMessage);
                    return true;
                },
                args => GameMenu.SwitchToMenu("recruiter"),
                false,
                1
            );

            // Recruiter
            campaignGameStarter.AddGameMenu(
                "recruiter",
                "{=recruiter_info}{DANGER_RECRUITER_INFO}",
                args => UpdateRecruiterMenuTextVariables(),
                GameOverlays.MenuOverlayType.SettlementWithBoth
            );
            campaignGameStarter.AddGameMenuOption(
                "recruiter",
                "recruiter_hire",
                "{=recruiter_hire}Hire Recruiter ({DANGER_RECRUITER_COST}{GOLD_ICON})",
                args =>
                {
                    if (DoesPlayerHaveRecruiterAtSettlement(Settlement.CurrentSettlement))
                    {
                        return false;
                    }
                    var canPlayerAffordToHireRecruiterAtSettlement = CanPlayerAffordToHireRecruiterAtSettlement(Settlement.CurrentSettlement);
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    args.IsEnabled = canPlayerAffordToHireRecruiterAtSettlement;
                    args.Tooltip = canPlayerAffordToHireRecruiterAtSettlement
                        ? TextObject.Empty
                        : new TextObject("You cannot afford to hire a recruiter here.");
                    return true;
                },
                args => OnOpenBankAccountAtSettlement(Settlement.CurrentSettlement)
            );
            campaignGameStarter.AddGameMenuOption(
                "recruiter",
                "recruiter_leave",
                "{=recruiter_leave}Leave recruiter",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                args => GameMenu.SwitchToMenu("town")
            );
        }

        private bool DoesPlayerHaveRecruiterAtSettlement(Settlement settlement)
        {
            var recruiterData = GetRecruiterDataAtSettlement(settlement);
            return recruiterData.HasRecruiter;
        }

        private (bool CanAccess, string ReasonMessage) CanPlayerAccessRecruiterAtSettlement(Settlement settlement)
        {
            if (!CampaignTime.Now.IsDayTime)
            {
                return (false, "The recruiter is not available. Try it in the morning.");
            }

            if (settlement.OwnerClan != Clan.PlayerClan)
            {
                return (false, "This is not your settlement. You cannot access recruiter.");
            }

            return (true, string.Empty);
        }

        private bool CanPlayerAffordToHireRecruiterAtSettlement(Settlement settlement)
        {
            return GetPlayerMoneyOnPerson() >= GetRecruiterCost();
        }

        private void OnOpenBankAccountAtSettlement(Settlement settlement)
        {
            if (TryToHireRecruiterAtSettlement(settlement))
            {
                GameMenu.SwitchToMenu("recruiter");
            }
        }

        private void UpdateRecruiterMenuTextVariables()
        {
            MBTextManager.SetTextVariable("DANGER_RECRUITER_SETTLEMENT_NAME", Settlement.CurrentSettlement.Name);
            MBTextManager.SetTextVariable("DANGER_RECRUITER_INFO", BuildRecruiterInfoText(Settlement.CurrentSettlement));
            MBTextManager.SetTextVariable("DANGER_RECRUITER_COST", GetRecruiterCost());
        }

        private string BuildRecruiterInfoText(Settlement settlement)
        {
            var recruiterData = GetRecruiterDataAtSettlement(settlement);
            return recruiterData.HasRecruiter
                ? "The recruiter is active, 5 new recruits will join your garrison every day."
                : "There is no active recruiter yet. Hire one to get 5 new recruits in your garrison every day.";
        }

        private int GetRecruiterCost()
        {
            return RecruiterSubModule.Config.RecruiterCost;
        }

        private int GetNumberOfDailyRecruitsAtSettlement(Settlement settlement)
        {
            return RecruiterSubModule.Config.RecruitsPerDay;
        }

        private RecruiterData GetRecruiterDataAtSettlement(Settlement settlement)
        {
            if (_settlementRecruiterDataBySettlementId.ContainsKey(settlement.StringId))
            {
                return _settlementRecruiterDataBySettlementId[settlement.StringId];
            }
            return _settlementRecruiterDataBySettlementId[settlement.StringId] = InitializeRecruiterDataAtSettlement(settlement);
        }

        private bool TryToHireRecruiterAtSettlement(Settlement settlement)
        {
            var costToHire = GetRecruiterCost();
            if (costToHire > Hero.MainHero.Gold)
            {
                InformationManager.DisplayMessage(new InformationMessage("You cannot afford to hire a recruiter here."));
                return false;
            }
            var recruiterData = GetRecruiterDataAtSettlement(settlement);
            recruiterData.HasRecruiter = true;
            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, costToHire);
            return true;
        }

        private RecruiterData InitializeRecruiterDataAtSettlement(Settlement settlement)
        {
            return new RecruiterData
            {
                HasRecruiter = false
            };
        }

        private static int GetPlayerMoneyOnPerson()
        {
            return Hero.MainHero.Gold;
        }
    }
}
