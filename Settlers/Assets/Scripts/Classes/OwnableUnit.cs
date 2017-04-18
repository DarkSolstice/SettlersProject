using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class OwnableUnit {

    public Guid id { get; private set; }
    public Player Owner { get; private set; }

    public OwnableUnit(Player owner)
    {
        this.Owner = owner;
        this.id = Guid.NewGuid();
    }

    public OwnableUnit(Player owner, Guid g)
    {
        this.Owner = owner;
        this.id = g;
    }
}

[Serializable]
public class OwnableUnitData
{
    public string id { get; set; }
    public string owner { get; set; }

    public OwnableUnitData(OwnableUnit source)
    {
        this.id = source.id.ToString();
        this.owner = source.Owner.name;
    }
}
