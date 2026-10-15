using Arcanum.Formats.Script;

namespace Arcanum.Script
{
    /// <summary>Static VM-dispatch coverage used by the retail script compatibility audit.</summary>
    public enum ScriptOpcodeSupportStatus
    {
        Implemented,
        Partial,
        Stub,
        Unsupported,
    }

    public readonly struct ScriptOpcodeProfile
    {
        public ScriptOpcodeProfile(ScriptOpcodeSupportStatus status, string dependency, string risk,
            string sourceLocation, string note = "")
        {
            Status = status;
            Dependency = dependency;
            Risk = risk;
            SourceLocation = sourceLocation;
            Note = note;
        }

        public ScriptOpcodeSupportStatus Status { get; }
        public string Dependency { get; }
        public string Risk { get; }
        public string SourceLocation { get; }
        public string Note { get; }
    }

    public static class ScriptOpcodeCompatibility
    {
        private const string ConditionSource = "arcanum-ce/src/game/script.c:script_execute_condition";
        private const string ActionSource = "arcanum-ce/src/game/script.c:script_execute_action";

        public static ScriptOpcodeProfile Condition(Sct opcode)
        {
            switch (opcode)
            {
                case Sct.True:
                case Sct.LocalFlag:
                case Sct.Eq:
                case Sct.Le:
                case Sct.PcQuestState:
                case Sct.GlobalQuestState:
                case Sct.GlobalFlag:
                    return Implemented("campaign/local script state", "low", ConditionSource);

                case Sct.Daytime:
                case Sct.HasGold:
                case Sct.ObjMetPcBefore:
                case Sct.ObjHasItemNamed:
                case Sct.ObjFollowingPc:
                case Sct.ObjIsMonsterOfType:
                case Sct.ObjIsNamed:
                case Sct.ObjIsWieldingItem:
                case Sct.ObjIsDead:
                case Sct.ObjIsInDialog:
                case Sct.ObjIsSwitchedOff:
                case Sct.ObjIsInCombat:
                case Sct.ObjIsAtLocation:
                case Sct.ObjWithinRange:
                case Sct.RumorKnown:
                case Sct.ObjIsOpen:
                case Sct.ObjIsAnimal:
                case Sct.ObjIsUndead:
                case Sct.KnowsSpell:
                case Sct.ObjHasReputation:
                case Sct.Identified:
                    return Implemented("IScriptHost production authority", "medium", ConditionSource);

                case Sct.ObjCanOpenContainer:
                case Sct.ObjCanSeeObj:
                case Sct.ObjCanHearObj:
                case Sct.CanOpenPortal:
                    return Partial("bounded AI/spatial approximation", "medium", ConditionSource,
                        "VM dispatch exists; production host intentionally uses a bounded approximation.");

                case Sct.ObjJilted:
                    return Partial("party relationship authority", "medium", ConditionSource,
                        "VM dispatch exists; hosts without a relationship model return false.");

                case Sct.ObjHasBless:
                case Sct.ObjHasCurse:
                case Sct.ObjIsPolymorphed:
                case Sct.ObjIsShrunk:
                case Sct.ObjHasBodySpell:
                case Sct.ObjIsInvisible:
                case Sct.ObjHasMirrorImage:
                case Sct.ObjHasSurrendered:
                case Sct.ObjIsBusted:
                case Sct.ObjHasMaxFollowers:
                case Sct.ObjIsInfluencedBySpell:
                case Sct.ObjHasBadAssociates:
                case Sct.ObjIsInvulnerable:
                case Sct.Prowling:
                case Sct.WaitingForLeader:
                case Sct.ItemsAreBeingRewielded:
                case Sct.RumorQuelled:
                case Sct.MonstergenDisabled:
                case Sct.SectorIsBlocked:
                case Sct.MasteredSpellCollege:
                    return Stub("missing downstream state authority", "high", ConditionSource,
                        "Strict execution rejects this condition; non-strict execution assumes false.");

                default:
                    return Unsupported("unclassified condition", "high", ConditionSource);
            }
        }

