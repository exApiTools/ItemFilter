using System.Collections.Generic;
using System.Linq;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;

namespace ItemFilterLibrary;

public partial class ItemData
{
    public sealed class PlayerData
    {
        private static readonly InventorySlotE[] EquippedSlots =
        [
            InventorySlotE.BodyArmour1,
            InventorySlotE.Weapon1,
            InventorySlotE.Offhand1,
            InventorySlotE.Helm1,
            InventorySlotE.Gloves1,
            InventorySlotE.Boots1,
            InventorySlotE.Amulet1,
            InventorySlotE.Ring1,
            InventorySlotE.Ring2,
            InventorySlotE.Ring3,
            InventorySlotE.Belt1,
        ];

        private static readonly InventorySlotE[] OffhandSlots = 
        [
            InventorySlotE.Weapon2,
            InventorySlotE.Offhand2,
        ];

        private readonly List<long> _equippedItemAddresses = [];
        private readonly List<long> _inventoryItemAddresses = [];
        private readonly List<long> _offhandItemAddresses = [];
        public int Level { get; }
        public int Strength { get; }
        public int Dexterity { get; }
        public int Intelligence { get; }

        public List<ItemData> EquippedItems { get; } = [];
        public List<ItemData> OffhandItems { get; } = [];
        public List<ItemData> InventoryItems { get; } = [];
        public List<ItemData> OwnedItems { get; } = [];

        public PlayerData(GameController gameController)
        {
            if (gameController == null)
            {
                return;
            }

            if (gameController.Player.TryGetComponent<Player>(out var playerComp))
            {
                Level = playerComp.Level;
                Strength = playerComp.Strength;
                Dexterity = playerComp.Dexterity;
                Intelligence = playerComp.Intelligence;
            }

            var itemsBySlot = gameController.IngameState.ServerData.PlayerInventories.ToLookup(x => x.Inventory.InventSlot, x => x.Inventory.Items);
            var equippedItems = EquippedSlots.SelectMany(x => itemsBySlot[x].SelectMany(i => i)).ToList();
            _equippedItemAddresses = equippedItems.Select(x => x.Address).OrderBy(x => x).ToList();
            EquippedItems = equippedItems.Select(x => new ItemData(x, gameController)).ToList();
            var offhandItems = OffhandSlots.SelectMany(x => itemsBySlot[x].SelectMany(i => i)).ToList();
            _offhandItemAddresses = offhandItems.Select(x => x.Address).OrderBy(x => x).ToList();
            OffhandItems = offhandItems.Select(x => new ItemData(x, gameController)).ToList();
            var inventoryItems = itemsBySlot[InventorySlotE.MainInventory1].SelectMany(x => x).ToList();
            _inventoryItemAddresses = inventoryItems.Select(x => x.Address).OrderBy(x => x).ToList();
            InventoryItems = inventoryItems.Select(x => new ItemData(x, gameController)).ToList();
            OwnedItems = EquippedItems.Concat(InventoryItems).Concat(OffhandItems).ToList();
        }

        public bool Equals(PlayerData other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Level == other.Level &&
                   Strength == other.Strength &&
                   Dexterity == other.Dexterity &&
                   Intelligence == other.Intelligence &&
                   _equippedItemAddresses.SequenceEqual(other._equippedItemAddresses) &&
                   _inventoryItemAddresses.SequenceEqual(other._equippedItemAddresses) && 
                   _offhandItemAddresses.SequenceEqual(other._offhandItemAddresses);
        }
    }

    public record ModsData(IReadOnlyCollection<ItemMod> ItemMods,
        IReadOnlyCollection<ItemMod> EnchantedMods,
        IReadOnlyCollection<ItemMod> ExplicitMods,
        IReadOnlyCollection<ItemMod> CorruptionImplicitMods,
        IReadOnlyCollection<ItemMod> ImplicitMods,
        IReadOnlyCollection<ItemMod> SynthesisMods)
    {
        public IReadOnlyDictionary<IReadOnlyCollection<ItemMod>, string> ModsDictionary { get; } = new Dictionary<IReadOnlyCollection<ItemMod>, string>
        {
            { ItemMods, "ItemMods" },
            { EnchantedMods, "EnchantedMods" },
            { ExplicitMods, "ExplicitMods" },
            { CorruptionImplicitMods, "CorruptionImplicitMods" },
            { ImplicitMods, "ImplicitMods" },
            { SynthesisMods, "SynthesisMods" },
        };

        public IReadOnlyCollection<ItemMod> Prefixes { get; } = ExplicitMods.Where(m => m.ModRecord.AffixType == ModType.Prefix).ToList();
        public IReadOnlyCollection<ItemMod> Suffixes { get; } = ExplicitMods.Where(m => m.ModRecord.AffixType == ModType.Suffix).ToList();
        public int MaxAllowedPrefixCount { get; set; } = -1;
        public int MaxAllowedSuffixCount { get; set; } = -1;
        public int OpenPrefixCount { get; set; } = -1;
        public int OpenSuffixCount { get; set; } = -1;
        public bool HasOpenPrefix { get; set; } = false;
        public bool HasOpenSuffix { get; set; } = false;
    }
}