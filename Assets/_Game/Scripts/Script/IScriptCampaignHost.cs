namespace Arcanum.Script
{
    /// <summary>
    /// Authoritative campaign services used by retail script operations beyond the small world-interaction
    /// surface in <see cref="IScriptHost"/>. Hosts opt into this interface only when they can provide the
    /// source semantics; strict VM execution fails closed when it is absent.
    /// </summary>
    public interface IScriptCampaignHost
    {
        int GetObjectType(object obj);
        int GetMagicTechAdjustment(int value, object item, object source, object target);
        int GetMagicTechAdjustmentEx(int value, object item, object source);

        int GetReaction(object npc, object pc);
        void SetReaction(object npc, object pc, int value);
        void AdjustReaction(object npc, object pc, int delta);
        bool HasReputation(object obj, int reputation);
        bool IsIdentified(object obj);
        void AddReputation(object obj, int reputation);
        void RemoveReputation(object obj, int reputation);
        int GetFaction(object obj);

        void GetLocation(object obj, out int x, out int y);
        int DistanceBetween(object left, object right);
        int GetArea(object obj);
        void GetFatiguePoints(object obj, out int current, out int maximum);

        void TransferItem(int name, object from, object to);
        void CreateItem(int prototype, object parent);
        void Destroy(object obj);
        void DestroyItemNamed(int name, object owner);

        int GetDaySinceStartup();
        int GetCurrentHour();
        int GetCurrentMinute();
        int GetHoursSinceStartup();
        void AdvanceTimeSeconds(int seconds);
        void ScheduleScriptCall(int scriptNumber, int startLine, object triggerer, object attachee,
            int seconds, bool atTimeOfDay);
        void RemoveScriptCall(int scriptNumber, object attachee);
        void AddEffect(object target, int effectId, int cause);
        void RemoveEffect(object target, int effectId);
        void GrantOneFatePoint(object obj);
    }
}
