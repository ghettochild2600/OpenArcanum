using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Runtime;
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

        [Test]
        public void WalkRegistrationAccumulatesSourceOffsetsAndMirrorsHorizontalDeltas()
        {
            ArtFrame[] frames =
            {
                Frame(10, 20, 99, 99),
                Frame(10, 20, 3, -2),
                Frame(10, 20, -1, 4),
            };
            Assert.That(WorldObjectSpriteOwner.CumulativeFrameOffsets(frames, false),
                Is.EqualTo(new[] { Vector2Int.zero, new Vector2Int(3, -2), new Vector2Int(2, 2) }));
            Assert.That(WorldObjectSpriteOwner.CumulativeFrameOffsets(frames, true),
                Is.EqualTo(new[] { Vector2Int.zero, new Vector2Int(-3, -2), new Vector2Int(-2, 2) }));

            Vector2 pivot = WorldObjectSpriteOwner.ExactPivot(frames[2], ArtId.TypeCritter,
                0, false, false, 2, 2);
            Assert.That(pivot.x, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(pivot.y, Is.EqualTo(.4f).Within(.0001f));
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

        private static ArtFrame Frame(int hotX, int hotY, int offsetX, int offsetY)
            => new(20, 30, hotX, hotY, offsetX, offsetY, new byte[600]);

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
