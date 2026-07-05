using System.Collections.Generic;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Text;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Pins the <c>.dlg</c> parsing + conversation logic: the engine's IQ-decides-role rule (blank/0 = NPC
    /// line, non-zero = player option), gender variants/gates, IQ gates, <c>@name@</c> expansion, and the
    /// regression where a gender-gated option (text2 = a number) was mis-read as NPC speech and truncated the
    /// option list. Pure-format tests over a synthetic dialog; see the DialogLine / DialogScript docs.
    /// </summary>
    public sealed class DialogFormatTests
    {
        // {num}{text1}{text2}{iq}{test}{goto}{effect}. Line 1 = NPC greeting (gender variants); 2–4 = options
        // (any / female-only / IQ≥15); 5 = NPC reply (no female variant → falls back to text1); 6 = end option.
        private const string Dlg =
            "{1}{Hello, sir.}{Hello, madam.}{}{}{0}{}\n" +
            "{2}{Tell me about @npcname@.}{}{1}{}{5}{}\n" +
            "{3}{A woman's question.}{0}{1}{}{5}{}\n" + // gender 0 = female-only (STAT_GENDER: female=0, male=1)
            "{4}{A clever question.}{}{15}{}{5}{}\n" +
            "{5}{I am @pcname@'s merchant.}{}{}{}{0}{}\n" +
            "{6}{Goodbye.}{}{1}{}{0}{}\n";

        private static DialogScript Parse() => DlgReader.Read(Latin1(Dlg));

        [Test]
        public void IqDecidesNpcVsOption()
        {
            DialogScript s = Parse();
            Assert.That(s.TryGet(1, out DialogLine npc) && npc.IsNpcSpeech, Is.True, "iq blank → NPC line");
            Assert.That(s.TryGet(2, out DialogLine opt) && !opt.IsNpcSpeech, Is.True, "iq non-zero → player option");
        }

        [Test]
        public void GenderGatedOptionDoesNotTruncateList() // the bug: a numeric text2 was read as NPC speech → break
        {
            var male = new Ctx { Male = true };
            List<DialogLine> options = new DialogConversation(Parse(), 1, 20, male).Options();
            CollectionAssert.Contains(Texts(options), "A clever question.",
                "the IQ≥15 option after the female-gated one must still appear");
            CollectionAssert.DoesNotContain(Texts(options), "A woman's question.", "female-only option hidden for a male PC");
        }

        [Test]
        public void OptionsRespectGenderAndIqGates()
        {
            // Female, smart: any + female-only + IQ option all show.
            var femaleSmart = new DialogConversation(Parse(), 1, 20, new Ctx { Male = false }).Options();
            CollectionAssert.AreEquivalent(
                new[] { "Tell me about Merchant.", "A woman's question.", "A clever question." }, Texts(femaleSmart));

            // Male, dull: female-only gated out by gender, clever gated out by IQ.
            var maleDull = new DialogConversation(Parse(), 1, 10, new Ctx { Male = true }).Options();
            CollectionAssert.AreEquivalent(new[] { "Tell me about Merchant." }, Texts(maleDull));
        }

        [Test]
        public void NpcTextPicksGenderVariantWithFallback()
        {
            Assert.That(new DialogConversation(Parse(), 1, 20, new Ctx { Male = true }).NpcText, Is.EqualTo("Hello, sir."));
            Assert.That(new DialogConversation(Parse(), 1, 20, new Ctx { Male = false }).NpcText, Is.EqualTo("Hello, madam."));
            // Line 5 has no female variant → a female PC falls back to text1 (and @pcname@ expands).
            Assert.That(new DialogConversation(Parse(), 5, 20, new Ctx { Male = false, Pc = "Hero" }).NpcText,
                Is.EqualTo("I am Hero's merchant."));
        }

        [Test]
        public void PickAdvancesToTarget()
        {
            var convo = new DialogConversation(Parse(), 1, 20, new Ctx { Male = true });
            DialogLine tell = convo.Options().Find(o => o.Num == 2);
            Assert.That(convo.Pick(tell), Is.True);
            Assert.That(convo.CurrentLine, Is.EqualTo(5));
            Assert.That(convo.NpcText, Does.Contain("merchant"));
        }

        [Test]
        public void GeneratedDialogText_PicksFromTokenRange()
        {
            var mes = new MesFile(new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(400, "Goodbye."),
                new KeyValuePair<int, string>(407, "Good day."),
                new KeyValuePair<int, string>(100, "No."),
            });
            var gd = new GeneratedDialogText(mes, new System.Random(1));
            string e = gd.For('e');
            Assert.That(e == "Goodbye." || e == "Good day.", $"'{e}' should come from the e: range (400–499)");
            Assert.That(gd.For('n'), Is.EqualTo("No.")); // 100–199
            Assert.That(gd.For('b'), Is.Null);           // barter has no generic text range
        }

        [Test]
        public void ExpandNameCodes()
        {
            var ctx = new Ctx { Pc = "Hero", Npc = "Merchant" };
            Assert.That(DialogText.Expand("@pcname@ meets @npcname@.", ctx), Is.EqualTo("Hero meets Merchant."));
            Assert.That(DialogText.Expand("Hi @unknown@!", ctx), Is.EqualTo("Hi !"));            // unknown code stripped
            Assert.That(DialogText.Expand("price: 5@ each", ctx), Is.EqualTo("price: 5@ each")); // unterminated → literal
            Assert.That(DialogText.Expand("plain", ctx), Is.EqualTo("plain"));
            Assert.That(DialogText.Expand(null, ctx), Is.Null);
        }

        private static List<string> Texts(List<DialogLine> lines)
        {
            // Mirror the UI: option label is the expanded text1.
            var ctx = new Ctx { Pc = "Hero", Npc = "Merchant" };
            var outp = new List<string>();
            foreach (DialogLine l in lines) outp.Add(DialogText.Expand(l.Text, ctx));
            return outp;
        }

        private static byte[] Latin1(string s)
        {
            var b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) b[i] = (byte)s[i];
            return b;
        }

        [Test]
        public void GenderZeroOptionIsFemaleOnly()
        {
            // STAT_GENDER: GENDER_FEMALE = 0, GENDER_MALE = 1 (stat.h). Verified against the shipped data —
            // e.g. 01618master_prowler.dlg option "How about buying a lady a drink?" carries gender 0.
            string dlg = "{1}{Greeting.}{}{}{}{0}{}\n" +
                         "{2}{Woman talk.}{0}{1}{}{5}{}\n" + // gender 0 → female-only
                         "{3}{Man talk.}{1}{1}{}{5}{}\n" +   // gender 1 → male-only
                         "{5}{Bye.}{}{}{}{0}{}\n";
            DialogScript s = DlgReader.Read(Latin1(dlg));
            List<string> male = Texts(new DialogConversation(s, 1, 20, new Ctx { Male = true }).Options());
            List<string> female = Texts(new DialogConversation(s, 1, 20, new Ctx { Male = false }).Options());
            CollectionAssert.Contains(male, "Man talk.");
            CollectionAssert.DoesNotContain(male, "Woman talk.");
            CollectionAssert.Contains(female, "Woman talk.");
            CollectionAssert.DoesNotContain(female, "Man talk.");
        }

        [Test]
        public void EmptyOptions_SynthesizeGeneratedGoodbye()
        {
            // Every option gated out → the engine injects a generated goodbye (sub_414E60); ending through it
            // hands control back to the SAP_DIALOG script (EndScriptLine 0 = resume after SAT_DIALOG).
            string dlg = "{1}{You bore me.}{}{}{}{0}{}\n" +
                         "{2}{Elite reply.}{}{19}{}{5}{}\n" + // IQ ≥ 19 — gated out at IQ 10
                         "{5}{Bye.}{}{}{}{0}{}\n";
            var convo = new DialogConversation(DlgReader.Read(Latin1(dlg)), 1, 10, new Ctx { Male = true });
            List<DialogLine> options = convo.Options();
            Assert.That(options.Count, Is.EqualTo(1));
            Assert.That(options[0].Num, Is.EqualTo(-1));       // synthetic
            Assert.That(options[0].Text, Is.EqualTo("Bye now.")); // Ctx.GeneratedText('e')
            Assert.That(convo.Pick(options[0]), Is.False);
            Assert.That(convo.EndScriptLine, Is.EqualTo(0));
        }

        [Test]
        public void TokenOption_ResolvesGeneratedText_AndUnrenderableTokensAreSkipped()
        {
            string dlg = "{1}{Hello.}{}{}{}{0}{}\n" +
                         "{2}{e:}{}{1}{}{0}{}\n" +   // goodbye token → generated text
                         "{3}{b:}{}{1}{}{5}{}\n" +   // barter token → no handler → skipped
                         "{4}{Ask away.}{}{1}{}{5}{}\n" +
                         "{5}{Sure.}{}{}{}{0}{}\n";
            var convo = new DialogConversation(DlgReader.Read(Latin1(dlg)), 1, 20, new Ctx { Male = true });
            List<string> texts = Texts(convo.Options());
            CollectionAssert.AreEquivalent(new[] { "Bye now.", "Ask away." }, texts);
        }

        [Test]
        public void OptionsCapAtFive()
        {
            var sb = new System.Text.StringBuilder("{1}{Pick.}{}{}{}{0}{}\n");
            for (int i = 0; i < 7; i++) sb.Append("{" + (2 + i) + "}{Option " + i + ".}{}{1}{}{0}{}\n");
            var convo = new DialogConversation(DlgReader.Read(Latin1(sb.ToString())), 1, 20, new Ctx { Male = true });
            Assert.That(convo.Options().Count, Is.EqualTo(5)); // engine DialogState.options[5]
        }

        [Test]
        public void FlEffect_IsFinalSay_ThenEndsWithoutScriptHandoff()
        {
            string dlg = "{1}{Hello.}{}{}{}{0}{}\n" +
                         "{2}{Anger the NPC.}{}{1}{}{5}{fl 9}\n" +
                         "{5}{Unreached.}{}{}{}{0}{}\n" +
                         "{9}{Get out of my sight!}{}{}{}{0}{}\n";
            var convo = new DialogConversation(DlgReader.Read(Latin1(dlg)), 1, 20, new Ctx { Male = true });
            DialogLine opt = convo.Options()[0];
            Assert.That(convo.Pick(opt), Is.True);              // the final say is shown…
            Assert.That(convo.CurrentLine, Is.EqualTo(9));
            Assert.That(convo.FinalSay, Is.True);
            List<DialogLine> last = convo.Options();            // …with only the generated goodbye left
            Assert.That(last.Count, Is.EqualTo(1));
            Assert.That(last[0].Num, Is.EqualTo(-1));
            Assert.That(convo.Pick(last[0]), Is.False);
            Assert.That(convo.EndScriptLine, Is.EqualTo(-1));   // fl ends outright — no script hand-off
        }

        [Test]
        public void NegativeTarget_EndsAndJumpsScriptToThatLine()
        {
            string dlg = "{1}{Deal?}{}{}{}{0}{}\n" +
                         "{2}{Yes.}{}{1}{}{-7}{}\n"; // engine sub_417590: negative → end + script line 7
            var convo = new DialogConversation(DlgReader.Read(Latin1(dlg)), 1, 20, new Ctx { Male = true });
            Assert.That(convo.Pick(convo.Options()[0]), Is.False);
            Assert.That(convo.EndScriptLine, Is.EqualTo(7));
        }

        [Test]
        public void EvaluatesSkillFollowAlignmentAreaConditions()
        {
            var ctx = new Ctx { Alignment = -50, Following = true };
            ctx.Basic[2] = 4; // basic skill index 2
            ctx.Tech[1] = 3;  // tech skill index 1 → .dlg value 12+1 = 13
            ctx.Areas.Add(7);

            Assert.That(DialogScriptEvaluator.TestPasses("sk2 3", ctx), Is.True);  // skill 4 ≥ 3
            Assert.That(DialogScriptEvaluator.TestPasses("sk2 5", ctx), Is.False); // skill 4 < 5
            Assert.That(DialogScriptEvaluator.TestPasses("sk13 3", ctx), Is.True); // tech skill 3 ≥ 3
            Assert.That(DialogScriptEvaluator.TestPasses("fo0", ctx), Is.True);    // 0 = must follow
            Assert.That(DialogScriptEvaluator.TestPasses("fo1", ctx), Is.False);   // 1 = must not follow
            Assert.That(DialogScriptEvaluator.TestPasses("na-40", ctx), Is.True);  // align -50 ≤ -40
            Assert.That(DialogScriptEvaluator.TestPasses("na40", ctx), Is.False);  // align -50 < -40 (need ≥)
            Assert.That(DialogScriptEvaluator.TestPasses("ar7", ctx), Is.True);    // area 7 known
            Assert.That(DialogScriptEvaluator.TestPasses("ar9", ctx), Is.False);   // area 9 not known
        }

        [Test]
        public void EvaluatesAptitudeCollegePartyAndStateFlagConditions()
        {
            var ctx = new Ctx { Aptitude = 40, CollegeLevel = 3, PartyMemberName = 6411, Jilted = false, Waiting = false, Quelled = true };

            // ma reads magick aptitude; ta reads its negation (Cmp sign convention).
            Assert.That(DialogScriptEvaluator.TestPasses("ma20", ctx), Is.True);   // 40 ≥ 20
            Assert.That(DialogScriptEvaluator.TestPasses("ma60", ctx), Is.False);  // 40 < 60
            Assert.That(DialogScriptEvaluator.TestPasses("ta-20", ctx), Is.True);  // −40 ≤ −20
            Assert.That(DialogScriptEvaluator.TestPasses("ta20", ctx), Is.False);  // −40 < 20 (need ≥)

            // sc: A = college, B = required level.
            Assert.That(DialogScriptEvaluator.TestPasses("sc4 3", ctx), Is.True);  // college level 3 ≥ 3
            Assert.That(DialogScriptEvaluator.TestPasses("sc4 4", ctx), Is.False); // 3 < 4

            // pa: party holds / lacks the named follower.
            Assert.That(DialogScriptEvaluator.TestPasses("pa6411", ctx), Is.True);   // present
            Assert.That(DialogScriptEvaluator.TestPasses("pa-6411", ctx), Is.False); // "must lack" but present
            Assert.That(DialogScriptEvaluator.TestPasses("pa9999", ctx), Is.False);  // absent

            // wt/wa: 1 = must be jilted/waiting, 0 = must not. Not jilted/waiting here.
            Assert.That(DialogScriptEvaluator.TestPasses("wt0", ctx), Is.True);   // not jilted, want-not ✓
            Assert.That(DialogScriptEvaluator.TestPasses("wt1", ctx), Is.False);  // want jilted, isn't
            Assert.That(DialogScriptEvaluator.TestPasses("wa0", ctx), Is.True);

            // rq: rumor quelled (this one is quelled).
            Assert.That(DialogScriptEvaluator.TestPasses("rq5", ctx), Is.True);    // must be quelled ✓
            Assert.That(DialogScriptEvaluator.TestPasses("rq-5", ctx), Is.False);  // must not be, but is
        }

        [Test]
        public void RunsLeavePartyEffect()
        {
            var ctx = new Ctx();
            DialogScriptEvaluator.RunEffect("lv", ctx, out _);
            Assert.That(ctx.Disbanded, Is.True);
        }

        // Minimal IDialogContext for the pure tests — only gender/names matter here; the rest is permissive.
        private sealed class Ctx : IDialogContext
        {
            public bool Male;
            public string Pc = "Hero";
            public string Npc = "Merchant";

            public bool PcIsMale => Male;
            public string PcName => Pc;
            public string NpcName => Npc;
            public string GeneratedText(char token) => token == 'e' ? "Bye now." : null;
            public int PcRace => 0;

            // Drivers for the ma/ta/sc/pa/wt/wa/rq gates.
            public int Aptitude;
            public int CollegeLevel;
            public int PartyMemberName = -1;
            public bool Jilted, Waiting, Quelled;
            public int MagickAptitude => Aptitude;
            public int SpellCollegeLevel(int college) => CollegeLevel;
            public bool PartyHasMemberNamed(int nameId) => nameId == PartyMemberName;
            public bool IsNpcJilted => Jilted;
            public bool IsNpcWaiting => Waiting;
            public bool RumorQuelled(int id) => Quelled;

            public int Intelligence => 20;
            public int Charisma => 10;
            public int Perception => 10;
            public int Level => 1;
            public int Gold { get; set; }
            public int PersuasionSkill => 0;
            public int HaggleSkill => 0;

            public int PcFlag(int index) => 0;
            public void SetPcFlag(int index, int value) { }
            public int PcVar(int index) => 0;
            public void SetPcVar(int index, int value) { }
            public int Quest(int num) => 0;
            public void SetQuest(int num, int state) { }

            public int GlobalFlag(int index) => 0;
            public void SetGlobalFlag(int index, int value) { }
            public int GlobalVar(int index) => 0;
            public void SetGlobalVar(int index, int value) { }

            public int Alignment { get; set; }
            public void AdjustAlignment(int delta) { }
            public void SetAlignment(int value) { }
            public int StoryState => 0;
            public void SetStoryState(int value) { }
            public bool RumorKnown(int id) => false;
            public void SetRumorKnown(int id) { }
            public bool HasReputation(int id) => false;
            public void AddReputation(int id) { }
            public void RemoveReputation(int id) { }
            public void MarkAreaKnown(int id) { }
            public bool HasMetNpc => false;
            public void KillNpc() { }

            public int NpcReaction => 50;
            public void AdjustReaction(int delta) { }
            public void SetReaction(int value) { }
            public int LocalFlag(int index) => 0;
            public void SetLocalFlag(int index, int value) { }
            public int LocalCounter(int index) => 0;
            public void SetLocalCounter(int index, int value) { }

            public bool HasItem(int protoNumber, bool pcSide) => false;
            public void TransferItem(int protoNumber, bool pcToNpc) { }

            public void GiveXp(int questId) { }
            public void GiveFatePoint() { }
            public void StartCombat() { }
            public void RecruitNpc() { }

            // --- newly-implemented gates (testable) ---
            public int[] Basic = new int[12];
            public int[] Tech = new int[4];
            public bool Following;
            public bool Disbanded;
            public readonly System.Collections.Generic.HashSet<int> Areas = new System.Collections.Generic.HashSet<int>();

            public int BasicSkillLevel(int skill) => Basic[skill];
            public int TechSkillLevel(int skill) => Tech[skill];
            public bool IsNpcFollowingPc => Following;
            public bool AreaKnown(int id) => Areas.Contains(id);
            public void DisbandNpc() => Disbanded = true;
        }
    }
}
