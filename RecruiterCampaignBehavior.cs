using System;
using System.Collections.Generic;
using System.Linq;
using NetworkMessages.FromServer;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Overlay;
using TaleWorlds.CampaignSystem.SandBox.GameComponents.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
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

        private void Cleanup()
        {
            foreach (var settlementId in _settlementRecruiterDataBySettlementId.Keys)
            {
                var settlement = Settlement.Find(settlementId);
                var recruiterData = GetRecruiterDataAtSettlement(settlement);

                // check if settlement is still owned by player, set recruiter to level 0 if not
                if (settlement.OwnerClan != Clan.PlayerClan)
                {
                    if (recruiterData.HasRecruiter)
                    {
                        recruiterData.HasRecruiter = false;
                        recruiterData.RecruiterLevel = 0;
                        recruiterData.IsRecruiterEnabled = false;
                        recruiterData.MaxPercentOfUnitsInGarrison = 50;
                        InformationManager.DisplayMessage(new InformationMessage($"{settlement.Name} is no longer in your hands. Your recruiter ran away."));
                    }
                }

                // this ensure that anyone with recruiter without level or limit (early version didn't have it) can use it properly
                if (recruiterData.HasRecruiter && recruiterData.RecruiterLevel == 0)
                {
                    recruiterData.RecruiterLevel = 1;
                    recruiterData.IsRecruiterEnabled = true;
                    recruiterData.MaxPercentOfUnitsInGarrison = 50;
                }
            }
        }
        
        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            Cleanup();
            AddMenus(campaignGameStarter);
        }

        private void OnDailyTick()
        {
            UpdateRecruiter();
        }

        private void UpdateRecruiter()
        {
            Cleanup();

            var message = "- ";
            InformationManager.DisplayMessage(new InformationMessage("Recruiter Garrison Report:"));
            
            var shouldAddSeparator = false;
            var totalNewRecruits = 0;
            
            foreach (var settlementId in _settlementRecruiterDataBySettlementId.Keys)
            {
                var settlement = Settlement.Find(settlementId);
                var recruiterData = GetRecruiterDataAtSettlement(settlement);
                
                if(recruiterData.HasRecruiter && recruiterData.IsRecruiterEnabled)
                {
                    // if there is no party, make one
                    if (settlement.Town.GarrisonParty == null)
                    {
                        settlement.AddGarrisonParty();
                    }
                    
                    // get number of troops to be recruited
                    var count = GetRecruitsPerDayAtSettlement(settlement);
                    var currentGarrisonCount = settlement.Town.GarrisonParty?.Party.NumberOfAllMembers ?? 0;
                    var maxGarrisonCount = settlement.Town.GarrisonParty?.Party.PartySizeLimit ?? 0;
                    
                    // make sure we don't recruit over the limit
                    if (currentGarrisonCount + count >= maxGarrisonCount)
                    {
                        count = maxGarrisonCount - currentGarrisonCount;
                    }
                    
                    // make sure we don't recruit over the limit based on garrison max count and user's limit in %
                    var maxGarrisonCountByPlayerLimit =
                        Math.Round((double)recruiterData.MaxPercentOfUnitsInGarrison / 100 * (double)maxGarrisonCount);
                    if (currentGarrisonCount + count >= maxGarrisonCountByPlayerLimit)
                    {
                        count = 0; // do not add any units
                    }

                    // add information to log message (only if there is a change)
                    if (count > 0)
                    {
                        totalNewRecruits += count;
                        if (shouldAddSeparator) message += ", ";
                        shouldAddSeparator = true;
                        
                        message += $"{settlement.Name} (+{count})";
                    }

                    // add roster to garrison
                    if (settlement.Town.GarrisonParty != null)
                        settlement.Town.GarrisonParty.AddElementToMemberRoster(settlement.Culture.BasicTroop, count,
                            false);
                }
            }

            // if there are no new recruits, tell player
            if (totalNewRecruits == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("- No new recruits today"));
                return;
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage($"- Total: {totalNewRecruits} new recruits"));
            }

            // log message (only if there are any settlements)
            if (_settlementRecruiterDataBySettlementId.Count > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage($"{message}"));
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
                "{=recruiter_info}{DANGER_RECRUITER_INFO}\n{DANGER_RECRUITER_CURRENT_LIMIT}",
                args => UpdateRecruiterMenuTextVariables(),
                GameOverlays.MenuOverlayType.SettlementWithBoth
            );
            campaignGameStarter.AddGameMenuOption(
                "recruiter",
                "recruiter_hire",
                "{=recruiter_hire}{DANGER_RECRUITER_ACTION} ({DANGER_RECRUITER_COST}{GOLD_ICON})",
                args =>
                {
                    var canPlayerAffordToHireRecruiterAtSettlement = CanPlayerAffordToHireRecruiterAtSettlement(Settlement.CurrentSettlement);
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    args.IsEnabled = canPlayerAffordToHireRecruiterAtSettlement;
                    args.Tooltip = canPlayerAffordToHireRecruiterAtSettlement
                        ? TextObject.Empty
                        : new TextObject("You cannot afford to hire a recruiter here.");
                    
                    if (DoesPlayerHaveRecruiterAtSettlement(Settlement.CurrentSettlement))
                    {
                        var recruiterData = GetRecruiterDataAtSettlement(Settlement.CurrentSettlement);
                        return recruiterData.RecruiterLevel < 4 ? true : false;
                    }
                    
                    return true;
                },
                args => OnRecruiterHireAtSettlement(Settlement.CurrentSettlement)
            );
            campaignGameStarter.AddGameMenuOption(
                "recruiter",
                "recruiter_increase",
                "{=recruiter_increase}{DANGER_RECRUITER_INCREASE_LIMIT}",
                args =>
                {
                    args.IsEnabled = GetRecruiterDataAtSettlement(Settlement.CurrentSettlement).MaxPercentOfUnitsInGarrison < GetIncreaseLimitInPercent(Settlement.CurrentSettlement);
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    return true;
                },
                args => OnRecruiterChangeLimit(Settlement.CurrentSettlement, GetIncreaseLimitInPercent(Settlement.CurrentSettlement))
            );
            campaignGameStarter.AddGameMenuOption(
                "recruiter",
                "recruiter_decrease",
                "{=recruiter_decrease}{DANGER_RECRUITER_DECREASE_LIMIT}",
                args =>
                {
                    args.IsEnabled = GetRecruiterDataAtSettlement(Settlement.CurrentSettlement).MaxPercentOfUnitsInGarrison > GetDecreaseLimitInPercent(Settlement.CurrentSettlement);
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    return true;
                },
                args => OnRecruiterChangeLimit(Settlement.CurrentSettlement, GetDecreaseLimitInPercent(Settlement.CurrentSettlement))
            );
            campaignGameStarter.AddGameMenuOption(
                "recruiter",
                "recruiter_apply_same_limit_to_all",
                "{=recruiter_apply_same_limit_to_all}{DANGER_RECRUITER_APPLY_SAME_LIMIT_TO_ALL}",
                args =>
                {
                    var hasRecruiter = GetRecruiterDataAtSettlement(Settlement.CurrentSettlement).HasRecruiter;
                    args.IsEnabled = hasRecruiter;
                    args.Tooltip = hasRecruiter
                        ? TextObject.Empty
                        : new TextObject("You didn't hire recruiter yet.");
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    return true;
                },
                args => OnRecruiterApplySameLimitToAllSettlements(Settlement.CurrentSettlement)
            );
            campaignGameStarter.AddGameMenuOption(
                "recruiter",
                "recruiter_enable_or_disable",
                "{=recruiter_enable_or_disable}{DANGER_RECRUITER_ENABLE_OR_DISABLE}",
                args =>
                {
                    var hasRecruiter = GetRecruiterDataAtSettlement(Settlement.CurrentSettlement).HasRecruiter;
                    args.IsEnabled = hasRecruiter;
                    args.Tooltip = hasRecruiter
                        ? TextObject.Empty
                        : new TextObject("You didn't hire recruiter yet.");
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    return true;
                },
                args => OnRecruiterEnabledOrDisabled(Settlement.CurrentSettlement)
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
                args => GameMenu.SwitchToMenu(Settlement.CurrentSettlement.IsTown ? "town" : "castle")
            );
        }

        private bool DoesPlayerHaveRecruiterAtSettlement(Settlement settlement)
        {
            var recruiterData = GetRecruiterDataAtSettlement(settlement);
            return recruiterData.HasRecruiter;
        }

        private (bool CanAccess, string ReasonMessage) CanPlayerAccessRecruiterAtSettlement(Settlement settlement)
        {
            if (RecruiterSubModule.Config.IsEnabledForGarrisonInCastles == false && settlement.IsCastle)
            {
                return (false, "If you want to access recruiter, enable it in Danger's Recruiter mod config.");
            }
            
            if (RecruiterSubModule.Config.IsEnabledForGarrisonInTowns == false && settlement.IsTown)
            {
                return (false, "If you want to access recruiter, enable it in Danger's Recruiter mod config.");
            }

            if (!CampaignTime.Now.IsDayTime)
            {
                return (false, "The recruiter is not available. Try it in the morning.");
            }

            if (settlement.OwnerClan != Clan.PlayerClan)
            {
                return (false, "This is not your settlement. The recruiter won't speak with you.");
            }

            return (true, string.Empty);
        }

        private bool CanPlayerAffordToHireRecruiterAtSettlement(Settlement settlement)
        {
            var recruiterData = GetRecruiterDataAtSettlement(settlement);

            if (recruiterData.HasRecruiter)
            {
                return GetPlayerMoneyOnPerson() >= GetRecruiterCost(recruiterData.RecruiterLevel + 1);
            }
            else
            {
                return GetPlayerMoneyOnPerson() >= GetRecruiterCost(1);
            }
        }

        private void OnRecruiterHireAtSettlement(Settlement settlement)
        {
            if (TryToHireRecruiterAtSettlement(settlement))
            {
                GameMenu.SwitchToMenu("recruiter");
            }
        }

        private void OnRecruiterEnabledOrDisabled(Settlement settlement)
        {
            if (SwitchEnabledAndDisabledState(settlement))
            {
                GameMenu.SwitchToMenu("recruiter");
            }
        }
        
        private void OnRecruiterChangeLimit(Settlement settlement, int newLimit)
        {
            var recruiterData = GetRecruiterDataAtSettlement(settlement);
            recruiterData.MaxPercentOfUnitsInGarrison = newLimit;
            GameMenu.SwitchToMenu("recruiter");
        }

        private void OnRecruiterApplySameLimitToAllSettlements(Settlement limitSettlement)
        {
            var num = 0;
            var limit = GetRecruiterDataAtSettlement(limitSettlement).MaxPercentOfUnitsInGarrison;
            
            foreach (var settlementId in _settlementRecruiterDataBySettlementId.Keys)
            {
                var settlement = Settlement.Find(settlementId);
                var recruiterData = GetRecruiterDataAtSettlement(settlement);
                
                if(recruiterData.HasRecruiter)
                {
                    recruiterData.MaxPercentOfUnitsInGarrison = limit;
                    num++;
                }
            }

            if (num > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Recruiter limit successfully set to {limit}% in {num} settlements."));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage($"You have no settlements with recruiter."));
            }
            
            GameMenu.SwitchToMenu("recruiter");
        }

        private void UpdateRecruiterMenuTextVariables()
        {
            MBTextManager.SetTextVariable("DANGER_RECRUITER_SETTLEMENT_NAME", Settlement.CurrentSettlement.Name);
            
            // cost
            var recruiterData = GetRecruiterDataAtSettlement(Settlement.CurrentSettlement);
            MBTextManager.SetTextVariable("DANGER_RECRUITER_COST", GetRecruiterCost(recruiterData.RecruiterLevel + 1));
            
            // recruits per day
            MBTextManager.SetTextVariable("DANGER_RECRUITS_PER_DAY", GetRecruitsPerDay(recruiterData.RecruiterLevel + 1));

            // title
            var recruiterTitle = recruiterData.RecruiterLevel >= 1
                ? $"recruiter (Level {recruiterData.RecruiterLevel})"
                : "recruiter";
            MBTextManager.SetTextVariable("DANGER_RECRUITER_TITLE", recruiterTitle);
            
            // action
            var recruiterAction = recruiterData.RecruiterLevel == 0
                ? "Hire a recruiter"
                : $"Upgrade to Level {recruiterData.RecruiterLevel + 1} ({GetRecruitsPerDay(recruiterData.RecruiterLevel + 1)} per day)";
            MBTextManager.SetTextVariable("DANGER_RECRUITER_ACTION", recruiterAction);
            
            // enable or disable
            var recruiterEnableOrDisable = recruiterData.IsRecruiterEnabled
                ? $"Disable recruiter in {Settlement.CurrentSettlement.Name}"
                : $"Enable recruiter in {Settlement.CurrentSettlement.Name}";
            MBTextManager.SetTextVariable("DANGER_RECRUITER_ENABLE_OR_DISABLE", recruiterEnableOrDisable);
            
            // increase and decrease limit
            MBTextManager.SetTextVariable("DANGER_RECRUITER_CURRENT_LIMIT", $"Current limit: {GetRecruiterDataAtSettlement(Settlement.CurrentSettlement).MaxPercentOfUnitsInGarrison}% of the garrison's max limit.");
            MBTextManager.SetTextVariable("DANGER_RECRUITER_INCREASE_LIMIT", $"Increase limit to {GetIncreaseLimitInPercent(Settlement.CurrentSettlement)}%.");
            MBTextManager.SetTextVariable("DANGER_RECRUITER_DECREASE_LIMIT", $"Decrease limit to {GetDecreaseLimitInPercent(Settlement.CurrentSettlement)}%.");
            MBTextManager.SetTextVariable("DANGER_RECRUITER_APPLY_SAME_LIMIT_TO_ALL", $"Apply this limit to all settlements ({GetRecruiterDataAtSettlement(Settlement.CurrentSettlement).MaxPercentOfUnitsInGarrison}%).");
            
            // info
            var recruitsPerDay = GetRecruitsPerDayAtSettlement(Settlement.CurrentSettlement);
            var recruiterInfo = recruiterData.HasRecruiter
                ? $"The {recruiterTitle} is active, {recruitsPerDay} new recruits will join your garrison every day."
                : "There is no active recruiter yet. Hire one to get 5 new recruits in your garrison every day.";
            if ((recruiterData.HasRecruiter && !recruiterData.IsRecruiterEnabled) || recruiterData.MaxPercentOfUnitsInGarrison == 0)
            {
                recruiterInfo = $"The {recruiterTitle} produces no recruits because it is disabled or the limit is set too low.";
            }
            MBTextManager.SetTextVariable("DANGER_RECRUITER_INFO", recruiterInfo);
        }

        private int GetIncreaseLimitInPercent(Settlement settlement)
        {
            var currentLimit = GetRecruiterDataAtSettlement(Settlement.CurrentSettlement).MaxPercentOfUnitsInGarrison;
            var step = RecruiterSubModule.Config.GarrisonPercentChangeByStep;
            var increaseLimit = (currentLimit + step) >= 100 ? 100 : currentLimit + step;
            return increaseLimit;
        }

        private int GetDecreaseLimitInPercent(Settlement settlement)
        {
            var currentLimit = GetRecruiterDataAtSettlement(Settlement.CurrentSettlement).MaxPercentOfUnitsInGarrison;
            var step = RecruiterSubModule.Config.GarrisonPercentChangeByStep;
            var decreaseLimit = (currentLimit - step) <= 0 ? 0 : currentLimit - step;
            return decreaseLimit;
        }
        
        private int GetRecruiterCost(int level)
        {
            switch (level)
            {
                case 1: return RecruiterSubModule.Config.RecruiterCost;
                case 2: return RecruiterSubModule.Config.EliteRecruiterCost;
                case 3: return RecruiterSubModule.Config.MasterRecruiterCost;
                case 4: return RecruiterSubModule.Config.GrandmasterRecruiterCost;
                default: return 0;
            }
        }
        
        private int GetRecruitsPerDay(int level)
        {
            switch (level)
            {
                case 1: return RecruiterSubModule.Config.RecruitsPerDay;
                case 2: return RecruiterSubModule.Config.EliteRecruitsPerDay;
                case 3: return RecruiterSubModule.Config.MasterRecruitsPerDay;
                case 4: return RecruiterSubModule.Config.GrandmasterRecruitsPerDay;
                default: return 0;
            }
        }

        private int GetRecruitsPerDayAtSettlement(Settlement settlement)
        {
            return GetRecruitsPerDay(GetRecruiterDataAtSettlement(settlement).RecruiterLevel);
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
            
            var recruiterData = GetRecruiterDataAtSettlement(settlement);
            
            var costToHire = GetRecruiterCost(recruiterData.RecruiterLevel + 1);
            if (costToHire > Hero.MainHero.Gold)
            {
                if (recruiterData.RecruiterLevel == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage("You cannot afford to hire a recruiter here."));
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage("You cannot afford to upgrade the recruiter."));
                }
                return false;
            }
            
            recruiterData.HasRecruiter = true;
            recruiterData.RecruiterLevel += 1;
            recruiterData.IsRecruiterEnabled = true;
            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, costToHire);
            return true;
        }

        private bool SwitchEnabledAndDisabledState(Settlement settlement)
        {
            var recruiterData = GetRecruiterDataAtSettlement(settlement);
            
            if (!recruiterData.HasRecruiter)
            {
                InformationManager.DisplayMessage(new InformationMessage("You didn't hire recruiter yet."));
            }
            
            recruiterData.IsRecruiterEnabled = !recruiterData.IsRecruiterEnabled;

            var stateText = recruiterData.IsRecruiterEnabled ? "Enabled" : "Disabled";
            InformationManager.DisplayMessage(new InformationMessage($"Recruiter in {settlement.Name} is {stateText} now."));
            
            return true;
        }

        private RecruiterData InitializeRecruiterDataAtSettlement(Settlement settlement)
        {
            return new RecruiterData
            {
                HasRecruiter = false,
                RecruiterLevel = 0
            };
        }

        private static int GetPlayerMoneyOnPerson()
        {
            return Hero.MainHero.Gold;
        }
    }
}
