using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.FilesInMemory;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Runtime.CompilerServices;
using System.Threading;
using ExileCore.PoEMemory;

namespace ItemFilterLibrary;

public partial class ItemData
{
    private static readonly ConditionalWeakTable<GameController, CachedValue<PlayerData>> PlayerDataCache = new();

    #region MapInfo

    public class MapOccupationData(bool ElderBoss, bool Enslaver, bool Eradicator, bool Constrictor, bool Purifier, bool ConquerorBoss, bool Baran, bool Veritania, bool AlHezmin, bool Drox)
    {
        public bool ElderBoss { get; set; } = ElderBoss;
        public bool Enslaver { get; set; } = Enslaver;
        public bool Eradicator { get; set; } = Eradicator;
        public bool Constrictor { get; set; } = Constrictor;
        public bool Purifier { get; set; } = Purifier;
        public bool ConquerorBoss { get; set; } = ConquerorBoss;
        public bool Baran { get; set; } = Baran;
        public bool Veritania { get; set; } = Veritania;
        public bool AlHezmin { get; set; } = AlHezmin;
        public bool Drox { get; set; } = Drox;
    }

    public class MapTypeData(bool Normal, bool Blighted, bool blightRavaged, bool Uber)
    {
        public bool Normal { get; set; } = Normal;
        public bool Blighted { get; set; } = Blighted;
        public bool BlightRavaged { get; set; } = blightRavaged;
        public bool Uber { get; set; } = Uber;
    }

    public class MapInfluenceData(bool Memory)
    {
        public bool Memory { get; set; } = Memory;
    }

    public class MapData(bool IsMap, int Tier, int Quantity, int Rarity, int PackSize, int Quality, int MoreMaps, int MoreScarabs, int MoreCurrency, bool Occupied, MapOccupationData OccupiedBy, MapTypeData Type, MapInfluenceData Influence, bool IsBonusCompleted, bool IsCompleted, WorldArea Area)
    {
        public bool IsMap { get; set; } = IsMap;
        public int Tier { get; set; } = Tier;
        public int Quantity { get; set; } = Quantity;
        public int Rarity { get; set; } = Rarity;
        public int PackSize { get; set; } = PackSize;
        public int Quality { get; set; } = Quality;
        public int MoreMaps { get; set; } = MoreMaps;
        public int MoreScarabs { get; set; } = MoreScarabs;
        public int MoreCurrency { get; set; } = MoreCurrency;
        public bool Occupied { get; set; } = Occupied;
        public MapOccupationData OccupiedBy { get; set; } = OccupiedBy;
        public MapTypeData Type { get; set; } = Type;
        public MapInfluenceData Influence { get; set; } = Influence;
        public bool IsBonusCompleted { get; set; } = IsBonusCompleted;
        public bool IsCompleted { get; set; } = IsCompleted;
        public WorldArea Area { get; set; } = Area;
    }
    #endregion

    public record NecropolisCorpseData(NecropolisCraftingMod CraftingMod, MonsterVariety Monster);

    public record SkillGemData(int Level, int MaxLevel, SkillGemQualityTypeE QualityType, bool IsGem);

    public record StackData(int Count, int MaxCount);

    public record ChargesData(int Current, int Max, int PerUse);

    public record SocketData(int LargestLinkSize, int SocketNumber, IReadOnlyCollection<IReadOnlyCollection<int>> Links, IReadOnlyCollection<string> SocketGroups, IReadOnlyCollection<ItemData> SocketedGems);

    public record FlaskData(int LifeRecovery, int ManaRecovery, Dictionary<GameStat, int> Stats);

    public record AttributeRequirementsData(int Strength, int Dexterity, int Intelligence);

    public record ArmourData(int Armour, int Evasion, int ES, int Perfection);

    public record AreaData(int Level, string Name, int Act, bool IsEndGame);

    public record AttackSpeedData(decimal Base, decimal Total);

    private readonly Dictionary<string, bool> _hasTagCache = new();
    private readonly Lazy<double> _estimatedValue;

