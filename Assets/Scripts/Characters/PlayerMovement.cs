using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon;
using Photon.Pun;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 10.0f;
    [SerializeField]
    private Rigidbody rb;
    private PhotonView photonView;
    [SerializeField]
    private Animator animator;
    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        if(photonView.IsMine == false)
        {
            rb.mass = 100000;
        }        
    }
    private void FixedUpdate()
    {
        if (photonView.IsMine == false)
        {
            rb.constraints = RigidbodyConstraints.FreezeAll;
            return;
        }
        Vector3 targetPos = transform.position;
        if (Input.GetKey(KeyCode.D))
        {
            targetPos += Vector3.right * speed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.Q))
        {
            targetPos += Vector3.left * speed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.Z))
        {
            targetPos += Vector3.forward * speed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.S))
        {
            targetPos += Vector3.back * speed * Time.deltaTime;
        }
        rb.velocity = targetPos - transform.position;
    }

}
