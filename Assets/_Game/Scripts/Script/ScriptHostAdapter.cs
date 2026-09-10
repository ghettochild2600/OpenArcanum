using Arcanum.Formats.Script;

namespace Arcanum.Script
{
    /// <summary>Fail-closed base for production hosts that intentionally expose only a bounded set of world operations.</summary>
    public abstract class ScriptHostAdapter : IScriptHost
    {
        protected static System.NotSupportedException Unsupported(string operation)
            => new System.NotSupportedException($"Script host operation is outside this production slice: {operation}.");

        public virtual void Teleport(int mapId, int x, int y) => throw Unsupported(nameof(Teleport));
        public virtual object[] ResolveFocus(int sfoType, int sfoValue, ScriptContext ctx) => throw Unsupported(nameof(ResolveFocus));
        public virtual void TogglePortal(object obj) => throw Unsupported(nameof(TogglePortal));
        public virtual void SetLocked(object obj, bool locked) => throw Unsupported(nameof(SetLocked));
        public virtual bool IsOpen(object obj) => throw Unsupported(nameof(IsOpen));
        public virtual bool IsDead(object obj) => throw Unsupported(nameof(IsDead));
        public virtual void FloatLine(object obj, int scriptNum, int lineNum) => throw Unsupported(nameof(FloatLine));
        public virtual void PrintLine(object obj, int scriptNum, int lineNum) => throw Unsupported(nameof(PrintLine));
        public virtual void ToggleOff(object obj) => throw Unsupported(nameof(ToggleOff));
        public virtual void Kill(object obj) => throw Unsupported(nameof(Kill));
        public virtual int GetGold(object obj) => throw Unsupported(nameof(GetGold));
        public virtual bool IsSwitchedOff(object obj) => throw Unsupported(nameof(IsSwitchedOff));
        public virtual bool IsFollowingPc(object obj) => throw Unsupported(nameof(IsFollowingPc));
        public virtual bool IsInCombat(object obj) => throw Unsupported(nameof(IsInCombat));
        public virtual bool IsAtTile(object obj, int x, int y) => throw Unsupported(nameof(IsAtTile));
        public virtual bool IsWithinRange(object obj, int x, int y, int range) => throw Unsupported(nameof(IsWithinRange));
        public virtual bool IsNamed(object obj, int name) => throw Unsupported(nameof(IsNamed));
        public virtual bool HasMetPc(object obj) => throw Unsupported(nameof(HasMetPc));
        public virtual bool IsInDialog(object obj) => throw Unsupported(nameof(IsInDialog));
        public virtual bool IsDaytime() => throw Unsupported(nameof(IsDaytime));
        public virtual bool HasItemNamed(object obj, int nameId) => throw Unsupported(nameof(HasItemNamed));
        public virtual bool IsMonsterOfType(object obj, int specie) => throw Unsupported(nameof(IsMonsterOfType));
        public virtual bool IsWieldingItemNamed(object obj, int nameId) => throw Unsupported(nameof(IsWieldingItemNamed));
        public virtual bool RumorKnown(object obj, int rumorId) => throw Unsupported(nameof(RumorKnown));
        public virtual bool CanOpenPortal(object actor, object portal, int direction) => throw Unsupported(nameof(CanOpenPortal));
        public virtual bool CanOpenContainer(object actor, object container) => throw Unsupported(nameof(CanOpenContainer));
        public virtual bool CanSeeObj(object observer, object target) => throw Unsupported(nameof(CanSeeObj));
        public virtual bool CanHearObj(object listener, object source) => throw Unsupported(nameof(CanHearObj));
        public virtual bool IsAnimal(object obj) => throw Unsupported(nameof(IsAnimal));
        public virtual bool IsUndead(object obj) => throw Unsupported(nameof(IsUndead));
        public virtual void AdjustGold(object obj, int amount) => throw Unsupported(nameof(AdjustGold));
        public virtual void CritterFollow(object obj) => throw Unsupported(nameof(CritterFollow));
        public virtual void CritterDisband(object obj) => throw Unsupported(nameof(CritterDisband));
        public virtual int GetStat(object obj, int stat) => throw Unsupported(nameof(GetStat));
        public virtual int GetSkill(object obj, int skill) => throw Unsupported(nameof(GetSkill));
        public virtual bool KnowsSpell(object obj, int spell) => throw Unsupported(nameof(KnowsSpell));
        public virtual void StartDialog(object obj, int dialogLine, int scriptNum, int scriptLine) => throw Unsupported(nameof(StartDialog));
        public virtual void Attack(object attacker, object target) => throw Unsupported(nameof(Attack));
        public virtual void Damage(object obj, int amount, int type) => throw Unsupported(nameof(Damage));
        public virtual void HealHp(object obj, int amount) => throw Unsupported(nameof(HealHp));
        public virtual void HealFatigue(object obj, int amount) => throw Unsupported(nameof(HealFatigue));
        public virtual void CastSpell(object source, int spell, object target) => throw Unsupported(nameof(CastSpell));
        public virtual void MarkMapLocation(object pc, int area) => throw Unsupported(nameof(MarkMapLocation));
        public virtual void SetRumor(object pc, int rumor) => throw Unsupported(nameof(SetRumor));
        public virtual void UnfogTownmap(int map) => throw Unsupported(nameof(UnfogTownmap));
        public virtual void QuellRumor(object pc, int rumor) => throw Unsupported(nameof(QuellRumor));
        public virtual void RunObjectScript(object triggerer, object attachee, int sap, int line) => throw Unsupported(nameof(RunObjectScript));
        public virtual ScriptFile GetScript(int num) => throw Unsupported(nameof(GetScript));
    }
}
