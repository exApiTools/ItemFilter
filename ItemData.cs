using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.FilesInMemory;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Cache;
using ExileCore2.Shared.Enums;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Models;
using GameOffsets2.Native;
using Map = ExileCore2.PoEMemory.Components.Map;

namespace ItemFilterLibrary;

public partial class ItemData
{
    private static readonly ConditionalWeakTable<GameController, CachedValue<PlayerData>> PlayerDataCache = new();

    public class MapData(bool IsMap, int Tier, int Quantity, int Rarity, int PackSize, int Quality)
    {
        public bool IsMap { get; set; } = IsMap;
        public int Tier { get; set; } = Tier;
        public int Quantity { get; set; } = Quantity;
        public int Rarity { get; set; } = Rarity;
        public int PackSize { get; set; } = PackSize;
        public int Quality { get; set; } = Quality;
    }

    public record NecropolisCorpseData(MonsterVariety Monster);

    public record SkillGemData(int Level, int MaxLevel, bool IsGem);

    public record StackData(int Count, int MaxCount);

    public record ChargesData(int Current, int Max, int PerUse);

    public record SocketData(int SocketNumber, IReadOnlyCollection<ItemData> SocketedItems, List<SocketTypes> SocketList);

    public record FlaskData(int LifeRecovery, int ManaRecovery, Dictionary<GameStat, int> Stats);

    public record AttributeRequirementsData(int Strength, int Dexterity, int Intelligence);

    public record ArmourData(int Armour, int Evasion, int ES);

    public record AreaData(int Level, string Name, int Act, bool IsEndGame);

    public record AttackSpeedData(decimal Base, decimal Total);

    public record DamageData(DamageRange Physical, DamageRange Fire, DamageRange Cold, DamageRange Lightning, DamageRange Chaos)
    {
        public DamageRange Total
        {
            get
            {
                var min = Physical.Min + Fire.Min + Cold.Min + Lightning.Min + Chaos.Min;
                var max = Physical.Max + Fire.Max + Cold.Max + Lightning.Max + Chaos.Max;
                return new DamageRange(min, max);
            }
        }
    }

    public record DamageRange(double Min, double Max)
    {
        public double Avg => (Min + Max) / 2;
    }

    private readonly Dictionary<string, bool> _hasTagCache = new();
    private readonly Lazy<double> _estimatedValue;
    private readonly Lazy<double?> _estimatedPublicPrice;

