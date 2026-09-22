using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M8ECombatValidation
{
    private const string EquipmentSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string CombatSector = "maps/arcanum1-024-fixed/59726889458.sec";
    private static readonly ArcanumObjectId Bow = Parse("G_1575DBCA_4990_C243_8184_524D51F7D533");
    private static readonly ArcanumObjectId Arrows = Parse("G_FBFA4631_D97D_D740_9636_F131B2FD9F7B");
    private static readonly ArcanumObjectId Skeleton = Parse("G_5ADE34A3_86FB_7A40_98C0_DDEB6335E848");
    private static readonly ArcanumObjectId Gold = Parse("G_6413F64C_29FD_4A44_8E2C_E9A414888116");
    private static readonly ArcanumObjectId Sword = Parse("G_2EB46E07_6D15_AD4C_87A1_B8543AA54F91");
    private static readonly ArcanumObjectId Missing = ArcanumObjectId.CreateGuid(
        Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));

    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M8E/Run Physical PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M8E harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
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
            Check(session.SelectSector(EquipmentSector), "authentic bow/ammo sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            ArcanumObjectId pc = session.PlayerState.Identity;
            PersistentObjectState bow = Require(session, Bow, ObjectType.Weapon, 6055);
            PersistentObjectState arrows = Require(session, Arrows, ObjectType.Ammo, 7058);
            MoveOwnedItem(session, bow, pc);
            MoveOwnedItem(session, arrows, pc);
            Check(session.EquipItem(pc, Bow, WornLocation.Weapon).Succeeded,
                "M3C equips the authentic bow on the production PC");

            Check(session.SelectSector(CombatSector), "authentic Greater Skeleton sector loads");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC rebinds");
            WorldObject pcRuntime = lifecycle.Presentation;
            PersistentObjectState corpse = Require(session, Skeleton, ObjectType.Npc, 28460);
            PersistentObjectState gold = Require(session, Gold, ObjectType.Gold, 9076);
            PersistentObjectState sword = Require(session, Sword, ObjectType.Weapon, 6050);
            Check(session.Combat.TryGetActorSource(Skeleton, out CombatActorSource source)
                  && source.DyingScriptNum == 0 && source.ExperienceWorth == 440,
                "Greater Skeleton carries source SAP_DYING=0 and XP worth 440");
            Check(corpse.Off, "authentic Greater Skeleton begins behind its source encounter gate");
            typeof(PersistentObjectState).GetProperty(nameof(PersistentObjectState.Off))?.SetValue(corpse, false);
            Check(!corpse.Off, "validation activates only the source encounter gate, not death state");
            session.ClearSelectedSector();
            yield return null;
            Check(session.SelectSector(CombatSector), "activated Greater Skeleton sector reloads");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC rebinds");
            pcRuntime = lifecycle.Presentation;
            Check(session.TryGetLoadedObject(Skeleton, out WorldObject skeletonRuntime),
                "Greater Skeleton has one production presentation");
            Check(gold.StackQuantity == 89 && gold.Placement == ObjectPlacement.ContainedBy(Skeleton),
                "authentic 89-gold object is corpse-owned");
            Check(sword.Placement == ObjectPlacement.EquippedBy(Skeleton, WornLocation.Weapon),
                "authentic sword begins equipped by the Greater Skeleton");

            Vector2Int firingTile = FindClearRangedTile(loader.NavigationMap, skeletonRuntime.Tile, 3, 8);
            MoveActor(session, loader, pc, pcRuntime, firingTile);
            int xpBefore = session.Progression.Get(pc).Experience;
            int levelBefore = session.Progression.Get(pc).Level;
            Relationship[] relationships = Relationships(session, Skeleton);
            int worldObjects = Count<WorldObject>();
            int deathTransitions = 0;
            session.Vitality.Changed += change =>
            {
                if (change.Identity == Skeleton && change.PreviousHitPoints > 0
                    && change.CurrentHitPoints <= 0) deathTransitions++;
            };

            Check(session.DeathConsequences.Process(pc, Skeleton).Failure
                  == DeathConsequenceFailure.VictimNotDead,
                "living target rejects consequence processing without XP");
            Check(session.DeathConsequences.Process(pc, Missing).Failure
                  == DeathConsequenceFailure.VictimNotFound,
                "invalid victim rejects without mutation");
            Check(session.DeathConsequences.LootItem(Skeleton, pc, Gold).Failure
                  == CorpseLootFailure.CorpseNotDead,
                "living actor cannot be opened as a corpse");

            Check(session.Combat.StartCombat(pc, Skeleton).Succeeded, "production StartCombat succeeds");
            AdvanceToPc(session, pc);
            int shots = 0;
            while (!session.Vitality.IsDead(Skeleton))
            {
                CombatHitChance chance = session.Combat.GetBasicRangedHitChance(pc, Skeleton,
                    bow.WeaponData, InteractionRangeRules.Distance(pcRuntime.Tile, skeletonRuntime.Tile));
                session.Combat.SetRandomSource(chance.DodgeChance > 0
                    ? new SequenceRandom(1, 100, 10, 5)
                    : new SequenceRandom(1, 10, 5));
                CombatAttackResult attack = session.Combat.Attack(pc, Skeleton, CombatAttackMode.BasicRanged);
                Check(attack.Succeeded && attack.Hit && attack.WeaponIdentity == Bow
                      && attack.AmmoIdentity == Arrows && attack.MitigatedHitPointDamage > 0,
                    "deterministic attack uses the production M8C/M4B path");
                shots++;
                if (!session.Vitality.IsDead(Skeleton))
                {
                    Check(session.Combat.EndCurrentTurn(pc).Succeeded, "PC turn ends after a nonlethal hit");
                    AdvanceToPc(session, pc);
                }
            }

            int xpAfterDeath = session.Progression.Get(pc).Experience;
            Check(deathTransitions == 1 && corpse.DeathConsequencesProcessed,
                "M8D death and M8E consequences commit exactly once");
            Check(xpAfterDeath == xpBefore + 88 && session.Progression.Get(pc).Level == levelBefore,
                "CharacterProgressionService awards exactly 88 XP without direct level mutation");
            Check(skeletonRuntime.Identity == Skeleton && skeletonRuntime.IsDead
                  && CountIdentity(Skeleton) == 1 && Count<WorldObject>() == worldObjects,
                "corpse retains the original NPC identity without replacement");
            Check(session.Combat.Participants.All(value => value.Identity != Skeleton),
                "dead Greater Skeleton leaves active combat participation");
            Check(Relationships(session, Skeleton).SequenceEqual(relationships),
                "death preserves corpse inventory and equipment relationships");
            Check(session.DeathConsequences.Process(pc, Skeleton).Failure
                  == DeathConsequenceFailure.AlreadyProcessed
                  && session.Progression.Get(pc).Experience == xpAfterDeath,
                "repeat death processing cannot replay XP or script consequences");
            int ap = session.Combat.CurrentActionPoints;
            int? arrowQuantity = arrows.StackQuantity;
            CombatAttackResult postDeathAttack = session.Combat.Attack(pc, Skeleton, CombatAttackMode.BasicRanged);
            Check(postDeathAttack.Failure == CombatFailure.ParticipantNotRegistered
                  && session.Combat.CurrentActionPoints == ap && arrows.StackQuantity == arrowQuantity
                  && session.Progression.Get(pc).Experience == xpAfterDeath
                  && Relationships(session, Skeleton).SequenceEqual(relationships),
                "post-death attack fails without AP, ammo, XP, or corpse mutation");
            Check(session.Combat.EndCombat(pc).Succeeded && session.Combat.CanUseOrdinaryMovement(pc),
                "explicit EndCombat remains required and restores ordinary gameplay");

            CorpseLootResult goldLoot = session.DeathConsequences.LootItem(Skeleton, pc, Gold);
            CorpseLootResult swordLoot = session.DeathConsequences.LootItem(Skeleton, pc, Sword);
            Check(goldLoot.Succeeded && goldLoot.Transfer.ItemIdentity == Gold
                  && gold.Placement == ObjectPlacement.ContainedBy(pc) && gold.StackQuantity == 89,
                "authentic gold ObjectID transfers atomically from corpse to PC");
            Check(swordLoot.Succeeded && swordLoot.Transfer.ItemIdentity == Sword
                  && sword.Placement == ObjectPlacement.ContainedBy(pc),
                "equipped sword clears its worn slot and transfers without cloning");
            Check(session.DeathConsequences.LootItem(Skeleton, pc, Gold).Failure
                  == CorpseLootFailure.ItemNotOwnedByCorpse,
                "repeat corpse loot fails without duplicating the item");
            Check(CountIdentity(Gold) == 0 && CountIdentity(Sword) == 0,
                "contained loot has no duplicate world presentation");

            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.TryGetLoadedObject(Skeleton, out skeletonRuntime)
                      && skeletonRuntime.Identity == Skeleton && skeletonRuntime.IsDead
                      && corpse.DeathConsequencesProcessed && session.Progression.Get(pc).Experience == xpAfterDeath
                      && gold.Placement == ObjectPlacement.ContainedBy(pc)
                      && sword.Placement == ObjectPlacement.ContainedBy(pc) && CountIdentity(Skeleton) == 1,
                    $"{mode} rebuild preserves corpse, reward, and loot state");
            }

            string json = session.SaveGames.SerializeCurrentSession();
            Check(json.Contains("\"version\": 1"), "post-loot save remains format V1");
            session.ClearSelectedSector();
            yield return null;
            Check(session.DeathConsequences.LootItem(Skeleton, pc, Gold).Failure
                  == CorpseLootFailure.CorpseUnavailable,
                "unloaded corpse is inaccessible without authoritative mutation");
            Check(session.SelectSector(CombatSector), "corpse sector reloads");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(session.Vitality.IsDead(Skeleton) && session.TryGetLoadedObject(Skeleton, out skeletonRuntime)
                  && skeletonRuntime.Identity == Skeleton && corpse.DeathConsequencesProcessed
                  && gold.Placement == ObjectPlacement.ContainedBy(pc)
                  && sword.Placement == ObjectPlacement.ContainedBy(pc)
                  && session.Progression.Get(pc).Experience == xpAfterDeath,
                "sector reload preserves corpse, loot, and exact-once reward");

            Check(session.SaveGames.LoadJson(json).Succeeded, "post-death V1 load succeeds");
            yield return null;
            Refresh(out loader, out lifecycle);
            corpse = Require(session, Skeleton, ObjectType.Npc, 28460);
            gold = Require(session, Gold, ObjectType.Gold, 9076);
            sword = Require(session, Sword, ObjectType.Weapon, 6050);
            Check(session.Vitality.IsDead(Skeleton) && corpse.DeathConsequencesProcessed
                  && session.Progression.Get(pc).Experience == xpAfterDeath
                  && gold.Placement == ObjectPlacement.ContainedBy(pc)
                  && sword.Placement == ObjectPlacement.ContainedBy(pc)
                  && session.DeathConsequences.Process(pc, Skeleton).Failure
                     == DeathConsequenceFailure.AlreadyProcessed,
                "V1 load restores death and loot without replaying consequences");

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log($"M8E PLAYMODE VALIDATION PASS: sector={CombatSector}; skeleton={Skeleton}; "
                      + $"lethalShots={shots}; deathTransitions=1; xp={xpBefore}->{xpAfterDeath}; award=88; "
                      + "dyingScript=0; killer=productionPC; corpse=sameIdentity; gold=89->PC; "
                      + "equippedSword=PC+slotCleared; repeatProcessing=blocked; repeatLoot=blocked; "
                      + "graphics=Original->Enhanced->Original; unloadReload=preserved; saveV1=noReplay; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static void AdvanceToPc(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        int remaining = session.Combat.Participants.Count;
        while (session.Combat.CurrentParticipant != pc && remaining-- > 0)
            Check(session.Combat.EndCurrentTurn(session.Combat.CurrentParticipant).Succeeded,
                "source-order participant advances to the production PC");
        Check(session.Combat.CurrentParticipant == pc, "production PC receives a turn");
    }

    private static void MoveOwnedItem(WorldMapSessionCoordinator session, PersistentObjectState item,
        ArcanumObjectId owner)
    {
        if (item.Placement.Kind == ObjectPlacementKind.Equipped)
            Check(session.UnequipItem(item.ParentIdentity, item.Placement.WornLocation).Succeeded,
                "source equipped item unequips before transfer");
        if (item.Placement != ObjectPlacement.ContainedBy(owner))
            Check(session.TransferItem(item.Identity, item.Placement, ObjectPlacement.ContainedBy(owner)).Succeeded,
                "authentic item transfers through M3 authority");
    }

    private static void MoveActor(WorldMapSessionCoordinator session, WorldObjectSectorLoader loader,
        ArcanumObjectId identity, WorldObject runtime, Vector2Int tile)
    {
        loader.NavigationMap.Unregister(runtime);
        Check(session.SetMovementState(identity, tile, runtime.ArtId, false),
            "authoritative movement accepts the validation firing tile");
        loader.NavigationMap.Register(runtime, runtime.SourceFlags);
        loader.NavigationMap.SetControlledObject(runtime);
    }

    private static Vector2Int FindClearRangedTile(SectorNavigationMap map, Vector2Int target,
        int minimumDistance, int maximumDistance)
    {
        for (int distance = minimumDistance; distance <= maximumDistance; distance++)
        for (int y = Math.Max(0, target.y - distance); y <= Math.Min(63, target.y + distance); y++)
        for (int x = Math.Max(0, target.x - distance); x <= Math.Min(63, target.x + distance); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (InteractionRangeRules.Distance(candidate, target) == distance && map.IsWalkable(candidate)
                && map.HasProjectileLineOfFire(candidate, target)) return candidate;
        }
        throw new InvalidOperationException("M8E validation FAIL: no clear in-range source tile.");
    }

    private static PersistentObjectState Require(WorldMapSessionCoordinator session,
        ArcanumObjectId identity, ObjectType type, int prototype)
    {
        Check(session.States.TryGetValue(identity, out PersistentObjectState state),
            $"exact ObjectID {identity} resolves; matching {type}/{prototype}: "
            + string.Join(", ", session.States.Values
                .Where(value => value.Type == type && value.PrototypeNumber == prototype)
                .Select(value => value.Identity)));
        Check(state.Type == type && state.PrototypeNumber == prototype,
            $"{identity} retains expected type/prototype");
        return state;
    }

    private static Relationship[] Relationships(WorldMapSessionCoordinator session, ArcanumObjectId parent)
        => session.States.Values.Where(value => value.ParentIdentity == parent)
            .Select(value => new Relationship(value.Identity, value.Placement))
            .OrderBy(value => value.Identity.ToString(), StringComparer.Ordinal).ToArray();

    private readonly struct Relationship : IEquatable<Relationship>
    {
        public readonly ArcanumObjectId Identity;
        private readonly ObjectPlacement _placement;
        public Relationship(ArcanumObjectId identity, ObjectPlacement placement)
        {
            Identity = identity;
            _placement = placement;
        }
        public bool Equals(Relationship other) => Identity == other.Identity && _placement == other._placement;
    }

    private sealed class SequenceRandom : ICombatRandom
    {
        private readonly Queue<int> _values;
        public SequenceRandom(params int[] values) => _values = new Queue<int>(values);
        public int NextInclusive(int minimum, int maximum)
        {
            Check(_values.Count > 0, "combat requests only expected deterministic RNG samples");
            int value = _values.Dequeue();
            Check(value >= minimum && value <= maximum, "deterministic sample is in source range");
            return value;
        }
    }

    private static void Refresh(out WorldObjectSectorLoader loader, out ProductionPlayerLifecycle lifecycle)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && lifecycle != null, "production TestTerrain composition remains available");
    }

    private static int CountIdentity(ArcanumObjectId identity)
        => Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(value => value.Identity == identity);

    private static int Count<T>() where T : Object
        => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

    private static ArcanumObjectId Parse(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid validation ObjectID: " + key);
        return identity;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M8E validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
