using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;

public class ConnectToServer : MonoBehaviourPunCallbacks
{
    [SerializeField]
    private TMP_InputField usernameInput;
    [SerializeField]
    private Button connectButton;
    private TMP_Text buttonText;

    private void Awake()
    {
        buttonText = connectButton.GetComponentInChildren<TMP_Text>();
    }

    public void OnConnectButtonClicked()
    {
        if(!string.IsNullOrWhiteSpace(usernameInput.text))
        {
            PhotonNetwork.NickName = usernameInput.text;
            buttonText.text = "Connecting...";
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        SceneManager.LoadScene("Lobby-Scene");
    }
}
