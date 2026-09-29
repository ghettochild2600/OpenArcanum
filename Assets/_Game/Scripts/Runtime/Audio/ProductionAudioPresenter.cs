using System;
using System.Collections;
using System.Collections.Generic;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Sound;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using UnityEngine;
using Material = Arcanum.Formats.Sound.Material;

namespace Arcanum.Runtime.Audio
{
    /// <summary>
    /// Central production presentation bridge. It observes completed authoritative results and translates
    /// them to source ids/paths; no audio callback can advance or alter gameplay. Transient sequence guards
    /// deliberately initialize from current authority so rebuild/save-load cannot replay historical actions.
    /// </summary>
    [RequireComponent(typeof(WorldObjectSectorLoader), typeof(WorldMapSessionCoordinator))]
    public sealed class ProductionAudioPresenter : MonoBehaviour, IGameAudioPresentation
    {
        private const int HistoryLimit = 128;
        private readonly List<AudioPlaybackRecord> _history = new();
        private WorldObjectSectorLoader _loader;
        private WorldMapSessionCoordinator _session;
        private DatVirtualFileSystem _vfs;
        private SoundBank _bank;
        private AudioService _effects;
        private MusicService _music;
        private AudioSource _voice;
        private PlayerInteractionController _interactions;
        private ProductionDialogueSession _dialogue;
        private long _processedAttackSequence;
        private long _sequence;
        private bool _combatWasActive;
        private string _voiceKey;

        public bool IsInitialized => _bank != null;
        public SoundBank Bank => _bank;
        public AudioService Effects => _effects;
        public MusicService Music => _music;
        public AudioSource VoiceSource => _voice;
        public AudioPlaybackRecord LastVoicePlayback { get; private set; }
        public IReadOnlyList<AudioPlaybackRecord> History => _history;

        public void ClearDiagnosticHistory() => _history.Clear();

        private void Awake() => BindProductionPresentation();
        private void OnEnable() => BindProductionPresentation();

        public void BindProductionPresentation()
        {
            _loader ??= GetComponent<WorldObjectSectorLoader>();
            _session ??= GetComponent<WorldMapSessionCoordinator>();
            if (_loader == null || _session == null) return;
            if (!IsInitialized && _loader.TryGetAudioSource(out _vfs)) InitializeServices();

            _session.SectorSelected -= OnSectorSelected;
            _session.SectorSelected += OnSectorSelected;
            _session.SectorUnloading -= OnSectorUnloading;
            _session.SectorUnloading += OnSectorUnloading;
            BindDialogue();

            PlayerInteractionController next = GetComponent<PlayerInteractionController>();
            if (_interactions != null) _interactions.Resolved -= OnInteractionResolved;
            _interactions = next;
            if (_interactions != null) _interactions.Resolved += OnInteractionResolved;

            _processedAttackSequence = _session.Combat.AttackResolutionSequence;
            _combatWasActive = _session.Combat.IsActive;
            GetComponent<ProductionGameUiPresenter>()?.Controller.BindAudioPresentation(this);
            if (_session.HasSelectedSector) OnSectorSelected(_session.SelectedSector);
        }

        private void InitializeServices()
        {
            _bank = SoundBank.Load(_vfs);
            _effects = GetComponent<AudioService>() ?? gameObject.AddComponent<AudioService>();
            _effects.Init(_bank, _loader.PixelsPerUnit);
            _effects.PlaybackStarted += Record;
            _music = GetComponent<MusicService>() ?? gameObject.AddComponent<MusicService>();
            _music.Init(_bank, _vfs, _effects, SourceHour);
            _music.PlaybackStarted += Record;
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0f;
            _voice.loop = false;
        }

