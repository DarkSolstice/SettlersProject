using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class Player {
    public int myColor;
    public static int playerCount = 0;
    public Dictionary<CommodityKind, int> cityImprovementLevels { get; set; }
    public Dictionary<ResourceKind, int> resources { get; set; }
    public Dictionary<CommodityKind, int> commodities { get; set; }

    public int gold { get; private set; }
    public int victoryPoints { get; private set; }
    public int fishTokens { get; private set; }
    public List<OwnableUnit> ownedUnits { get; set; }
    public List<HarbourKind> ownedHarbour { get; set; }										 

	public int numberofCityWalls{ get; set;}
	
    public List<VillageKind> settlementPool { get; set; }

    public List<VillageKind> citiesPool { get; set; }

    public List<ProgressCardKind> cardsInHand { get; set; }

    public List<KnightLevel> knightTokens { get; set; }

    public string name;

    public bool hasMerchant { get; private set; }
    public ResourceKind merchantResource { get; set; }
    public bool hasLongestTradeRoute { get; private set; }

    public bool hasBoot = false;

    public bool hasFreeRoad = false;

    public int availableWalls { get; set; }

    //temp variables for forced knight moves
    public Knight storedKnight;
    public Intersection storedInter;
    public bool hasToMoveKnight = false;

    //temp variable for ship/knight move
    public Edges selectedShip;
    public Intersection selectedKnight;

    //temp variable for deserter
    public KnightLevel desertKnightLevel;
    public bool desertKnightActive;

    //TO SAVE!!: stores intersections where first 2 cities were built
    public Intersection firstCity;
    public Intersection secondCity;

    public Player()
    {
        myColor = playerCount;
        playerCount++;
        hasMerchant = false;
        hasLongestTradeRoute = false;
		availableWalls = 3;												   
        // Possibly move this code to a constructor
        resources = new Dictionary<ResourceKind, int>()
        {
            { ResourceKind.Brick, 0 },
            { ResourceKind.Grain, 0 },
            { ResourceKind.Lumber, 0 },
            { ResourceKind.Ore, 0 },
            { ResourceKind.Wool, 0 }
        };
        commodities = new Dictionary<CommodityKind, int>()
        {
            { CommodityKind.Cloth, 0 },
            { CommodityKind.Coin, 0 },
            { CommodityKind.Paper, 0 }
        };
        cityImprovementLevels = new Dictionary<CommodityKind, int>()
        {
            { CommodityKind.Cloth, 0 },
            { CommodityKind.Coin, 0 },
            { CommodityKind.Paper, 0 }
        };
        gold = 20;
        fishTokens = 20;
        ownedUnits = new List<OwnableUnit>();
        ownedHarbour = new List<HarbourKind>();
        citiesPool = new List<VillageKind>();
        settlementPool = new List<VillageKind>();
        cardsInHand = new List<ProgressCardKind>();
        knightTokens = new List<KnightLevel>();

        //add the tokens in the pool
        for (int i = 0; i < 4; i++)
        {
            citiesPool.Add(VillageKind.City);
            settlementPool.Add(VillageKind.Settlement);
            if (i < 3)
            {
                //add 3 of each knight that can be built
                knightTokens.Add(KnightLevel.Basic);
                knightTokens.Add(KnightLevel.Mighty);
                knightTokens.Add(KnightLevel.Strong);
            }
        }
        settlementPool.Add(VillageKind.Settlement);
        victoryPoints = 0;

    }

    #region Player attributes Manipulation
    public void AddResources(int quantity, ResourceKind resourceKind)
    {
        resources[resourceKind] += quantity;
    }
    public void AddCommodities(int quantity, CommodityKind commodityKind)
    {
        commodities[commodityKind] += quantity;
    }
    public void AddGold(int delta)
    {
        this.gold += delta;
    }
    public void AddVictoryPoints(int value)
    {
        victoryPoints += value;
    }
    public void AddFishTokens(int value)
    {
        fishTokens += value;
    }
    public void PayFishTokens(int value)
    {
        fishTokens -= value;
    }

    public void PayResources(int quantity, ResourceKind resourceKind)
    {
        resources[resourceKind] -= quantity;
    }
    public void PayCommoditys(int quantity, CommodityKind commodityKind)
    {
        commodities[commodityKind] -= quantity;
    }
    public void PaySettlementResources()
    {
        PayResources(1, ResourceKind.Grain);
        PayResources(1, ResourceKind.Lumber);
        PayResources(1, ResourceKind.Brick);
        PayResources(1, ResourceKind.Wool);
    }
    public void PayRoadResources()
    {
        PayResources(1, ResourceKind.Lumber);
        PayResources(1, ResourceKind.Brick);
    }
    public void PayShipResources()
    {
        PayResources(1, ResourceKind.Lumber);
        PayResources(1, ResourceKind.Wool);
    }
    public void PayKnightResources()
    {
        PayResources(1, ResourceKind.Wool);
        PayResources(1, ResourceKind.Ore);
    }
    public void PayKnightActivationResources()
    {
        PayResources(1, ResourceKind.Grain);
    }

    public void RemoveCity()
    {
        citiesPool.Remove(VillageKind.City);
    }
    public void RemoveSettlement()
    {
        settlementPool.Remove(VillageKind.Settlement);
    }
    public void RemoveKnight(KnightLevel level)
    {
        knightTokens.Remove(level);
    }

    public void AddCity()
    {
        citiesPool.Add(VillageKind.City);
    }
    public void AddSettlement()
    {
        settlementPool.Add(VillageKind.Settlement);
    }
    public void AddKnight(KnightLevel level)
    {
        knightTokens.Add(level);
    }

    #endregion

    #region GameActions
    public void MaritimeTrade(ResourceKind traded, ResourceKind returned)
    {
        PayResources(4, traded);
        AddResources(1, returned);
    }

    public void UpgradeSettlementToCity(Village settlement, bool playedMedicinePC)
    {
        if (playedMedicinePC)
        {
            PayResources(1, ResourceKind.Grain);
            PayResources(2, ResourceKind.Ore);
        }
        else
        {
            PayResources(2, ResourceKind.Grain);
            PayResources(3, ResourceKind.Ore);
        }
        settlement.setVillageType(VillageKind.City);
    }

    public void payCityResources(bool playedMedicinePC)
    {
        if (playedMedicinePC)
        {
            PayResources(1, ResourceKind.Grain);
            PayResources(2, ResourceKind.Ore);
        }
        else
        {
            PayResources(2, ResourceKind.Grain);
            PayResources(3, ResourceKind.Ore);
        }
    }

    public void improveCity(CommodityKind kind, bool playedCraneCard)
    {
        //pay and upgrade
		if (playedCraneCard)
		{
			PayCommoditys(GetCityImprovementLevel(kind), kind);
		}
		else
		{
        	PayCommoditys(GetCityImprovementLevel(kind) + 1, kind);
		}
        cityImprovementLevels[kind] += 1;
    }

	public void payWallResources(bool playedEngPC)
	{
		if (!playedEngPC)
		{	
			PayResources(2, ResourceKind.Brick);
		}
	}				  									   
    public void GiveLongestTradeRoute()
    {
        this.hasLongestTradeRoute = true;
        this.AddVictoryPoints(2);
    }

    public void TakeLongestRoad()
    {
        this.hasLongestTradeRoute = false;
        this.AddVictoryPoints(-2);
    }

    public void GiveMerchant()
    {
        this.hasMerchant = true;
        this.AddVictoryPoints(1);
    }

    public void TakeMerchant()
    {
        this.hasMerchant = false;
        this.AddVictoryPoints(-1);
    }
    #endregion

    #region Return
    public bool HasCityUpgradeResources(bool playedMedicinePC)
    {
        if (playedMedicinePC)
        {
            return HasResources(1, ResourceKind.Grain) && HasResources(2, ResourceKind.Ore);
        }
        else
        {
            return HasResources(2, ResourceKind.Grain) && HasResources(3, ResourceKind.Ore);
        }
    }

    public int SumResources()
    {
        int sum = 0;
        IEnumerator counter = resources.Values.GetEnumerator();
        while (counter.MoveNext())
        {
            sum += (int)counter.Current;
        }
        counter = commodities.Values.GetEnumerator();
        while (counter.MoveNext())
        {
            sum += (int)counter.Current;
        }
        return sum;
    }

    public bool HasResources(int quantity, ResourceKind resourceKind)
    {
        return resources[resourceKind] >= quantity;
    }

    public bool HasCommodities(int quantity, CommodityKind commodityKind)
    {
        return commodities[commodityKind] >= quantity;
    }

    public bool HasSettlementResources()
    {
        if (HasResources(1, ResourceKind.Grain) && HasResources(1, ResourceKind.Lumber) && HasResources(1, ResourceKind.Brick) && HasResources(1, ResourceKind.Wool))
        {
            return true;
        }
        return false;
    }

    public bool HasRoadResources()
    {
        if (HasResources(1, ResourceKind.Lumber) && HasResources(1, ResourceKind.Brick))
        {
            return true;
        }
        return false;
    }

    public bool HasShipResources()
    {
        if (HasResources(1, ResourceKind.Wool) && HasResources(1, ResourceKind.Brick))
        {
            return true;
        }
        return false;
    }

    public bool HasKnightResources()
    {
        return this.HasResources(1, ResourceKind.Wool) && this.HasResources(1, ResourceKind.Ore);
    }

    public bool HasKnightActivatingResources()
    {
        return this.HasResources(1, ResourceKind.Grain);
    }

public bool HasWallResources(bool playedEngCard) {
		if (!playedEngCard) {
			return this.HasResources (2, ResourceKind.Brick);
		}
		return true;
	}													  
    public int GetCityImprovementLevel(CommodityKind kind)
    {
        return cityImprovementLevels[kind];
    }

    public int[] getResourceValues()
    {
        int[] resourceValues = new int[8];
        resourceValues[0] = resources[ResourceKind.Brick];
        resourceValues[1] = resources[ResourceKind.Ore];
        resourceValues[2] = resources[ResourceKind.Wool];
        resourceValues[3] = commodities[CommodityKind.Coin];
        resourceValues[4] = resources[ResourceKind.Grain];
        resourceValues[5] = commodities[CommodityKind.Cloth];
        resourceValues[6] = resources[ResourceKind.Lumber];
        resourceValues[7] = commodities[CommodityKind.Paper];

        return resourceValues;
    }

    // Get the number of active knights owned by this player
    public int getActiveKnightStrength()
    {
        var total = 0;
        foreach (OwnableUnit u in ownedUnits)
        {
            if (u is Knight)
            {
                var k = u as Knight;
                if (k.isKnightActive())
                    total += k.level == KnightLevel.None ? 0 : (int)k.level + 1;
            }
        }
        return total;
    }

    // Get the number of city villages owned by this player
    public int getCityCount()
    {
        return getCities().Count;
    }

    // Get the total number of metropolises owned by this player of any kind
    public int getMetropolisCount()
    {
        return getMetropolises().Count;
    }

    // get a list of villages that are cities
    public List<Village> getCities()
    {
        List<Village> to_ret = new List<Village>();
        foreach (OwnableUnit own in ownedUnits)
        {
            if (own is Village)
            {
                Village vil = own as Village;
                if (vil.myKind == VillageKind.City)
                    to_ret.Add(vil);
            }
        }

        return to_ret;
    }

    // Get a list of villages that are metropolises
    public List<Village> getMetropolises()
    {
        List<Village> to_ret = new List<Village>();
        foreach (OwnableUnit own in ownedUnits)
        {
            if (own is Village)
            {
                Village vil = own as Village;
                if (vil.myKind == VillageKind.PoliticsMetropole || vil.myKind == VillageKind.ScienceMetropole || vil.myKind == VillageKind.TradeMetropole)
                    to_ret.Add(vil);
            }
        }

        return to_ret;
    }

    public bool HasSettlements()
    {
        if (settlementPool.Contains(VillageKind.Settlement))
        {
            return true;
        }
        return false;
    }

    public bool HasCities()
    {
        if (citiesPool.Contains(VillageKind.City))
        {
            return true;
        }
        return false;
    }

    public bool HasWalls()
	{
		if (availableWalls > 0)
		{
			return true;
		}
		return false;
	}										 
    public bool HasKnights(KnightLevel level)
    {
        return knightTokens.Contains(level);
    }

    #endregion

    public static Player Load(PlayerData data)
    {
        var p = new Player();
        p.name = data.name;
        p.myColor = data.myColor;
        p.cityImprovementLevels = data.cityImprovementLevels;
        p.commodities = data.commodities;
        p.resources = data.resources;
        p.gold = data.gold;
        p.victoryPoints = data.victoryPoints;
        p.ownedHarbour = data.ownedHarbour;
        p.cardsInHand = data.cardsInHand;
        p.hasMerchant = data.hasMerchant;
        p.merchantResource = data.merchantResource;
        p.hasLongestTradeRoute = data.hasLongestTradeRoute;
        p.settlementPool = data.settlementPool;
        p.citiesPool = data.citiesPool;
        p.knightTokens = data.knightTokens;
        foreach (OwnableUnitData o in data.ownedUnits)
        {
            if (o is VillageData)
            {
                p.ownedUnits.Add(Village.Load((VillageData)o, p));
            }
            else if (o is KnightData)
            {
                p.ownedUnits.Add(Knight.Load((KnightData)o, p));
            }
        }
        return p;
    }
}