    public string Path { get; } = string.Empty;
    public string ClassName { get; } = string.Empty;
    public string BaseName { get; } = string.Empty;
    public string Name { get; } = string.Empty;
    public string PublicPrice { get; } = string.Empty;
    public string HeistContractJobType { get; } = string.Empty;
    public int ItemQuality { get; } = 0;
    public int VeiledModCount { get; } = 0;
    public int FracturedModCount { get; } = 0;
    public int ItemLevel { get; } = 0;
    public int RequiredLevel { get; } = 0;
    public int RealRequiredLevel { get; } = 0;
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
    public List<string> ModsNames { get; } = new List<string>();
    public List<string> PathTags { get; } = new List<string>();
    public List<string> Tags { get; } = new List<string>();
    public LabelOnGround LabelOnGround { get; } = null;
    public SkillGemData GemInfo { get; } = new SkillGemData(0, 0, SkillGemQualityTypeE.Superior, false);
    public uint InventoryId { get; }
    public uint Id { get; }
    public int Height { get; } = 0;
    public int Width { get; } = 0;
    public int MemoryStrands { get; } = 0;
    public bool IsWeapon { get; } = false;
    public int ShieldBlockChance { get; } = 0;
    public float Distance => GroundItem?.DistancePlayer ?? float.PositiveInfinity;
    public double EstimatedValue => _estimatedValue.Value;
    public StackData StackInfo { get; } = new StackData(0, 0);
    public Entity Entity { get; }
    public Entity GroundItem { get; }
    public GameController GameController { get; }
    public NecropolisCorpseData CorpseInfo { get; } = new NecropolisCorpseData(new NecropolisCraftingMod(), new MonsterVariety());
    public SocketData SocketInfo { get; } = new SocketData(0, 0, new List<IReadOnlyCollection<int>>(), new List<string>(), new List<ItemData>());
    public ChargesData ChargeInfo { get; } = new ChargesData(0, 0, 0);
    public FlaskData FlaskInfo { get; } = new FlaskData(0, 0, new Dictionary<GameStat, int>());
    public AttributeRequirementsData AttributeRequirements { get; } = new AttributeRequirementsData(0, 0, 0);
    public ArmourData ArmourInfo { get; } = new ArmourData(0, 0, 0, 0);
    public ModsData ModsInfo { get; } = new ModsData(new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>());
    public AreaData AreaInfo { get; } = new AreaData(0, "N/A", 0, false);
    public ExpeditionSaga ExpeditionInfo { get; } = new ExpeditionSaga();
    public MapData MapInfo { get; set; } = new MapData(false, 0, 0, 0, 0, 0, 0, 0, 0, false, new MapOccupationData(false, false, false, false, false, false, false, false, false, false), new MapTypeData(false, false, false, false), new MapInfluenceData(false), false, false, null);

    public AttackSpeedData AttackSpeed { get; } = new AttackSpeedData(0, 0);

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

    public static PlayerData StaticPlayerData => PlayerDataCache.GetValue(RemoteMemoryObject.pTheGame.GameController, PlayerDataCacheProvider).Value;
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

    public ItemData(LabelOnGround queriedItem, GameController gc) :
        this(queriedItem.ItemOnGround?.GetComponent<WorldItem>()?.ItemEntity, queriedItem.ItemOnGround, gc, queriedItem)
    {
    }

    public ItemData(Entity queriedItem, GameController gc) :
        this(queriedItem, null, gc, null)
    {
    }

    public ItemData(Entity queriedItem, Entity groundItem, GameController gameController) : this(queriedItem, groundItem, gameController, null)
    {
    }

    private ItemData(Entity itemEntity, Entity groundItem, GameController gc, LabelOnGround itemLabelOnGround)
    {
        if (itemEntity == null) return;
        var item = itemEntity;

        LabelOnGround = itemLabelOnGround;
        GroundItem = groundItem;
        Entity = itemEntity;
        GameController = gc;
        Path = item.Path;
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
            Name = string.IsNullOrWhiteSpace(modsComp.UniqueName) ? Name : modsComp.UniqueName;
            FracturedModCount = modsComp.CountFractured;
            IsSynthesised = modsComp.Synthesised;
            IsMirrored = modsComp.IsMirrored;
            Enchanted = modsComp.EnchantedMods?.Count > 0;

            ModsInfo = new ModsData(modsComp.ItemMods, modsComp.EnchantedMods, modsComp.ExplicitMods, modsComp.FracturedMods, modsComp.ImplicitMods, modsComp.ScourgeMods, modsComp.SynthesisMods, modsComp.CrucibleMods);
            if (IsIdentified)
            {
                ModsInfo.OpenPrefixCount = Math.Max(0, affixSlots - ModsInfo.Prefixes.Count + ItemStats[GameStat.LocalMaximumPrefixesAllowed]);
                ModsInfo.OpenSuffixCount = Math.Max(0, affixSlots - ModsInfo.Suffixes.Count + ItemStats[GameStat.LocalMaximumSuffixesAllowed]);
                ModsInfo.HasOpenPrefix = ModsInfo.OpenPrefixCount >= 1;
                ModsInfo.HasOpenSuffix = ModsInfo.OpenSuffixCount >= 1;
            }
            ModsNames = ModsInfo.ItemMods.Select(mod => mod.Name).ToList();
            VeiledModCount = ModsInfo.ItemMods.Count(m => m.DisplayName.Contains("Veil"));
            DeliriumStacks = ModsInfo.ItemMods.Count(m => m.Name.Contains("AfflictionMapReward"));

            MemoryStrands = modsComp.MemoryStrands;
        }

