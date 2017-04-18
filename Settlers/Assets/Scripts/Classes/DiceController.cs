using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

public class DiceController
{
    //the random has to be static as if you reset each time its always the same value as the seed remains the same
    //static wont reset it
    static Random roller = new Random();
    //tile pools and token pools for balanced board
    ArrayList tilePool = new ArrayList();
    ArrayList tokenPool = new ArrayList();
    ArrayList harborPool = new ArrayList();
    //seperate card pools
    ArrayList politicsPool = new ArrayList();
    ArrayList sciencePool = new ArrayList();
    ArrayList tradePool = new ArrayList();

    int redDiceValue, yellowDiceValue, eventDiceValue, terrainKind, tokenValue;


    public DiceController()
    {
        for(int i = 0; i<5; i++)
        {
            //added once to the pool
            if (i < 1)
            {
                //add to pool one of each type
                harborPool.Add(HarbourKind.Brick);
                harborPool.Add(HarbourKind.Wool);
                harborPool.Add(HarbourKind.Lumber);
                harborPool.Add(HarbourKind.Grain);
                harborPool.Add(HarbourKind.Ore);

                //added science cards
                sciencePool.Add(ProgressCardKind.EngineerCard);
                sciencePool.Add(ProgressCardKind.PrinterCard);

                //added politics cards
                politicsPool.Add(ProgressCardKind.ConstitutionCard);
            }
            //added twice
            if (i < 2)
            {
                tokenPool.Add(2);
                tokenPool.Add(12);
                
                tilePool.Add(TerrainKind.GoldMine);

                //added science cards
                sciencePool.Add(ProgressCardKind.AlchemistCard);
                sciencePool.Add(ProgressCardKind.InventorCard);
                sciencePool.Add(ProgressCardKind.CraneCard);
                sciencePool.Add(ProgressCardKind.IrrigationCard);
                sciencePool.Add(ProgressCardKind.MedicineCard);
                sciencePool.Add(ProgressCardKind.MiningCard);
                sciencePool.Add(ProgressCardKind.RoadBuildingCard);
                sciencePool.Add(ProgressCardKind.SmithCard);

                //added politics cards
                politicsPool.Add(ProgressCardKind.BishopCard);
                politicsPool.Add(ProgressCardKind.DiplomatCard);
                politicsPool.Add(ProgressCardKind.DeserterCard);
                politicsPool.Add(ProgressCardKind.IntrigueCard);
                politicsPool.Add(ProgressCardKind.SaboteurCard);
                politicsPool.Add(ProgressCardKind.WarlordCard);
                politicsPool.Add(ProgressCardKind.WeddingCard);

                //added trade cards
                tradePool.Add(ProgressCardKind.ComercialHarborCard);
                tradePool.Add(ProgressCardKind.MasterMerchantCard);
                tradePool.Add(ProgressCardKind.MerchantFleetCard);
                tradePool.Add(ProgressCardKind.ResourceMonopolyCard);
                tradePool.Add(ProgressCardKind.ResourceMonopolyCard);
                tradePool.Add(ProgressCardKind.TradeMonopolyCard);
            }
            //basically added 3 times
            if( i < 3)
            {          
                tokenPool.Add(3);
                tokenPool.Add(4);
                tokenPool.Add(10);
                tokenPool.Add(11);
                tokenPool.Add(5);
                tokenPool.Add(9);
                tokenPool.Add(6);
                tokenPool.Add(8);

                tilePool.Add(TerrainKind.Desert);

                //added politics cards
                politicsPool.Add(ProgressCardKind.SpyCard);

                //added trade cards 6 times total 3x2
                tradePool.Add(ProgressCardKind.MerchantCard);
                tradePool.Add(ProgressCardKind.MerchantCard);
            }
            //added 4 times
            if (i < 4)
            {
                harborPool.Add(HarbourKind.Generic);
            }
            //added 5 times
            tilePool.Add(TerrainKind.Hills);
            tilePool.Add(TerrainKind.Mountains);
            tilePool.Add(TerrainKind.Pasture);
            tilePool.Add(TerrainKind.Fields);
            tilePool.Add(TerrainKind.Forest);
        }
        for(int i = 0; i<19; i++)
        {
            tilePool.Add(TerrainKind.Sea);
        }
        

    }   
    public void rollDice()
    {
        redDiceValue = roller.Next(1, 7);
        yellowDiceValue = roller.Next(1, 7);
        eventDiceValue = roller.Next(1, 6);
    }
    public void rollTile()
    {
        int tileIndex = roller.Next(0, tilePool.Count);
        int tokenIndex = roller.Next(0, tokenPool.Count);
        if (tilePool.Count > 0 && tokenPool.Count > 0)
        {
            terrainKind = (int)tilePool[tileIndex];
            tokenValue = (int)tokenPool[tokenIndex];
        }
        else
        {
            terrainKind = (int)tilePool[0];
            tokenValue = (int)tokenPool[0];
        }
        //we dont waste token pool on sea and desert;
        if(terrainKind != (int)TerrainKind.Desert && terrainKind != (int)TerrainKind.Sea)
        {
            tokenPool.Remove(tokenValue);
        }
        tilePool.Remove((TerrainKind)terrainKind);
        
    }

