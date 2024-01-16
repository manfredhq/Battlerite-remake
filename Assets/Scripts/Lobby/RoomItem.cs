using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class RoomItem : MonoBehaviour
{
    public TMP_Text roomName;
    private LobbyManager lobbyManager;

    public void SetRoomName(string _roomName)
    {
        roomName.text = _roomName;
    }

    public void SetLobbyManager(LobbyManager _lobbyManager)
    {
        lobbyManager = _lobbyManager;
    }

    public void OnJoinClicked()
    {
        lobbyManager.JoinRoom(roomName.text);
    }
}
