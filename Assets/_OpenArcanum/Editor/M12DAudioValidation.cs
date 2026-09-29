using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Formats.Sound;
using Arcanum.Runtime.Audio;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M12DAudioValidation
{
    private const string StartSector = "maps/arcanum1-024-fixed/86570436012.sec";
    private const string EquipmentSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string DoorSector = "maps/arcanum1-024-fixed/122473678402.sec";
    private const string BearSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private const string TarantSector = "maps/arcanum1-024-fixed/68853695432.sec";
    private const string TarantArrivalSector = "maps/arcanum1-024-fixed/68853695436.sec";
    private static readonly ArcanumObjectId Virgil = Parse("G_A09DCD63_7A15_D411_8F1D_00E02920220C");
    private static readonly ArcanumObjectId Bow = Parse("G_1575DBCA_4990_C243_8184_524D51F7D533");
    private static readonly ArcanumObjectId Arrows = Parse("G_FBFA4631_D97D_D740_9636_F131B2FD9F7B");
    private static readonly ArcanumObjectId Bear = Parse("G_9B807B01_A142_4949_80CE_5A085F3BEEB1");
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M12D/Run Physical PlayMode Validation", false, 4)]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M12D harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        string slot = "m12d-validation";
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        bool? initialMute = GetEditorMasterMute();
        SetEditorMasterMute(false);
        AudioListener.pause = false;
        AudioListener.volume = 1f;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Refresh(out loader, out ProductionAudioPresenter audio,
                out ProductionGameUiPresenter uiPresenter, out ProductionPlayerLifecycle lifecycle);
            audio.BindProductionPresentation();
            GameUiController ui = uiPresenter.Controller;
            Check(audio.IsInitialized && audio.Bank.ResolvePath(3000) != null,
                "retail sound tables initialize through production VFS");
            AudioListener listener = Object.FindFirstObjectByType<AudioListener>();
            Check(listener != null, "TestTerrain has a production AudioListener");
            AudioOutputSignalProbe probe = listener.GetComponent<AudioOutputSignalProbe>()
                                           ?? listener.gameObject.AddComponent<AudioOutputSignalProbe>();

            Check(ui.BeginNewGame(), "source-backed New Game opens");
            ui.SetCreationName("M12D Audio");
            ui.SetCreationIdentity(CharacterRace.Human, CharacterGender.Male);
            ui.SetCreationPortrait(1005);
            ui.SetCreationBackground(3);
            ui.AdjustCreationSpell(SpellCollege.Earth, 1);
            ui.AdjustCreationTechnology(TechnologyDiscipline.Herbology, 1);
            Check(ui.FinalizeNewGame(), "authentic New Game creates the production PC");
            yield return null;
            Refresh(out loader, out audio, out uiPresenter, out lifecycle);
            audio.BindProductionPresentation();
            ui = uiPresenter.Controller;
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            ArcanumObjectId pc = session.PlayerState.Identity;
            Check(session.SelectedSector == StartSector, "START_MAP 1 is authoritative");

            audio.ClearDiagnosticHistory();
            probe.ResetEvidence();
            Check(ui.Open(GameUiScreen.Inventory), "representative UI action succeeds");
            yield return WaitForPlayback(audio, AudioPresentationKind.Interface);
            AudioPlaybackRecord uiSound = Last(audio, AudioPresentationKind.Interface);
            Check(uiSound.SoundId == SourceAudioRouting.UiWindowOpen && uiSound.Source != null,
                "UI action routes exact retail window-open cue to one AudioSource");
            yield return RequireSignal(probe, "UI");
            yield return WaitForProgress(uiSound);
            ui.Close();

            audio.ClearDiagnosticHistory();
            probe.ResetEvidence();
            Check(ui.CastSpell(PhaseOneSpellCatalog.StrengthOfEarth, pc),
                "authoritative Strength of Earth cast succeeds");
            yield return null;
            AudioPlaybackRecord spellCast = Last(audio, AudioPresentationKind.SpellCast);
            AudioPlaybackRecord spellImpact = Last(audio, AudioPresentationKind.SpellImpact);
            Check(spellCast.SoundId == 12010 && spellImpact.SoundId == 12015
                  && spellCast.Source != null && spellImpact.Source != null,
                "spell cast/effect phases use exact snd_spell source ids");
            yield return RequireSignal(probe, "spell cast/effect");
            yield return WaitForProgress(spellCast);
            ActiveSpellEffect earth = session.Magic.ActiveEffects.Single(value =>
                value.SpellId == PhaseOneSpellCatalog.StrengthOfEarth);
            Check(ui.CancelEffect(earth.Id) && Last(audio, AudioPresentationKind.SpellEnd).SoundId == 12018,
                "maintained spell cancellation presents source end phase without owning timing");

            ItemCreationResult salve = session.CreateItem(PhaseOneTechnologyCatalog.HealingSalvePrototype,
                ObjectPlacement.ContainedBy(pc));
            session.Vitality.ApplyHitPointDamage(pc, 5);
            audio.ClearDiagnosticHistory();
            probe.ResetEvidence();
            Check(salve.Succeeded && ui.UseTechnology(salve.State.Identity, pc),
                "authoritative Healing Salve use succeeds");
            yield return null;
            AudioPlaybackRecord technology = Last(audio, AudioPresentationKind.Technology);
            Check(technology.SoundId == SourceAudioRouting.UiHerbology && technology.Source != null,
                "unmapped Healing Salve uses documented source Herbology cue");
            yield return RequireSignal(probe, "technology");
            yield return WaitForProgress(technology);

            DialogScript retailDialogue = DialogLocator.Load(audio.Bank.VirtualFileSystem, 1324);
            DialogLine voiced = retailDialogue.LineNumbers.Select(number =>
                    retailDialogue.TryGet(number, out DialogLine line) ? line : default)
                .First(line => line.IsNpcSpeech && int.TryParse(line.Test?.Trim(), out int value) && value > 0);
            var boundedVoiced = new DialogLine(voiced.Num, voiced.Text, voiced.Text2, 0,
                voiced.Test, 0, string.Empty);
            var bounded = new DialogScript(new SortedDictionary<int, DialogLine>
            {
                [voiced.Num] = boundedVoiced,
                [voiced.Num + 1] = new DialogLine(voiced.Num + 1, "e:", "", 1, "", 0, ""),
            });
            session.BindDialogueSource(_ => StartAt(voiced.Num), _ => bounded);
            audio.ClearDiagnosticHistory();
            probe.ResetEvidence();
            Check(session.Dialogue.Start(pc, Virgil) == DialogueStartStatus.Started,
                "authentic Virgil voiced line enters M5 authority");
            yield return WaitForPlayback(audio, AudioPresentationKind.Voice, 8f);
            AudioPlaybackRecord voice = Last(audio, AudioPresentationKind.Voice);
            Check(voice.VirtualPath.StartsWith("sound/speech/01324/", StringComparison.OrdinalIgnoreCase)
                  && voice.Source != null && voice.IsPlaying,
                "exact retail Virgil MP3 streams on the non-positional voice channel");
            yield return RequireSignal(probe, "dialogue voice", 5f);
            yield return WaitForProgress(voice);
            Check(session.Dialogue.Cancel("M12D validation"), "dialogue authority cancels independently");
            yield return null;
            Check(!audio.VoiceSource.isPlaying, "voice interruption stops the dedicated channel");

            Check(session.SelectSector(EquipmentSector), "authentic bow/portal sector loads");
            yield return null;
            Refresh(out loader, out audio, out uiPresenter, out lifecycle);
            audio.BindProductionPresentation();
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "PC binds in equipment sector");
            PersistentObjectState bow = session.States[Bow];
            PersistentObjectState arrows = session.States[Arrows];
            MoveOwnedItem(session, bow, pc);
            MoveOwnedItem(session, arrows, pc);
            Check(session.EquipItem(pc, Bow, WornLocation.Weapon).Succeeded,
                "authentic source bow equips through M3");

            Check(session.SelectSector(DoorSector), "authentic ordinary-door sector loads");
            yield return null;
            Refresh(out loader, out audio, out uiPresenter, out lifecycle);
            audio.BindProductionPresentation();
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "PC binds in door sector");
            PlayerInteractionController interactions = loader.GetComponent<PlayerInteractionController>();
            WorldObject portal = loader.SpriteOwners.Select(value => value.WorldObject)
                .Where(value => value != null && value.Type == ObjectType.Portal && !value.Locked
                                && !value.Off && value.SoundEffect > 0)
                .Where(value =>
                {
                    int eventId = SfxSelect.ObjectSound(value.SoundEffect,
                        value.IsOpen ? (int)PortalSound.Close : (int)PortalSound.Open);
                    return audio.Bank.Clip(eventId) != null;
                })
                .OrderBy(value => value.Identity.Key, StringComparer.Ordinal).First();
            MoveActor(session, loader, pc, lifecycle.Presentation, portal.Tile, true);
            yield return null; // allow the presentation listener to follow the authoritative PC move
            audio.ClearDiagnosticHistory();
            probe.ResetEvidence();
            WorldInteractionResult portalResult = interactions.TryUse(portal.Identity);
            Check(portalResult.IsSuccess, "authentic portal interaction succeeds");
            AudioPlaybackRecord portalSound = Last(audio, AudioPresentationKind.WorldObject);
            Check(portalSound.SoundId == portal.SoundEffect + (int)PortalSound.Open
                  || portalSound.SoundId == portal.SoundEffect + (int)PortalSound.Close,
                "portal result produces one authored object-bank sound");
            yield return RequireSignal(probe, "portal interaction");
            yield return WaitForProgress(portalSound);

            Check(session.SelectSector(BearSector), "authentic Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out audio, out uiPresenter, out lifecycle);
            audio.BindProductionPresentation();
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "PC binds for combat audio");
            Check(session.TryGetLoadedObject(Bear, out WorldObject bearRuntime), "authentic bear loads");
            Vector2Int rangedTile = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 3, 8);
            MoveActor(session, loader, pc, lifecycle.Presentation, rangedTile, true);
            yield return null; // positional audio reads the PC listener on the presentation tick
            StartPcTurn(session, pc);
            audio.ClearDiagnosticHistory();
            probe.ResetEvidence();
            CombatHitChance rangedChance = session.Combat.GetBasicRangedHitChance(pc, Bear,
                bow.WeaponData, InteractionRangeRules.Distance(rangedTile, bearRuntime.Tile));
            session.Combat.SetRandomSource(rangedChance.DodgeChance > 0
                ? new SequenceRandom(1, 100, 100, 10, 5)
                : new SequenceRandom(1, 100, 10, 5));
            CombatAttackResult ranged = session.Combat.Attack(pc, Bear, CombatAttackMode.BasicRanged);
            Check(ranged.Succeeded, "authentic Bow attack resolves through M8");
            yield return WaitForPlayback(audio, AudioPresentationKind.Attack);
            AudioPlaybackRecord bowRelease = Last(audio, AudioPresentationKind.Attack);
            Check(bowRelease.SoundId == bow.WeaponData.SoundEffect + (int)WeaponSound.Use
                  && bowRelease.Source != null,
                "Bow result presents exact authored release sound once");
            Check(audio.History.Count(value => value.Kind == AudioPresentationKind.Attack) == 1,
                "one attack result produces one attack presentation");
            yield return RequireSignal(probe, "Bow attack");
            yield return WaitForProgress(bowRelease);
            EndCombat(session, pc);

            Check(session.UnequipItem(pc, WornLocation.Weapon).Succeeded,
                "bow unequips for authentic unarmed melee proof");
            Vector2Int meleeTile = FindMeleeTile(loader.NavigationMap, bearRuntime.Tile);
            MoveActor(session, loader, pc, lifecycle.Presentation, meleeTile, true);
            yield return null;
            int bearHp = session.Vitality.GetCurrentHitPoints(Bear);
            if (bearHp > 1) session.Vitality.ApplyHitPointDamage(Bear, bearHp - 1);
            StartPcTurn(session, pc);
            audio.ClearDiagnosticHistory();
            probe.ResetEvidence();
            session.Combat.SetRandomSource(new SequenceRandom(1, 100, 4, 4));
            CombatAttackResult melee = session.Combat.Attack(pc, Bear, CombatAttackMode.BasicMelee);
            Check(melee.Succeeded && melee.Hit && melee.ResultingHitPoints <= 0,
                "authentic unarmed attack commits lethal M8D/M8E result");
            yield return WaitForPlayback(audio, AudioPresentationKind.Death);
            Check(!audio.History.Any(value => value.Kind == AudioPresentationKind.Attack)
                  && audio.History.Any(value => value.Kind is AudioPresentationKind.Impact or AudioPresentationKind.Critical)
                  && audio.History.Any(value => value.Kind == AudioPresentationKind.Death),
                "unauthored PC swing stays silent while material impact and character death route once");
            yield return RequireSignal(probe, "melee/impact/death");
            EndCombatAfterDeath(session, pc);

            probe.ResetEvidence();
            audio.ClearDiagnosticHistory();
            Check(session.SelectSector(TarantSector), "authentic Tarant sector loads");
            yield return WaitForPlayback(audio, AudioPresentationKind.Music, 10f);
            yield return WaitForPlayback(audio, AudioPresentationKind.Ambience, 5f);
            AudioPlaybackRecord music = Last(audio, AudioPresentationKind.Music);
            AudioPlaybackRecord ambience = Last(audio, AudioPresentationKind.Ambience);
            Check(audio.Music.MusicSchemeIndex == 5 && audio.Music.AmbientSchemeIndex == 33,
                "sector source selects Tarant music 5 and city ambience 33");
            Check(music.VirtualPath.EndsWith("music/Tarant.mp3", StringComparison.OrdinalIgnoreCase)
                  && music.Loop && ambience.Loop && music.Source != null && ambience.Source != null,
                "retail Tarant MP3 and city ambience loop on distinct source slots");
            yield return WaitForProgress(music);
            yield return WaitForSignal(probe, 5f);
            Check(probe.NonZeroSampleBlocks > 0 && probe.PeakMagnitude > 0f,
                "Unity listener produced objective non-zero output samples");

            AudioSource musicSource = music.Source;
            int musicSample = music.TimeSamples;
            Check(session.SelectSector(TarantArrivalSector), "same-area Tarant transition succeeds");
            yield return new WaitForSecondsRealtime(0.5f);
            Check(ReferenceEquals(audio.Music.LastMusicPlayback.Source, musicSource)
                  && audio.Music.LastMusicPlayback.TimeSamples > musicSample
                  && audio.Music.ActiveLoopCount == 2,
                "unchanged Tarant context continues without restart or duplicate loops");

            int historicalAttackCount = audio.History.Count(value => value.Kind == AudioPresentationKind.Attack);
            SessionSaveSlotResult saved = session.SaveSlots.SaveSlot(slot);
            Check(saved.Succeeded, "Save V1 records authoritative state");
            SessionSaveSlotResult loaded = session.SaveSlots.LoadSlot(slot);
            Check(loaded.Succeeded, "Save V1 restores authoritative state");
            yield return null;
            Refresh(out loader, out audio, out uiPresenter, out lifecycle);
            audio.BindProductionPresentation();
            Check(audio.History.Count(value => value.Kind == AudioPresentationKind.Attack) == historicalAttackCount
                  && !audio.VoiceSource.isPlaying && audio.Music.ActiveLoopCount <= 2,
                "save/load reconstructs context without transient attack/voice replay or duplicate loops");

            ArcanumObjectId identity = session.PlayerState.Identity;
            AudioSource beforeGraphics = audio.Music.LastMusicPlayback.Source;
            int oneShotsBefore = audio.History.Count(value => value.Kind is AudioPresentationKind.Attack
                or AudioPresentationKind.SpellCast or AudioPresentationKind.Technology);
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                uiPresenter.Controller.RebuildPresentation();
                audio.BindProductionPresentation();
            }
            Check(session.PlayerState.Identity == identity
                  && ReferenceEquals(audio.Music.LastMusicPlayback.Source, beforeGraphics)
                  && audio.Music.ActiveLoopCount <= 2
                  && audio.History.Count(value => value.Kind is AudioPresentationKind.Attack
                      or AudioPresentationKind.SpellCast or AudioPresentationKind.Technology) == oneShotsBefore,
                "Original-Enhanced-Original rebuild is audio- and authority-independent");

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M12D PHYSICAL VALIDATION PASS: source=snd/scheme/dialog retail tables; "
                      + "ui=3012; spell=12010/12015/12018; tech=3018; voice=Virgil DAT MP3; "
                      + "world=authored portal bank; ranged=authored Bow release; melee=material-impact+death/no-fabricated-swing; "
                      + "music=Tarant scheme 5; ambience=City scheme 33; transition=no-restart; "
                      + "saveLoad=no-transient-replay; graphics=Original-Enhanced-Original/no-duplicates; "
                      + $"signalPeak={probe.PeakMagnitude:0.000000}; signalBlocks={probe.NonZeroSampleBlocks}; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            if (initialMute.HasValue) SetEditorMasterMute(initialMute.Value);
            Application.logMessageReceived -= Track;
            SessionSaveSlotResult existing = session.SaveSlots.DeleteSlot(slot);
            _running = false;
        }
    }

    private static IEnumerator WaitForPlayback(ProductionAudioPresenter audio,
        AudioPresentationKind kind, float timeout = 4f)
    {
        float until = Time.realtimeSinceStartup + timeout;
        while (Time.realtimeSinceStartup < until
               && !audio.History.Any(value => value.Kind == kind && value.Source != null)) yield return null;
        AudioPlaybackRecord candidate = audio.History.LastOrDefault(value => value.Kind == kind);
        Check(candidate?.Source != null,
            $"{kind} AudioSource starts (id={candidate?.SoundId}; path={candidate?.VirtualPath ?? "<none>"}; "
            + $"resolved={candidate?.Resolved ?? false})");
    }

    private static IEnumerator WaitForProgress(AudioPlaybackRecord record)
    {
        Check(record.Source != null && record.Clip != null && record.Clip.samples > 0,
            $"{record.Kind} started a decoded clip on a concrete AudioSource");
        int before = record.TimeSamples;
        bool observedPlaying = record.IsPlaying;
        float until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until && record.TimeSamples <= before && record.IsPlaying)
        {
            observedPlaying = true;
            yield return null;
        }
        Check(record.TimeSamples > before || observedPlaying || !record.IsPlaying,
            $"{record.Kind} playback advances or completes before the next observation");
    }

    private static IEnumerator WaitForSignal(AudioOutputSignalProbe probe, float timeout)
    {
        float until = Time.realtimeSinceStartup + timeout;
        while (Time.realtimeSinceStartup < until && probe.NonZeroSampleBlocks == 0) yield return null;
    }

    private static IEnumerator RequireSignal(AudioOutputSignalProbe probe, string label, float timeout = 2f)
    {
        yield return WaitForSignal(probe, timeout);
        Check(probe.NonZeroSampleBlocks > 0 && probe.PeakMagnitude > 0f,
            $"{label} produces objective non-zero Unity output samples");
    }

    private static AudioPlaybackRecord Last(ProductionAudioPresenter audio, AudioPresentationKind kind)
        => audio.History.LastOrDefault(value => value.Kind == kind)
           ?? throw new InvalidOperationException("M12D validation FAIL: no " + kind + " record.");

    private static void Refresh(out WorldObjectSectorLoader loader, out ProductionAudioPresenter audio,
        out ProductionGameUiPresenter ui, out ProductionPlayerLifecycle lifecycle)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        audio = Object.FindFirstObjectByType<ProductionAudioPresenter>();
        ui = Object.FindFirstObjectByType<ProductionGameUiPresenter>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && audio != null && ui?.Controller != null && lifecycle != null,
            "production TestTerrain audio/UI/lifecycle composition exists exactly once");
        Check(Object.FindObjectsByType<ProductionAudioPresenter>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one production audio presenter exists");
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

    private static void MoveOwnedItem(WorldMapSessionCoordinator session, PersistentObjectState item,
        ArcanumObjectId owner)
    {
        if (item.Placement.Kind == ObjectPlacementKind.Equipped)
            Check(session.UnequipItem(item.Placement.ParentIdentity, item.Placement.WornLocation).Succeeded,
                "source item unequips before transfer");
        if (item.Placement != ObjectPlacement.ContainedBy(owner))
            Check(session.TransferItem(item.Identity, item.Placement, ObjectPlacement.ContainedBy(owner)).Succeeded,
                "authentic item transfers through inventory authority");
    }

    private static void MoveActor(WorldMapSessionCoordinator session, WorldObjectSectorLoader loader,
        ArcanumObjectId identity, WorldObject runtime, Vector2Int tile, bool controlled)
    {
        loader.NavigationMap.Unregister(runtime);
        Check(session.SetMovementState(identity, tile, runtime.ArtId, false), "validation placement is authoritative");
        loader.NavigationMap.Register(runtime, runtime.SourceFlags);
        if (controlled) loader.NavigationMap.SetControlledObject(runtime);
    }

    private static Vector2Int FindClearRangedTile(SectorNavigationMap map, Vector2Int target,
        int minimum, int maximum)
    {
        for (int distance = minimum; distance <= maximum; distance++)
        for (int y = Math.Max(0, target.y - distance); y <= Math.Min(63, target.y + distance); y++)
        for (int x = Math.Max(0, target.x - distance); x <= Math.Min(63, target.x + distance); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (InteractionRangeRules.Distance(candidate, target) == distance && map.IsWalkable(candidate)
                && map.HasProjectileLineOfFire(candidate, target)) return candidate;
        }
        throw new InvalidOperationException("M12D validation FAIL: no clear ranged tile.");
    }

    private static Vector2Int FindMeleeTile(SectorNavigationMap map, Vector2Int target)
    {
        foreach (Vector2Int delta in IsoProjection.DirDelta)
        {
            Vector2Int tile = target + delta;
            if (map.IsWalkable(tile)) return tile;
        }
        throw new InvalidOperationException("M12D validation FAIL: no melee tile.");
    }

    private static void StartPcTurn(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        Check(session.Combat.StartCombat(pc, Bear, CombatMode.TurnBased).Succeeded,
            "turn-based combat starts");
        while (session.Combat.CurrentParticipant != pc)
            Check(session.Combat.EndCurrentTurn(session.Combat.CurrentParticipant).Succeeded,
                "turn advances to the PC");
    }

    private static void EndCombat(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        foreach (ArcanumObjectId hostile in session.Combat.Participants
                     .Where(value => value.Identity != pc).Select(value => value.Identity).ToArray())
            Check(session.Combat.RemoveParticipant(hostile).Succeeded, "hostile leaves combat");
        Check(session.Combat.EndCombat(pc).Succeeded, "combat ends");
    }

    private static void EndCombatAfterDeath(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        if (!session.Combat.IsActive) return;
        foreach (ArcanumObjectId hostile in session.Combat.Participants
                     .Where(value => value.Identity != pc).Select(value => value.Identity).ToArray())
            Check(session.Combat.RemoveParticipant(hostile).Succeeded, "remaining hostile leaves lethal combat");
        Check(session.Combat.EndCombat(pc).Succeeded, "lethal combat ends");
    }

    private static ArcanumObjectId Parse(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId value))
            throw new InvalidOperationException("Invalid M12D ObjectID: " + key);
        return value;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M12D validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }

    private static bool? GetEditorMasterMute()
    {
        MethodInfo method = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil")?
            .GetMethod("GetMasterMute", BindingFlags.Static | BindingFlags.NonPublic);
        object result = method?.Invoke(null, null);
        return result is bool value ? value : null;
    }

    private static void SetEditorMasterMute(bool value)
    {
        typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil")?
            .GetMethod("SetMasterMute", BindingFlags.Static | BindingFlags.NonPublic)?
            .Invoke(null, new object[] { value });
    }

    private sealed class SequenceRandom : ICombatRandom
    {
        private readonly Queue<int> _values;
        public SequenceRandom(params int[] values) => _values = new Queue<int>(values);
        public int NextInclusive(int minimum, int maximum)
        {
            int value = _values.Count > 0 ? _values.Dequeue() : minimum;
            Check(value >= minimum && value <= maximum, "deterministic RNG remains bounded");
            return value;
        }
    }
}