    public string Path { get; } = string.Empty;
    public string ClassName { get; } = string.Empty;
    public string BaseName { get; } = string.Empty;
    public string Name { get; } = string.Empty;
    public string UniqueName { get; } = string.Empty;
    public string PublicPrice { get; } = string.Empty;
    public string HeistContractJobType { get; } = string.Empty;
    public int ItemQuality { get; } = 0;
    public int VeiledModCount { get; } = 0;
    public int ItemLevel { get; } = 0;
    public int RequiredLevel { get; } = 0;
    // public int RealRequiredLevel { get; } = 0; // TODO: Add calculations from equipped gems to get real required level
    public int DeliriumStacks { get; } = 0;
    public int HeistContractReqJobLevel { get; } = 0;
    public int ScourgeTier { get; } = 0;
    public bool IsIdentified { get; } = false;
    public Influence InfluenceFlags { get; set; }
    public bool IsMirrored { get; } = false;
    public bool IsCorrupted { get; } = false;
    public bool IsElder { get; } = false;
    public bool IsShaper { get; } = false;
    public bool IsCrusader { get; } = false;
    public bool IsRedeemer { get; } = false;
    public bool IsHunter { get; } = false;
    public bool IsWarlord { get; } = false;
    public bool IsInfluenced { get; } = false;
    public bool IsSynthesised { get; } = false;
    public bool Enchanted { get; } = false;
    public ItemRarity Rarity { get; } = ItemRarity.Normal;
    public List<string> ModsNames { get; } = [];
    public List<string> PathTags { get; } = [];
    public List<string> Tags { get; } = [];
    public LabelOnGround LabelOnGround { get; } = null;
    public SkillGemData GemInfo { get; } = new SkillGemData(0, 0, false);
    public uint InventoryId { get; }
    public uint Id { get; }
    public int Height { get; } = 0;
    public int Width { get; } = 0;
    public bool IsWeapon { get; } = false;
    public int ShieldBlockChance { get; } = 0;
    public float Distance => GroundItem?.DistancePlayer ?? float.PositiveInfinity;
    public double EstimatedValue => _estimatedValue.Value;
    public double? EstimatedPublicValue => _estimatedPublicPrice.Value;
    public StackData StackInfo { get; } = new StackData(0, 0);
    public Entity Entity { get; }
    public Entity GroundItem { get; }
    public GameController GameController { get; }
    public NecropolisCorpseData CorpseInfo { get; } = new NecropolisCorpseData(new MonsterVariety());
    public SocketData SocketInfo { get; } = new SocketData(0, new List<ItemData>(), new List<SocketTypes>());
    public ChargesData ChargeInfo { get; } = new ChargesData(0, 0, 0);
    public FlaskData FlaskInfo { get; } = new FlaskData(0, 0, new Dictionary<GameStat, int>());
    public AttributeRequirementsData AttributeRequirements { get; } = new AttributeRequirementsData(0, 0, 0);
    public ArmourData ArmourInfo { get; } = new ArmourData(0, 0, 0);
    public ModsData ModsInfo { get; } = new ModsData(new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>());
    public AreaData AreaInfo { get; } = new AreaData(0, "N/A", 0, false);
    public ExpeditionSaga ExpeditionInfo { get; } = new ExpeditionSaga();
    public MapData MapInfo { get; set; } = new MapData(false, 0, 0, 0, 0, 0);

    public AttackSpeedData AttackSpeed { get; } = new AttackSpeedData(0, 0);

    public DamageData Damage { get; } = new DamageData(new DamageRange(0, 0), new DamageRange(0, 0), new DamageRange(0, 0), new DamageRange(0, 0), new DamageRange(0, 0));
    public DamageData DPS { get; } = new DamageData(new DamageRange(0, 0), new DamageRange(0, 0), new DamageRange(0, 0), new DamageRange(0, 0), new DamageRange(0, 0));

    public PlayerData PlayerInfo => _lastPlayerData = CurrentPlayerData;

    private PlayerData CurrentPlayerData =>
        GameController != null
            ? PlayerDataCache.GetValue(GameController, PlayerDataCacheProvider)!.Value
            : new PlayerData(GameController);

    private static CachedValue<PlayerData> PlayerDataCacheProvider(GameController gc)
    {
        var updateFunc = CacheUtils.RememberLastValue<PlayerData>(prev => (prev, new PlayerData(gc)) switch
        {
            (null, var @new) => @new,
            ({ } old, { } @new) => @new.Equals(old) ? old : @new
        });
        return new TimeCache<PlayerData>(updateFunc, 1000);
    }

    private PlayerData _lastPlayerData;
    private bool _wasDynamicallyUpdated;
    private readonly Dictionary<string, IReadOnlyDictionary<GameStat, int>> _statsCache = new();
    private IReadOnlyDictionary<GameStat, int> _itemStatsCache;

    public static PlayerData StaticPlayerData => PlayerDataCache.GetValue(RemoteMemoryObject.GameController, PlayerDataCacheProvider).Value;
    public string ResourcePath { get; } = string.Empty;

    public bool WasDynamicallyUpdated
    {
        get => _wasDynamicallyUpdated || (_wasDynamicallyUpdated = CurrentPlayerData.Equals(_lastPlayerData));
        set
        {
            _ = PlayerInfo;
            _wasDynamicallyUpdated = value;
        }
    }

    public Dictionary<GameStat, int> LocalStats { get; } = new Dictionary<GameStat, int>();

