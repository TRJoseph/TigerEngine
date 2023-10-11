using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Chess
{
    public class UIController : MonoBehaviour
    {
        public TextMeshProUGUI WhichPlayerMoveText;

        public void UpdateMoveStatusText(bool whiteToMove)
        {
            if (whiteToMove)
            {
                WhichPlayerMoveText.text = "White to move";
            }
            else
            {
                WhichPlayerMoveText.text = "Black to move";
            }

        }

    }
}

