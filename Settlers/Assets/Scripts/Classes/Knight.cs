using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Knight : IntersectionUnit {

    public KnightLevel level;
    bool isActive = false;
    bool firstTurn = true;
    bool promotedThisTurn = false;
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
    public Knight(Player p,KnightLevel level) : base(p)
    {
        this.level = level;
    }

    public Knight(Player p, Guid g, KnightLevel level) : base(p,g)
    {
        this.level = level;
    }

    public void upgradeKnight()
    {
        if(level != KnightLevel.Mighty)
        {
            level = (KnightLevel)(((int)level) + 1);
        }
        
    }
    public void activateKnight()
    {
        isActive = true;
    }
    public void deactivateKnight()
    {
        isActive = false;
    }

    public bool isKnightActive()
    {
        return isActive;
    }

    public bool isFirstTurn()
    {
        return firstTurn;
    }

    public void setFirstTurn(bool ft)
    {
        this.firstTurn = ft;
    }

    public bool isPromotedThisTurn()
    {
        return promotedThisTurn;
    }

    public void setPromotedThisTurn(bool value)
    {
        promotedThisTurn = value;
    }

    public static Knight Load(KnightData data, Player p)
    {
        var k = new Knight(p, new Guid(data.id), data.level);
        if (data.isActive)
        {
            k.activateKnight();
        }
        else
        {
            k.deactivateKnight();
        }
        k.setFirstTurn(data.firstTurn);
        return k;
    }
}

[Serializable]
public class KnightData : IntersectionUnitData
{
    public KnightLevel level { get; set; }
    public bool isActive { get; set; }
    public bool firstTurn { get; set; }

    public KnightData(Knight source) : base(source)
    {
        this.level = source.level;
        this.isActive = source.isKnightActive();
        this.firstTurn = source.isFirstTurn();
    }
}
