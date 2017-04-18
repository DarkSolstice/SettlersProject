using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class playerControl : NetworkBehaviour {

    private bool resourcesShown = true;
    private bool rollsShown = true;
    private bool cardsShown = true;
    public bool buildShip = false;
    public bool moveShip = false;
    public bool shipSelected = false;
    public bool movedShipThisTurn = false;
    public Color oldEdgeColor;

    public bool buyWithGold = true;
    public bool TradeCard = false;
    public bool BarbarianDraw = false;


    public bool interactKnight = false;
    public bool activateKnight = false;
    public bool upgradeKnight = false;
    public bool moveKnight = false;
    public bool buildKnight = false;
    private bool selectForScare = false;

    private bool forceMoveKnight = false;
    public bool knightSelected = false;
    public Color oldKnightColor;
    public GameObject selectedInter;
    public bool desertSelectKnight = false;
    public bool desertMoveKnight = false;
    public bool intrigue = false;

    private bool pickMetropolis = false;
    private bool playInventor = false;
    private bool playDiplomat = false;
    private GameObject[] tilesToSwap = null;
    private GameObject gameState;
    private bool isSeletionOpen = false;
    public GameObject resourcesWindow, ChatWindow, MenuWindow, MaritimeWindow,
                      MapSelector, DiceWindow, SelectionWindow, nameWindow, CardPanel,
                      discardPanel, improvementPanel, inGameMenuPanel, goldShopPanel,
                      victoryPanel, fishPanel, stealPanel, cardChoicePanel;
    public GameObject cardPrefab;
    private List<byte> saveGameData = null;

    // @author xingwei
    // P2P Trade Resources
    /* * Brick, Ore, Wool, Coin, Wheat, Cloth, Lumber, Paper, Gold */
    private int giveBrick = 0;
    private int giveOre = 0;
    private int giveWool = 0;
    private int giveCoin = 0;
    private int giveWheat = 0;
    private int giveCloth = 0;
    private int giveLumber = 0;
    private int givePaper = 0;
    private int giveGold = 0;
    private int wantsBrick = 0;
    private int wantsOre = 0;
    private int wantsWool = 0;
    private int wantsCoin = 0;
    private int wantsWheat = 0;
    private int wantsCloth = 0;
    private int wantsLumber = 0;
    private int wantsPaper = 0;
    private int wantsGold = 0;

    public GameObject P2PTradePanel, P2PTrade_PlayerWants, P2PTrade_PlayerGives, P2PTradeOfferPanel;
    public Text P2PTrade_DebugText, P2PTradeOfferedDescriptionText, P2PTradeGivingDescriptionText, P2PTradeOfferFromText;

    private GameObject tradingPlayer;

    #region SyncVar
    //resource panel values
    [SyncVar(hook = "OnChangedBrick")]
    string Brick;
    [SyncVar(hook = "OnChangedOre")]
    string Ore;
    [SyncVar(hook = "OnChangedWool")]
    string Wool;
    [SyncVar(hook = "OnChangedCoin")]
    string Coin;
    [SyncVar(hook = "OnChangedGrain")]
    string Grain;
    [SyncVar(hook = "OnChangedCloth")]
    string Cloth;
    [SyncVar(hook = "OnChangedLumber")]
    string Lumber;
    [SyncVar(hook = "OnChangedPaper")]
    string Paper;
    [SyncVar(hook = "OnChangedGold")]
    string Gold;
    [SyncVar(hook = "OnChangedVictory")]
    string VictoryPoints;
    [SyncVar(hook = "OnChangeFish")]
    int fishTokens;

    //dice panel Values
    [SyncVar(hook = "OnChangedRed")]
    string Red;
    [SyncVar(hook = "OnChangedYellow")]
    string Yellow;
    [SyncVar(hook = "OnChangedEvent")]
    string Event;

    // valid name
    [SyncVar(hook = "OnNameValidated")]
    public bool isValidName;

    [SyncVar(hook = "OnRobberMoveChange")]
    public bool robberMove;
    #endregion

    #region Setup
    void Start()
    {
        if (SceneManager.GetSceneByName("In-Game") != SceneManager.GetActiveScene()) return;
        if (!isLocalPlayer) return;
        nameWindow.gameObject.SetActive(true);

    }
    void getGameStateOnServer()
    {
        if (!isServer) return;
        gameState = GameObject.Find("GameState");

    }

    public override void OnStartLocalPlayer()
    {
        CmdStartUp();
    }
    #endregion
    void Update()
    {
        if (!isLocalPlayer) return;
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Pressed left click, casting ray.");
            detectClickedObject();
        }
        if (Input.GetButtonDown("Submit"))
        {
            if (nameWindow.activeInHierarchy)
            {
                getNameToSend();
            }
            else
            {
                string message = ChatWindow.transform.GetChild(1).GetChild(2).GetComponent<Text>().text;
                if (!message.Equals("") && message != null)
                {
                    ChatWindow.transform.GetChild(1).GetComponent<InputField>().text = "";
                    CmdSendMessage(gameObject, message);
                }

            }
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            inGameMenuPanel.SetActive(!inGameMenuPanel.activeInHierarchy);
            CmdGetGameData();
        }
    }

    #region UI Related
    public void switchResourcesView()
    {
        Animation resourcesAnimation = transform.GetChild(0).GetComponent<Animation>();
        if (resourcesShown)
        {
            resourcesAnimation.Play("HideResources");
        }
        else
        {
            resourcesAnimation.Play("ShowResources");
        }
        resourcesShown = !resourcesShown;
    }

    public void switchRollsView()
    {
        Animation rollsAnimation = DiceWindow.transform.GetComponent<Animation>();
        if (rollsShown)
        {
            rollsAnimation.Play("HideRolls");
        }
        else
        {
            rollsAnimation.Play("ShowRolls");
        }
        rollsShown = !rollsShown;
    }

    public void switchCardPanel()
    {
        Animation cardsAnimation = CardPanel.transform.GetComponent<Animation>();
        if (cardsShown)
        {
            cardsAnimation.Play("HideCards");
            CardPanel.transform.GetChild(2).GetChild(0).GetComponent<Text>().text = "Maximize";
        }
        else
        {
            cardsAnimation.Play("ShowCards");
            CardPanel.transform.GetChild(2).GetChild(0).GetComponent<Text>().text = "Minimize";
        }
        cardsShown = !cardsShown;
    }

    public void closeSelectView()
    {
        isSeletionOpen = false;
    }

    public void switchFishText()
    {
        fishPanel.transform.GetChild(2).GetComponent<Text>().text = "You have: " + fishTokens + " remaining";
    }
    public void setTextValues(Dictionary<ResourceKind, int> resources, Dictionary<CommodityKind, int> commodities, int gold, int victoryPoints, int fishTokens)
    {
        if (!isServer) return;
        int temp;
        resources.TryGetValue(ResourceKind.Brick, out temp);
        Brick = temp.ToString();
        resources.TryGetValue(ResourceKind.Ore, out temp);
        Ore = temp.ToString();
        resources.TryGetValue(ResourceKind.Grain, out temp);
        Grain = temp.ToString();
        resources.TryGetValue(ResourceKind.Lumber, out temp);
        Lumber = temp.ToString();
        resources.TryGetValue(ResourceKind.Wool, out temp);
        Wool = temp.ToString();

        commodities.TryGetValue(CommodityKind.Cloth, out temp);
        Cloth = temp.ToString();
        commodities.TryGetValue(CommodityKind.Coin, out temp);
        Coin = temp.ToString();
        commodities.TryGetValue(CommodityKind.Paper, out temp);
        Paper = temp.ToString();

        Gold = gold.ToString();
        VictoryPoints = victoryPoints.ToString();

        this.fishTokens = fishTokens;

    }
    public void setDiceValues(int red, int yellow, int eventValue)
    {
        if (!isServer) return;
        this.Red = "Red Dice Roll: " + red.ToString();
        this.Yellow = "Yellow Dice Roll: " + yellow.ToString();
        this.Event = "Event Dice Roll: " + ((EventKind)eventValue).ToString();

    }

    public void setToBuildRoads()
    {
        buildShip = false;
        moveShip = false;
        MenuWindow.transform.GetChild(4).GetComponent<Image>().color = new Color32(121, 240, 121, 240);
        MenuWindow.transform.GetChild(5).GetComponent<Image>().color = new Color32(255, 255, 255, 255);
    }

    public void setToBuildShips()
    {
        if (buildShip == false)
        {
            CmdDeselectShip(gameObject);
            buildShip = true;
            moveShip = false;
            MenuWindow.transform.GetChild(4).GetComponent<Image>().color = new Color32(255, 255, 255, 255);
            MenuWindow.transform.GetChild(5).GetComponent<Image>().color = new Color32(121, 240, 121, 240);
        }
        else if (buildShip == true && moveShip == false)
        {
            buildShip = false;
            moveShip = true;
            MenuWindow.transform.GetChild(4).GetComponent<Image>().color = new Color32(255, 255, 255, 255);
            MenuWindow.transform.GetChild(5).GetComponent<Image>().color = new Color32(121, 121, 240, 240); //button becomes blue
        }
    }
    public void setToInteractWithSettlements()
    {
        interactKnight = false;
        MenuWindow.transform.GetChild(3).GetComponent<Image>().color = new Color32(121, 240, 121, 240);
        MenuWindow.transform.GetChild(8).GetComponent<Image>().color = new Color32(255, 255, 255, 255);
    }

    public void setToInteractWithKnights()
    {
        interactKnight = true;
        MenuWindow.transform.GetChild(3).GetComponent<Image>().color = new Color32(255, 255, 255, 255);
        if (!buildKnight && !activateKnight && !upgradeKnight)
        {
            buildKnight = true;
            activateKnight = false;
            moveKnight = false;
            upgradeKnight = false;

            MenuWindow.transform.GetChild(8).GetComponent<Image>().color = new Color32(121, 121, 121, 121);
            MenuWindow.transform.GetChild(8).GetChild(0).GetComponent<Text>().text = "Building Knight";

        }
        else if (!activateKnight && !upgradeKnight & !moveKnight)
        {
            buildKnight = false;
            activateKnight = true;
            upgradeKnight = false;
            moveKnight = false;

            MenuWindow.transform.GetChild(8).GetComponent<Image>().color = new Color32(121, 240, 121, 240);
            MenuWindow.transform.GetChild(8).GetChild(0).GetComponent<Text>().text = "Activating Knight";
        }
        else if (!upgradeKnight && !moveKnight && !buildKnight)
        {
            buildKnight = false;
            activateKnight = false;
            upgradeKnight = true;
            moveKnight = false;

            MenuWindow.transform.GetChild(8).GetComponent<Image>().color = new Color32(121, 121, 240, 240);
            MenuWindow.transform.GetChild(8).GetChild(0).GetComponent<Text>().text = "Upgrading Knight";
        }
        else
        {
            buildKnight = false;
            activateKnight = false;
            upgradeKnight = false;
            moveKnight = true;
            MenuWindow.transform.GetChild(8).GetComponent<Image>().color = new Color32(121, 240, 240, 121);

            MenuWindow.transform.GetChild(8).GetChild(0).GetComponent<Text>().text = "Moving Knight";
        }
    }
    public void OnClickAcceptP2PButton()
    {
        CmdAcceptP2PTradeRequest();
    }

    /*
	 * Called when clicked the Green Confirm Button on the P2P trade panel. 
	 * 
	 * The order of resource stored in the List is
	 * Brick, Ore, Wool, Coin, Wheat, Cloth, Lumber, Paper, Gold
	 * 
	 * Send Trade Request to server
	 * 
	 */
    public void confirmP2PTradeStatus()
    {
        //InputField i = this.P2PTrade_PlayerGives.transform.Find ("Brick").transform.GetComponentInChildren<InputField> ();
        string txt = "";
        foreach (Transform child in this.P2PTrade_PlayerGives.transform)
        {
            //print (child.name);
            string input = child.transform.GetComponentInChildren<InputField>().text;
            int number = 0;
            if (int.TryParse(input, out number) && number > 0)
            {
                //txt += child.name + ": " + child.transform.GetComponentInChildren<InputField> ().text + "\n";
                assignNumberToVariable(child.name, number, false);
            }
            else
            {
                assignNumberToVariable(child.name, 0, false);
            }
        }

        foreach (Transform child in this.P2PTrade_PlayerWants.transform)
        {
            //print (child.name);
            string input = child.transform.GetComponentInChildren<InputField>().text;
            int number = 0;
            if (int.TryParse(input, out number) && number > 0)
            {
                //txt += child.name + ": " + child.transform.GetComponentInChildren<InputField> ().text + "\n";
                assignNumberToVariable(child.name, number, true);
            }
            else
            {
                assignNumberToVariable(child.name, 0, true);
            }
        }
        CmdSendP2PTradeRequest(giveBrick, giveOre, giveWool, giveCoin, giveWheat, giveCloth, giveLumber, givePaper, giveGold, wantsBrick, wantsOre, wantsWool, wantsCoin, wantsWheat, wantsCloth, wantsLumber, wantsPaper, wantsGold);

        //gameState.GetComponent<Game> ().P2PTradeOffer (gameObject, giveBrick, giveOre, giveWool, giveCoin, giveWheat, giveCloth, giveLumber, givePaper, giveGold, wantsBrick, wantsOre, wantsWool, wantsCoin, wantsWheat, wantsCloth, wantsLumber, wantsPaper, wantsGold);
    }

    /* Brick, Ore, Wool, Coin, Wheat, Cloth, Lumber, Paper, Gold */
    private void assignNumberToVariable(string name, int quantity, bool wants)
    {
        if (name == "Brick")
        {
            if (wants)
            {
                this.wantsBrick = quantity;
            }
            else
            {
                this.giveBrick = quantity;
            }
        }
        else if (name == "Ore")
        {
            if (wants)
            {
                this.wantsOre = quantity;
            }
            else
            {
                this.giveOre = quantity;
            }
        }
        else if (name == "Wool")
        {
            if (wants)
            {
                this.wantsWool = quantity;
            }
            else
            {
                this.giveWool = quantity;
            }
        }
        else if (name == "Coin")
        {
            if (wants)
            {
                this.wantsCoin = quantity;
            }
            else
            {
                this.giveCoin = quantity;
            }
        }
        else if (name == "Wheat")
        {
            if (wants)
            {
                this.wantsWheat = quantity;
            }
            else
            {
                this.giveWheat = quantity;
            }
        }
        else if (name == "Cloth")
        {
            if (wants)
            {
                this.wantsCloth = quantity;
            }
            else
            {
                this.giveCloth = quantity;
            }
        }
        else if (name == "Lumber")
        {
            if (wants)
            {
                this.wantsLumber = quantity;
            }
            else
            {
                this.giveLumber = quantity;
            }
        }
        else if (name == "Paper")
        {
            if (wants)
            {
                this.wantsPaper = quantity;
            }
            else
            {
                this.givePaper = quantity;
            }
        }
        else if (name == "Gold")
        {
            if (wants)
            {
                this.wantsGold = quantity;
            }
            else
            {
                this.giveGold = quantity;
            }
        }
    }
    #endregion

    #region Retrieve Client Info
    void detectClickedObject()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity);
        if (hit && !EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log(hit.collider.gameObject.name);
            if (hit.collider.gameObject.CompareTag("Intersection"))
            {

                if (selectForScare)
                {
                    CmdSelectKnightForScare(gameObject, hit.collider.gameObject);
                }

                else if (desertSelectKnight)
                {
                    CmdDesertKnight(gameObject, hit.collider.gameObject);
                }

                else if (desertMoveKnight)
                {
                    CmdDesertMoveKnight(gameObject, hit.collider.gameObject);
                }
                else if (intrigue)
                {
                    CmdIntrigue(gameObject, hit.collider.gameObject);
                }

                else if (pickMetropolis)
                {
                    CmdSetMetropole(gameObject, hit.collider.gameObject);
                }

                else if (forceMoveKnight)
                {
                    CmdForceMoveKnight(gameObject, hit.collider.gameObject);
                }

                else if (interactKnight && !moveKnight)

                {
                    CmdBuildKnight(gameObject, hit.collider.gameObject, buildKnight, upgradeKnight);
                }
                else if (interactKnight && moveKnight)
                {
                    CmdMoveKnight(gameObject, hit.collider.gameObject, knightSelected);
                }
                else
                {
                    CmdBuildOnIntersection(hit.collider.gameObject);
                }

            }
            if (hit.collider.gameObject.CompareTag("Edge") && moveShip != true && !forceMoveKnight)
            {
                if (playDiplomat)
                {
                    CmdMoveRoad(hit.collider.gameObject);
                }
                else
                {
                    CmdBuildOnEdge(gameObject, hit.collider.gameObject, buildShip);
                }    
            }
            if (hit.collider.gameObject.CompareTag("TerrainHex"))
            {
                if (playInventor)
                {
                    if (tilesToSwap[0] == null)
                    {
                        tilesToSwap[0] = hit.collider.gameObject;
                    }
                    else
                    {
                        tilesToSwap[1] = hit.collider.gameObject;
                        CmdSwapTokens(tilesToSwap);
                    }
                }
                else
                {
                    if (hit.collider.gameObject.GetComponent<TerrainHex>().myTerrain == TerrainKind.Sea)
                    {
                        CmdMovePirate(gameObject, hit.collider.gameObject);
                    }
                    else
                    {
                        if (!robberMove)
                        {
                            CmdMoveMerchant(gameObject, hit.collider.gameObject);
                        }
                        else
                        {
                            CmdMoveRobber(gameObject, hit.collider.gameObject);
                        }
                    }
                    if (!selectForScare)
                        CmdScareRobber(gameObject, hit.collider.gameObject);
                }
            }
            if (hit.collider.gameObject.CompareTag("Edge") && moveShip == true && movedShipThisTurn == false && !forceMoveKnight)
            {
                CmdMoveShip(gameObject, hit.collider.gameObject, shipSelected);
            }
        }
    }

    public void getNameToSend()
    {
        string playerName = nameWindow.transform.GetChild(0).GetChild(2).GetComponent<Text>().text;
        if (!playerName.Equals("") && playerName != null)
        {
            CmdValidateName(playerName);
            //if (!isValidName) return;
            //CmdSendName(playerName);
            //open the menus
            //resourcesWindow.gameObject.SetActive(true);
            //MenuWindow.gameObject.SetActive(true);
            //DiceWindow.gameObject.SetActive(true);
            //ChatWindow.gameObject.SetActive(true);
            //CardPanel.gameObject.SetActive(true);
            //closet the window
            //nameWindow.SetActive(false);
        }

    }

    [Command]
    void CmdAcceptP2PTradeRequest()
    {
        gameState.GetComponent<Game>().playerAcceptedTrade(tradingPlayer, gameObject, this.giveBrick, this.giveOre, this.giveWool, this.giveCoin, this.giveWheat, this.giveCloth, this.giveLumber, this.givePaper, this.giveGold, this.wantsBrick, this.wantsOre, this.wantsWool, this.wantsCoin, this.wantsWheat, this.wantsCloth, this.wantsLumber, this.wantsPaper, this.wantsGold);
    }
    // Send P2P trade request to Game
    [Command]
    private void CmdSendP2PTradeRequest(int giveBrick, int giveOre, int giveWool, int giveCoin, int giveWheat, int giveCloth, int giveLumber, int givePaper, int giveGold, int wantsBrick, int wantsOre, int wantsWool, int wantsCoin, int wantsWheat, int wantsCloth, int wantsLumber, int wantsPaper, int wantsGold)
    {
        gameState.GetComponent<Game>().P2PTradeOffer(gameObject, giveBrick, giveOre, giveWool, giveCoin, giveWheat, giveCloth, giveLumber, givePaper, giveGold, wantsBrick, wantsOre, wantsWool, wantsCoin, wantsWheat, wantsCloth, wantsLumber, wantsPaper, wantsGold);
    }
    [Command]
    private void CmdValidateName(string name)
    {
        gameState.GetComponent<Game>().ValidateName(gameObject, name);
    }

    public void validateName(bool result)
    {
        this.isValidName = result;
    }
    public void getTradeValue()
    {
        int toGive, wanted;
        toGive = transform.GetChild(3).GetChild(2).GetComponent<Dropdown>().value;
        wanted = transform.GetChild(3).GetChild(3).GetComponent<Dropdown>().value;
        CmdSendNpcTrade(gameObject, toGive, wanted);
    }

    public void GetTradeBuyValue()
    {
        var toBuy = goldShopPanel.transform.GetChild(1).GetComponent<Dropdown>().value;
        CmdBuyFromBank(gameObject, toBuy, buyWithGold);
    }

    public void getDiscardValues()
    {
        int[] values = new int[8];
        int sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            //i = 0 is wool like enum 0 = wool etc...
            if (discardPanel.transform.GetChild(i).GetChild(2).GetComponent<InputField>().text.Equals(""))
            {
                values[i] = 0;
                sum += 0;
            }
            else
            {

                values[i] = int.Parse(discardPanel.transform.GetChild(i).GetChild(2).GetComponent<InputField>().text);
                sum += values[i];
            }
        }
        int needed = int.Parse(discardPanel.transform.GetChild(10).GetComponent<Text>().text);
        //loop to check if he has enough of all the resources
        if (sum == needed)
        {
            CmdSendDiscards(gameObject, values);
            discardPanel.SetActive(false);
        }
        else if (sum > needed)
        {
            discardPanel.transform.GetChild(9).GetComponent<Text>().text = "You need to discard : " + needed.ToString() + " you want to discard : " + sum.ToString() + " please remove : " + (sum - needed);
        }
        else
        {
            discardPanel.transform.GetChild(9).GetComponent<Text>().text = "You need to discard : " + needed.ToString() + " you want to discard : " + sum.ToString() + " please add : " + (needed - sum);
        }

    }

    public void getFishAction()
    {
        int action = fishPanel.transform.GetChild(0).GetComponent<Dropdown>().value;
        switch (action)
        {
            //move robber
            case 0:
                {
                    if (fishTokens > 1)
                    {
                        CmdResetPirate(gameObject);
                    }
                    break;
                }
            case 1:
                {
                    if (fishTokens > 1)
                    {
                        CmdResetRobber(gameObject);
                    }
                    break;
                }
            case 2:
                {
                    if (fishTokens > 2)
                    {
                        CmdInitiateSteal(gameObject);
                    }
                    break;
                }
            case 3:
                {
                    if (fishTokens > 3)
                    {
                        goldShopPanel.SetActive(true);
                        goldShopPanel.transform.GetChild(3).gameObject.SetActive(false);
                        buyWithGold = false;
                    }
                    break;
                }
            case 4:
                {
                    if (fishTokens > 4)
                    {
                        CmdGetFreeRoad(gameObject);
                    }
                    break;
                }
            case 5:
                {
                    if (fishTokens > 6)
                    {
                        CmdInitiateCardChoice(gameObject);
                    }
                    break;
                }
        }

    }

    public void getStealPlayer()
    {
        int player = -1;
        player = stealPanel.transform.GetChild(0).GetComponent<Dropdown>().value;
        string name = stealPanel.transform.GetChild(0).GetComponent<Dropdown>().options[player].text;
        Debug.Log(name);
        CmdStealPlayer(gameObject, name);
    }

    public void getCardChoice(GameObject buttonPressed)
    {
        
        string text = buttonPressed.transform.GetChild(0).GetComponent<Text>().text;
        int temp = -1;
        string cardName = "";
        if (text.Equals("Politics"))
        {
            if (BarbarianDraw)
            {
                CmdGetCardFromSelectedDeck(gameObject, EventKind.Politics);
            }
            else
            {
                temp = cardChoicePanel.transform.GetChild(0).GetChild(1).GetComponent<Dropdown>().value;
                cardName = cardChoicePanel.transform.GetChild(0).GetChild(1).GetComponent<Dropdown>().options[temp].text;
                CmdGetCardChoice(gameObject, cardName);
            }        
        }
        else if (text.Equals("Trade"))
        {
            if (BarbarianDraw)
            {
                CmdGetCardFromSelectedDeck(gameObject, EventKind.Trade);
            }
            else
            {
                temp = cardChoicePanel.transform.GetChild(1).GetChild(1).GetComponent<Dropdown>().value;
                cardName = cardChoicePanel.transform.GetChild(1).GetChild(1).GetComponent<Dropdown>().options[temp].text;
                CmdGetCardChoice(gameObject, cardName);
            }
        }
        else if (text.Equals("Science"))
        {
            if (BarbarianDraw)
            {
                CmdGetCardFromSelectedDeck(gameObject, EventKind.Science);
            }
            else
            {
                temp = cardChoicePanel.transform.GetChild(2).GetChild(1).GetComponent<Dropdown>().value;
                cardName = cardChoicePanel.transform.GetChild(2).GetChild(1).GetComponent<Dropdown>().options[temp].text;
                CmdGetCardChoice(gameObject, cardName);
            }
        }
        
    }

    public void SaveGame()
    {
        var savePath = FileHelper.SanitizePath(inGameMenuPanel.transform.Find("FilePath").GetComponent<InputField>().text);
        if (!string.IsNullOrEmpty(savePath))
        {
            //CmdGetGameData();
            if (this.saveGameData != null)
            {
                File.WriteAllBytes(Application.persistentDataPath + "/" + savePath + ".dat", this.saveGameData.ToArray());
            }
        }
    }
    #endregion

    #region Commands
    [Command]
    public void CmdStartUp()
    {
        if (SceneManager.GetSceneByName("In-Game") != SceneManager.GetActiveScene()) return;
        getGameStateOnServer();
        if (MainMenuBehaviour.isLoaded)
        {
            if (!gameState.GetComponent<Game>().isLoaded)
            {
                // Load the game from a file
                gameState.GetComponent<Game>().Load(MainMenuBehaviour.loadedGameData);
                gameState.GetComponent<Game>().isLoaded = true;
            }
        }
        else
        {
            gameState.GetComponent<Game>().setPlayer(gameObject);
        }
        base.OnStartServer();
    }
    [Command]
    public void CmdSendName(string name)
    {
        gameState.GetComponent<Game>().setPlayerName(gameObject, name);
    }
    [Command]
    void CmdBuildOnIntersection(GameObject intersection)
    {
        gameState.GetComponent<Game>().buildOnIntersection(gameObject, intersection);
    }
    [Command]
    void CmdMoveShip(GameObject player, GameObject edge, bool selected)
    {
        if (!selected)
        {
            gameState.GetComponent<Game>().removeShipCheck(player, edge);
        }
        else
        {
            gameState.GetComponent<Game>().placeShipCheck(player, edge);
        }
    }
    [Command]
    void CmdMoveKnight(GameObject player, GameObject inter, bool selected)
    {
        if (!selected)
        {
            gameState.GetComponent<Game>().selectKnightCheck(player, inter);
        }
        else
        {
            gameState.GetComponent<Game>().moveKnightCheck(player, inter);
        }
    }
    [Command]
    void CmdIntrigue(GameObject player, GameObject inter)
    {
        gameState.GetComponent<Game>().Intrigue(player, inter);
    }


    [Command]
    void CmdDesertKnight(GameObject player, GameObject inter)
    {
        gameState.GetComponent<Game>().desertKnight(player, inter);
    }
    [Command]
    void CmdDesertMoveKnight(GameObject player, GameObject inter)
    {
        gameState.GetComponent<Game>().moveDesertKnight(player, inter);
    }


    [Command]
    void CmdForceMoveKnight(GameObject player, GameObject inter)
    {
        gameState.GetComponent<Game>().forceMoveKnight(player, inter);
    }

    [Command]
    void CmdBuildOnEdge(GameObject player, GameObject edge, bool buildShip)
    {
        if (buildShip)
        {
            gameState.GetComponent<Game>().buildShip(player, edge);
        }
        else
        {
            gameState.GetComponent<Game>().buildRoad(player, edge);
        }

    }
    [Command]
    void CmdDeselectShip(GameObject player)
    {
        gameState.GetComponent<Game>().DeselectShip(player);
    }
    [Command]
    void CmdGetFreeRoad(GameObject player)
    {
        gameState.GetComponent<Game>().freeRoad(player);
    }
    [Command]
    void CmdRollDice(GameObject player)
    {
        gameState.GetComponent<Game>().rollDice(player);
    }
    [Command]
    void CmdSendSelection(GameObject player, int value, bool toggle)
    {
        gameState.GetComponent<Game>().updateSelection(player, value, toggle);
    }
    [Command]
    void CmdEndTurn(GameObject player)
    {
        gameState.GetComponent<Game>().endTurn(player);
    }
    [Command]
    void CmdSendNpcTrade(GameObject player, int toGive, int wanted)
    {

        //obviously not going to trade 4 brick -> 1 brick
        if (toGive != wanted)
        {
            gameState.GetComponent<Game>().NpcTrade(player, toGive, wanted);
        }

    }
    [Command]
    void CmdBuyFromBank(GameObject player, int toBuy, bool currency)
    {
        gameState.GetComponent<Game>().BuyFromBank(player, toBuy, currency);
    }
    [Command]
    void CmdSendMessage(GameObject player, string message)
    {
        gameState.GetComponent<Game>().chatOnServer(player, message);
    }
    [Command]
    void CmdMoveRobber(GameObject player, GameObject tile)
    {
        gameState.GetComponent<Game>().moveRobber(player, tile);
    }
    [Command]
    void CmdMoveMerchant(GameObject player, GameObject tile)
    {
        gameState.GetComponent<Game>().moveMerchent(player, tile);
    }
    [Command]
    void CmdMovePirate(GameObject player, GameObject tile)
    {
        gameState.GetComponent<Game>().movePirate(player, tile);
    }
    [Command]
    void CmdResetRobber(GameObject player)
    {
        gameState.GetComponent<Game>().resetRobber(player);
    }

    [Command]
    void CmdScareRobber(GameObject player, GameObject tile)
    {
        gameState.GetComponent<Game>().scareRobber(player, tile);
    }
    [Command]
    void CmdSelectKnightForScare(GameObject player, GameObject inter)
    {
        gameState.GetComponent<Game>().useKnightScareRobber(player, inter);
    }

    [Command]
    void CmdResetPirate(GameObject player)
    {
        gameState.GetComponent<Game>().resetPirate(player);
    }
    [Command]
    void CmdSendDiscards(GameObject player, int[] values)
    {
        gameState.GetComponent<Game>().discardResources(player, values);
    }
    [Command]
    public void CmdUseCard(ProgressCardKind k)
    {
        gameState.GetComponent<Game>().playCard(gameObject, k);
    }
    [Command]
    void CmdBuildKnight(GameObject player, GameObject intersection, bool build, bool upgrade)
    {
        gameState.GetComponent<Game>().buildKnightOnIntersection(player, intersection, build, upgrade);
    }
    [Command]
    public void CmdSetMetropole(GameObject player, GameObject intersection)
    {
        gameState.GetComponent<Game>().setMetropole(player, intersection);
    }

    [Command]
    public void CmdCityUpgrade(int kind)
    {
        gameState.GetComponent<Game>().improveCity(gameObject, kind);
    }
    [Command]
    public void CmdInitiateSteal(GameObject player)
    {
        gameState.GetComponent<Game>().initiateSteal(player);
    }
    [Command]
    public void CmdStealPlayer(GameObject player, string name)
    {
        gameState.GetComponent<Game>().stealPlayer(gameObject, name);
    }
    [Command]
    public void CmdInitiateCardChoice(GameObject player)
    {
        gameState.GetComponent<Game>().initiateCardChoice(gameObject);
    }
    [Command]
    public void CmdGetCardChoice(GameObject player, string cardName)
    {
        gameState.GetComponent<Game>().CardChoice(gameObject, cardName);
    }
    [Command]
    public void CmdGetCardFromSelectedDeck(GameObject player, EventKind k)
    {
        gameState.GetComponent<Game>().getCardFromDraw(player, k);
        cardChoicePanel.transform.GetChild(0).GetChild(1).gameObject.SetActive(true);
        cardChoicePanel.transform.GetChild(1).GetChild(1).gameObject.SetActive(true);
        cardChoicePanel.transform.GetChild(2).GetChild(1).gameObject.SetActive(true);
        cardChoicePanel.gameObject.SetActive(false);
        BarbarianDraw = false;
    }
    [Command]
    public void CmdSwapTokens(GameObject[] tiles)
    {
        gameState.GetComponent<Game>().SwapTokens(gameObject, tiles);
    }

    [Command]
    public void CmdGetGameData()
    {
        gameState.GetComponent<Game>().SaveGameData(this);
    }

    [Command]
    public void CmdMoveRoad(GameObject edge)
    {
        gameState.GetComponent<Game>().MoveRoad(this.gameObject, edge);
    }

    [ClientRpc]
    public void RpcGetGameData(byte[] data, int offset)
    {
        if (this.saveGameData == null)
        {
            this.saveGameData = new List<byte>();
        }
        for (int i = offset; i < offset + data.Length; i++)
        {
            this.saveGameData.Add(data[i - offset]);
        }

    }
    #endregion

    #region Sync Hooks
    public void OnRobberMoveChange(bool value)
    {
        robberMove = value;
    }
    void OnChangedBrick(string value)
    {
        transform.GetChild(0).GetChild(1).GetChild(0).GetComponent<Text>().text = value;
    }
    void OnChangedOre(string value)
    {
        transform.GetChild(0).GetChild(2).GetChild(0).GetComponent<Text>().text = value;
    }
    void OnChangedWool(string value)
    {
        transform.GetChild(0).GetChild(3).GetChild(0).GetComponent<Text>().text = value;
    }
    void OnChangedCoin(string value)
    {

        transform.GetChild(0).GetChild(4).GetChild(0).GetComponent<Text>().text = value;
    }
    void OnChangedGrain(string value)
    {
        transform.GetChild(0).GetChild(5).GetChild(0).GetComponent<Text>().text = value;
    }
    void OnChangedCloth(string value)
    {
        transform.GetChild(0).GetChild(6).GetChild(0).GetComponent<Text>().text = value;
    }
    void OnChangedLumber(string value)
    {
        transform.GetChild(0).GetChild(7).GetChild(0).GetComponent<Text>().text = value;
    }
    void OnChangedPaper(string value)
    {
        transform.GetChild(0).GetChild(8).GetChild(0).GetComponent<Text>().text = value;
    }
    void OnChangedGold(string value)
    {
        transform.GetChild(0).GetChild(9).GetChild(0).GetComponent<Text>().text = value;
    }
    void OnChangedVictory(string value)
    {
        transform.GetChild(0).GetChild(10).GetChild(0).GetComponent<Text>().text = value;
    }
    void OnChangeFish(int value)
    {
        this.fishTokens = value;
        fishPanel.transform.GetChild(2).GetComponent<Text>().text = "You have: " + fishTokens + " remaining";
    }
    void OnChangedRed(string value)
    {
        DiceWindow.transform.GetChild(2).GetComponent<Text>().text = value;
    }
    void OnChangedYellow(string value)
    {
        DiceWindow.transform.GetChild(3).GetComponent<Text>().text = value;
    }
    void OnChangedEvent(string value)
    {
        DiceWindow.transform.GetChild(1).GetComponent<Text>().text = value;
    }
    void OnNameValidated(bool value)
    {
        this.isValidName = value;
    }
    #endregion

    #region ClientRPC
    [ClientRpc]
    public void RpcUpdateChat(string message)
    {
        ChatWindow.transform.GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetComponent<Text>().text += message;
        Canvas.ForceUpdateCanvases();
        ChatWindow.transform.GetChild(0).GetChild(2).GetComponent<Scrollbar>().value = 0f;
        Canvas.ForceUpdateCanvases();
    }

    // the value to get if he has aquaduct and doesnt receive anything
    [ClientRpc]
    public void RpcAskDesiredAquaResource()
    {
        if (!isLocalPlayer) return;
        isSeletionOpen = true;
        SelectionWindow.gameObject.SetActive(true);
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Clear();

        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Wool" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Lumber" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Ore" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Brick" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Grain" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Coin" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Cloth" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Paper" });

        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().value = 1;
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().value = 0;
        int selectedValue = 0;
        while (isSeletionOpen)
        {
            selectedValue = SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().value;
        }
        SelectionWindow.gameObject.SetActive(false);
        CmdSendSelection(gameObject, selectedValue, TradeCard);
    }
    [ClientRpc]
    public void RpcCloseTrade(bool accepted)
    {
        if (accepted)
        {
            MaritimeWindow.gameObject.SetActive(false);
        }
    }

    [ClientRpc]
    public void RpcCloseGoldShop(bool accepted)
    {
        if (accepted)
        {
            goldShopPanel.gameObject.SetActive(false);
            goldShopPanel.transform.GetChild(3).gameObject.SetActive(true);
            buyWithGold = true;
        }
    }
    [ClientRpc]
    public void RpcBeginShipMove()
    {
        this.shipSelected = true;
    }

    [ClientRpc]
    public void RpcEndShipMove(bool success)
    {
        this.shipSelected = false;
        if (success)
        {
            this.movedShipThisTurn = true;
        }
    }

    [ClientRpc]
    public void RpcCanMoveShipAgain()
    {
        this.movedShipThisTurn = false;
        this.shipSelected = false;
    }

    [ClientRpc]
    public void RpcStartScare()
    {
        selectForScare = true;
    }

    [ClientRpc]
    public void RpcEndScare()
    {
        selectForScare = false;
    }

    [ClientRpc]
    public void RpcStartDesertKnight()
    {
        desertSelectKnight = true;
    }

    [ClientRpc]
    public void RpcEndDesertKnight()
    {
        desertSelectKnight = false;
    }
    [ClientRpc]
    public void RpcBeginDesertKnightMove()
    {
        desertMoveKnight = true;
    }

    [ClientRpc]
    public void RpcEndDesertKnightMove()
    {
        desertMoveKnight = false;
    }


    [ClientRpc]
    public void RpcBeginMetropoleChoice()
    {
        this.improvementPanel.SetActive(false);
        this.pickMetropolis = true;
    }

    [ClientRpc]
    public void RpcEndMetropoleChoice()
    {
        this.pickMetropolis = false;
    }
    [ClientRpc]
    public void RpcBeginKnightMove()
    {
        this.knightSelected = true;
    }
    [ClientRpc]
    public void RpcEndKnightMove()
    {
        this.knightSelected = false;
    }

    [ClientRpc]
    public void RpcBeginForcedKnightMove()
    {
        this.forceMoveKnight = true;
    }

    [ClientRpc]
    public void RpcEndForcedKnightMove()
    {
        this.forceMoveKnight = false;
    }

    [ClientRpc]
    public void RpcStartSelectIntrigue()
    {
        intrigue = true;
    }

    [ClientRpc]
    public void RpcEndSelectIntrigue()
    {
        intrigue = false;
    }

    [ClientRpc]
    public void RpcUpdateTurn(string value)
    {
        transform.GetChild(8).GetComponent<Text>().text = value;
    }

    [ClientRpc]
    public void RpcAddProgressCard(ProgressCardKind value)
    {
        if (!isLocalPlayer) return;
        //adds a card to panel when received;
        GameObject tempCard = Instantiate(cardPrefab);
        //set the card value and it will change its sprite accordingly
        tempCard.GetComponent<CardControl>().setCard(new Card(value));
        //put it in the view
        tempCard.transform.SetParent(CardPanel.transform.GetChild(0).GetChild(0).GetChild(0).transform, false);
    }

    [ClientRpc]
    public void RpcRemoveProgressCard(ProgressCardKind value)
    {
        if (!isLocalPlayer) return;
        CardControl[] tempCards = CardPanel.transform.GetChild(0).GetChild(0).GetChild(0).GetComponentsInChildren<CardControl>();
        foreach (CardControl card in tempCards)
        {
            if (card.getCard().k == value)
            {
                card.removeCard();
                break;
            }
        }
    }
    [ClientRpc]
    public void RpcResourceMonopoly()
    {
        if (!isLocalPlayer) return;
        isSeletionOpen = true;
        SelectionWindow.gameObject.SetActive(true);
        int selectedValue = 0;
        SelectionWindow.transform.GetChild(3).GetComponent<Text>().text = "Select the resource you wish to steal 2 of from all players.";
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Clear();

        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Wool" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Lumber" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Ore" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Brick" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Grain" });

        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().value = 1;
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().value = 0;
        while (isSeletionOpen)
        {
            selectedValue = SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().value;
        }
        SelectionWindow.gameObject.SetActive(false);
        CmdSendSelection(gameObject, selectedValue, TradeCard);
        TradeCard = false;
        SelectionWindow.transform.GetChild(3).GetComponent<Text>().text = "";
    }
    [ClientRpc]
    public void RpcTradeMonopoly()
    {
        if (!isLocalPlayer) return;
        isSeletionOpen = true;
        SelectionWindow.gameObject.SetActive(true);
        int selectedValue = 0;
        SelectionWindow.transform.GetChild(3).GetComponent<Text>().text = "Select the commodity you wish to steal from all players.";
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Clear();

        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Coin" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Cloth" });
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = "Paper" });

        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().value = 1;
        SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().value = 0;
        while (isSeletionOpen)
        {
            selectedValue = SelectionWindow.transform.GetChild(1).GetComponent<Dropdown>().value;
        }
        SelectionWindow.gameObject.SetActive(false);
        CmdSendSelection(gameObject, selectedValue+5, TradeCard);
        TradeCard = false;
        SelectionWindow.transform.GetChild(3).GetComponent<Text>().text = "";
    }
    [ClientRpc]
    public void RpcDiscardTime(int discardAmount, string ExtraInfo)
    {
        if (!isLocalPlayer) return;
        discardPanel.SetActive(true);
        //in order of enums for easy for looping later
        discardPanel.transform.GetChild(0).GetChild(0).GetComponent<Text>().text = Wool;
        discardPanel.transform.GetChild(1).GetChild(0).GetComponent<Text>().text = Lumber;
        discardPanel.transform.GetChild(2).GetChild(0).GetComponent<Text>().text = Ore;
        discardPanel.transform.GetChild(3).GetChild(0).GetComponent<Text>().text = Brick;
        discardPanel.transform.GetChild(4).GetChild(0).GetComponent<Text>().text = Grain;
        discardPanel.transform.GetChild(5).GetChild(0).GetComponent<Text>().text = Coin;
        discardPanel.transform.GetChild(6).GetChild(0).GetComponent<Text>().text = Cloth;
        discardPanel.transform.GetChild(7).GetChild(0).GetComponent<Text>().text = Paper;

        discardPanel.transform.GetChild(9).GetComponent<Text>().text = "You need to discard a total of : " + discardAmount.ToString() + "\n" + ExtraInfo;
        discardPanel.transform.GetChild(10).GetComponent<Text>().text = discardAmount.ToString();
    }

    [ClientRpc]
    public void RpcUpdateSliders(int level, int kind)
    {
        if (!isLocalPlayer) return;
        if (level == 1)
        {
            improvementPanel.transform.GetChild(kind).GetChild(0).GetChild(1).GetChild(0).gameObject.SetActive(true);
        }
        improvementPanel.transform.GetChild(kind).GetChild(0).GetComponent<Slider>().value = level;
    }

    [ClientRpc]
    public void RpcVictoryPanel(string message)
    {
        if (!isLocalPlayer) return;
        this.victoryPanel.SetActive(true);
        this.victoryPanel.transform.Find("VictoryMessage").GetComponent<Text>().text = message;
    }
    [ClientRpc]
    public void RpcNameCheck(bool result)
    {
        if (!isLocalPlayer) return;
        if (result)
        {
            //open the menus
            resourcesWindow.gameObject.SetActive(true);
            MenuWindow.gameObject.SetActive(true);
            DiceWindow.gameObject.SetActive(true);
            ChatWindow.gameObject.SetActive(true);
            CardPanel.gameObject.SetActive(true);
            //closet the window
            nameWindow.SetActive(false);
        }
        else
        {
            nameWindow.transform.GetChild(0).GetComponent<InputField>().text = "";
        }
    }
    /**
 * @author xingwei
 * P2P trade UI text upgrading RPC functions
 */
    [ClientRpc]
    public void RpcLogP2PTradeDebugText(string txt, bool red)
    {

        if (red)
        {
            P2PTrade_DebugText.color = Color.red;
        }
        else
        {
            P2PTrade_DebugText.color = Color.black;
        }
        P2PTrade_DebugText.text = txt;																	 		  
    }
    [ClientRpc]			
	public void RpcReceiveP2PTradeRequestFrom(GameObject requestingPlayer, int giveBrick, int giveOre, int giveWool, int giveCoin, int giveWheat, int giveCloth, int giveLumber, int givePaper, int giveGold, int wantsBrick, int wantsOre, int wantsWool, int wantsCoin, int wantsWheat, int wantsCloth, int wantsLumber, int wantsPaper, int wantsGold, string tradername)
    {
		
		tradingPlayer = requestingPlayer;

		this.P2PTradeOfferFromText.text = "Trade Request From " + tradername;

        this.giveOre = giveOre;
        this.giveBrick = giveBrick;
        this.givePaper = givePaper;
        this.giveWool = giveWool;
        this.giveCoin = giveCoin;
        this.giveWheat = giveWheat;
        this.giveCloth = giveCloth;
        this.giveGold = giveGold;
        this.giveLumber = giveLumber;

        this.wantsOre = wantsOre;
        this.wantsBrick = wantsBrick;
        this.wantsPaper = wantsPaper;
        this.wantsWool = wantsWool;
        this.wantsCoin = wantsCoin;
        this.wantsWheat = wantsWheat;
        this.wantsCloth = wantsCloth;
        this.wantsGold = wantsGold;
        this.wantsLumber = wantsLumber;

        string offerTxt = "";
        if (wantsBrick > 0)
        {
            offerTxt = offerTxt + "Brick :" + wantsBrick.ToString() + "\n";
        }
        if (wantsOre > 0)
        {
            offerTxt = offerTxt + "Ore :" + wantsOre.ToString() + "\n";
        }
        if (wantsWool > 0)
        {
            offerTxt = offerTxt + "Wool :" + wantsWool.ToString() + "\n";
        }
        if (wantsCoin > 0)
        {
            offerTxt = offerTxt + "Coin :" + wantsCoin.ToString() + "\n";
        }
        if (wantsWheat > 0)
        {
            offerTxt = offerTxt + "Wheat :" + wantsWheat.ToString() + "\n";
        }
        if (wantsCloth > 0)
        {
            offerTxt = offerTxt + "Cloth :" + wantsCloth.ToString() + "\n";
        }
        if (wantsLumber > 0)
        {
            offerTxt = offerTxt + "Lumber :" + wantsLumber.ToString() + "\n";
        }
        if (wantsPaper > 0)
        {
            offerTxt = offerTxt + "Paper :" + wantsPaper.ToString() + "\n";
        }
        if (wantsGold > 0)
        {
            offerTxt = offerTxt + "Gold :" + wantsGold.ToString() + "\n";
        }
        this.P2PTradeGivingDescriptionText.text = offerTxt;

        string givesTxt = ""; // trading player gives, so other player receives
        if (giveBrick > 0)
        {
            givesTxt = givesTxt + "Brick :" + giveBrick.ToString() + "\n";
        }
        if (giveOre > 0)
        {
            givesTxt = givesTxt + "Ore :" + giveOre.ToString() + "\n";
        }
        if (giveWool > 0)
        {
            givesTxt = givesTxt + "Wool :" + giveWool.ToString() + "\n";
        }
        if (giveCoin > 0)
        {
            givesTxt = givesTxt + "Coin :" + giveCoin.ToString() + "\n";
        }
        if (giveWheat > 0)
        {
            givesTxt = givesTxt + "Wheat :" + giveWheat.ToString() + "\n";
        }
        if (giveCloth > 0)
        {
            givesTxt = givesTxt + "Cloth :" + giveCloth.ToString() + "\n";
        }
        if (giveLumber > 0)
        {
            givesTxt = givesTxt + "Lumber :" + giveLumber.ToString() + "\n";
        }
        if (givePaper > 0)
        {
            givesTxt = givesTxt + "Paper :" + givePaper.ToString() + "\n";
        }
        if (giveGold > 0)
        {
            givesTxt = givesTxt + "Gold :" + giveGold.ToString() + "\n";
        }
        this.P2PTradeOfferedDescriptionText.text = givesTxt;

		if (isLocalPlayer) {
			this.P2PTradeOfferPanel.SetActive (true);
		}
    }

    [ClientRpc]
    public void RpcSetP2PTradeOfferedDescriptionText(string txt)
    {
        P2PTradeOfferedDescriptionText.text = txt;
    }

    [ClientRpc]
    public void RpcSetP2PTradeGivingDescriptionText(string txt)
    {
        P2PTradeGivingDescriptionText.text = txt;
    }

    [ClientRpc]
    public void RpcSetP2PTradeOfferPanelActive(bool active)
    {
        P2PTradeOfferPanel.SetActive(active);
    }

    [ClientRpc]
    public void RpcSetP2PTradePanelActive(bool active)
    {
        P2PTradePanel.SetActive(active);
    }

    /**
	 * Reset the input field of P2P Trade Panel 
	 */
    [ClientRpc]
    public void RpcResetP2PTradeInput()
    {
        foreach (Transform child in this.P2PTrade_PlayerGives.transform)
        {
            child.transform.GetComponent<InputField>().text = "";
        }		  

    }
    [ClientRpc]
    public void RpcSetupStealInterface(string[] names)
    {
        if (!isLocalPlayer) return;
        stealPanel.SetActive(true);
        stealPanel.transform.GetChild(0).GetComponent<Dropdown>().options.Clear();
        foreach (string s in names)
        {
            stealPanel.transform.GetChild(0).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = s });
        }
        stealPanel.transform.GetChild(0).GetComponent<Dropdown>().value = 1;
        stealPanel.transform.GetChild(0).GetComponent<Dropdown>().value = 0;
    }
    [ClientRpc]
    public void RpcSetupCardChoiceInterface(string[] politics, string[] trade, string[] science, bool barbarian)
    {
        if (!isLocalPlayer) return;
        if (barbarian)
        {
            cardChoicePanel.transform.GetChild(0).GetChild(1).gameObject.SetActive(false);
            cardChoicePanel.transform.GetChild(1).GetChild(1).gameObject.SetActive(false);
            cardChoicePanel.transform.GetChild(2).GetChild(1).gameObject.SetActive(false);
            BarbarianDraw = true;
        }
        else
        {
            cardChoicePanel.transform.GetChild(0).GetChild(1).GetComponent<Dropdown>().options.Clear();
            cardChoicePanel.transform.GetChild(1).GetChild(1).GetComponent<Dropdown>().options.Clear();
            cardChoicePanel.transform.GetChild(2).GetChild(1).GetComponent<Dropdown>().options.Clear();
            foreach (string s in politics)
            {
                cardChoicePanel.transform.GetChild(0).GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = s });
            }
            foreach (string s in trade)
            {
                cardChoicePanel.transform.GetChild(1).GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = s });
            }
            foreach (string s in science)
            {
                cardChoicePanel.transform.GetChild(2).GetChild(1).GetComponent<Dropdown>().options.Add(new Dropdown.OptionData() { text = s });
            }
            cardChoicePanel.transform.GetChild(0).GetChild(1).GetComponent<Dropdown>().value = 1;
            cardChoicePanel.transform.GetChild(0).GetChild(1).GetComponent<Dropdown>().value = 0;
            cardChoicePanel.transform.GetChild(1).GetChild(1).GetComponent<Dropdown>().value = 1;
            cardChoicePanel.transform.GetChild(1).GetChild(1).GetComponent<Dropdown>().value = 0;
            cardChoicePanel.transform.GetChild(2).GetChild(1).GetComponent<Dropdown>().value = 1;
            cardChoicePanel.transform.GetChild(2).GetChild(1).GetComponent<Dropdown>().value = 0;

            cardChoicePanel.SetActive(true);
        }     
    }
    [ClientRpc]
    public void RpcEndStealInterface()
    {
        if (!isLocalPlayer) return;
        stealPanel.SetActive(false);
    }
    [ClientRpc]
    public void RpcEndCardChoiceInterface()
    {
        if (!isLocalPlayer) return;
        cardChoicePanel.SetActive(false);
    }

    [ClientRpc]
    public void RpcBeginInventor()
    {
        this.playInventor = true;
        this.tilesToSwap = new GameObject[2] { null, null };
    }

    [ClientRpc]
    public void RpcEndInventor(bool success)
    {
        this.playInventor = !success;
        this.tilesToSwap = new GameObject[2] { null, null };
    }

    [ClientRpc]
    public void RpcBeginDiplomat()
    {
        this.playDiplomat = true;
    }

    [ClientRpc]
    public void RpcEndDiplomat()
    {
        this.playDiplomat = false;
    }
    #endregion

    public void ExitGame()
    {
        Application.Quit();
    }
}
