using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;

namespace Arcanum.Runtime.Dialogue
{
    /// <summary>Reads the generated source tables used by dialog.c's training subviews.</summary>
    public sealed class SourceTrainingDialogueText : ITrainingDialogueTextSource
    {
        private readonly WorldMapSessionCoordinator _world;
        private readonly Dictionary<string, MesFile> _tables = new(StringComparer.OrdinalIgnoreCase);
        private readonly MesFile _skills;
        private readonly Random _random;

        public SourceTrainingDialogueText(WorldMapSessionCoordinator world, Func<string, MesFile> load,
            Random random = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            if (load == null) throw new ArgumentNullException(nameof(load));
            foreach (string name in new[]
                     {
                         "gd_pc2m", "gd_pc2f", "gd_dumb_pc2m", "gd_dumb_pc2f",
                         "gd_cls_pc2m", "gd_cls_pc2f", "gd_cls_dumb_pc2m", "gd_cls_dumb_pc2f",
                         "gd_cls_m2m", "gd_cls_m2f", "gd_cls_f2m", "gd_cls_f2f",
                         "gd_cls_dumb_m2m", "gd_cls_dumb_m2f", "gd_cls_dumb_f2m", "gd_cls_dumb_f2f",
                     })
                _tables.Add(name, load($"mes/{name}.mes")
                                  ?? throw new InvalidOperationException($"Missing source generated-dialog table {name}."));
            _skills = load("mes/skill.mes")
                      ?? throw new InvalidOperationException("Missing source skill message table.");
            _random = random ?? new Random();
        }

        public string NpcClassMessage(ArcanumObjectId npc, ArcanumObjectId pc, int sourceKey)
        {
            PersistentCharacterState npcState = _world.Characters.Get(npc);
            PersistentCharacterState pcState = _world.Characters.Get(pc);
            bool dumb = npcState.GetEffective(CharacterAttribute.Intelligence) <= 4;
            string name = "gd_cls_" + (dumb ? "dumb_" : string.Empty)
                          + (npcState.Gender == CharacterGender.Male ? "m2" : "f2")
                          + (pcState.Gender == CharacterGender.Male ? "m" : "f");
            int socialClass = !dumb && _world.TryGetObjectState(npc, out PersistentObjectState source)
                ? source.SocialClass : 0;
            return InRange(_tables[name], sourceKey + 50 * socialClass,
                sourceKey + 50 * socialClass + 49);
        }

        public string PcClassMessage(ArcanumObjectId npc, ArcanumObjectId pc, int sourceKey)
        {
            PersistentCharacterState npcState = _world.Characters.Get(npc);
            PersistentCharacterState pcState = _world.Characters.Get(pc);
            bool dumb = pcState.GetEffective(CharacterAttribute.Intelligence) <= 4;
            string name = "gd_cls_" + (dumb ? "dumb_" : string.Empty)
                          + "pc2" + (npcState.Gender == CharacterGender.Male ? "m" : "f");
            int socialClass = !dumb && _world.TryGetObjectState(npc, out PersistentObjectState source)
                ? source.SocialClass : 0;
            return InRange(_tables[name], sourceKey + 50 * socialClass,
                sourceKey + 50 * socialClass + 49);
        }

        public string PcGenericMessage(ArcanumObjectId npc, ArcanumObjectId pc, int firstKey, int lastKey)
        {
            PersistentCharacterState npcState = _world.Characters.Get(npc);
            PersistentCharacterState pcState = _world.Characters.Get(pc);
            bool dumb = pcState.GetEffective(CharacterAttribute.Intelligence) <= 4;
            string name = "gd_" + (dumb ? "dumb_" : string.Empty)
                          + "pc2" + (npcState.Gender == CharacterGender.Male ? "m" : "f");
            return InRange(_tables[name], firstKey, lastKey);
        }

        public string SkillName(CharacterSkill skill)
        {
            CharacterSkillRules.ValidateSkill(skill);
            return _skills.Get((int)skill)?.Trim();
        }

        private string InRange(MesFile table, int firstKey, int lastKey)
        {
            var present = new List<string>();
            foreach (var entry in table.Entries)
                if (entry.Key >= firstKey && entry.Key <= lastKey && !string.IsNullOrWhiteSpace(entry.Value))
                    present.Add(entry.Value.Trim());
            return present.Count == 0 ? null : present[_random.Next(present.Count)];
        }
    }
}
