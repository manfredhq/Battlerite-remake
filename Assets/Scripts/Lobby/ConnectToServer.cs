using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using Steamworks;

public class ConnectToServer : MonoBehaviourPunCallbacks
{
    [SerializeField]
    private TMP_InputField usernameInput;
    [SerializeField]
    private Button connectButton;
    [SerializeField]
    private TMP_Text connectingText;
    private TMP_Text buttonText;

    private void Update()
    {
        if (SteamManager.Initialized)
        {
            usernameInput.text = SteamFriends.GetPersonaName();
            OnConnectButtonClicked();
        }
    }

    private void Awake()
    {
        buttonText = connectButton.GetComponentInChildren<TMP_Text>();
    }

    public void OnConnectButtonClicked()
    {
        if(!string.IsNullOrWhiteSpace(usernameInput.text))
        {
            connectingText.text = "Connecting...";
            PhotonNetwork.NickName = usernameInput.text;
            PhotonNetwork.ConnectUsingSettings();
            PhotonNetwork.AutomaticallySyncScene = true;
        }
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        SceneManager.LoadScene("Lobby-Scene");
    }
}
