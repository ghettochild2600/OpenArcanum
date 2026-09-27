using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Party;
using Arcanum.Runtime.World;
using Arcanum.Script;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M9BPartyValidation
{
    private const string VirgilSector = "maps/arcanum1-024-fixed/86570436012.sec";
    private const string OverlandSector = "maps/arcanum1-024-fixed/68853695432.sec";
    private const string BatesSector = "maps/bates mansion lev 1/67108865.sec";
    private const string TarantSector = "maps/arcanum1-024-fixed/68853695436.sec";
    private const string BearSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private static readonly ArcanumObjectId Virgil = Parse("G_A09DCD63_7A15_D411_8F1D_00E02920220C");
    private static readonly ArcanumObjectId Bear = Parse("G_9B807B01_A142_4949_80CE_5A085F3BEEB1");
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M9B Phase 1/Audit Authentic Follower Fixture", false, 0)]
    private static void AuditAuthenticFollowerFixture()
    {
        string archive = GameDataLocator.Find("modules/Arcanum.dat");
        if (string.IsNullOrEmpty(archive)) throw new InvalidOperationException("Arcanum.dat was not found.");
        using var vfs = new DatVirtualFileSystem();
        vfs.MountFile(archive);
        foreach (string dataArchive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
        {
            string path = GameDataLocator.Find(dataArchive);
            if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
        }
        int found = 0;
        foreach (string path in vfs.EnumerateFiles("maps/Arcanum1-024-fixed/"))
        {
            if (!path.EndsWith(".mob", StringComparison.OrdinalIgnoreCase)) continue;
            byte[] bytes = vfs.ReadAllBytes(path);
            int offset = 0;
            ObjectInstance instance = ObjectInstanceReader.Read(bytes, ref offset);
            if (instance.Type != ObjectType.Npc || instance.DialogNum != 1324) continue;
            found++;
            long sectorX = (uint)instance.MapX >> 6;
            long sectorY = (uint)instance.MapY >> 6;
            long sectorId = sectorX | (sectorY << 26);
            Debug.Log($"M9B AUTHENTIC FOLLOWER: path={path}; identity={instance.Identity}; "
                      + $"prototype={instance.PrototypeNumber}; dialog={instance.DialogNum}; "
                      + $"tile={instance.TileX},{instance.TileY}; sector=maps/arcanum1-024-fixed/{sectorId}.sec");
        }
        DialogScript dialogue = DialogLocator.Load(vfs, 1324);
        if (dialogue == null) throw new InvalidOperationException("Authentic dialogue 1324 was not found.");
        Debug.Log($"M9B AUTHENTIC DIALOGUE SOURCE: path={DialogLocator.FindPath(vfs, 1324)}; "
                  + $"lines={dialogue.LineNumbers.Count}");
        foreach (int lineNumber in dialogue.LineNumbers)
        {
            dialogue.TryGet(lineNumber, out DialogLine line);
            string source = $"{line.Test} {line.Effect}";
            if (lineNumber is 72 or 514 or 524
                || source.Contains("fo", StringComparison.OrdinalIgnoreCase))
                Debug.Log($"M9B AUTHENTIC DIALOGUE: line={line.Num}; target={line.Target}; "
                          + $"test={line.Test}; effect={line.Effect}; text={line.Text}");
        }
        Debug.Log($"M9B AUTHENTIC FOLLOWER AUDIT: found={found}");
    }

    [MenuItem("OpenArcanum/M9B Phase 1/Run Physical PlayMode Validation", false, 0)]
    private static void RunPhysical()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M9B harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    [MenuItem("OpenArcanum/M9B Phase 2/Run Physical Dialogue Validation", false, 0)]
    private static void RunPhysicalDialogue()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M9B harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no object loader.");
        loader.StartCoroutine(ValidateDialogue(loader));
    }

    private static IEnumerator ValidateDialogue(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(VirgilSector), "authentic Virgil sector loads for dialogue proof");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle, out _);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            Check(session.TryGetObjectState(Virgil, out PersistentObjectState virgilState)
                  && virgilState.Type == ObjectType.Npc && virgilState.PrototypeNumber == 17102
                  && virgilState.DialogNum == 1324, "retail Virgil identity and dialogue source resolve");

            using var vfs = new DatVirtualFileSystem();
            string module = GameDataLocator.Find("modules/Arcanum.dat");
            if (string.IsNullOrEmpty(module)) throw new InvalidOperationException("Arcanum.dat was not found.");
            vfs.MountFile(module);
            foreach (string dataArchive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
            {
                string path = GameDataLocator.Find(dataArchive);
                if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
            }
            DialogScript retail = DialogLocator.Load(vfs, 1324)
                                  ?? throw new InvalidOperationException("Authentic dialogue 1324 was not found.");
            retail.TryGet(72, out DialogLine join);
            Check(join.Effect == "jo 0 74",
                "authentic Virgil join row 72 resolves");
            retail.TryGet(514, out DialogLine leave);
            retail.TryGet(524, out DialogLine leaveResult);
            Check(leave.Effect == "lv" && leaveResult.Effect == "lf31 1",
                "authentic Virgil leave rows 514/524 resolve");

            var joinDialogue = new DialogScript(new SortedDictionary<int, DialogLine>
            {
                [71] = new DialogLine(71, "Join proof", "", 0, "", 0, ""),
                [72] = join,
            });
            session.BindDialogueSource(_ => StartAt(71), _ => joinDialogue);
            Check(session.Dialogue.Start(session.PlayerState.Identity, Virgil) == DialogueStartStatus.Started,
                "production dialogue transaction opens the authentic join row");
            int joinIndex = IndexOfResponse(session.Dialogue.AvailableResponses, 72);
            Check(joinIndex >= 0 && session.Dialogue.SelectResponse(joinIndex) == DialogueChoiceStatus.Completed,
                "authentic jo effect completes through production dialogue");
            Check(session.Party.Members.Count == 1 && session.Party.Members[0].Identity == Virgil,
                "authentic jo effect enrolls Virgil exactly once");
            CheckUnique(loader, Virgil);

            var leaveDialogue = new DialogScript(new SortedDictionary<int, DialogLine>
            {
                [513] = new DialogLine(513, "Leave proof", "", 0, "", 0, ""),
                [514] = leave,
                [524] = leaveResult,
            });
            session.BindDialogueSource(_ => StartAt(513), _ => leaveDialogue);
            Check(session.Dialogue.Start(session.PlayerState.Identity, Virgil) == DialogueStartStatus.Started,
                "production dialogue transaction opens the authentic leave row");
            int leaveIndex = IndexOfResponse(session.Dialogue.AvailableResponses, 514);
            DialogueChoiceStatus leaveStatus = leaveIndex < 0
                ? DialogueChoiceStatus.InvalidChoice
                : session.Dialogue.SelectResponse(leaveIndex);
            Check(leaveStatus is DialogueChoiceStatus.Advanced or DialogueChoiceStatus.Completed,
                "authentic lv effect advances through its retail result node");
            Check(!session.Party.IsMember(Virgil)
                  && session.Campaign.GetLocalFlag(Virgil, (int)Sap.Dialog, 31) == 1,
                "authentic lv effect removes Virgil and commits retail local flag 31");
            Check(session.TryGetLoadedObject(Virgil, out _),
                "disband preserves the same authoritative Virgil world identity");
            CheckUnique(loader, Virgil);
            Check(_errors == 0 && _warnings == 0, "accepted dialogue proof produced no warnings or errors");
            Debug.Log("M9B PHASE 2 PHYSICAL PASS: retail Virgil 1324 rows 72/514/524; production jo/lv dialogue "
                      + "transactions; exactly-once membership; stable identity; authored leave flag. "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(VirgilSector), "authentic Virgil sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle, out ProductionCombatAiDriver aiDriver);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            Check(session.TryGetObjectState(Virgil, out PersistentObjectState virgilState)
                  && virgilState.Type == ObjectType.Npc && virgilState.PrototypeNumber == 17102
                  && virgilState.DialogNum == 1324, "exact retail Virgil fixture resolves");
            Check(session.Party.Join(Virgil).Succeeded, "Virgil joins through authoritative party transaction");
            Check(session.Party.Members.Count == 1 && session.Party.Members[0].Identity == Virgil,
                "ordered party membership contains Virgil exactly once");
            Check(session.TryGetLoadedObject(Virgil, out WorldObject virgilRuntime), "Virgil presentation is loaded");
            Vector2Int virgilStart = virgilRuntime.Tile;
            Vector2Int pcStart = FindDistantWalkable(loader.NavigationMap, virgilStart, 7);
            Check(pcStart.x >= 0 && MoveActor(session, loader.NavigationMap, session.PlayerState.Identity,
                    lifecycle.Presentation, pcStart), "production PC is placed beyond source follow range");
            var movement = new PartyFollowerMovementService(session);
            FollowerMoveResult move = movement.AdvanceOneStep(Virgil, loader.NavigationMap);
            Check(move == FollowerMoveResult.Moved
                  && InteractionRangeRules.Distance(virgilStart, virgilRuntime.Tile) == 1,
                "Virgil follows by one production-navigation step without teleporting");

            Check(session.TryTransitionPlayer(OverlandSector, new Vector2(24, 0), session.PlayerState.ArtId),
                "normal cross-sector traversal succeeds");
            yield return null;
            Refresh(out loader, out lifecycle, out aiDriver);
            Check(session.Party.IsMember(Virgil)
                  && session.States[Virgil].Placement.Sector == OverlandSector
                  && session.TryGetLoadedObject(Virgil, out virgilRuntime),
                "same Virgil identity and one runtime survive cross-sector traversal");
            CheckUnique(loader, Virgil);

            PersistentObjectState entrance = session.States[AreaEntranceResolver.BatesEntranceIdentity];
            Check(session.SetMovementState(session.PlayerState.Identity, entrance.TilePosition,
                    session.PlayerState.ArtId, false),
                "PC reaches authentic Bates entrance");
            Check(session.RequestAreaEntrance(session.PlayerState.Identity, entrance.Identity).Succeeded,
                "existing M7 local-map entrance transaction succeeds");
            yield return null;
            Refresh(out loader, out lifecycle, out aiDriver);
            Check(session.SelectedSector == BatesSector && session.Party.IsMember(Virgil)
                  && session.States[Virgil].Placement.Sector == BatesSector
                  && session.TryGetLoadedObject(Virgil, out _),
                "Virgil accompanies the PC through authentic local-map travel");
            CheckUnique(loader, Virgil);

            Check(MoveActor(session, loader.NavigationMap, session.PlayerState.Identity,
                    lifecycle.Presentation, new Vector2Int(45, 28)), "PC reaches authentic Bates return tile");
            Check(session.RequestCurrentJumpPoint(session.PlayerState.Identity).Succeeded,
                "existing M7 passive return succeeds");
            yield return null;
            Refresh(out loader, out lifecycle, out aiDriver);
            session.Campaign.DiscoverArea(new AreaId(21));
            WorldMapSelectionResult selection = session.WorldMapDestinations.TrySelectWorldArea(new AreaId(21));
            Check(selection.Succeeded
                  && session.RequestWorldMapTravel(session.PlayerState.Identity, selection.Request).Succeeded,
                "existing M7 Bates-to-Tarant world travel succeeds");
            yield return null;
            Refresh(out loader, out lifecycle, out aiDriver);
            Check(session.SelectedSector == TarantSector && session.Party.IsMember(Virgil)
                  && session.States[Virgil].Placement.Sector == TarantSector
                  && session.TryGetLoadedObject(Virgil, out _),
                "Virgil accompanies world-map travel with stable identity");
            CheckUnique(loader, Virgil);

            Check(session.TryTransitionPlayer(BearSector, new Vector2(1, 1), session.PlayerState.ArtId),
                "party reaches authentic Polar Bear Cub sector");
            yield return null;
            Refresh(out loader, out lifecycle, out aiDriver);
            aiDriver.enabled = false;
            WorldObject bearRuntime = null;
            Check(session.TryGetLoadedObject(Virgil, out virgilRuntime)
                  && session.TryGetLoadedObject(Bear, out bearRuntime),
                "Virgil and authentic Polar Bear Cub presentations resolve");
            Vector2Int followerTile = FindAdjacentWalkable(loader.NavigationMap, bearRuntime.Tile);
            Vector2Int playerTile = FindDistantWalkable(loader.NavigationMap, bearRuntime.Tile, 3);
            Check(followerTile.x >= 0 && MoveActor(session, loader.NavigationMap, Virgil, virgilRuntime, followerTile),
                "Virgil is placed in supported attack range");
            Check(playerTile.x >= 0 && MoveActor(session, loader.NavigationMap,
                    session.PlayerState.Identity, lifecycle.Presentation, playerTile),
                "PC remains a separate allied actor");
            Check(session.Combat.TryGetActorSource(Virgil, out CombatActorSource virgilSource),
                "Virgil retains registered combat source across travel");
            Check(session.TryGetPlacement(Virgil, out ObjectPlacement virgilPlacement)
                  && virgilPlacement.Kind == ObjectPlacementKind.World
                  && virgilPlacement.Sector == session.SelectedSector
                  && Mathf.Approximately(virgilPlacement.TilePosition.x, Mathf.Round(virgilPlacement.TilePosition.x))
                  && Mathf.Approximately(virgilPlacement.TilePosition.y, Mathf.Round(virgilPlacement.TilePosition.y)),
                $"Virgil has integral current-sector combat placement ({virgilPlacement})");
            Check(virgilSource.ObjectType == ObjectType.Npc
                  && session.Vitality.GetCurrentHitPoints(Virgil) > 0
                  && session.Vitality.GetCurrentFatigue(Virgil) > 0,
                "Virgil is an available NPC combat actor");
            Check(session.Combat.StartCombat(session.PlayerState.Identity, Bear, CombatMode.TurnBased).Succeeded,
                "turn-based party combat starts");
            Check(session.Combat.Participants.Any(value => value.Identity == Virgil),
                "turn-based roster automatically enrolls Virgil");
            AdvanceTo(session, Virgil);
            session.Combat.SetRandomSource(new MaxRandom());
            CombatAiDecision turnDecision = aiDriver.Controller.DecideAndSubmit(Virgil);
            Check(turnDecision.Target == Bear && turnDecision.Target != session.PlayerState.Identity
                  && turnDecision.Action is CombatAiActionKind.Attack or CombatAiActionKind.Move,
                "Virgil enrolls and autonomously targets the hostile through M9A");
            Check(session.Combat.Participants.Any(value => value.Identity == Virgil)
                  && session.Combat.AreAllies(Virgil, session.PlayerState.Identity),
                "M8 roster projects the follower/PC ally relationship");
            EndCombat(session);

            Check(session.Combat.StartCombat(session.PlayerState.Identity, Bear, CombatMode.RealTime).Succeeded,
                "real-time party combat starts");
            session.Combat.SetRandomSource(new MaxRandom());
            CombatAiDecision realTime = aiDriver.Controller.DecideAndSubmit(Virgil);
            Check(realTime.Scheduled && session.Combat.TryGetRealTimeActorState(Virgil, out _),
                "Virgil uses M9A and M8H READY/BUSY authority in real time");
            EndCombat(session);

            session.Vitality.ApplyFatigueDamage(Virgil, session.Vitality.GetCurrentFatigue(Virgil));
            Check(session.Party.IsMember(Virgil) && !session.Party.CanAccompany(Virgil),
                "unconscious Virgil retains membership but cannot follow or act");
            string save = session.SaveGames.SerializeCurrentSession();
            Check(save.Contains("\"party\"") && !save.Contains("currentParticipant")
                  && session.SaveGames.LoadJson(save).Succeeded,
                "Save V1 persists party authority and normalizes combat/follow transients");
            yield return null;
            Refresh(out loader, out lifecycle, out aiDriver);
            Check(session.Party.Members.Count == 1 && session.Party.Members[0].Identity == Virgil
                  && session.Vitality.IsUnconscious(Virgil),
                "load restores the same unconscious follower identity and membership");

            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                yield return null;
                Check(session.Party.IsMember(Virgil), "graphics rebuild leaves party authority unchanged");
            }
            Check(_errors == 0 && _warnings == 0, "accepted physical run produced no warnings or errors");
            Debug.Log("M9B PHASE 1 PHYSICAL PASS: authentic Virgil join; local follow; sector, area, and world travel; "
                      + "turn-based and real-time M9A combat; ally policy; unconscious retention; Save V1; "
                      + $"graphics rebuild. warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static void Refresh(out WorldObjectSectorLoader loader, out ProductionPlayerLifecycle lifecycle,
        out ProductionCombatAiDriver driver)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        driver = Object.FindFirstObjectByType<ProductionCombatAiDriver>();
        if (lifecycle.Presentation == null) lifecycle.SpawnAndBind();
    }

    private static bool MoveActor(WorldMapSessionCoordinator session, SectorNavigationMap map,
        ArcanumObjectId identity, WorldObject runtime, Vector2Int tile)
    {
        if (runtime == null || map == null || !map.Contains(tile) || !map.IsWalkable(tile)) return false;
        map.Unregister(runtime);
        bool moved = session.SetMovementState(identity, tile, runtime.ArtId, false);
        map.Register(runtime, runtime.SourceFlags);
        if (runtime.Type == ObjectType.Pc) map.SetControlledObject(runtime);
        return moved;
    }

    private static Vector2Int FindAdjacentWalkable(SectorNavigationMap map, Vector2Int origin)
    {
        for (int rotation = 0; rotation < 8; rotation++)
        {
            Vector2Int candidate = origin + IsoProjection.DirDelta[rotation];
            if (map.Contains(candidate) && map.IsWalkable(candidate)) return candidate;
        }
        return new Vector2Int(-1, -1);
    }

    private static Vector2Int FindDistantWalkable(SectorNavigationMap map, Vector2Int origin, int distance)
    {
        for (int radius = distance; radius < 20; radius++)
        for (int y = origin.y - radius; y <= origin.y + radius; y++)
        for (int x = origin.x - radius; x <= origin.x + radius; x++)
        {
            var candidate = new Vector2Int(x, y);
            if (InteractionRangeRules.Distance(candidate, origin) == radius
                && map.Contains(candidate) && map.IsWalkable(candidate)) return candidate;
        }
        return new Vector2Int(-1, -1);
    }

    private static void AdvanceTo(WorldMapSessionCoordinator session, ArcanumObjectId actor)
    {
        for (int guard = 0; guard < 64 && session.Combat.IsActive
                            && session.Combat.CurrentParticipant != actor; guard++)
            session.Combat.EndCurrentTurn(session.Combat.CurrentParticipant);
        Check(session.Combat.CurrentParticipant == actor,
            $"turn authority reaches Virgil (active={session.Combat.IsActive}, current={session.Combat.CurrentParticipant}, "
            + $"participants={string.Join(",", session.Combat.Participants.Select(value => value.Identity.Key))})");
    }

    private static void EndCombat(WorldMapSessionCoordinator session)
    {
        if (!session.Combat.IsActive) return;
        ArcanumObjectId pc = session.PlayerState.Identity;
        foreach (ArcanumObjectId hostile in session.Combat.Participants
                     .Select(value => value.Identity)
                     .Where(identity => session.Combat.AreOpponents(pc, identity)).ToArray())
            session.Combat.RemoveParticipant(hostile);
        if (session.Combat.IsActive) session.Combat.EndCombat(pc);
    }

    private static void CheckUnique(WorldObjectSectorLoader loader, ArcanumObjectId identity)
        => Check(loader.SpriteOwners.Count(owner => owner?.WorldObject?.Identity == identity) == 1,
            $"one presentation exists for {identity}");

    private static int IndexOfResponse(IReadOnlyList<DialogLine> responses, int line)
    {
        for (int index = 0; index < responses.Count; index++)
            if (responses[index].Num == line) return index;
        return -1;
    }

    private static ScriptFile StartAt(int line)
    {
        var action = new ScriptAction { Type = (int)Sat.Dialog };
        action.OpType[0] = (byte)Svt.Number;
        action.OpValue[0] = line;
        var script = new ScriptFile();
        script.Entries.Add(new ScriptCondition
        {
            Type = (int)Sct.True,
            Action = action,
            Els = new ScriptAction { Type = (int)Sat.DoNothing },
        });
        return script;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("M9B physical validation failed: " + label);
        Debug.Log("M9B PASS: " + label);
    }

    private static void Track(string condition, string stack, LogType type)
    {
        if (condition.StartsWith("M9B PASS:", StringComparison.Ordinal)
            || condition.StartsWith("M9B PHASE 1 PHYSICAL PASS:", StringComparison.Ordinal)
            || condition.StartsWith("M9B PHASE 2 PHYSICAL PASS:", StringComparison.Ordinal)) return;
        if (type == LogType.Warning) _warnings++;
        if (type is LogType.Error or LogType.Exception or LogType.Assert) _errors++;
    }

    private static ArcanumObjectId Parse(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid fixture ObjectID " + key);
        return identity;
    }

    private sealed class MaxRandom : ICombatRandom
    {
        public int NextInclusive(int minimum, int maximum) => maximum;
    }
}
