using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Text;
using Arcanum.Runtime;
using Arcanum.Runtime.Art;
using UnityEditor;
using UnityEngine;

internal static class RetailUiReferenceExporter
{
    private const string InterfaceMesPath = "art/interface/interface.mes";
    private const string ReferenceRoot = @"D:\OpenArcanum\UIReference\Original";
    private const string ManifestPath = "documentation_unity/ui/ui-asset-manifest.csv";
    private const string SourceRoot = @"D:\OpenArcanum\Research\Repositories\arcanum-ce\src";

    private static readonly Regex LiteralInterfaceId = new Regex(
        @"tig_art_interface_id_create\s*\(\s*(\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, string> SourceScreens =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["anim_ui.c"] = "Animation UI",
            ["broadcast_ui.c"] = "Party",
            ["charedit_ui.c"] = "Character Creation",
            ["combat_ui.c"] = "Combat HUD",
            ["compact_ui.c"] = "Gameplay HUD",
            ["cyclic_ui.c"] = "Character / Skills / Magic / Technology",
            ["dialog_ui.c"] = "Dialogue",
            ["fate_ui.c"] = "Fate Points",
            ["follower_ui.c"] = "Party",
            ["hotkey_ui.c"] = "Quick Slots",
            ["intgame.c"] = "Gameplay HUD",
            ["inven_ui.c"] = "Inventory / Equipment / Merchant / Loot",
            ["item_ui.c"] = "Item Inspection",
            ["logbook_ui.c"] = "Journal / Logbook",
            ["mainmenu_ui.c"] = "Main Menu / Character Creation / Save / Load / Options",
            ["options_ui.c"] = "Options",
            ["roller_ui.c"] = "Roller / Confirmation",
            ["schematic_ui.c"] = "Crafting / Schematics",
            ["scrollbar_ui.c"] = "Common",
            ["skill_ui.c"] = "Skills",
            ["sleep_ui.c"] = "Sleep",
            ["spell_ui.c"] = "Magic",
            ["tb_ui.c"] = "Combat HUD",
            ["tech_ui.c"] = "Technology",
            ["wmap_ui.c"] = "World Map",
            ["written_ui.c"] = "Written / Inspection",
        };

    [MenuItem("OpenArcanum/UI Audit/Export Retail UI References")]
    private static void Export()
    {
        Directory.CreateDirectory(ReferenceRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath) ?? "documentation_unity/ui");

        using var vfs = new DatVirtualFileSystem();
        MountRetailData(vfs);
        if (!vfs.Exists(InterfaceMesPath))
            throw new FileNotFoundException($"Retail UI table '{InterfaceMesPath}' was not found.");

        byte[] interfaceTableBytes = vfs.ReadAllBytes(InterfaceMesPath);
        MesFile interfaceTable = MesReader.Read(interfaceTableBytes);
        File.WriteAllBytes(Path.Combine(ReferenceRoot, "interface.mes"), interfaceTableBytes);
        Dictionary<int, List<SourceUse>> sourceUses = BuildSourceUses();
        var manifest = new List<ManifestRow>(interfaceTable.Count);
        int decoded = 0;
        int failed = 0;

