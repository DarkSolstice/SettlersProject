using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardControl : MonoBehaviour{
    public Sprite[] ProgressCardSprites;
    private Card myCard;
    public void Start()
    {
    }

    public void setCard(Card card)
    {
        myCard = card;
        transform.GetComponent<Image>().sprite = ProgressCardSprites[(int)myCard.k];
    }

    public void clickedCard()
    {
        transform.parent.parent.parent.parent.parent.transform.gameObject.GetComponent<playerControl>().CmdUseCard(myCard.k);
    }

    public Card getCard()
    {
        return myCard;
    }

    public void removeCard()
    {
        DestroyImmediate(gameObject);
    }
}

public class Card
{
    public ProgressCardKind k { get; private set; }

    public Card(ProgressCardKind k)
    {
        this.k = k;
    }
     
}


