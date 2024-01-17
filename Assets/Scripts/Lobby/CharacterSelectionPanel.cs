using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectionPanel : MonoBehaviour
{
    [SerializeField]
    private Canvas canvas;
    private static Canvas c;
    private static PlayerItem localPlayerItem;

    private void Start()
    {
        c = canvas;
        c.enabled = false;
    }
    public static void ChangeCanvasStatus(PlayerItem playerItem)
    {
        c.enabled = !c.enabled;
        localPlayerItem = playerItem;
    }

    public void OnCharacterClicked(int characterID)
    {
        Debug.Log(characterID);
        localPlayerItem.OnPlayerAvatarSelected(characterID);
        ChangeCanvasStatus(localPlayerItem);
    }
}