        foreach (KeyValuePair<int, string> entry in interfaceTable.Entries.OrderBy(pair => pair.Key))
        {
            string sourcePath = ResolveArtPath(entry.Value);
            var row = new ManifestRow
            {
                SourceAssetId = entry.Key.ToString(CultureInfo.InvariantCulture),
                SourcePath = sourcePath,
                SourceFormat = "ART",
                State = "other",
                OriginalX = "unknown",
                OriginalY = "unknown",
                EnhancedReplacementExpected = "yes",
                NineSliceSuitability = "unverified",
            };

            ApplySourceUses(entry.Key, sourceUses, row);
            ApplyNameClassification(entry.Key, row);
            ApplyNineSliceClassification(entry.Key, row);
            try
            {
                if (!vfs.Exists(sourcePath))
                    throw new FileNotFoundException("The interface table target is absent from the mounted retail data.");

                ArtFile art = ArtReader.Read(vfs.ReadAllBytes(sourcePath));
                if (art.PrimaryPalette == null)
                    throw new InvalidDataException("The ART resource has no embedded palette.");

                ArtFrame primary = art.Rotations[0].Frames[0];
                row.Width = primary.Width.ToString(CultureInfo.InvariantCulture);
                row.Height = primary.Height.ToString(CultureInfo.InvariantCulture);
                row.OriginalLogicalWidth = row.Width;
                row.OriginalLogicalHeight = row.Height;
                row.AlphaPresent = HasTransparentPixels(art) ? "yes" : "no";
                row.DecodedReferencePath = ExportFrames(entry.Key, sourcePath, art);
                row.EnhancedReplacementPath = BuildEnhancedPath(entry.Key, sourcePath);
                row.Notes = art.FramesPerRotation > 1 || art.RotationCount > 1
                    ? $"{art.Palettes.Count} palette(s), {art.RotationCount} rotation(s), {art.FramesPerRotation} frame(s) per rotation; preserve registration across the complete family."
                    : $"{art.Palettes.Count} palette(s), one static frame.";
                if (sourcePath.IndexOf("font", StringComparison.OrdinalIgnoreCase) >= 0)
                    row.State = "glyph-set";
                else if (art.FramesPerRotation > 1)
                    row.State = "multi-frame family";
                decoded++;
            }
            catch (Exception ex)
            {
                row.Width = "unknown";
                row.Height = "unknown";
                row.AlphaPresent = "unknown";
                row.OriginalLogicalWidth = "unknown";
                row.OriginalLogicalHeight = "unknown";
                row.EnhancedReplacementPath = BuildEnhancedPath(entry.Key, sourcePath);
                row.Notes = "EXPORT ERROR: " + SingleLine(ex.Message);
                failed++;
            }

            manifest.Add(row);
        }

