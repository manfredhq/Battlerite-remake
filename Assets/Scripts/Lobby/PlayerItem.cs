using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;
using Photon.Realtime;

public class PlayerItem : MonoBehaviour
{
    [SerializeField]
    private TMP_Text playerUsername;
    [SerializeField]
    private Image BackgroundImage;
    public Color highlightColor;

    [SerializeField]
    private Button playerButton;

    private void Start()
    {
        playerButton.enabled = false;
    }
    public void SetPlayerInfos(Player player)
    {
        playerUsername.text = player.NickName;
    }

    public void ApplyLocalChanges()
    {
        BackgroundImage.color = highlightColor;
        playerButton.enabled = true;
    }

    public void OnPlayerButtonClicked()
    {

    }
}
