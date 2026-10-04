using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Party;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.Script;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M13AOpeningCompatibility")]
    public sealed class M13AOpeningCompatibilityTests
    {
        private const string CrashSector = "maps/arcanum1-024-fixed/86570436012.sec";
        private const int VirgilDialogueNumber = 1324;
        private static readonly ArcanumObjectId Virgil = Parse(
            "G_A09DCD63_7A15_D411_8F1D_00E02920220C");

        private static readonly (string key, int inventoryCount)[] SourceCorpses =
        {
            ("G_DD753C8F_B655_D411_8F1D_00A0CC6511C6", 1),
            ("G_39EBE998_F113_D411_8F1D_00E02920220C", 2),
            ("G_B82C1DC3_080B_D411_8F1D_00E02920220C", 2),
        };

        private static readonly string[] HeartbeatInitializedCorpses =
        {
            "G_729DB2F6_503E_B84E_B5E3_0DADC947D188",
            "G_D3A6E04A_78E5_FE41_AE7A_555C6CBC77B7",
            "G_91A6CACB_B653_DD4A_A763_93911F389C9D",
            "G_F2C1B8B6_AA9F_AE4E_BDF1_92C1DAB6B64B",
            "G_0BBB1D21_0997_2A49_B8CD_0D0099667DED",
            "G_81B28CDB_13AA_0048_8326_F2C432DFE976",
            "G_9C04A958_C5F2_784D_AA9C_5E76A603C764",
            "G_2798503E_F21C_724D_B1A7_2E968EB88EAA",
            "G_1BAD7FCC_C094_5148_875F_B72C51004AF2",
            "G_75FFCBCC_04BF_234D_B380_6C7B60E4ECD3",
        };

        private static DatVirtualFileSystem _vfs;
        private static ScriptDatabase _scripts;
        private static DialogScript _virgilDialogue;

        [OneTimeSetUp]
        public void LoadAuthenticResources()
        {
            string module = GameDataLocator.Find("modules/Arcanum.dat");
            if (string.IsNullOrEmpty(module))
                Assert.Ignore("The local clean Arcanum module archive is unavailable.");
            _vfs = new DatVirtualFileSystem();
            _vfs.MountFile(module);
            foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
            {
                string path = GameDataLocator.Find(archive);
                if (!string.IsNullOrEmpty(path)) _vfs.MountFile(path);
            }
            _scripts = ScriptDatabase.Load(_vfs);
            _virgilDialogue = DialogLocator.Load(_vfs, VirgilDialogueNumber);
            Assert.That(_scripts.Get(VirgilDialogueNumber), Is.Not.Null);
            Assert.That(_virgilDialogue, Is.Not.Null);
        }

        [OneTimeTearDown]
        public void ReleaseAuthenticResources() => _vfs?.Dispose();

        [Test]
        public void AuthenticVirgilEntryExecutesReachableDialogAndLeavesDormantFloatLineDormant()
        {
            ScriptFile script = _scripts.Get(VirgilDialogueNumber);
            Assert.That((Sat)script.Entries[16].Action.Type, Is.EqualTo(Sat.FloatLine),
                "retail line 16 is the dormant opcode that the former eager scan rejected");

            GameObject root = new(nameof(AuthenticVirgilEntryExecutesReachableDialogAndLeavesDormantFloatLineDormant));
            try
            {
                WorldMapSessionCoordinator session = root.AddComponent<WorldMapSessionCoordinator>();
                WorldObjectSectorLoader loader = root.AddComponent<WorldObjectSectorLoader>();
                loader.BindSessionAuthority();
                Assert.That(session.SelectSector(CrashSector), Is.True);
                PersistentPlayerState pc = session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                    CrashSector, new Vector2(1, 1), 0x28100000u);
                session.BindPlayer(CrashSector, pc, Runtime(root, "Player", ObjectType.Pc, pc.Identity));
                Assert.That(session.TryGetObjectState(Virgil, out PersistentObjectState virgil), Is.True);
                Assert.That(virgil.DialogNum, Is.EqualTo(VirgilDialogueNumber));
                Assert.That(session.TryGetLoadedObject(Virgil, out _), Is.True);
                Assert.That(session.TryFindContainedItemByName(Virgil, 2804,
                    out PersistentObjectState amulet), Is.True);
                session.BindDialogueSource(_scripts.Get,
                    number => number == VirgilDialogueNumber ? _virgilDialogue : null);

                Assert.That(session.Dialogue.Start(pc.Identity, Virgil), Is.EqualTo(DialogueStartStatus.Started));
                Assert.That(session.Dialogue.CurrentLine, Is.EqualTo(1));
                Assert.That(session.Dialogue.Phase, Is.EqualTo(DialogueSessionPhase.AwaitingPlayerChoice));
                Assert.That(amulet.ParentIdentity, Is.EqualTo(pc.Identity));
                Assert.That(session.Campaign.GetFlag(2004), Is.EqualTo(1));
                Assert.That(session.Campaign.GetPcQuestState(1010), Is.EqualTo(2));
                Assert.That(session.Campaign.IsRumorKnown(2013), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReachedUnsupportedDialogueOpcodeStillFailsClosedBeforeMutation()
        {
            GameObject root = new(nameof(ReachedUnsupportedDialogueOpcodeStillFailsClosedBeforeMutation));
            try
            {
                WorldMapSessionCoordinator session = root.AddComponent<WorldMapSessionCoordinator>();
                session.RegisterObjectOwner(new Owner(session));
                Assert.That(session.SelectSector(CrashSector), Is.True);
                PersistentPlayerState pc = session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                    CrashSector, Vector2.one, 0x28100000u);
                session.BindPlayer(CrashSector, pc, Runtime(root, "Player", ObjectType.Pc, pc.Identity));
                var source = new ObjectInstance(ObjectType.Npc, 17102, Location(2, 1),
                    0x28100000u, 0, 0, oid: GuidBytes(Virgil.Key), dialogNum: VirgilDialogueNumber);
                PersistentObjectState npc = session.GetOrCreate(source, CrashSector,
                    source.CurrentArtId.Value, false, false);
                int[] stats = Stats();
                session.Characters.GetOrCreateSourceCharacter(Virgil, ObjectType.Npc, 17102, stats, null);
                session.Progression.GetOrCreateSourceCharacter(Virgil, ObjectType.Npc, 17102,
                    CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
                session.DerivedStats.GetOrCreateSourceCharacter(Virgil, ObjectType.Npc, 17102,
                    CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50, 0, 0));
                session.Vitality.GetOrCreateSourceCharacter(Virgil, ObjectType.Npc, 17102,
                    CharacterVitalitySource.Resolve(stats, null, null, 0, null, 0, null, 0,
                        null, 0, null, 0, null, 0));
                session.Bind(CrashSector, npc, Runtime(root, "Npc", ObjectType.Npc, Virgil));
                var unsupported = new ScriptFile();
                unsupported.Entries.Add(new ScriptCondition
                {
                    Type = (int)Sct.True,
                    Action = new ScriptAction { Type = (int)Sat.Attack },
                    Els = new ScriptAction { Type = (int)Sat.DoNothing },
                });
                session.BindDialogueSource(_ => unsupported, _ => _virgilDialogue);

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                    "OpenArcanum dialogue compatibility:.*UnsupportedScriptOpcode"));
                Assert.That(session.Dialogue.Start(pc.Identity, Virgil),
                    Is.EqualTo(DialogueStartStatus.UnsupportedScript));
                Assert.That(session.Campaign.GetLocalFlag(Virgil, (int)Sap.Dialog, 1), Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void VirgilRemainingWaGateAndMmEffectsUseAuthoritativeSourceState()
        {
            Assert.That(_virgilDialogue.TryGet(71, out DialogLine firstMarker), Is.True);
            Assert.That(firstMarker.Effect.Replace(" ", string.Empty), Is.EqualTo("mm1,mm2"));
            Assert.That(_virgilDialogue.TryGet(120, out DialogLine secondMarker), Is.True);
            Assert.That(secondMarker.Effect.Replace(" ", string.Empty), Is.EqualTo("mm1"));
            foreach (int line in new[] { 386, 387, 388, 389, 390 })
            {
                Assert.That(_virgilDialogue.TryGet(line, out DialogLine option), Is.True, $"line {line}");
                Assert.That(option.Test.Replace(" ", string.Empty), Is.EqualTo("wa0"), $"line {line}");
            }

            GameObject root = new(nameof(VirgilRemainingWaGateAndMmEffectsUseAuthoritativeSourceState));
            try
            {
                WorldMapSessionCoordinator session = root.AddComponent<WorldMapSessionCoordinator>();
                WorldObjectSectorLoader loader = root.AddComponent<WorldObjectSectorLoader>();
                loader.BindSessionAuthority();
                Assert.That(session.SelectSector(CrashSector), Is.True);
                PersistentPlayerState pc = session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                    CrashSector, Vector2.one, 0x28100000u);
                session.BindPlayer(CrashSector, pc, Runtime(root, "Player", ObjectType.Pc, pc.Identity));
                Assert.That(session.TryGetObjectState(Virgil, out PersistentObjectState virgil), Is.True);
                Assert.That(virgil.NpcFlags & 0x00000008, Is.Zero,
                    "retail Virgil is not in ONF_AI_WAIT_HERE state at the opening");

                var lines = new SortedDictionary<int, DialogLine>
                {
                    [385] = new(385, "What is it that you want of me?", "", 0, "", 0, ""),
                    [386] = new(386, "Show me the marked places.", "", 1, "wa 0", 0, "mm1, mm2"),
                };
                session.BindDialogueSource(_ => StartDialogueAt(385),
                    number => number == VirgilDialogueNumber ? new DialogScript(lines) : null);

                Assert.That(session.Dialogue.Start(pc.Identity, Virgil), Is.EqualTo(DialogueStartStatus.Started));
                Assert.That(session.Dialogue.AvailableResponses, Has.Count.EqualTo(1));
                Assert.That(session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.Completed));
                Assert.That(session.Campaign.IsAreaKnown(new AreaId(1)), Is.True);
                Assert.That(session.Campaign.IsAreaKnown(new AreaId(2)), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test, Category("RealData")]
        public void AuthenticOpeningCorpsesFreezeOnFinalFrameRemainSelectableAndKeepSourceInventory()
        {
            GameObject root = new(nameof(AuthenticOpeningCorpsesFreezeOnFinalFrameRemainSelectableAndKeepSourceInventory));
            try
            {
                WorldMapSessionCoordinator session = root.AddComponent<WorldMapSessionCoordinator>();
                WorldObjectSectorLoader loader = root.AddComponent<WorldObjectSectorLoader>();
                loader.BindSessionAuthority();
                Assert.That(session.SelectSector(CrashSector), Is.True);

                foreach ((string key, int inventoryCount) in SourceCorpses)
                {
                    ArcanumObjectId identity = Parse(key);
                    Assert.That(session.TryGetObjectState(identity, out PersistentObjectState corpse), Is.True, key);
                    Assert.That(session.Vitality.IsDead(identity), Is.True, key);
                    Assert.That(corpse.DeathConsequencesProcessed, Is.False, key);
                    Assert.That(session.States.Values.Count(value => value.ParentIdentity == identity),
                        Is.EqualTo(inventoryCount), key);
                    Assert.That(session.TryGetLoadedObject(identity, out WorldObject runtime), Is.True, key);
                    Assert.That(runtime.Identity, Is.EqualTo(identity));
                    Assert.That(runtime.IsDead, Is.True, key);
                    Assert.That((runtime.ArtId >> 6) & 0x1Fu, Is.EqualTo(7u), key);

                    WorldObjectSpriteOwner owner = loader.SpriteOwners.Single(value =>
                        value.WorldObject.Identity == identity);
                    Assert.That(owner.FrameCount, Is.GreaterThan(0), key);
                    Assert.That(owner.CurrentFrameIndex, Is.EqualTo(owner.FrameCount - 1), key);
                    Assert.That(TryOpaqueWorldPoint(owner.CurrentSprite, owner.transform, out Vector2 point), Is.True);
                    Assert.That(WorldObjectTargetSelector.TrySelectInteractionTarget(new[] { owner }, point,
                        out ArcanumObjectId selected, out ObjectType type), Is.True, key);
                    Assert.That((selected, type), Is.EqualTo((identity, ObjectType.Npc)));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test, Category("RealData")]
        public void AuthenticCrashSiteHeartbeatCasualtiesInitializeDeadWithoutRuntimeDeathConsequences()
        {
            GameObject root = new(nameof(AuthenticCrashSiteHeartbeatCasualtiesInitializeDeadWithoutRuntimeDeathConsequences));
            try
            {
                WorldMapSessionCoordinator session = root.AddComponent<WorldMapSessionCoordinator>();
                WorldObjectSectorLoader loader = root.AddComponent<WorldObjectSectorLoader>();
                loader.BindSessionAuthority();
                Assert.That(session.SelectSector(CrashSector), Is.True);

                foreach (string key in HeartbeatInitializedCorpses)
                {
                    ArcanumObjectId identity = Parse(key);
                    Assert.That(session.TryGetObjectState(identity, out PersistentObjectState corpse), Is.True, key);
                    Assert.That(session.Vitality.IsDead(identity), Is.True, key);
                    Assert.That(session.Vitality.Get(identity).HitPointDamage, Is.EqualTo(32000), key);
                    Assert.That(corpse.DeathConsequencesProcessed, Is.False, key);
                    Assert.That(session.TryGetLoadedObject(identity, out WorldObject runtime), Is.True, key);
                    Assert.That(runtime.IsDead, Is.True, key);
                    Assert.That((runtime.ArtId >> 6) & 0x1Fu, Is.EqualTo(7u), key);
                }

                Assert.That(session.Vitality.IsAlive(Virgil), Is.True, "Virgil is the opening living control");
                Assert.That(session.States.Values.Where(value => value.PrototypeNumber is 28359 or 27356),
                    Is.Not.Empty, "authentic wolf/scout controls must be present");
                foreach (PersistentObjectState control in session.States.Values.Where(value =>
                             value.PrototypeNumber is 28359 or 27356))
                    Assert.That(session.Vitality.IsAlive(control.Identity), Is.True,
                        $"living control {control.Identity}");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void StateOnlyHeartbeatResolverSupportsBothRetailBranchesAndRejectsUnsafePrograms()
        {
            ScriptFile retailShape = HeartbeatGate(2350);
            var campaign = new CampaignStateService();
            Assert.That(SourceHeartbeatInitialStateResolver.TryResolve(retailShape, campaign,
                out SourceHeartbeatInitialStateResolver.Result absent), Is.True);
            Assert.That(absent, Is.EqualTo(SourceHeartbeatInitialStateResolver.Result.Dead));

            campaign.SetFlag(2350, 1);
            Assert.That(SourceHeartbeatInitialStateResolver.TryResolve(retailShape, campaign,
                out SourceHeartbeatInitialStateResolver.Result hidden), Is.True);
            Assert.That(hidden, Is.EqualTo(SourceHeartbeatInitialStateResolver.Result.Off));

            retailShape.Entries[4].Action = Action(Sat.AdjustGold);
            Assert.That(SourceHeartbeatInitialStateResolver.TryResolve(retailShape, campaign, out _), Is.False,
                "ordinary gameplay mutations are outside initial-state reconstruction");
        }

        [Test]
        public void WalkRegistrationLeavesSourceMovementDeltasOutOfSpritePivot()
        {
            ArtFrame[] frames =
            {
                Frame(10, 20, 99, 99),
                Frame(10, 20, 3, -2),
                Frame(10, 20, -1, 4),
            };
            Vector2 pivot = WorldObjectSpriteOwner.ExactPivot(frames[2], ArtId.TypeCritter,
                0, false, false);
            Assert.That(pivot.x, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(pivot.y, Is.EqualTo(1f / 3f).Within(.0001f));

            Vector2 mirrored = WorldObjectSpriteOwner.ExactPivot(frames[2], ArtId.TypeCritter,
                7, true, false);
            Assert.That(mirrored.x, Is.EqualTo(.45f).Within(.0001f));
            Assert.That(mirrored.y, Is.EqualTo(1f / 3f).Within(.0001f));
        }

        [Test, Category("RealData")]
        public void AuthenticHumanWalkOffsetsAreMovementDeltasNotPresentationAnchors()
        {
            const string path = "art/critter/hmm/hmmv1xab.art";
            Assert.That(_vfs.Exists(path), Is.True);
            ArtFile art = ArtReader.Read(_vfs.ReadAllBytes(path));
            ArtFrame[] east = art.Rotations[2].Frames;
            Assert.That(east.Select(frame => frame.OffsetX),
                Is.EqualTo(new[] { 4, 10, 8, 4, 6, 4, 8, 8, 4, 4 }));
            Assert.That(east.Select(frame => frame.OffsetY), Is.All.EqualTo(0));
            for (int frameIndex = 0; frameIndex < east.Length; frameIndex++)
            {
                ArtFrame frame = east[frameIndex];
                Vector2 pivot = WorldObjectSpriteOwner.ExactPivot(frame, ArtId.TypeCritter,
                    2, false, false);
                Assert.That(pivot.x, Is.EqualTo(frame.HotX / (float)frame.Width).Within(.0001f),
                    $"frame {frameIndex}");
                Assert.That(pivot.y, Is.EqualTo((frame.Height - frame.HotY) / (float)frame.Height)
                    .Within(.0001f), $"frame {frameIndex}");
            }
        }

        [Test]
        public void ProductionHudLateBindsInteractionControllerCreatedAfterPresenterAwake()
        {
            GameObject root = new(nameof(ProductionHudLateBindsInteractionControllerCreatedAfterPresenterAwake));
            try
            {
                ProductionGameUiPresenter presenter = root.AddComponent<ProductionGameUiPresenter>();
                Assert.That(root.GetComponent<PlayerInteractionController>(), Is.Null,
                    "the production loader creates the HUD before PlayerClickMoveInput creates interaction authority");

                PlayerInteractionController interaction = root.AddComponent<PlayerInteractionController>();
                presenter.EnsureInteractionSubscription();

                var interactionField = typeof(ProductionGameUiPresenter).GetField("_interaction",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var subscribedField = typeof(ProductionGameUiPresenter).GetField("_interactionSubscribed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(interactionField, Is.Not.Null);
                Assert.That(subscribedField, Is.Not.Null);
                Assert.That(interactionField.GetValue(presenter), Is.SameAs(interaction));
                Assert.That(subscribedField.GetValue(presenter), Is.EqualTo(true));

                presenter.EnsureInteractionSubscription();
                Assert.That(interactionField.GetValue(presenter), Is.SameAs(interaction),
                    "the subscription path must remain idempotent on subsequent production updates");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SourceWalkClockProjectsAuthoredFrameDeltasIntoRouteProgress()
        {
            Assert.That(SourceLocomotionTiming.AdjustedWalkFramesPerSecond(17, 8, 0x28100000u),
                Is.EqualTo(17));
            Assert.That(SourceLocomotionTiming.AdjustedWalkFramesPerSecond(17, 9, 0x28100000u),
                Is.EqualTo(20), "source sub_437990 uses the actor SPEED timing formula");

            float east = new[] { 4, 10, 8, 4, 6, 4, 8, 8, 4, 4 }
                .Sum(offset => SourceLocomotionTiming.TileProgress(offset, 0, 2));
            Assert.That(east, Is.EqualTo(.75f).Within(.0001f),
                "60 authored pixels are 0.75 of rotation-2's 80-pixel tile vector");

            var route = new TileRouteFollower();
            route.Replace(Vector2.zero, new[] { new Vector2Int(-1, 1) });
            Assert.That(route.Advance(east), Is.True);
            Assert.That(route.Position.x, Is.EqualTo(-.75f).Within(.0001f));
            Assert.That(route.Position.y, Is.EqualTo(.75f).Within(.0001f));
            Assert.That(route.Facing, Is.EqualTo(2));
        }

        [Test, Category("RealData")]
        public void AuthenticCrashSiteUsesCompleteSourceThreeByThreeTerrainWindow()
        {
            SectorStreamingWindow.Entry[] window = SectorStreamingWindow.Around(CrashSector).ToArray();
            Assert.That(window, Has.Length.EqualTo(9));
            Assert.That(window.Select(value => (value.DeltaX, value.DeltaY)).Distinct().Count(), Is.EqualTo(9));
            Assert.That(window.All(value => _vfs.Exists(value.Path)), Is.True,
                "all eight authentic crash-site neighbors are present in the retail module");
            Assert.That(window.Single(value => value.DeltaX == 0 && value.DeltaY == 0).Path,
                Is.EqualTo(CrashSector));
        }

        [Test, Category("RealData")]
        public void AuthenticCrashSiteCurrentAreaUsesStartMapTownmapSemantics()
        {
            MapList maps = MapList.Read(_vfs.ReadAllBytes("rules/maplist.mes"));
            AreaList areas = AreaList.FromMes(MesReader.Read(_vfs.ReadAllBytes("mes/gamearea.mes")));
            var resolver = new MapAreaResolver(maps, areas, _vfs.Exists, _vfs.ReadAllBytes);
            Assert.That(resolver.Resolve(CrashSector, new Vector2(92958, 82592)), Is.EqualTo(2),
                "retail START_MAP location is in the Crash Site townmap/area");
        }

        [Test, Category("RealData")]
        public void AuthenticVirgilSsIaCeAndWaReachProductionAuthorityWithoutCompatibilityDiagnostics()
        {
            var responseSet = new List<DialogLine>();
            foreach (int line in Enumerable.Range(397, 33))
                if (_virgilDialogue.TryGet(line, out DialogLine value))
                    responseSet.Add(value);
            Assert.That(responseSet.Any(value => value.Test.Replace(" ", string.Empty).Contains("ss")), Is.True);
            Assert.That(_virgilDialogue.TryGet(454, out DialogLine areaGate), Is.True);
            Assert.That(areaGate.Test.Replace(" ", string.Empty), Is.EqualTo("ia21"));
            Assert.That(_virgilDialogue.TryGet(396, out DialogLine examine), Is.True);
            Assert.That(examine.Effect.Replace(" ", string.Empty), Is.EqualTo("ce"));
            Assert.That(_virgilDialogue.TryGet(510, out DialogLine wait), Is.True);
            Assert.That(wait.Test.Replace(" ", string.Empty), Is.EqualTo("wa0"));
            Assert.That(wait.Effect.Replace(" ", string.Empty), Is.EqualTo("wa"));

            GameObject root = new(nameof(AuthenticVirgilSsIaCeAndWaReachProductionAuthorityWithoutCompatibilityDiagnostics));
            try
            {
                WorldMapSessionCoordinator session = root.AddComponent<WorldMapSessionCoordinator>();
                WorldObjectSectorLoader loader = root.AddComponent<WorldObjectSectorLoader>();
                loader.BindSessionAuthority();
                Assert.That(session.SelectSector(CrashSector), Is.True);
                PersistentPlayerState pc = session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                    CrashSector, new Vector2(30, 32), 0x28100000u);
                session.BindPlayer(CrashSector, pc, Runtime(root, "Player", ObjectType.Pc, pc.Identity));
                Assert.That(session.CurrentArea, Is.EqualTo(2));
                var controller = new GameUiController(session);

                var examinationLines = new SortedDictionary<int, DialogLine>
                {
                    [395] = new(395, "Let me examine your abilities.", "", 0, "", 0, ""),
                    [396] = new(396, "Go ahead.", "", 1, "ss 0, ia 2", 0, "ce"),
                };
                session.BindDialogueSource(_ => StartDialogueAt(395),
                    number => number == VirgilDialogueNumber ? new DialogScript(examinationLines) : null);
                Assert.That(session.Dialogue.Start(pc.Identity, Virgil), Is.EqualTo(DialogueStartStatus.Started));
                controller.Refresh();
                int examineIndex = session.Dialogue.AvailableResponses.ToList()
                    .FindIndex(value => value.Num == 396);
                Assert.That(examineIndex, Is.GreaterThanOrEqualTo(0),
                    "ss/ia filtering must admit the source-valid examination response");
                Assert.That(controller.SelectDialogueResponse(examineIndex), Is.True);
                Assert.That(controller.Screen, Is.EqualTo(GameUiScreen.Character));
                Assert.That(controller.CharacterTarget, Is.EqualTo(Virgil));
                Assert.That(controller.CharacterReadOnly, Is.True);
                Assert.That(controller.ProjectCharacter(), Is.Not.Null);

                Assert.That(session.Party.Join(Virgil).Succeeded, Is.True);
                var waitLines = new SortedDictionary<int, DialogLine>
                {
                    [505] = new(505, "Do you want me to wait here?", "", 0, "", 0, ""),
                    [510] = new(510, "Wait here.", "", 1, "wa 0", 0, "wa"),
                };
                session.BindDialogueSource(_ => StartDialogueAt(505),
                    number => number == VirgilDialogueNumber ? new DialogScript(waitLines) : null);
                Assert.That(session.Dialogue.Start(pc.Identity, Virgil), Is.EqualTo(DialogueStartStatus.Started));
                int waitIndex = session.Dialogue.AvailableResponses.ToList().FindIndex(value => value.Num == 510);
                Assert.That(waitIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(session.Dialogue.SelectResponse(waitIndex), Is.EqualTo(DialogueChoiceStatus.Completed));
                Assert.That(session.TryGetObjectState(Virgil, out PersistentObjectState virgil), Is.True);
                Assert.That(virgil.NpcFlags & 0x00000008, Is.EqualTo(0x00000008));
                Assert.That(session.Party.IsMember(Virgil), Is.True,
                    "source wait preserves the restored leader relation");
                Assert.That(session.Party.CanAccompany(Virgil), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static ArtFrame Frame(int hotX, int hotY, int offsetX, int offsetY)
            => new(20, 30, hotX, hotY, offsetX, offsetY, new byte[600]);

        private static ScriptFile StartDialogueAt(int line)
        {
            var file = new ScriptFile();
            file.Entries.Add(Condition(Sct.True, Action(Sat.Dialog, (Svt.Number, line)), Action(Sat.DoNothing)));
            return file;
        }

        private static ScriptFile HeartbeatGate(int flag)
        {
            var file = new ScriptFile();
            file.Entries.Add(Condition(Sct.Eq,
                Action(Sat.DoNothing), Action(Sat.Goto, (Svt.Number, 4)),
                (Svt.GlFlag, flag), (Svt.Number, 1)));
            file.Entries.Add(Condition(Sct.True,
                ObjectAction(Sat.ToggleState, Sfo.Attachee), Action(Sat.DoNothing)));
            file.Entries.Add(Condition(Sct.True, Action(Sat.RemoveThisScript), Action(Sat.DoNothing)));
            file.Entries.Add(Condition(Sct.True, Action(Sat.ReturnAndSkipDefault), Action(Sat.DoNothing)));
            file.Entries.Add(Condition(Sct.True,
                ObjectAction(Sat.Kill, Sfo.Attachee), Action(Sat.DoNothing)));
            file.Entries.Add(Condition(Sct.True, Action(Sat.ReturnAndRunDefault), Action(Sat.DoNothing)));
            return file;
        }

        private static ScriptCondition Condition(Sct type, ScriptAction action, ScriptAction els,
            params (Svt type, int value)[] operands)
        {
            var condition = new ScriptCondition { Type = (int)type, Action = action, Els = els };
            for (int index = 0; index < operands.Length; index++)
            {
                condition.OpType[index] = (byte)operands[index].type;
                condition.OpValue[index] = operands[index].value;
            }
            return condition;
        }

        private static ScriptAction Action(Sat type, params (Svt type, int value)[] operands)
        {
            var action = new ScriptAction { Type = (int)type };
            for (int index = 0; index < operands.Length; index++)
            {
                action.OpType[index] = (byte)operands[index].type;
                action.OpValue[index] = operands[index].value;
            }
            return action;
        }

        private static ScriptAction ObjectAction(Sat type, Sfo focus)
        {
            var action = Action(type);
            action.OpType[0] = (byte)focus;
            return action;
        }

        private static bool TryOpaqueWorldPoint(Sprite sprite, Transform transform, out Vector2 point)
        {
            point = default;
            if (sprite == null || sprite.texture == null) return false;
            Rect rect = sprite.rect;
            for (int y = 0; y < (int)rect.height; y++)
            for (int x = 0; x < (int)rect.width; x++)
            {
                if (sprite.texture.GetPixel((int)rect.x + x, (int)rect.y + y).a <= .01f) continue;
                Vector3 local = new((x + .5f - sprite.pivot.x) / sprite.pixelsPerUnit,
                    (y + .5f - sprite.pivot.y) / sprite.pixelsPerUnit, 0f);
                point = transform.TransformPoint(local);
                return true;
            }
            return false;
        }

        private static WorldObject Runtime(GameObject root, string name, ObjectType type,
            ArcanumObjectId identity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            WorldObject runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            runtime.Identity = identity;
            runtime.ArtId = 0x28100000u;
            runtime.Tile = new Vector2Int(3, 1);
            runtime.TilePosition = runtime.Tile;
            return runtime;
        }

        private static int[] Stats()
        {
            var result = new int[CharacterAttributeSet.SourceStatArrayCount];
            for (int index = 0; index < CharacterAttributeSet.Count; index++) result[index] = 8;
            result[CharacterProgressionSource.LevelSourceSlot] = 1;
            result[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            result[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            return result;
        }

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);
        private static ArcanumObjectId Parse(string key)
        {
            Assert.That(ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity), Is.True);
            return identity;
        }

        private static byte[] GuidBytes(string key)
        {
            ArcanumObjectId identity = Parse(key);
            string compact = identity.Key.Substring(2).Replace("_", "");
            var bytes = new byte[24];
            bytes[0] = (byte)ArcanumObjectIdType.Guid;
            for (int index = 0; index < 16; index++)
                bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
            return bytes;
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => CrashSector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string path)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(path);
                _session.BeginSector(PresentedSector);
                return true;
            }
            public void ClearPresentedSector()
            {
                string path = PresentedSector;
                PresentedSector = null;
                if (path != null) _session.UnloadSector(path);
            }
        }
    }
}