        public static ScriptOpcodeProfile Action(Sat opcode)
        {
            switch (opcode)
            {
                case Sat.DoNothing:
                case Sat.ReturnAndSkipDefault:
                case Sat.ReturnAndRunDefault:
                case Sat.Goto:
                case Sat.SetLocalFlag:
                case Sat.ClearLocalFlag:
                case Sat.AssignNum:
                case Sat.Add:
                case Sat.Subtract:
                case Sat.Multiply:
                case Sat.Divide:
                case Sat.AssignObj:
                case Sat.SetPcQuestState:
                case Sat.SetQuestGlobalState:
                case Sat.LoopFor:
                case Sat.LoopEnd:
                case Sat.LoopBreak:
                case Sat.GetStoryState:
                case Sat.SetStoryState:
                case Sat.Random:
                case Sat.SetGlobalFlag:
                case Sat.ClearGlobalFlag:
                    return Implemented("VM/campaign state", "low", ActionSource);

                case Sat.Dialog:
                case Sat.RemoveThisScript:
                case Sat.ChangeThisScriptToScript:
                case Sat.CallScript:
                case Sat.CritterFollow:
                case Sat.CritterDisband:
                case Sat.FloatLine:
                case Sat.PrintLine:
                case Sat.GetStat:
                case Sat.AdjustGold:
                case Sat.Attack:
                case Sat.GetSkill:
                case Sat.CastSpell:
                case Sat.MarkMapLocation:
                case Sat.SetRumor:
                case Sat.QuellRumor:
                case Sat.SetLockState:
                case Sat.CallScriptEx:
                case Sat.ToggleState:
                case Sat.Kill:
                case Sat.Damage:
                case Sat.HealHp:
                case Sat.HealFatigue:
                case Sat.UnfogTownmap:
                case Sat.ToggleOpenClosed:
                case Sat.GetHitPoints:
                case Sat.Teleport:
                    return Implemented("IScriptHost production authority", "medium", ActionSource);

                case Sat.GetObjectType:
                case Sat.GetMagictechAdjustment:
                case Sat.GetMagictechAdjustmentEx:
                case Sat.GetReaction:
                case Sat.SetReaction:
                case Sat.AdjustReaction:
                case Sat.AddReputation:
                case Sat.RemoveReputation:
                case Sat.GetFaction:
                case Sat.GetDaySinceStartup:
                case Sat.GetCurrentHour:
                case Sat.GetCurrentMinute:
                case Sat.GetHoursSinceStartup:
                case Sat.CallScriptIn:
                case Sat.CallScriptAt:
                case Sat.RemoveScriptCall:
                case Sat.GetLocation:
                case Sat.DistanceBetween:
                case Sat.GetArea:
                case Sat.TransferItem:
                case Sat.CreateItem:
                case Sat.Destroy:
                case Sat.DestroyItemNamed:
                case Sat.GetFatiguePoints:
                case Sat.GrantOneFatePoint:
                    return Implemented("IScriptCampaignHost production authority", "medium", ActionSource);

                case Sat.FadeAndTeleport:
                    return Partial("map transition authority", "medium", ActionSource,
                        "Teleport is authoritative; the source fade presentation is intentionally omitted.");
                case Sat.Fade:
                    return Partial("source time authority", "medium", ActionSource,
                        "Campaign time advancement is authoritative; fade, sound, and movie presentation is omitted.");

                case Sat.PlaySound:
                case Sat.PlaySoundOn:
                case Sat.PlaySoundScheme:
                case Sat.PlaySpellEyeCandy:
                case Sat.StopSpellEyeCandy:
                case Sat.PlayScriptEyeCandy:
                case Sat.StopScriptEyeCandy:
                case Sat.TouchArt:
                    return Partial("presentation authority", "low", ActionSource,
                        "Script control flow is preserved; source audio/visual presentation is not yet projected.");

                default:
                    return Unsupported(Dependency(opcode), Risk(opcode), ActionSource);
            }
        }

        private static string Dependency(Sat opcode)
        {
            switch (opcode)
            {
                case Sat.GetReaction:
                case Sat.SetReaction:
                case Sat.AdjustReaction:
                case Sat.AddReputation:
                case Sat.RemoveReputation:
                case Sat.GetFaction:
                case Sat.SetFaction:
                    return "social/reputation authority";
                case Sat.TransferItem:
                case Sat.CreateObject:
                case Sat.CreateItem:
                case Sat.Destroy:
                case Sat.DestroyItemNamed:
                case Sat.ToggleItemInventoryDisplay:
                case Sat.ActionUseItem:
                    return "world object/inventory authority";
                case Sat.GetDaySinceStartup:
                case Sat.GetCurrentHour:
                case Sat.GetCurrentMinute:
                case Sat.GetHoursSinceStartup:
                case Sat.GiveQuestXp:
                    return "character progression authority";
                case Sat.ActionWalkTo:
                case Sat.ActionRunTo:
                case Sat.ActionWaitForLeader:
                case Sat.ActionStopAttacking:
                    return "movement/AI intent authority";
                case Sat.GetLocation:
                case Sat.DistanceBetween:
                case Sat.GetArea:
                    return "world spatial authority";
                case Sat.HealPoison:
                    return "poison authority";
                default:
                    return "missing downstream gameplay authority";
            }
        }

        private static string Risk(Sat opcode)
        {
            switch (opcode)
            {
                case Sat.GetDaySinceStartup:
                case Sat.GetCurrentHour:
                case Sat.GetCurrentMinute:
                case Sat.GetHoursSinceStartup:
                case Sat.GetLocation:
                case Sat.DistanceBetween:
                case Sat.GetArea:
                case Sat.GetReaction:
                case Sat.GetFaction:
                    return "medium";
                default:
                    return "high";
            }
        }

        private static ScriptOpcodeProfile Implemented(string dependency, string risk, string source,
            string note = "")
            => new(ScriptOpcodeSupportStatus.Implemented, dependency, risk, source, note);

        private static ScriptOpcodeProfile Partial(string dependency, string risk, string source,
            string note = "")
            => new(ScriptOpcodeSupportStatus.Partial, dependency, risk, source, note);

        private static ScriptOpcodeProfile Stub(string dependency, string risk, string source,
            string note = "")
            => new(ScriptOpcodeSupportStatus.Stub, dependency, risk, source, note);

        private static ScriptOpcodeProfile Unsupported(string dependency, string risk, string source,
            string note = "")
            => new(ScriptOpcodeSupportStatus.Unsupported, dependency, risk, source, note);
    }
}
