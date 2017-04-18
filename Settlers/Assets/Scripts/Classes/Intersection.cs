using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Intersection : NetworkBehaviour {
    public TerrainHex[] linked;
    public Edges[] paths;
    public Sprite settlement, city, intersection;
    public Sprite[] knightSprites, activeKnightSprites;
    public Sprite metropolisSprite;
    public Sprite cityWallSprite;
    public bool owned;

    public IntersectionUnit positionedUnit { get; private set; }
    public bool knightRemoved = false;

    [SyncVar(hook = "OnHarbour")]
    public HarbourKind harbor = HarbourKind.None;
    [SyncVar(hook = "OnKnight")]
    public KnightLevel knight = KnightLevel.None;
    [SyncVar(hook = "OnActivateKnight")]
    public bool knightActive = false;
    [SyncVar(hook = "OnOwned")]
    Color color;
    [SyncVar(hook = "OnBuild")]
    int type = 0;
    [SyncVar(hook = "OnMetropole")]
    public VillageKind metropolis = VillageKind.City;
    public bool movedKnight = false;
    [SyncVar(hook = "OnFishing")]
    public bool isFishingInter = false;


    // Use this for initialization
    void Awake()
    {
        positionedUnit = null;
        cityWallSprite = (Sprite)Resources.Load<Sprite>("CityWall");
        intersection = (Sprite)Resources.Load<Sprite>("intersection");
    }

    public Color getColor()
    {
        return color;
    }

    public int getType()
    {
        return type;
    }

    #region Actions
    public void BuildSettlement(Player player)
    {
        positionedUnit = new Village(player);
        player.ownedUnits.Add(positionedUnit);
        type = 1;
        owned = true;
        switch (positionedUnit.Owner.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }
        //add the harbour to the list of owned harbour for when he trades it knows
        if (harbor != HarbourKind.None)
        {
            player.ownedHarbour.Add(harbor);
        }
    }
    public void BuildCity(Player player)
    {
        positionedUnit = new Village(player);
        ((Village)positionedUnit).setVillageType(VillageKind.City);
        owned = true;
        player.ownedUnits.Add(positionedUnit);
        type = 2;
        switch (positionedUnit.Owner.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }
        //add the harbour to the list of owned harbour for when he trades it knows
        if (harbor != HarbourKind.None)
        {
            player.ownedHarbour.Add(harbor);
        }
    }
    public void UpgradeSettlement(Player player)
    {
        ((Village)positionedUnit).setVillageType(VillageKind.City);
        type = 2;
        switch (positionedUnit.Owner.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }
    }

    public void DowngradeCity(Player player)
    {
        type = 1;
        switch (positionedUnit.Owner.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }
    }
    #endregion
    public void BuildKnight(Player player)
    {
        positionedUnit = new Knight(player, KnightLevel.Basic);
        player.ownedUnits.Add(positionedUnit);
        owned = true;
        knight = KnightLevel.Basic;
        switch (positionedUnit.Owner.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }
    }

    public void BuildDesertKnight (Player player)
    {
        movedKnight = true;
        positionedUnit = new Knight(player, player.desertKnightLevel);
        player.ownedUnits.Add(positionedUnit);
        owned = true;
        knight = player.desertKnightLevel;
        if (player.desertKnightActive)
        {
            var temp = positionedUnit as Knight;
            temp.activateKnight();
        }
        knightActive = player.desertKnightActive;

        switch (positionedUnit.Owner.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }
    }
    public void MoveKnight(Player player, Knight knightToMove, bool forced)
    {
        movedKnight = true;
        Knight temp = knightToMove;
        positionedUnit = temp;
        owned = true;
        knight = temp.level;
        if (!forced)
        {
            temp.deactivateKnight();
        }
        else
        {
            knightActive = knightToMove.isKnightActive();
        } 

        switch (positionedUnit.Owner.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }											
    }
    public void RemoveKnight(Player player, bool destroy)
    {
        knightRemoved = true;
        owned = false;
        Knight temp = (Knight)positionedUnit;
        // temp.deactivateKnight();
        knight = KnightLevel.None;
        knightActive = false;


        positionedUnit = null;

        if (destroy)
        {
            player.AddKnight(temp.level);
            player.ownedUnits.Remove(positionedUnit);
        }

        color = new Color(255, 255, 255);

    }

    public void SelectKnight()
    {
        color = new Color32(121, 121, 240, 240);
    }

    public void DeselectKnight()
    {
        switch (positionedUnit.Owner.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }

    }

    public void BuildWall(Player player)
    {
        type = 3;
        player.availableWalls--;
        switch (positionedUnit.Owner.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }
    }

    #region Sync Hooks
    public void OnOwned(Color value)
    {
        gameObject.GetComponent<SpriteRenderer>().color = value;
        if (knightRemoved)
        {
            knightRemoved = false;
        }
        else
        {
            owned = true;
        }
    }
	public void OnWall(bool value){
		
		transform.GetComponent<SpriteRenderer>().sprite = cityWallSprite;
	}							
    public void OnBuild(int value)
    {
        type = value;
        if (value == 1)
        {
            transform.GetComponent<SpriteRenderer>().sprite = settlement;
        }
        else if (value == 2)
        {
            transform.GetComponent<SpriteRenderer>().sprite = city;
        }
        else if (value == 3)
        {
            
            transform.GetComponent<SpriteRenderer>().sprite = cityWallSprite;

            transform.GetComponent<CircleCollider2D>().radius = 0.6f;
        }
    }
    public void OnHarbour(HarbourKind value)
    {
        harbor = value;
    }
    public void OnKnight(KnightLevel value)
    {
        knight = value;
        if (knight == KnightLevel.None)
        {
            transform.GetComponent<SpriteRenderer>().sprite = intersection;
            transform.GetComponent<SpriteRenderer>().color = Color.white;
            transform.GetComponent<CircleCollider2D>().radius = 0.2f;
        }
        else if (knight == KnightLevel.Basic || movedKnight)
        {
			movedKnight = false;					
            transform.GetComponent<SpriteRenderer>().sprite = knightSprites[(int)knight];
            transform.GetComponent<CircleCollider2D>().radius = 0.6f;
        }
        else if (knightActive)
        {
            transform.GetComponent<SpriteRenderer>().sprite = activeKnightSprites[(int)knight];
        }
        						  		 																							   		 
        else if (!knightActive)
        {
            transform.GetComponent<SpriteRenderer>().sprite = knightSprites[(int)knight];
        }

    }
    public void OnActivateKnight(bool value)
    {
        knightActive = value;
        if (knightActive)
        {
            transform.GetComponent<SpriteRenderer>().sprite = activeKnightSprites[(int)knight];
        }
        else
        {
            if (knight != KnightLevel.None)
                transform.GetComponent<SpriteRenderer>().sprite = knightSprites[(int)knight];
        }
    }

    public void OnMetropole(VillageKind value)
    {
        metropolis = value;
        switch (metropolis)
        {
            case VillageKind.PoliticsMetropole:
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = metropolisSprite;
                transform.GetChild(0).GetComponent<SpriteRenderer>().color = Color.blue;
                break;
            case VillageKind.ScienceMetropole:
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = metropolisSprite;
                transform.GetChild(0).GetComponent<SpriteRenderer>().color = Color.green;
                break;
            case VillageKind.TradeMetropole:
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = metropolisSprite;
                transform.GetChild(0).GetComponent<SpriteRenderer>().color = Color.yellow;
                break;
            default:
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = null;
                break;
        }
    }
    public void OnFishing(bool value)
    {
        isFishingInter = value;
    }
    #endregion

    #region Loading
    public void Load(IntersectionData data, IntersectionUnit u)
    {
        this.harbor = data.harbourKind;
        this.isFishingInter = data.isFishing;
        this.owned = data.owned;
        if (u != null)
        {
            if (u is Village)
            {
                var village = (Village)u;
                LoadVillage(village, data);
            }
            else if (u is Knight)
            {
                var knight = (Knight)u;
                LoadKnight(knight);
            }
        }
    }

    private void LoadVillage(Village village, IntersectionData data)
    {
        positionedUnit = village;
        type = data.type;
        owned = true;
        switch (positionedUnit.Owner.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }
        metropolis = data.metropolis;
    }

    private void LoadKnight(Knight k)
    {
        positionedUnit = k;
        owned = true;
        knight = k.level;
        switch (positionedUnit.Owner.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }
    }
    #endregion
}

