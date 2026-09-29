using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M12BCharacterCreationValidation
{
    private const string StartSector = "maps/arcanum1-024-fixed/86570436012.sec";
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M12B/Run Physical PlayMode Validation #&v", false, 4)]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M12B harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        WorldMapSessionCoordinator session = loader.Session;
        string baseline = session.PlayerState != null && session.HasSelectedSector
            ? session.SaveGames.SerializeCurrentSession() : null;
        Application.logMessageReceived += Track;
        try
        {
            var human = new CharacterCreationSpecification
            {
                Name = "M12B Human", Race = CharacterRace.Human, Gender = CharacterGender.Male,
                BackgroundId = 3, PortraitId = 1005,
            };
            human.SetAttribute(CharacterAttribute.Strength, 9);
            human.SetSkillPoints(CharacterSkill.Melee, 1);
            human.SetSpellRank(SpellCollege.Earth, 1);
            human.SetTechnologyRank(TechnologyDiscipline.Herbology, 1);
            CharacterCreationValidationResult validation = session.CharacterCreation.Validate(human);
            Check(validation.Succeeded && validation.SpentCharacterPoints == 4
                  && validation.RemainingCharacterPoints == 1,
                "Human choices account for exactly four spent and one unspent Character Point");

            CharacterCreationFinalizeResult finalized = session.CharacterCreation.FinalizeNewGame(human);
            Check(finalized.Succeeded, "authentic Human finalization: " + finalized.Message);
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC presentation binds");
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            Check(session.SelectedSector == StartSector && session.PlayerState.TilePosition == new Vector2(30, 32),
                "campaign enters retail START_MAP 1 at global (92958,82592), local (30,32)");
            Check(session.Characters.GetBaseAttribute(pc, CharacterAttribute.Strength) == 9
                  && session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Strength) == 9
                  && session.Progression.GetUnspentCharacterPoints(pc) == 1,
                "M4 owns exact finalized attributes and remaining Character Points");
            Check(session.Magic.KnowsSpell(pc, PhaseOneSpellCatalog.StrengthOfEarth)
                  && session.Technology.GetLearnedDegree(pc, TechnologyDiscipline.Herbology)
                     == TechnologyDegree.Novice,
                "M10A spell and M10B discipline selections are authoritative");
            PersistentObjectState armor = session.States.Values.Single(value => value.PrototypeNumber == 8157);
            Check(session.GetGold(pc) == 400 && armor.ParentIdentity == pc
                  && armor.Placement.Kind == ObjectPlacementKind.Equipped
                  && armor.Placement.WornLocation == WornLocation.Armor,
                "Raised by Elves starts with exact 400 Gold and equipped prototype 8157");
            Check(lifecycle.Presentation != null && lifecycle.Presentation.Identity == pc
                  && lifecycle.Presentation.ArtId == session.PlayerState.ArtId,
                "production presentation projects the authoritative Human identity and ART");

            string newGame = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(newGame).Succeeded, "immediate Save V1 load succeeds");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(session.CharacterCreation.IsFinalized
                  && session.CharacterCreation.Finalized.Name == "M12B Human"
                  && session.GetGold(pc) == 400
                  && session.Magic.KnowsSpell(pc, PhaseOneSpellCatalog.StrengthOfEarth)
                  && session.Technology.GetLearnedDegree(pc, TechnologyDiscipline.Herbology)
                     == TechnologyDegree.Novice
                  && session.SelectedSector == StartSector,
                "Save V1 restores creation identity, runtime choices, inventory, and campaign start");

            uint humanArt = session.PlayerState.ArtId;
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                yield return null;
                Check(session.PlayerState.Identity == pc && session.PlayerState.ArtId == humanArt
                      && session.GetGold(pc) == 400 && session.SelectedSector == StartSector,
                    $"{mode} presentation rebuild preserves PC identity and New Game authority");
            }

            session.ResetAuthoritativeSession();
            var dwarf = new CharacterCreationSpecification
            {
                Name = "M12B Dwarf", Race = CharacterRace.Dwarf, Gender = CharacterGender.Male,
                BackgroundId = 0, PortraitId = 1001,
            };
            Check(session.CharacterCreation.FinalizeNewGame(dwarf).Succeeded,
                "authentic non-Human Dwarf finalizes through the same pipeline");
            yield return null;
            Check(session.Characters.Get(pc).Race == CharacterRace.Dwarf
                  && session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Strength) == 9
                  && ((session.PlayerState.ArtId >> 24) & 7) == 1
                  && session.GetGold(pc) == 400 && session.SelectedSector == StartSector,
                "Dwarf race modifier, body ART, default background Gold, and campaign entry are exact");

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M12B PHYSICAL VALIDATION PASS: "
                      + "human=background-3+portrait-1005; cp=4-spent/1-unspent; "
                      + "magic=Earth-1; technology=Herbology-Novice; inventory=8157-equipped; gold=400; "
                      + "start=map-1/86570436012.sec/local-30,32; saveV1=round-trip; "
                      + "dwarf=male+portrait-1001+body-1+Strength-9; "
                      + "presentation=Original->Enhanced->Original-independent; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            if (baseline != null && session.PlayerState != null && session.HasSelectedSector)
                session.SaveGames.LoadJson(baseline);
            _running = false;
        }
    }

    private static void Refresh(out WorldObjectSectorLoader loader, out ProductionPlayerLifecycle lifecycle)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && lifecycle != null, "production TestTerrain composition remains available");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M12B validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
