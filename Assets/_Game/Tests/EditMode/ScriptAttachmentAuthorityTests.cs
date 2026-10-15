using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Runtime;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using Arcanum.Script;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [TestFixture]
    [Category("ScriptCompatibility")]
    public sealed class ScriptAttachmentAuthorityTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private ArcanumObjectId _player;
        private ArcanumObjectId _target;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(ScriptAttachmentAuthorityTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _player = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _session.GetOrCreatePlayer(_player, Sector, Vector2.one, 0x28100000u);
            _target = AddObject(91).Identity;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void EverySourceAttachmentSlotIsIndependentAndZeroDetachesOnlyTheRequestedSlot()
        {
            foreach (Sap slot in Enum.GetValues(typeof(Sap)))
                _session.Campaign.SetScriptNumber(_target, (int)slot, 1000 + (int)slot);

            _session.Campaign.SetScriptNumber(_target, (int)Sap.Heartbeat, 0);

            foreach (Sap slot in Enum.GetValues(typeof(Sap)))
                Assert.That(_session.Campaign.GetScriptNumber(_target, (int)slot, 77),
                    Is.EqualTo(slot == Sap.Heartbeat ? 0 : 1000 + (int)slot), slot.ToString());
        }

        [Test]
        public void ScriptNumberMutationPreservesLocalHeaderAndSurvivesMapAndSaveLoad()
        {
            _session.Campaign.SetScriptAttachment(_target, (int)Sap.InsertItem, 0x1234u, 0x55667788u);
            _session.Campaign.SetScriptNumber(_target, (int)Sap.InsertItem, 2222);
            string json = _session.SaveGames.SerializeCurrentSession();
            _session.Campaign.SetScriptNumber(_target, (int)Sap.InsertItem, 3333);
            _session.ClearSelectedSector();
            Assert.That(_session.SelectSector(Sector), Is.True);

            SessionLoadResult result = _session.SaveGames.LoadJson(json);

            Assert.That(result.Succeeded, Is.True, result.Message);
            ScriptAttachmentState restored = _session.Campaign.GetScriptAttachment(_target, (int)Sap.InsertItem);
            Assert.That(restored.ScriptNumber, Is.EqualTo(2222));
            Assert.That(restored.Flags, Is.EqualTo(0x1234u));
            Assert.That(restored.Counters, Is.EqualTo(0x55667788u));
        }

        [Test]
        public void SeveralRealRetailChangeScriptInstructionsMutateAuthoritativeAttachments()
        {
            using DatVirtualFileSystem vfs = MountSourceData();
            var host = new Host(_session);
            object triggerer = new WorldScriptObjectReference(_player);
            object attachee = new WorldScriptObjectReference(_target);
            int proofs = 0;
            foreach (string path in vfs.EnumerateFiles("scr/")
                         .Where(value => value.EndsWith(".scr", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                ScriptFile retail;
                try { retail = ScriptReader.Read(vfs.ReadAllBytes(path)); }
                catch { continue; }
                ScriptAction source = retail.Entries.SelectMany(entry => new[] { entry.Action, entry.Els })
                    .FirstOrDefault(action => action != null && action.Type == (int)Sat.ChangeScript
                                              && action.OpType[1] == (byte)Svt.Number
                                              && action.OpType[2] == (byte)Svt.Number
                                              && (Sfo)action.OpType[0] is Sfo.Triggerer or Sfo.Attachee or Sfo.Player);
                if (source == null) continue;
                string stem = Path.GetFileNameWithoutExtension(path);
                int scriptNumber = int.Parse(new string(stem.TakeWhile(char.IsDigit).ToArray()));
                var context = new ScriptContext
                {
                    ScriptNum = scriptNumber,
                    Triggerer = triggerer,
                    Attachee = attachee,
                    AttachmentPoint = source.OpValue[1],
                };

                ScriptExecutionResult result = new ScriptVm(host, _session.Campaign)
                    .ExecuteStrict(File(Entry(source)), context);
                Assert.That(result.Succeeded, Is.True, $"{path}: {result.Detail}");
                ArcanumObjectId changed = (Sfo)source.OpType[0] switch
                {
                    Sfo.Triggerer or Sfo.Player => _player,
                    Sfo.Attachee => _target,
                    _ => throw new AssertionException("Retail proof selected an unsupported focus operand."),
                };
                Assert.That(_session.Campaign.GetScriptNumber(changed, source.OpValue[1], -1),
                    Is.EqualTo(source.OpValue[2]), path);
                Assert.That(context.ScriptNum, Is.EqualTo(scriptNumber),
                    "the live invocation keeps its original script identity");
                Debug.Log($"RETAIL CHANGESCRIPT PROOF: {path}; slot={(Sap)source.OpValue[1]}; " +
                          $"replacement={source.OpValue[2]}");
                if (++proofs == 3) break;
            }
            Assert.That(proofs, Is.EqualTo(3), "The retail corpus should provide three literal ChangeScript proofs.");
        }

        [Test]
        public void RetailCorpusUsesOnlySourceDefinedAttachmentSlots()
        {
            using DatVirtualFileSystem vfs = MountSourceData();
            var counts = new SortedDictionary<int, int>();
            int occurrences = 0;
            foreach (string path in vfs.EnumerateFiles("scr/")
                         .Where(value => value.EndsWith(".scr", StringComparison.OrdinalIgnoreCase)))
            {
                ScriptFile file;
                try { file = ScriptReader.Read(vfs.ReadAllBytes(path)); }
                catch { continue; }
                foreach (ScriptAction action in file.Entries.SelectMany(entry => new[] { entry.Action, entry.Els }))
                {
                    if (action == null || action.Type != (int)Sat.ChangeScript) continue;
                    occurrences++;
                    if (action.OpType[1] != (byte)Svt.Number) continue;
                    counts[action.OpValue[1]] = counts.GetValueOrDefault(action.OpValue[1]) + 1;
                }
            }
            Assert.That(occurrences, Is.EqualTo(134));
            Assert.That(counts.Keys.All(value => Enum.IsDefined(typeof(Sap), value)), Is.True);
            Debug.Log("RETAIL CHANGESCRIPT SLOTS: " + string.Join(", ",
                counts.Select(pair => $"{(Sap)pair.Key}={pair.Value}")));
        }

        private PersistentObjectState AddObject(int value)
        {
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Authored;
            Array.Copy(BitConverter.GetBytes(value), 0, bytes, 8, 4);
            var source = new ObjectInstance(ObjectType.Scenery, 2000 + value, 0L,
                0x20000000u, 0, 0, oid: bytes);
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false);
        }

        private static DatVirtualFileSystem MountSourceData()
        {
            var vfs = new DatVirtualFileSystem();
            string module = GameDataLocator.Find("modules/Arcanum.dat");
            if (string.IsNullOrEmpty(module)) throw new FileNotFoundException("modules/Arcanum.dat");
            vfs.MountFile(module);
            foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
            {
                string path = GameDataLocator.Find(archive);
                if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
            }
            return vfs;
        }

        private static ScriptAction Nop() => new() { Type = (int)Sat.DoNothing };
        private static ScriptCondition Entry(ScriptAction action)
            => new() { Type = (int)Sct.True, Action = action, Els = Nop() };
        private static ScriptFile File(params ScriptCondition[] entries)
        {
            var file = new ScriptFile();
            file.Entries.AddRange(entries);
            return file;
        }

        private sealed class Host : WorldScriptCampaignHost
        {
            public Host(WorldMapSessionCoordinator session) : base(session) { }
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string sectorPath)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
                _session.BeginSector(PresentedSector);
                return true;
            }
            public void ClearPresentedSector()
            {
                string sector = PresentedSector;
                PresentedSector = null;
                if (sector != null) _session.UnloadSector(sector);
            }
        }
    }
}
