using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ExileCore.Shared.Cache;
using System.Linq.Dynamic.Core.CustomTypeProviders;

namespace ItemFilterLibrary;

public class ItemData
{
    private static readonly ConditionalWeakTable<GameController, CachedValue<PlayerData>> PlayerDataCache = new();
    public record SkillGemData(int Level, int MaxLevel, SkillGemQualityTypeE QualityType);

    public record StackData(int Count, int MaxCount);

    public record ChargesData(int Current, int Max, int PerUse);

    public record SocketData(int LargestLinkSize, int SocketNumber, IReadOnlyCollection<IReadOnlyCollection<int>> Links, IReadOnlyCollection<string> SocketGroups);

    public record FlaskData(int LifeRecovery, int ManaRecovery, Dictionary<GameStat, int> Stats);

    public record AttributeRequirementsData(int Strength, int Dexterity, int Intelligence);

    public record ArmourData(int Armour, int Evasion, int ES);

    public record AreaData(int Level, string Name, int Act, bool IsEndGame);

    public record ModsData(IEnumerable<ItemMod> ItemMods, IEnumerable<ItemMod> EnchantedMods, IEnumerable<ItemMod> ExplicitMods, IEnumerable<ItemMod> FracturedMods, IEnumerable<ItemMod> ImplicitMods, IEnumerable<ItemMod> ScourgeMods, IEnumerable<ItemMod> SynthesisMods, IEnumerable<ItemMod> CrucibleMods);

    public record PlayerData(int Level, int Strength, int Dexterity, int Intelligence);

    public record AttackSpeedData(decimal Base, decimal Total);

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
    public int MapTier { get; } = 0;
    public int DeliriumStacks { get; } = 0;
    public int HeistContractReqJobLevel { get; } = 0;
    public int ScourgeTier { get; } = 0;
    public bool IsIdentified { get; } = false;
    public Influence InfluenceFlags { get; set; }
    public bool IsCorrupted { get; } = false;
    public bool IsElder { get; } = false;
    public bool IsShaper { get; } = false;
    public bool IsCrusader { get; } = false;
    public bool IsRedeemer { get; } = false;
    public bool IsHunter { get; } = false;
    public bool IsWarlord { get; } = false;
    public bool IsInfluenced { get; } = false;
    public bool IsSynthesised { get; } = false;
    public bool IsBlightMap { get; } = false;
    public bool IsMap { get; } = false;
    public bool IsElderGuardianMap { get; } = false;
    public bool Enchanted { get; } = false;
    public ItemRarity Rarity { get; } = ItemRarity.Normal;
    public List<string> ModsNames { get; } = new List<string>();
    public List<string> PathTags { get; } = new List<string>();
    public List<string> Tags { get; } = new List<string>();
    public LabelOnGround LabelOnGround { get; } = null;
    public SkillGemData GemInfo { get; } = new SkillGemData(0, 0, SkillGemQualityTypeE.Superior);
    public uint InventoryId { get; }
    public uint Id { get; }
    public int Height { get; } = 0;
    public int Width { get; } = 0;
    public bool IsWeapon { get; } = false;
    public int ShieldBlockChance { get; } = 0;
    public float Distance => LabelOnGround.ItemOnGround?.DistancePlayer ?? float.PositiveInfinity;
    public StackData StackInfo { get; } = new StackData(0, 0);
    public Entity Entity { get; }
    public GameController GameController { get; }
    public SocketData SocketInfo { get; } = new SocketData(0, 0, new List<IReadOnlyCollection<int>>(), new List<string>());
    public ChargesData ChargeInfo { get; } = new ChargesData(0, 0, 0);
    public FlaskData FlaskInfo { get; } = new FlaskData(0, 0, new Dictionary<GameStat, int>());
    public AttributeRequirementsData AttributeRequirements { get; } = new AttributeRequirementsData(0, 0, 0);
    public ArmourData ArmourInfo { get; } = new ArmourData(0, 0, 0);
    public ModsData ModsInfo { get; } = new ModsData(new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>(), new List<ItemMod>());
    public AreaData AreaInfo { get; } = new AreaData(0, "N/A", 0, false);

    public AttackSpeedData AttackSpeed { get; } = new AttackSpeedData(0, 0);

    public PlayerData PlayerInfo => _lastPlayerData = CurrentPlayerData;

    private PlayerData CurrentPlayerData =>
        GameController != null
            ? PlayerDataCache.GetValue(GameController, gc => new FrameCache<PlayerData>(() => CreatePlayerData(gc)))!.Value
            : new PlayerData(0, 0, 0, 0);

    private PlayerData _lastPlayerData;
    private bool _wasDynamicallyUpdated;

    public string ResourcePath { get; } = string.Empty;

    public bool WasDynamicallyUpdated
    {
        get => _wasDynamicallyUpdated || (_wasDynamicallyUpdated = _lastPlayerData != CurrentPlayerData);
        set
        {
            _ = PlayerInfo;
            _wasDynamicallyUpdated = value;
        }
    }

    public Dictionary<GameStat, int> LocalStats { get; } = new Dictionary<GameStat, int>();
    public ItemData(LabelOnGround queriedItem, GameController gc) :
        this(queriedItem.ItemOnGround?.GetComponent<WorldItem>()?.ItemEntity, gc, queriedItem)
    {
    }

