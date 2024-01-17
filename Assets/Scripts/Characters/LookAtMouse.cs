using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

public class LookAtMouse : MonoBehaviour
{
    private PhotonView photonView;
    private void Awake()
    {
        photonView = GetComponentInParent<PhotonView>();
        if (photonView.IsMine == false)
        {
            enabled = false;
        }
    }
    private void Update()
    {
        Vector3 p = Input.mousePosition;
        p.z = 20;
        Vector3 pos = Camera.main.ScreenToWorldPoint(p);
        pos.y = transform.position.y;
        transform.LookAt(pos);
    }
}