    public ItemData(LabelOnGround queriedItem, GameController gc = null) :
        this(queriedItem.ItemOnGround?.GetComponent<WorldItem>()?.ItemEntity, queriedItem.ItemOnGround, gc, queriedItem)
    {
    }

    public ItemData(Entity queriedItem, GameController gc = null) :
        this(queriedItem, null, gc, null)
    {
    }

    public ItemData(Entity queriedItem, Entity groundItem, GameController gameController) : this(queriedItem, groundItem, gameController, null)
    {
    }

    private ItemData(Entity itemEntity, Entity groundItem, GameController gc, LabelOnGround itemLabelOnGround)
    {
        gc ??= Core.Current?.GameController;
        if (itemEntity == null) return;
        var item = itemEntity;

        LabelOnGround = itemLabelOnGround;
        GroundItem = groundItem;
        Entity = itemEntity;
        GameController = gc;
        Path = item.Path ?? string.Empty;
        Id = item.Id;
        InventoryId = item.InventoryId;

        var baseItemType = gc.Files.BaseItemTypes.Translate(Path);
        if (baseItemType != null)
        {
            ClassName = baseItemType.ClassName;
            BaseName = baseItemType.BaseName;
            Width = baseItemType.Width;
            Height = baseItemType.Height;
            PathTags = baseItemType.MoreTagsFromPath.ToList();
            Tags = baseItemType.Tags.ToList();
        }

        var curArea = gc.Area.CurrentArea;
        if (curArea != null)
        {
            AreaInfo = new AreaData(curArea.RealLevel, curArea.Name, curArea.Act, curArea.Act > 10);
        }

        if (item.TryGetComponent<Quality>(out var quality))
        {
            ItemQuality = quality.ItemQuality;
            MapInfo.Quality = quality.ItemQuality;
        }

        if (item.TryGetComponent<Base>(out var baseComp))
        {
            Name = baseComp.Name ?? "";
            PublicPrice = baseComp.PublicPrice ?? "";
            InfluenceFlags = baseComp.InfluenceFlag;
            ScourgeTier = baseComp.ScourgedTier;
            IsElder = baseComp.isElder;
            IsShaper = baseComp.isShaper;
            IsHunter = baseComp.isHunter;
            IsWarlord = baseComp.isWarlord;
            IsCrusader = baseComp.isCrusader;
            IsRedeemer = baseComp.isRedeemer;
            IsCorrupted = baseComp.isCorrupted;
            IsInfluenced = IsCrusader || IsRedeemer || IsWarlord || IsHunter || IsShaper || IsElder;
            ItemLevel = baseComp.CurrencyItemLevel; //give Mods priority, but still set if possible
        }

        if (item.TryGetComponent<Mods>(out var modsComp))
        {
            RequiredLevel = modsComp.RequiredLevel;
            Rarity = modsComp.ItemRarity;
            var affixSlots = GetTotalAffixSlots();
            IsIdentified = modsComp.Identified;
            ItemLevel = modsComp.ItemLevel;
            UniqueName = modsComp.UniqueName ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(UniqueName) ? Name : UniqueName;
            IsSynthesised = modsComp.Synthesised;
            IsMirrored = modsComp.IsMirrored;
            Enchanted = modsComp.EnchantedMods?.Count > 0;

            ModsInfo = new ModsData(modsComp.ItemMods, modsComp.EnchantedMods, modsComp.ExplicitMods, modsComp.CorruptionImplicitMods, modsComp.ImplicitMods, modsComp.SynthesisMods);
            ModsInfo.MaxAllowedPrefixCount = Math.Max(0, affixSlots + ItemStats[GameStat.LocalMaximumPrefixesAllowed]);
            ModsInfo.MaxAllowedSuffixCount = Math.Max(0, affixSlots + ItemStats[GameStat.LocalMaximumSuffixesAllowed]);
            if (IsIdentified)
            {
                ModsInfo.OpenPrefixCount = Math.Max(0, ModsInfo.MaxAllowedPrefixCount - ModsInfo.Prefixes.Count);
                ModsInfo.OpenSuffixCount = Math.Max(0, ModsInfo.MaxAllowedSuffixCount - ModsInfo.Suffixes.Count);
                ModsInfo.HasOpenPrefix = ModsInfo.OpenPrefixCount > 0;
                ModsInfo.HasOpenSuffix = ModsInfo.OpenSuffixCount > 0;
            }
            ModsNames = ModsInfo.ItemMods.Select(mod => mod.Name).ToList();
            VeiledModCount = ModsInfo.ItemMods.Count(m => m.DisplayName.Contains("Veil"));
            DeliriumStacks = ModsInfo.ItemMods.Count(m => m.Name.Contains("AfflictionMapReward"));
        }

        if (item.TryGetComponent<Sockets>(out var socketComp))
        {
            // issue to be resolved in core, if a corrupted ring with sockets gets a new implicit it will still have the component but the component logic will throw an exception
            if (socketComp.NumberOfSockets > 0)
                SocketInfo = new SocketData(socketComp.NumberOfSockets,
                    socketComp.SocketedItems.Select(x => new ItemData(x.ItemEntity, GameController)).ToList(), socketComp.SocketList);
        }

        if (item.TryGetComponent<SkillGem>(out var gemComp))
        {
            GemInfo = new SkillGemData(gemComp.Level, gemComp.MaxLevel, true);
        }

        if (item.TryGetComponent<Stack>(out var stackComp))
        {
            StackInfo = new StackData(stackComp.Size, stackComp.Info.MaxStackSize);
        }

        if (item.TryGetComponent<Map>(out var mapComp))
        {
            MapInfo.Tier = mapComp.Tier;
            MapInfo.IsMap = true;
            var itemStats = GetItemStats(ModsInfo.ItemMods);

            MapInfo.PackSize = itemStats[GameStat.MapPackSizePct];
            MapInfo.Quantity = itemStats[GameStat.MapItemDropQuantityPct];
            MapInfo.Rarity = itemStats[GameStat.MapItemDropRarityPct];
        }

        if (item.TryGetComponent<HeistContract>(out var heistComp))
        {
            HeistContractJobType = heistComp.RequiredJob?.Name ?? "";
            HeistContractReqJobLevel = heistComp.RequiredJobLevel;
        }

        if (item.TryGetComponent<Charges>(out var chargesComp))
        {
            ChargeInfo = new ChargesData(chargesComp.NumCharges, chargesComp.ChargesMax, chargesComp.ChargesPerUse);
        }

        if (item.TryGetComponent<RenderItem>(out var renderItemComp))
        {
            ResourcePath = renderItemComp.ResourcePath;
        }

        if (item.TryGetComponent<Flask>(out var flaskComp))
        {
            FlaskInfo = new FlaskData(flaskComp.LifeRecover, flaskComp.ManaRecover, flaskComp.FlaskStatDictionary);
        }

        if (item.TryGetComponent<LocalStats>(out var localStatsComp))
        {
            LocalStats = localStatsComp.StatDictionary;
        }

        if (item.TryGetComponent<Weapon>(out var weaponComp))
        {
            IsWeapon = true;

            #region Attack Speed Calculation

            var tempAttackSpeedBase = 1000m / weaponComp.AttackTime;
            var tempAttackSpeedTotal = tempAttackSpeedBase;

            if (LocalStats != null)
            {
                var modifier = LocalStats
                    .Where(kvp => kvp.Key == GameStat.LocalAttackSpeedPct)
                    .Select(kvp => (100m + kvp.Value) / 100)
                    .DefaultIfEmpty(1)
                    .First();

                tempAttackSpeedTotal *= modifier;
            }
            AttackSpeed = new AttackSpeedData(decimal.Round(tempAttackSpeedBase, 2, MidpointRounding.ToPositiveInfinity),
                                              decimal.Round(tempAttackSpeedTotal, 2, MidpointRounding.ToPositiveInfinity));

            #endregion Attack Speed Calculation

            Damage = new DamageData(new DamageRange(
                    (weaponComp.DamageMin + LocalStats.GetValueOrDefault(GameStat.LocalMinimumAddedPhysicalDamage)) *
                    (100 + LocalStats.GetValueOrDefault(GameStat.LocalPhysicalDamagePct)) / 100.0,
                    (weaponComp.DamageMax + LocalStats.GetValueOrDefault(GameStat.LocalMaximumAddedPhysicalDamage)) *
                    (100 + LocalStats.GetValueOrDefault(GameStat.LocalPhysicalDamagePct)) / 100.0),
                new DamageRange(LocalStats.GetValueOrDefault(GameStat.LocalMinimumAddedFireDamage), LocalStats.GetValueOrDefault(GameStat.LocalMaximumAddedFireDamage)),
                new DamageRange(LocalStats.GetValueOrDefault(GameStat.LocalMinimumAddedColdDamage), LocalStats.GetValueOrDefault(GameStat.LocalMaximumAddedColdDamage)),
                new DamageRange(LocalStats.GetValueOrDefault(GameStat.LocalMinimumAddedLightningDamage), LocalStats.GetValueOrDefault(GameStat.LocalMaximumAddedLightningDamage)),
                new DamageRange(LocalStats.GetValueOrDefault(GameStat.LocalMinimumAddedChaosDamage), LocalStats.GetValueOrDefault(GameStat.LocalMaximumAddedChaosDamage)));

            var attackSpeedTotal = (double)AttackSpeed.Total;
            DPS = new DamageData(
                new DamageRange(Damage.Physical.Min * attackSpeedTotal, Damage.Physical.Max * attackSpeedTotal),
                new DamageRange(Damage.Fire.Min * attackSpeedTotal, Damage.Fire.Max * attackSpeedTotal),
                new DamageRange(Damage.Cold.Min * attackSpeedTotal, Damage.Cold.Max * attackSpeedTotal),
                new DamageRange(Damage.Lightning.Min * attackSpeedTotal, Damage.Lightning.Max * attackSpeedTotal),
                new DamageRange(Damage.Chaos.Min * attackSpeedTotal, Damage.Chaos.Max * attackSpeedTotal)
            );
        }

        if (item.TryGetComponent<AttributeRequirements>(out var attributeReqComp))
        {
            AttributeRequirements = new AttributeRequirementsData(attributeReqComp.strength, attributeReqComp.dexterity, attributeReqComp.intelligence);
        }

        if (item.TryGetComponent<Armour>(out var armourComp))
        {
            ArmourInfo = new ArmourData(armourComp.ArmourScore, armourComp.EvasionScore, armourComp.EnergyShieldScore);
        }

        if (item.TryGetComponent<Shield>(out var shieldComp))
        {
            ShieldBlockChance = shieldComp.ChanceToBlock;
        }

        if (item.TryGetComponent<ExpeditionSaga>(out var expeditionComp))
        {
            ExpeditionInfo = expeditionComp;
        }

        _estimatedValue = new Lazy<double>(() =>
        {
            var value = gc.PluginBridge.GetMethod<Func<Entity, double>>("NinjaPrice.GetValue")?.Invoke(Entity);
            if (value == null)
            {
                DebugWindow.LogError("Using EstimatedValue without NinjaPricer is not supported");
            }
            return value ?? 0;
        }, LazyThreadSafetyMode.PublicationOnly);

        _estimatedPublicPrice = new Lazy<double?>(() =>
        {
            if (string.IsNullOrWhiteSpace(PublicPrice)) return null;

            var parts = PublicPrice.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || parts[0] != "~b/o") return null;

            if (!int.TryParse(parts[1], out var quantity)) return null;

            var currencyCode = string.Join(" ", parts.Skip(2));
            var noteCode = gc.Files.ItemNoteCode.EntriesList.FirstOrDefault(x => string.Equals(x.Code, currencyCode, StringComparison.OrdinalIgnoreCase));
            if (noteCode?.CurrencyItem?.Type == null) return null;

            var getCurrencyValue = gc.PluginBridge.GetMethod<Func<BaseItemType, double>>("NinjaPrice.GetBaseItemTypeValue");

            return quantity * getCurrencyValue?.Invoke(noteCode.CurrencyItem.Type);
        }, LazyThreadSafetyMode.PublicationOnly);
    }

    public int GetTotalAffixSlots() => Rarity switch
    {
        ItemRarity.Magic => 1,
        ItemRarity.Rare => ClassName switch
        {
            "Jewel" => 2,
            _ => 3
        },
        _ => 0
    };
    public bool HasAnyModSet(string[][] sets) => HasAnyModSet(ModsInfo.ExplicitMods, sets);

    public bool HasAnyModSet(IEnumerable<ItemMod> mods, string[][] sets)
    {
        return sets.Any(set => set.All(modName => mods.Any(mod => mod.RawName.Equals(modName, StringComparison.OrdinalIgnoreCase))));
    }

    public bool HasAnyMatchingConditionSet(bool[][] sets) => sets.Any(set => set.All(condition => condition));

    public bool IsUnownedItem(Func<ItemData, bool> criterion) => criterion(this) && !PlayerInfo.OwnedItems.Any(criterion);

    public List<ItemMod> FindMods(string wantedMod) => ModsInfo.ItemMods
        .Where(item => item.Name.Contains(wantedMod, StringComparison.OrdinalIgnoreCase)).ToList();

    public List<ItemMod> FindModTranslationRegex(params string[] regexStrings)
    {
        var regexes = regexStrings.Select(x=> new Regex(x, RegexOptions.IgnoreCase));
        return ModsInfo.ItemMods
            .Where(item => regexes.Any(r => r.IsMatch(item.Translation))).ToList();
    }

    public IReadOnlyDictionary<GameStat, int> ModStats(params string[] wantedMods)
    {
        if (ModsInfo.ItemMods == null)
        {
            return new DefaultDictionary<GameStat, int>(0);
        }

        return SumModStats(ModsInfo.ItemMods.IntersectBy(wantedMods, x => x.Name, StringComparer.OrdinalIgnoreCase));
    }

    public IReadOnlyDictionary<GameStat, int> GetItemStats(IEnumerable<ItemMod> list)
    {
        var cacheKey = list is IReadOnlyCollection<ItemMod> collection
            ? ModsInfo.ModsDictionary.GetValueOrDefault(collection)
            : null;
        if (cacheKey == null)
        {
            return ComputeItemStats(list);
        }

        if (!_statsCache.TryGetValue(cacheKey, out var cachedStats))
        {
            cachedStats = ComputeItemStats(list);
            _statsCache[cacheKey] = cachedStats;
        }
        return cachedStats;
    }

    public IReadOnlyDictionary<GameStat, int> ItemStats => _itemStatsCache ??= GetItemStats(ModsInfo.ItemMods);

    private IReadOnlyDictionary<GameStat, int> ComputeItemStats(IEnumerable<ItemMod> list)
    {
        return list == null ? new DefaultDictionary<GameStat, int>(0) : SumModStats(list);
    }

    public IReadOnlyDictionary<GameStat, float> ModWeightedStatSum(params (string, float)[] wantedMods)
    {
        if (ModsInfo.ItemMods == null)
        {
            return new DefaultDictionary<GameStat, float>(0);
        }

        return SumModStats(ModsInfo.ItemMods.Join(wantedMods, x => x.Name, x => x.Item1, (mod, w) => (mod, w.Item2), StringComparer.OrdinalIgnoreCase));
    }

    public IReadOnlyDictionary<GameStat, float> ModWeightedStatSum(Dictionary<string, float> wantedMods)
    {
        if (ModsInfo.ItemMods == null)
        {
            return new DefaultDictionary<GameStat, float>(0);
        }

        return SumModStats(ModsInfo.ItemMods.Join(wantedMods, x => x.Name, x => x.Key, (mod, w) => (mod, w.Value), StringComparer.OrdinalIgnoreCase));
    }

    public bool HasMods(params string[] wantedMods)
    {
        return ModsInfo.ItemMods != null &&
               ModsInfo.ItemMods.IntersectBy(wantedMods, x => x.Name, StringComparer.OrdinalIgnoreCase)
                   .Count() == wantedMods.Length;
    }

    public static IReadOnlyDictionary<GameStat, int> SumModStats(IEnumerable<ItemMod> mods)
    {
        return new DefaultDictionary<GameStat, int>(mods
            .SelectMany(x => x.ModRecord.StatNames.Zip(x.Values, (name, value) => (name.MatchingStat, value)))
            .GroupBy(x => x.MatchingStat, x => x.value, (stat, values) => (stat, values.Sum()))
            .ToDictionary(x => x.stat, x => x.Item2), 0);
    }

    public static IReadOnlyDictionary<GameStat, int> SumModStats(params ItemMod[] mods) =>
        SumModStats((IEnumerable<ItemMod>)mods);

    public static IReadOnlyDictionary<GameStat, float> SumModStats(IEnumerable<(ItemMod mod, float weight)> mods)
    {
        return new DefaultDictionary<GameStat, float>(mods
            .SelectMany(x => x.mod.ModRecord.StatNames.Zip(x.mod.Values, (name, value) => (name.MatchingStat, value: value * x.weight)))
            .GroupBy(x => x.MatchingStat, x => x.value, (stat, values) => (stat, values.Sum()))
            .ToDictionary(x => x.stat, x => x.Item2), 0);
    }

    public static IReadOnlyDictionary<GameStat, float> SumModStats(params (ItemMod mod, float weight)[] mods) =>
        SumModStats((IEnumerable<(ItemMod mod, float weight)>)mods);

    private bool CheckAndCacheTags(string cacheKey, Func<bool> checkFunction)
    {
        if (!_hasTagCache.TryGetValue(cacheKey, out var result))
        {
            result = checkFunction();
            _hasTagCache[cacheKey] = result;
        }
        return result;
    }

    public bool HasTag(string wantedTag)
    {
        return CheckAndCacheTags($"Single_{wantedTag.ToLower()}",
            () => Tags.Concat(PathTags).Any(tag => tag.Contains(wantedTag, StringComparison.OrdinalIgnoreCase)));
    }

    public bool HasTagCase(string wantedTag)
    {
        return CheckAndCacheTags($"SingleCase_{wantedTag}",
            () => Tags.Concat(PathTags).Any(tag => tag.Contains(wantedTag)));
    }

    public bool HasTag(List<string> tags, string wantedTag)
    {
        return CheckAndCacheTags($"List_{string.Join("_", tags.OrderBy(x => x))}_{wantedTag.ToLower()}",
            () => tags.Any(tag => tag.Contains(wantedTag, StringComparison.OrdinalIgnoreCase)));
    }

    public bool HasTagCase(List<string> tags, string wantedTag)
    {
        return CheckAndCacheTags($"ListCase_{string.Join("_", tags.OrderBy(x => x))}_{wantedTag}",
            () => tags.Any(tag => tag.Contains(wantedTag)));
    }

    public bool ContainsString(string inputString, params string[] wantedStrings) => wantedStrings.Any(wantedString => inputString.Contains(wantedString, StringComparison.OrdinalIgnoreCase));

    public bool ContainsStringCase(string inputString, params string[] wantedStrings) => wantedStrings.Select(wantedString => wantedString).Any(inputString.Contains);

    public override string ToString() => $"{BaseName} ({ClassName}) Dist: {Distance}";
}

[DynamicLinqType]
public static class ItemDataExtensions
{
    public static IReadOnlyDictionary<GameStat, int> GetStats(this IEnumerable<ItemMod> mods)
    {
        if (mods == null || !mods.Any())
        {
            return new DefaultDictionary<GameStat, int>(0);
        }

        return ItemData.SumModStats(mods.ToList());
    }
}
