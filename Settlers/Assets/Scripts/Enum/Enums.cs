using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public enum VillageKind {Settlement,City,TradeMetropole,PoliticsMetropole,ScienceMetropole};
[Serializable]
public enum ResourceKind {Wool,Lumber,Ore,Brick,Grain};
[Serializable]
public enum CommodityKind {Coin,Cloth,Paper};
[Serializable]
public enum ProgressCardKind {AlchemistCard
	,CraneCard
	,EngineerCard
	,InventorCard
	,IrrigationCard
	,MedicineCard
	,MiningCard
	,PrinterCard
	,RoadBuildingCard
	,SmithCard
	,BishopCard
	,ConstitutionCard
	,DeserterCard
	,DiplomatCard
	,IntrigueCard
	,SaboteurCard
	,SpyCard
	,WarlordCard
	,WeddingCard
	,ComercialHarborCard
	,MasterMerchantCard
	,MerchantCard
	,MerchantFleetCard
	,ResourceMonopolyCard
	,TradeMonopolyCard
    ,NoCard};

public enum PlayerStatus {Offline,Available,InGame,Ready};

public enum EventKind {Barbarian,Trade,Politics,Science};
[Serializable]
public enum GamePhase {ReadyToJoin,SetupRoundOne,SetupRoundTwo,TurnFirstPhase,TurnDiceRolled,TurnRobberPirate,
    ForcedKnightMove,TurnRobberOnly,TurnPirateOnly,TurnDesertKnight, DesertKnightMove, Intrigue, TurnMerchant};

[Serializable]
public enum HarbourKind{
    Generic
	,Lumber
    ,Wool
    ,Ore
    ,Grain
    ,Brick
    ,None};
[Serializable]
public enum TerrainKind{Sea
	,Desert
	,Pasture
	,Forest
	,Mountains
	,Hills
	,Fields
	,GoldMine
    ,None};
[Serializable]
public enum KnightLevel
{
    Basic,
    Strong,
    Mighty,
    None
}