        WriteManifest(ManifestPath, manifest);
        WriteManifest(Path.Combine(ReferenceRoot, "ui-source-index.csv"), manifest);
        WriteReadme(interfaceTable.Count, decoded, failed);
        AssetDatabase.Refresh();
        Debug.Log(
            $"Retail UI reference export complete: tableEntries={interfaceTable.Count}, decoded={decoded}, failed={failed}, " +
            $"referenceRoot='{ReferenceRoot}', manifest='{ManifestPath}'. Retail PNGs remain outside Git.");
    }

    private static void MountRetailData(DatVirtualFileSystem vfs)
    {
        // DatVirtualFileSystem is first-mount-wins. Module and later retail patch archives therefore mount first.
        string[] archives =
        {
            "modules/Arcanum.dat",
            "arcanum4.dat",
            "arcanum3.dat",
            "arcanum2.dat",
            "arcanum1.dat",
        };

        int mounted = 0;
        foreach (string archive in archives)
        {
            string path = GameDataLocator.Find(archive);
            if (string.IsNullOrEmpty(path)) continue;
            vfs.MountFile(path);
            mounted++;
        }

        if (mounted == 0)
            throw new FileNotFoundException("No retail Arcanum DAT archives were found through GameDataLocator.");
    }

    private static string ResolveArtPath(string tableValue)
    {
        string value = (tableValue ?? string.Empty).Trim().Replace('\\', '/');
        if (!value.EndsWith(".art", StringComparison.OrdinalIgnoreCase)) value += ".art";
        if (!value.StartsWith("art/", StringComparison.OrdinalIgnoreCase)) value = "art/interface/" + value;
        return DatFileEntry.Normalize(value);
    }

    private static string ExportFrames(int sourceId, string sourcePath, ArtFile art)
    {
        string stem = SafeStem(Path.GetFileNameWithoutExtension(sourcePath));
        string relativeDirectory = Path.Combine("art", "interface", $"{sourceId:D4}-{stem}");
        string outputDirectory = Path.Combine(ReferenceRoot, relativeDirectory);
        Directory.CreateDirectory(outputDirectory);

        for (int paletteIndex = 0; paletteIndex < art.Palettes.Count; paletteIndex++)
        {
            ArtPalette palette = art.Palettes[paletteIndex];
            for (int rotation = 0; rotation < art.RotationCount; rotation++)
            {
                ArtFrame[] frames = art.Rotations[rotation].Frames;
                for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                {
                    Texture2D texture = ArtTextureFactory.CreateTexture(frames[frameIndex], palette);
                    try
                    {
                        string fileName = $"p{paletteIndex:D2}-r{rotation:D2}-f{frameIndex:D3}.png";
                        File.WriteAllBytes(Path.Combine(outputDirectory, fileName), texture.EncodeToPNG());
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                }
            }
        }

        return ForwardSlashes(Path.Combine(ReferenceRoot, relativeDirectory, "p00-r00-f000.png"));
    }

    private static bool HasTransparentPixels(ArtFile art)
    {
        foreach (ArtRotation rotation in art.Rotations)
            foreach (ArtFrame frame in rotation.Frames)
                if (Array.IndexOf(frame.Indices, (byte)0) >= 0)
                    return true;
        return false;
    }

    private static string BuildEnhancedPath(int sourceId, string sourcePath)
    {
        string stem = SafeStem(Path.GetFileNameWithoutExtension(sourcePath));
        return $"HDAssets/ui/by-source-id/{sourceId:D4}-{stem}/p00-r00-f000.png";
    }

    private static Dictionary<int, List<SourceUse>> BuildSourceUses()
    {
        var uses = new Dictionary<int, List<SourceUse>>();
        if (!Directory.Exists(SourceRoot)) return uses;

        foreach (string path in Directory.EnumerateFiles(SourceRoot, "*.c", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(path);
            string screen = SourceScreens.TryGetValue(fileName, out string mapped) ? mapped : "unknown";
            string[] lines = File.ReadAllLines(path);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                foreach (Match match in LiteralInterfaceId.Matches(lines[lineIndex]))
                {
                    int id = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    if (!uses.TryGetValue(id, out List<SourceUse> list))
                    {
                        list = new List<SourceUse>();
                        uses.Add(id, list);
                    }

                    string relative = ForwardSlashes(path.Substring(SourceRoot.Length).TrimStart(Path.DirectorySeparatorChar));
                    if (!list.Any(use => use.RelativePath == relative && use.Line == lineIndex + 1))
                        list.Add(new SourceUse(screen, relative, lineIndex + 1));
                }
            }
        }

        return uses;
    }

    private static void ApplySourceUses(int id, Dictionary<int, List<SourceUse>> uses, ManifestRow row)
    {
        if (!uses.TryGetValue(id, out List<SourceUse> list) || list.Count == 0)
        {
            row.Screen = "unknown";
            row.ComponentRole = "unknown";
            row.ReusedAcrossScreens = "unknown";
            row.SourceEvidence = $"Retail {InterfaceMesPath} {{{id}}}; no literal call-site match (indirect/table-driven use remains possible).";
            return;
        }

        string[] screens = list.Select(use => use.Screen).Distinct(StringComparer.Ordinal).OrderBy(value => value).ToArray();
        row.Screen = string.Join("; ", screens);
        row.ComponentRole = InferRole(list);
        row.ReusedAcrossScreens = screens.Length > 1 ? "yes" : "no";
        row.SourceEvidence = $"Retail {InterfaceMesPath} {{{id}}}; " + string.Join(
            "; ",
            list.Take(8).Select(use => $"arcanum-ce/src/{use.RelativePath}:{use.Line}"));
        if (list.Count > 8) row.SourceEvidence += $"; +{list.Count - 8} additional literal call site(s)";
    }

    private static string InferRole(List<SourceUse> uses)
    {
        string evidence = string.Join(" ", uses.Select(use => use.RelativePath)).ToLowerInvariant();
        if (evidence.Contains("scrollbar")) return "scrollbar";
        if (evidence.Contains("combat")) return "combat presentation";
        if (evidence.Contains("hotkey")) return "quick-slot presentation";
        if (evidence.Contains("dialog")) return "dialogue presentation";
        if (evidence.Contains("inven")) return "inventory presentation";
        if (evidence.Contains("mainmenu")) return "menu or modal presentation";
        return "source-referenced presentation";
    }

    private static void ApplyNameClassification(int sourceId, ManifestRow row)
    {
        if (!string.Equals(row.Screen, "unknown", StringComparison.Ordinal)) return;

        string name = Path.GetFileNameWithoutExtension(row.SourcePath).ToLowerInvariant();
        string screen = null;
        string role = null;
        if (name.Contains("cursor") || name.StartsWith("cur_") || name.Contains("hover") || name.StartsWith("scroll-"))
        {
            screen = "Common / Cursors";
            role = "cursor or targeting feedback";
        }
        else if (name.Contains("mainmenu") || name.Contains("singleplayer") || name.Contains("multiplayerback") || name == "menuplaque")
        {
            screen = "Main Menu";
            role = "menu presentation";
        }
        else if (name.Contains("option"))
        {
            screen = "Options";
            role = "options presentation";
        }
        else if (name.Contains("saveload") || name.Contains("savegame") || name.Contains("loadgame"))
        {
            screen = "Save / Load";
            role = "save/load presentation";
        }
        else if (name.Contains("dialog") || name.Contains("talk"))
        {
            screen = "Dialogue";
            role = "dialogue presentation";
        }
        else if (name.Contains("barter") || name.Contains("gamble"))
        {
            screen = "Merchant / Barter";
            role = "barter presentation";
        }
        else if (name.Contains("inven") || name.StartsWith("inv_") || name.StartsWith("cvr_"))
        {
            screen = "Inventory / Equipment";
            role = "inventory or equipment presentation";
        }
        else if (name.Contains("book") || name.Contains("log_") || name.StartsWith("quest") || name.StartsWith("rumor"))
        {
            screen = "Journal / Logbook";
            role = "journal presentation";
        }
        else if (name.Contains("wmap") || name.Contains("mapmain") || name.Contains("mapnote") || name.StartsWith("wm_"))
        {
            screen = "World Map";
            role = "world-map presentation";
        }
        else if (name.Contains("tmap") || name.Contains("townmap") || name.EndsWith("map"))
        {
            screen = "Local Map";
            role = "local-map presentation";
        }
        else if (name.StartsWith("slp_") || name.Contains("sleep"))
        {
            screen = "Sleep";
            role = "sleep presentation";
        }
        else if (name.Contains("fate"))
        {
            screen = "Fate Points";
            role = "fate presentation";
        }
        else if (name.Contains("scheme") || name.Contains("schematic") || name.Contains("combine"))
        {
            screen = "Crafting / Schematics";
            role = "crafting presentation";
        }
        else if (name.Contains("tech"))
        {
            screen = "Technology";
            role = "technology presentation";
        }
        else if (name.Contains("spell") || (sourceId <= 128 && name.StartsWith("s_")))
        {
            screen = "Magic";
            role = "spell presentation";
        }
        else if (name.StartsWith("s_"))
        {
            screen = "Crafting / Schematics";
            role = "schematic illustration";
        }
        else if (name.Contains("skill"))
        {
            screen = "Skills";
            role = "skill presentation";
        }
        else if (name.Contains("char") || name.Contains("race") || name.Contains("gender") || name.Contains("portrait"))
        {
            screen = "Character / Character Creation";
            role = "character presentation";
        }
        else if (name.Contains("combat") || name.StartsWith("ap_") || name.Contains("calledshot"))
        {
            screen = "Combat HUD";
            role = "combat presentation";
        }
        else if (name.Contains("follow") || name.Contains("party") || name.Contains("broadcast"))
        {
            screen = "Party";
            role = "party presentation";
        }
        else if (name.Contains("scroll") || name.StartsWith("scrll"))
        {
            screen = "Common";
            role = "scrollbar component";
        }
        else if (name.Contains("font"))
        {
            screen = "Common / Typography";
            role = "bitmap font or glyph set";
        }
        else if (name == "inttop" || name == "intbotom" || name.Contains("vial") || name.Contains("hotkey") || name.Contains("ammo_icon"))
        {
            screen = "Gameplay HUD";
            role = "HUD presentation";
        }
        else if (name == "intrface")
        {
            screen = "unknown / legacy composite candidate";
            role = "unproven composite/reference asset";
        }

        if (screen == null) return;
        row.Screen = screen;
        row.ComponentRole = role;
        row.SourceEvidence += "; source-name classification only; confirm table-driven call site before implementation.";
    }

    private static void ApplyNineSliceClassification(int sourceId, ManifestRow row)
    {
        switch (sourceId)
        {
            case 354:
                row.NineSliceSuitability = "yes: 8 px fixed perimeter; scalable center/edges; provisional minimum 16x16";
                break;
            case 822:
                row.NineSliceSuitability = "horizontal only: preserve 16 px end ornaments; provisional minimum 32x136";
                break;
            case 238:
            case 240:
            case 787:
                row.NineSliceSuitability = "no: source-authentic 11 px tiled scrollbar assembly component";
                break;
            default:
                row.NineSliceSuitability = "unverified; fixed-size by default";
                break;
        }
    }

    private static void WriteManifest(string path, IEnumerable<ManifestRow> rows)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var output = new StringBuilder();
        output.AppendLine(
            "SourceAssetId,SourcePath,SourceFormat,DecodedReferencePath,Width,Height,AlphaPresent,Screen,ComponentRole,State," +
            "OriginalLogicalWidth,OriginalLogicalHeight,OriginalX,OriginalY,ReusedAcrossScreens,RelatedAssetIds,SourceEvidence," +
            "EnhancedReplacementExpected,EnhancedReplacementPath,NineSliceSuitability,Notes");
        foreach (ManifestRow row in rows)
        {
            output.AppendLine(string.Join(",", new[]
            {
                row.SourceAssetId, row.SourcePath, row.SourceFormat, row.DecodedReferencePath, row.Width, row.Height,
                row.AlphaPresent, row.Screen, row.ComponentRole, row.State, row.OriginalLogicalWidth,
                row.OriginalLogicalHeight, row.OriginalX, row.OriginalY, row.ReusedAcrossScreens,
                row.RelatedAssetIds, row.SourceEvidence, row.EnhancedReplacementExpected, row.EnhancedReplacementPath,
                row.NineSliceSuitability, row.Notes,
            }.Select(Csv)));
        }

        File.WriteAllText(path, output.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteReadme(int entries, int decoded, int failed)
    {
        var text = new StringBuilder();
        text.AppendLine("OpenArcanum retail UI reference export");
        text.AppendLine("====================================");
        text.AppendLine();
        text.AppendLine($"Source table: {InterfaceMesPath}");
        text.AppendLine($"Entries: {entries}; decoded: {decoded}; failed: {failed}");
        text.AppendLine("PNG identity: <source-id>-<source-stem>/p<palette>-r<rotation>-f<frame>.png");
        text.AppendLine("Palette index 0 pixels are exported as transparent RGBA pixels.");
        text.AppendLine("These retail-derived images are local references only and must never be committed or redistributed.");
        File.WriteAllText(Path.Combine(ReferenceRoot, "README.txt"), text.ToString(), new UTF8Encoding(false));
    }

    private static string SafeStem(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char ch in value.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '-');
        return builder.ToString();
    }

    private static string ForwardSlashes(string value) => value.Replace('\\', '/');
    private static string SingleLine(string value) => (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');

    private static string Csv(string value)
    {
        value ??= string.Empty;
        return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
    }

    private readonly struct SourceUse
    {
        public readonly string Screen;
        public readonly string RelativePath;
        public readonly int Line;

        public SourceUse(string screen, string relativePath, int line)
        {
            Screen = screen;
            RelativePath = relativePath;
            Line = line;
        }
    }

    private sealed class ManifestRow
    {
        public string SourceAssetId;
        public string SourcePath;
        public string SourceFormat;
        public string DecodedReferencePath;
        public string Width;
        public string Height;
        public string AlphaPresent;
        public string Screen;
        public string ComponentRole;
        public string State;
        public string OriginalLogicalWidth;
        public string OriginalLogicalHeight;
        public string OriginalX;
        public string OriginalY;
        public string ReusedAcrossScreens;
        public string RelatedAssetIds;
        public string SourceEvidence;
        public string EnhancedReplacementExpected;
        public string EnhancedReplacementPath;
        public string NineSliceSuitability;
        public string Notes;
    }
}