        if (item.TryGetComponent<NecropolisCorpse>(out var corpseComp))
        {
            ItemLevel = corpseComp.Level;
            CorpseInfo = new NecropolisCorpseData(corpseComp.CraftingMod, corpseComp.MonsterVariety);
        }

        if (item.TryGetComponent<Sockets>(out var socketComp))
        {
            // issue to be resolved in core, if a corrupted ring with sockets gets a new implicit it will still have the component but the component logic will throw an exception
            if (socketComp.NumberOfSockets > 0)
                SocketInfo = new SocketData(socketComp.LargestLinkSize, socketComp.NumberOfSockets, socketComp.Links, socketComp.SocketGroup,
                    socketComp.SocketedGems.Select(x => new ItemData(x.GemEntity, GameController)).ToList());
        }

        if (item.TryGetComponent<SkillGem>(out var gemComp))
        {
            GemInfo = new SkillGemData(gemComp.Level, gemComp.MaxLevel, gemComp.QualityType, true);
        }

        if (item.TryGetComponent<Stack>(out var stackComp))
        {
            StackInfo = new StackData(stackComp.Size, stackComp.Info.MaxStackSize);
        }

        if (item.TryGetComponent<ExileCore.PoEMemory.Components.Map>(out var mapComp))
        {
            MapInfo.Tier = mapComp.Tier;
            MapInfo.IsMap = true;
            var itemStats = GetItemStats(ModsInfo.ItemMods);

            #region MapOccupation

            switch (itemStats[GameStat.MapElderBossVariation])
            {
                case 1:
                    MapInfo.OccupiedBy.Enslaver = true;
                    break;
                case 2:
                    MapInfo.OccupiedBy.Eradicator = true;
                    break;
                case 3:
                    MapInfo.OccupiedBy.Constrictor = true;
                    break;
                case 4:
                    MapInfo.OccupiedBy.Purifier = true;
                    break;
            }

            switch (itemStats[GameStat.MapContainsCitadel])
            {
                case 1:
                    MapInfo.OccupiedBy.Baran = true;
                    break;
                case 2:
                    MapInfo.OccupiedBy.Veritania = true;
                    break;
                case 3:
                    MapInfo.OccupiedBy.AlHezmin = true;
                    break;
                case 4:
                    MapInfo.OccupiedBy.Drox = true;
                    break;
            }

            MapInfo.OccupiedBy.ElderBoss = MapInfo.OccupiedBy.Enslaver || MapInfo.OccupiedBy.Eradicator || MapInfo.OccupiedBy.Constrictor || MapInfo.OccupiedBy.Purifier;
            MapInfo.OccupiedBy.ConquerorBoss = MapInfo.OccupiedBy.Baran || MapInfo.OccupiedBy.Veritania || MapInfo.OccupiedBy.AlHezmin || MapInfo.OccupiedBy.Drox;
            MapInfo.Occupied = MapInfo.OccupiedBy.ElderBoss || MapInfo.OccupiedBy.ConquerorBoss;
            #endregion

            MapInfo.Type.BlightRavaged = itemStats[GameStat.IsUberBlightedMap] == 1;
            MapInfo.Type.Blighted = itemStats[GameStat.IsBlightedMap] == 1;
            MapInfo.Type.Uber = itemStats[GameStat.MapIsUberMap] == 1;
            MapInfo.Type.Normal = !MapInfo.Type.Blighted && !MapInfo.Type.BlightRavaged && !MapInfo.Occupied && !MapInfo.Type.Uber;

            MapInfo.Influence.Memory = itemStats[GameStat.MapZanaInfluence] == 1;

            MapInfo.PackSize = itemStats[GameStat.MapPackSizePct];
            MapInfo.Quantity = itemStats[GameStat.MapItemDropQuantityPct];
            MapInfo.Rarity = itemStats[GameStat.MapItemDropRarityPct];
            MapInfo.MoreMaps = itemStats[GameStat.MapMapItemDropChancePctFinalFromUberMod];
            MapInfo.MoreScarabs = itemStats[GameStat.MapScarabDropChancePctFinalFromUberMod];
            MapInfo.MoreCurrency = itemStats[GameStat.MapCurrencyDropChancePctFinalFromUberMod];
            MapInfo.Area = mapComp.Area;
            MapInfo.IsBonusCompleted = GameController.IngameState.ServerData.BonusCompletedAreas.Contains(MapInfo.Area) ? true : false;
            MapInfo.IsCompleted = GameController.IngameState.ServerData.CompletedAreas.Contains(MapInfo.Area) ? true : false;
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
        }

