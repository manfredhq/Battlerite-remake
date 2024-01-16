using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.UI;
using Photon.Realtime;
using TMPro;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [SerializeField]
    private TMP_InputField roomNameInput;
    [SerializeField]
    private GameObject lobbyPanel;
    [SerializeField]
    private GameObject roomPanel;
    [SerializeField]
    private TMP_Text roomName;
    [SerializeField]
    private Transform roomListParent;
    [SerializeField]
    private GameObject roomListPrefab;
    private List<GameObject> roomList = new List<GameObject>();

    private float timeBetweenListUpdate = .5f;
    private float nextListUpdateTime = 0;

    private void Start()
    {
        PhotonNetwork.JoinLobby();
        roomPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    public void OnClickCreateRoom()
    {
        if (!string.IsNullOrEmpty(roomNameInput.text))
        {
            PhotonNetwork.CreateRoom(roomNameInput.text); //Can add some room options
        }
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        lobbyPanel.SetActive(false);
        roomPanel.SetActive(true);
        roomName.text = $"Room: {PhotonNetwork.CurrentRoom.Name}";
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        base.OnRoomListUpdate(roomList);
        if(Time.time >= nextListUpdateTime)
        {
            UpdateRoomList(roomList);
            nextListUpdateTime = Time.time + timeBetweenListUpdate;
        }
    }

    private void UpdateRoomList(List<RoomInfo> roomInfoList)
    {
        foreach (var room in roomList)
        {
            Destroy(room);
        }
        roomList.Clear();

        foreach (var roomInfo in roomInfoList)
        {
            RoomItem temp = Instantiate(roomListPrefab, roomListParent).GetComponent<RoomItem>();
            temp.SetLobbyManager(this);
            temp.SetRoomName(roomInfo.Name);
            roomList.Add(temp.gameObject);
        }
    }

    public void JoinRoom(string roomName)
    {
        PhotonNetwork.JoinRoom(roomName);
    }

    public void OnClickLeaveRoomButton()
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        roomPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        PhotonNetwork.JoinLobby();
    }
}
