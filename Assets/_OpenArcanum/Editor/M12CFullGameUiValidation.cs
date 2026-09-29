using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Formats.Script;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Crafting;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.Script;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M12CFullGameUiValidation
{
    private const string StartSector = "maps/arcanum1-024-fixed/86570436012.sec";
    private const string TarantMerchantSector = "maps/arcanum1-024-fixed/68853695432.sec";
    private const string TarantArrivalSector = "maps/arcanum1-024-fixed/68853695436.sec";
    private const string BearSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private static readonly ArcanumObjectId Virgil = Parse("G_A09DCD63_7A15_D411_8F1D_00E02920220C");
    private static readonly ArcanumObjectId Merchant = Parse("G_C626B82F_5190_2C40_995A_00BCB987F7A5");
    private static readonly ArcanumObjectId Bear = Parse("G_9B807B01_A142_4949_80CE_5A085F3BEEB1");
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M12C/Run Physical PlayMode Validation #&p", false, 4)]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M12C harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        string validationSlot = null;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Refresh(out loader, out ProductionGameUiPresenter presenter, out ProductionPlayerLifecycle lifecycle);
            GameUiController ui = presenter.Controller;
            ui.Refresh();
            Check(ui.Screen == GameUiScreen.MainMenu && !ui.HasPlayer,
                "no-session composition presents the production main menu");
            Check(ui.BeginNewGame(), "source-backed New Game screen opens");
            ui.SetCreationName("M12C Human");
            ui.SetCreationIdentity(CharacterRace.Human, CharacterGender.Male);
            ui.SetCreationPortrait(1005);
            ui.SetCreationBackground(3);
            ui.AdjustCreationAttribute(CharacterAttribute.Strength, 1);
            ui.AdjustCreationSkill(CharacterSkill.Melee, 1);
            ui.AdjustCreationSpell(SpellCollege.Earth, 1);
            ui.AdjustCreationTechnology(TechnologyDiscipline.Herbology, 1);
            Check(ui.CreationValidation.Succeeded
                  && ui.CreationValidation.RemainingCharacterPoints == 1,
                "character-creation UI projects exact four-spent/one-unspent authority");
            Check(ui.FinalizeNewGame(), "New Game command enters the authentic campaign");
            yield return null;
            Refresh(out loader, out presenter, out lifecycle);
            ui = presenter.Controller;
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            ArcanumObjectId pc = session.PlayerState.Identity;
            Check(session.SelectedSector == StartSector && ui.ProjectHud()?.HitPoints
                  == session.Vitality.GetCurrentHitPoints(pc),
                "main HUD loads authoritative New Game health in START_MAP 1");

            Check(ui.Open(GameUiScreen.Inventory), "inventory opens");
            PersistentObjectState armor = session.States.Values.Single(value =>
                value.PrototypeNumber == 8157 && value.ParentIdentity == pc);
            Check(ui.ProjectInventory().Any(value => value.Identity == armor.Identity && value.IsEquipped),
                "inventory projects authentic equipped starting armor");
            Check(ui.Unequip(WornLocation.Armor) && ui.Equip(armor.Identity)
                  && session.States[armor.Identity].Placement.WornLocation == WornLocation.Armor,
                "one real unequip/equip transaction routes through M3");
            Check(ui.Open(GameUiScreen.Character)
                  && ui.ProjectCharacter().Attributes.Single(value => value.Attribute == CharacterAttribute.Strength)
                     .Effective == 9
                  && ui.ProjectCharacter().Skills.Single(value => value.Skill == CharacterSkill.Melee)
                     .PurchasedPoints == 1,
                "character sheet reflects authoritative Strength and Melee investment");

            Check(ui.Open(GameUiScreen.Magic)
                  && ui.ProjectSpells().Any(value => value.Definition.Id == PhaseOneSpellCatalog.StrengthOfEarth
                                                     && value.Learned),
                "magic screen exposes the authentic learned Earth spell");
            int fatigueBeforeSpell = session.Vitality.GetCurrentFatigue(pc);
            Check(ui.CastSpell(PhaseOneSpellCatalog.StrengthOfEarth, pc)
                  && session.Vitality.GetCurrentFatigue(pc) < fatigueBeforeSpell
                  && ui.ProjectHud().ActiveEffects.Any(value =>
                      value.SpellId == PhaseOneSpellCatalog.StrengthOfEarth),
                "magic UI submits one authentic maintained spell cast");
            ActiveSpellEffect earth = ui.ProjectHud().ActiveEffects.First(value =>
                value.SpellId == PhaseOneSpellCatalog.StrengthOfEarth);
            Check(ui.CancelEffect(earth.Id), "source-supported maintained spell cancellation routes through M10A");

            Check(ui.Open(GameUiScreen.Technology)
                  && ui.ProjectTechnology().Single(value => value.Discipline == TechnologyDiscipline.Herbology)
                     .Learned == TechnologyDegree.Novice,
                "technology screen exposes authentic Herbology Novice state");
            ItemCreationResult salve = session.CreateItem(PhaseOneTechnologyCatalog.HealingSalvePrototype,
                ObjectPlacement.ContainedBy(pc));
            session.Vitality.ApplyHitPointDamage(pc, 10);
            int damagedPc = session.Vitality.GetCurrentHitPoints(pc);
            Check(salve.Succeeded && ui.UseTechnology(salve.State.Identity, pc)
                  && session.Vitality.GetCurrentHitPoints(pc) > damagedPc,
                "technology UI submits one authentic Healing Salve use");

            Check(ui.Open(GameUiScreen.Crafting), "schematics screen opens");
            ItemCreationResult blueprint = session.CreateItem(14095, ObjectPlacement.ContainedBy(pc));
            Check(blueprint.Succeeded
                  && session.Crafting.LearnFoundSchematic(pc, blueprint.State.Identity).Succeeded,
                "authentic written blueprint teaches found schematic 4020");
            GameUiSchematicView cure = ui.ProjectSchematics().First(value => value.Definition.Id.Value == 4020);
            Check(session.CreateItem(10084, ObjectPlacement.ContainedBy(pc)).Succeeded
                  && session.CreateItem(15116, ObjectPlacement.ContainedBy(pc)).Succeeded,
                "authentic found-schematic components enter M3 inventory");
            const int product = 15169;
            int productsBefore = session.States.Values.Count(value => value.PrototypeNumber == product
                                                                      && value.ParentIdentity == pc);
            Check(ui.Craft(cure.Definition.Id)
                  && session.States.Values.Count(value => value.PrototypeNumber == product
                                                         && value.ParentIdentity == pc) > productsBefore,
                "crafting UI submits one authentic found-schematic transaction");

            session.Campaign.SetPcQuestState(1005, (int)QuestState.Mentioned);
            Check(ui.Open(GameUiScreen.Journal)
                  && ui.ProjectJournal().Any(value => value.QuestId == 1005
                                                      && value.State == QuestState.Mentioned),
                "journal projects an authentic source quest entry");

            using var vfs = new DatVirtualFileSystem();
            string module = GameDataLocator.Find("modules/Arcanum.dat");
            Check(!string.IsNullOrEmpty(module), "retail Arcanum dialogue archive is mounted");
            vfs.MountFile(module);
            foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
            {
                string path = GameDataLocator.Find(archive);
                if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
            }
            DialogScript retail = DialogLocator.Load(vfs, 1324);
            if (retail == null || !retail.TryGet(72, out DialogLine join))
                throw new InvalidOperationException("M12C validation FAIL: authentic Virgil join response 72 resolves");
            Check(join.Effect == "jo 0 74", "authentic Virgil join response 72 resolves");
            var boundedDialogue = new DialogScript(new SortedDictionary<int, DialogLine>
            {
                [71] = new DialogLine(71, "Will you join me?", "", 0, "", 0, ""),
                [72] = join,
            });
            session.BindDialogueSource(_ => StartAt(71), _ => boundedDialogue);
            Check(session.Dialogue.Start(pc, Virgil) == DialogueStartStatus.Started,
                "bounded authentic Virgil dialogue opens through M5");
            ui.Refresh();
            int joinChoice = IndexOfLine(ui.Dialogue, 72);
            Check(ui.Screen == GameUiScreen.Dialogue && joinChoice >= 0
                  && ui.SelectDialogueResponse(joinChoice),
                "dialogue UI displays and executes the authentic Virgil join response");

            Check(session.Party.IsMember(Virgil) && ui.Open(GameUiScreen.Party)
                  && ui.ProjectParty().Single().Identity == Virgil,
                "party UI reflects authentic Virgil membership");
            Check(ui.DismissFollower(Virgil) && session.Party.Count == 0,
                "source-supported follower dismissal routes through M9B");

            ui.OpenSaveLoad(SaveLoadPanelMode.Save);
            validationSlot = ui.SaveLoad.SuggestedSlotId;
            ui.SaveLoad.RequestSave();
            Check(ui.SaveLoad.SelectedSlotId == validationSlot
                  && string.IsNullOrEmpty(ui.SaveLoad.ErrorMessage),
                "integrated save UI writes one bounded manual slot");
            session.Campaign.SetFlag(987, 1);
            ui.SaveLoad.SetMode(SaveLoadPanelMode.Load);
            Check(ui.SaveLoad.SelectSlot(validationSlot), "validation slot remains selectable");
            ui.RequestLoad();
            yield return null;
            Refresh(out loader, out presenter, out lifecycle);
            ui = presenter.Controller;
            Check(session.Campaign.GetFlag(987) == 0 && ui.ProjectHud() != null,
                "load UI restores authority and HUD reconstructs without stale modal state");
            ui.OpenSaveLoad(SaveLoadPanelMode.Load);
            Check(ui.SaveLoad.SelectSlot(validationSlot), "created validation slot is found for cleanup");
            ui.SaveLoad.RequestDelete();
            ui.SaveLoad.Confirm();
            validationSlot = null;

            Check(session.SelectSector(TarantMerchantSector), "authentic Tarant merchant sector loads");
            yield return null;
            Refresh(out loader, out presenter, out lifecycle);
            ui = presenter.Controller;
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "PC rebinds in Tarant");
            Check(session.Economy.RestockNow(Merchant, out string restockFailure),
                "authentic merchant restocks: " + restockFailure);
            session.AddGold(pc, 1000000);
            Check(ui.BeginMerchant(Merchant) && ui.ProjectMerchantWares().Count > 0,
                "barter UI projects authentic merchant inventory and source prices");
            GameUiMerchantItemView ware = ui.ProjectMerchantWares().First(value => value.Failure == EconomyFailure.None);
            int playerGold = session.GetGold(pc);
            Check(ui.Trade(EconomyTransactionDirection.BuyFromMerchant, ware.Item.Identity)
                  && session.GetGold(pc) == playerGold - ware.UnitPrice,
                "merchant UI submits one exact-price authoritative purchase");

            Check(session.SetMovementState(pc, new Vector2(24, 0), session.PlayerState.ArtId, false),
                "PC reaches authentic Bates return tile");
            session.Campaign.DiscoverArea(new AreaId(21));
            Check(ui.Open(GameUiScreen.Map)
                  && ui.ProjectDestinations(out WorldMapDestinationFailure destinationFailure)
                     .Any(value => value.AreaId == new AreaId(21))
                  && destinationFailure == WorldMapDestinationFailure.None,
                "map UI projects known Tarant from campaign authority");
            Check(ui.TravelTo(new AreaId(21)) && session.SelectedSector == TarantArrivalSector,
                "map UI submits the authentic four-sector Bates-to-Tarant travel");
            yield return null;
            Refresh(out loader, out presenter, out lifecycle);
            ui = presenter.Controller;

            Check(session.SelectSector(BearSector), "authentic Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out presenter, out lifecycle);
            ui = presenter.Controller;
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "PC binds for combat UI proof");
            Check(session.TryGetLoadedObject(Bear, out WorldObject bearRuntime), "authentic bear target loads");
            MoveActor(session, loader, pc, lifecycle.Presentation, FindMeleeTile(loader.NavigationMap, bearRuntime.Tile));
            CombatStateService combat = session.Combat;
            Check(combat.StartCombat(pc, Bear, CombatMode.TurnBased).Succeeded,
                "turn-based combat starts");
            while (combat.CurrentParticipant != pc)
                Check(combat.EndCurrentTurn(combat.CurrentParticipant).Succeeded, "turn reaches PC");
            ui.Combat.Refresh();
            Check(ui.Combat.SelectTarget(Bear), "full HUD combat target uses stable identity");
            ui.Combat.SetAttackMode(CombatAttackMode.BasicMelee);
            combat.SetRandomSource(new SequenceRandom(1, 100, 4, 4));
            int ap = combat.CurrentActionPoints;
            Check(ui.Combat.SubmitAttack() == CombatFailure.None && combat.CurrentActionPoints < ap,
                "full HUD submits one authoritative turn-based attack and projects AP");
            EndCombat(combat, pc);

            Check(combat.StartCombat(pc, Bear, CombatMode.RealTime).Succeeded,
                "real-time combat starts");
            ui.Combat.Refresh();
            Check(ui.Combat.SelectTarget(Bear), "real-time HUD reselects authentic target");
            combat.SetRandomSource(new SequenceRandom(100, 100));
            Check(ui.Combat.SubmitAttack() == CombatFailure.None,
                "full HUD submits one real-time action");
            Check(combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState pending)
                  && pending.HasPendingAction,
                "full HUD submits one real-time action and projects BUSY");
            int throughReady = checked((int)(pending.PendingAction.ReadyAtMilliseconds
                                             - combat.ElapsedCombatTimeMilliseconds));
            Check(combat.AdvanceRealTime(throughReady).Succeeded, "real-time action resolves to READY");
            ui.Combat.Refresh();
            Check(ui.Combat.IsRealTimeReady, "HUD projects real-time READY after resolution");
            EndCombat(combat, pc);

            ArcanumObjectId identity = session.PlayerState.Identity;
            ui.Open(GameUiScreen.Character);
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                ui.RebuildPresentation();
                Check(session.PlayerState.Identity == identity && ui.Screen == GameUiScreen.Character,
                    $"{mode} rebuild preserves authority and reconstructs current UI");
            }
            ui.Close();
            Check(Object.FindObjectsByType<ProductionGameUiPresenter>(FindObjectsInactive.Include,
                      FindObjectsSortMode.None).Length == 1
                  && Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include,
                      FindObjectsSortMode.None).Count(value => value.Identity == identity) == 1,
                "major-screen lifecycle retains one UI presenter and one PC presentation");

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M12C PHYSICAL VALIDATION PASS: NewGame=source-backed; HUD=HP/fatigue/item/readiness; "
                      + "inventory=unequip+equip; character=attributes+skills; combat=turn-based+real-time; "
                      + "magic=Earth-cast+cancel; technology=Herbology+HealingSalve; crafting=schematic-4020; "
                      + "merchant=authentic-priced-purchase; journal=quest-1005; map=Bates->Tarant; "
                      + "party=Virgil+dismiss; dialogue=Virgil-join; saveLoad=slot-round-trip+UI-rebuild; "
                      + "presentation=Original->Enhanced->Original; duplicatePresentation=false; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            session.Economy.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            if (validationSlot != null)
            {
                SaveLoadPanelController cleanup = Object.FindFirstObjectByType<ProductionGameUiPresenter>()?
                    .Controller?.SaveLoad;
                if (cleanup != null)
                {
                    cleanup.Open(SaveLoadPanelMode.Load);
                    if (cleanup.SelectSlot(validationSlot)) { cleanup.RequestDelete(); cleanup.Confirm(); }
                }
            }
            _running = false;
        }
    }

    private static int IndexOfLine(ProductionDialogueSession dialogue, int line)
    {
        for (int index = 0; index < dialogue.AvailableResponses.Count; index++)
            if (dialogue.AvailableResponses[index].Num == line) return index;
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

    private static Vector2Int FindMeleeTile(SectorNavigationMap map, Vector2Int target)
    {
        foreach (Vector2Int delta in IsoProjection.DirDelta)
        {
            Vector2Int tile = target + delta;
            if (map.IsWalkable(tile)) return tile;
        }
        throw new InvalidOperationException("M12C validation FAIL: no melee tile exists.");
    }

    private static void MoveActor(WorldMapSessionCoordinator session, WorldObjectSectorLoader loader,
        ArcanumObjectId identity, WorldObject runtime, Vector2Int tile)
    {
        loader.NavigationMap.Unregister(runtime);
        Check(session.SetMovementState(identity, tile, runtime.ArtId, false),
            "combat fixture movement is authoritative");
        loader.NavigationMap.Register(runtime, runtime.SourceFlags);
        loader.NavigationMap.SetControlledObject(runtime);
    }

    private static void EndCombat(CombatStateService combat, ArcanumObjectId pc)
    {
        foreach (ArcanumObjectId hostile in combat.Participants.Where(value => value.Identity != pc)
                     .Select(value => value.Identity).ToArray())
            Check(combat.RemoveParticipant(hostile).Succeeded, "hostile exits combat cleanly");
        Check(combat.EndCombat(pc).Succeeded, "combat ends cleanly");
    }

    private static void Refresh(out WorldObjectSectorLoader loader,
        out ProductionGameUiPresenter presenter, out ProductionPlayerLifecycle lifecycle)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        presenter = Object.FindFirstObjectByType<ProductionGameUiPresenter>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && presenter != null && presenter.Controller != null && lifecycle != null,
            "production TestTerrain composition contains one full UI, loader, and PC lifecycle");
    }

    private static ArcanumObjectId Parse(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid validation ObjectID: " + key);
        return identity;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M12C validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }

    private sealed class SequenceRandom : ICombatRandom
    {
        private readonly System.Collections.Generic.Queue<int> _values;
        internal SequenceRandom(params int[] values)
            => _values = new System.Collections.Generic.Queue<int>(values);
        public int NextInclusive(int minimum, int maximum)
        {
            int value = _values.Count > 0 ? _values.Dequeue() : minimum;
            Check(value >= minimum && value <= maximum, "deterministic combat RNG stays in range");
            return value;
        }
    }
}
