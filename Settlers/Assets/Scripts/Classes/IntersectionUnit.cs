using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public abstract class IntersectionUnit : OwnableUnit
{
    public IntersectionUnit(Player owner) : base(owner)
    {

    }

    public IntersectionUnit(Player p, Guid g) : base(p, g)
    {

    }
}

[Serializable]
public class IntersectionUnitData : OwnableUnitData
{
    public string location { get; set; }

    public IntersectionUnitData(IntersectionUnit source) : base(source)
    {
        
    }
}
