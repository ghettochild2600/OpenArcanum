using System;
using System.Collections.Generic;
using NUnit.Framework;
using Arcanum.Formats.Sound;
using Arcanum.Formats.Text;

namespace Arcanum.Formats.Tests
{
    /// <summary>Locks the audio data layer: the snd-table manifest resolution (gsound_resolve_path
    /// order), the scheme mini-language, the sfx.c selection key math, and the WAV decoder.</summary>
    public class SoundFormatsTests
    {
        private static MesFile Mes(params (int key, string text)[] entries)
        {
            var list = new List<KeyValuePair<int, string>>();
            foreach ((int key, string text) in entries) list.Add(new KeyValuePair<int, string>(key, text));
            return new MesFile(list);
        }

        [Test]
        public void SoundTable_ManifestOrder_FirstHitWins()
        {
            var files = new Dictionary<string, MesFile>
            {
                ["sound/snd_00index.mes"] = Mes((1, "snd_a.mes"), (2, "snd_b.mes")),
                ["sound/snd_a.mes"] = Mes((100, "a.wav")),
                ["sound/snd_b.mes"] = Mes((100, "b.wav"), (200, "only_b.wav")),
                ["sound/snd_user.mes"] = Mes((300, "user.wav")),
            };
            SoundTable t = SoundTable.Load(p => files.TryGetValue(p, out MesFile m) ? m : null);

            Assert.AreEqual("sound/a.wav", t.Resolve(100)); // snd_a searched before snd_b
            Assert.AreEqual("sound/only_b.wav", t.Resolve(200));
            Assert.AreEqual("sound/user.wav", t.Resolve(300)); // module file is the last fallback
            Assert.IsNull(t.Resolve(999));
        }

        [Test]
        public void Schemes_ParseTheMiniLanguage()
        {
            var table = SoundSchemeTable.Read(
                Mes((5, "M.City (Tarrant) #500")),
                Mes((500, @"music\Tarant.mp3 /loop /vol:50 /time:5-19"),
                    (501, "rooster.wav /freq:30 /time:6-8 /scatter:100"),
                    (502, "DRIP1.WAV /bal:1-100 /vol:10-20 /freq:10")));

            SoundScheme scheme = table.Get(5);
            Assert.AreEqual("M.City (Tarrant)", scheme.Name);
            Assert.AreEqual(3, scheme.Entries.Count);

            SchemeEntry music = scheme.Entries[0];
            Assert.IsTrue(music.IsLoop);
            Assert.AreEqual("music/Tarant.mp3", music.File);
            Assert.AreEqual(50, music.VolMin);
            Assert.IsTrue(music.InHourWindow(12));
            Assert.IsFalse(music.InHourWindow(22));

            SchemeEntry rooster = scheme.Entries[1];
            Assert.IsFalse(rooster.IsMusic);
            Assert.AreEqual(30, rooster.Frequency);
            Assert.AreEqual(100, rooster.Scatter);

            SchemeEntry drip = scheme.Entries[2];
            Assert.AreEqual(1, drip.BalMin);
            Assert.AreEqual(100, drip.BalMax);
            Assert.AreEqual(10, drip.VolMin);
            Assert.AreEqual(20, drip.VolMax);
        }

        [Test]
        public void Schemes_WrappingHourWindow()
        {
            var table = SoundSchemeTable.Read(
                Mes((1, "night #10")), Mes((10, "village_night.wav /loop /time:19-0")));
            SchemeEntry e = table.Get(1).Entries[0];
            Assert.IsTrue(e.InHourWindow(22));
            Assert.IsTrue(e.InHourWindow(0));
            Assert.IsFalse(e.InHourWindow(12));
        }

        [Test]
        public void SfxSelect_KeyMath()
        {
            Func<int, int, int> zero = (lo, hi) => lo;

            Assert.AreEqual(2920, SfxSelect.Footstep(0, 3, zero));                    // stone, no armor
            Assert.AreEqual(2924, SfxSelect.Footstep(SfxSelect.ArmorChain, 3, zero)); // chainmail +4 on stone
            Assert.AreEqual(2916, SfxSelect.Footstep(SfxSelect.ArmorChain, 2, zero)); // no chain bank on snow
            Assert.AreEqual(2900, SfxSelect.Footstep(SfxSelect.ArmorPlate, 3, zero)); // plate ignores surface

            // 7000 + weaponClass·20 + targetClass·3: metal (heavy=2, but 1000 stones demotes to light=1)
            // vs flesh (0) → 7000 + 20 + 0.
            Assert.AreEqual(7020, SfxSelect.MeleeHit(Material.Metal, 1000, Material.Flesh, zero));
            // Heavy metal (weight 5000) vs stone target (3) → 7000 + 40 + 9.
            Assert.AreEqual(7049, SfxSelect.MeleeHit(Material.Metal, 5000, Material.Stone, zero));

            Assert.AreEqual(5958, SfxSelect.ItemPickup(Material.Metal, 9999, isGold: true));
            Assert.AreEqual(5965, SfxSelect.ItemDrop(Material.Glass, 100, isGold: false)); // 5960 + GLASS(5)
            Assert.AreEqual(-1, SfxSelect.ObjectSound(0, 3)); // unauthored base → no sound
        }

        [Test]
        public void WavPcm_Decodes16BitStereo()
        {
            // A minimal 44-byte-header PCM WAV: 2 frames of 16-bit stereo at 22050 Hz.
            short[] pcm = { 1000, -1000, 32767, -32768 };
            byte[] data = new byte[pcm.Length * 2];
            Buffer.BlockCopy(pcm, 0, data, 0, data.Length);
            var bytes = new List<byte>();
            void Str(string s) { foreach (char c in s) bytes.Add((byte)c); }
            void I32(int v) => bytes.AddRange(BitConverter.GetBytes(v));
            void I16(short v) => bytes.AddRange(BitConverter.GetBytes(v));
            Str("RIFF"); I32(36 + data.Length); Str("WAVE");
            Str("fmt "); I32(16); I16(1); I16(2); I32(22050); I32(22050 * 4); I16(4); I16(16);
            Str("data"); I32(data.Length); bytes.AddRange(data);

            PcmData result = WavPcm.Decode(bytes.ToArray());
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Channels);
            Assert.AreEqual(22050, result.SampleRate);
            Assert.AreEqual(2, result.FrameCount);
            Assert.AreEqual(32767f / 32768f, result.Samples[2], 1e-4f);
            Assert.AreEqual(-1f, result.Samples[3], 1e-4f);
        }

        [Test]
        public void SoundParams_AttenuationAndPan()
        {
            SoundParams p = SoundParams.Read(null); // engine defaults
            Assert.AreEqual(127, p.Volume(SoundSize.Large, 100));  // inside min radius (150)
            Assert.AreEqual(0, p.Volume(SoundSize.Large, 900));    // beyond max radius (800)
            int mid = p.Volume(SoundSize.Large, 475);              // halfway 150..800
            Assert.IsTrue(mid > 55 && mid < 72, $"midpoint volume was {mid}");

            Assert.AreEqual(SoundParams.BalanceCenter, p.Balance(0f));
            Assert.AreEqual(0, p.Balance(-500f));   // far left
            Assert.AreEqual(127, p.Balance(500f));  // far right
        }
    }
}
