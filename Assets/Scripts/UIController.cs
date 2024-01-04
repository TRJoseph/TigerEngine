using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Net.Security;

namespace Chess
{
    public class UIController : MonoBehaviour
    {

        public static UIController Instance;
        public TextMeshProUGUI WhichPlayerMoveText;

        public GameObject PromotionPanel;
        public Image[] pieceButtons; // References to the Image components of the buttons
        public Button[] promotionButtons;

        public string promotionSelection;

        public bool selectionMade = false;

        public List<Sprite> whitePieceSprites; // Sprites for the white promotion pieces (queen, rook, bishop, knight)
        public List<Sprite> blackPieceSprites; // Sprites for the black promotion pieces


        void Start()
        {
            promotionButtons[0].onClick.AddListener(() => HandlePromotion("Queen"));
            promotionButtons[1].onClick.AddListener(() => HandlePromotion("Rook"));
            promotionButtons[2].onClick.AddListener(() => HandlePromotion("Bishop"));
            promotionButtons[3].onClick.AddListener(() => HandlePromotion("Knight"));
        }

        private void HandlePromotion(string button)
        {
            switch (button)
            {
                case "Queen":
                    promotionSelection = "Queen";
                    break;
                case "Rook":
                    promotionSelection = "Rook";
                    break;
                case "Bishop":
                    promotionSelection = "Bishop";
                    break;
                case "Knight":
                    promotionSelection = "Knight";
                    break;
                default:
                    promotionSelection = null;
                    break;
            }
            selectionMade = true;
        }

        public void ShowPromotionDropdown(int newPieceMove)
        {
            Board.currentState = Board.GameState.AwaitingPromotion;
            PromotionPanel.gameObject.SetActive(true);

            PromotionPanel.gameObject.transform.position = new Vector3((newPieceMove % 8) - 1, (newPieceMove / 8) - 2, -2);

            List<Sprite> pieceSprites = BoardManager.whiteToMove ? whitePieceSprites : blackPieceSprites;
            for (int i = 0; i < pieceButtons.Length; i++)
            {
                pieceButtons[i].sprite = pieceSprites[i];
            }

            // this begins the coroutine that essential 'waits' for the user to select a new piece before allowing the game to continue
            StartCoroutine(WaitForSelection(newPieceMove, PromotionPanel));
        }

        /* This method is important as it allows for the program to wait for the user to make a selection. During this time the gamestate is
        locked on "Awaiting Promotion". This prevents the main thread from attempting to calculate pawn moves off the edge of the board.
        Once the selection is made, the Board is set back to its normal game state and the internal board is updated with the new piece type information
        */
        IEnumerator WaitForSelection(int newPieceMove, GameObject PromotionPanel)
        {
            selectionMade = false;

            while (!selectionMade)
            {
                yield return null; // Wait for the next frame
            }

            // hide the promotion panel gameobject
            PromotionPanel.gameObject.SetActive(false);

            // Updates the game state
            Board.currentState = Board.GameState.Normal;

            // Now update pawn to new selected piece and recalculate moves
            Board.UpdatePromotedPawn(newPieceMove);
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
            if (BoardManager.whiteToMove)
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