        private float SourceHour()
            => (12f + _session.SourceTime.ElapsedMilliseconds / 3_600_000f) % 24f;

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.SectorSelected -= OnSectorSelected;
                _session.SectorUnloading -= OnSectorUnloading;
                if (_dialogue != null) _dialogue.Changed -= OnDialogueChanged;
            }
            if (_interactions != null) _interactions.Resolved -= OnInteractionResolved;
            if (_effects != null) _effects.PlaybackStarted -= Record;
            if (_music != null) _music.PlaybackStarted -= Record;
        }

        private void Update()
        {
            if (!IsInitialized) return;
            if (!ReferenceEquals(_dialogue, _session.Dialogue)) BindDialogue();
            if (_session.PlayerState != null
                && _session.TryGetLoadedObject(_session.PlayerState.Identity, out WorldObject player))
                _effects.SetListener(player.transform.position);

            bool combat = _session.Combat.IsActive;
            if (combat != _combatWasActive)
            {
                _combatWasActive = combat;
                _music.SetCombat(combat);
            }

            long sequence = _session.Combat.AttackResolutionSequence;
            if (sequence > _processedAttackSequence)
            {
                _processedAttackSequence = sequence;
                if (_session.Combat.LastAttackResult.HasValue)
                    PresentAttack(_session.Combat.LastAttackResult.Value);
            }
            else if (sequence < _processedAttackSequence)
            {
                // Save/load and combat normalization clear transient sequence state; never replay history.
                _processedAttackSequence = sequence;
            }
        }

        private void BindDialogue()
        {
            if (_dialogue != null) _dialogue.Changed -= OnDialogueChanged;
            _dialogue = _session.Dialogue;
            _dialogue.Changed -= OnDialogueChanged;
            _dialogue.Changed += OnDialogueChanged;
        }

        private void OnSectorSelected(string sector)
        {
            if (!IsInitialized || !_loader.TryGetSectorAudio(sector, out int music, out int ambient)) return;
            _music.PlayScheme(music, ambient);
        }

        private void OnSectorUnloading(string _)
        {
            StopVoice();
            // Incoming SectorSelected owns the next schemes. The old loops remain until that atomic context
            // swap, matching the source transition rather than introducing presentation-owned silence.
        }

        public void PresentInterface(InterfaceAudioCue cue, bool succeeded = true)
            => _effects?.PlayUi(SourceAudioRouting.InterfaceSound(cue, succeeded),
                AudioPresentationKind.Interface);

        public void PresentSpellCast(SpellCastRequest request, bool succeeded)
        {
            if (!succeeded) { PresentInterface(InterfaceAudioCue.InvalidAction, false); return; }
            Vector3 caster = Position(request.Caster);
            Vector3 target = Position(request.Target);
            int cast = SourceAudioRouting.SpellCast(request.SpellId);
            int impact = SourceAudioRouting.SpellImpact(request.SpellId);
            if (cast >= 0) _effects.PlayAt(cast, caster, SoundSize.Large, AudioPresentationKind.SpellCast);
            if (impact >= 0) _effects.PlayAt(impact, target, SoundSize.Large, AudioPresentationKind.SpellImpact);
        }

        public void PresentSpellEnd(int spellId)
        {
            int id = SourceAudioRouting.SpellEnd(spellId);
            if (id >= 0) _effects?.PlayAt(id, ListenerPosition(), SoundSize.Large,
                AudioPresentationKind.SpellEnd);
        }

        public void PresentTechnology(TechnologyUseRequest request, bool succeeded)
        {
            if (!succeeded) { PresentInterface(InterfaceAudioCue.InvalidAction, false); return; }
            int soundId = -1;
            if (_session.TryGetObjectState(request.Item, out PersistentObjectState item))
                soundId = item.WeaponData?.SoundEffect ?? _session.ResolvePrototype(item.PrototypeNumber)?.SoundEffect ?? -1;
            // Healing Salve has no snd_spell row in retail data. Its supported source discipline cue is the
            // Herbology interface sound; an authored item bank wins when one exists.
            if (soundId <= 0) soundId = SourceAudioRouting.UiHerbology;
            _effects?.PlayAt(soundId, Position(request.Target), SoundSize.Large,
                AudioPresentationKind.Technology);
        }

        private void OnInteractionResolved(WorldInteractionResult result)
        {
            if (!result.IsSuccess || !IsInitialized) return;
            WorldInteractionCommand command = result.Command;
            if (_session.TryGetLoadedObject(command.Target, out WorldObject runtime))
            {
                int id = -1;
                AudioPresentationKind kind = AudioPresentationKind.WorldObject;
                if (command.Type == WorldInteractionCommandType.Use)
                    id = SfxSelect.ObjectSound(runtime.SoundEffect,
                        result.RequestedPortalOpen == false ? (int)PortalSound.Close : (int)PortalSound.Open);
                else if (command.Type == WorldInteractionCommandType.PickUp)
                {
                    id = SfxSelect.ItemPickup((Material)runtime.Material, runtime.Weight,
                        runtime.Type == ObjectType.Gold);
                    kind = AudioPresentationKind.Item;
                }
                if (id > 0) _effects.PlayAt(id, runtime.transform.position, SoundSize.Large, kind);
            }
        }

        private void PresentAttack(CombatAttackResult result)
        {
            if (!result.Succeeded) return;
            Vector3 attacker = Position(result.Request.Attacker);
            Vector3 target = Position(result.EffectTargetIdentity.IsNull
                ? result.Request.Target : result.EffectTargetIdentity);
            PersistentObjectState weaponState = null;
            if (!result.WeaponIdentity.IsNull) _session.TryGetObjectState(result.WeaponIdentity, out weaponState);
            Weapon weapon = weaponState?.WeaponData;

            int attackId = weapon != null
                ? SfxSelect.ObjectSound(weapon.SoundEffect, (int)WeaponSound.Use)
                : CritterSoundId(result.Request.Attacker, CritterSound.Attacking);
            if (attackId > 0) _effects.PlayAt(attackId, attacker, SoundSize.Large,
                AudioPresentationKind.Attack);

            if (!result.Hit && !result.CriticalDodge)
            {
                int miss = weapon != null
                    ? SfxSelect.ObjectSound(weapon.SoundEffect, (int)WeaponSound.Miss) : -1;
                _effects.PlayAt(miss > 0 ? miss : SfxSelect.FallbackMissSoundId, target,
                    SoundSize.Large, AudioPresentationKind.Miss);
                return;
            }

            int hit;
            if (weapon != null && weapon.SoundEffect > 0)
            {
                int offset = result.Outcome == CombatAttackOutcome.CriticalSuccess
                    ? (int)WeaponSound.CritHit
                    : PresentationRandomInclusive(0, 1) == 0
                        ? (int)WeaponSound.Hit1 : (int)WeaponSound.Hit2;
                hit = SfxSelect.ObjectSound(weapon.SoundEffect, offset);
            }
            else
            {
                Material weaponMaterial = weapon != null ? (Material)weapon.MaterialId : Material.Flesh;
                int weight = weapon?.Weight ?? 0;
                hit = SfxSelect.MeleeHit(weaponMaterial, weight, TargetMaterial(result.EffectTargetIdentity),
                    PresentationRandomInclusive);
            }
            _effects.PlayAt(hit, target, SoundSize.Large,
                result.Outcome == CombatAttackOutcome.CriticalSuccess
                    ? AudioPresentationKind.Critical : AudioPresentationKind.Impact);

            if (result.ResultingHitPoints <= 0)
            {
                int death = CritterSoundId(result.EffectTargetIdentity, CritterSound.Dying);
                if (death > 0) _effects.PlayAt(death, target, SoundSize.Large,
                    AudioPresentationKind.Death);
            }
            else if (result.MitigatedHitPointDamage > 0)
            {
                int pain = CritterSoundId(result.EffectTargetIdentity, CritterSound.CriticallyHit);
                if (pain > 0) _effects.PlayAt(pain, target, SoundSize.Large,
                    AudioPresentationKind.Impact);
            }
        }

        private int CritterSoundId(ArcanumObjectId identity, CritterSound sound)
            => _session.TryGetLoadedObject(identity, out WorldObject runtime)
                ? SfxSelect.ObjectSound(runtime.SoundEffect, (int)sound)
                : -1;

        private static int PresentationRandomInclusive(int minimum, int maximum)
            => UnityEngine.Random.Range(minimum, maximum + 1);

        private Material TargetMaterial(ArcanumObjectId identity)
        {
            if (_session.TryGetLoadedObject(identity, out WorldObject runtime))
                return (Material)runtime.Material;
            if (_session.TryGetObjectState(identity, out PersistentObjectState state))
                return (Material)(_session.ResolvePrototype(state.PrototypeNumber)?.Material ?? (int)Material.Flesh);
            return Material.Flesh;
        }

        private void OnDialogueChanged()
        {
            ProductionDialogueSession dialogue = _session.Dialogue;
            if (dialogue.Phase != DialogueSessionPhase.AwaitingPlayerChoice || dialogue.CurrentLine <= 0)
            {
                StopVoice();
                return;
            }
            DialogScript script = DialogLocator.Load(_vfs, dialogue.DialogueNumber);
            if (script == null || !script.TryGet(dialogue.CurrentLine, out DialogLine line)
                || !int.TryParse(line.Test?.Trim(), out int voiceNumber) || voiceNumber <= 0) return;
            bool female = _session.Characters.TryGet(dialogue.NpcIdentity, out PersistentCharacterState npc)
                          && npc.Gender == CharacterGender.Female;
            string key = $"{dialogue.DialogueNumber}:{dialogue.CurrentLine}:{voiceNumber}:{female}";
            if (key == _voiceKey) return;
            _voiceKey = key;
            string path = SourceAudioRouting.VoicePath(dialogue.DialogueNumber, voiceNumber, female);
            if (!_bank.Exists(path) && female)
                path = SourceAudioRouting.MaleVoiceFallback(dialogue.DialogueNumber, voiceNumber);
            if (_bank.Exists(path)) StartCoroutine(PlayVoice(path, key));
        }

        private IEnumerator PlayVoice(string path, string key)
        {
            StopVoice(false);
            AudioClip clip = null;
            yield return _music.StreamMp3(path, value => clip = value);
            if (_voiceKey != key || clip == null) yield break;
            _voice.clip = clip;
            _voice.volume = 0.8f;
            _voice.loop = false;
            _voice.Play();
            LastVoicePlayback = new AudioPlaybackRecord
            {
                Sequence = ++_sequence, Kind = AudioPresentationKind.Voice,
                Category = AudioCategory.Voice, VirtualPath = path, Clip = clip, Source = _voice,
            };
            Record(LastVoicePlayback);
        }

        private void StopVoice(bool clearKey = true)
        {
            if (_voice != null) _voice.Stop();
            if (clearKey) _voiceKey = null;
        }

        private Vector3 Position(ArcanumObjectId identity)
        {
            if (_session.TryGetLoadedObject(identity, out WorldObject runtime))
                return runtime.transform.position;
            if (_session.TryGetObjectState(identity, out PersistentObjectState state))
                return state.TilePosition;
            return ListenerPosition();
        }

        private Vector3 ListenerPosition()
            => _session.PlayerState != null ? PositionWithoutFallback(_session.PlayerState.Identity) : transform.position;

        private Vector3 PositionWithoutFallback(ArcanumObjectId identity)
            => _session.TryGetLoadedObject(identity, out WorldObject runtime)
                ? runtime.transform.position : transform.position;

        private void Record(AudioPlaybackRecord record)
        {
            if (record == null) return;
            if (record.Sequence == 0) record.Sequence = ++_sequence;
            else _sequence = Math.Max(_sequence, record.Sequence);
            _history.Add(record);
            if (_history.Count > HistoryLimit) _history.RemoveAt(0);
        }
    }
}
