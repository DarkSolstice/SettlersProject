using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class Game : NetworkBehaviour
{
    public const int BARB_ATTACK_POSITION = 7;
    public int defenders = 0;
    static System.Random rng = new System.Random();

    DiceController gameDices = new DiceController();
    public bool waitingForRoad = false;
    public bool firstBarbAttack = false;
    public bool bootDistributed = false;
    public bool stealAll = false;

    public int barbPosition = 0; // Max 7. Use MoveBarbs()
    public GamePhase currentPhase { get; private set; }

    public Dictionary<GameObject, Player> gamePlayers = new Dictionary<GameObject, Player>();
    //inverse for easier lookup only max 4 values so
    public Dictionary<Player, GameObject> playerObjects = new Dictionary<Player, GameObject>();
    //
    public IEnumerator currentPlayer;

    public GamePhase tempPhase;
    private Player ForcedMovePlayer;
    private Player playedDeserter;

    private string currentPlayerString;
    public Dictionary<GameObject, Player> reverseOrder = new Dictionary<GameObject, Player>();

    //added all the references for easier algorithm writing
    public GameObject[] boardTile;
    public GameObject[] edges;
    public GameObject[] intersections;
    public GameObject canvas;

    //keep track of the tile where the robber is
    public GameObject robberTile, pirateTile, merchantTile, lakeTile;

    public List<ProgressCardKind> CardsInPlay = new List<ProgressCardKind>();

    public bool isLoaded = false;

    private Dictionary<string, Player> tempPlayersByName;
    private VillageKind metropolisType = VillageKind.City;

    //for checking if ship was built this turn;
    private List<Edges> shipsBuiltThisTurn = new List<Edges>();

    private TerrainHex readyToScare;

    #region Initial Setup
    void Start()
    {
        if (!isLoaded)
        {
            currentPhase = GamePhase.SetupRoundOne;
            setupBoard();
        }
        this.pirateTile = null;
        this.merchantTile = null;
    }

    public void ValidateName(GameObject player, string name)
    {
        var isValidName = !isLoaded || tempPlayersByName.ContainsKey(name);
        player.transform.GetComponent<playerControl>().isValidName = isValidName;
        if (isValidName)
        {
            setPlayerName(player, name);
        }

        player.GetComponent<playerControl>().RpcNameCheck(isValidName);										   

    }

    //setup references for the game
    public void setPlayer(GameObject setPlayer)
    {
        Player temp = new Player();
        gamePlayers.Add(setPlayer, temp);
        playerObjects.Add(temp, setPlayer);
        updatePlayerResourcesUI(setPlayer);
        reverseOrder = gamePlayers.Reverse().ToDictionary(x => x.Key, x => x.Value);
    }

    public void setPlayerName(GameObject player, string name)
    {
        if (isLoaded)
        {
            var p = tempPlayersByName[name];
            gamePlayers.Add(player, p);
            playerObjects.Add(p, player);
            updatePlayerResourcesUI(player);
            reverseOrder = gamePlayers.Reverse().ToDictionary(x => x.Key, x => x.Value);
            tempPlayersByName.Remove(name);
            if (tempPlayersByName.Count == 0)
            {
                tempPlayersByName = null;
                // All players have joined, we can try setting the current player
                this.currentPlayer = gamePlayers.Values.GetEnumerator();
                currentPlayer.MoveNext();
                while(((Player)currentPlayer.Current).name != currentPlayerString)
                {
                    currentPlayer.MoveNext();
                }
            }
        }
        else
        {
            gamePlayers[player].name = name;
        }
    }

    private void setupBoard()
    {
        bool robberSpawned = false;
        bool lakeSpawned = false;
        int fishToken = 4;
        System.Random temp = new System.Random();
        foreach (GameObject tile in boardTile)
        {
            gameDices.rollTile();
            tile.GetComponent<TerrainHex>().setTile(gameDices.getTerrain(), gameDices.getToken());
            TerrainHex hex = tile.GetComponent<TerrainHex>();
            if (gameDices.getTerrain() == (int)TerrainKind.Desert && !robberSpawned)
            {
                hex.isRobber = true;
                robberTile = tile;
                robberSpawned = true;
            }
            else if (gameDices.getTerrain() == (int)TerrainKind.Desert && robberSpawned && !lakeSpawned)
            {
                hex.isLake = true;
                lakeTile = tile;
                lakeSpawned = true;
            }
        }
        foreach (GameObject road in edges)
        {
            bool hasSea = false; bool hasLand = false; bool hasProximity = false;
            if (road.GetComponent<Edges>().inBetween.Length == 2)
            {
                foreach (TerrainHex hex in road.GetComponent<Edges>().inBetween)
                {
                    if (hex.myTerrain == TerrainKind.Sea)
                    {
                        hasSea = true;
                    }
                    else
                    {
                        hasLand = true;
                    }
                    foreach (Intersection inter in road.GetComponent<Edges>().endPoints)
                    {
                        if (inter.harbor != HarbourKind.None)
                        {
                            hasProximity = true;
                        }
                    }
                }

            }
            if (road.GetComponent<Edges>().inBetween.Length == 1)
            {
                // check doesnt need to be on edge we automatically assume outside board is sea
                hasSea = true;
                foreach (TerrainHex hex in road.GetComponent<Edges>().inBetween)
                {
                    if (hex.myTerrain != TerrainKind.Sea)
                    {
                        hasLand = true;
                    }
                    foreach (Intersection inter in road.GetComponent<Edges>().endPoints)
                    {
                        if (inter.harbor != HarbourKind.None)
                        {
                            hasProximity = true;
                        }
                    }
                }
            }
            int luck = temp.Next(0, 7);
            if (luck == 0 && hasSea && hasLand && !hasProximity)
            {
                HarbourKind type = gameDices.getHarbour();
                foreach (Intersection inter in road.GetComponent<Edges>().endPoints)
                {
                    inter.harbor = type;
                }
                road.GetComponent<Edges>().setHarborKind(type);
            }

        }
        //fishing docks spawning
        foreach (GameObject tile in boardTile)
        {
            TerrainHex hex = tile.GetComponent<TerrainHex>();

            if (hex.myTerrain == (int)TerrainKind.Sea)
            {
                bool proximity = false;
                bool isBorder = false;
                foreach (Intersection inter in hex.corners)
                {
                    if (inter.isFishingInter)
                    {
                        proximity = true;
                    }
                }

                foreach (Intersection inter in hex.corners)
                {
                    if (!hex.hasFishing)
                    {
                        int i = 0;
                        Intersection[] fishingInters = new Intersection[3];
                        bool hasLand = true;
                        fishingInters[i] = inter; i++;
                        foreach (Edges e in inter.paths)
                        {
                            if (e.inBetween.Length == 2 && (hex == e.inBetween[0] || hex == e.inBetween[1]))
                            {
                                //one of the point swill be completely in water
                                if ((e.inBetween[0].myTerrain == TerrainKind.Sea || e.inBetween[0].isLake) && (e.inBetween[1].myTerrain == TerrainKind.Sea || e.inBetween[1].isLake))
                                {
                                    hasLand = false;
                                }
                                foreach (Intersection inter2 in e.endPoints)
                                {
                                    foreach (TerrainHex hex2 in inter2.linked)
                                    {
                                        if (hex2.hasFishing)
                                        {
                                            proximity = true;
                                        }
                                    }
                                    if (inter2.harbor != HarbourKind.None)
                                    {
                                        proximity = true;
                                    }
                                    else if (!inter2.Equals(inter))
                                    {
                                        fishingInters[i] = inter2;
                                        i++;
                                    }
                                }
                            }
                            else if (e.inBetween.Length == 2)
                            {

                            }
                            else
                            {
                                isBorder = true;
                            }

                        }
                        if (!proximity && hasLand && fishToken < 11 && !isBorder)
                        {
                            fishingInters[0].isFishingInter = true;
                            fishingInters[1].isFishingInter = true;
                            fishingInters[2].isFishingInter = true;
                            hex.hasFishing = true;
                            hex.numberToken = fishToken;
                            if(fishToken == 6)
                            {
                                fishToken++;
                            }
                            fishToken++;
                        }
                    }
                }

            }
        }

    }
    #endregion


    #region UI Updates

    public void updateTurn()
    {
        IEnumerator keys = (gamePlayers.Keys).GetEnumerator();
        bool remaining = true;
        //set to first player
        keys.MoveNext();
        while (remaining)
        {
            GameObject player = (GameObject)keys.Current;
            string playerTurn = playerTurn = ((Player)(currentPlayer.Current)).name;
            switch (currentPhase)
            {
                case GamePhase.TurnDiceRolled: playerTurn += " Roll Dice"; break;
                case GamePhase.SetupRoundOne: playerTurn += " First Setup"; break;
                case GamePhase.SetupRoundTwo: playerTurn += " Second Setup"; break;
                case GamePhase.TurnFirstPhase: playerTurn += " Build & Trade"; break;
                case GamePhase.TurnRobberPirate: playerTurn += " Move Robber or Pirate"; break;
				case GamePhase.ForcedKnightMove: playerTurn = ForcedMovePlayer.name; playerTurn += " Forced Knight Move"; break;
                case GamePhase.TurnMerchant: playerTurn += " Must place the merchant"; break;
                case GamePhase.TurnRobberOnly: playerTurn += " Move Robber "; break;
                case GamePhase.TurnPirateOnly: playerTurn += " Move Pirate "; break;
                case GamePhase.TurnDesertKnight: playerTurn += " Deserter "; break;
                case GamePhase.Intrigue: playerTurn += " Intrigue "; break;

            }
            player.GetComponent<playerControl>().RpcUpdateTurn(playerTurn);
            if (!keys.MoveNext())
            {
                remaining = false;
            }
        }
    }

    public void updatePlayerResourcesUI(GameObject upPlayer)
    {
        Player data;
        gamePlayers.TryGetValue(upPlayer, out data);
        upPlayer.GetComponent<playerControl>().setTextValues(data.resources, data.commodities, data.gold, data.victoryPoints, data.fishTokens);
    }

    public void updateRollsUI()
    {
        IEnumerator keys = (gamePlayers.Keys).GetEnumerator();
        bool remaining = true;
        //set to first player
        keys.MoveNext();
        while (remaining)
        {
            GameObject player = (GameObject)keys.Current;
            player.GetComponent<playerControl>().setDiceValues(gameDices.getRed(), gameDices.getYellow(), (int)gameDices.getEventKind());
            if (!keys.MoveNext())
            {
                remaining = false;
            }
        }
    }

    public void updatePlayerStatisticsUI()
    {
        int count = 1; int i = 0;
        int[] player1 = new int[8], player2 = new int[8], player3 = new int[8], player4 = new int[8];
        string[] names = new string[gamePlayers.Count];
        IEnumerator values = (gamePlayers.Values).GetEnumerator();
        //set to first player

        while (values.MoveNext() != false)
        {
            Player temp = (Player)values.Current;
            names[i] = temp.name;
            switch (count)
            {
                case 1: player1 = temp.getResourceValues(); break;
                case 2: player2 = temp.getResourceValues(); break;
                case 3: player3 = temp.getResourceValues(); break;
                case 4: player4 = temp.getResourceValues(); break;

            }
            count++;
            i++;
        }
        canvas.GetComponent<statistics>().RpcSetStatistics(player1, player2, player3, player4, names);
    }

    //normal chat and server wide messages to inform all players, like barbarians have 1 space left to kill you get ready
    public void chatOnServer(GameObject player, string message)
    {
        IEnumerator keys = (gamePlayers.Keys).GetEnumerator();
        //set to first player

        while (keys.MoveNext() != false)
        {
            GameObject sendToPlayer = (GameObject)keys.Current;
            sendToPlayer.transform.GetComponent<playerControl>().RpcUpdateChat(gamePlayers[player].name + ": " + message + "\n");
        }
    }

    // Game messages to everyone
    public void broadcastMessage(string message)
    {
        foreach (GameObject recipient in gamePlayers.Keys)
        {
            recipient.transform.GetComponent<playerControl>().RpcUpdateChat("SettlersOfCatan: " + message + "\n");
        }
    }

    //personally sent to the player object that is referenced, used for messaging like you cant roll right now and stuff
    public void logAPlayer(GameObject player, string message)
    {
        player.transform.GetComponent<playerControl>().RpcUpdateChat(message + "\n");
    }

    public void initiateSteal(GameObject player)
    {
        if(currentPhase == GamePhase.TurnFirstPhase)
        {
            int i = 0;
            Player current = (Player)currentPlayer.Current;
            string[] names = new string[gamePlayers.Count - 1];
            IEnumerator values = gamePlayers.Values.GetEnumerator();
            while (values.MoveNext())
            {
                Player temp = (Player)values.Current;
                if (!current.name.Equals(temp.name))
                {
                    names[i] = temp.name;
                    i++;
                }
            }
            if(names.Length > 0)
            {
                player.GetComponent<playerControl>().RpcSetupStealInterface(names);
            }       
        }
        else
        {
            logAPlayer(player, "You need to be in the build/trade phase to use the shop.");
        }
        
    }
    public void initiateCardChoice(GameObject player)
    {
        player.GetComponent<playerControl>().RpcSetupCardChoiceInterface(gameDices.returnPoliticDeck(), gameDices.returnTradeDeck(), gameDices.returnScienceDeck(),false);
    }
    #endregion

    #region Game Actions
    //selection when aqueduct is used and no resources gained
    public void updateSelection(GameObject player, int value, bool toggle)
    {
        //toggle on means the steal from resourcemonopoly is used, else regular aqueduct selection
        if (toggle)
        {
            foreach(Player victim in gamePlayers.Values)
            {
                if (!victim.Equals(gamePlayers[player]))
                {
                    if (value < 5)
                    {
                        if (victim.HasResources(2, (ResourceKind)value))
                        {
                            victim.PayResources(2, (ResourceKind)value);
                            gamePlayers[player].AddResources(2, (ResourceKind)value);
                        }
                        else if (victim.HasResources(1, (ResourceKind)value))
                        {
                            victim.PayResources(1, (ResourceKind)value);
                            gamePlayers[player].AddResources(1, (ResourceKind)value);
                        }
                        else { }
                    }
                    else
                    {
                        if (victim.HasCommodities(2, (CommodityKind)(value-5)))
                        {
                            victim.PayCommoditys(2, (CommodityKind)(value - 5));
                            gamePlayers[player].AddCommodities(2, (CommodityKind)(value - 5));
                        }
                        else if (victim.HasCommodities(1, (CommodityKind)(value - 5)))
                        {
                            victim.PayCommoditys(1, (CommodityKind)(value - 5));
                            gamePlayers[player].AddCommodities(1, (CommodityKind)(value - 5));
                        }
                        else { }                       
                    }
                    updatePlayerResourcesUI(playerObjects[victim]);
                    updatePlayerResourcesUI(player);
                }
            }
        }
        else
        {
            if (value < 5)
            {
                gamePlayers[player].AddResources(1, (ResourceKind)value);
            }
            else
            {
                gamePlayers[player].AddCommodities(1, (CommodityKind)(value - 5));
            }
            updatePlayerResourcesUI(player);
        }
    }
    public void P2PTradeAccept(GameObject player)
    {

    }

    /**
	 *  P2P Trade that is in charge of the player to player trade.
	 *  author xingwei
	 * 
	 *  Brick, Ore, Wool, Coin, Wheat, Cloth, Lumber, Paper, Gold
	 *  
	 *  1. Check if the player is the current player and the player is in the correct game phase
	 *   1.1 Check if the player has the resource he's offering
	 *    1.1.1 if no then log a player and call reset input from the player on the panel
	 *   1.2 Open trade offer screen on other players
	 */
    public void P2PTradeOffer(GameObject player, int giveBrick, int giveOre, int giveWool, int giveCoin, int giveWheat, int giveCloth, int giveLumber, int givePaper, int giveGold, int wantsBrick, int wantsOre, int wantsWool, int wantsCoin, int wantsWheat, int wantsCloth, int wantsLumber, int wantsPaper, int wantsGold)
    {
        Player tradingPlayer = gamePlayers[player];
        if (checkCorrectPlayer(player) && currentPhase == GamePhase.TurnFirstPhase)
        {
            //TODO: Check if the player has enough resource to trade
            bool enoughResource = true;
            if (!tradingPlayer.HasResources(giveBrick, ResourceKind.Brick))
            {
                enoughResource = false;
            }
            else if (!tradingPlayer.HasResources(giveOre, ResourceKind.Ore))
            {
                enoughResource = false;
            }
            else if (!tradingPlayer.HasResources(giveWool, ResourceKind.Wool))
            {
                enoughResource = false;
            }
            else if (!tradingPlayer.HasCommodities(giveCoin, CommodityKind.Coin))
            {
                enoughResource = false;
            }
            else if (!tradingPlayer.HasResources(giveWheat, ResourceKind.Grain))
            {
                enoughResource = false;
            }
            else if (!tradingPlayer.HasCommodities(giveCloth, CommodityKind.Cloth))
            {
                enoughResource = false;
            }
            else if (!tradingPlayer.HasResources(giveLumber, ResourceKind.Lumber))
            {
                enoughResource = false;
            }
            else if (!tradingPlayer.HasCommodities(givePaper, CommodityKind.Paper))
            {
                enoughResource = false;
            }
            else if (tradingPlayer.gold < giveGold)
            {
                enoughResource = false;
            }
            if (enoughResource == false)
            {
                player.GetComponent<playerControl>().RpcLogP2PTradeDebugText("You do not have enough Resource ", true);
                player.GetComponent<playerControl>().RpcResetP2PTradeInput();
                return;
            }

            player.GetComponent<playerControl>().RpcLogP2PTradeDebugText("Waiting for other players... ", false);

            //TODO: Open trade request panel on other players and log the trading player (Waiting for other players etc)
			foreach (Player p in gamePlayers.Values)
            {
                //TODO: print these offers and takes to other players' panels
				if (p != tradingPlayer) {
					playerObjects [p].GetComponent<playerControl> ().RpcReceiveP2PTradeRequestFrom (player, giveBrick, giveOre, giveWool, giveCoin, giveWheat, giveCloth, giveLumber, givePaper, giveGold, wantsBrick, wantsOre, wantsWool, wantsCoin, wantsWheat, wantsCloth, wantsLumber, wantsPaper, wantsGold, tradingPlayer.name);
				}
            }
        }
        else if (checkCorrectPlayer(player))
        {
            logAPlayer(player, "Please roll dice before performing trade.");
        }
        else
        {
            logAPlayer(player, "Can't trade! It isn't your turn.");
        }
    }

    public void playerAcceptedTrade(GameObject fromPlayer, GameObject toPlayer, int giveBrick, int giveOre, int giveWool, int giveCoin, int giveWheat, int giveCloth, int giveLumber, int givePaper, int giveGold, int wantsBrick, int wantsOre, int wantsWool, int wantsCoin, int wantsWheat, int wantsCloth, int wantsLumber, int wantsPaper, int wantsGold)					  
    {
        Player fPlayer = gamePlayers[fromPlayer];
        Player tPlayer = gamePlayers[toPlayer];
        bool enoughResource = true;
        if (!tPlayer.HasResources(wantsBrick, ResourceKind.Brick))
        {
            enoughResource = false;
        }
        else if (!tPlayer.HasResources(wantsOre, ResourceKind.Ore))
        {
            enoughResource = false;
        }
        else if (!tPlayer.HasResources(wantsWool, ResourceKind.Wool))
        {
            enoughResource = false;
        }
        else if (!tPlayer.HasCommodities(wantsCoin, CommodityKind.Coin))
        {
            enoughResource = false;
        }
        else if (!tPlayer.HasResources(wantsWheat, ResourceKind.Grain))
        {
            enoughResource = false;
        }
        else if (!tPlayer.HasCommodities(wantsCloth, CommodityKind.Cloth))
        {
            enoughResource = false;
        }
        else if (!tPlayer.HasResources(wantsLumber, ResourceKind.Lumber))
        {
            enoughResource = false;
        }
        else if (!tPlayer.HasCommodities(wantsPaper, CommodityKind.Paper))
        {
            enoughResource = false;
        }
        else if (tPlayer.gold < wantsGold)
        {
            enoughResource = false;
        }

        if (enoughResource == false)
        {
            toPlayer.GetComponent<playerControl>().RpcSetP2PTradeOfferPanelActive(false);
            logAPlayer(toPlayer, "Sorry, you do not have enough resource to trade into.");
            return;
        }

        foreach (GameObject p in gamePlayers.Keys)
        {
            p.GetComponent<playerControl>().RpcSetP2PTradeOfferPanelActive(false);
            p.GetComponent<playerControl>().RpcSetP2PTradePanelActive(false);
        }


        gamePlayers[fromPlayer].AddResources(wantsWheat, ResourceKind.Grain);
        gamePlayers[fromPlayer].AddResources(wantsOre, ResourceKind.Ore);
        gamePlayers[fromPlayer].AddResources(wantsLumber, ResourceKind.Lumber);
        gamePlayers[fromPlayer].AddResources(wantsBrick, ResourceKind.Brick);
        gamePlayers[fromPlayer].AddResources(wantsWool, ResourceKind.Wool);
        gamePlayers[fromPlayer].AddCommodities(wantsPaper, CommodityKind.Paper);
        gamePlayers[fromPlayer].AddCommodities(wantsCoin, CommodityKind.Coin);
        gamePlayers[fromPlayer].AddCommodities(wantsCloth, CommodityKind.Cloth);
        gamePlayers[fromPlayer].AddGold(wantsGold);

        gamePlayers[fromPlayer].PayResources(giveWheat, ResourceKind.Grain);
        gamePlayers[fromPlayer].PayResources(giveOre, ResourceKind.Ore);
        gamePlayers[fromPlayer].PayResources(giveLumber, ResourceKind.Lumber);
        gamePlayers[fromPlayer].PayResources(giveBrick, ResourceKind.Brick);
        gamePlayers[fromPlayer].PayResources(giveWool, ResourceKind.Wool);
        gamePlayers[fromPlayer].PayCommoditys(givePaper, CommodityKind.Paper);
        gamePlayers[fromPlayer].PayCommoditys(giveCoin, CommodityKind.Coin);
        gamePlayers[fromPlayer].PayCommoditys(giveCloth, CommodityKind.Cloth);
        gamePlayers[fromPlayer].AddGold(-giveGold);

        gamePlayers[toPlayer].AddResources(giveWheat, ResourceKind.Grain);
        gamePlayers[toPlayer].AddResources(giveOre, ResourceKind.Ore);
        gamePlayers[toPlayer].AddResources(giveLumber, ResourceKind.Lumber);
        gamePlayers[toPlayer].AddResources(giveBrick, ResourceKind.Brick);
        gamePlayers[toPlayer].AddResources(giveWool, ResourceKind.Wool);
        gamePlayers[toPlayer].AddCommodities(givePaper, CommodityKind.Paper);
        gamePlayers[toPlayer].AddCommodities(giveCoin, CommodityKind.Coin);
        gamePlayers[toPlayer].AddCommodities(giveCloth, CommodityKind.Cloth);
        gamePlayers[toPlayer].AddGold(giveGold);

        gamePlayers[toPlayer].PayResources(wantsWheat, ResourceKind.Grain);
        gamePlayers[toPlayer].PayResources(wantsOre, ResourceKind.Ore);
        gamePlayers[toPlayer].PayResources(wantsLumber, ResourceKind.Lumber);
        gamePlayers[toPlayer].PayResources(wantsBrick, ResourceKind.Brick);
        gamePlayers[toPlayer].PayResources(wantsWool, ResourceKind.Wool);
        gamePlayers[toPlayer].PayCommoditys(wantsPaper, CommodityKind.Paper);
        gamePlayers[toPlayer].PayCommoditys(wantsCoin, CommodityKind.Coin);
        gamePlayers[toPlayer].PayCommoditys(wantsCloth, CommodityKind.Cloth);
        gamePlayers[toPlayer].AddGold(-wantsGold);


        updatePlayerResourcesUI(toPlayer);
        updatePlayerResourcesUI(fromPlayer);
    }

    public void NpcTrade(GameObject player, int offer, int wants)
    {
        bool check = false;
        Player tradingPlayer = gamePlayers[player];
        bool hasSpecial = false;
        bool hasGeneric = false;
        bool hasTradingHouse = false;
        string log = "";

        if (checkCorrectPlayer(player) && currentPhase == GamePhase.TurnFirstPhase)
        {
            //check if he has the special kind of harbor for his trade type
            switch (offer)
            {
                case 0:
                    if (tradingPlayer.ownedHarbour.Contains(HarbourKind.Wool))
                    {
                        hasSpecial = true;

                    }
                    if (tradingPlayer.hasMerchant && tradingPlayer.merchantResource == ResourceKind.Wool)
                    {
                        hasSpecial = true;
                    }
                    break;
                case 1:
                    if (tradingPlayer.ownedHarbour.Contains(HarbourKind.Lumber))
                    {
                        hasSpecial = true;

                    }
                    if (tradingPlayer.hasMerchant && tradingPlayer.merchantResource == ResourceKind.Lumber)
                    {
                        hasSpecial = true;
                    }
                    break;
                case 2:
                    if (tradingPlayer.ownedHarbour.Contains(HarbourKind.Ore))
                    {
                        hasSpecial = true;

                    }
                    if (tradingPlayer.hasMerchant && tradingPlayer.merchantResource == ResourceKind.Ore)
                    {
                        hasSpecial = true;
                    }
                    break;
                case 3:
                    if (tradingPlayer.ownedHarbour.Contains(HarbourKind.Brick))
                    {
                        hasSpecial = true;

                    }
                    if (tradingPlayer.hasMerchant && tradingPlayer.merchantResource == ResourceKind.Brick)
                    {
                        hasSpecial = true;
                    }
                    break;
                case 4:
                    if (tradingPlayer.ownedHarbour.Contains(HarbourKind.Grain))
                    {
                        hasSpecial = true;

                    }
                    if (tradingPlayer.hasMerchant && tradingPlayer.merchantResource == ResourceKind.Grain)
                    {
                        hasSpecial = true;
                    }
                    break;
                default:
                    break;

            }
            //merchant fleet makes auto 2:1 trade for the duration of the turn
            if (CardsInPlay.Contains(ProgressCardKind.MerchantFleetCard))
            {
                hasSpecial = true;
            }
            if (!hasSpecial && tradingPlayer.ownedHarbour.Contains(HarbourKind.Generic))
            {
                hasGeneric = true;
            }
            if (tradingPlayer.cityImprovementLevels[CommodityKind.Cloth] >= 3)
            {
                hasTradingHouse = true;
            }
            //offering resrouce wants  a resource
            if (offer < 5 && wants < 5)
            {
                //special payment
                if (hasSpecial && tradingPlayer.HasResources(2, (ResourceKind)offer))
                {
                    tradingPlayer.PayResources(2, (ResourceKind)offer);
                    log += "Has traded 2 ";
                    check = true;
                }
                //generic
                else if (hasGeneric && tradingPlayer.HasResources(3, (ResourceKind)offer))
                {
                    tradingPlayer.PayResources(3, (ResourceKind)offer);
                    log += "Has traded 3 ";
                    check = true;
                }
                //shitty
                else if (tradingPlayer.HasResources(4, (ResourceKind)offer))
                {
                    tradingPlayer.PayResources(4, (ResourceKind)offer);
                    log += "Has traded 4 ";
                    check = true;
                }
                if (check)
                {
                    tradingPlayer.AddResources(1, (ResourceKind)wants);
                    log += ((ResourceKind)offer).ToString() + " for 1 " + ((ResourceKind)wants).ToString();
                }
            }
            //offering resources wants a commodity
            else if (offer < 5 && wants >= 5)
            {
                //special
                if (hasSpecial && tradingPlayer.HasResources(2, (ResourceKind)offer))
                {
                    tradingPlayer.PayResources(2, (ResourceKind)offer);
                    log += "Has traded 2 ";
                    check = true;
                }
                //generic
                else if (hasGeneric && tradingPlayer.HasResources(3, (ResourceKind)offer))
                {
                    tradingPlayer.PayResources(3, (ResourceKind)offer);
                    log += "Has traded 3 ";
                    check = true;
                }
                //shitty
                else if (tradingPlayer.HasResources(4, (ResourceKind)offer))
                {
                    tradingPlayer.PayResources(4, (ResourceKind)offer);
                    log += "Has traded 4 ";
                    check = true;
                }
                if (check)
                {
                    gamePlayers[player].AddCommodities(1, (CommodityKind)wants - 5);
                    log += ((ResourceKind)offer).ToString() + " for 1 " + ((CommodityKind)wants - 5).ToString();
                }
            }
            //offering commodity wants resource
            else if (offer >= 5 && wants < 5)
            {
                if (hasTradingHouse && gamePlayers[player].HasCommodities(2, (CommodityKind)(offer - 5)))
                {
                    gamePlayers[player].PayCommoditys(2, (CommodityKind)(offer - 5));
                    gamePlayers[player].AddResources(1, (ResourceKind)wants);
                    log += "Has traded 2 " + ((CommodityKind)offer - 5).ToString() + " for 1 " + ((ResourceKind)wants).ToString();
                    check = true;
                }
                else if (gamePlayers[player].HasCommodities(4, (CommodityKind)(offer - 5)))
                {

                    gamePlayers[player].PayCommoditys(4, (CommodityKind)(offer - 5));
                    gamePlayers[player].AddResources(1, (ResourceKind)wants);
                    log += "Has traded 4 " + ((CommodityKind)offer - 5).ToString() + " for 1 " + ((ResourceKind)wants).ToString();
                    check = true;
                }
            }
            //offering commodity wants commodity
            else if (offer >= 5 && wants >= 5)
            {
                if (hasTradingHouse && gamePlayers[player].HasCommodities(2, (CommodityKind)(offer - 5)))
                {
                    gamePlayers[player].PayCommoditys(2, (CommodityKind)(offer - 5));
                    gamePlayers[player].AddCommodities(1, (CommodityKind)wants - 5);
                    log += "Has Traded 2 " + ((CommodityKind)offer - 5).ToString() + " for 1 " + ((CommodityKind)wants - 5).ToString();
                    check = true;
                }
                else if (gamePlayers[player].HasCommodities(4, (CommodityKind)(offer - 5)))
                {
                    gamePlayers[player].PayCommoditys(4, (CommodityKind)(offer - 5));
                    gamePlayers[player].AddCommodities(1, (CommodityKind)wants - 5);
                    log += "Has Traded 4 " + ((CommodityKind)offer - 5).ToString() + " for 1 " + ((CommodityKind)wants - 5).ToString();
                    check = true;
                }
            }
            //update his ui
            updatePlayerResourcesUI(player);
            player.GetComponent<playerControl>().RpcCloseTrade(check);
            // log
            chatOnServer(player, log);
        }
        else if (checkCorrectPlayer(player))
        {
            logAPlayer(player, "Please roll dice before performing trade.");
        }
        else
        {
            logAPlayer(player, "Can't trade! It isn't your turn.");
        }

    }

    public void BuyFromBank(GameObject player, int wants, bool currency)
    {
        Player tradingPlayer = gamePlayers[player];
        if (checkCorrectPlayer(player) && currentPhase == GamePhase.TurnFirstPhase)
        {
            if (currency && tradingPlayer.gold>=2)
            {
                tradingPlayer.AddGold(-2);
                tradingPlayer.AddResources(1, (ResourceKind)wants);
                updatePlayerResourcesUI(player);
                player.GetComponent<playerControl>().RpcCloseGoldShop(true);
                // log
                chatOnServer(player, tradingPlayer.name + " bought 1 " + (ResourceKind)wants + " from the bank for 2 gold.");
            }
            else
            {
                tradingPlayer.PayFishTokens(4);
                tradingPlayer.AddResources(1, (ResourceKind)wants);
                updatePlayerResourcesUI(player);
                player.GetComponent<playerControl>().RpcCloseGoldShop(true);
                // log
                chatOnServer(player, tradingPlayer.name + " You bought 1 " + (ResourceKind)wants + " from the bank for 4 fishes.");
            }

        }
        else if (checkCorrectPlayer(player))
        {
            logAPlayer(player, "Please roll dice before performing purchase.");
        }
        else
        {
            logAPlayer(player, "Can't buy! It isn't your turn.");
        }

    }

    //buildSettlement ran on server from playerCOntrol class with authority
    //runs the build settlement on the intersection selected by the player
    public void buildOnIntersection(GameObject player, GameObject intersection)
    {
        Intersection inter = intersection.GetComponent<Intersection>();
        Player currentBuilder = gamePlayers[player];
        bool correctPlayer = checkCorrectPlayer(player);
        bool isOwned = intersection.GetComponent<Intersection>().owned;
        bool canBuild = canBuildConnectedCity(currentBuilder, intersection);
        bool hasSettlements = currentBuilder.HasSettlements();
        bool hasLand = false;

        foreach (TerrainHex tile in intersection.GetComponent<Intersection>().linked)
        {
            if (tile.myTerrain != TerrainKind.Sea)
            {
                hasLand = true;
            }
        }

        if (!correctPlayer)
        {
            logAPlayer(player, "Can't build when it isn't your turn.");
        }
        else if (!hasLand)
        {
            logAPlayer(player, "Can't build in the sea.");
        }
        else if (!canBuild && !isOwned)
        {
            logAPlayer(player, "Not following the distance rule.");
        }
        else if (currentPhase == GamePhase.TurnMerchant)
        {
            logAPlayer(player, "Please place the merchant first");
        }
        if (correctPlayer && hasLand && currentPhase != GamePhase.TurnMerchant)
        {
            //first Phase Spawn settlement
            if (currentPhase == GamePhase.SetupRoundOne && !waitingForRoad && canBuild)
            {
                inter.BuildSettlement(currentBuilder);
                //remove one from the pool
                currentBuilder.RemoveSettlement();
                // Add the victory points
                currentBuilder.AddVictoryPoints(1);
                updatePlayerResourcesUI(player);
                waitingForRoad = true;

                currentBuilder.firstCity = inter;
            }
            //second setup spawns City
            else if (currentPhase == GamePhase.SetupRoundTwo && !waitingForRoad && canBuild)
            {
                inter.BuildCity(currentBuilder);
                // remove one from the pool
                currentBuilder.RemoveCity();
                // Add the victory points
                currentBuilder.AddVictoryPoints(2);
                foreach (TerrainHex hex in intersection.GetComponent<Intersection>().linked)
                {
                    payCitySpawn(currentBuilder, hex);
                }
                updatePlayerResourcesUI(player);
                waitingForRoad = true;

                currentBuilder.secondCity = inter;
            }
            else if (currentPhase == GamePhase.TurnFirstPhase)
            {
                //check if empty spot, follow the distance rules and has resources and has not reached the 4 settlemnet cap
                if (currentBuilder.HasSettlementResources() && !isOwned && canBuild)
                {
                    if (currentBuilder.HasSettlements())
                    {
                        currentBuilder.PaySettlementResources();
                        inter.BuildSettlement(currentBuilder);
                        //remove one from the pool
                        currentBuilder.RemoveSettlement();
                        // Add the victory points
                        currentBuilder.AddVictoryPoints(1);
                        //update his UI to let him know he lost the resources;
                        updatePlayerResourcesUI(player);
                        CheckForVictory();
                    }
                    else
                    {
                        logAPlayer(player, "You've reached the 5 settlement cap, try upgrading a city before attempting to place another settlement");
                    }
                }
                else if (isOwned && inter.positionedUnit.Owner.Equals(currentBuilder))
                {
                    // Check that it actually is a settlement
                    var village = inter.positionedUnit as Village;
                    // check for player if he has resources and has actually not reached the 4 city cap
                    if (village != null && village.myKind == VillageKind.Settlement)
                    {
                        bool medCard = false;
                        if (CardsInPlay.Contains(ProgressCardKind.MedicineCard))
                        {
                            medCard = true;
                        }
                        if (!currentBuilder.HasCityUpgradeResources(medCard))
                        {
                            logAPlayer(player, "Your resources are insufficient for upgrading to a city.");
                        }
                        else if (!currentBuilder.HasCities())
                        {
                            logAPlayer(player, "You've reached the cities cap (4).");
                        }
                        else if (currentBuilder.HasCityUpgradeResources(medCard) && currentBuilder.HasCities())
                        {
                            currentBuilder.payCityResources(medCard);
                            inter.UpgradeSettlement(currentBuilder);
                            currentBuilder.RemoveCity();
                            currentBuilder.AddSettlement();
                            // Add the victory points
                            currentBuilder.AddVictoryPoints(1);
                            CardsInPlay.Remove(ProgressCardKind.MedicineCard);
                            //update his UI to let him know he lost the resources;

                            updatePlayerResourcesUI(player);
                            logAPlayer(player, "You upgraded your settlement into a city!");
                            CheckForVictory();
                        }
                    }
                    else if (village != null && (village.myKind == VillageKind.City || village.myKind == VillageKind.TradeMetropole || village.myKind == VillageKind.PoliticsMetropole || village.myKind == VillageKind.ScienceMetropole))
                    {
                        bool engCard = false;
                        if (CardsInPlay.Contains(ProgressCardKind.EngineerCard)) {
                            engCard = true;
                        }

                        if (!currentBuilder.HasWallResources(engCard))
                        {
                            logAPlayer(player, "Your resources are insufficient for building a city wall.");
                        }
                        else if (!currentBuilder.HasWalls())
                        {
                            logAPlayer(player, "You've reached the city walls cap (3).");
                        }

                        else if (inter.getType() == 3)
                        {
                            logAPlayer(player, "There is already a city wall here.");
                        }

                        else if (currentBuilder.HasWallResources(engCard) && currentBuilder.HasWalls())
                        {
                            currentBuilder.payWallResources(engCard);
                            inter.BuildWall(currentBuilder);

                            if (CardsInPlay.Contains(ProgressCardKind.EngineerCard))
                            {
                                CardsInPlay.Remove(ProgressCardKind.EngineerCard);
                                logAPlayer(player, "You used the Engineer Card");
                            }
                            
                            updatePlayerResourcesUI(player);
                            
                            logAPlayer(player, "You built a city wall!");
                        }
                    }
                    CheckForLongestTradeRoute();
                    updateTurn();
                }
            }
        }
    }

    public void buildKnightOnIntersection(GameObject player, GameObject intersection, bool build, bool upgrade)
    {
        Intersection inter = intersection.GetComponent<Intersection>();
        Player currentBuilder = gamePlayers[player];
        bool correctPlayer = checkCorrectPlayer(player);
        bool isOwned = inter.owned;
        bool canBuild = canBuildKnight(currentBuilder, intersection);
        bool hasKnights = currentBuilder.HasKnights(KnightLevel.Basic);
        bool hasLand = false;

        foreach (TerrainHex tile in intersection.GetComponent<Intersection>().linked)
        {
            if (tile.myTerrain != TerrainKind.Sea)
            {
                hasLand = true;
            }
        }

        if (!correctPlayer)
        {
            logAPlayer(player, "Can't build or Upgrade Knights when it isn't your turn.");
        }
        else if (currentPhase == GamePhase.TurnMerchant)
        {
            logAPlayer(player, "Place merchant before placing knights");
        }
        else if (build)
        {
            if (!hasLand && inter.knight == KnightLevel.None)
            {
                logAPlayer(player, "Can't build a Knight in the sea.");
            }
            else if (!canBuild)
            {
                logAPlayer(player, "You need to be connected to your road structure.");
            }
            else
            {
                //if nothing is built hire a knight
                if (!isOwned)
                {
                    if (currentPhase == GamePhase.TurnFirstPhase)
                    {
                        if (currentBuilder.HasKnightResources())
                        {
                            if (currentBuilder.HasKnights(KnightLevel.Basic))
                            {
                                currentBuilder.PayKnightResources();
                                inter.BuildKnight(currentBuilder);
                                currentBuilder.RemoveKnight(KnightLevel.Basic);
                                //update his UI to let him know he lost the resources;
                                logAPlayer(player, "You built a knight!");
                                updatePlayerResourcesUI(player);
                            }

                            else
                            {
                                logAPlayer(player, "You've reached the 3 basic Knight cap, try upgrading a knight before attempting to hire another knight");
                            }
                        }
                        else
                        {
                            logAPlayer(player, "You need 1 wool and 1 ore to hire a basic knight.");
                        }
                    }
                    else
                    {
                        logAPlayer(player, "You can't hire knights on this phase.");
                    }
                }
                else
                {
                    logAPlayer(player, "This place is already occupied by something else.");
                }
            }

        }
        else if (upgrade) {
			
			if (isOwned && inter.positionedUnit.Owner.Equals(currentBuilder)) 
			{				
				if (currentPhase == GamePhase.TurnFirstPhase) 
				{
					// Check that it actually is a knight
					var knight = inter.positionedUnit as Knight;
					// Upgrading knight

					if (knight != null)
                    {
    
                        if (!currentBuilder.HasKnightResources () && !CardsInPlay.Remove(ProgressCardKind.SmithCard)) 
						{
                            logAPlayer (player, "Your resources are insufficient for upgrading this Knight."); 
						}
                        else if (knight.level == KnightLevel.Mighty)

                        {
							logAPlayer (player, "Can't upgrade further he's already the mightiest.");
						}
                        else if (knight.isPromotedThisTurn())
                        {
                            logAPlayer(player, "You already promoted the knight this turn!");
                        }
                        else if (knight.level == KnightLevel.Basic)
                        {
							if (currentBuilder.HasKnights (KnightLevel.Strong)) {

                                if (CardsInPlay.Contains(ProgressCardKind.SmithCard))
                                {
                                    knight.upgradeKnight();
                                    inter.knight = KnightLevel.Strong;
                                    currentBuilder.AddKnight(KnightLevel.Basic);
                                    currentBuilder.RemoveKnight(KnightLevel.Strong);
                                    CardsInPlay.Remove(ProgressCardKind.SmithCard);
                                    logAPlayer(player, "You upgraded this knight freely because of your Smith Card.");
                                    knight.setPromotedThisTurn(true);
                                }
                                else if (currentBuilder.HasKnightResources())
                                {
                                    currentBuilder.PayKnightResources();
                                    knight.upgradeKnight();
                                    inter.knight = KnightLevel.Strong;
                                    currentBuilder.AddKnight(KnightLevel.Basic);
                                    currentBuilder.RemoveKnight(KnightLevel.Strong);
                                    knight.setPromotedThisTurn(true);
                                    updatePlayerResourcesUI(player);
                                }
                                else
                                {
                                    logAPlayer(player, "Your resources are insufficient for upgrading this Knight.");
                                }			

							}
                            else
                            {
								logAPlayer (player, "Reached the strong cap(3) upgrade a strong knight before placing another.");
							}						 

						}
                        else if (knight.level == KnightLevel.Strong)
                        {
							if (currentBuilder.cityImprovementLevels [CommodityKind.Coin] < 3)
                            {
								logAPlayer (player, "You need a fortress to create mighty knights.");
							}
                            else if (currentBuilder.HasKnights (KnightLevel.Mighty))
                            {

                                if (CardsInPlay.Contains(ProgressCardKind.SmithCard))
                                {
                                    knight.upgradeKnight();
                                    inter.knight = KnightLevel.Mighty;
                                    currentBuilder.AddKnight(KnightLevel.Strong);
                                    currentBuilder.RemoveKnight(KnightLevel.Mighty);
                                    CardsInPlay.Remove(ProgressCardKind.SmithCard);
                                    logAPlayer(player, "You upgraded this knight freely because of your Smith Card.");
                                    knight.setPromotedThisTurn(true);
                                }
                                else if (currentBuilder.HasKnightResources())
                                {
                                    currentBuilder.PayKnightResources();
                                    knight.upgradeKnight();
                                    inter.knight = KnightLevel.Mighty;
                                    currentBuilder.AddKnight(KnightLevel.Strong);
                                    currentBuilder.RemoveKnight(KnightLevel.Mighty);
                                    updatePlayerResourcesUI(player);
                                    knight.setPromotedThisTurn(true);
                                }
                                else
                                {
                                    logAPlayer(player, "Your resources are insufficient for upgrading this Knight.");
                                }                             
							}
                            else
                            {
								logAPlayer (player, "Reached the Mighty cap(3), you can't upgrade strongs anymore.");
							}
					}
				}
				else
				{				 
					logAPlayer(player, "You must select a knight!");
				}
			}
			else
			{
				logAPlayer(player, "You can't upgrade or activate knights in this phase.");
			}
			}
		}
        else
        {
            //check for activation
            if (isOwned && inter.positionedUnit.Owner.Equals(currentBuilder))
            {
                if (currentPhase == GamePhase.TurnFirstPhase)
                {
                    // Check that it actually is a knight
                    var knight = inter.positionedUnit as Knight;

                    if (knight != null && !knight.isKnightActive())
                    {
                        if (!currentBuilder.HasKnightActivatingResources() )
                        {
                            logAPlayer(player, "Your resources are insufficient to activate this Knight.");
                        }
                        else
                        {
                            currentBuilder.PayKnightActivationResources();
                            knight.activateKnight();
                            inter.knightActive = true;
                            updatePlayerResourcesUI(player);
                            knight.setFirstTurn(false);
                            logAPlayer(player, "You have activated this knight.");
                            
                        }
                    }
                    else if (knight != null && knight.isKnightActive())
                    {
                        logAPlayer(player, "You have already activated this knight!");
                    }
                    else
                    {
                        logAPlayer(player, "You must select a knight!");
                    }
                }
                else
                {
                    logAPlayer(player, "You can't upgrade or activate knights in this phase.");
                }

            }


        }
        CheckForLongestTradeRoute();
        updateTurn();
    }

    //buildRoad ran on server from playerCOntrol class with authority
    //runs the build Road on the Edge selected by the player
    public void buildRoad(GameObject player, GameObject edge)
    {
        bool correctPlayer = checkCorrectPlayer(player);
        bool canBuild = canBuildConnectedRoad(gamePlayers[player], edge);
        bool onLand = false;
        bool isOwned = edge.GetComponent<Edges>().owned;
        foreach (TerrainHex tile in edge.GetComponent<Edges>().inBetween)
        {
            if (tile.myTerrain != TerrainKind.Sea)
            {
                onLand = true;
            }
        }
        if (!correctPlayer)
        {
            logAPlayer(player, "It isn't your turn.");
        }
        else if (!onLand)
        {
            logAPlayer(player, "You cant build a road in the sea.");
        }
        else if (isOwned)
        {
            logAPlayer(player, "There's already something built here.");
        }
        else if (!canBuild && currentPhase == GamePhase.TurnFirstPhase)
        {
            logAPlayer(player, "The road you are trying to build isn't connected");
        }
        else if (currentPhase == GamePhase.TurnMerchant)
        {
            logAPlayer(player, "Please finish placing the merchent");
        }

        if (correctPlayer && onLand && !isOwned && canBuild && currentPhase != GamePhase.TurnMerchant)
        {
            //first Phase Spawn settlement
            if (currentPhase == GamePhase.SetupRoundOne && waitingForRoad)
            {
                edge.GetComponent<Edges>().BuildRoad(gamePlayers[player]);
                waitingForRoad = false;

                if (!currentPlayer.MoveNext())
                {
                    currentPlayer = reverseOrder.Values.GetEnumerator();
                    //currentPlayer.Reset();
                    currentPlayer.MoveNext();
                    currentPhase = GamePhase.SetupRoundTwo;
                }
            }
            //second setup spawns City
            else if (currentPhase == GamePhase.SetupRoundTwo && waitingForRoad)
            {
                edge.GetComponent<Edges>().BuildRoad(gamePlayers[player]);
                waitingForRoad = false;

                if (!currentPlayer.MoveNext())
                {
                    //currentPlayer.Reset();
                    currentPlayer = gamePlayers.Values.GetEnumerator();
                    currentPlayer.MoveNext();
                    currentPhase = GamePhase.TurnDiceRolled;

                }
            }
            else if (currentPhase == GamePhase.TurnFirstPhase && CardsInPlay.Contains(ProgressCardKind.RoadBuildingCard))
            {
                edge.GetComponent<Edges>().BuildRoad(gamePlayers[player]);
                CardsInPlay.Remove(ProgressCardKind.RoadBuildingCard);
                logAPlayer(player, "You built a free road because of the Road Building Card.");
            }
            else if (currentPhase == GamePhase.TurnFirstPhase && CardsInPlay.Contains(ProgressCardKind.DiplomatCard))
            {
                edge.GetComponent<Edges>().BuildRoad(gamePlayers[player]);
                CardsInPlay.Remove(ProgressCardKind.DiplomatCard);
                logAPlayer(player, "You have moved your road.");
            }
            else if (currentPhase == GamePhase.TurnFirstPhase && gamePlayers[player].hasFreeRoad)
            {
                edge.GetComponent<Edges>().BuildRoad(gamePlayers[player]);
                gamePlayers[player].hasFreeRoad = false;
                logAPlayer(player, "The workers give you this road because of your fish donation.");
            }
            //during first phase building
            else if (currentPhase == GamePhase.TurnFirstPhase && gamePlayers[player].HasRoadResources())
            {
                gamePlayers[player].PayRoadResources();
                edge.GetComponent<Edges>().BuildRoad(gamePlayers[player]);
            }
            CheckForLongestTradeRoute();
            updatePlayerResourcesUI(player);
            updateTurn();
        }
    }

    //buildShip on edges
    public void buildShip(GameObject player, GameObject edge)
    {
        bool correctPlayer = checkCorrectPlayer(player);
        bool canBuild = canBuildConnectedShip(gamePlayers[player], edge);
        bool onWater = false;
        bool isOwned = edge.GetComponent<Edges>().owned;
        foreach (TerrainHex tile in edge.GetComponent<Edges>().inBetween)
        {
            if (tile.myTerrain == TerrainKind.Sea)
            {
                onWater = true;
            }
        }
        if (!correctPlayer)
        {
            logAPlayer(player, "It isn't your turn.");
        }
        else if (!onWater)
        {
            logAPlayer(player, "You can't build a ship on land.");
        }
        else if (isOwned)
        {
            logAPlayer(player, "There's already something built here.");
        }
        else if (!canBuild && currentPhase == GamePhase.TurnFirstPhase)
        {
            logAPlayer(player, "The ship you are trying to build isn't connected.");
        }
        else if (currentPhase == GamePhase.TurnMerchant)
        {
            logAPlayer(player, "Please finish placing the merchent");
        }

        if (correctPlayer && onWater && !isOwned && canBuild && currentPhase != GamePhase.TurnMerchant)
        {
            //first Phase Spawn settlement
            if (currentPhase == GamePhase.SetupRoundOne && waitingForRoad)
            {
                edge.GetComponent<Edges>().BuildShip(gamePlayers[player]);
                waitingForRoad = false;

                if (!currentPlayer.MoveNext())
                {
                    currentPlayer = reverseOrder.Values.GetEnumerator();
                    //currentPlayer.Reset();
                    currentPlayer.MoveNext();
                    currentPhase = GamePhase.SetupRoundTwo;
                }
            }
            //second setup spawns City
            else if (currentPhase == GamePhase.SetupRoundTwo && waitingForRoad)
            {
                edge.GetComponent<Edges>().BuildShip(gamePlayers[player]);
                waitingForRoad = false;

                if (!currentPlayer.MoveNext())
                {
                    //currentPlayer.Reset();
                    currentPlayer = gamePlayers.Values.GetEnumerator();
                    currentPlayer.MoveNext();
                    currentPhase = GamePhase.TurnDiceRolled;

                }
            }
            //check to see if the road card was built
            else if (currentPhase == GamePhase.TurnFirstPhase && CardsInPlay.Contains(ProgressCardKind.RoadBuildingCard))
            {
                edge.GetComponent<Edges>().BuildShip(gamePlayers[player]);
                CardsInPlay.Remove(ProgressCardKind.RoadBuildingCard);
                logAPlayer(player, "You built a free ship because of the Road Building Card.");
                shipsBuiltThisTurn.Add(edge.GetComponent<Edges>());
            }
            else if (currentPhase == GamePhase.TurnFirstPhase && gamePlayers[player].hasFreeRoad)
            {
                edge.GetComponent<Edges>().BuildShip(gamePlayers[player]);
                gamePlayers[player].hasFreeRoad = false;
                logAPlayer(player, "The workers give you this ship because of your fish donation.");
                shipsBuiltThisTurn.Add(edge.GetComponent<Edges>());
            }
            //during first phase building
            else if (currentPhase == GamePhase.TurnFirstPhase && gamePlayers[player].HasShipResources())
            {
                gamePlayers[player].PayShipResources();
                edge.GetComponent<Edges>().BuildShip(gamePlayers[player]);
                shipsBuiltThisTurn.Add(edge.GetComponent<Edges>());
                //update his UI to let him know he lost the resources;
            }
            CheckForLongestTradeRoute();
            updatePlayerResourcesUI(player);
            updateTurn();
        }
    }
    public void removeShipCheck(GameObject player, GameObject edge)
    {
        bool correctPlayer = checkCorrectPlayer(player);
        if (!correctPlayer)
        {
            logAPlayer(player, "It isn't your turn.");
        }
        else
        {

            //owned check
            Edges temp = edge.GetComponent<Edges>();
            Player temp2 = gamePlayers[player];


            if (temp.isShip == true && !temp.belongsTo.Equals(temp2))
            {
                logAPlayer(player, "This ship does not belong to you!");
            }
            else if (temp.isShip == true && temp.belongsTo.Equals(temp2))
            {
                if (shipsBuiltThisTurn.Contains(temp))
                {
                    logAPlayer(player, "Can't move ships just built!");
                }
                else {
                    // not connected to 2 ships/units check
                    bool connectCheck = false;
                    int count = 0;
                    int count2 = 0;
                    foreach (Intersection i in temp.endPoints)
                    {

                        foreach (Edges e in i.paths)
                        {
                            //check to see if owned or else belongs to is obviously null and return null pointer
                            if (e.owned)
                            {
                                if (e.belongsTo.Equals(temp2))
                                {
                                    if (!connectCheck)
                                    {
                                        count++;
                                        if (count == 2)
                                        {
                                            connectCheck = true;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        count2++;
                                        if (count2 == 2)
                                            break;
                                    }

                                }

                            }
                        }
                        if (count == 2 || count2 == 2)
                        {
                            continue;
                        }

                        // Check to see if ship connected to any of player's units
                        if (temp2.ownedUnits.Contains(i.positionedUnit))
                        {
                            if (!connectCheck)
                            {
                                count = 2;
                                if (count == 2)
                                {
                                    connectCheck = true;
                                }

                            }
                            else
                            {
                                count2 = 2;
                            }
                        }

                    }

                    if (count2 > 1)
                    {
                        logAPlayer(player, "Can't move ships connected on both ends to your other pieces!");
                    }
                    else
                    {
                        //pirate check
                        bool pirateCheck = false;
                        foreach (TerrainHex a in temp.inBetween)
                        {
                            if (a.isPirate == true)
                            {
                                pirateCheck = true;
                            }
                        }
                        if (pirateCheck)
                        {
                            logAPlayer(player, "Can't move ships that are next to pirate!");
                        }
                        else
                        {
                            temp.SelectShipForMoving(temp2);
                            temp2.selectedShip = temp;
                            player.GetComponent<playerControl>().RpcBeginShipMove();
                            logAPlayer(player, "Ship Selected!");
                        }

                    }
                }
            }
            else
            {
                logAPlayer(player, "Please select a ship to move.");
            }
        }

    }

    public void DeselectShip(GameObject player)
    {
        Edges shipToMove = gamePlayers[player].selectedShip;
        if (shipToMove != null)
        {
            shipToMove.DeselectShipForMoving(gamePlayers[player]);
            player.GetComponent<playerControl>().RpcEndShipMove(false);
        }
    }

    public void placeShipCheck(GameObject player, GameObject edge)
    {
        bool correctPlayer = checkCorrectPlayer(player);
        Edges temp = edge.GetComponent<Edges>();

        bool canBuild = canBuildConnectedShip(gamePlayers[player], edge);
        bool onWater = false;
        bool isOwned = temp.owned;

        var temp3 = player.GetComponent<playerControl>();
        Player p = gamePlayers[player];

        //get the ship selected
        Edges shipToMove = p.selectedShip;

        foreach (TerrainHex tile in temp.inBetween)
        {
            if (tile.myTerrain == TerrainKind.Sea)
            {
                onWater = true;
            }
        }

		//pirate check
		bool pirateCheck = false;
		foreach (TerrainHex a in temp.inBetween) {
			if (a.isPirate == true) {
				pirateCheck = true;
				
			}
		}
		if (!correctPlayer) {
			logAPlayer (player, "It isn't your turn.");
            shipToMove.DeselectShipForMoving(p);
            temp3.RpcEndShipMove(false);

        }
        else if (!onWater)
        {
            logAPlayer(player, "You cant build a ship on land.");
            shipToMove.DeselectShipForMoving(p);
            temp3.RpcEndShipMove(false);

        }
        else if (isOwned)
        {
            logAPlayer(player, "There's already something built here.");
            shipToMove.DeselectShipForMoving(p);
            temp3.RpcEndShipMove(false);
            
        } else if (pirateCheck) {
			logAPlayer (player, "Can't move ships next to pirate!");
            shipToMove.DeselectShipForMoving(p);
            temp3.RpcEndShipMove(false);
		
		} else if (correctPlayer && onWater && !isOwned && canBuild) {
			temp.BuildShip (p);
            shipToMove.RemoveShip (p);
            CheckForLongestTradeRoute();
            logAPlayer (player, "Ship Moved! You cannot move anymore ships this turn.");
            temp3.RpcEndShipMove(true);
        }
        else
        {
            logAPlayer(player, "Ship is not connected with one of your roads/ships!");
            shipToMove.DeselectShipForMoving(p);
            temp3.RpcEndShipMove(false);
        }
    }

    public void selectKnightCheck(GameObject player, GameObject inter)
    {
        Intersection temp = inter.GetComponent<Intersection>();
        Player temp2 = gamePlayers[player];
        bool correctPlayer = checkCorrectPlayer(player);
        if (!correctPlayer)
        {
            logAPlayer(player, "It isn't your turn.");

        }

        //Make sure you have selected one of your knights

        else if (temp.knight != KnightLevel.None)
        {
            IntersectionUnit playerKnight = temp.positionedUnit;
            if (temp2.ownedUnits.Contains(playerKnight))
            {

                Knight k = (Knight)temp.positionedUnit;

                //Make sure knight is activated
                if (temp.knightActive == false)
                {
                    logAPlayer(player, "Can't move unactivated knights!");
                }

                //Make sure knight was not activated on the same turn
                else if (!k.isFirstTurn())
                {
                    logAPlayer(player, "Can't move knights that were just activated!");
                }
                else
                {

                    temp.SelectKnight();
                    player.GetComponent<playerControl>().RpcBeginKnightMove();
                    temp2.selectedKnight = temp;
                    logAPlayer(player, "Knight selected!");

                }

            }
            else
            {
                logAPlayer(player, "This knight does not belong to you!");
            }

        }

    }

    public void moveKnightCheck(GameObject player, GameObject inter)
    {
        bool correctPlayer = checkCorrectPlayer(player);
        if (!correctPlayer)
        {
            logAPlayer(player, "It isn't your turn.");
        }
        else
        {
            Intersection temp = inter.GetComponent<Intersection>();
            Player temp3 = gamePlayers[player];

            Intersection knightToMove = temp3.selectedKnight;

            //Check to see if oldinter and the new inter are connected by roads by BFS

            Queue<Intersection> openSet = new Queue<Intersection>();
            HashSet<Intersection> closedSet = new HashSet<Intersection>();
            openSet.Enqueue(knightToMove);

            bool connectCheck = false;
            while (openSet.Count > 0)
            {
                Intersection currentInter = openSet.Dequeue();
                closedSet.Add(currentInter);

                foreach (Edges e in currentInter.paths)
                {
                    if (e.belongsTo == null)
                    {
                        continue;
                    }
                    else if (!e.belongsTo.Equals(temp3))
                    {
                        continue;
                    }
                    foreach (Intersection i in e.endPoints)
                    {
                        if (!i.Equals(currentInter))
                        {
                            if (i.Equals(temp))
                            {
                                connectCheck = true;
                                break;
                            }
                            //Add intersection to open set if it hasn't been explored and hasn't been owned or hasn't been explored and but player owns it
                            else if (!closedSet.Contains(i) && (!i.owned || (i.owned && temp3.ownedUnits.Contains(i.positionedUnit))))
                            {
                                openSet.Enqueue(i);
                            }

                        }
                    }
                }
                if (connectCheck)
                    break;
            }

            if (!connectCheck)
            {
                knightToMove.DeselectKnight();
                player.GetComponent<playerControl>().RpcEndKnightMove();
                logAPlayer(player, "Can't move your knight here! New place must be connected to old place.");
            }

            //Check to see if no cities or higher lvl knights at new intersection


            //If there is a city/settlement at the new place
            else if (temp.owned && temp.knight == KnightLevel.None)
            {
                knightToMove.DeselectKnight();
                player.GetComponent<playerControl>().RpcEndKnightMove();
                logAPlayer(player, "Can't move your knight here! There is already something here.");
            }

            //If there is a knight at the new place
            else if (temp.owned && temp.knight != KnightLevel.None)
            {
                if (!temp.positionedUnit.Owner.Equals(temp3))
                {
                    //Check to see if knight can be displaced

                    Player opponent = temp.positionedUnit.Owner;
                    Knight opKnight = (Knight)temp.positionedUnit;

                    GameObject opponentGameObject = null;

                    foreach (KeyValuePair<GameObject, Player> entry in gamePlayers)
                    {
                        if (entry.Value.Equals(opponent))
                        {
                            opponentGameObject = entry.Key;
                            break;
                        }
                    }

                    if (knightToMove.knight == KnightLevel.Basic)
                    {
                        knightToMove.DeselectKnight();
                        player.GetComponent<playerControl>().RpcEndKnightMove();
                        logAPlayer(player, "Your knight is not strong enough to displace that knight!");

                    }
                    else if (knightToMove.knight == KnightLevel.Strong)
                    {
                        if (temp.knight == KnightLevel.Basic)
                        {
                            Knight temp4 = (Knight)knightToMove.positionedUnit;

                            if (opponentKnightCheck(temp, knightToMove))
                            {
                                logAPlayer(opponentGameObject, "Your knight has been displaced and you must move it!");
                                opponent.storedKnight = opKnight;
                                opponent.storedInter = temp;
                                opponent.hasToMoveKnight = true;

                                temp.RemoveKnight(opponent, false);
                                tempPhase = currentPhase;
                                currentPhase = GamePhase.ForcedKnightMove;
                                ForcedMovePlayer = opponent;
                                updateTurn();

                                opponentGameObject.GetComponent<playerControl>().RpcBeginForcedKnightMove();
                            }
                            else
                            {
       
                                logAPlayer(opponentGameObject, "Your knight has been removed from the board!");
                                temp.RemoveKnight(opponent, true);

                                
                            }

                                             
                            temp.MoveKnight(temp3, temp4, false);
                            knightToMove.RemoveKnight(temp3, false);
                            player.GetComponent<playerControl>().RpcEndKnightMove();
                            logAPlayer(player, "Knight moved!");
                            CheckForLongestTradeRoute();
                            
                        }
                        else
                        {
                            knightToMove.DeselectKnight();
                            player.GetComponent<playerControl>().RpcEndKnightMove();
                            logAPlayer(player, "Your knight is not strong enough to displace that knight!");
                        }
                    }
                    else
                    {
                        if (temp.knight == KnightLevel.Basic || temp.knight == KnightLevel.Strong)
                        {

                            if (opponentKnightCheck(temp, knightToMove))
                            {
                                logAPlayer(opponentGameObject, "Your knight has been displaced and you must move it!");
                                opponent.storedKnight = opKnight;
                                opponent.storedInter = temp;
                                opponent.hasToMoveKnight = true;

                                temp.RemoveKnight(opponent, false);
                                tempPhase = currentPhase;
                                currentPhase = GamePhase.ForcedKnightMove;
                                ForcedMovePlayer = opponent;
                                updateTurn();

                                opponentGameObject.GetComponent<playerControl>().RpcBeginForcedKnightMove();
                            }
                            else
                            {
                                logAPlayer(opponentGameObject, "Your knight has been removed from the board!");
                                temp.RemoveKnight(opponent, true);
                            }

                            Knight temp4 = (Knight)knightToMove.positionedUnit;
                            temp.MoveKnight(temp3, temp4, false);
                            knightToMove.RemoveKnight(temp3, false);
                            player.GetComponent<playerControl>().RpcEndKnightMove();
                            logAPlayer(player, "Knight moved!");
                            CheckForLongestTradeRoute();

                        }
                        else
                        {
                            knightToMove.DeselectKnight();
                            player.GetComponent<playerControl>().RpcEndKnightMove();
                            logAPlayer(player, "Your knight is not strong enough to displace that knight!");
                        }
                    }
                }
                else
                {
                    knightToMove.DeselectKnight();
                    player.GetComponent<playerControl>().RpcEndKnightMove();
                    logAPlayer(player, "You can't displace your own knights!");
                }
            }
            //if there is nothing there 
            else
            {
                Knight temp4 = (Knight)knightToMove.positionedUnit;
                knightToMove.RemoveKnight(temp3, false);
                temp.MoveKnight(temp3, temp4, false);
                logAPlayer(player, "Knight moved!");
                player.GetComponent<playerControl>().RpcEndKnightMove();
                CheckForLongestTradeRoute();
            }
        }
    }

    //Check to see if knight forced to move has anyplace to go
    public bool opponentKnightCheck(Intersection inter, Intersection oldinter)
    {

        Player opponent = inter.positionedUnit.Owner;

        Queue<Intersection> openSet = new Queue<Intersection>();
        HashSet<Intersection> closedSet = new HashSet<Intersection>();
        openSet.Enqueue(inter);

        bool connectCheck = false;
        while (openSet.Count > 0)
        {
            Intersection currentInter = openSet.Dequeue();
            closedSet.Add(currentInter);

            foreach (Edges e in currentInter.paths)
            {
                if (e.belongsTo == null)
                {
                    continue;
                }
                else if (!e.belongsTo.Equals(opponent))
                {
                    continue;
                }
                foreach (Intersection i in e.endPoints)
                {
                    if (!i.Equals(currentInter))
                    {

                        //empty space has been found!
                        if (!i.owned || i.Equals(oldinter))
                        {
                            connectCheck = true;
                            break;
                        }
                        //Add intersection to open set if intersection is occupied by a friendly unit and it hasn't been explored yet
                        else if (!closedSet.Contains(i) && opponent.ownedUnits.Contains(i.positionedUnit))
                        {
                            openSet.Enqueue(i);
                        }
                    }

                }
            }
            if (connectCheck)
                break;
        }
        return connectCheck;
    }

    public void forceMoveKnight(GameObject player, GameObject inter)
    {

        Player p = gamePlayers[player];
        Knight k = p.storedKnight;
        Intersection oldInter = p.storedInter;

        Intersection temp = inter.GetComponent<Intersection>();

        Queue<Intersection> openSet = new Queue<Intersection>();
        HashSet<Intersection> closedSet = new HashSet<Intersection>();
        openSet.Enqueue(oldInter);

        if (oldInter.Equals(temp))
        {
            logAPlayer(player, "Sadly, not a valid place to move your knight.");
        }
        else if (temp.owned)
        {
            logAPlayer(player, "Sadly, not a valid place to move your knight.");
        }   
        else
        {

            bool connectCheck = false;
            while (openSet.Count > 0)
            {
                Intersection currentInter = openSet.Dequeue();
                closedSet.Add(currentInter);

                foreach (Edges e in currentInter.paths)
                {
                    if (e.belongsTo == null)
                    {
                        continue;
                    }
                    else if (!e.belongsTo.Equals(p))
                    {
                        continue;
                    }
                    foreach (Intersection i in e.endPoints)
                    {
                        if (!i.Equals(currentInter))
                        {
                            if (i.Equals(temp))
                            {
                                connectCheck = true;
                                break;
                            }
                            //Add intersection to open set if it hasn't been explored and hasn't been owned or hasn't been explored and but player owns it
                            else if (!closedSet.Contains(i) && (!i.owned || (i.owned && p.ownedUnits.Contains(i.positionedUnit))))
                            {
                                openSet.Enqueue(i);
                            }

                        }
                    }
                }
                if (connectCheck)
                    break;
            }

            if (!connectCheck)
            {
                logAPlayer(player, "Sadly, not a valid place to move your knight.");
            }
            else
            {
                temp.MoveKnight(p, k, true);
                logAPlayer(player, "Knight moved!");
                CheckForLongestTradeRoute();
                currentPhase = tempPhase;
                updateTurn();
                player.GetComponent<playerControl>().RpcEndForcedKnightMove();
            }
        }
    }

    public void scareRobber(GameObject player, GameObject tile)
    {
        if (checkCorrectPlayer(player))
        {
            TerrainHex hex = tile.GetComponent<TerrainHex>();
            if (hex.isRobber || hex.isPirate)
            {
                bool canScare = false;
                foreach(Intersection i in hex.corners)
                {
                    if (i.owned)
                    {
                        if (i.positionedUnit.Owner.Equals(gamePlayers[player]))
                        {
                            var knight = i.positionedUnit as Knight;
                            if (knight != null)
                            {
                                if (knight.isKnightActive())
                                {
                                    canScare = true;
                                }
                            }
                        }
                    
                    }
                }
                if (canScare)
                {
                    readyToScare = hex;
                    logAPlayer(player, "Select an active knight to scare the robber with!");
                    player.GetComponent<playerControl>().RpcStartScare();
                }
            }
        }
    }

    public void useKnightScareRobber(GameObject player, GameObject inter)
    {
        Intersection temp = inter.GetComponent<Intersection>();
        Player p = gamePlayers[player];
        if (temp.owned) { 
            var knight = temp.positionedUnit as Knight;

            if (knight != null)
            {
                if (knight.Owner.Equals(p))
                {
                    if (knight.isKnightActive())
                    {

                        if (readyToScare.isRobber)
                        {
                            knight.deactivateKnight();
                            temp.knightActive = false;
                            logAPlayer(player, "Choose place to move robber!");
                            currentPhase = GamePhase.TurnRobberOnly;
                        }
                        else if (readyToScare.isPirate)
                        {
                            knight.deactivateKnight();
                            temp.knightActive = false;
                            logAPlayer(player, "Choose place to move pirate!");
                            currentPhase = GamePhase.TurnPirateOnly;
                        }
                    }
                    else
                    {
                        logAPlayer(player, "Not an active knight!");
                        player.GetComponent<playerControl>().RpcEndScare();
                    }
                } else
                {
                    logAPlayer(player, "Not your knight!");
                    player.GetComponent<playerControl>().RpcEndScare();
                }
            }
        }

        
        updateTurn();
    }

    //end player turn
    public void endTurn(GameObject player)
    {

		if (checkCorrectPlayer(player) && currentPhase == GamePhase.ForcedKnightMove)
        {
            logAPlayer(player, "Opponent must move his displaced knight first.");
        }
        else if (checkCorrectPlayer(player) && currentPhase == GamePhase.Intrigue)
        {
            logAPlayer(player, "Select a knight to displace first");
        }
        else if (checkCorrectPlayer(player) && currentPhase != GamePhase.TurnRobberPirate && currentPhase != GamePhase.TurnDesertKnight && currentPhase != GamePhase.TurnMerchant)
        {
            if (currentPhase != GamePhase.TurnDiceRolled)
            {
			    player.GetComponent<playerControl> ().RpcCanMoveShipAgain();
                shipsBuiltThisTurn.Clear();
                player.GetComponent<playerControl>().RpcEndScare();

                //Reset all knights' firstturn variables that are false since they were activated this turn
                Player temp = gamePlayers[player];
				foreach (IntersectionUnit k in temp.ownedUnits.Where(u => u is Knight)) {
					Knight knight = (Knight) k;
					knight.setFirstTurn (true);
                    knight.setPromotedThisTurn(false);
				}

                // suppose player ends turn while selecting something...
                if (temp.selectedShip != null)
                    if (temp.selectedShip.belongsTo != null)
                        temp.selectedShip.DeselectShipForMoving(temp);

                if (temp.selectedKnight != null)
                    if (temp.selectedKnight.positionedUnit != null)
                        temp.selectedKnight.DeselectKnight();
                currentPhase = GamePhase.TurnDiceRolled;

                if (!currentPlayer.MoveNext())
                {
                    currentPlayer.Reset();
                    currentPlayer.MoveNext();
                }
                //remove all remaining cards
                CardsInPlay.Clear();
            }

        }
        else if (checkCorrectPlayer(player) && currentPhase == GamePhase.TurnRobberPirate)
        {
            logAPlayer(player, "Move the robber before ending your turn.");
        }
        updateTurn();
    }

    //roll dice and update values and uis
    public void rollDice(GameObject player)
    {
        //check to make sure rolls are done when needed
        if (checkCorrectPlayer(player) && currentPhase == GamePhase.TurnDiceRolled)
        {
            gameDices.rollDice();
            updateRollsUI();
            HandleEventDice(); // Handle the outcome of the event dice
            gamePlayers[player].cardsInHand.Add(ProgressCardKind.DeserterCard);
            if (gameDices.getRed() + gameDices.getYellow() == 7 && firstBarbAttack)
            {
                currentPhase = GamePhase.TurnRobberPirate;
                //SEND ClientRpc to discard correct amount;
                robberDiscarding();

            }
            else
            {
                currentPhase = GamePhase.TurnFirstPhase;
            }
            DistributeResources();
        }
        else
        {
            logAPlayer(player, "You've already rolled this turn");
        }
        updateTurn();
    }

    //allows client to actually move the robber
    public void moveMerchent(GameObject player, GameObject tile)
    {
        Player mover = (Player)gamePlayers[player];
        List<String> names = new List<string>();
        if (currentPhase == GamePhase.TurnMerchant && checkCorrectPlayer(player))
        {
            if (tile.GetComponent<TerrainHex>().hasMerchant == true)
            {
                logAPlayer(player, "You can't reselect the same hextile");
            }
            else
            {
                var ownsCityNextToTile = false;
                foreach (Intersection inter in tile.GetComponent<TerrainHex>().corners)
                {
                    if (inter.owned && inter.positionedUnit is Village && inter.positionedUnit.Owner.Equals(mover))
                    {
                        ownsCityNextToTile = true;
                    }
                }

                ResourceKind selectedResource = ResourceKind.Grain;
                bool validResourceTile = true;
                switch (tile.GetComponent<TerrainHex>().myTerrain)
                {
                    case TerrainKind.Pasture:
                        selectedResource = ResourceKind.Wool;
                        break;
                    case TerrainKind.Forest:
                        selectedResource = ResourceKind.Lumber;
                        break;
                    case TerrainKind.Mountains:
                        selectedResource = ResourceKind.Ore;
                        break;
                    case TerrainKind.Hills:
                        selectedResource = ResourceKind.Brick;
                        break;
                    case TerrainKind.Fields:
                        selectedResource = ResourceKind.Grain;
                        break;
                    default:
                        validResourceTile = false;
                        break;
                }

                if (!ownsCityNextToTile)
                {
                    logAPlayer(player, "You do not own any villages next to this tile");
                }
                else if (!validResourceTile)
                {
                    logAPlayer(player, "You must place the merchant on a basic resource tile");
                }
                else
                {
                    
                    if (merchantTile != null)
                        merchantTile.GetComponent<TerrainHex>().hasMerchant = false;
                    merchantTile = tile;
                    mover.merchantResource = selectedResource;
                    tile.GetComponent<TerrainHex>().hasMerchant = true;
                    player.GetComponent<playerControl>().robberMove = true;
                    currentPhase = GamePhase.TurnFirstPhase;
                    updateTurn();
                }
            }
        }
    }

    //allows client to actually move the robber
    public void moveRobber(GameObject player, GameObject tile)
    {
        bool Bishop = CardsInPlay.Contains(ProgressCardKind.BishopCard);
        Player mover = (Player)gamePlayers[player];
        List<String> names = new List<string>();
        if ((currentPhase == GamePhase.TurnRobberPirate || currentPhase == GamePhase.TurnRobberOnly) && checkCorrectPlayer(player))
        {
            if (tile.GetComponent<TerrainHex>().isRobber == true)
            {
                logAPlayer(player, "You can't reselect the same hextile");
            }
            else
            {
                robberTile.GetComponent<TerrainHex>().isRobber = false;
                foreach(Intersection inter in tile.GetComponent<TerrainHex>().corners)
                {
                    if(inter.owned && inter.positionedUnit is Village && !inter.positionedUnit.Owner.Equals(mover))
                    {
                        if (!names.Contains(inter.positionedUnit.Owner.name))
                        {
                            names.Add(inter.positionedUnit.Owner.name);
                        }                    
                    }
                }
                robberTile = tile;
                tile.GetComponent<TerrainHex>().isRobber = true;
                currentPhase = GamePhase.TurnFirstPhase;
                if (names.Count > 0 && !Bishop)
                {
                    if (stealAll)
                    {
                        foreach (String name in names)
                        {
                            stealPlayer(player, name);
                            stealAll = false;
                        }
                    }
                    else
                    {
                        player.GetComponent<playerControl>().RpcSetupStealInterface(names.ToArray());
                    }
                }
                //Bishop steals from each player
                else if (names.Count > 0 && Bishop)
                {
                    foreach(String s in names)
                    {
                        stealPlayer(player, s);
                    }
                    CardsInPlay.Remove(ProgressCardKind.BishopCard);
                }
                    updateTurn();
            }
        }
    }

    public void resetRobber(GameObject player)
    {
        if (checkCorrectPlayer(player))
        {
            if (currentPhase == GamePhase.TurnFirstPhase)
            {
                if (robberTile.GetComponent<TerrainHex>().isRobber)
                {
                    Player tempPlay = gamePlayers[player];
                    robberTile.GetComponent<TerrainHex>().isRobber = false;
                    tempPlay.PayFishTokens(2);
                    updatePlayerResourcesUI(player);
                }
                else
                {
                    logAPlayer(player, "Robber is already off the board");
                }
            }
            else
            {
                logAPlayer(player, "You must be in the build/trade phase.");
            }

        }
        else
        {
            logAPlayer(player, "Wait your turn.");
        }

    }
    public void movePirate(GameObject player, GameObject tile)
    {
        if ((currentPhase == GamePhase.TurnRobberPirate || currentPhase == GamePhase.TurnPirateOnly) && checkCorrectPlayer(player) && firstBarbAttack)
        {
            if (tile.GetComponent<TerrainHex>().isPirate == true)
            {
                logAPlayer(player, "You can't reselect the same hextile");
            }
            else
            {
                if (pirateTile != null)
                {
                    pirateTile.GetComponent<TerrainHex>().isPirate = false;
                }
                pirateTile = tile;
                tile.GetComponent<TerrainHex>().isPirate = true;
                currentPhase = GamePhase.TurnFirstPhase;
                updateTurn();
            }
        }
    }
    public void resetPirate(GameObject player)
    {

        if (checkCorrectPlayer(player))
        {
            if (currentPhase == GamePhase.TurnFirstPhase)
            {

                if (pirateTile != null)
                {
                    if (pirateTile.GetComponent<TerrainHex>().isPirate)
                    {
                        Player tempPlay = gamePlayers[player];
                        pirateTile.GetComponent<TerrainHex>().isPirate = false;
                        tempPlay.PayFishTokens(2);
                        updatePlayerResourcesUI(player);
                    }            
                }
                else
                {
                    logAPlayer(player, "Pirate is already off the board.");
                }
            }
            else
            {
                logAPlayer(player, "You must be in the build/trade phase.");
            }
        }
        else
        {
            logAPlayer(player, "Wait your turn.");
        }
    }
    public void playCard(GameObject player, ProgressCardKind k)
    {
        bool rightPlayer = checkCorrectPlayer(player);
        Player cardPlayer = gamePlayers[player];
        if (rightPlayer)
        {
            switch (k)
            {

                case ProgressCardKind.AlchemistCard:
                    {
                        if (currentPhase == GamePhase.TurnDiceRolled)
                        {
                            // command to choose dice rolls
                            cardPlayer.cardsInHand.Remove(k);
                            gameDices.returnCard(k);
                        }
                        else
                        {
                            logAPlayer(player, "Can only play this alchemist card before rolling due to it's effect.");
                        }
                        break;
                    }
                //done
                case ProgressCardKind.CraneCard:
                    {
                        cardPlayer.cardsInHand.Remove(k);
						CardsInPlay.Add(k);
                        gameDices.returnCard(k);
						player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        break;
                    }
                //done
                case ProgressCardKind.EngineerCard:
                    {                     
                        cardPlayer.cardsInHand.Remove(k);
                        CardsInPlay.Add(k);
                        gameDices.returnCard(k);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        break;
                    }
                //done
                case ProgressCardKind.InventorCard:
                    {
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
						player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        logAPlayer(player, "Select two number tokens to swap.");
                        player.GetComponent<playerControl>().RpcBeginInventor();
                        break;
                    }
                //done
                case ProgressCardKind.IrrigationCard:
                    {
                        int sum = 0;
                        foreach (GameObject tile in boardTile)
                        {
                            if (tile.GetComponent<TerrainHex>().myTerrain == TerrainKind.Fields)
                            {
                                foreach (Intersection inter in tile.GetComponent<TerrainHex>().corners)
                                {
                                    if (inter.owned && inter.positionedUnit.Owner.Equals(player))
                                    {
                                        cardPlayer.AddResources(2, ResourceKind.Grain);
                                        sum += 2;
                                        break;
                                    }
                                }
                            }

                        }
                        updatePlayerResourcesUI(player);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        logAPlayer(player, "The Irrigation card has given you : " + sum + " grain.");
                        break;
                    }
                //done
                case ProgressCardKind.MedicineCard:
                    {
                        if (cardPlayer.HasCities() && cardPlayer.HasCityUpgradeResources(true) && !CardsInPlay.Contains(ProgressCardKind.MedicineCard))
                        {
                            CardsInPlay.Add(k);
                            player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                            cardPlayer.cardsInHand.Remove(k);
                            gameDices.returnCard(k);
                        }
                        else
                        {
                            logAPlayer(player, "You will waste the card as you can't build due to lack of resources or city cap OR a medicine card is already in play.");
                        }
                        break;
                    }
                //done
                case ProgressCardKind.MiningCard:
                    {
                        int sum = 0;
                        foreach (GameObject tile in boardTile)
                        {
                            if (tile.GetComponent<TerrainHex>().myTerrain == TerrainKind.Mountains)
                            {
                                foreach (Intersection inter in tile.GetComponent<TerrainHex>().corners)
                                {
                                    if (inter.owned && inter.positionedUnit.Owner.Equals(player))
                                    {
                                        cardPlayer.AddResources(2, ResourceKind.Ore);
                                        sum += 2;
                                        break;
                                    }
                                }
                            }

                        }
                        updatePlayerResourcesUI(player);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        logAPlayer(player, "The Mining card has given you : " + sum + " ore.");
                        break;
                    }
                //done
                case ProgressCardKind.PrinterCard:
                    {
                        cardPlayer.AddVictoryPoints(1);
                        broadcastMessage("Player " + cardPlayer.name + " has gained a victory point using the Printer card.");
                        updatePlayerResourcesUI(player);
                        break;
                    }
                //done
                case ProgressCardKind.RoadBuildingCard:
                    {
                        //almost like 2 bools but will be removed when road is built.
                        CardsInPlay.Add(k);
                        CardsInPlay.Add(k);
                        cardPlayer.cardsInHand.Remove(k);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        gameDices.returnCard(k);
                        break;
                    }
                //done
                case ProgressCardKind.SmithCard:
                    {
                        //to bool checks for when promoting a knight
                        CardsInPlay.Add(k);
                        CardsInPlay.Add(k);
                        cardPlayer.cardsInHand.Remove(k);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        gameDices.returnCard(k);
                        break;
                    }
                //done
                case ProgressCardKind.BishopCard:
                    {
                        CardsInPlay.Add(k);
                        currentPhase = GamePhase.TurnRobberOnly;
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        currentPhase = GamePhase.TurnRobberOnly;
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        stealAll = true;
                        updateTurn();
                        break;
                    }
                //done
                case ProgressCardKind.ConstitutionCard:
                    {
                        cardPlayer.AddVictoryPoints(1);
                        broadcastMessage("Player " + cardPlayer.name + " has gained a victory point using the Constitution card.");
                        updatePlayerResourcesUI(player);
                        break;
                    }
                
                //done
                case ProgressCardKind.DeserterCard:
                    {
                        //I have no idea what im doing
                       
                        List<String> names = new List<string>();

                        IEnumerator keys = (gamePlayers.Keys).GetEnumerator();
                        bool remaining = true;
                        //set to first player
                        keys.MoveNext();
                        while (remaining)
                        {
                            GameObject ObjectPlayer = (GameObject)keys.Current;
                            Player p = gamePlayers[ObjectPlayer];
                            foreach (IntersectionUnit i in p.ownedUnits.Where(u => u is Knight))
                            {
                                if (!names.Contains(p.name) && !p.Equals(cardPlayer))
                                {
                                    names.Add(p.name);
                                }
                            }


                            if (!keys.MoveNext())
                            {
                                remaining = false;
                            }
                        }
        
                        if (names.Count > 0)
                        {
                            tempPhase = currentPhase;
                            currentPhase = GamePhase.TurnDesertKnight;
                            cardPlayer.cardsInHand.Remove(k);
                            gameDices.returnCard(k);
                            playedDeserter = cardPlayer;
                            updateTurn();
                            player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                            player.GetComponent<playerControl>().RpcSetupStealInterface(names.ToArray());
                            
                        }
                        else
                        {
                            logAPlayer(player, "No opponents have knights, can't play this card");
                        }

                        break;
                    }
                
                //done
                case ProgressCardKind.DiplomatCard:
                    {
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        CardsInPlay.Add(k);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        logAPlayer(player, "Select a road to move.");
                        player.GetComponent<playerControl>().RpcBeginDiplomat();
                        break;
                    }

                
                //done
                case ProgressCardKind.IntrigueCard:
                    {
                        bool atLeastOneKnight = false;

                        IEnumerator keys = (gamePlayers.Keys).GetEnumerator();
                        bool remaining = true;
                        //set to first player
                        keys.MoveNext();
                        while (remaining)
                        {
                            GameObject ObjectPlayer = (GameObject)keys.Current;
                            Player p = gamePlayers[ObjectPlayer];
                            foreach (IntersectionUnit i in p.ownedUnits.Where(u => u is Knight))
                            {
                                if (!p.Equals(cardPlayer))
                                {
                                    atLeastOneKnight = true;
                                }
                            }


                            if (!keys.MoveNext())
                            {
                                remaining = false;
                            }
                        }

                        if (atLeastOneKnight)
                        {
                            tempPhase = currentPhase;
                            currentPhase = GamePhase.Intrigue;
                            updateTurn();
                            player.GetComponent<playerControl>().RpcStartSelectIntrigue();
                            logAPlayer(player, "Select a knight to intrigue!");

                        }
                        else
                        {
                            logAPlayer(player, "No opponents have knights, can't play this card");
                        }

                        break;
                    }
                //done
                case ProgressCardKind.SaboteurCard:
                    {
                        IEnumerator keys = gamePlayers.Keys.GetEnumerator();
                        while (keys.MoveNext())
                        {
                            Player temp = (Player)keys.Current;
                            if (!temp.Equals(cardPlayer) && temp.victoryPoints >= cardPlayer.victoryPoints)
                            {
                                //send the discard request to all involved players
                                playerObjects[temp].GetComponent<playerControl>().RpcDiscardTime((int)(temp.SumResources() / 2.0),
                                    cardPlayer.name + ": has played the saboteur card and you must discard some cards.");
                            }
                        }
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        break;
                    }                
                case ProgressCardKind.SpyCard:
                    {
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        break;
                    }
                //done
                case ProgressCardKind.WarlordCard:
                    {
                        List<OwnableUnit> units = cardPlayer.ownedUnits;
                        foreach (OwnableUnit unit in units)
                        {
                            if (unit is Knight)
                            {
                                ((Knight)unit).activateKnight();
                                var inter = intersections.FirstOrDefault(i => i.GetComponent<Intersection>().positionedUnit == unit);
                                if (inter != null)
                                {
                                    inter.GetComponent<Intersection>().knightActive = true;
                                }
                            }
                        }
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        break;
                    }
                case ProgressCardKind.WeddingCard:
                    {
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        break;
                    }
                case ProgressCardKind.ComercialHarborCard:
                    {
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        break;
                    }
                case ProgressCardKind.MasterMerchantCard:
                    {
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        break;
                    }
                case ProgressCardKind.MerchantCard:
                    {
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        // See if someone owned the merchant before
                        var merchantPlayer = gamePlayers.Values.FirstOrDefault(p => p.hasMerchant);
                        if (merchantPlayer != null)
                        {
                            merchantPlayer.TakeMerchant();
                            updatePlayerResourcesUI(playerObjects[merchantPlayer]);
                        }
                        cardPlayer.GiveMerchant();
                        updatePlayerResourcesUI(player);
                        CheckForVictory();
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        currentPhase = GamePhase.TurnMerchant;
                        player.GetComponent<playerControl>().robberMove = false;
                        updateTurn();
                        break;
                    }
                case ProgressCardKind.MerchantFleetCard:
                    {
                        CardsInPlay.Add(k);
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(k);
                        break;
                    }
                //done
                case ProgressCardKind.ResourceMonopolyCard:
                    {
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        playerObjects[cardPlayer].GetComponent<playerControl>().RpcResourceMonopoly();
                        break;
                    }
                //done
                case ProgressCardKind.TradeMonopolyCard:
                    {
                        cardPlayer.cardsInHand.Remove(k);
                        gameDices.returnCard(k);
                        playerObjects[cardPlayer].GetComponent<playerControl>().RpcTradeMonopoly();
                        break;
                    }
            }
            CheckForVictory();
        }
        else
        {
            logAPlayer(player, "Can't play cards when it isn't your turn.");
        }
    }

    public void Intrigue(GameObject player, GameObject inter)
    {
        Intersection i = inter.GetComponent<Intersection>();
        Player p = gamePlayers[player];

        if (i.owned)
        {
            IntersectionUnit temp = i.positionedUnit;
            if (!temp.Owner.Equals(p))
            {
                // Check that it actually is a knight
                var byebye = temp as Knight;

                if (byebye != null)
                {
                    //check to see if knight is connected to at least one of your roads
                    bool connectCheck = false;
                    foreach (Edges e in i.paths)
                    {
                        if (e.belongsTo == null)
                        {
                            continue;
                        } else if (e.belongsTo.Equals(p))
                        {
                            connectCheck = true;
                            break;
                        }
                    }
                    if (connectCheck)
                    {
                        player.GetComponent<playerControl>().RpcRemoveProgressCard(ProgressCardKind.IntrigueCard);
                        gamePlayers[player].cardsInHand.Remove(ProgressCardKind.IntrigueCard);
                        gameDices.returnCard(ProgressCardKind.IntrigueCard);
                        player.GetComponent<playerControl>().RpcEndSelectIntrigue();
                        logAPlayer(player, "You have intrigued a knight!");

                        Player opponent = byebye.Owner;

                        if (IntrigueCheck(i))
                        {
                            logAPlayer(playerObjects[opponent], "Because " + p.name + " has used the Intrigue card, your knight has been displaced and you must move it!");
                            opponent.storedKnight = byebye;
                            opponent.storedInter = i;
                            opponent.hasToMoveKnight = true;
                            i.RemoveKnight(opponent, false);

                            currentPhase = GamePhase.ForcedKnightMove;
                            ForcedMovePlayer = opponent;
                            updateTurn();
                            playerObjects[opponent].GetComponent<playerControl>().RpcBeginForcedKnightMove();
                        } else
                        {
                            logAPlayer(playerObjects[opponent], "Because " + p.name + " has used the Intrigue card, your knight has been removed from the board!");
                            i.RemoveKnight(opponent, true);
                            currentPhase = tempPhase;
                            updateTurn();

                        }


                    }
                    else
                    {
                        logAPlayer(player, "Opponent knight must be connected to one of your roads!");
                        player.GetComponent<playerControl>().RpcEndSelectIntrigue();
                        currentPhase = tempPhase;
                        updateTurn();
                    }
                }
                else
                {
                    logAPlayer(player, "Select an opponent knight!");
                    player.GetComponent<playerControl>().RpcEndSelectIntrigue();
                    currentPhase = tempPhase;
                    updateTurn();
                }
            }
            else
            {
                logAPlayer(player, "Select an opponent knight!");
                player.GetComponent<playerControl>().RpcEndSelectIntrigue();
                currentPhase = tempPhase;
                updateTurn();
            }
        } else
        {
            logAPlayer(player, "Select an opponent knight!");
            player.GetComponent<playerControl>().RpcEndSelectIntrigue();
            currentPhase = tempPhase;
            updateTurn();
        }
    }

    bool IntrigueCheck(Intersection inter)
    {
        Player opponent = inter.positionedUnit.Owner;

        Queue<Intersection> openSet = new Queue<Intersection>();
        HashSet<Intersection> closedSet = new HashSet<Intersection>();
        openSet.Enqueue(inter);

        bool connectCheck = false;
        while (openSet.Count > 0)
        {
            Intersection currentInter = openSet.Dequeue();
            closedSet.Add(currentInter);

            foreach (Edges e in currentInter.paths)
            {
                if (e.belongsTo == null)
                {
                    continue;
                }
                else if (!e.belongsTo.Equals(opponent))
                {
                    continue;
                }
                foreach (Intersection i in e.endPoints)
                {
                    if (!i.Equals(currentInter))
                    {

                        //empty space has been found!
                        if (!i.owned)
                        {
                            connectCheck = true;
                            break;
                        }
                        //Add intersection to open set if intersection is occupied by a friendly unit and it hasn't been explored yet
                        else if (!closedSet.Contains(i) && opponent.ownedUnits.Contains(i.positionedUnit))
                        {
                            openSet.Enqueue(i);
                        }
                    }

                }
            }
            if (connectCheck)
                break;
        }
        return connectCheck;
    }

    public void MoveRoad(GameObject player, GameObject edgeGO)
    {
        var edge = edgeGO.GetComponent<Edges>();
        if (!edge.owned || edge.isShip)
        {
            logAPlayer(player, "Select a road to move.");
            return;
        }
        var isOpenPoint = new bool[2] { true, true };
        int j = 0;
        foreach (Intersection i in edge.endPoints)
        {
            if (i.positionedUnit != null)
            {
                isOpenPoint[j] = false;
            }
            foreach (Edges e in i.paths)
            {
                if (e.owned && e != edge)
                {
                    isOpenPoint[j] = false;
                }
            }
            j++;
        }
        var isOpen = isOpenPoint[0] || isOpenPoint[1];
        if (!isOpen)
        {
            logAPlayer(player, "Select an open road to move.");
            return;
        }
        else
        {
            logAPlayer(player, "You have moved a road");
            if (edge.belongsTo != gamePlayers[player])
            {
                CardsInPlay.Remove(ProgressCardKind.DiplomatCard);
            }
            else
            {
                logAPlayer(player, "Please replace your road.");
            }
            edge.RemoveRoad(gamePlayers[player]);
            CheckForLongestTradeRoute();
            updatePlayerResourcesUI(player);
            player.GetComponent<playerControl>().RpcEndDiplomat();
        }
    }
    public void getCardFromDraw(GameObject player,EventKind k)
    {
        ProgressCardKind card = gameDices.rollCard(k);
        player.GetComponent<playerControl>().RpcAddProgressCard(card);
        logAPlayer(player, "You got the " + card + " From drawing or winning the the barbarian win.");
    }

	public void SwapTokens(GameObject player, GameObject[] tiles)
    {
        var invalidNumbers = new int[5] { 1, 2, 6, 8, 12 };
        if (invalidNumbers.Contains(tiles[0].GetComponent<TerrainHex>().numberToken) || invalidNumbers.Contains(tiles[1].GetComponent<TerrainHex>().numberToken))
        {
            logAPlayer(player, "One or more of the tiles you have selected is invalid. Please select another pair.");
            player.GetComponent<playerControl>().RpcEndInventor(false);
            return;
        }
        var temp = tiles[0].GetComponent<TerrainHex>().numberToken;
        tiles[0].GetComponent<TerrainHex>().setTileNumber(tiles[1].GetComponent<TerrainHex>().numberToken);
        tiles[1].GetComponent<TerrainHex>().setTileNumber(temp);
        logAPlayer(player, "Tokens successfully swapped");
        player.GetComponent<playerControl>().RpcEndInventor(true);
    }	
    public void improveCity(GameObject player, int kind)
    {
        bool turnCheck = checkCorrectPlayer(player);
        bool hasCity = false;
        bool hasMetropolis = false;
        bool willBeMetropolis = false;
        var mapper = new Dictionary<CommodityKind, VillageKind>()
                {
                    { CommodityKind.Cloth, VillageKind.TradeMetropole },
                    { CommodityKind.Coin, VillageKind.PoliticsMetropole },
                    { CommodityKind.Paper, VillageKind.ScienceMetropole }
                };
        Player currentUpgrader = gamePlayers[player];
        foreach (OwnableUnit unit in currentUpgrader.ownedUnits)
        {
            if (unit is Village && ((Village)unit).myKind == VillageKind.City)
            {
                hasCity = true;
                break;
            }
            else if (unit is Village && ((Village)unit).myKind == mapper[(CommodityKind)kind])
            {
                hasMetropolis = true;
                break;
            }
        }
        // Check if this will be a metropolis
        if (currentUpgrader.cityImprovementLevels[(CommodityKind)kind] + 1 >= 4)
        {
            GameObject metropolis;
            switch (kind)
            {
                case (int)CommodityKind.Cloth:
                    metropolis = intersections.FirstOrDefault(i => i.GetComponent<Intersection>().positionedUnit is Village && ((Village)i.GetComponent<Intersection>().positionedUnit).myKind == VillageKind.TradeMetropole);
                    break;

                case (int)CommodityKind.Coin:
                    metropolis = intersections.FirstOrDefault(i => i.GetComponent<Intersection>().positionedUnit is Village && ((Village)i.GetComponent<Intersection>().positionedUnit).myKind == VillageKind.PoliticsMetropole);
                    break;

                case (int)CommodityKind.Paper:
                    metropolis = intersections.FirstOrDefault(i => i.GetComponent<Intersection>().positionedUnit is Village && ((Village)i.GetComponent<Intersection>().positionedUnit).myKind == VillageKind.ScienceMetropole);
                    break;

                default:
                    updatePlayerResourcesUI(player);
                    return;
            }
            if (metropolis == null || metropolis.GetComponent<Intersection>().positionedUnit.Owner.cityImprovementLevels[(CommodityKind)kind] < currentUpgrader.cityImprovementLevels[(CommodityKind)kind] + 1)
            {
                willBeMetropolis = true;
            }
        }
                if (!turnCheck)
        {
            logAPlayer(player, "Not your turn!");
        }
        else if (currentPhase != GamePhase.TurnFirstPhase)
        {
            logAPlayer(player, "Can't improve cities on this phase!");
        }
        else if (!hasCity && !hasMetropolis && willBeMetropolis)
        {
            logAPlayer(player, "Can't upgrade without a city. Get a city first!");
        }
        else
        {
            int level = currentUpgrader.GetCityImprovementLevel((CommodityKind)kind);
            int cost = level + 1;
            if (CardsInPlay.Contains(ProgressCardKind.CraneCard))
            {
                cost--;
            }
			if (level == 5)
            {
                logAPlayer(player, "Your improvement level in this category is MAXED!");
            }
            else if (!currentUpgrader.HasCommodities(cost, (CommodityKind)kind))
            {
                logAPlayer(player, "You dont have the Commodities to upgrade you need " + (cost) + " of " + ((CommodityKind)kind).ToString() + ".");

            }
            else
            {
                currentUpgrader.improveCity((CommodityKind)kind, CardsInPlay.Contains(ProgressCardKind.CraneCard));
				if (CardsInPlay.Contains(ProgressCardKind.CraneCard))
					CardsInPlay.Remove(ProgressCardKind.CraneCard);
                logAPlayer(player, "You just improved your cities!");
                player.GetComponent<playerControl>().RpcUpdateSliders(level + 1, kind);
                updatePlayerResourcesUI(player);
                // Check if we are now creating a metropolis
                if (currentUpgrader.cityImprovementLevels[(CommodityKind)kind] >= 4)
                {
                    GameObject metropolis;
                    switch (kind)
                    {
                        case (int)CommodityKind.Cloth:
                            metropolis = intersections.FirstOrDefault(i => i.GetComponent<Intersection>().positionedUnit is Village && ((Village)i.GetComponent<Intersection>().positionedUnit).myKind == VillageKind.TradeMetropole);
                            break;

                        case (int)CommodityKind.Coin:
                            metropolis = intersections.FirstOrDefault(i => i.GetComponent<Intersection>().positionedUnit is Village && ((Village)i.GetComponent<Intersection>().positionedUnit).myKind == VillageKind.PoliticsMetropole);
                            break;

                        case (int)CommodityKind.Paper:
                            metropolis = intersections.FirstOrDefault(i => i.GetComponent<Intersection>().positionedUnit is Village && ((Village)i.GetComponent<Intersection>().positionedUnit).myKind == VillageKind.ScienceMetropole);
                            break;

                        default:
                            updatePlayerResourcesUI(player);
                            return;
                    }
                    if (metropolis == null || metropolis.GetComponent<Intersection>().positionedUnit.Owner.cityImprovementLevels[(CommodityKind)kind] < currentUpgrader.cityImprovementLevels[(CommodityKind)kind])
                    {
                        // You now have the metropolis of this type
                        if (metropolis != null && metropolis.GetComponent<Intersection>().positionedUnit.Owner != currentUpgrader)
                        {
                            metropolis.GetComponent<Intersection>().metropolis = VillageKind.City;
                            ((Village)metropolis.GetComponent<Intersection>().positionedUnit).setVillageType(VillageKind.City);
                            metropolis.GetComponent<Intersection>().positionedUnit.Owner.AddVictoryPoints(-2);
                            updatePlayerResourcesUI(playerObjects[metropolis.GetComponent<Intersection>().positionedUnit.Owner]);
                        }
                        // Call the RPC for the new player to set their metropolis
                        logAPlayer(player, "Select a city to upgrade to a metropolis.");
                        metropolisType = mapper[(CommodityKind)kind];
                        player.GetComponent<playerControl>().RpcBeginMetropoleChoice();
                    }
                }
            }
        }
    }

    public void setMetropole(GameObject player, GameObject intersection)
    {
        var cityUnit = intersection.GetComponent<Intersection>().positionedUnit;
        if (cityUnit != null)
        {
            var city = cityUnit as Village;
            if (city == null)
            {
                logAPlayer(player, "You must select a village.");
                return;
            }
            else if (city.Owner != gamePlayers[player])
            {
                logAPlayer(player, "You must select a city that you own.");
                return;
            }
            else if (city.myKind != VillageKind.City)
            {
                logAPlayer(player, "You must select a city.");
                return;
            }
            city.setVillageType(metropolisType);
            intersection.GetComponent<Intersection>().metropolis = metropolisType;
            gamePlayers[player].AddVictoryPoints(2);
            updatePlayerResourcesUI(player);
            logAPlayer(player, "Your city has become a metropolis!");
            metropolisType = VillageKind.City;
			CheckForVictory();
            player.GetComponent<playerControl>().RpcEndMetropoleChoice();
        }
    }

    //player steals from player with the name name
    public void stealPlayer(GameObject player, string name)
    {
        if (checkCorrectPlayer(player))
        {
            if (currentPhase == GamePhase.TurnFirstPhase)
            {
                IEnumerator values = gamePlayers.Values.GetEnumerator();
                Player receiver = (Player)gamePlayers[player];
                Player victim = null;
                bool stolen = true;
                while (values.MoveNext())
                {
                    Player temp = (Player)values.Current;
                    if (temp.name.Equals(name) && !receiver.name.Equals(name))
                    {
                        victim = temp;
                        break;
                    }
                }
                while (stolen)
                {
                    System.Random luck = new System.Random();
                    int roll = luck.Next(0, 2);
                    if (roll == 0)
                    {
                        int roll2 = luck.Next(0, 5);
                        bool has = victim.HasResources(1, (ResourceKind)roll2);
                        if (has)
                        {
                            victim.PayResources(1, (ResourceKind)roll2);
                            receiver.AddResources(1, (ResourceKind)roll2);
                            receiver.PayFishTokens(3);
                            updatePlayerResourcesUI(playerObjects[victim]);
                            updatePlayerResourcesUI(player);
                            player.GetComponent<playerControl>().RpcEndStealInterface();
                            logAPlayer(player, "you stole 1 " + (ResourceKind)roll2 + " from " + victim.name + ".");
                            logAPlayer(playerObjects[victim], receiver.name + "stole 1 " + (ResourceKind)roll2 + " from you.");
                            break;
                        }
                    }
                    else
                    {
                        int roll2 = luck.Next(0, 3);
                        bool has = victim.HasCommodities(1, (CommodityKind)roll2);
                        if (has)
                        {
                            victim.PayCommoditys(1, (CommodityKind)roll2);
                            receiver.AddCommodities(1, (CommodityKind)roll2);
                            receiver.PayFishTokens(3);
                            updatePlayerResourcesUI(playerObjects[victim]);
                            updatePlayerResourcesUI(player);
                            player.GetComponent<playerControl>().RpcEndStealInterface();
                            logAPlayer(player, "you stole 1 " + (CommodityKind)roll2 + " from " + victim.name + ".");
                            logAPlayer(playerObjects[victim], receiver.name + "stole 1 " + (CommodityKind)roll2 + " from you.");
                            break;
                        }
                    }
                }
            }
            else if (currentPhase == GamePhase.TurnDesertKnight)
            {
                IEnumerator values = gamePlayers.Values.GetEnumerator();
                Player receiver = (Player)gamePlayers[player];
                Player victim = null;
                while (values.MoveNext())
                {
                    Player temp = (Player)values.Current;
                    if (temp.name.Equals(name) && !receiver.name.Equals(name))
                    {
                        victim = temp;
                        break;
                    }
                }
                player.GetComponent<playerControl>().RpcEndStealInterface();
                playerObjects[victim].GetComponent<playerControl>().RpcStartDesertKnight();
                logAPlayer(playerObjects[victim], receiver.name + " played the Deserter card on you! Select a knight to be deserted!");

            }
            else
            {
                logAPlayer(player, "You must be in the build/trade phase.");
            }

        }
        else
        {
            logAPlayer(player, "Wait your turn.");
        }
    }

    public void desertKnight (GameObject player, GameObject inter)
    {
        Intersection i = inter.GetComponent<Intersection>();
        Player loser = gamePlayers[player];

        if (i.owned)
        {
            IntersectionUnit temp = i.positionedUnit;
            if (temp.Owner.Equals(loser))
            {
                // Check that it actually is a knight
                var deserter = temp as Knight;

                if (deserter != null)
                {
                    player.GetComponent<playerControl>().RpcEndDesertKnight();

                    playedDeserter.desertKnightLevel = deserter.level;
                    playedDeserter.desertKnightActive = deserter.isKnightActive();
                    i.RemoveKnight(loser, true);

                    //check to see if player can build knight
                    
                    if (playedDeserter.HasKnights(playedDeserter.desertKnightLevel))
                    {

                        //look everywhere for a place to build a knight
                        Queue<Intersection> openSet = new Queue<Intersection>();
                        HashSet<Intersection> closedSet = new HashSet<Intersection>();
                        openSet.Enqueue(playedDeserter.firstCity);

                        bool connectCheck = false;
                        while (openSet.Count > 0)
                        {
                            Intersection currentInter = openSet.Dequeue();
                            closedSet.Add(currentInter);

                            foreach (Edges e in currentInter.paths)
                            {
                                if (e.belongsTo == null)
                                {
                                    continue;
                                }
                                else if (!e.belongsTo.Equals(playedDeserter))
                                {
                                    continue;
                                }
                                foreach (Intersection a in e.endPoints)
                                {
                                    if (!a.Equals(currentInter))
                                    {
                                        if (a.owned == false)
                                        {
                                            connectCheck = true;
                                            break;
                                        }
                                        //Add intersection to open set if it hasn't been explored and hasn't been owned or hasn't been explored and but player owns it
                                        else if (!closedSet.Contains(a) && (!a.owned || (a.owned && playedDeserter.ownedUnits.Contains(a.positionedUnit))))
                                        {
                                            openSet.Enqueue(a);
                                        }

                                    }
                                }
                            }
                            if (connectCheck)
                                break;
                        }

                        if (!connectCheck)
                        {
                            openSet = new Queue<Intersection>();
                            closedSet = new HashSet<Intersection>();
                            openSet.Enqueue(playedDeserter.secondCity);


                            while (openSet.Count > 0)
                            {
                                Intersection currentInter = openSet.Dequeue();
                                closedSet.Add(currentInter);

                                foreach (Edges e in currentInter.paths)
                                {
                                    if (e.belongsTo == null)
                                    {
                                        continue;
                                    }
                                    else if (!e.belongsTo.Equals(playedDeserter))
                                    {
                                        continue;
                                    }
                                    foreach (Intersection a in e.endPoints)
                                    {
                                        if (!a.Equals(currentInter))
                                        {
                                            if (a.owned == false)
                                            {
                                                connectCheck = true;
                                                break;
                                            }
                                            //Add intersection to open set if it hasn't been explored and hasn't been owned or hasn't been explored and but player owns it
                                            else if (!closedSet.Contains(a) && (!a.owned || (a.owned && playedDeserter.ownedUnits.Contains(a.positionedUnit))))
                                            {
                                                openSet.Enqueue(a);
                                            }

                                        }
                                    }
                                }
                                if (connectCheck)
                                    break;
                            }
                        }

                        if (!connectCheck)
                        {
                            logAPlayer(playerObjects[playedDeserter], "You have nowhere to place a new knight. You got deserted as well!");
                            currentPhase = tempPhase;
                            updateTurn();
                        }
                        else
                        {
                            logAPlayer(playerObjects[playedDeserter], "You have gotten a new knight! Place it somewhere!");
                            playerObjects[playedDeserter].GetComponent<playerControl>().RpcBeginDesertKnightMove();
                        }

                    }
                    else
                    {
                        logAPlayer(playerObjects[playedDeserter], "You can't build any knights of the deserted knights level. You got deserted as well!");
                        currentPhase = tempPhase;
                        updateTurn();
                    }

                }
                    

                }
                 else
                {
                    logAPlayer(player, "You must select one of your own knights");
                }
            }
            else {
                logAPlayer(player, "You must select one of your own knights");
            }
    }

    public void moveDesertKnight(GameObject player, GameObject inter)
    {
        Intersection i = inter.GetComponent<Intersection>();
        Player p = gamePlayers[player];

        if (!i.owned)
        {
            //look everywhere for a place to build a knight
            bool connectCheck = false;
            foreach (Edges e in i.paths)
            {
                    if (e.belongsTo == null)
                    {
                        continue;
                    }
                    else if (e.belongsTo.Equals(playedDeserter))
                    {
                        connectCheck = true;
                    }
    
            }


            if (connectCheck)
            {

                i.BuildDesertKnight(p);
                logAPlayer(player, "Deserted Knight placed!");
                player.GetComponent<playerControl>().RpcEndDesertKnightMove();
                currentPhase = tempPhase;
                updateTurn();

            } else
            {
                logAPlayer(player, "Not a valid place for your knight!");
            }
        }

        else
        {
            logAPlayer(player, "Not a valid place for your knight!");
        }
    }



    public void CardChoice(GameObject player,string cardName)
    {
        ProgressCardKind card = (ProgressCardKind) Enum.Parse(typeof(ProgressCardKind), cardName);
        if((card == ProgressCardKind.PrinterCard || card == ProgressCardKind.ConstitutionCard) && gameDices.hasCardInDeck(card))
        {
            ProgressCardKind myCard = gameDices.getCard(card);
            playCard(player, myCard);
            logAPlayer(player,"You played " + myCard + " Automatically +1 VP.");
            player.GetComponent<playerControl>().RpcEndCardChoiceInterface();
            gamePlayers[player].PayFishTokens(7);
            updatePlayerResourcesUI(player);
        }
        else if (gameDices.hasCardInDeck(card))
        {
            ProgressCardKind myCard = gameDices.getCard(card);
            player.GetComponent<playerControl>().RpcAddProgressCard(myCard);
            player.GetComponent<playerControl>().RpcEndCardChoiceInterface();
            logAPlayer(player, "You got your " + myCard + ".");
            gamePlayers[player].PayFishTokens(7);
            updatePlayerResourcesUI(player);
        }
        else
        {
            initiateCardChoice(player);
        }

    }
    public void MoveBarbs()
    {
        barbPosition = (barbPosition + 1) % 8;
    }
    #endregion

    #region Constraint Checks
    private bool checkCorrectPlayer(GameObject player)
    {
        if (currentPlayer == null)
        {
            currentPlayer = gamePlayers.Values.GetEnumerator();
            currentPlayer.MoveNext();
        }
        bool check = false;
        if ((currentPlayer.Current).Equals(gamePlayers[player]))
        {
            check = true;
        }
        return check;
    }

    private bool canBuildConnectedRoad(Player player, GameObject edge)
    {
        bool check = false;
        bool topInterKnight = false;
        bool bottomInterKnight = false;
        bool topConnected = false;
        bool bottomConnected = false;
        int j = 0;
        //on first setup road must be built connected to settlement built
        if (currentPhase == GamePhase.SetupRoundOne)
        {
            foreach (Intersection i in edge.GetComponent<Edges>().endPoints)
            {
                //check owned true or else owned is null pointer
                if (i.owned && i.positionedUnit.Owner.Equals(player) && ((Village)i.positionedUnit).myKind == VillageKind.Settlement)
                {
                    check = true;
                    break;
                }
            }
        }
        //on second setup road must be connected to city
        else if (currentPhase == GamePhase.SetupRoundTwo)
        {
            foreach (Intersection i in edge.GetComponent<Edges>().endPoints)
            {
                if (i.owned && i.positionedUnit.Owner.Equals(player) && ((Village)i.positionedUnit).myKind == VillageKind.City)
                {
                    check = true;
                    break;
                }
            }
        }
        //on build phase it can be built/connect to any road or city.
        else if (currentPhase == GamePhase.TurnFirstPhase)
        {

            foreach (Intersection i in edge.GetComponent<Edges>().endPoints)
            {
                /*if (j == 0)
                {
                    if (i.owned && i.positionedUnit is Knight && !i.positionedUnit.Owner.Equals(player))
                    {
                        topInterKnight = true;
                    }
                    else
                    {
                        bottomInterKnight = true;
                    }               
                }
                foreach (Edges e in i.paths)
                {
                    //check to see if owned or else bleongs to is obviously null and return null pointer
                    if (e.owned && e.belongsTo.Equals(player) && e.isShip == false)
                    {
                        if (j == 0)
                        {
                            topConnected = true;
                        }
                        else
                        {
                            bottomConnected = true;
                        }
                    }
                }
                j++;*/
                
                //if enemy knight on an intersection, ignore edges from it even if they may be owned by you
                if (i.owned && i.positionedUnit is Knight && !i.positionedUnit.Owner.Equals(player))
                {
                    continue;
                }
                //else if settlement/city on intersection, automatically connected
                else if (i.owned && !(i.positionedUnit is Knight) && i.positionedUnit.Owner.Equals(player))
                {
                    check = true;   
                }
                //Check for other roads owned by you going into intersection;
                else
                {
                    foreach (Edges e in i.paths)
                    {
                        //check to see if owned or else belongs to is obviously null and return null pointer
                        if (e.owned && e.belongsTo.Equals(player) && e.isShip == false)
                        {
                            check = true;

                        }

                    }
                }
            }
        }
        //return (check || (bottomConnected && !bottomInterKnight) || (topConnected && !topInterKnight));
        return check;
    }

    private bool canBuildConnectedShip(Player player, GameObject edge)
    {
        bool check = false;
        //on first setup road/ship must be built connected to settlement built
        if (currentPhase == GamePhase.SetupRoundOne)
        {
            foreach (Intersection i in edge.GetComponent<Edges>().endPoints)
            {
                //check owned true or else owned is null pointer
                if (i.owned && i.positionedUnit.Owner.Equals(player) && ((Village)i.positionedUnit).myKind == VillageKind.Settlement)
                {
                    check = true;
                    break;
                }
            }
        }
        //on second setup road/ship must be connected to city
        else if (currentPhase == GamePhase.SetupRoundTwo)
        {
            foreach (Intersection i in edge.GetComponent<Edges>().endPoints)
            {
                //check owned true or else owned is null pointer
                if (i.owned && i.positionedUnit.Owner.Equals(player) && ((Village)i.positionedUnit).myKind == VillageKind.City)
                {
                    check = true;
                    break;
                }
            }
        }
        //on build phase it has to be connected to a city or another boat.
        else if (currentPhase == GamePhase.TurnFirstPhase)
        {
            /*
            foreach (Intersection i in edge.GetComponent<Edges>().endPoints)
            {
                //check owned true or else owned is null pointer
                if (i.owned && i.positionedUnit.Owner.Equals(player))
                {
                    check = true;
                    break;
                }
                foreach (Edges e in i.paths)
                {
                    //check to see if owned or else bleongs to is obviously null and return null pointer
                    if (e.owned && e.isShip == true)
                    {
                        if (e.belongsTo.Equals(player))
                        {
                            check = true;
                            break;
                        }
                    }
                }
            }
            */

            foreach (Intersection i in edge.GetComponent<Edges>().endPoints)
            {
                
                //if enemy knight on an intersection, ignore edges from it even if they may be owned by you
                if (i.owned && i.positionedUnit is Knight && !i.positionedUnit.Owner.Equals(player))
                {
                    continue;
                }
                //else if settlement/city on intersection, automatically connected
                else if (i.owned && !(i.positionedUnit is Knight) && i.positionedUnit.Owner.Equals(player))
                {
                    check = true;
                }
                //Check for other ships owned by you going into intersection;
                else
                {
                    foreach (Edges e in i.paths)
                    {
                        //check to see if owned or else belongs to is obviously null and return null pointer
                        if (e.owned && e.belongsTo.Equals(player) && e.isShip == true)
                        {
                            check = true;

                        }

                    }
                }
            }



        }
        return check;
    }

    private bool canBuildConnectedCity(Player player, GameObject intersection)
    {
        bool checkProximity = true;
        bool checkRoadConnection = false;
        bool checkIsLand = false;
        foreach (TerrainHex tile in intersection.GetComponent<Intersection>().linked)
        {
            if (tile.myTerrain != TerrainKind.Sea)
            {
                checkIsLand = true;
            }
        }
        foreach (Edges e in intersection.GetComponent<Intersection>().paths)
        {
            //check that a road is on any of the possible edges of this intersection
            if ((currentPhase == GamePhase.TurnFirstPhase) && e.belongsTo != null && e.belongsTo.Equals(player))
            {
                checkRoadConnection = true;
            }
            //automatically can build in setup
            else if (currentPhase == GamePhase.SetupRoundOne || currentPhase == GamePhase.SetupRoundTwo)
            {
                checkRoadConnection = true;
            }
            //check if any close by intersection have already built settlements/cities
            foreach (Intersection i in e.endPoints)
            {

                if (i.owned &&  i.positionedUnit is Village)
                {
                    checkProximity = false;
                    break;
                }
            }
        }
        return (checkProximity && checkRoadConnection && checkIsLand);
    }

    private bool canBuildKnight(Player player, GameObject intersection)
    {
        bool checkRoadConnection = false;
        bool checkIsLand = false;
        foreach (TerrainHex tile in intersection.GetComponent<Intersection>().linked)
        {
            if (tile.myTerrain != TerrainKind.Sea)
            {
                checkIsLand = true;
            }
        }
        foreach (Edges e in intersection.GetComponent<Intersection>().paths)
        {
            //check that a road is on any of the possible edges of this intersection
            if ((currentPhase == GamePhase.TurnFirstPhase) && e.belongsTo != null && e.belongsTo.Equals(player))
            {
                checkRoadConnection = true;
            }
            //automatically can build in setup
            else if (currentPhase == GamePhase.SetupRoundOne || currentPhase == GamePhase.SetupRoundTwo)
            {
                checkRoadConnection = true;
            }
        }
        return (checkRoadConnection && checkIsLand);
    }

    #endregion

    #region Resource Modifiers
    //gives correct resources after a diced is rolled on beggining of turn
    public void DistributeResources()
    {
        List<Player> receivingPlayers = new List<Player>();
        int sum = gameDices.getRed() + gameDices.getYellow();
        System.Random luck = new System.Random();
        if (sum != 7)
        {
            foreach (GameObject tile in boardTile)
            {
                TerrainHex tempTile = tile.GetComponent<TerrainHex>();
                if (tempTile.numberToken == sum && !tempTile.isRobber && !tempTile.isPirate)
                {
                    foreach (Intersection connected in tile.GetComponent<TerrainHex>().corners)
                    {
                        //check if owned and is a village type as knights dont fucking gain resources
                        if (connected.owned == true && connected.positionedUnit.GetType() == typeof(Village))
                        {
                            Player gainer = connected.positionedUnit.Owner;
                            bool gainResources = false;
                            Village hisVillage = (Village)(connected.positionedUnit);
                            switch (tile.GetComponent<TerrainHex>().myTerrain)
                            {
                                case TerrainKind.Pasture:
                                    {
                                        gainer.AddResources(1, ResourceKind.Wool);
                                        if (hisVillage.myKind != VillageKind.Settlement)
                                        {
                                            gainer.AddCommodities(1, CommodityKind.Cloth);
                                        }
                                        gainResources = true;
                                        break;
                                    }

                                case TerrainKind.Forest:
                                    {
                                        gainer.AddResources(1, ResourceKind.Lumber);
                                        if (hisVillage.myKind != VillageKind.Settlement)
                                        {
                                            gainer.AddCommodities(1, CommodityKind.Paper);
                                        }
                                        gainResources = true;
                                        break;
                                    }

                                case TerrainKind.Mountains:
                                    {
                                        gainer.AddResources(1, ResourceKind.Ore);
                                        if (hisVillage.myKind != VillageKind.Settlement)
                                        {
                                            gainer.AddCommodities(1, CommodityKind.Coin);
                                        }
                                        gainResources = true;
                                        break;
                                    }

                                case TerrainKind.Hills:
                                    {
                                        if (hisVillage.myKind == VillageKind.Settlement)
                                        {
                                            gainer.AddResources(1, ResourceKind.Brick);
                                        }                                     
                                        else
                                        {
                                            gainer.AddResources(2, ResourceKind.Brick);
                                        }
                                        gainResources = true;
                                        break;
                                    }

                                case TerrainKind.Fields:
                                    {
                                        if (hisVillage.myKind == VillageKind.Settlement)
                                        {
                                            gainer.AddResources(1, ResourceKind.Grain);
                                        }
                                        else
                                        {
                                            gainer.AddResources(2, ResourceKind.Grain);
                                        }
                                        gainResources = true;
                                        break;
                                    }

                                case TerrainKind.GoldMine:
                                    {
                                        if (hisVillage.myKind == VillageKind.Settlement)
                                        {
                                            gainer.AddGold(1);
                                        }
                                        else
                                        {
                                            gainer.AddGold(2);
                                        }
                                        gainResources = true;
                                        break;
                                    }                             
                                case TerrainKind.Sea:
                                    {
                                        if (hisVillage.myKind == VillageKind.Settlement)
                                        {
                                            giveFishTokens(1, gainer);
                                        }
                                        else
                                        {
                                            giveFishTokens(2, gainer);
                                        }                                          
                                        break;
                                    }
                            }
                            updatePlayerResourcesUI(playerObjects[gainer]);
                            if (gainResources)
                            {
                                receivingPlayers.Add(gainer);
                            }
                            
                        }
                    }

                }
                
            }
            // Now, for players who didn't receive anything, we check for aqueducts
            IEnumerator values = (gamePlayers.Values).GetEnumerator();


            while (!values.MoveNext())
            {
                Player cur = (Player)values.Current;
                //means he isnt receiving during the roll and has an aqueduct green(3+)
                if (!receivingPlayers.Contains(cur) && cur.GetCityImprovementLevel(CommodityKind.Cloth) >= 3)
                {
                    GameObject selector;
                    playerObjects.TryGetValue(cur, out selector);
                    gamePlayers[selector].AddGold(2);
                }
            }
        }
        if (sum == 2 || sum == 3 || sum == 11 || sum == 12)
        {
            foreach (Intersection inter in lakeTile.GetComponent<TerrainHex>().corners)
            {
                if (inter.owned && inter.positionedUnit is Village)
                {
                    if (((Village)inter.positionedUnit).myKind == VillageKind.Settlement)
                    {
                        giveFishTokens(1, inter.positionedUnit.Owner);
                    }
                    else
                    {
                        giveFishTokens(2, inter.positionedUnit.Owner);
                    }
                }
            }
        }
        updatePlayerStatisticsUI();
    }

    public void payCitySpawn(Player paidTo, TerrainHex hex)
    {
        switch (hex.myTerrain)
        {
            case TerrainKind.Pasture:
                {
                    paidTo.AddResources(1, ResourceKind.Wool);
                    paidTo.AddCommodities(1, CommodityKind.Cloth);
                    break;
                }

            case TerrainKind.Forest:
                {
                    paidTo.AddResources(1, ResourceKind.Lumber);
                    paidTo.AddCommodities(1, CommodityKind.Paper);
                    break;
                }

            case TerrainKind.Mountains:
                {
                    paidTo.AddResources(1, ResourceKind.Ore);
                    paidTo.AddCommodities(1, CommodityKind.Coin);

                    break;
                }

            case TerrainKind.Hills:
                {
                    paidTo.AddResources(2, ResourceKind.Brick);
                    break;
                }

            case TerrainKind.Fields:
                {
                    paidTo.AddResources(2, ResourceKind.Grain);
                    break;
                }

            case TerrainKind.GoldMine:
                {
                    paidTo.AddGold(2);
                    break;
                }
            case TerrainKind.Sea:
                {
                    giveFishTokens(2, paidTo);
                    break;
                }
            case TerrainKind.Desert:
                {
                    if (hex.isLake)
                    {
                        giveFishTokens(2, paidTo);                  
                    }
                    break;
                }
        }
    }

    public void giveFishTokens(int tokenAmount, Player player)
    {
        System.Random luck = new System.Random();
        for(int i=0; i<tokenAmount; i++)
        {
            if (!bootDistributed)
            {
                int value = luck.Next(1, 31);
                if (value == 30)
                {
                    player.hasBoot = true;
                    bootDistributed = true;
                }
                else if (value > 21)
                {
                    player.AddFishTokens(3);
                }
                else if (value > 11)
                {
                    player.AddFishTokens(2);
                }
                else
                {
                    player.AddFishTokens(1);
                }
            }
            else
            {
                int value = luck.Next(1, 30);
                if (value > 21)
                {
                    player.AddFishTokens(3);
                }
                else if (value > 11)
                {
                    player.AddFishTokens(2);
                }
                else
                {
                    player.AddFishTokens(1);
                }
            }
        }
    }

    //stupid bypass function cuz of server issues
    public void freeRoad(GameObject player)
    {
        ((Player)gamePlayers[player]).hasFreeRoad = true;
        ((Player)gamePlayers[player]).PayFishTokens(5);
        updatePlayerResourcesUI(player);
    }
    public void discardResources(GameObject player, int[] values)
    {
        Player discardingPlayer = gamePlayers[player];
        bool hasAll = true;
        int i = 0;
        //check if he has enough of what he tries to discard
        while (i < values.Length)
        {
            if (i < 5 && !discardingPlayer.HasResources(values[i], (ResourceKind)i))
            {
                hasAll = false;
                break;
            }
            else if (i >= 5 && !discardingPlayer.HasCommodities(values[i], (CommodityKind)(i - 5)))
            {
                hasAll = false;
                break;
            }
            i++;
        }
        if (hasAll)
        {
            i = 0;
            //make payment loop no need to recheck has constraint was checked earlier
            while (i < values.Length)
            {
                if (i < 5)
                {
                    discardingPlayer.PayResources(values[i], (ResourceKind)i);
                }
                else if (i >= 5)
                {
                    discardingPlayer.PayCommoditys(values[i], (CommodityKind)(i - 5));
                }
                i++;
            }
        }
        else
        {
            string notEnoughOf = "";
            if (i < 5)
            {
                notEnoughOf += "You dont have enough: " + (ResourceKind)i;
            }
            else
            {
                notEnoughOf += "You dont have enough: " + (CommodityKind)(i - 5);
            }

            player.GetComponent<playerControl>().RpcDiscardTime((int)(gamePlayers[player].SumResources() / 2.0), notEnoughOf);

        }
        updatePlayerResourcesUI(player);
    }

    //send a popup to all players that have over 7 resource/commodities cards asking them to discard the correct amount
    public void robberDiscarding()
    {
        IEnumerator values = (gamePlayers.Values).GetEnumerator();
        while (values.MoveNext())
        {
            Player tempPlayer = (Player)values.Current;
            if (tempPlayer.SumResources() > (7 + 2*(3-tempPlayer.availableWalls)))
            {
                int toDiscard = (int)(tempPlayer.SumResources() / 2.0);
                playerObjects[tempPlayer].GetComponent<playerControl>().RpcDiscardTime(toDiscard, "");
            }
        }
    }
    #endregion

    // Methods for handling event rolls
    #region Event Roll

    // Checks the event dice and dispatches the actions accordingly
    private void HandleEventDice()
    {
        Debug.Log(gameDices.getEventKind());
        EventKind roll = gameDices.getEventKind();
        if (roll == EventKind.Barbarian)
        {
            HandleBarbarianRoll();
        }
        else
        {
            HandeCityGateRoll();
        }
    }

    // Handles a barbarian dice roll
    private void HandleBarbarianRoll()
    {
        MoveBarbs();
        if (barbPosition == BARB_ATTACK_POSITION - 1)
        {
            broadcastMessage("The barbarians will attack on the next roll!");
        }
        else if (barbPosition == BARB_ATTACK_POSITION)
        {
            broadcastMessage("Barbarians Rolled. Prepare for the attack!");
            BarbarianAttack();
            firstBarbAttack = true;
            barbPosition = 0;
            // Deactivate all knights
            foreach (GameObject go in intersections)
            {
                var inter = go.GetComponent<Intersection>();
                if (inter.positionedUnit != null && inter.positionedUnit is Knight)
                {
                    var knight = inter.positionedUnit as Knight;
                    knight.deactivateKnight();
                    inter.knightActive = false;
                }
            }
        }
        else
        {
            broadcastMessage("The barbarians have approached... they are now " + (BARB_ATTACK_POSITION - barbPosition) + " rolls away.");
        }   
    }

    // Handle the barbarian attack
    private void BarbarianAttack()
    {
        int playerStrength = getActiveKnightCount();
        int barbStrength = getCityAndMetrCount();
        if (playerStrength > barbStrength)
        {
            broadcastMessage("Players win!");
            defeatBarbarians();
        }
        else
        {
            broadcastMessage("Barbarians win. Prepare for punishment!");
            barbarianPillage();
        }
    }

    private void defeatBarbarians()
    {
        // Find out which player contributed the most
        int mostContributedAmount = 0;
        List<Player> mostContributed = new List<Player>();
        foreach (Player p in gamePlayers.Values)
        {
            mostContributedAmount = Mathf.Max(mostContributedAmount, p.getActiveKnightStrength());
        }
        foreach (Player p in gamePlayers.Values)
        {
            if (p.getActiveKnightStrength() == mostContributedAmount)
                mostContributed.Add(p);
        }
        // If one player, give that player victory point
        if (mostContributed.Count == 1)
        {
            var bestPlayer = mostContributed[0];
            broadcastMessage("Player " + bestPlayer.name + " is the defender of Catan!");
            if (defenders < 6)
            {
                bestPlayer.AddVictoryPoints(1);
                updatePlayerResourcesUI(playerObjects[bestPlayer]);
                CheckForVictory();
            }
            else
            {
                playerObjects[bestPlayer].GetComponent<playerControl>().RpcSetupCardChoiceInterface(gameDices.returnPoliticDeck(), gameDices.returnTradeDeck(), gameDices.returnScienceDeck(),true);
            }
            defenders++;
        }
        else
        {
            foreach (Player p in mostContributed)
            {
                
                broadcastMessage("Player " + p.name + " is a defender of Catan and received a progress card.");
                var pGO = playerObjects[p];
                pGO.GetComponent<playerControl>().RpcSetupCardChoiceInterface(null, null, null, true);

            }
        }
    }

    // TODO: getActiveKnightCount()
    // Handle the barbarian victory and pillaging
    private void barbarianPillage()
    {
        // Find out which players are susceptable to the attack
        List<Player> victims = new List<Player>();
        foreach (Player p in gamePlayers.Values)
        {
            if (p.getCityCount() > 0)
                victims.Add(p);
        }

        // Find out which victim contributed the least
        int leastContributedAmount = int.MaxValue;
        List<Player> leastContributed = new List<Player>();
        foreach (Player p in victims)
        {
            leastContributedAmount = Mathf.Min(leastContributedAmount, p.getActiveKnightStrength());
        }
        foreach (Player p in victims)
        {
            if (p.getActiveKnightStrength() == leastContributedAmount)
                leastContributed.Add(p);
        }

        // Punish the victims
        foreach (Player p in leastContributed)
        {
            broadcastMessage(p.name + " was punished for providing the least active knights. A city has been downgraded.");
            List<Village> cities = p.getCities();
            int ind = rng.Next(cities.Count);
            if (cities[ind].cityWall)
                p.availableWalls++;
            cities[ind].downgradeToSettlement();
            // Update the pools
            p.AddCity();
            p.RemoveSettlement();
            var inter = intersections.FirstOrDefault(i => i.GetComponent<Intersection>().positionedUnit == cities[ind]);
            if (inter != null)
            {
                var i = inter.GetComponent<Intersection>();
                i.DowngradeCity(p);
            }
            updatePlayerResourcesUI(playerObjects[p]);
        }
    }

    // get the total number of active knights
    private int getActiveKnightCount()
    {
        int total = 0;
        foreach (Player p in gamePlayers.Values)
        {
            total += p.getActiveKnightStrength();
        }
        return total;
    }

    // get the total number of cities and metropolises
    private int getCityAndMetrCount()
    {
        int total = 0;
        foreach (Player p in gamePlayers.Values)
        {
            total += p.getCityCount();
            total += p.getMetropolisCount();
        }
        return total;
    }

    // Handles what happens when a city gate is rolled on the event dice
    private void HandeCityGateRoll()
    {
        EventKind gate = gameDices.getEventKind();
        IEnumerator values = gamePlayers.Values.GetEnumerator();
        // TODO: Handle what happens when a city gate is rolled
        switch (gate)
        {
            case EventKind.Politics:
                {
                    while (values.MoveNext())
                    {
                        Player temp = (Player)(values.Current);
                        int hisLevel = temp.GetCityImprovementLevel(CommodityKind.Coin);
                        if (hisLevel != 0 && hisLevel + 1 >= gameDices.getRed())
                        {
                            if (temp.cardsInHand.Count == 4)
                            {
                                logAPlayer(playerObjects[temp], "You weren't awarded your card as your hand is full(4).");
                            }
                            else if (!gameDices.HasCardsInPool(EventKind.Politics))
                            {
                                logAPlayer(playerObjects[temp], "All cards have been drawn, better luck next time.");
                            }
                            else
                            {
                                ProgressCardKind card = gameDices.rollCard(EventKind.Politics);
                                //handle the automatically played constitution card
                                if (card == ProgressCardKind.ConstitutionCard)
                                {
                                    playCard(playerObjects[temp], card);
                                    logAPlayer(playerObjects[temp], "You were awarded 1 VP for drawing the constitution Card. Lucky You!");
                                }
                                else
                                {
                                    //card added to hand for reference
                                    temp.cardsInHand.Add(card);
                                    playerObjects[temp].GetComponent<playerControl>().RpcAddProgressCard(card);
                                    logAPlayer(playerObjects[temp], "You have just drawn " + card.ToString());
                                }

                            }
                        }
                    }
                    break;
                }
            case EventKind.Science:
                {
                    while (values.MoveNext())
                    {
                        Player temp = (Player)(values.Current);
                        int hisLevel = temp.GetCityImprovementLevel(CommodityKind.Paper);
                        if (hisLevel != 0 && hisLevel + 1 >= gameDices.getRed())
                        {
                            if (temp.cardsInHand.Count == 4)
                            {
                                logAPlayer(playerObjects[temp], "You weren't awarded your card as your hand is full(4).");
                            }
                            else if (!gameDices.HasCardsInPool(EventKind.Science))
                            {
                                logAPlayer(playerObjects[temp], "All cards have been drawn, better luck next time.");
                            }
                            else
                            {
                                ProgressCardKind card = gameDices.rollCard(EventKind.Science);
                                //the printer card is handle differently so thats why there's an if condition
                                if (card == ProgressCardKind.PrinterCard)
                                {
                                    playCard(playerObjects[temp], card);
                                    logAPlayer(playerObjects[temp], "You were awarded 1 VP for drawing the Printer Card. Lucky You!");
                                }
                                else
                                {
                                    temp.cardsInHand.Add(card);
                                    playerObjects[temp].GetComponent<playerControl>().RpcAddProgressCard(card);
                                    logAPlayer(playerObjects[temp], "You have just drawn " + card.ToString());
                                }
                            }
                        }
                    }
                    break;
                }
            case EventKind.Trade:
                {
                    while (values.MoveNext())
                    {
                        Player temp = (Player)(values.Current);
                        int hisLevel = temp.GetCityImprovementLevel(CommodityKind.Cloth);
                        if (hisLevel != 0 && hisLevel + 1 >= gameDices.getRed())
                        {
                            if (temp.cardsInHand.Count == 4)
                            {
                                logAPlayer(playerObjects[temp], "You weren't awarded your card as your hand is full(4).");
                            }
                            else if (!gameDices.HasCardsInPool(EventKind.Trade))
                            {
                                logAPlayer(playerObjects[temp], "All cards have been drawn, better luck next time.");
                            }
                            else
                            {
                                ProgressCardKind card = gameDices.rollCard(EventKind.Trade);
                                temp.cardsInHand.Add(card);
                                playerObjects[temp].GetComponent<playerControl>().RpcAddProgressCard(card);
                                logAPlayer(playerObjects[temp], "You have just drawn " + card.ToString());
                            }

                        }
                    }
                    break;
                }
            default:
                {
                    break;
                }
        }
    }
    #endregion

    // Victory point checks
    #region Victory points

    private void CheckForVictory()
    {
        foreach (Player p in gamePlayers.Values)
        {
            if (p.hasBoot && p.victoryPoints >= 14)
            {
                string message = "Player " + p.name + " has won this game! \n Press the Exit button to quit.";
                foreach (GameObject pl in gamePlayers.Keys)
                {
                    pl.GetComponent<playerControl>().RpcVictoryPanel(message);
                }
            }
            else if(p.victoryPoints>= 13)
            {
                string message = "Player " + p.name + " has won this game! \n Press the Exit button to quit.";
                foreach (GameObject pl in gamePlayers.Keys)
                {
                    pl.GetComponent<playerControl>().RpcVictoryPanel(message);
                }
            }
        }
    }

    private void CheckForLongestTradeRoute()
    {
        // Check each player for a potential longest road
        int longestRoadLength;
        var longestRoadPlayer = gamePlayers.Values.FirstOrDefault(p => p.hasLongestTradeRoute);
        if (longestRoadPlayer == null)
        {
            longestRoadLength = 4;
        }
        else
        {
            longestRoadLength = GetPlayerLongestTradeRoute(longestRoadPlayer);
        }
        Player newLongestRoadPlayer = null;
        foreach (Player p in gamePlayers.Values)
        {
            var longestRoadLengthP = GetPlayerLongestTradeRoute(p);
            if (longestRoadLengthP > longestRoadLength)
            {
                longestRoadLength = longestRoadLengthP;
                newLongestRoadPlayer = p;
            }
        }
        if (newLongestRoadPlayer != null)
        {
            if (longestRoadPlayer != null && longestRoadPlayer != newLongestRoadPlayer)
            {
                longestRoadPlayer.TakeLongestRoad();
                updatePlayerResourcesUI(playerObjects[longestRoadPlayer]);
                logAPlayer(playerObjects[longestRoadPlayer], "You have lost the longest trade route...");
                newLongestRoadPlayer.GiveLongestTradeRoute();
                updatePlayerResourcesUI(playerObjects[newLongestRoadPlayer]);
                logAPlayer(playerObjects[newLongestRoadPlayer], "You now have the longest trade route!");
            }
            else if (longestRoadPlayer == null)
            {
                newLongestRoadPlayer.GiveLongestTradeRoute();
                updatePlayerResourcesUI(playerObjects[newLongestRoadPlayer]);
                logAPlayer(playerObjects[newLongestRoadPlayer], "You now have the longest trade route!");
            }
            CheckForVictory();
        }
        else
        {
            // Check if the longest road was broken up
            if (longestRoadPlayer != null && GetPlayerLongestTradeRoute(longestRoadPlayer) < longestRoadLength)
            {
                longestRoadPlayer.TakeLongestRoad();
                updatePlayerResourcesUI(playerObjects[longestRoadPlayer]);
                logAPlayer(playerObjects[longestRoadPlayer], "You have lost the longest trade route...");
            }
        }
    }

    private int GetPlayerLongestTradeRoute(Player p)
    {
        var edgeSets = new List<List<Edges>>();
        // First, we break edges into connected sets
        while (true)
        {
            var edgesToVisit = new Stack<Edges>();
            var visitedEdges = new List<Edges>();
            var temp = edges.FirstOrDefault(e => e.GetComponent<Edges>().belongsTo == p && !edgeSets.Any(l => l.Contains(e.GetComponent<Edges>())));
            if (temp == null)
                break;
            var startingEdge = temp.GetComponent<Edges>();
            foreach (Intersection endpoint in startingEdge.endPoints)
            {
                if (endpoint.positionedUnit == null || endpoint.positionedUnit.Owner == p)
                {
                    foreach (Edges e in endpoint.paths)
                    {
                        if (e.belongsTo == p && ((!startingEdge.isShip && !e.isShip) || (startingEdge.isShip && e.isShip) ||
                                    (endpoint.positionedUnit != null && endpoint.positionedUnit.Owner == p && endpoint.positionedUnit is Village) ||
                                    (endpoint.positionedUnit != null && endpoint.positionedUnit.Owner == p && endpoint.positionedUnit is Village)))
                        {
                            edgesToVisit.Push(e);
                        }
                    }
                }
            }
            visitedEdges.Add(startingEdge);
            while (edgesToVisit.Count > 0)
            {
                var currentEdge = edgesToVisit.Pop();
                if (!visitedEdges.Contains(currentEdge))
                {
                    foreach (Intersection endpoint in currentEdge.endPoints)
                    {
                        if (endpoint.positionedUnit == null || endpoint.positionedUnit.Owner == p)
                        {
                            foreach (Edges e in endpoint.paths)
                            {
                                if (e.belongsTo == p && ((!currentEdge.isShip && !e.isShip) || (currentEdge.isShip && e.isShip) ||
                                    (endpoint.positionedUnit != null && endpoint.positionedUnit.Owner == p && endpoint.positionedUnit is Village) || 
                                    (endpoint.positionedUnit != null && endpoint.positionedUnit.Owner == p && endpoint.positionedUnit is Village)))
                                {
                                    edgesToVisit.Push(e);
                                }
                            }
                        }
                    }
                    visitedEdges.Add(currentEdge);
                }
            }
            edgeSets.Add(visitedEdges);
        }
        // Then we do DFS on each set to find the longest connected set
        var lengthLR = 0;
        foreach (List<Edges> edgeSet in edgeSets)
        {
            var l = ConnectedRoadSegmentLength(edgeSet, p);
            if (lengthLR < l)
            {
                lengthLR = l;
            }
        }
        return lengthLR;
    }

    private int ConnectedRoadSegmentLength(List<Edges> connectedSet, Player p)
    {
        // Find an endpoint
        List<Edges> endpoints = new List<Edges>();
        foreach (Edges temp in connectedSet)
        {
            int connectedEdges = 0;
            foreach (Intersection i in temp.endPoints)
            {
                if (i.positionedUnit == null || i.positionedUnit.Owner == p)
                {
                    foreach (Edges e in i.paths)
                    {
                        if (connectedSet.Contains(e) && e != temp)
                        {
                            connectedEdges++;
                        }
                    }
                }
            }
            if (connectedEdges == 1)
            {
                endpoints.Add(temp);
                break;
            }
        }
        if (endpoints.Count == 0)
        {
            endpoints = connectedSet;
        }
        int m = 0;
        foreach (Edges endpoint in endpoints)
        {
            int maxLength = 0;
            // Start the DFS
            var visitedEdges = new List<Edges>();
            var visitedInts = new List<Intersection>();
            var edgesToVisit = new Stack<EdgeDFSNode>();
            var root = new EdgeDFSNode(endpoint, 0, null);
            edgesToVisit.Push(root);
            while (edgesToVisit.Count > 0)
            {
                var currentEdge = edgesToVisit.Pop();
                if (maxLength < currentEdge.depth)
                {
                    maxLength = currentEdge.depth;
                }
                visitedInts.Add(currentEdge.from);
                if (!visitedEdges.Contains(currentEdge.edge))
                {
                    foreach (Intersection i in currentEdge.edge.endPoints)
                    {
                        if (!visitedInts.Contains(i))
                        {
                            if (i.positionedUnit == null || i.positionedUnit.Owner == p)
                            {
                                foreach (Edges e in i.paths)
                                {
                                    if (connectedSet.Contains(e))
                                    {
                                        edgesToVisit.Push(new EdgeDFSNode(e, currentEdge.depth + 1, i));
                                    }
                                }
                            }
                        }
                    }
                    visitedEdges.Add(currentEdge.edge);
                }
            }
            if (m < maxLength)
            {
                m = maxLength;
            }
        }
        Debug.Log(m);
        return m;
    }


    private class EdgeDFSNode
    {
        public Edges edge { get; private set; }
        public int depth { get; private set; }
        public Intersection from { get; private set; }
        public EdgeDFSNode(Edges e, int d, Intersection i)
        {
            this.edge = e;
            this.depth = d;
            this.from = i;
        }
    }

    #endregion

    #region Save/Load
    public void SaveGameData(playerControl client)
    {
        if (currentPhase == GamePhase.SetupRoundOne || currentPhase == GamePhase.SetupRoundTwo) return;
        var toSave = new GameData(this);
        MemoryStream stream = new MemoryStream();
        BinaryFormatter binaryFormatter = new BinaryFormatter();
        binaryFormatter.Serialize(stream, toSave);
        FileHelper.SendGameData(stream.GetBuffer(), client);
    }

    public void Load(GameData game)
    {
        LoadFromDataFile(game);
    }

    private void LoadFromDataFile(GameData data)
    {
        this.waitingForRoad = data.waitingForRoad;
        this.firstBarbAttack = data.firstBarbAttack;
        this.barbPosition = data.barbPosition;
        this.currentPhase = data.currentPhase;
        this.CardsInPlay = data.CardsInPlay;
        this.currentPlayerString = data.currentPlayer;
        this.bootDistributed = data.bootDistributed;
        this.stealAll = data.stealAll;
        this.tempPlayersByName = new Dictionary<string, Player>();
        this.robberTile = string.IsNullOrEmpty(data.robberTile) ? null : GameObject.Find(data.robberTile);
        this.pirateTile = string.IsNullOrEmpty(data.pirateTile) ? null : GameObject.Find(data.pirateTile);
        this.merchantTile = string.IsNullOrEmpty(data.merchantTile) ? null : GameObject.Find(data.merchantTile);
        var tempOwnedUnitsByPlayer = new Dictionary<IntersectionUnit, Player>();
        foreach (PlayerData pd in data.gamePlayers)
        {
            var player = Player.Load(pd);
            tempPlayersByName.Add(pd.name, player);
            foreach (IntersectionUnit ou in player.ownedUnits)
            {
                tempOwnedUnitsByPlayer.Add(ou, player);
            }

        }

        for (int i = 0; i < data.boardTile.Length; i++)
        {
            var tile = boardTile.First(t => t.name == data.boardTile[i].name);
            if (tile != null)
                tile.GetComponent<TerrainHex>().Load(data.boardTile[i]);
        }
        for (int j = 0; j < data.edges.Length; j++)
        {
            var edge = edges.First(e => e.name == data.edges[j].name);
            if (edge != null)
            {
                if (!string.IsNullOrEmpty(data.edges[j].belongsTo))
                    edge.GetComponent<Edges>().Load(data.edges[j], tempPlayersByName[data.edges[j].belongsTo]);
                else
                    edge.GetComponent<Edges>().Load(data.edges[j], null);
            }

        }
        for (int k = 0; k < data.intersections.Length; k++)
        {
            var inter = intersections.First(i => i.name == data.intersections[k].name);
            if (inter != null)
            {
                var positionedUnit = tempOwnedUnitsByPlayer.Keys.FirstOrDefault(u => u.id == data.intersections[k].positionedUnit);
                inter.GetComponent<Intersection>().Load(data.intersections[k], positionedUnit);
            }
        }

        // Ordering issue: assign the robber tile here
        if (robberTile != null) robberTile.GetComponent<TerrainHex>().isRobber = true;
        if (pirateTile != null) pirateTile.GetComponent<TerrainHex>().isPirate = true;
    }

    #endregion
}

[Serializable]
public class GameData
{
    public bool waitingForRoad { get; set; }
    public bool firstBarbAttack { get; set; }

    public bool bootDistributed { get; set;}
    public bool stealAll { get; set; }
    public int barbPosition { get; set; }
    public int defenders { get; private set; }
    public GamePhase currentPhase { get; set; }
    public List<PlayerData> gamePlayers { get; set; }
    public string currentPlayer;
    public TerrainHexData[] boardTile { get; set; }
    public EdgeData[] edges { get; set; }
    public IntersectionData[] intersections { get; set; }
    public List<ProgressCardKind> CardsInPlay { get; set; }
    public string robberTile { get; set; }
    public string pirateTile { get; set; }
    public string merchantTile { get; set; }

    public GameData(Game source)
    {
        this.waitingForRoad = source.waitingForRoad;
        this.firstBarbAttack = source.firstBarbAttack;
        this.barbPosition = source.barbPosition;
        this.defenders = source.defenders;
        this.currentPhase = source.currentPhase;
        this.currentPlayer = source.currentPlayer == null ? 
            null : 
            ((Player)source.currentPlayer.Current).name;
        this.CardsInPlay = source.CardsInPlay;
        this.bootDistributed = source.bootDistributed;
        this.stealAll = source.stealAll;
        this.gamePlayers = source.gamePlayers.Values.Select(p => new PlayerData(p)).ToList();
        this.boardTile = source.boardTile.Select(t => new TerrainHexData(t.GetComponent<TerrainHex>())).ToArray();
        this.edges = source.edges.Select(t => new EdgeData(t.GetComponent<Edges>())).ToArray();
        this.intersections = source.intersections.Select(t => new IntersectionData(t.GetComponent<Intersection>())).ToArray();
        this.robberTile = source.robberTile == null ? string.Empty : source.robberTile.name;
        this.pirateTile = source.pirateTile == null ? string.Empty : source.pirateTile.name;
        this.merchantTile = source.merchantTile == null ? string.Empty : source.merchantTile.name;
    }
    // Dummy constructor for Unet
    public GameData()
    {

    }
}