[Serializable]
public class PlayerData
{
    public int myColor { get; set; }
    public Dictionary<CommodityKind, int> cityImprovementLevels { get; set; }
    public Dictionary<ResourceKind, int> resources { get; set; }
    public Dictionary<CommodityKind, int> commodities { get; set; }
    public int gold { get; private set; }
    public int victoryPoints { get; private set; }
    public List<OwnableUnitData> ownedUnits { get; set; }
    public List<HarbourKind> ownedHarbour { get; set; }
    public List<ProgressCardKind> cardsInHand { get; set; }
    public string name { get; set; }
    public bool hasMerchant { get; set; }
    public ResourceKind merchantResource { get; set; }
    public bool hasLongestTradeRoute { get; set; }
    public List<VillageKind> settlementPool { get; set; }
    public List<VillageKind> citiesPool { get; set; }
    public List<KnightLevel> knightTokens { get; set; }
    public PlayerData(Player source)
    {
        this.name = source.name;
        this.myColor = source.myColor;
        this.cityImprovementLevels = source.cityImprovementLevels;
        this.resources = source.resources;
        this.commodities = source.commodities;
        this.gold = source.gold;
        this.victoryPoints = source.victoryPoints;
        this.ownedHarbour = source.ownedHarbour;
        this.cardsInHand = source.cardsInHand;
        this.hasMerchant = source.hasMerchant;
        this.merchantResource = source.merchantResource;
        this.hasLongestTradeRoute = source.hasLongestTradeRoute;
        this.settlementPool = source.settlementPool;
        this.citiesPool = source.citiesPool;
        this.knightTokens = source.knightTokens;
        this.ownedUnits = source.ownedUnits.Select<OwnableUnit, OwnableUnitData>(u =>
        {
            if (u is Village)
                return new VillageData((Village)u);
            else if (u is Knight)
                return new KnightData((Knight)u);
            else return null;
        }).ToList();
    }
}