    #region Cards
    public void returnCard(ProgressCardKind k)
    {
        if((int)k < 10)
        {
            sciencePool.Add(k);
        }
        else if((int)k < 19)
        {
            politicsPool.Add(k);
        }
        else
        {
            tradePool.Add(k);
        }
    }
    public bool hasCardInDeck(ProgressCardKind k)
    {
        bool check = false;
        if ((int)k < 10 && sciencePool.Contains(k))
        {
            check = true;
        }
        else if ((int)k < 19 && politicsPool.Contains(k))
        {
            check = true;
        }
        else if (tradePool.Contains(k))
        {
            check = true;
        }
        return check;
    }

    public ProgressCardKind getCard(ProgressCardKind k)
    {
        ProgressCardKind card = ProgressCardKind.NoCard;
        if ((int)k < 10)
        {
            card = k; 
            sciencePool.Remove(k);
        }
        else if ((int)k < 19 && politicsPool.Contains(k))
        {
            card = k;
            politicsPool.Remove(k);
        }
        else if (tradePool.Contains(k))
        {
            card = k;
            tradePool.Remove(k);
        }
        return card;
    }

    public string[] returnPoliticDeck()
    {
        string[] deck = new string[politicsPool.Count];
        int i = 0;
        foreach(object o in politicsPool)
        {
            deck[i] = o.ToString();
            i++;
        }
        return deck;
    }
    public string[] returnScienceDeck()
    {
        string[] deck = new string[sciencePool.Count];
        int i = 0;
        foreach (object o in sciencePool)
        {
            deck[i] = o.ToString();
            i++;
        }
        return deck;
    }
    public string[] returnTradeDeck()
    {
        string[] deck = new string[tradePool.Count];
        int i = 0;
        foreach (object o in tradePool)
        {
            deck[i] = o.ToString();
            i++;
        }
        return deck;
    }
    #endregion

    public ProgressCardKind rollCard(EventKind k)
    {
        switch (k)
        {
            case EventKind.Science:
                {
                    ProgressCardKind to_ret = (ProgressCardKind)sciencePool[roller.Next(0, sciencePool.Count)];
                    sciencePool.Remove(to_ret);
                    return to_ret;
                }
            case EventKind.Trade:
                {
                    ProgressCardKind to_ret = (ProgressCardKind)tradePool[roller.Next(0, tradePool.Count)];
                    tradePool.Remove(to_ret);
                    return to_ret;
                }
            case EventKind.Politics:
                {
                    ProgressCardKind to_ret = (ProgressCardKind)politicsPool[roller.Next(0, politicsPool.Count)];
                    politicsPool.Remove(to_ret);
                    return to_ret;
                }
            default:
                {
                    //will actually never return this its just to shut up compiler.
                    return ProgressCardKind.NoCard;
                }
        }
    }



    public int getRed()
    {
        return redDiceValue;
    }
    public int getYellow()
    {
        return yellowDiceValue;
    }
    // Returns the current event role as a kind
    public EventKind getEventKind()
    {
        switch (eventDiceValue)
        {
            case 4:
                return EventKind.Politics;
            case 5:
                return EventKind.Science;
            case 6:
                return EventKind.Trade;
            default:
                return EventKind.Barbarian;
        }
    }

    public int getTerrain()
    {
        return terrainKind;
    }
    public int getToken()
    {
        return tokenValue;
    }
    
    public HarbourKind getHarbour()
    {
        if (harborPool.Count > 0)
        {
            HarbourKind harbourType = (HarbourKind)harborPool[roller.Next(0, harborPool.Count)];
            harborPool.Remove(harbourType);
            return harbourType;
        }
        else
        {
            return HarbourKind.None;
        }
        
    }

    public bool HasCardsInPool(EventKind k)
    {
        bool to_ret = false;
        switch (k)
        {
            case EventKind.Science:
                {
                    if(sciencePool.Count != 0)
                    {
                        to_ret = true;
                    }
                    break;
                }
            case EventKind.Trade:
                {
                    if (tradePool.Count != 0)
                    {
                        to_ret = true;
                    }
                    return to_ret;
                }
            case EventKind.Politics:
                {
                    if (politicsPool.Count != 0)
                    {
                        to_ret = true;
                    }
                    return to_ret;
                }

        }
        return to_ret;
    }
}