[Serializable]
public class IntersectionData
{
    public string name { get; set; }
    public string[] linkedHexes { get; set; }
    public string[] paths { get; set; }
    public float[] color { get; set; }
    public Guid positionedUnit { get; set; }
    public float[] position { get; set; }
    public HarbourKind harbourKind { get; set; }
    public KnightLevel knight { get; set; }
    public bool knightActive { get; set; }
    public int type { get; set; }
    public VillageKind metropolis { get; set; }
    public bool isFishing { get; set; }

    public bool owned { get; set; }
    public bool movedKnight { get; set; }

    public IntersectionData(Intersection source)
    {
        this.name = source.name;
        this.color = source.getColor() != null ? 
            new float[] { source.getColor().r, source.getColor().g, source.getColor().b, source.getColor().a } : 
            null;
        this.knight = source.knight;
        this.knightActive = source.knightActive;
        this.type = source.getType();
        this.metropolis = source.metropolis;
        this.positionedUnit = source.positionedUnit == null ? 
            Guid.Empty : 
            source.positionedUnit.id;
        this.owned = source.owned;
        this.harbourKind = source.harbor;
        this.isFishing = source.isFishingInter;

        this.linkedHexes = new string[source.linked.Length];
        for (int i = 0; i < source.linked.Length; i++)
        {
            this.linkedHexes[i] = source.linked[i].name;
        }

        this.paths = new string[source.paths.Length];
        for (int i = 0; i < source.paths.Length; i++)
        {
            this.paths[i] = source.paths[i].name;
        }

        this.position = new float[3] { source.transform.position.x, source.transform.position.y, source.transform.position.z };
    }
}
