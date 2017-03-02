﻿using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEngine.UI;
using UnityEngine.Networking;

public class playerControl : NetworkBehaviour {

    Animator resourceAnimator;
    NetworkManager networkManage;
    public bool resourcesShown;
    InputField chat;
	// Use this for initialization
	void Start () {
        if (!isLocalPlayer) return;
        transform.GetChild(1).GetChild(0).gameObject.SetActive(true);
        transform.GetChild(1).GetChild(2).gameObject.SetActive(false);
        resourceAnimator = GetComponent<Animator>();
        resourcesShown = resourceAnimator.GetBool("ResourcesShown");
        //get networkManager that has been used (only works if a host/client has started)
        networkManage = NetworkManager.singleton;
        chat = transform.GetChild(2).GetChild(1).GetComponent<InputField>();
    }
	
	// Update is called once per frame
	void Update () {
        if (!isLocalPlayer) return;
        if (Input.GetButton("Tab"))
        {
            transform.GetChild(1).gameObject.SetActive(true);
        }
        else { transform.GetChild(1).gameObject.SetActive(false); }

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Pressed left click, casting ray.");
            detectClickedObject();
        }
        if (Input.GetButtonDown("Submit"))
        {
          
        }
	}
    public void switchResourcesView()
    {
        resourcesShown = !resourcesShown;
        resourceAnimator.SetBool("ResourcesShown", resourcesShown);
    }

    void detectClickedObject()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity);
        if(hit)
        {
            Debug.Log(hit.collider.gameObject.name);
            if (hit.collider.gameObject.CompareTag("TerrainHex"))
            {
                CmdChange(hit.collider.gameObject);
            }
        }   
    }
    [Command]
    void CmdChange(GameObject go)
    {
        go.GetComponent<TerrainHex>().CmdChangeColor();
    }
}
