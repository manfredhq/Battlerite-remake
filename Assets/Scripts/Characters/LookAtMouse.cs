using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

public class LookAtMouse : MonoBehaviour
{
    public Camera cam;
    private void Update()
    {
        Vector3 p = Input.mousePosition;
        p.z = 20;
        Vector3 pos = Camera.main.ScreenToWorldPoint(p);
        pos.y = transform.position.y;
        transform.LookAt(pos);
    }
}
