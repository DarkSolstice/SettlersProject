using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

[Serializable]
public class Village : IntersectionUnit
{
    public VillageKind myKind { get; private set; }
    public bool cityWall { get; private set; }
    
    public Village(Player p) : base(p)
    {
        this.myKind = VillageKind.Settlement;
        this.cityWall = false;
    }

    public Village(Player p, Guid g) : base(p, g)
    {

    }
    
    public void setVillageType(VillageKind k)
    {
        this.myKind = k;
    }

    public void setCityWalls(bool cw)
    {
        this.cityWall = cw;
    }

    // Used for barbarians to make sure city wall is cleared
    public void downgradeToSettlement()
    {
        this.myKind = VillageKind.Settlement;
        this.cityWall = false;
    }

    public static Village Load(VillageData data, Player p)
    {
        var v = new Village(p, new Guid(data.id));
        v.setCityWalls(data.cityWall);
        v.setVillageType(data.myKind);
        return v;
    }
}

[Serializable]
public class VillageData : IntersectionUnitData
{
    public VillageKind myKind { get; set; }
    public bool cityWall { get; set; }

    public VillageData(Village source) : base(source)
    {
        this.myKind = source.myKind;
        this.cityWall = source.cityWall;
    }
}

