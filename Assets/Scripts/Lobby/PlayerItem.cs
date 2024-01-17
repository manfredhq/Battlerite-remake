using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;
using Photon.Realtime;

public class PlayerItem : MonoBehaviourPunCallbacks
{
    [SerializeField]
    private TMP_Text playerUsername;
    [SerializeField]
    private Image BackgroundImage;
    public Color highlightColor;

    [SerializeField]
    private Button playerButton;

    ExitGames.Client.Photon.Hashtable playerProperties = new ExitGames.Client.Photon.Hashtable();
    [SerializeField]
    private Image playerAvatar;
    [SerializeField]
    private Sprite[] avatars;

    Player player;

    public void SetPlayerInfos(Player _player)
    {
        playerUsername.text = _player.NickName;
        player = _player;
        UpdatePlayerCharacter(player);
    }

    public void ApplyLocalChanges()
    {
        BackgroundImage.color = highlightColor;
        playerButton.enabled = true;
        UpdatePlayerCharacter(player);
    }

    public void OnPlayerButtonClicked()
    {
        CharacterSelectionPanel.ChangeCanvasStatus(this);
    }

    public void OnPlayerAvatarSelected(int avatarID)
    {
        playerProperties["character"] = avatarID;

        PhotonNetwork.SetPlayerCustomProperties(playerProperties);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if(player ==  targetPlayer)
        {
            UpdatePlayerCharacter(player);
        }
    }

    private void UpdatePlayerCharacter(Player player)
    {
        if (player.CustomProperties.ContainsKey("character"))
        {
            playerAvatar.sprite = avatars[(int)player.CustomProperties["character"]];
            playerProperties["character"] = (int)player.CustomProperties["character"];
        }
        else
        {
            playerProperties["character"] = 0;
        }
    }
}
