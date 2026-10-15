using System;
using System.Linq;
using Arcanum.Formats.Script;
using Arcanum.Script;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    [TestFixture]
    [Category("ScriptCompatibility")]
    public sealed class ScriptCompatibilityAuditTests
    {
        [Test]
        public void EveryKnownRetailOpcodeHasAnExplicitCompatibilityClassification()
        {
            foreach (Sct condition in Enum.GetValues(typeof(Sct)).Cast<Sct>())
                Assert.That(ScriptOpcodeCompatibility.Condition(condition).SourceLocation, Is.Not.Empty,
                    $"condition {condition}");
            foreach (Sat action in Enum.GetValues(typeof(Sat)).Cast<Sat>())
                Assert.That(ScriptOpcodeCompatibility.Action(action).SourceLocation, Is.Not.Empty,
                    $"action {action}");
        }

        [Test]
        public void KnownStrictVmFallbacksRemainVisibleToTheAudit()
        {
            Assert.That(ScriptOpcodeCompatibility.Condition(Sct.ObjHasReputation).Status,
                Is.EqualTo(ScriptOpcodeSupportStatus.Implemented));
            Assert.That(ScriptOpcodeCompatibility.Action(Sat.TransferItem).Status,
                Is.EqualTo(ScriptOpcodeSupportStatus.Implemented));
            Assert.That(ScriptOpcodeCompatibility.Action(Sat.FadeAndTeleport).Status,
                Is.EqualTo(ScriptOpcodeSupportStatus.Partial));
            Assert.That(ScriptOpcodeCompatibility.Action(Sat.SetPcQuestState).Status,
                Is.EqualTo(ScriptOpcodeSupportStatus.Implemented));
        }
    }
}
