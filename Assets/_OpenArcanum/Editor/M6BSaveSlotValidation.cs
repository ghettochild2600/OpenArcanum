using System;
using System.Collections;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M6BSaveSlotValidation
{
    private const string Ground = "maps/arcanum1-024-fixed/101602821845.sec";
    private const string FoodKey = "G_8781D726_74FE_0846_AD0A_88EE591B6383";
    private const string Inventory = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string ContainerKey = "G_8F454608_E327_1341_B85B_E7A5402D4758";
    private const string ArmorKey = "G_0435F503_6600_6342_97B2_6D9E1A85A2F2";
    private const string FemaleKey = "G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F";
    private const string Door = "maps/arcanum1-024-fixed/122473678402.sec";
    private const string Mayor = "maps/arcanum1-024-fixed/96636765255.sec";
    private const string MayorKey = "G_787AD4AB_9061_2B4E_A691_F582800B2BB3";
    private static int _warnings, _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M6B/Run Two-Slot PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode in TestTerrain first.");
        var loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        GraphicsMode initial = OpenArcanumGraphicsSettings.Mode;
        string directory = Path.Combine(Application.temporaryCachePath,
            "OpenArcanum-M6B-" + Guid.NewGuid().ToString("N"));
        BeginTracking();
        try
        {
            WorldMapSessionCoordinator session = loader.Session;
            var slots = new SessionSaveSlotService(session, directory);
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(Ground), "real GUID-item sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle life,
                out PlayerNavigationController navigation, out _);
            ArcanumObjectId pc = session.PlayerState.Identity;
            PersistentObjectState food = Require(session, FoodKey, ObjectType.Food, 10078);
            Check(session.TryGetLoadedObject(food.Identity, out WorldObject foodView)
                  && session.SetMovementState(pc, foodView.Tile, session.PlayerState.ArtId, false)
                  && session.ExecuteInteraction(WorldInteractionCommand.PickUp(pc, food.Identity)).IsSuccess,
                "real food pickup succeeds");

            Check(session.SelectSector(Inventory), "real source-parent sector loads");
            yield return null;
            PersistentObjectState container = Require(session, ContainerKey, ObjectType.Container, 3052);
            PersistentObjectState armor = Require(session, ArmorKey, ObjectType.Armor, 8127);
            PersistentObjectState female = Require(session, FemaleKey, ObjectType.Npc, 17101);
            Check(armor.AuthoredParentIdentity == container.Identity, "real authored parent is retained");
            Check(session.TransferItem(armor.Identity, armor.Placement, ObjectPlacement.ContainedBy(pc)).Succeeded,
                "real armor relocates from its source parent");
            Check(WorldMapSessionCoordinator.TryGetNaturalWornLocation(armor, out WornLocation armorSlot),
                "real armor resolves a natural worn slot");
            Check(session.EquipItem(pc, armor.Identity, armorSlot).Succeeded, "real armor equips");
            session.Vitality.ApplyHitPointDamage(female.Identity, 3);
            session.DerivedStats.SetAlignment(female.Identity, 17);

            Check(session.SelectSector(Door), "real portal sector loads");
            yield return null;
            Refresh(out loader, out life, out navigation, out _);
            WorldObjectSpriteOwner doorOwner = loader.SpriteOwners.Where(owner => owner?.WorldObject != null
                    && owner.WorldObject.Type == ObjectType.Portal
                    && owner.WorldObject.Identity.Type == ArcanumObjectIdType.Positional
                    && !PortalTransitionScheduler.IsWindow(owner.WorldObject.ArtId)
                    && owner.FrameCount > PortalTransitionScheduler.OpenFrame(owner.WorldObject.ArtId))
                .OrderBy(owner => owner.WorldObject.Identity.Key, StringComparer.Ordinal).FirstOrDefault();
            Check(doorOwner != null, "real animated portal has P_ identity");
            ArcanumObjectId door = doorOwner.WorldObject.Identity;
            yield return SetPortal(session, door, true);

            Check(session.SelectSector(Mayor), "real campaign sector loads");
            yield return null;
            Refresh(out loader, out life, out navigation, out _);
            PersistentObjectState mayor = Require(session, MayorKey, ObjectType.Npc, 17088);
            Vector2Int a = FindWalkable(loader, new Vector2Int(2, 2));
            Check(a.x >= 0 && session.SetMovementState(pc, a, session.PlayerState.ArtId, false), "slot A position");
            SetAccepted(session, mayor.Identity);
            session.Campaign.SetVar(10, 111);
            session.AddGold(pc, 100);
            Check(slots.SaveSlot("alpha").Succeeded, "slot A saves atomically");

            Vector2Int b = FindWalkable(loader, new Vector2Int(a.x + 6, a.y + 6));
            Check(b.x >= 0 && session.SetMovementState(pc, b, session.PlayerState.ArtId, false), "slot B position");
            session.Campaign.SetPcQuestState(1005, (int)QuestState.Completed);
            Check(session.Progression.IncreaseSkill(pc, CharacterSkill.Persuasion) == SkillIncreaseResult.Success
                  && session.Progression.SetTrainingLevel(pc, CharacterSkill.Persuasion,
                      SkillTrainingLevel.Apprentice) == TrainingAssignmentResult.Success, "slot B training");
            Check(session.TryTransferGold(pc, mayor.Identity, 99, out _), "slot B Gold payment");
            Check(session.UnequipItem(pc, armorSlot).Succeeded
                  && session.TransferItem(armor.Identity, armor.Placement,
                      ObjectPlacement.ContainedBy(container.Identity)).Succeeded, "slot B containment change");
            Check(session.ExecuteInteraction(WorldInteractionCommand.Drop(pc, food.Identity, Mayor, b)).IsSuccess,
                "slot B foreign-sector relocation");
            Check(session.SelectSector(Door), "slot B portal sector");
            yield return null;
            yield return SetPortal(session, door, false);
            Check(session.SelectSector(Mayor), "slot B selected sector");
            yield return null;
            Refresh(out loader, out life, out navigation, out _);
            Check(session.SetMovementState(pc, b, session.PlayerState.ArtId, false),
                "slot B position is restored after the portal-sector visit");
            session.Campaign.SetVar(10, 222);
            Check(slots.SaveSlot("beta").Succeeded, "slot B saves atomically");

            SessionSaveSlotListResult list = slots.ListSlots();
            Check(list.Succeeded && list.Slots.Select(x => x.SlotId).SequenceEqual(new[] { "alpha", "beta" })
                  && list.Slots.All(x => x.IsValid && x.Metadata.SessionVersion == 1 && x.Metadata.PcIdentity == pc.Key),
                "sorted metadata-bearing slot enumeration");
            Inspect(slots, "alpha"); Inspect(slots, "beta");

            yield return LoadA(slots, session, pc, food.Identity, armor.Identity, armorSlot, door,
                mayor.Identity, female.Identity, a);
            yield return LoadB(slots, session, pc, food.Identity, armor.Identity, container.Identity,
                door, mayor.Identity, b);
            yield return LoadA(slots, session, pc, food.Identity, armor.Identity, armorSlot, door,
                mayor.Identity, female.Identity, a);

            PersistentPlayerState active = session.PlayerState;
            int activeVar = session.Campaign.GetVar(10);
            File.WriteAllText(Path.Combine(slots.SaveDirectory,
                "beta" + SessionSaveSlotService.SlotFileExtension), "{corrupt");
            SessionSaveSlotResult corrupt = slots.LoadSlot("beta");
            Check(corrupt.Failure == SessionSaveSlotFailure.MalformedSlot
                  && ReferenceEquals(session.PlayerState, active) && session.Campaign.GetVar(10) == activeVar,
                "corrupt B leaves active A exact");

            Check(session.Journal.TryProject(1005, false, out QuestJournalEntry journal)
                  && journal.State == QuestState.Accepted, "slot A journal remains Accepted");
            DialogueStartStatus dialogueStart = session.Dialogue.Start(pc, mayor.Identity);
            Check(dialogueStart == DialogueStartStatus.Started,
                $"slot A dialogue restarts ({dialogueStart})");
            string responseNumbers = string.Join(",", session.Dialogue.AvailableResponses.Select(x => x.Num));
            Check(session.Dialogue.AvailableResponses.Any(x => x.Num == 11)
                  && session.Dialogue.AvailableResponses.All(x => x.Num != 5),
                $"slot A Accepted/no-dagger dialogue branch remains available ({responseNumbers})");
            session.Dialogue.Cancel("M6B proof");
            Vector2Int drop = Vector2Int.RoundToInt(session.PlayerState.TilePosition);
            Check(session.ExecuteInteraction(WorldInteractionCommand.Drop(pc, food.Identity, Mayor, drop)).IsSuccess
                  && session.ExecuteInteraction(WorldInteractionCommand.PickUp(pc, food.Identity)).IsSuccess,
                "post-load drop/pickup works");
            Check(session.UnequipItem(pc, armorSlot).Succeeded
                  && session.EquipItem(pc, armor.Identity, armorSlot).Succeeded, "post-load unequip/equip works");
            Vector2 before = session.PlayerState.MapPosition;
            Check(TryStartRoute(loader, navigation), "post-load navigation starts");
            float deadline = Time.realtimeSinceStartup + 15f;
            while (navigation.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Check(Time.realtimeSinceStartup < deadline && session.PlayerState.MapPosition != before,
                "post-load navigation completes");

            Check(session.SelectSector(Door), "post-load portal reload");
            yield return null;
            Refresh(out loader, out life, out navigation, out _);
            Check(session.TryGetLoadedObject(door, out WorldObject loadedDoor)
                  && loadedDoor.RequestPortalOpen(false), "post-load portal closes");
            yield return WaitPortal(session, door);
            Check(loadedDoor.RequestPortalOpen(true), "post-load portal reopens");
            yield return WaitPortal(session, door);

            Check(session.SelectSector(Ground), "original food sector reloads");
            yield return null;
            Check(session.States[food.Identity].Placement == ObjectPlacement.ContainedBy(pc)
                  && !session.TryGetLoadedObject(food.Identity, out _), "no food resurrection at source");
            Check(session.SelectSector(Inventory), "original armor sector reloads");
            yield return null;
            Refresh(out loader, out life, out navigation, out _);
            Check(session.States[armor.Identity].AuthoredParentIdentity == container.Identity
                  && session.States[armor.Identity].Placement == ObjectPlacement.EquippedBy(pc, armorSlot)
                  && !session.TryGetLoadedObject(armor.Identity, out _), "no authored containment resurrection");
            Check(session.Characters.States.ContainsKey(female.Identity)
                  && session.Vitality.Get(female.Identity).HitPointDamage == 3
                  && session.DerivedStats.GetAlignment(female.Identity) == 17, "multi-NPC registries remain exact");
            Rebuild(loader, initial); yield return null;
            CheckUnique(loader, life, navigation, session);
            Check(slots.DeleteSlot("beta").Succeeded && slots.DeleteSlot("alpha").Succeeded
                  && slots.ListSlots().Slots.Count == 0, "delete and empty list work");
            StopTracking();
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log($"M6B PLAYMODE VALIDATION PASS: alpha/beta/alpha; pc={pc}; food={food.Identity}; "
                      + $"armor={armor.Identity}; parent={container.Identity}; portal={door}; "
                      + $"npcs={mayor.Identity},{female.Identity}; corrupt={corrupt.Failure}; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initial); StopTracking();
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
        }
    }

    private static IEnumerator LoadA(SessionSaveSlotService slots, WorldMapSessionCoordinator s,
        ArcanumObjectId pc, ArcanumObjectId food, ArcanumObjectId armor, WornLocation slot,
        ArcanumObjectId door, ArcanumObjectId mayor, ArcanumObjectId female, Vector2 position)
    {
        Check(slots.LoadSlot("alpha").Succeeded, "load A"); yield return null;
        Check(s.PlayerState.TilePosition == position && s.States[door].PortalOpen
              && s.States[food].Placement == ObjectPlacement.ContainedBy(pc)
              && s.States[armor].Placement == ObjectPlacement.EquippedBy(pc, slot)
              && s.Campaign.GetVar(10) == 111 && s.Campaign.GetPcQuestState(1005) == (int)QuestState.Accepted
              && s.Progression.GetTrainingLevel(pc, CharacterSkill.Persuasion) == SkillTrainingLevel.None
              && s.GetGold(pc) == 100 && s.GetGold(mayor) == 0
              && s.Vitality.Get(female).HitPointDamage == 3, "A exact/no B leakage");
    }

    private static IEnumerator LoadB(SessionSaveSlotService slots, WorldMapSessionCoordinator s,
        ArcanumObjectId pc, ArcanumObjectId food, ArcanumObjectId armor, ArcanumObjectId container,
        ArcanumObjectId door, ArcanumObjectId mayor, Vector2 position)
    {
        Check(slots.LoadSlot("beta").Succeeded, "load B"); yield return null;
        Check(s.PlayerState.TilePosition == position, "B PC position");
        Check(!s.States[door].PortalOpen, "B portal state");
        Check(s.States[food].Placement == ObjectPlacement.InWorld(Mayor, position), "B relocated food");
        Check(s.States[armor].Placement == ObjectPlacement.ContainedBy(container), "B armor containment");
        Check(s.Campaign.GetVar(10) == 222
              && s.Campaign.GetPcQuestState(1005) == (int)QuestState.Completed, "B campaign");
        Check(s.Progression.GetTrainingLevel(pc, CharacterSkill.Persuasion) == SkillTrainingLevel.Apprentice,
            "B training");
        Check(s.GetGold(pc) == 1 && s.GetGold(mayor) == 99, "B Gold");
        Check(s.Journal.TryProject(1005, false, out QuestJournalEntry j) && j.State == QuestState.Completed
              && s.Dialogue.Start(pc, mayor) == DialogueStartStatus.Started
              && s.Dialogue.AvailableResponses.Any(x => x.Num == 5), "B journal/dialogue/reward idempotency");
        s.Dialogue.Cancel("M6B B proof");
    }

    private static void Inspect(SessionSaveSlotService slots, string id)
    {
        string json = File.ReadAllText(Path.Combine(slots.SaveDirectory, id + SessionSaveSlotService.SlotFileExtension));
        Check(json.Contains("\"slotFormat\": \"OpenArcanum.SaveSlot\"") && json.Contains("\"metadata\":")
              && json.Contains("\"session\":") && json.Contains("\"format\": \"OpenArcanum.SessionSave\"")
              && !json.Contains("GameObject") && !json.Contains("instanceId")
              && !json.Contains(Application.dataPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase),
            $"inspect {id}");
    }

    private static IEnumerator SetPortal(WorldMapSessionCoordinator s, ArcanumObjectId id, bool open)
    {
        if (s.States[id].PortalOpen == open) yield break;
        Check(s.Portals.Request(id, open), "portal request"); yield return WaitPortal(s, id);
    }

    private static IEnumerator WaitPortal(WorldMapSessionCoordinator s, ArcanumObjectId id)
    {
        float end = Time.realtimeSinceStartup + 6f;
        while (s.Portals.TryGetPhase(id, out PortalPhase p) && p is PortalPhase.Opening or PortalPhase.Closing
               && Time.realtimeSinceStartup < end) yield return null;
        Check(Time.realtimeSinceStartup < end, "portal stable");
    }

    private static void SetAccepted(WorldMapSessionCoordinator s, ArcanumObjectId mayor)
    {
        if (s.Campaign.GetPcQuestState(1005) == (int)QuestState.Unknown)
            s.Campaign.SetPcQuestState(1005, (int)QuestState.Mentioned);
        Check(s.Dialogue.Start(s.PlayerState.Identity, mayor) == DialogueStartStatus.Started,
            "authentic mayor dialogue starts for slot A acceptance");
        int response = s.Dialogue.AvailableResponses.ToList().FindIndex(line => line.Num == 3);
        Check(response >= 0 && s.Dialogue.SelectResponse(response) == DialogueChoiceStatus.Advanced
              && s.Campaign.GetPcQuestState(1005) == (int)QuestState.Accepted,
            "authored response 3 establishes slot A Accepted state");
        Check(s.Dialogue.Cancel("M6B slot A fixture"), "slot A acceptance dialogue cancels");
    }

    private static PersistentObjectState Require(WorldMapSessionCoordinator s, string key, ObjectType type, int pro)
    {
        PersistentObjectState value = s.States.Values.Single(x => x.Identity.Key == key);
        Check(value.Type == type && value.PrototypeNumber == pro, key); return value;
    }

    private static Vector2Int FindWalkable(WorldObjectSectorLoader l, Vector2Int p)
    {
        if (l.NavigationMap.IsWalkable(p)) return p;
        for (int y = 0; y < SectorCoordinate.Size; y++) for (int x = 0; x < SectorCoordinate.Size; x++)
            if (l.NavigationMap.IsWalkable(new Vector2Int(x, y))) return new Vector2Int(x, y);
        return new Vector2Int(-1, -1);
    }

    private static bool TryStartRoute(WorldObjectSectorLoader l, PlayerNavigationController n)
    {
        Vector2Int start = Vector2Int.RoundToInt(n.Player.TilePosition);
        for (int r = 1; r <= 8; r++) for (int y = Math.Max(0, start.y-r); y <= Math.Min(63, start.y+r); y++)
        for (int x = Math.Max(0, start.x-r); x <= Math.Min(63, start.x+r); x++)
        { var p = new Vector2Int(x,y); if (p != start && l.NavigationMap.IsWalkable(p) && n.TrySetDestination(p)) return true; }
        return false;
    }

    private static void Rebuild(WorldObjectSectorLoader l, GraphicsMode initial)
    {
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original); l.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced); l.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original); l.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(initial); l.RebuildVisuals();
    }

    private static void Refresh(out WorldObjectSectorLoader l, out ProductionPlayerLifecycle p,
        out PlayerNavigationController n, out PlayerInteractionController i)
    {
        l=Object.FindFirstObjectByType<WorldObjectSectorLoader>(); p=Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        n=Object.FindFirstObjectByType<PlayerNavigationController>(); i=Object.FindFirstObjectByType<PlayerInteractionController>();
        Check(l != null && p != null && n != null && i != null, "composition");
    }

    private static void CheckUnique(WorldObjectSectorLoader l, ProductionPlayerLifecycle p,
        PlayerNavigationController n, WorldMapSessionCoordinator s)
    {
        Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"one session");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"one loader");
        Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"one nav");
        Check(Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"one interaction");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(x=>x.Identity==s.PlayerState.Identity)==1,"one PC");
        Check(l.SpriteOwners.Where(x=>x?.WorldObject!=null).GroupBy(x=>x.WorldObject.Identity).All(g=>g.Count()==1),"unique views");
        Check(n.Player==p.Presentation,"PC binding");
    }

    private static void Check(bool value, string label) { if(!value) throw new InvalidOperationException("M6B validation FAIL: "+label); }
    private static void BeginTracking(){StopTracking();_warnings=0;_errors=0;Application.logMessageReceived+=Track;_tracking=true;}
    private static void StopTracking(){if(!_tracking)return;Application.logMessageReceived-=Track;_tracking=false;}
    private static void Track(string _,string __,LogType t){if(t==LogType.Warning)_warnings++;else if(t is LogType.Error or LogType.Assert or LogType.Exception)_errors++;}
}
