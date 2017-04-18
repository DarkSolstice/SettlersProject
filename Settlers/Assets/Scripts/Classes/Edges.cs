using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Edges : NetworkBehaviour {

    [SyncVar(hook = "OnOwned")]
    Color color;
    public bool owned = false;
    [SyncVar(hook = "OnShip")]
    public bool isShip = false;
    public Player belongsTo;
    public Intersection[] endPoints;
    public TerrainHex[] inBetween;
    public Sprite[] harborSprites;
    public Sprite shipSprite, roadSprite;
	public bool shipRemoved = false;

    [SyncVar(hook = "OnHarbor")]
    public HarbourKind harbor = HarbourKind.None;
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public void BuildRoad(Player player)
    {
        belongsTo = player;
        owned = true;
        switch (belongsTo.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }
    }

    public void BuildShip(Player player)
    {
        belongsTo = player;
        owned = true;
        isShip = true;
        switch (belongsTo.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }
    }

	public void RemoveShip (Player player){
		belongsTo = null;
		owned = false;
		isShip = false;
		shipRemoved = true;
		color = new Color(255, 255, 255);
	}

    public void RemoveRoad(Player player)
    {
        belongsTo = null;
        owned = false;
        isShip = false;
        shipRemoved = true;
        color = new Color(255, 255, 255);
    }

    public void SelectShipForMoving(Player player)
    {
        color = new Color32(121, 121, 240, 240);
    }

    public void DeselectShipForMoving(Player player)
    {
        switch (belongsTo.myColor)
        {
            case 0: color = Color.red; break;
            case 1: color = Color.blue; break;
            case 2: color = Color.green; break;
            case 3: color = new Color(255, 128, 0); break;
        }
    }



    public void OnOwned(Color value)
    {
        gameObject.GetComponent<SpriteRenderer>().color = value;
		if (shipRemoved) {
			shipRemoved = false;
		} else {
        	owned = true;
		}
    }

    public void OnShip(bool value)
    {
        isShip = value;
        if (isShip)
        {
            transform.GetComponent<SpriteRenderer>().sprite = shipSprite;
        }
        else
        {
            transform.GetComponent<SpriteRenderer>().sprite = roadSprite;
        }
    }
    public void setHarborKind(HarbourKind kind)
    {
        harbor = kind;
    }
    void OnHarbor(HarbourKind value)
    {
        if (value != HarbourKind.None) { 
            harbor = value;
            bool flip = false;
            float x = 0; float y = 0;
            if (inBetween.Length == 1)
            {
                x = transform.localPosition.x - inBetween[0].transform.localPosition.x;
                y = transform.localPosition.y - inBetween[0].transform.localPosition.y;


            }
            else
            {
                foreach (TerrainHex tile in inBetween)
                {
                    if (tile.myTerrain != TerrainKind.Sea)
                    {
                        y = transform.localPosition.y - tile.transform.localPosition.y;
                        x = transform.localPosition.x - tile.transform.localPosition.x;            
                    }
                }
            }
            Debug.Log(x + "," + y);
            //flip checks according to position of the sea tile or the outside of the land
            if (x > 0 && (y > -0.2 && y < 0.2))
            {
                flip = true;
            }
            else if (x < 0 && y < -0.25 )
            {
                flip = true;
            }
            else if (x > 0 && y < -0.25 )
            {
                flip = true;
            }
            //spawn it
            transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = harborSprites[(int)value];
            //checked to see if its on the right side if not flip it over to the other side of the edge
            if (flip)
            {
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = harborSprites[(int)value];
                transform.GetChild(0).localPosition = new Vector3(-transform.GetChild(0).localPosition.x, -transform.GetChild(0).localPosition.y, 0);
                transform.GetChild(0).localScale = new Vector3(-1, -1, 1);
            }
        }
    }

    public void Load(EdgeData data, Player p)
    {
        if (p != null)
        {
            if (data.isShip)
            {
                BuildShip(p);
            }
            else
            {
                BuildRoad(p);
            }
        }
    }
}
[Serializable]
public class EdgeData
{
    public string name { get; set; }
    public string belongsTo { get; set; }
    public string[] inBetween { get; set; }
    public string[] endPoints { get; set; }
    public bool isShip { get; set; }
    public float[] position { get; set; }

    public EdgeData(Edges source)
    {
        this.name = source.name;
        this.belongsTo = source.belongsTo == null ? string.Empty : source.belongsTo.name;
        this.inBetween = new string[source.inBetween.Length];
        for (int i = 0; i < source.inBetween.Length; i++)
        {
            this.inBetween[i] = source.inBetween[i].name;
        }

        this.endPoints = new string[source.endPoints.Length];
        for (int i = 0; i < source.endPoints.Length; i++)
        {
            this.endPoints[i] = source.endPoints[i].name;
        }

        this.isShip = source.isShip;
        this.position = new float[3] { source.transform.position.x, source.transform.position.y, source.transform.position.z };
    }
}