    public ItemData(Entity queriedItem, GameController gc) :
        this(queriedItem, gc, null)
    {
    }

    private ItemData(Entity itemEntity, GameController gc, LabelOnGround itemLabelOnGround)
    {
        if (itemEntity == null) return;
        var item = itemEntity;

        LabelOnGround = itemLabelOnGround;
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
        }

        if (item.TryGetComponent<Mods>(out var modsComp))
        {
            Rarity = modsComp.ItemRarity;
            IsIdentified = modsComp.Identified;
            ItemLevel = modsComp.ItemLevel;
            Name = string.IsNullOrWhiteSpace(modsComp.UniqueName) ? Name : modsComp.UniqueName;
            FracturedModCount = modsComp.CountFractured;
            IsSynthesised = modsComp.Synthesised;
            Enchanted = modsComp.EnchantedMods?.Count > 0;
            ModsInfo = new ModsData(modsComp.ItemMods, modsComp.EnchantedMods, modsComp.ExplicitMods, modsComp.FracturedMods, modsComp.ImplicitMods, modsComp.ScourgeMods, modsComp.SynthesisMods, modsComp.CrucibleMods);
            ModsNames = ModsInfo.ItemMods.Select(mod => mod.Name).ToList();
            VeiledModCount = ModsInfo.ItemMods.Count(m => m.DisplayName.Contains("Veil"));
            IsBlightMap = ModsInfo.ItemMods.Any(m => m.Name.Contains("InfectedMap"));
            DeliriumStacks = ModsInfo.ItemMods.Count(m => m.Name.Contains("AfflictionMapReward"));
            IsElderGuardianMap = ModsInfo.ItemMods.Any(m => m.Name.Contains("MapElderContainsBoss"));
        }

        if (item.TryGetComponent<Sockets>(out var socketComp))
        {
            // issue to be resolved in core, if a corrupted ring with sockets gets a new implicit it will still have the component but the component logic will throw an exception
            if (socketComp.NumberOfSockets > 0)
                SocketInfo = new SocketData(socketComp.LargestLinkSize, socketComp.NumberOfSockets, socketComp.Links, socketComp.SocketGroup);
        }

        if (item.TryGetComponent<SkillGem>(out var gemComp))
        {
            GemInfo = new SkillGemData(gemComp.Level, gemComp.MaxLevel, gemComp.QualityType);
        }

        if (item.TryGetComponent<Stack>(out var stackComp))
        {
            StackInfo = new StackData(stackComp.Size, stackComp.Info.MaxStackSize);
        }

        if (item.TryGetComponent<ExileCore.PoEMemory.Components.Map>(out var mapComp))
        {
            MapTier = mapComp.Tier;
            IsMap = true;
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
            ArmourInfo = new ArmourData(armourComp.ArmourScore, armourComp.EvasionScore, armourComp.EnergyShieldScore);
        }

        if (item.TryGetComponent<Shield>(out var shieldComp))
        {
            ShieldBlockChance = shieldComp.ChanceToBlock;
        }
    }

    private static PlayerData CreatePlayerData(GameController gameController)
    {
        return gameController.Player.TryGetComponent<Player>(out var playerComp) 
            ? new PlayerData(playerComp.Level, playerComp.Strength, playerComp.Dexterity, playerComp.Intelligence) 
            : new PlayerData(0, 0, 0, 0);
    }

    public bool HasUnorderedSocketGroup(string groupText)
    {
        return SocketInfo.SocketGroups.Any(x =>
            x.Length >= groupText.Length &&
            ParseSocketString(x) is var group &&
            ParseSocketString(groupText) is var request &&
            request.Literals.Sum(g => Math.Max(g.Count() - group.Literals[g.Key].Count(), 0)) <= group.Whites - request.Whites
        );
    }

    public bool HasSockets(string socketText)
    {
        return string.Concat(SocketInfo.SocketGroups) is var sockets &&
               sockets.Length >= socketText.Length &&
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

    public IReadOnlyDictionary<GameStat, int> ItemStats => GetItemStats(ModsInfo.ItemMods);

    public IReadOnlyDictionary<GameStat, int> GetItemStats(IEnumerable<ItemMod> list)
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

    public bool HasTag(string wantedTag) => Tags.Concat(PathTags).Select(s => s.ToLower()).Any(s => wantedTag.ToLower().Contains(s));
    public bool HasTagCase(string wantedTag) => Tags.Concat(PathTags).Select(s => s).Any(s => wantedTag.Contains(s));

    public bool HasTag(List<string> tags, string wantedTag) => tags.Select(s => s.ToLower()).Any(s => wantedTag.ToLower().Contains(s));
    public bool HasTagCase(List<string> tags, string wantedTag) => tags.Select(s => s).Any(s => wantedTag.Contains(s));

    public bool ContainsString(string @string, params string[] wantedStrings) => wantedStrings.Select(s => s.ToLower()).Any(s => @string.ToLower().Contains(s));
    public bool ContainsStringCase(string @string, params string[] wantedStrings) => wantedStrings.Select(s => s).Any(s => @string.Contains(s));

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