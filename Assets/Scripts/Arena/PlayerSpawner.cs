using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Cinemachine;

public class PlayerSpawner : MonoBehaviourPunCallbacks
{
    [SerializeField]
    private GameObject[] playerPrefabs;
    [SerializeField]
    private Transform[] playerSpawnpoints;
    [SerializeField]
    private CinemachineVirtualCamera vcam;

    private void Start()
    {
        GameObject playerToSpawn = playerPrefabs[(int)PhotonNetwork.LocalPlayer.CustomProperties["character"]];
        GameObject spawnedPlayer = PhotonNetwork.Instantiate(playerToSpawn.name, playerSpawnpoints[Random.Range(0,playerSpawnpoints.Length)].position,Quaternion.identity);
        vcam.Follow = spawnedPlayer.transform;
        vcam.LookAt = spawnedPlayer.transform;
    }
}