        if (item.TryGetComponent<AttributeRequirements>(out var attributeReqComp))
        {
            AttributeRequirements = new AttributeRequirementsData(attributeReqComp.strength, attributeReqComp.intelligence, attributeReqComp.dexterity);
        }

        if (item.TryGetComponent<Armour>(out var armourComp))
        {
            ArmourInfo = new ArmourData(armourComp.ArmourScore, armourComp.EvasionScore, armourComp.EnergyShieldScore, armourComp.PerfectionScore);
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

        RealRequiredLevel = Math.Max(
            SocketInfo.SocketedGems.Select(
                    g =>
                    {
                        if (g.Entity.TryGetComponent<SkillGem>(out var gemComp))
                            return gemComp.RequiredLevel;
                        if (g.Entity.TryGetComponent<Mods>(out var modsComp))
                            return modsComp.RequiredLevel;
                        return 0;
                    })
                .DefaultIfEmpty(0)
                .Max(), RequiredLevel);
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

    public bool IsUnownedGem(Func<ItemData, bool> criterion) => GemInfo.IsGem && criterion(this) && !PlayerInfo.OwnedGems.Any(criterion);

    public bool HasUnorderedSocketGroup(string groupText) => HasUnorderedSocketGroup(groupText, false);

    public bool HasUnorderedSocketGroup(string groupText, bool socketGroupMatch)
    {
        return SocketInfo.SocketGroups.Any(x =>
            (socketGroupMatch ? x.Length == groupText.Length : x.Length >= groupText.Length) &&
            ParseSocketString(x) is var group &&
            ParseSocketString(groupText) is var request &&
            request.Literals.Sum(g => Math.Max(g.Count() - group.Literals[g.Key].Count(), 0)) <= group.Whites - request.Whites
        );
    }

    public bool HasSockets(string socketText) => HasSockets(socketText, false);

    public bool HasSockets(string socketText, bool socketsMatch)
    {
        return string.Concat(SocketInfo.SocketGroups) is var sockets &&
               (socketsMatch ? sockets.Length == socketText.Length : sockets.Length >= socketText.Length) &&
               ParseSocketString(sockets) is var group &&
               ParseSocketString(socketText) is var request &&
               request.Literals.Sum(g => Math.Max(g.Count() - group.Literals[g.Key].Count(), 0)) <= group.Whites - request.Whites;
    }

    private static (ILookup<char, char> Literals, int Wildcards, int Whites) ParseSocketString(string socketText)
    {
        var wildcards = socketText.Count(x => x is '?');
        var whites = socketText.Count(x => x is 'w' or 'W');
        return (socketText.Where(x => x is not ('?' or 'w' or 'W')).ToLookup(char.ToLowerInvariant), wildcards, whites);
    }

    public List<ItemMod> FindMods(string wantedMod) => ModsInfo.ItemMods
        .Where(item => item.Name.Contains(wantedMod, StringComparison.OrdinalIgnoreCase)).ToList();

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
        if (list == null)
        {
            return new DefaultDictionary<GameStat, int>(0);
        }

        return SumModStats(list);
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
        if (!_hasTagCache.TryGetValue(cacheKey, out bool result))
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
