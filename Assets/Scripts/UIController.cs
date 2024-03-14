using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Net.Security;
using static Chess.Board;
using static Chess.PositionInformation;

namespace Chess
{
    public class UIController : MonoBehaviour
    {
        public static UIController Instance;
        public TextMeshProUGUI WhichPlayerMoveText;

        public PieceMovementManager MovementManager;

        public GameObject PromotionPanel;
        public Image[] pieceButtons; // References to the Image components of the buttons
        public Button[] promotionButtons;

        public bool selectionMade = false;

        public List<Sprite> whitePieceSprites; // Sprites for the white promotion pieces (queen, rook, bishop, knight)
        public List<Sprite> blackPieceSprites; // Sprites for the black promotion pieces

        public delegate void PromotionSelectedHandler(PromotionFlags selectedFlag);
        public static event PromotionSelectedHandler OnPromotionSelected;


        void Start()
        {
            promotionButtons[0].onClick.AddListener(() => HandlePromotion("Queen"));
            promotionButtons[1].onClick.AddListener(() => HandlePromotion("Rook"));
            promotionButtons[2].onClick.AddListener(() => HandlePromotion("Bishop"));
            promotionButtons[3].onClick.AddListener(() => HandlePromotion("Knight"));

            // Subscribe to the promotion selected event
            OnPromotionSelected += HandlePromotionSelected;
        }

        private void HandlePromotionSelected(PromotionFlags selectedFlag)
        {
            // execute move with new flag 
            SavedMoveForPromotion.promotionFlag = selectedFlag;
            MovementManager.DoMove(SavedMoveForPromotion);
        }

        // This method is called when the user selects a promotion option
        public void PromotionSelected(PromotionFlags selectedFlag)
        {
            // Hide the promotion panel
            PromotionPanel.gameObject.SetActive(false);

            // Fire the promotion selected event
            OnPromotionSelected?.Invoke(selectedFlag);
        }


        private void HandlePromotion(string button)
        {
            switch (button)
            {
                case "Queen":
                    PromotionSelected(PromotionFlags.PromoteToQueenFlag);
                    break;
                case "Rook":
                    PromotionSelected(PromotionFlags.PromoteToRookFlag);
                    break;
                case "Bishop":
                    PromotionSelected(PromotionFlags.PromoteToBishopFlag);
                    break;
                case "Knight":
                    PromotionSelected(PromotionFlags.PromoteToKnightFlag);
                    break;
                default:
                    PromotionSelected(PromotionFlags.None);
                    break;
            }
        }

        public void ShowPromotionDropdown(ulong toSquare)
        {
            currentStatus = GameStatus.AwaitingPromotion;
            PromotionPanel.gameObject.SetActive(true);

            PromotionPanel.gameObject.transform.position = new Vector3(((int)Math.Log(toSquare, 2) % 8) - 1, ((int)Math.Log(toSquare, 2) / 8) - 2, -2);

            List<Sprite> pieceSprites = whiteToMove ? whitePieceSprites : blackPieceSprites;
            for (int i = 0; i < pieceButtons.Length; i++)
            {
                pieceButtons[i].sprite = pieceSprites[i];
            }

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

