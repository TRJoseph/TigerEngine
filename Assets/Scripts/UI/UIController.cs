using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Net.Security;
using static Chess.Board;
using static Chess.PositionInformation;
using System.Linq;

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

        public delegate void PromotionSelectedHandler(Move move);
        public static event PromotionSelectedHandler OnPromotionSelected;

        List<Move> currentPromotionMoves = new();

        void Start()
        {
            promotionButtons[0].onClick.AddListener(() => HandlePromotion(GetCurrentPromotionMove(PromotionFlags.PromoteToQueenFlag)));
            promotionButtons[1].onClick.AddListener(() => HandlePromotion(GetCurrentPromotionMove(PromotionFlags.PromoteToRookFlag)));
            promotionButtons[2].onClick.AddListener(() => HandlePromotion(GetCurrentPromotionMove(PromotionFlags.PromoteToBishopFlag)));
            promotionButtons[3].onClick.AddListener(() => HandlePromotion(GetCurrentPromotionMove(PromotionFlags.PromoteToKnightFlag)));

            // Subscribe to the promotion selected event
            OnPromotionSelected += HandlePromotionSelected;
        }

        private Move GetCurrentPromotionMove(PromotionFlags flag)
        {
            return currentPromotionMoves.Single(move => move.promotionFlag == flag);
        }
        private void HandlePromotionSelected(Move move)
        {
            MovementManager.DoMove(move);
        }

        // This method is called when the user selects a promotion option
        public void PromotionSelected(Move move)
        {
            // Hide the promotion panel
            PromotionPanel.gameObject.SetActive(false);

            // Fire the promotion selected event
            OnPromotionSelected?.Invoke(move);
        }


        private void HandlePromotion(Move move)
        {
            PromotionSelected(move);
        }

        public void ShowPromotionDropdown(ulong toSquare, List<Move> savedPromotionMoves)
        {
            currentStatus = GameStatus.AwaitingPromotion;

            // this is responsible for updating the dropdown list with the correct corresponding promotion move choice
            currentPromotionMoves = savedPromotionMoves;

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

