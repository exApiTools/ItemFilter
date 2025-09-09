using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using System.Collections.Generic;
using System.Linq;

namespace ItemFilterLibrary;

public partial class ItemData
{
    public sealed class PlayerData
    {
        private static readonly InventorySlotE[] EquippedSlots = new[]
        {
            InventorySlotE.BodyArmour1,
            InventorySlotE.Weapon1,
            InventorySlotE.Offhand1,
            InventorySlotE.Helm1,
            InventorySlotE.Gloves1,
            InventorySlotE.Boots1,
            InventorySlotE.Amulet1,
            InventorySlotE.Ring1,
            InventorySlotE.Ring2,
            InventorySlotE.Belt1,
        };

        private readonly List<long> _equippedItemAddresses = new();
        private readonly List<long> _inventoryItemAddresses = new();
        public int Level { get; }
        public int Strength { get; }
        public int Dexterity { get; }
        public int Intelligence { get; }

        public List<ItemData> EquippedItems { get; } = new();
        public List<ItemData> InventoryItems { get; } = new();
        public List<ItemData> OwnedItems { get; } = new();
        public List<ItemData> OwnedGems { get; } = new();

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
            var inventoryItems = itemsBySlot[InventorySlotE.MainInventory1].SelectMany(x => x).ToList();
            _inventoryItemAddresses = inventoryItems.Select(x => x.Address).OrderBy(x => x).ToList();
            InventoryItems = inventoryItems.Select(x => new ItemData(x, gameController)).ToList();
            OwnedItems = EquippedItems.Concat(InventoryItems).ToList();
            OwnedGems = OwnedItems.Concat(OwnedItems.SelectMany(x => x.SocketInfo.SocketedGems)).Where(x => x.GemInfo.IsGem).ToList();
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
                   _inventoryItemAddresses.SequenceEqual(other._equippedItemAddresses);
        }
    }

    public record ModsData(IReadOnlyCollection<ItemMod> ItemMods,
        IReadOnlyCollection<ItemMod> EnchantedMods,
        IReadOnlyCollection<ItemMod> ExplicitMods,
        IReadOnlyCollection<ItemMod> FracturedMods,
        IReadOnlyCollection<ItemMod> ImplicitMods,
        IReadOnlyCollection<ItemMod> ScourgeMods,
        IReadOnlyCollection<ItemMod> SynthesisMods,
        IReadOnlyCollection<ItemMod> CrucibleMods)
    {
        public IReadOnlyDictionary<IReadOnlyCollection<ItemMod>, string> ModsDictionary { get; } = new Dictionary<IReadOnlyCollection<ItemMod>, string>
        {
            { ItemMods, "ItemMods" },
            { EnchantedMods, "EnchantedMods" },
            { ExplicitMods, "ExplicitMods" },
            { FracturedMods, "FracturedMods" },
            { ImplicitMods, "ImplicitMods" },
            { ScourgeMods, "ScourgeMods" },
            { SynthesisMods, "SynthesisMods" },
            { CrucibleMods, "CrucibleMods" }
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