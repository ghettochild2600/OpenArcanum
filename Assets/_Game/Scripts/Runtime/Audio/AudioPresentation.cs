using System;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Technology;
using UnityEngine;

namespace Arcanum.Runtime.Audio
{
    public enum AudioCategory { Effects, Music, Ambience, Voice, Interface }

    public enum AudioPresentationKind
    {
        Interface, WorldObject, Item, Attack, Impact, Miss, Critical, Death,
        SpellCast, SpellImpact, SpellEnd, Technology, Music, Ambience, Voice,
    }

    public enum InterfaceAudioCue
    {
        ButtonClick, InvalidAction, WindowOpen, WindowClose, BookOpen, BookClose,
        BookPageTurn, BookSwitch, DialogueResponse, SaveLoad,
    }

    /// <summary>Objective presentation evidence. It describes what retail resource Unity was asked to
    /// present and, when playback started, the concrete source/clip that owns it. It is diagnostic state,
    /// never gameplay authority and is intentionally not serialized.</summary>
    public sealed class AudioPlaybackRecord
    {
        public long Sequence { get; internal set; }
        public AudioPresentationKind Kind { get; internal set; }
        public AudioCategory Category { get; internal set; }
        public int SoundId { get; internal set; } = -1;
        public string VirtualPath { get; internal set; }
        public bool Positional { get; internal set; }
        public bool Loop { get; internal set; }
        public Vector3 WorldPosition { get; internal set; }
        public AudioClip Clip { get; internal set; }
        public AudioSource Source { get; internal set; }
        public bool Resolved => Clip != null;
        public bool IsPlaying => Source != null && Source.isPlaying;
        public int TimeSamples => Source != null ? Source.timeSamples : 0;
    }

    /// <summary>Narrow M12D presentation boundary used by the game UI. Commands resolve first; audio is
    /// merely informed of the completed authoritative result.</summary>
    public interface IGameAudioPresentation
    {
        void PresentInterface(InterfaceAudioCue cue, bool succeeded = true);
        void PresentSpellCast(SpellCastRequest request, bool succeeded);
        void PresentSpellEnd(int spellId);
        void PresentTechnology(TechnologyUseRequest request, bool succeeded);
    }

    /// <summary>Source id/path rules recovered from snd.h, snd_interface.mes, snd_spell.mes and
    /// dialog.c. Keeping the arithmetic here makes the routing deterministic and directly testable.</summary>
    public static class SourceAudioRouting
    {
        public const int UiButtonClick = 3000;
        public const int UiInvalidAction = 3004;
        public const int UiBookOpen = 3008;
        public const int UiBookClose = 3009;
        public const int UiBookPageTurn = 3010;
        public const int UiBookSwitch = 3011;
        public const int UiWindowOpen = 3012;
        public const int UiWindowClose = 3013;
        public const int UiHerbology = 3018;

        public static int InterfaceSound(InterfaceAudioCue cue, bool succeeded)
        {
            if (!succeeded) return UiInvalidAction;
            return cue switch
            {
                InterfaceAudioCue.InvalidAction => UiInvalidAction,
                InterfaceAudioCue.WindowOpen => UiWindowOpen,
                InterfaceAudioCue.WindowClose => UiWindowClose,
                InterfaceAudioCue.BookOpen => UiBookOpen,
                InterfaceAudioCue.BookClose => UiBookClose,
                InterfaceAudioCue.BookPageTurn => UiBookPageTurn,
                InterfaceAudioCue.BookSwitch => UiBookSwitch,
                _ => UiButtonClick,
            };
        }

        /// <summary>snd_spell.mes key: 9000 + college*1000 + rank*10 + phase offset.</summary>
        public static int SpellSound(int spellId, int phaseOffset)
        {
            if (!PhaseOneSpellCatalog.TryGet(spellId, out SpellDefinition spell)) return -1;
            return 9000 + (int)spell.College * 1000 + spell.Rank * 10 + phaseOffset;
        }

        public static int SpellCast(int spellId) => SpellSound(spellId, 0);
        public static int SpellImpact(int spellId) => SpellSound(spellId, 5);
        public static int SpellEnd(int spellId) => SpellSound(spellId, 8);

        /// <summary>Retail dialogue voice naming from dialog.c. Female playback falls back to the male
        /// path when the female resource is absent.</summary>
        public static string VoicePath(int dialogueNumber, int voiceNumber, bool female)
            => $"sound/speech/{dialogueNumber:00000}/v{voiceNumber}_{(female ? 'f' : 'm')}.mp3";

        public static string MaleVoiceFallback(int dialogueNumber, int voiceNumber)
            => VoicePath(dialogueNumber, voiceNumber, false);
    }

    /// <summary>Validation-only Unity mix probe. It samples the post-listener output and records objective
    /// non-zero signal evidence without feeding any result back into gameplay.</summary>
    [AddComponentMenu("")]
    public sealed class AudioOutputSignalProbe : MonoBehaviour
    {
        private readonly float[] _samples = new float[1024];
        public float PeakMagnitude { get; private set; }
        public int NonZeroSampleBlocks { get; private set; }
        public int SampledBlocks { get; private set; }

        public static float Peak(float[] samples)
        {
            if (samples == null) return 0f;
            float peak = 0f;
            for (int index = 0; index < samples.Length; index++)
                peak = Mathf.Max(peak, Mathf.Abs(samples[index]));
            return peak;
        }

        public void ResetEvidence()
        {
            PeakMagnitude = 0f;
            NonZeroSampleBlocks = 0;
            SampledBlocks = 0;
        }

        private void Update()
        {
            AudioListener.GetOutputData(_samples, 0);
            float peak = Peak(_samples);
            SampledBlocks++;
            if (peak > 0.000001f) NonZeroSampleBlocks++;
            PeakMagnitude = Mathf.Max(PeakMagnitude, peak);
        }
    }
}
