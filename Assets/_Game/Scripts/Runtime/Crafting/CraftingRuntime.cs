using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.World;

namespace Arcanum.Runtime.Crafting
{
    public readonly struct SchematicId : IEquatable<SchematicId>, IComparable<SchematicId>
    {
        public int Value { get; }
        public SchematicId(int value)
        {
            if (value < 2000 || value > 5999 || value % 10 != 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public bool Equals(SchematicId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SchematicId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(SchematicId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
        public static implicit operator int(SchematicId value) => value.Value;
    }

    public enum SchematicAcquisition { BuiltIn, Found }

    public sealed class SchematicDefinition
    {
        public SchematicId Id { get; }
        public int NameTextId { get; }
        public string Name { get; }
        public int DescriptionTextId { get; }
        public string Description { get; }
        public int ArtNumber { get; }
        public IReadOnlyList<int> ComponentOneAliases { get; }
        public IReadOnlyList<int> ComponentTwoAliases { get; }
        public IReadOnlyList<int> ProductAliases { get; }
        public int ProductQuantity { get; }
        public SchematicAcquisition Acquisition { get; }
        public TechnologyDiscipline? BuiltInDiscipline { get; }
        public TechnologyDegree? BuiltInDegree { get; }

        internal SchematicDefinition(SchematicId id, int nameTextId, string name, int descriptionTextId,
            string description, int artNumber, int[] first, int[] second, int[] products, int quantity,
            SchematicAcquisition acquisition, TechnologyDiscipline? discipline, TechnologyDegree? degree)
        {
            Id = id; NameTextId = nameTextId; Name = name; DescriptionTextId = descriptionTextId;
            Description = description; ArtNumber = artNumber; ComponentOneAliases = first;
            ComponentTwoAliases = second; ProductAliases = products; ProductQuantity = quantity;
            Acquisition = acquisition; BuiltInDiscipline = discipline; BuiltInDegree = degree;
        }
    }

    public sealed class SchematicCatalog
    {
        private readonly Dictionary<int, SchematicDefinition> _definitions;
        public IReadOnlyDictionary<int, SchematicDefinition> Definitions => _definitions;
        private SchematicCatalog(Dictionary<int, SchematicDefinition> definitions) => _definitions = definitions;
        public bool TryGet(SchematicId id, out SchematicDefinition definition)
            => _definitions.TryGetValue(id.Value, out definition);
        public bool Contains(SchematicId id) => _definitions.ContainsKey(id.Value);

        public static SchematicCatalog FromMes(MesFile rules, MesFile text)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (text == null) throw new ArgumentNullException(nameof(text));
            var definitions = new Dictionary<int, SchematicDefinition>();
            foreach (KeyValuePair<int, string> row in rules.Entries)
            {
                int id = row.Key;
                if (id < 2000 || id > 5999 || id % 10 != 0 || definitions.ContainsKey(id)) continue;
                if (!TryInt(rules.Get(id), out int nameId) || !TryInt(rules.Get(id + 1), out int descriptionId)
                    || !TryInt(rules.Get(id + 2), out int art) || !TryAliases(rules.Get(id + 3), out int[] first)
                    || !TryAliases(rules.Get(id + 4), out int[] second)
                    || !TryAliases(rules.Get(id + 5), out int[] products)
                    || !TryInt(rules.Get(id + 6), out int quantity) || quantity < 1)
                    throw new FormatException($"Schematic {id} is incomplete or malformed.");
                SchematicAcquisition acquisition = id < 4000 ? SchematicAcquisition.BuiltIn : SchematicAcquisition.Found;
                TechnologyDiscipline? discipline = null;
                TechnologyDegree? degree = null;
                if (acquisition == SchematicAcquisition.BuiltIn)
                {
                    int relative = id - 1990;
                    int d = relative / 200;
                    int r = relative % 200 / 10;
                    if (d < 0 || d > 7 || r < 1 || r > 7
                        || id != PhaseOneTechnologyCatalog.BuiltInSchematicId((TechnologyDiscipline)d, (TechnologyDegree)r))
                        throw new FormatException($"Built-in schematic {id} has no source degree mapping.");
                    discipline = (TechnologyDiscipline)d;
                    degree = (TechnologyDegree)r;
                }
                definitions.Add(id, new SchematicDefinition(new SchematicId(id), nameId, text.Get(nameId),
                    descriptionId, text.Get(descriptionId), art, first, second, products, quantity,
                    acquisition, discipline, degree));
            }
            return new SchematicCatalog(definitions);
        }

        private static bool TryInt(string value, out int result)
            => int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        private static bool TryAliases(string value, out int[] aliases)
        {
            aliases = null;
            string[] tokens = (value ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 1 || tokens.Length > 3) return false;
            var parsed = new int[3];
            for (int i = 0; i < tokens.Length; i++)
                if (!int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed[i])
                    || parsed[i] <= 0) return false;
            for (int i = tokens.Length; i < 3; i++) parsed[i] = parsed[tokens.Length - 1];
            aliases = parsed;
            return true;
        }
    }

    public enum SchematicLearningFailure
    {
        None, InvalidCharacter, NotPlayerCharacter, WrittenItemNotFound, WrittenItemNotOwned,
        NotASchematic, UnknownSchematic, AlreadyKnown, InventoryFailure,
    }
    public readonly struct SchematicLearningResult
    {
        public SchematicLearningFailure Failure { get; }
        public bool Succeeded => Failure == SchematicLearningFailure.None;
        public SchematicId Schematic { get; }
        public bool ItemConsumed { get; }
        internal SchematicLearningResult(SchematicLearningFailure failure, SchematicId schematic = default,
            bool consumed = false) { Failure = failure; Schematic = schematic; ItemConsumed = consumed; }
    }
    public readonly struct CraftingRequest
    {
        public ArcanumObjectId Crafter { get; }
        public SchematicId Schematic { get; }
        public CraftingRequest(ArcanumObjectId crafter, SchematicId schematic)
        { Crafter = crafter; Schematic = schematic; }
    }
    public readonly struct CraftingComponentSelection
    {
        public ArcanumObjectId Item { get; }
        public int AliasIndex { get; }
        public int PrototypeNumber { get; }
        internal CraftingComponentSelection(ArcanumObjectId item, int aliasIndex, int prototype)
        { Item = item; AliasIndex = aliasIndex; PrototypeNumber = prototype; }
    }
    public enum CraftingFailure
    {
        None, InvalidCrafter, CrafterDead, UnknownSchematic, SchematicNotKnown,
        MissingComponentOne, MissingComponentTwo, InsufficientExpertise, UnsupportedComponent,
        ProductUnavailable, InventoryFailure,
    }
    public readonly struct CraftingResult
    {
        public CraftingFailure Failure { get; }
        public bool Succeeded => Failure == CraftingFailure.None;
        public CraftingRequest Request { get; }
        public CraftingComponentSelection ComponentOne { get; }
        public CraftingComponentSelection ComponentTwo { get; }
        public int ProductPrototype { get; }
        public int ProductQuantity { get; }
        public IReadOnlyList<ArcanumObjectId> Products { get; }
        internal CraftingResult(CraftingFailure failure, CraftingRequest request,
            CraftingComponentSelection first = default, CraftingComponentSelection second = default,
            int product = 0, int quantity = 0, IReadOnlyList<ArcanumObjectId> products = null)
        { Failure = failure; Request = request; ComponentOne = first; ComponentTwo = second;
          ProductPrototype = product; ProductQuantity = quantity; Products = products ?? Array.Empty<ArcanumObjectId>(); }
    }

    public sealed class CraftingStateService
    {
        public const int WrittenSchematicSubtype = 5;
        private readonly WorldMapSessionCoordinator _world;
        private readonly Dictionary<ArcanumObjectId, List<int>> _found = new();
        private SchematicCatalog _catalog;
        internal CraftingStateService(WorldMapSessionCoordinator world)
            => _world = world ?? throw new ArgumentNullException(nameof(world));
        public void BindCatalog(SchematicCatalog catalog)
            => _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        internal bool IsDefined(SchematicId id) => _catalog?.Contains(id) == true;
        public IReadOnlyList<SchematicId> GetFoundSchematics(ArcanumObjectId identity)
            => _found.TryGetValue(identity, out List<int> values)
                ? values.Select(value => new SchematicId(value)).ToArray() : Array.Empty<SchematicId>();
        public bool Knows(ArcanumObjectId identity, SchematicId id)
        {
            if (_catalog == null || !_catalog.TryGet(id, out SchematicDefinition definition)) return false;
            if (definition.Acquisition == SchematicAcquisition.BuiltIn)
                return _world.Technology.KnowsBuiltInSchematic(identity,
                    definition.BuiltInDiscipline.Value, definition.BuiltInDegree.Value);
            return _found.TryGetValue(identity, out List<int> values) && values.Contains(id.Value);
        }

        public SchematicLearningResult LearnFoundSchematic(ArcanumObjectId character, ArcanumObjectId writtenItem)
        {
            if (!_world.Characters.TryGet(character, out var source)) return LearnFailure(SchematicLearningFailure.InvalidCharacter);
            if (source.ObjectType != ObjectType.Pc || _world.PlayerState == null || character != _world.PlayerState.Identity)
                return LearnFailure(SchematicLearningFailure.NotPlayerCharacter);
            if (!_world.TryGetObjectState(writtenItem, out PersistentObjectState item))
                return LearnFailure(SchematicLearningFailure.WrittenItemNotFound);
            if (item.Placement.Kind != ObjectPlacementKind.Contained || item.ParentIdentity != character)
                return LearnFailure(SchematicLearningFailure.WrittenItemNotOwned);
            if (item.Type != ObjectType.Written || item.WrittenSubtype != WrittenSchematicSubtype
                || item.WrittenStartLine < 4000 || item.WrittenStartLine > 5999 || item.WrittenStartLine % 10 != 0)
                return LearnFailure(SchematicLearningFailure.NotASchematic);
            var id = new SchematicId(item.WrittenStartLine);
            if (_catalog == null || !_catalog.TryGet(id, out SchematicDefinition definition)
                || definition.Acquisition != SchematicAcquisition.Found)
                return LearnFailure(SchematicLearningFailure.UnknownSchematic, id);
            if (!_found.TryGetValue(character, out List<int> values)) _found.Add(character, values = new List<int>());
            if (values.Contains(id.Value)) return LearnFailure(SchematicLearningFailure.AlreadyKnown, id);
            if (!_world.ConsumeSingularItem(writtenItem, character))
                return LearnFailure(SchematicLearningFailure.InventoryFailure, id);
            values.Add(id.Value);
            return new SchematicLearningResult(SchematicLearningFailure.None, id, true);
        }

        public CraftingResult Preview(CraftingRequest request) => Resolve(request, false);
        public CraftingResult Execute(CraftingRequest request) => Resolve(request, true);
        private CraftingResult Resolve(CraftingRequest request, bool commit)
        {
            if (!_world.Characters.TryGet(request.Crafter, out _) || !_world.Vitality.TryGet(request.Crafter, out _))
                return Failure(CraftingFailure.InvalidCrafter, request);
            if (_world.Vitality.IsDead(request.Crafter)) return Failure(CraftingFailure.CrafterDead, request);
            if (_catalog == null || !_catalog.TryGet(request.Schematic, out SchematicDefinition definition))
                return Failure(CraftingFailure.UnknownSchematic, request);
            if (!Knows(request.Crafter, request.Schematic)) return Failure(CraftingFailure.SchematicNotKnown, request);
            var reserved = new Dictionary<ArcanumObjectId, int>();
            if (!TrySelect(request.Crafter, definition.ComponentOneAliases, reserved, out CraftingComponentSelection first))
                return Failure(CraftingFailure.MissingComponentOne, request);
            Reserve(first.Item, reserved);
            if (!TrySelect(request.Crafter, definition.ComponentTwoAliases, reserved, out CraftingComponentSelection second))
                return Failure(CraftingFailure.MissingComponentTwo, request, first);
            Reserve(second.Item, reserved);
            bool firstEnough = HasExpertise(request.Crafter, first, out bool firstSupported);
            bool secondEnough = HasExpertise(request.Crafter, second, out bool secondSupported);
            if (!firstEnough || !secondEnough)
                return Failure(firstSupported && secondSupported ? CraftingFailure.InsufficientExpertise
                    : CraftingFailure.UnsupportedComponent, request, first, second);
            int variant = first.AliasIndex != 0 ? first.AliasIndex : second.AliasIndex;
            int product = definition.ProductAliases[variant];
            ObjectProtoInfo productPrototype = _world.ResolvePrototype(product);
            if (productPrototype == null || !WorldMapSessionCoordinator.IsItemType(productPrototype.Type))
                return Failure(CraftingFailure.ProductUnavailable, request, first, second, product, definition.ProductQuantity);
            InventoryAcceptance capacity = _world.InventoryCapacity.EvaluateCraftOutputs(productPrototype,
                _world.ResolveInventoryFootprint(productPrototype.InvAid), request.Crafter,
                definition.ProductQuantity, reserved);
            if (!capacity.Succeeded)
                return Failure(CraftingFailure.InventoryFailure, request, first, second, product,
                    definition.ProductQuantity);
            if (!commit) return new CraftingResult(CraftingFailure.None, request, first, second, product, definition.ProductQuantity);

            WorldMapSessionCoordinator.DialogueInventorySnapshot snapshot = _world.CaptureDialogueInventorySnapshot();
            try
            {
                foreach (var pair in reserved)
                {
                    if (!_world.TryGetObjectState(pair.Key, out PersistentObjectState item))
                        throw new InvalidOperationException("A selected component disappeared after preflight.");
                    if (item.Type == ObjectType.Ammo)
                    {
                        if (!_world.ConsumeAmmo(pair.Key, pair.Value, out _))
                            throw new InvalidOperationException("An ammunition component could not be consumed.");
                    }
                    else if (pair.Value != 1 || !_world.ConsumeSingularItem(pair.Key, request.Crafter))
                        throw new InvalidOperationException("A singular component could not be consumed.");
                }
                var products = new List<ArcanumObjectId>(definition.ProductQuantity);
                for (int index = 0; index < definition.ProductQuantity; index++)
                {
                    ItemCreationResult created = _world.CreateItem(product, ObjectPlacement.ContainedBy(request.Crafter));
                    if (!created.Succeeded) throw new InvalidOperationException($"Product creation failed: {created.Code}.");
                    products.Add(created.State.Identity);
                }
                return new CraftingResult(CraftingFailure.None, request, first, second, product,
                    definition.ProductQuantity, products);
            }
            catch
            {
                _world.RestoreDialogueInventorySnapshot(snapshot);
                return Failure(CraftingFailure.InventoryFailure, request, first, second, product, definition.ProductQuantity);
            }
        }

        private bool TrySelect(ArcanumObjectId crafter, IReadOnlyList<int> aliases,
            IReadOnlyDictionary<ArcanumObjectId, int> reserved, out CraftingComponentSelection selection)
        {
            for (int aliasIndex = 0; aliasIndex < 3; aliasIndex++)
            {
                int alias = aliases[aliasIndex];
                ObjectProtoInfo aliasPrototype = _world.ResolvePrototype(alias);
                var candidates = _world.States.Values.Where(item => item.Placement.Kind == ObjectPlacementKind.Contained
                        && item.ParentIdentity == crafter && (item.PrototypeNumber == alias
                            || aliasPrototype?.Description != null
                            && _world.ResolvePrototype(item.PrototypeNumber)?.Description == aliasPrototype.Description))
                    .OrderBy(item => item.InventoryLocation).ThenBy(item => item.Identity.Key, StringComparer.Ordinal);
                foreach (PersistentObjectState item in candidates)
                {
                    int already = reserved.TryGetValue(item.Identity, out int value) ? value : 0;
                    if (item.Type == ObjectType.Ammo ? item.StackQuantity.GetValueOrDefault() > already : already == 0)
                    {
                        selection = new CraftingComponentSelection(item.Identity, aliasIndex, item.PrototypeNumber);
                        return true;
                    }
                }
            }
            selection = default;
            return false;
        }

        private bool HasExpertise(ArcanumObjectId crafter, CraftingComponentSelection selection, out bool supported)
        {
            supported = false;
            ObjectProtoInfo prototype = _world.ResolvePrototype(selection.PrototypeNumber);
            if (prototype == null) return false;
            int required = Math.Max(0, -prototype.ItemComplexity.GetValueOrDefault());
            if (required == 0) { supported = true; return true; }
            if (!prototype.ItemDiscipline.HasValue || prototype.ItemDiscipline.Value < 0
                || prototype.ItemDiscipline.Value > 7) return false;
            supported = true;
            return _world.Technology.GetEffectiveLevel(crafter,
                (TechnologyDiscipline)prototype.ItemDiscipline.Value) >= required;
        }
        private static void Reserve(ArcanumObjectId identity, Dictionary<ArcanumObjectId, int> reserved)
            => reserved[identity] = reserved.TryGetValue(identity, out int value) ? value + 1 : 1;
        public CraftingSaveData ExportSaveData() => new()
        {
            Characters = _found.OrderBy(pair => pair.Key.Key, StringComparer.Ordinal)
                .Select(pair => new CraftingCharacterSaveData
                { Identity = pair.Key.Key, FoundSchematicIds = new List<int>(pair.Value) }).ToList(),
        };
        internal void RestoreSaveData(CraftingSaveData data)
        {
            if (data?.Characters == null) return;
            foreach (CraftingCharacterSaveData value in data.Characters)
            {
                ArcanumObjectId.TryParsePersistent(value.Identity, out ArcanumObjectId identity);
                _found.Add(identity, new List<int>(value.FoundSchematicIds));
            }
        }
        private static SchematicLearningResult LearnFailure(SchematicLearningFailure failure, SchematicId id = default)
            => new(failure, id);
        private static CraftingResult Failure(CraftingFailure failure, CraftingRequest request,
            CraftingComponentSelection first = default, CraftingComponentSelection second = default,
            int product = 0, int quantity = 0) => new(failure, request, first, second, product, quantity);
    }
}
