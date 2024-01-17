using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class PlayerSpawner : MonoBehaviourPunCallbacks
{
    [SerializeField]
    private GameObject[] playerPrefabs;
    [SerializeField]
    private Transform[] playerSpawnpoints;

    private void Start()
    {
        GameObject playerToSpawn = playerPrefabs[(int)PhotonNetwork.LocalPlayer.CustomProperties["character"]];
        PhotonNetwork.Instantiate(playerToSpawn.name, playerSpawnpoints[Random.Range(0,playerSpawnpoints.Length)].position,Quaternion.identity);
    }
}
