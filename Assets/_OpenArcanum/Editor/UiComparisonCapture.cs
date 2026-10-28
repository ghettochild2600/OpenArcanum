using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace OpenArcanum.Editor
{
    [InitializeOnLoad]
    internal static class UiComparisonCapture
    {
        private static bool _startingEquipmentCaptured;
        private static bool _startingEquipmentDragQueued;
        private static RetailStartingEquipmentView _startingEquipmentDragView;
        private static int _startingEquipmentDragDelayFrames;
        private static RetailStartingEquipmentDragTarget _startingEquipmentDragTarget;
        private static PointerEventData _startingEquipmentDropPointer;
        private static int _startingEquipmentDropDelayFrames;

        static UiComparisonCapture()
            => EditorApplication.update += CaptureStartingEquipmentOnce;

        [MenuItem("OpenArcanum/UI Comparison/Capture ST 8 at 800x600 _F7")]
        private static void CaptureStrengthEight()
            => SetStrengthAndCapture(8);

        [MenuItem("OpenArcanum/UI Comparison/Capture ST 10 at 800x600 _F10")]
        private static void CaptureStrengthTen()
            => SetStrengthAndCapture(10);

        [MenuItem("OpenArcanum/UI Comparison/Capture ST 20 at 800x600 _F12")]
        private static void CaptureStrengthTwenty()
            => SetStrengthAndCapture(20);

        [MenuItem("OpenArcanum/UI Comparison/Capture Current 800x600 _F6")]
        private static void CaptureCurrent()
        {
            Canvas.ForceUpdateCanvases();
            Capture("openarcanum-current-800x600.png");
        }

        [MenuItem("OpenArcanum/UI Comparison/Enter Starting Shopkeeper _F11")]
        private static void EnterStartingShopkeeper()
        {
            if (!EditorApplication.isPlaying) return;
            ProductionGameUiPresenter presenter = Object.FindFirstObjectByType<ProductionGameUiPresenter>();
            if (presenter?.Controller?.CreationDraft == null) return;
            presenter.Controller.FinalizeNewGame();
        }

        private static void CaptureStartingEquipmentOnce()
        {
            if (_startingEquipmentCaptured || !EditorApplication.isPlaying) return;
            RetailStartingEquipmentView view = Object.FindFirstObjectByType<RetailStartingEquipmentView>();
            if (view?.IsVisible != true) return;
            _startingEquipmentCaptured = true;
            Canvas.ForceUpdateCanvases();
            Capture("openarcanum-shopkeeper-followup-800x600.png");
            CaptureStartingEquipmentState();
            QueueStartingEquipmentDragCapture(view);
        }

        private static void QueueStartingEquipmentDragCapture(RetailStartingEquipmentView view)
        {
            if (_startingEquipmentDragQueued || view == null) return;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "UiComparison",
                "openarcanum-shopkeeper-followup-merchant-active-drag-800x600.png"));
            if (File.Exists(path)) return;
            _startingEquipmentDragQueued = true;
            _startingEquipmentDragView = view;
            _startingEquipmentDragDelayFrames = 12;
            EditorApplication.update += AwaitStartingEquipmentDragCapture;
        }

        private static void AwaitStartingEquipmentDragCapture()
        {
            if (!EditorApplication.isPlaying || _startingEquipmentDragView == null)
            {
                EditorApplication.update -= AwaitStartingEquipmentDragCapture;
                _startingEquipmentDragView = null;
                return;
            }
            if (--_startingEquipmentDragDelayFrames > 0) return;
            EditorApplication.update -= AwaitStartingEquipmentDragCapture;
            RetailStartingEquipmentView view = _startingEquipmentDragView;
            _startingEquipmentDragView = null;
            BeginStartingEquipmentDragCapture(view);
        }

        private static void BeginStartingEquipmentDragCapture(RetailStartingEquipmentView view)
        {
            if (!EditorApplication.isPlaying || view == null) return;
            RetailStartingEquipmentDragTarget target = view
                .GetComponentsInChildren<RetailStartingEquipmentDragTarget>(includeInactive: false)
                .FirstOrDefault(value => value != null && value.gameObject.activeInHierarchy
                    && value.name.StartsWith("BuyFromMerchant", StringComparison.Ordinal));
            if (target == null || EventSystem.current == null) return;

            RectTransform item = (RectTransform)target.transform;
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, item.TransformPoint(item.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = start };
            target.OnBeginDrag(pointer);
            Vector2 before = item.anchoredPosition;
            pointer.position = start + new Vector2(96f, 48f);
            target.OnDrag(pointer);
            if ((item.anchoredPosition - before).sqrMagnitude < 1f)
                item.anchoredPosition = before + new Vector2(96f, 48f);
            Canvas.ForceUpdateCanvases();
            Capture("openarcanum-shopkeeper-followup-merchant-active-drag-800x600.png");

            // Complete the same merchant-grid drag in a later rendered frame. Source restores the
            // authored merchant placement rather than treating this as an inventory relocation.
            _startingEquipmentDragTarget = target;
            _startingEquipmentDropPointer = new PointerEventData(EventSystem.current)
            {
                position = start + new Vector2(96f, -48f),
            };
            _startingEquipmentDropDelayFrames = 12;
            EditorApplication.update += AwaitStartingEquipmentDropCapture;
        }

        private static void AwaitStartingEquipmentDropCapture()
        {
            if (!EditorApplication.isPlaying || _startingEquipmentDragTarget == null)
            {
                EditorApplication.update -= AwaitStartingEquipmentDropCapture;
                _startingEquipmentDragTarget = null;
                _startingEquipmentDropPointer = null;
                return;
            }
            if (--_startingEquipmentDropDelayFrames > 0) return;
            EditorApplication.update -= AwaitStartingEquipmentDropCapture;
            RetailStartingEquipmentDragTarget target = _startingEquipmentDragTarget;
            PointerEventData pointer = _startingEquipmentDropPointer;
            _startingEquipmentDragTarget = null;
            _startingEquipmentDropPointer = null;
            target.OnEndDrag(pointer);
            Canvas.ForceUpdateCanvases();
            Capture("openarcanum-shopkeeper-followup-merchant-restored-800x600.png");
        }

        [MenuItem("OpenArcanum/UI Comparison/Dump Starting Shopkeeper State _F9")]
        private static void CaptureStartingEquipmentState()
        {
            ProductionGameUiPresenter presenter = Object.FindFirstObjectByType<ProductionGameUiPresenter>();
            if (presenter?.Controller == null) return;
            GameUiController controller = presenter.Controller;
            WorldMapSessionCoordinator session = Object.FindFirstObjectByType<WorldMapSessionCoordinator>();
            if (session == null) return;
            var builder = new StringBuilder();
            builder.AppendLine($"merchant={controller.MerchantTarget}");
            if (session.TryGetObjectState(controller.MerchantTarget, out var merchant))
                builder.AppendLine($"merchantProto={merchant.PrototypeNumber}; source={merchant.InventorySourceId}; gold={session.GetGold(controller.MerchantTarget)}");
            if (session.Economy.TryResolveMerchant(controller.MerchantTarget, out var inventoryOwner, out var inventorySource))
                builder.AppendLine($"inventoryOwner={inventoryOwner}; source={inventorySource.Id}; name={inventorySource.Name}; coins={inventorySource.MinimumGold}-{inventorySource.MaximumGold}; entries={string.Join(",", inventorySource.Entries.Select(value => $"{value.ChancePercent}:{value.PrototypeNumber}"))}");
            foreach (GameUiMerchantItemView value in controller.ProjectMerchantWares())
                builder.AppendLine($"merchantItem={value.Item.PrototypeNumber};{value.Item.Name};qty={value.Item.Quantity};loc={value.Item.InventoryLocation};footprint={value.Item.InventoryFootprint.Width}x{value.Item.InventoryFootprint.Height};price={value.UnitPrice}");
            foreach (GameUiMerchantItemView value in controller.ProjectPlayerSaleItems())
                builder.AppendLine($"playerItem={value.Item.PrototypeNumber};{value.Item.Name};qty={value.Item.Quantity};loc={value.Item.InventoryLocation};equipped={value.Item.IsEquipped};weight={value.Item.Weight}");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "UiComparison"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "openarcanum-shopkeeper-state.txt"), builder.ToString());
            WriteStartingInventoryMatrix(session, directory);
            WriteMerchantStockMatrix(session, controller, directory);
        }

        private static void WriteStartingInventoryMatrix(WorldMapSessionCoordinator session, string directory)
        {
            CharacterCreationCatalog catalog = session.CharacterCreation.Catalog;
            if (catalog == null) return;
            const string evidence = "rules/backgrnd.mes lines 10n+2/+3/+4; arcanum-ce background_generate_inventory/item_generate_inventory";
            var identities = Enum.GetValues(typeof(CharacterRace)).Cast<CharacterRace>()
                .Where(CharacterCreationRules.IsPlayableRace)
                .SelectMany(race => Enum.GetValues(typeof(CharacterGender)).Cast<CharacterGender>()
                    .Where(sex => CharacterCreationRules.IsGenderLegal(race, sex))
                    .Select(sex => new { Race = race, Sex = sex })).ToArray();
            var rows = new StringBuilder();
            rows.AppendLine("background_id,background_name,source_eligibility,player_eligible,legal_race_sex,starting_gold,authored_item_ids,authored_item_names,authored_items_identified,source_evidence");
            foreach (BackgroundDefinition background in catalog.Backgrounds.Values.OrderBy(value => value.Id))
            {
                string[] legal = identities.Where(value => background.IsLegal(value.Race, value.Sex))
                    .Select(value => $"{value.Race} {value.Sex}").ToArray();
                string ids = string.Join("|", background.StartingItems);
                string names = string.Join("|", background.StartingItems.Select(session.ResolvePrototypeDisplayName));
                rows.AppendLine(string.Join(",", background.Id, Csv(background.Name), Csv(background.Conditions),
                    legal.Length > 0 ? "true" : "false", Csv(string.Join("|", legal)), background.StartingGold,
                    Csv(ids), Csv(names), background.StartingItems.Count > 0 ? "true" : "n/a", Csv(evidence)));
            }
            File.WriteAllText(Path.Combine(directory, "starting-inventory-background-rules.csv"), rows.ToString());

            var clothing = new StringBuilder();
            clothing.AppendLine("race,sex,default_clothing_id,default_clothing_name,equipped,source_evidence");
            foreach (var identity in identities)
            {
                CharacterRace race = identity.Race;
                CharacterGender sex = identity.Sex;
                int prototype = CharacterCreationRules.DefaultClothingPrototype(race, sex);
                clothing.AppendLine(string.Join(",", race, sex, prototype,
                    Csv(session.ResolvePrototypeDisplayName(prototype)), "false",
                    Csv("arcanum-ce item_generate_inventory; suppressed when an authored starting item occupies natural armor slot 0")));
            }
            File.WriteAllText(Path.Combine(directory, "starting-inventory-clothing-rules.csv"), clothing.ToString());

            var conditional = new StringBuilder();
            conditional.AppendLine("predicate,item_id,item_name,quantity,equipped,identified,source_evidence");
            conditional.AppendLine(string.Join(",", Csv("effective Pick Locks points > 0"),
                CharacterCreationRules.CrudeLockpicksPrototype,
                Csv(session.ResolvePrototypeDisplayName(CharacterCreationRules.CrudeLockpicksPrototype)), 1, "false", "false",
                Csv("arcanum-ce item_generate_inventory")));
            conditional.AppendLine(string.Join(",", Csv("effective Heal points > 0"),
                CharacterCreationRules.BandagesPrototype,
                Csv(session.ResolvePrototypeDisplayName(CharacterCreationRules.BandagesPrototype)), 1, "false", "false",
                Csv("arcanum-ce item_generate_inventory")));
            File.WriteAllText(Path.Combine(directory, "starting-inventory-conditional-rules.csv"), conditional.ToString());

            var classes = new StringBuilder();
            classes.AppendLine("class_id,starting_gold,authored_item_ids,background_ids,background_names,eligibility_records,source_evidence");
            int classId = 1;
            foreach (var group in catalog.Backgrounds.Values.OrderBy(value => value.Id)
                         .Where(background => identities.Any(value => background.IsLegal(value.Race, value.Sex)))
                         .GroupBy(value => $"{value.StartingGold}:{string.Join("|", value.StartingItems)}"))
            {
                BackgroundDefinition first = group.First();
                classes.AppendLine(string.Join(",", classId++, first.StartingGold,
                    Csv(string.Join("|", first.StartingItems)),
                    Csv(string.Join("|", group.Select(value => value.Id))),
                    Csv(string.Join("|", group.Select(value => value.Name))),
                    Csv(string.Join("|", group.Select(value => $"{value.Id}:{value.Conditions}"))), Csv(evidence)));
            }
            File.WriteAllText(Path.Combine(directory, "starting-inventory-equivalence-classes.csv"), classes.ToString());
        }

        private static void WriteMerchantStockMatrix(WorldMapSessionCoordinator session, GameUiController controller,
            string directory)
        {
            if (!session.Economy.TryResolveMerchant(controller.MerchantTarget, out _, out var source)) return;
            var rows = new StringBuilder();
            rows.AppendLine("source_order,chance_percent,prototype_id,item_name,source_evidence");
            for (int index = 0; index < source.Entries.Count; index++)
            {
                var entry = source.Entries[index];
                rows.AppendLine(string.Join(",", index, entry.ChancePercent, entry.PrototypeNumber,
                    Csv(session.ResolvePrototypeDisplayName(entry.PrototypeNumber)),
                    Csv("rules/InvenSource.mes row 19; arcanum-ce sub_463E20 in declared order")));
            }
            File.WriteAllText(Path.Combine(directory, "starting-shopkeeper-stock-source.csv"), rows.ToString());
        }

        private static string Csv(string value)
            => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

        private static void SetStrengthAndCapture(int value)
        {
            ProductionGameUiPresenter presenter = Object.FindFirstObjectByType<ProductionGameUiPresenter>();
            RetailCharacterCreationView view = Object.FindFirstObjectByType<RetailCharacterCreationView>();
            if (presenter?.Controller?.CreationDraft == null || view == null)
            {
                Debug.LogError("UI comparison capture requires the live Character Creation editor.");
                return;
            }

            presenter.Controller.CreationDraft.SetAttribute(CharacterAttribute.Strength, value);
            view.Synchronize();
            Canvas.ForceUpdateCanvases();
            Capture($"openarcanum-st{value}-800x600.png");
        }

        private static void Capture(string fileName)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("UI comparison capture requires Play Mode.");
                return;
            }

            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "UiComparison"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log($"UI comparison capture queued: {path}");
        }
    }
}
