using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class statistics : NetworkBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        if (Input.GetButton("Tab"))
        {
            transform.GetChild(0).gameObject.SetActive(true);
        }
        else { transform.GetChild(0).gameObject.SetActive(false); }
    }
    [ClientRpc]
    public void RpcSetStatistics(int[] player1, int[] player2, int[] player3, int[] player4, string[] names)
    {
        for(int i = 0; i<8; i++)
        {
            transform.GetChild(0).GetChild(10).GetChild(i + 1).GetComponent<Text>().text = player1[i].ToString();
        }
        for (int i = 0; i < 8; i++)
        {
            transform.GetChild(0).GetChild(11).GetChild(i + 1).GetComponent<Text>().text = player2[i].ToString();
        }
        for (int i = 0; i < 8; i++)
        {
            transform.GetChild(0).GetChild(12).GetChild(i + 1).GetComponent<Text>().text = player3[i].ToString();
        }
        for (int i = 0; i < 8; i++)
        {
            transform.GetChild(0).GetChild(13).GetChild(i + 1).GetComponent<Text>().text = player4[i].ToString();
        }
        for (int i = 0; i < names.Length; i++)
        {
            transform.GetChild(0).GetChild(10 + i).GetChild(0).GetComponent<Text>().text = names[i];
            if (names[i].Equals(""))
            {
                //disable useless info
                transform.GetChild(0).GetChild(10 + i).gameObject.SetActive(false);
            }
        }
    }
}
