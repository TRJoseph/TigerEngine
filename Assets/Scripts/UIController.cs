using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Chess
{
    public class UIController : MonoBehaviour
    {

        public static UIController Instance;
        public TextMeshProUGUI WhichPlayerMoveText;

        public TMP_Dropdown promotionDropdown;

        public List<Sprite> whitePieceSprites; // Sprites for the white promotion pieces (queen, rook, bishop, knight)
        public List<Sprite> blackPieceSprites; // Sprites for the black promotion pieces


        void Start()
        {
            InitializePromotionDropdown();
            promotionDropdown.gameObject.SetActive(false);

        }

        private void InitializePromotionDropdown()
        {
            promotionDropdown.ClearOptions();

            // should be white to move, okay to assume white piece sprites when game begins
            foreach (var sprite in whitePieceSprites)
            {
                var option = new TMP_Dropdown.OptionData { image = sprite };
                promotionDropdown.options.Add(option);
            }
            promotionDropdown.Show();
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }


        public void UpdateMoveStatusUIInformation()
        {
            promotionDropdown.ClearOptions();
            if (GridManager.whiteToMove)
            {
                foreach (var sprite in whitePieceSprites)
                {
                    var option = new TMP_Dropdown.OptionData { image = sprite };
                    promotionDropdown.options.Add(option);
                }
                WhichPlayerMoveText.text = "White to move";
            }
            else
            {
                foreach (var sprite in blackPieceSprites)
                {
                    var option = new TMP_Dropdown.OptionData { image = sprite };
                    promotionDropdown.options.Add(option);
                }
                WhichPlayerMoveText.text = "Black to move";
            }

        }

    }
}

