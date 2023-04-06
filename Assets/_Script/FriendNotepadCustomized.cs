using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FriendNotepadCustomized : MonoBehaviour
{

    public TMP_InputField _input;

    // Start is called before the first frame update
   public void OnClickedReset()
    {
        _input.text = Config.Get().ResetConfig();

    }
}
