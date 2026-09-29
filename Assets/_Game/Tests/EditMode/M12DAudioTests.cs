using System;
using System.IO;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Sound;
using Arcanum.Formats.Text;
using Arcanum.Runtime.Audio;
using Arcanum.Runtime;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M12DAudio")]
    public sealed class M12DAudioTests
    {
        private DatVirtualFileSystem _vfs;
        private SoundBank _bank;
        private GameObject _root;

        [OneTimeSetUp]
        public void LoadRetailAudioSource()
        {
            _vfs = new DatVirtualFileSystem();
            string loose = GameDataLocator.FindDirectory("modules/Arcanum");
            if (!string.IsNullOrEmpty(loose)) _vfs.MountDirectory(loose);
            foreach (string archive in new[] { "modules/Arcanum.dat", "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
            {
                string path = GameDataLocator.Find(archive);
                if (!string.IsNullOrEmpty(path)) _vfs.MountFile(path);
            }
            _bank = SoundBank.Load(_vfs);
        }

        [SetUp] public void SetUp() => _root = new GameObject(nameof(M12DAudioTests));
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);
        [OneTimeTearDown] public void DisposeSource() => _vfs?.Dispose();

        [Test]
        public void RetailSoundIdentifierResolvesDeterministically()
        {
            string first = _bank.ResolvePath(SourceAudioRouting.UiButtonClick);
            string second = _bank.ResolvePath(SourceAudioRouting.UiButtonClick);
            Assert.That(first, Is.Not.Null.And.StartsWith("sound/"));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(_bank.Exists(first), Is.True);
        }

        [Test]
        public void MissingSoundIdentifierFailsSafely()
        {
            Assert.That(_bank.ResolvePath(int.MaxValue), Is.Null);
            Assert.That(_bank.Clip(int.MaxValue), Is.Null);
        }

        [Test]
        public void RetailPcmClipIsLoadedLazilyAndCached()
        {
            AudioClip first = _bank.Clip(SourceAudioRouting.UiButtonClick);
            AudioClip second = _bank.Clip(SourceAudioRouting.UiButtonClick);
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first));
            Assert.That(first.samples, Is.GreaterThan(0));
        }

        [Test]
        public void InterfaceRoutingUsesExactSourceIds()
        {
            Assert.That(SourceAudioRouting.InterfaceSound(InterfaceAudioCue.ButtonClick, true), Is.EqualTo(3000));
            Assert.That(SourceAudioRouting.InterfaceSound(InterfaceAudioCue.BookOpen, true), Is.EqualTo(3008));
            Assert.That(SourceAudioRouting.InterfaceSound(InterfaceAudioCue.WindowClose, true), Is.EqualTo(3013));
            Assert.That(SourceAudioRouting.InterfaceSound(InterfaceAudioCue.ButtonClick, false), Is.EqualTo(3004));
        }

        [Test]
        public void SpellRoutingUsesSourceCollegeRankAndPhaseFormula()
        {
            Assert.That(SourceAudioRouting.SpellCast(PhaseOneSpellCatalog.StrengthOfEarth), Is.EqualTo(12010));
            Assert.That(SourceAudioRouting.SpellImpact(PhaseOneSpellCatalog.StrengthOfEarth), Is.EqualTo(12015));
            Assert.That(SourceAudioRouting.SpellEnd(PhaseOneSpellCatalog.StrengthOfEarth), Is.EqualTo(12018));
            Assert.That(SourceAudioRouting.SpellCast(PhaseOneSpellCatalog.MinorHealing), Is.EqualTo(21010));
        }

        [Test]
        public void VoicePathUsesRetailNamingAndMaleFallback()
        {
            Assert.That(SourceAudioRouting.VoicePath(1324, 1, false), Is.EqualTo("sound/speech/01324/v1_m.mp3"));
            Assert.That(SourceAudioRouting.VoicePath(1324, 1, true), Is.EqualTo("sound/speech/01324/v1_f.mp3"));
            Assert.That(SourceAudioRouting.MaleVoiceFallback(1324, 1), Is.EqualTo("sound/speech/01324/v1_m.mp3"));
            Assert.That(_bank.Exists(SourceAudioRouting.MaleVoiceFallback(1324, 1)), Is.True);
        }

        [Test]
        public void PositionalPresentationUsesSourceIsometricDistanceAndPan()
        {
            var service = _root.AddComponent<AudioService>();
            service.Init(_bank, SoundParams.PixelsPerTile);
            service.SetListener(Vector3.zero);
            (int centerVolume, int centerBalance) = service.Positional(Vector3.zero, SoundSize.Large);
            (int rightVolume, int rightBalance) = service.Positional(new Vector3(10, 0), SoundSize.Large);
            (int verticalVolume, _) = service.Positional(new Vector3(0, 10), SoundSize.Large);
            Assert.That((centerVolume, centerBalance), Is.EqualTo((127, 64)));
            Assert.That(rightBalance, Is.EqualTo(127));
            Assert.That(verticalVolume, Is.LessThan(rightVolume), "source doubles isometric Y distance");
        }

        [Test]
        public void OneShotRecordCarriesSourceCategoryAndClip()
        {
            var service = _root.AddComponent<AudioService>();
            service.Init(_bank, 1f);
            AudioPlaybackRecord record = service.PlayUi(SourceAudioRouting.UiButtonClick);
            Assert.That(record.SoundId, Is.EqualTo(3000));
            Assert.That(record.Category, Is.EqualTo(AudioCategory.Interface));
            Assert.That(record.VirtualPath, Is.EqualTo(_bank.ResolvePath(3000)));
            Assert.That(record.Clip, Is.Not.Null);
            Assert.That(record.Loop, Is.False);
        }

        [Test]
        public void MissingOneShotStillProducesSafeRoutingEvidence()
        {
            var service = _root.AddComponent<AudioService>();
            service.Init(_bank, 1f);
            AudioPlaybackRecord record = service.PlayUi(int.MaxValue);
            Assert.That(record.Resolved, Is.False);
            Assert.That(record.Source, Is.Null);
            Assert.That(record.SoundId, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void SchemeContextIsIdempotentBeforePlayback()
        {
            var effects = _root.AddComponent<AudioService>();
            effects.Init(_bank, 1f);
            var music = _root.AddComponent<MusicService>();
            music.Init(_bank, _vfs, effects, () => 12f);
            music.PlayScheme(5, 33);
            music.PlayScheme(5, 33);
            Assert.That((music.MusicSchemeIndex, music.AmbientSchemeIndex), Is.EqualTo((5, 33)));
            Assert.That(music.ActiveLoopCount, Is.Zero, "unchanged context does not create an eager duplicate");
        }

        [Test]
        public void LooseMusicResolvesWithoutCopyingRetailData()
        {
            var effects = _root.AddComponent<AudioService>(); effects.Init(_bank, 1f);
            var music = _root.AddComponent<MusicService>(); music.Init(_bank, _vfs, effects, () => 12f);
            string path = music.MaterializedPath("sound/music/Tarant.mp3");
            Assert.That(path, Is.Not.Null);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(path.Replace('\\', '/'), Does.Contain("modules/Arcanum/sound/music/Tarant.mp3").IgnoreCase);
        }

        [Test]
        public void DatOnlyVoiceGetsDeterministicGeneratedCache()
        {
            var effects = _root.AddComponent<AudioService>(); effects.Init(_bank, 1f);
            var music = _root.AddComponent<MusicService>(); music.Init(_bank, _vfs, effects, () => 12f);
            string virtualPath = SourceAudioRouting.MaleVoiceFallback(1324, 1);
            string first = music.MaterializedPath(virtualPath);
            string second = music.MaterializedPath(virtualPath);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(File.Exists(first), Is.True);
            Assert.That(first.Replace('\\', '/'), Does.Contain("OpenArcanumAudio/sound/speech/01324/v1_m.mp3"));
        }

        [Test]
        public void OutputProbeDistinguishesSilenceFromSignal()
        {
            Assert.That(AudioOutputSignalProbe.Peak(new float[16]), Is.Zero);
            Assert.That(AudioOutputSignalProbe.Peak(new[] { 0f, -0.25f, 0.1f }), Is.EqualTo(0.25f));
        }

        [Test]
        public void GameUiReportsPresentationOnlyAfterAuthoritativeScreenChange()
        {
            WorldMapSessionCoordinator session = _root.AddComponent<WorldMapSessionCoordinator>();
            var audio = new SpyAudio();
            var controller = new GameUiController(session, new EmptySlots());
            controller.BindAudioPresentation(audio);
            Assert.That(controller.Open(GameUiScreen.Inventory), Is.False);
            Assert.That(audio.InterfaceCalls, Is.EqualTo(1));
            Assert.That(audio.LastSucceeded, Is.False);
            Assert.That(session.PlayerState, Is.Null);
        }

        [Test]
        public void InterfaceOpenCloseDoesNotMutateSessionAuthority()
        {
            WorldMapSessionCoordinator session = _root.AddComponent<WorldMapSessionCoordinator>();
            session.RegisterObjectOwner(new Owner(session));
            Assert.That(session.SelectSector("maps/test/1.sec"), Is.True);
            session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                "maps/test/1.sec", Vector2.one, 0x28100000u);
            var audio = new SpyAudio();
            var controller = new GameUiController(session, new EmptySlots());
            controller.BindAudioPresentation(audio);
            string sector = session.SelectedSector;
            Assert.That(controller.Open(GameUiScreen.Inventory), Is.True);
            controller.Close();
            Assert.That(audio.InterfaceCalls, Is.EqualTo(2));
            Assert.That(session.SelectedSector, Is.EqualTo(sector));
        }

        [Test]
        public void TechnologyFallbackIsExplicitSourceHerbologyCue()
            => Assert.That(SourceAudioRouting.UiHerbology, Is.EqualTo(3018));

        [Test]
        public void RuntimeCreatedWeaponRetainsGenericSourceAudioFacts()
        {
            const uint artId = 0x50004000u;
            var fields = new WeaponFields { Range = 15, AmmoType = 0, AmmoConsumption = 1 };
            var prototype = new ObjectProtoInfo(6055, ObjectType.Weapon, artId,
                weight: 1250, weapon: fields) { SoundEffect = 4100, Material = 5 };
            WorldMapSessionCoordinator session = _root.AddComponent<WorldMapSessionCoordinator>();
            session.BindPrototypeSource(number => number == prototype.ProtoNumber ? prototype : null);

            ItemCreationResult created = session.CreateItem(prototype.ProtoNumber,
                ObjectPlacement.InWorld("maps/test/1.sec", Vector2.zero));

            Assert.That(created.Succeeded, Is.True);
            Assert.That(created.State.WeaponData, Is.Not.Null);
            Assert.That((created.State.WeaponData.SoundEffect, created.State.WeaponData.MaterialId,
                    created.State.WeaponData.Weight, created.State.WeaponData.ItemArtId),
                Is.EqualTo((4100, 5, 1250, artId)));
        }

        private sealed class SpyAudio : IGameAudioPresentation
        {
            public int InterfaceCalls;
            public bool LastSucceeded;
            public void PresentInterface(InterfaceAudioCue cue, bool succeeded = true)
            { InterfaceCalls++; LastSucceeded = succeeded; }
            public void PresentSpellCast(SpellCastRequest request, bool succeeded) { }
            public void PresentSpellEnd(int spellId) { }
            public void PresentTechnology(TechnologyUseRequest request, bool succeeded) { }
        }

        private sealed class EmptySlots : ISessionSaveSlotOperations
        {
            public SessionSaveSlotResult SaveSlot(string slotId) => new(SessionSaveSlotFailure.None);
            public SessionSaveSlotResult LoadSlot(string slotId) => new(SessionSaveSlotFailure.None);
            public SessionSaveSlotListResult ListSlots() => new(SessionSaveSlotFailure.None, Array.Empty<SessionSaveSlotInfo>());
            public SessionSaveSlotResult DeleteSlot(string slotId) => new(SessionSaveSlotFailure.None);
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => "maps/test/1.sec";
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string path) { PresentedSector = path; _session.BeginSector(path); return true; }
            public void ClearPresentedSector() { if (PresentedSector != null) _session.UnloadSector(PresentedSector); PresentedSector = null; }
        }
    }
}
