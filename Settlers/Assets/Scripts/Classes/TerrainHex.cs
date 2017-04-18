using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class TerrainHex : NetworkBehaviour
{
    public Sprite[] terrainSprites;
    public Sprite[] tokensSprites;
    public Sprite robberSprite;
    public Sprite pirateSprite;
    public Sprite merchantSprite;
    public Sprite merchantAndRobberSprite;
    public Sprite lakeSprite;
    public Sprite fishingSprite;

    [SyncVar(hook = "OnChangeKind")]
    public TerrainKind myTerrain = TerrainKind.None;

    [SyncVar(hook = "OnSetToken")]
    public int numberToken = 1;

    [SyncVar(hook = "OnChangeRobber")]
    public bool isRobber = false;

    [SyncVar(hook = "OnChangePirate")]
    public bool isPirate = false;

    [SyncVar(hook = "OnChangeLake")]
    public bool isLake = false;

    [SyncVar(hook = "OnChangeFishingSpot")]
    public bool hasFishing = false;

    [SyncVar(hook = "OnChangeMerchant")]
    public bool hasMerchant = false;

    public Intersection[] corners;
    public Edges[] myEdges;

    // Use this for initialization
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }
    void OnChangeKind(TerrainKind value)
    {
        myTerrain = value;
        transform.GetComponent<SpriteRenderer>().sprite = terrainSprites[(int)value];

    }
    void OnSetToken(int value)
    {
        numberToken = value;
        transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = tokensSprites[value - 1];
    }

    public void setTileNumber(int i)
    {
        numberToken = i;
    }
    void OnChangeRobber(bool value)
    {
        isRobber = value;
        if (value && hasMerchant)
        {
            transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = merchantAndRobberSprite;
        }
        else if (value)
        {
            transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = robberSprite;
        }
        else
        {
            if (hasMerchant)
            {
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = merchantSprite;
            }
            else if (numberToken == 1)
            {
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = null;
            }
            else
            {
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = tokensSprites[numberToken - 1];
            }
        }
    }

    void OnChangePirate(bool value)
    {
        isPirate = value;
        if (value)
        {
            transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = pirateSprite;
        }
        else
        {
            if (numberToken == 1)
            {
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = null;
            }
            else
            {
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = tokensSprites[numberToken - 1];
            }
        }
    }
    public void OnChangeLake(bool value)
    {
        isLake = value;
        transform.GetComponent<SpriteRenderer>().sprite = lakeSprite;
    }
    public void OnChangeFishingSpot(bool value)
    {
        hasFishing = value;
        Intersection centerPoint = null;
        if (hasFishing)
        {
            foreach (Intersection inter in corners)
            {
                if (!inter.isFishingInter)
                {
                    continue;
                }
                int neighbors = 0;
                foreach (Edges e in inter.paths)
                {
                    if (e.inBetween[0] == this || e.inBetween[1] == this)
                    {
                        foreach (Intersection inter2 in e.endPoints)
                        {
                            if (inter2 != inter && inter2.isFishingInter)
                            {
                                neighbors++;
                                continue;
                            }
                        }
                    }
                }
                if (neighbors == 2)
                {
                    centerPoint = inter;
                    break;
                }
            }
        }
        if (centerPoint != null)
        {
            //rotating the sprite the correct way based on the center point
            float x = centerPoint.transform.localPosition.x;
            float y = centerPoint.transform.localPosition.y;
            Debug.Log("(" + x + "," + y + ")");
            float x2 = this.transform.localPosition.x;
            float y2 = this.transform.localPosition.y;
            Debug.Log("(" + x2 + "," + y2 + ")");

            float difX = x - x2; float difY = y - y2;
            if (difX > -0.1 && difX < 0.1 && difY > 0)
            {
                //do nothing basic sprite will do
            }
            else if (difX > -0.1 && difX < 0.1 && difY < 0)
            {
                Debug.Log("turning 60 deg counter");
                transform.localRotation = Quaternion.Euler(0, 0, 180);
                transform.GetChild(0).localRotation = Quaternion.Euler(0, 0, -180);
            }
            else if (difX > 0 && difY > 0)
            {
                Debug.Log("turning 300 deg counter");
                transform.localRotation = Quaternion.Euler(0, 0, 300);
                transform.GetChild(0).localRotation = Quaternion.Euler(0, 0, -300);
            }
            else if (difX > 0 && difY < 0)
            {
                Debug.Log("turning 240 deg counter");
                transform.localRotation = Quaternion.Euler(0, 0, 240);
                transform.GetChild(0).localRotation = Quaternion.Euler(0, 0, -240);
            }
            else if (difX < 0 && difY > 0)
            {
                Debug.Log("turning 60 deg counter");
                transform.localRotation = Quaternion.Euler(0, 0, 60);
                transform.GetChild(0).localRotation = Quaternion.Euler(0, 0, -60);
            }
            else if (difX < 0 && difY < 0)
            {
                Debug.Log("turning 120 deg counter");
                transform.localRotation = Quaternion.Euler(0, 0, 120);
                transform.GetChild(0).localRotation = Quaternion.Euler(0, 0, -120);
            }

            transform.GetComponent<SpriteRenderer>().sprite = fishingSprite;
        }

    }

    public void OnChangeMerchant(bool value)
    {
        hasMerchant = value;
        if (value)
        {
            if (isRobber)
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = merchantAndRobberSprite;
            else
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = merchantSprite;
        }
        else
        {
            if (isRobber)
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = robberSprite;
            else if (numberToken == 1)
            {
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = null;
            }
            else
            {
                transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = tokensSprites[numberToken - 1];
            }
        }
    }

    public void setTile(int terrainKind, int tokenValue)
    {
        myTerrain = (TerrainKind)terrainKind;
        if ((TerrainKind)terrainKind != TerrainKind.Desert && (TerrainKind)terrainKind != TerrainKind.Sea)
        {
            numberToken = tokenValue;
        }
    }

    public void Load(TerrainHexData data)
    {
        this.myTerrain = data.myTerrain;
        this.numberToken = data.numberToken;
        this.isRobber = data.isRobber;
        this.isPirate = data.isPirate;
        this.hasMerchant = data.hasMerchent;
        this.hasFishing = data.hasFishing;
    }
}

[Serializable]
public class TerrainHexData
{
    public string name { get; set; }
    public string[] edges { get; set; }
    public string[] corners { get; set; }
    public TerrainKind myTerrain { get; set; }
    public int numberToken { get; set; }
    public bool isRobber { get; set; }
    public bool isPirate { get; set; }
    public bool hasMerchent { get; set; }
    public float[] position { get; set; }

    public bool hasFishing { get; set; }

    public TerrainHexData(TerrainHex source)
    {
        this.name = source.name;
        this.myTerrain = source.myTerrain;
        this.numberToken = source.numberToken;
        this.isRobber = source.isRobber;
        this.isPirate = source.isPirate;
        this.hasMerchent = source.hasMerchant;
        this.edges = new string[source.myEdges.Length];
        this.hasFishing = source.hasFishing;
        for (int i = 0; i < source.myEdges.Length; i++)
        {
            this.edges[i] = source.myEdges[i].name;
        }

        this.corners = new string[source.corners.Length];
        for (int i = 0; i < source.corners.Length; i++)
        {
            this.corners[i] = source.corners[i].name;
        }

        this.position = new float[3] { source.transform.position.x, source.transform.position.y, source.transform.position.z };
    }
}
