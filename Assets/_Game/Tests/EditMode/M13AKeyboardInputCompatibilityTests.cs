using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Party;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M13AKeyboardInput")]
    public sealed class M13AKeyboardInputCompatibilityTests
    {
        private ProductionKeyboardController _keyboard;
        private Commands _commands;
        private static ProductionKeyboardContext Hud(bool combat = false,
            CombatMode mode = CombatMode.TurnBased)
            => new(GameUiScreen.None, true, false, combat, mode);

        [SetUp] public void SetUp() { _keyboard = new ProductionKeyboardController(); _commands = new Commands(); }

        [TestCase(KeyCode.I, GameUiScreen.Inventory)]
        [TestCase(KeyCode.C, GameUiScreen.Character)]
        [TestCase(KeyCode.M, GameUiScreen.Magic)]
        [TestCase(KeyCode.T, GameUiScreen.Technology)]
        [TestCase(KeyCode.K, GameUiScreen.Skills)]
        [TestCase(KeyCode.L, GameUiScreen.Journal)]
        [TestCase(KeyCode.W, GameUiScreen.Map)]
        [TestCase(KeyCode.O, GameUiScreen.Options)]
        public void LetterDefaultsDispatchToRealProductionScreens(KeyCode key, GameUiScreen screen)
        {
            Assert.That(_keyboard.Dispatch(key, ProductionKeyPhase.Down, Hud(), _commands), Is.True);
            Assert.That(_commands.Screen, Is.EqualTo(screen));
        }

        [Test]
        public void F1ThroughF6AreTheSixSourceBroadcastOrdersOnRelease()
        {
            for (int index = 0; index < 6; index++)
                Assert.That(_keyboard.Dispatch((KeyCode)((int)KeyCode.F1 + index),
                    ProductionKeyPhase.Up, Hud(), _commands), Is.True);
            Assert.That(_commands.PartyOrders, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
        }

        [Test]
        public void F7F8AndF12RouteToAutoSlotsAndScreenshotPresentation()
        {
            _keyboard.Dispatch(KeyCode.F7, ProductionKeyPhase.Up, Hud(), _commands);
            _keyboard.Dispatch(KeyCode.F8, ProductionKeyPhase.Up, Hud(), _commands);
            _keyboard.Dispatch(KeyCode.F12, ProductionKeyPhase.Down, Hud(), _commands);
            Assert.That((_commands.Saves, _commands.Loads, _commands.Screenshots), Is.EqualTo((1, 1, 1)));
        }

        [Test]
        public void NumberRowMapsAllTenRetailQuickSlots()
        {
            for (int value = (int)KeyCode.Alpha1; value <= (int)KeyCode.Alpha9; value++)
                _keyboard.Dispatch((KeyCode)value, ProductionKeyPhase.Down, Hud(), _commands);
            _keyboard.Dispatch(KeyCode.Alpha0, ProductionKeyPhase.Down, Hud(), _commands);
            Assert.That(_commands.QuickSlots, Is.EqualTo(Enumerable.Range(0, 10)));
        }

        [Test]
        public void DialogueOwnsNumberKeysAndTextEntrySuppressesGameplayShortcuts()
        {
            var dialogue = new ProductionKeyboardContext(GameUiScreen.Dialogue, true, false, false, default);
            Assert.That(_keyboard.Dispatch(KeyCode.Alpha1, ProductionKeyPhase.Down, dialogue, _commands), Is.False);
            var typing = new ProductionKeyboardContext(GameUiScreen.CharacterCreation, false, true, false, default);
            foreach (KeyCode key in new[] { KeyCode.W, KeyCode.A, KeyCode.L, KeyCode.T, KeyCode.E, KeyCode.Return })
                Assert.That(_keyboard.Dispatch(key, ProductionKeyPhase.Down, typing, _commands), Is.False);
            Assert.That(_commands.Screen, Is.EqualTo(GameUiScreen.None));
        }

        [Test]
        public void EscapeAndSpaceHonorModalPrecedenceBeforeGlobalActions()
        {
            var inventory = new ProductionKeyboardContext(GameUiScreen.Inventory, true, false, true, CombatMode.TurnBased);
            _keyboard.Dispatch(KeyCode.Space, ProductionKeyPhase.Down, inventory, _commands);
            Assert.That((_commands.Closes, _commands.CombatToggles), Is.EqualTo((1, 0)));
            _keyboard.Dispatch(KeyCode.Space, ProductionKeyPhase.Down, Hud(true), _commands);
            _keyboard.Dispatch(KeyCode.Escape, ProductionKeyPhase.Up, inventory, _commands);
            Assert.That((_commands.Escapes, _commands.CombatToggles), Is.EqualTo((1, 1)));
        }

        [Test]
        public void CalledShotsAreHeldAndUseHeadArmLegSourceMapping()
        {
            foreach ((KeyCode key, CombatCalledLocation expected) in new[]
                     {
                         (KeyCode.Comma, CombatCalledLocation.Head),
                         (KeyCode.Period, CombatCalledLocation.Arm),
                         (KeyCode.Slash, CombatCalledLocation.Leg),
                     })
            {
                _keyboard.Dispatch(key, ProductionKeyPhase.Down, Hud(true), _commands);
                Assert.That(_commands.CalledLocation, Is.EqualTo(expected));
                _keyboard.Dispatch(key, ProductionKeyPhase.Up, Hud(true), _commands);
                Assert.That(_commands.CalledLocation, Is.EqualTo(CombatCalledLocation.Torso));
            }
        }

        [Test]
        public void EEndsOnlyTurnBasedCombat()
        {
            Assert.That(_keyboard.Dispatch(KeyCode.E, ProductionKeyPhase.Up, Hud(false), _commands), Is.False);
            Assert.That(_keyboard.Dispatch(KeyCode.E, ProductionKeyPhase.Up,
                Hud(true, CombatMode.RealTime), _commands), Is.True);
            Assert.That(_commands.EndTurns, Is.Zero);
            _keyboard.Dispatch(KeyCode.E, ProductionKeyPhase.Up, Hud(true), _commands);
            Assert.That(_commands.EndTurns, Is.EqualTo(1));
        }

        [Test]
        public void NumLockDefaultAndControlHeldTemporarilyInvertRunWalk()
        {
            Assert.That(_keyboard.EffectiveRun, Is.False);
            _keyboard.SetModifiers(true, false, false);
            Assert.That(_keyboard.EffectiveRun, Is.True);
            _keyboard.Dispatch(KeyCode.Numlock, ProductionKeyPhase.Down, Hud(), _commands);
            Assert.That((_keyboard.AlwaysRun, _keyboard.EffectiveRun), Is.EqualTo((true, false)));
            _keyboard.SetModifiers(false, true, true);
            Assert.That((_keyboard.EffectiveRun, _keyboard.ShiftHeld, _keyboard.AltHeld),
                Is.EqualTo((true, true, true)));
        }

        [Test]
        public void ArrowKeysDispatchContinuousCameraIntentOnly()
        {
            Vector2 player = new(12, 17);
            foreach (KeyCode key in new[] { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow })
                _keyboard.Dispatch(key, ProductionKeyPhase.Held, Hud(), _commands);
            Assert.That(_commands.CameraScrolls, Has.Count.EqualTo(4));
            Assert.That(player, Is.EqualTo(new Vector2(12, 17)));
        }

        [Test]
        public void UnsupportedRetailSurfacesFailClosedInsteadOfOpeningDummyWindows()
        {
            foreach (KeyCode key in new[] { KeyCode.S, KeyCode.F, KeyCode.Return })
                Assert.That(_keyboard.Dispatch(key, ProductionKeyPhase.Down, Hud(), _commands), Is.True);
            Assert.That(_commands.UnsupportedActions, Is.EqualTo(new[] { "Sleep", "Fate points", "Broadcast/chat" }));
            Assert.That(_commands.Screen, Is.EqualTo(GameUiScreen.None));
        }

        [Test]
        public void HomeRAndActiveActionRouteThroughControllerBoundaries()
        {
            _keyboard.Dispatch(KeyCode.Home, ProductionKeyPhase.Up, Hud(), _commands);
            _keyboard.Dispatch(KeyCode.R, ProductionKeyPhase.Down, Hud(), _commands);
            _keyboard.Dispatch(KeyCode.A, ProductionKeyPhase.Down, Hud(), _commands);
            Assert.That((_commands.Centers, _commands.ReadyToggles, _commands.ActiveActions), Is.EqualTo((1, 1, 1)));
        }

        [Test]
        public void PartyOrderStatePreservesSourceFormationAndFollowSemantics()
        {
            using var fixture = new SessionFixture();
            PartyStateService party = fixture.Session.Party;
            Assert.That(party.IssueOrder(PartyOrder.StayClose), Is.True);
            Assert.That((party.FollowingEnabled, party.DesiredFollowRange),
                Is.EqualTo((true, PartyFollowerMovementService.SourceCloseRange)));
            Assert.That(party.IssueOrder(PartyOrder.SpreadOut), Is.True);
            Assert.That(party.DesiredFollowRange, Is.EqualTo(PartyFollowerMovementService.SourceSpreadRange));
            Assert.That(party.IssueOrder(PartyOrder.BackOff), Is.True);
            Assert.That(party.FollowingEnabled, Is.False);
            Assert.That(party.IssueOrder(PartyOrder.Follow), Is.True);
            Assert.That((party.FollowingEnabled, party.DesiredFollowRange),
                Is.EqualTo((true, PartyFollowerMovementService.SourceFollowRange)));
        }

        [Test]
        public void QuickSlotStoresAuthoritativeItemReferenceAndFindsSamePrototypeReplacement()
        {
            using var fixture = new SessionFixture();
            PersistentObjectState first = fixture.AddItem(1, 8100);
            PersistentObjectState replacement = fixture.AddItem(2, 8100);
            Assert.That(fixture.Session.Shortcuts.AssignItem(0, first.Identity), Is.True);
            first.Placement = ObjectPlacement.InWorld(SessionFixture.Sector, Vector2.zero);
            Assert.That(fixture.Session.Shortcuts.TryResolveItem(0, out ArcanumObjectId resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(replacement.Identity));
        }

        [Test]
        public void QuickSlotSpellAssignmentRequiresAuthoritativeKnowledge()
        {
            using var fixture = new SessionFixture();
            Assert.That(fixture.Session.Shortcuts.AssignSpell(1, PhaseOneSpellCatalog.StrengthOfEarth), Is.False);
            fixture.Session.Magic.SetKnownCollegeRank(fixture.Pc, SpellCollege.Earth, 1);
            Assert.That(fixture.Session.Shortcuts.AssignSpell(1, PhaseOneSpellCatalog.StrengthOfEarth), Is.True);
            Assert.That(fixture.Session.Shortcuts.Get(1).Kind, Is.EqualTo(QuickSlotKind.Spell));
        }

        [Test]
        public void AutoSaveAndLoadUseTheDedicatedAuthoritativeAutoSlot()
        {
            using var fixture = new SessionFixture();
            var slots = new Slots();
            var ui = new GameUiController(fixture.Session, slots);
            Assert.That(ui.AutoSave(), Is.True);
            Assert.That(ui.AutoLoad(), Is.True);
            Assert.That((slots.Saved, slots.Loaded), Is.EqualTo(("auto", "auto")));
        }

        private sealed class Commands : IProductionKeyboardCommands
        {
            public GameUiScreen Screen;
            public readonly List<int> PartyOrders = new();
            public readonly List<int> QuickSlots = new();
            public readonly List<Vector2> CameraScrolls = new();
            public readonly List<string> UnsupportedActions = new();
            public int Saves, Loads, Screenshots, Closes, CombatToggles, Escapes, EndTurns;
            public int Centers, ReadyToggles, ActiveActions;
            public CombatCalledLocation CalledLocation;
            public void Escape() => Escapes++;
            public void ToggleScreen(GameUiScreen screen) => Screen = screen;
            public void CloseInterface() => Closes++;
            public void ToggleCombatMode() => CombatToggles++;
            public void EndTurn() => EndTurns++;
            public void ToggleAttackTalkMode() => ReadyToggles++;
            public void ActivateQuickSlot(int index) => QuickSlots.Add(index);
            public void ActivateRecentAction() => ActiveActions++;
            public void SetCalledLocation(CombatCalledLocation location) => CalledLocation = location;
            public void IssuePartyOrder(int sourceIndex) => PartyOrders.Add(sourceIndex);
            public void AutoSave() => Saves++;
            public void AutoLoad() => Loads++;
            public void CaptureScreenshot() => Screenshots++;
            public void ShowVersion() { }
            public void CenterCamera() => Centers++;
            public void ScrollCamera(Vector2 direction) => CameraScrolls.Add(direction);
            public void Unsupported(string sourceAction) => UnsupportedActions.Add(sourceAction);
        }

        private sealed class SessionFixture : IDisposable
        {
            public const string Sector = "maps/test/1.sec";
            private readonly GameObject _root = new(nameof(M13AKeyboardInputCompatibilityTests));
            public WorldMapSessionCoordinator Session { get; }
            public ArcanumObjectId Pc { get; }
            public SessionFixture()
            {
                Session = _root.AddComponent<WorldMapSessionCoordinator>();
                Session.RegisterObjectOwner(new Owner(Session));
                Assert.That(Session.SelectSector(Sector), Is.True);
                Pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
                Session.GetOrCreatePlayer(Pc, Sector, Vector2.one, 0x28100000u);
            }
            public PersistentObjectState AddItem(int sequence, int prototype)
            {
                var source = new ObjectInstance(ObjectType.Armor, prototype, null, 0x70000000u, 0, 0,
                    oid: GuidBytes(sequence), parentOid: Bytes(Pc), invLocation: 0);
                return Session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false,
                    inventoryArtId: 0u, unitWeight: 1, inventoryLocation: 0);
            }
            public void Dispose() => Object.DestroyImmediate(_root);
            private static byte[] GuidBytes(int sequence)
                => Bytes(Parse($"G_{sequence:X8}_0000_0000_0000_000000000000"));
            private static ArcanumObjectId Parse(string key)
            { ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId id); return id; }
            private static byte[] Bytes(ArcanumObjectId identity)
            {
                string compact = identity.Key.Substring(2).Replace("_", "");
                var bytes = new byte[24]; bytes[0] = (byte)ArcanumObjectIdType.Guid;
                for (int i = 0; i < 16; i++) bytes[8 + i] = Convert.ToByte(compact.Substring(i * 2, 2), 16);
                return bytes;
            }
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => SessionFixture.Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string path)
            { PresentedSector = path; _session.BeginSector(path); return true; }
            public void ClearPresentedSector()
            { string path = PresentedSector; PresentedSector = null; if (path != null) _session.UnloadSector(path); }
        }

        private sealed class Slots : ISessionSaveSlotOperations
        {
            public string Saved, Loaded;
            public SessionSaveSlotResult SaveSlot(string slotId) { Saved = slotId; return new(SessionSaveSlotFailure.None); }
            public SessionSaveSlotResult LoadSlot(string slotId) { Loaded = slotId; return new(SessionSaveSlotFailure.None); }
            public SessionSaveSlotListResult ListSlots()
                => new(SessionSaveSlotFailure.None, Array.Empty<SessionSaveSlotInfo>());
            public SessionSaveSlotResult DeleteSlot(string slotId) => new(SessionSaveSlotFailure.None);
        }
    }
}
