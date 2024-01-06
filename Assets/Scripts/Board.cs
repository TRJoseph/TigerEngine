using System;
using System.Linq;
using System.Collections.Generic;


namespace Chess
{
    public static class Board
    {
        public struct InternalSquare
        {
            // this encoded value contains information about the piece itself (type, color, if it has moved)
            public int encodedPiece;

            // distances to edge of board from whichever tile the piece resides on
            public int DistanceNorth;
            public int DistanceSouth;
            public int DistanceEast;
            public int DistanceWest;

            public int DistanceNorthWest;
            public int DistanceNorthEast;
            public int DistanceSouthWest;
            public int DistanceSouthEast;
        }

        // values for decoding the encoded piece from binary (MCCTTT) M = Move Status flag bit, CC = Color bits, TTT = Piece Type bits
        private const int PieceTypeMask = 7;
        private const int PieceColorMask = 24;
        private const int PieceMoveStatusFlag = 32;
        //

        private const int BoardSize = 64;
        public static InternalSquare[] Squares = new InternalSquare[BoardSize];

        // this structure will hold a move that can be executed
        public struct LegalMove
        {
            // 'startSquare' and 'endSquare' holds the internal board start square and end square 
            public int startSquare;
            public int endSquare;

            // special move flags
            public bool? kingSideCastling;

            public bool? queenSideCastling;

            public bool? enPassant;
        }

        /* opting for a variable to control which list the algorithm places a move 
        into instead of passing the corresponding list as a parameter to every move 
        calculation function (opponent list or friendly list). */
        public static bool friendlyList = true;

        public static List<LegalMove> legalMoves = new List<LegalMove>();

        private enum Direction { North, South, East, West, NorthWest, NorthEast, SouthWest, SouthEast };

        // west, north, east, south
        private static readonly int[] cardinalOffsets = { -1, 8, 1, -8 };

        // northwest, northeast, southeast, southwest
        private static readonly int[] interCardinalOffsets = { 7, 9, -7, -9 };

        private static readonly int[] pawnOffsets = { 7, 8, 9 };

        private static int lastPawnDoubleMoveSquare = -1;

        private static readonly int[,] knightOffsets = { { 17, -15, 15, -17 }, { 10, -6, 6, -10 } };

        public enum GameState
        {
            Normal,
            AwaitingPromotion,
            Ended
        }

        public static GameState currentState = GameState.Normal;
        public static void UpdateInternalState(int originalXPosition, int originalYPosition, int newXPosition, int newYPosition)
        {
            int newPieceMove = newYPosition * 8 + newXPosition;
            // grab current piece and store it
            int currentPiece = Squares[originalYPosition * 8 + originalXPosition].encodedPiece;

            // when the piece has moved, set the 6th bit to 1
            currentPiece = currentPiece | PieceMoveStatusFlag;

            // removing the piece from its old position
            Squares[originalYPosition * 8 + originalXPosition].encodedPiece = Piece.Empty;

            // placing the piece in its new position
            Squares[newPieceMove].encodedPiece = currentPiece;

            // check for special move flags
            /* TODO, THIS MAY CAUSE ISSUES WHEN CREATING THE CHESS ENGINE PART. I have not given this enough thought quite yet
               although my intitial hunch is that it should be okay as the engine will likely work entirely on the back end 
               and never have to update the internal state from the front end, but there may be a need to have new internal update functions for
               the engine to take advantage of. */

            HandlePawnPromotionInternal(newPieceMove, currentPiece, newYPosition);

            // checks for a potential en passant capture
            HandleEnPassantInternal(currentPiece, originalXPosition, originalYPosition, newXPosition, newYPosition, newPieceMove);

            // checks for moves with the kingSideCastling flag 
            HandleKingSideCastleInternal(newPieceMove, newXPosition, newYPosition);

            // checks for moves with queenSideCastling flag
            HandleQueenSideCastleInternal(newPieceMove, newXPosition, newYPosition);
        }

        private static void HandlePawnPromotionInternal(int newPieceMove, int currentPiece, int newYPosition)
        {
            if (IsPawnPromotion(currentPiece, newYPosition))
            {

                if (BoardManager.CurrentTurn == BoardManager.ComputerSide)
                {
                    UpdatePromotedPawnEngine(newPieceMove);
                }
                else
                {
                    UIController.Instance.ShowPromotionDropdown(newPieceMove);
                }

            }
        }

        public static void UpdatePromotedPawnEngine(int newPieceMove)
        {
            int chosenPiece = Engine.EvaluateBestPromotionPiece();
            switch (chosenPiece)
            {
                case Piece.Queen:
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 5;
                    break;

                case Piece.Rook:
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 4;
                    break;

                case Piece.Bishop:
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 3;
                    break;

                case Piece.Knight:
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 2;
                    break;

                default:
                    // this should not happen
                    throw new Exception();
            }

            UIController.Instance.UpdateMoveStatusUIInformation();
        }

        public static void UpdatePromotedPawn(int newPieceMove)
        {
            // this line performs a logical and operation on the entire piece to remove the piece type from the three least-significant bits
            Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece & (PieceColorMask + PieceMoveStatusFlag);

            int newPieceXPos = newPieceMove % 8;
            int newPieceYPos = newPieceMove / 8;

            switch (UIController.Instance.promotionSelection)
            {
                case "Queen":
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 5;

                    break;

                case "Rook":
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 4;
                    break;

                case "Bishop":
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 3;
                    break;

                case "Knight":
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 2;
                    break;

                default:

                    // this should not happen
                    throw new Exception();
            }

            // update the front end sprite so the new correct piece is visible
            PieceMovementManager.UpdateFrontEndPromotion(Squares[newPieceMove].encodedPiece, newPieceXPos, newPieceYPos);

            // once the pawn has been swapped internally
            ClearListMoves();

            legalMoves = AfterMove();

            UIController.Instance.UpdateMoveStatusUIInformation();

        }

        private static bool IsPawnPromotion(int currentPiece, int newYPosition)
        {
            if ((currentPiece & PieceTypeMask) == Piece.Pawn)
            {
                if ((currentPiece & PieceColorMask) == Piece.White && newYPosition == 7)
                {
                    return true;
                }

                if ((currentPiece & PieceColorMask) == Piece.Black && newYPosition == 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static void HandleEnPassantInternal(int currentPiece, int originalXPosition, int originalYPosition, int newXPosition, int newYPosition, int newPieceMove)
        {
            if ((currentPiece & PieceTypeMask) == Piece.Pawn && Math.Abs(originalYPosition * 8 + originalXPosition - newPieceMove) == 16)
            {
                lastPawnDoubleMoveSquare = newPieceMove;
            }
            else
            {
                lastPawnDoubleMoveSquare = -1;
            }

            if (legalMoves.Any(move => move.endSquare == newPieceMove && move.enPassant == true))
            {
                if ((currentPiece & PieceColorMask) == Piece.White)
                {
                    Squares[newPieceMove - 8].encodedPiece = Piece.Empty;
                    //blackPieces.Remove(newPieceMove - 8);
                    //PieceMovementManager.UpdateFrontEndSpecialMove(newXPosition, newYPosition - 1, false, false, true);
                }
                else if ((currentPiece & PieceColorMask) == Piece.Black)
                {
                    Squares[newPieceMove + 8].encodedPiece = Piece.Empty;
                    //whitePieces.Remove(newPieceMove + 8);
                    //PieceMovementManager.UpdateFrontEndSpecialMove(newXPosition, newYPosition + 1, false, false, true);
                }

            }
        }

        private static void HandleKingSideCastleInternal(int newPieceMove, int newXPosition, int newYPosition)
        {
            if (legalMoves.Any(move => move.endSquare == newPieceMove && move.kingSideCastling == true))
            {

                // grab rook in the corner on kingside
                int cornerRook = Squares[newPieceMove + 1].encodedPiece;

                Squares[newPieceMove + 1].encodedPiece = Piece.Empty;

                // update move and piece move status
                Squares[newPieceMove - 1].encodedPiece = cornerRook | PieceMoveStatusFlag;

                // updates front end board representation, moves king to new position and moves kingside rook to the square left of new king position
                //PieceMovementManager.UpdateFrontEndSpecialMove(newXPosition + 1, newYPosition, true, false, false);
            }
        }

        private static void HandleQueenSideCastleInternal(int newPieceMove, int newXPosition, int newYPosition)
        {
            if (legalMoves.Any(move => move.endSquare == newPieceMove && move.queenSideCastling == true))
            {
                // grab rook in the corner on queenside
                int cornerRook = Squares[newPieceMove - 2].encodedPiece;

                Squares[newPieceMove - 2].encodedPiece = Piece.Empty;

                // update move and piece move status
                Squares[newPieceMove + 1].encodedPiece = cornerRook | PieceMoveStatusFlag;

                // updates front end board representation, moves king to new position and moves queenside rook to the square right of new king position
                //PieceMovementManager.UpdateFrontEndSpecialMove(newXPosition - 2, newYPosition, false, true, false);
            }

        }

        private static LegalMove AddLegalMove(int startSquare, int endSquare, bool? kingSideCastling, bool? queenSideCastling, bool? enPassant)
        {
            return
                new LegalMove
                {
                    startSquare = startSquare,
                    endSquare = endSquare,
                    kingSideCastling = kingSideCastling,
                    queenSideCastling = queenSideCastling,
                    enPassant = enPassant

                };
        }

        private static List<LegalMove> CheckWhitePawnCaptures(int startSquare)
        {
            List<LegalMove> pawnCaptureMoves = new List<LegalMove>();
            int northWestSquare = startSquare + pawnOffsets[0];
            int northEastSquare = startSquare + pawnOffsets[2];

            // square one square northWest, checking if an enemy piece is there available for capture
            if (Squares[startSquare].DistanceNorthWest >= 1)
            {
                if (IsOpponentPiece(northWestSquare, Piece.Black))
                {
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, northWestSquare, false, false, false));
                }
            }

            // square one square northEast, checking if an enemy piece is there available for capture

            if (Squares[startSquare].DistanceNorthEast >= 1)
            {
                if (IsOpponentPiece(northEastSquare, Piece.Black))
                {
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, northEastSquare, false, false, false));
                }
            }

            // for en passant
            if (lastPawnDoubleMoveSquare != -1)
            {
                if ((lastPawnDoubleMoveSquare + 8 - startSquare) == pawnOffsets[2] && Squares[startSquare].DistanceEast >= 1)
                {
                    // one square above the black pawn that just moved
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, lastPawnDoubleMoveSquare + 8, false, false, true));
                }

                if ((lastPawnDoubleMoveSquare + 8 - startSquare) == pawnOffsets[0] && Squares[startSquare].DistanceWest >= 1)
                {
                    // one square above the black pawn that just moved
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, lastPawnDoubleMoveSquare + 8, false, false, true));
                }
            }

            return pawnCaptureMoves;
        }

        private static List<LegalMove> CheckBlackPawnCaptures(int startSquare)
        {
            List<LegalMove> pawnCaptureMoves = new List<LegalMove>();
            int southEastSquare = startSquare - pawnOffsets[0];
            int southWestSquare = startSquare - pawnOffsets[2];

            // square one square southEast, checking if an enemy piece is there available for capture
            if (Squares[startSquare].DistanceSouthEast >= 1)
            {
                if (IsOpponentPiece(southEastSquare, Piece.White))
                {
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, southEastSquare, false, false, false));
                }
            }
            // square one square southWest, checking if an enemy piece is there available for capture

            if (Squares[startSquare].DistanceSouthWest >= 1)
            {
                if (IsOpponentPiece(southWestSquare, Piece.White))
                {
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, southWestSquare, false, false, false));
                }
            }

            // for en passant
            if (lastPawnDoubleMoveSquare != -1)
            {
                if (startSquare - (lastPawnDoubleMoveSquare - 8) == pawnOffsets[2] && Squares[startSquare].DistanceWest >= 1)
                {
                    // one square below the white pawn that just moved
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, lastPawnDoubleMoveSquare - 8, false, false, true));
                }

                if (startSquare - (lastPawnDoubleMoveSquare - 8) == pawnOffsets[0] && Squares[startSquare].DistanceEast >= 1)
                {
                    // one square below the white pawn that just moved
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, lastPawnDoubleMoveSquare - 8, false, false, true));
                }
            }

            return pawnCaptureMoves;
        }

        private static bool IsOpponentPiece(int square, int opponentColor)
        {
            return Squares[square].encodedPiece != Piece.Empty && (Squares[square].encodedPiece & PieceColorMask) == opponentColor;
        }

        private static List<LegalMove> CalculatePawnMoves(int startSquare, int decodedColor, int decodedPieceStatus)
        {
            List<LegalMove> pawnMoves = new List<LegalMove>();

            // if white pawn
            if (decodedColor == Piece.White)
            {
                if (decodedPieceStatus == PieceMoveStatusFlag)
                {
                    // if pawn has moved, legal moves is only a one square advance
                    // checks if the square in front of the pawn is empty
                    if (Squares[startSquare + pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        pawnMoves.Add(AddLegalMove(startSquare, startSquare + pawnOffsets[1], false, false, false));
                    }

                    pawnMoves.AddRange(CheckWhitePawnCaptures(startSquare));

                }
                else
                {
                    // if pawn has not moved, legal moves is a two square advance
                    if (Squares[startSquare + pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        pawnMoves.Add(AddLegalMove(startSquare, startSquare + pawnOffsets[1], false, false, false));

                        if (Squares[startSquare + (2 * pawnOffsets[1])].encodedPiece == Piece.Empty)
                        {
                            pawnMoves.Add(AddLegalMove(startSquare, startSquare + (2 * pawnOffsets[1]), false, false, false));
                        }
                    }

                    pawnMoves.AddRange(CheckWhitePawnCaptures(startSquare));
                }
            }
            else
            {
                // if black pawn
                if (decodedPieceStatus == PieceMoveStatusFlag)
                {
                    // if pawn has moved, legal moves is only a one square advance
                    if (Squares[startSquare - pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        pawnMoves.Add(AddLegalMove(startSquare, startSquare - pawnOffsets[1], false, false, false));
                    }

                    pawnMoves.AddRange(CheckBlackPawnCaptures(startSquare));

                }
                else
                {
                    // if pawn has not moved, legal moves is a two square advance
                    if (Squares[startSquare - pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        pawnMoves.Add(AddLegalMove(startSquare, startSquare - pawnOffsets[1], false, false, false));

                        if (Squares[startSquare - (2 * pawnOffsets[1])].encodedPiece == Piece.Empty)
                        {
                            pawnMoves.Add(AddLegalMove(startSquare, startSquare - (2 * pawnOffsets[1]), false, false, false));
                        }
                    }

                    pawnMoves.AddRange(CheckBlackPawnCaptures(startSquare));
                }
            }

            return pawnMoves;
        }


        private static List<LegalMove> CalculateKnightMovesHelper(int[] xOffsets, int[] yOffsets, int knightOffsetIndex, int startSquare, int decodedColor)
        {
            List<LegalMove> knightJumps = new List<LegalMove>();
            for (int i = 0; i < 2; i++) // Loop for x
            {
                int xOffset = xOffsets[i];
                if (xOffset <= Squares[startSquare].DistanceEast && xOffset >= -Squares[startSquare].DistanceWest)
                {
                    for (int j = 0; j < 2; j++) // Loop for y
                    {
                        int yOffset = yOffsets[j];
                        if (yOffset <= Squares[startSquare].DistanceNorth && yOffset >= -Squares[startSquare].DistanceSouth)
                        {

                            int offsetIndex = (i * 2 + j); // Calculate the offset index based on i and j

                            if (Squares[startSquare + knightOffsets[knightOffsetIndex, offsetIndex]].encodedPiece != Piece.Empty)
                            {
                                if (decodedColor == (Squares[startSquare + knightOffsets[knightOffsetIndex, offsetIndex]].encodedPiece & PieceColorMask))
                                {
                                    // same color piece
                                    continue;
                                }
                            }

                            knightJumps.Add(AddLegalMove(startSquare, startSquare + knightOffsets[knightOffsetIndex, offsetIndex], false, false, false));
                        }
                    }
                }
            }
            return knightJumps;
        }
        private static List<LegalMove> CalculateKnightMoves(int startSquare, int decodedColor)
        {
            List<LegalMove> knightMoves = new List<LegalMove>();

            // {(𝑥±1,𝑦±2}∪{𝑥±2,y±1} represents available knight moves

            int[] xOffsets = { 1, -1 };
            int[] yOffsets = { 2, -2 };

            knightMoves.AddRange(CalculateKnightMovesHelper(xOffsets, yOffsets, 0, startSquare, decodedColor));
            knightMoves.AddRange(CalculateKnightMovesHelper(yOffsets, xOffsets, 1, startSquare, decodedColor));

            return knightMoves;
        }

        private static List<LegalMove> CalculateSlidingPiecesMoves(int piece, Direction direction, int startSquare, int decodedColor)
        {
            List<LegalMove> slidingPiecesMoves = new();

            // direction offset
            int dOffset = 0, distance = 0;

            // limits search algorithm to one square if the sliding piece is the king
            int kingLimits = piece == Piece.King ? 1 : int.MaxValue;

            switch (direction)
            {
                case Direction.North:
                    dOffset = cardinalOffsets[1];
                    distance = Squares[startSquare].DistanceNorth;
                    break;
                case Direction.South:
                    dOffset = cardinalOffsets[3];
                    distance = Squares[startSquare].DistanceSouth;
                    break;
                case Direction.East:
                    dOffset = cardinalOffsets[2];
                    distance = Squares[startSquare].DistanceEast;
                    break;
                case Direction.West:
                    dOffset = cardinalOffsets[0];
                    distance = Squares[startSquare].DistanceWest;
                    break;
                case Direction.NorthWest:
                    dOffset = interCardinalOffsets[0];
                    distance = Squares[startSquare].DistanceNorthWest;
                    break;
                case Direction.NorthEast:
                    dOffset = interCardinalOffsets[1];
                    distance = Squares[startSquare].DistanceNorthEast;
                    break;
                case Direction.SouthWest:
                    dOffset = interCardinalOffsets[3];
                    distance = Squares[startSquare].DistanceSouthWest;
                    break;
                case Direction.SouthEast:
                    dOffset = interCardinalOffsets[2];
                    distance = Squares[startSquare].DistanceSouthEast;
                    break;
            }

            for (int i = 1, offset = dOffset; i <= distance && i <= kingLimits; i++, offset += dOffset)
            {
                //if a square is occupied by a piece of the same color, stop the loop
                //by a different color, add the move and stop the loop(capturing the piece)
                if (Squares[startSquare + offset].encodedPiece != Piece.Empty)
                {
                    if (decodedColor == (Squares[startSquare + offset].encodedPiece & PieceColorMask))
                    {
                        break;
                    }
                    else
                    {
                        slidingPiecesMoves.Add(AddLegalMove(startSquare, startSquare + offset, false, false, false));
                        break;
                    }
                }
                slidingPiecesMoves.Add(AddLegalMove(startSquare, startSquare + offset, false, false, false));
            }

            return slidingPiecesMoves;
        }

        private static List<LegalMove> CalculateRookMoves(int startSquare, int decodedColor)
        {
            List<LegalMove> rookMoves = new List<LegalMove>();
            rookMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Rook, Direction.North, startSquare, decodedColor));
            rookMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Rook, Direction.South, startSquare, decodedColor));
            rookMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Rook, Direction.East, startSquare, decodedColor));
            rookMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Rook, Direction.West, startSquare, decodedColor));

            return rookMoves;

        }

        private static List<LegalMove> CalculateBishopMoves(int startSquare, int decodedColor)
        {
            List<LegalMove> bishopMoves = new List<LegalMove>();
            bishopMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Bishop, Direction.NorthWest, startSquare, decodedColor));
            bishopMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Bishop, Direction.NorthEast, startSquare, decodedColor));
            bishopMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Bishop, Direction.SouthWest, startSquare, decodedColor));
            bishopMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Bishop, Direction.SouthEast, startSquare, decodedColor));

            return bishopMoves;
        }

        private static List<LegalMove> CalculateKingMoves(int startSquare, int decodedColor)
        {
            List<LegalMove> kingMoves = new();
            foreach (Direction direction in Enum.GetValues(typeof(Direction)))
            {
                kingMoves.AddRange(CalculateSlidingPiecesMoves(Piece.King, direction, startSquare, decodedColor));
            }
            return kingMoves;
        }

        /// <summary>
        ///  This method checks if king-side castling is even possible in the current position.
        ///  Castling requires a rook and king that have both not moved to have no pieces in between them in order to castle
        ///  At this point the opponent responses are not necessarily available (for the final rule: king cannot leave a square, 
        ///  traverse across a square, or land on a square that is under attack) so this function essentially adds a potential castling move.
        ///  The move added here will always be pseudo-legal.
        /// </summary>
        /// <param name="startSquare"></param>
        /// <returns></returns>
        private static LegalMove? CheckKingSideCastle(int startSquare)
        {
            // first checks if a rook piece is present in the corner and a king is on the e file
            // (this can be useful in non-standard FEN string positions)
            if ((Squares[startSquare].encodedPiece & PieceTypeMask) != Piece.King || ((Squares[startSquare + 3].encodedPiece & PieceTypeMask) != Piece.Rook))
            {
                return null;
            }
            // decodes piece move status; if king or rook on kingside has moved, castling not allowed
            if ((Squares[startSquare].encodedPiece & PieceMoveStatusFlag) == PieceMoveStatusFlag || ((Squares[startSquare + 3].encodedPiece & PieceMoveStatusFlag) == PieceMoveStatusFlag))
            {
                return null;
            }

            // check for empty squares between rook and king
            for (int i = startSquare + 1; i < (startSquare + 3); i++)
            {
                if (Squares[i].encodedPiece != Piece.Empty)
                {
                    return null;
                }

            }

            /* final rule (king cannot leave a square, traverse across a square, or land on a square that is under attack) is verified
            in the GenerateLegalMoves method.
            */


            // this adds a legal move with the kingSideCastling flag set to true
            return AddLegalMove(startSquare, startSquare + 2, true, false, false);

        }

        /// <summary>
        ///  This method checks if queen-side castling is even possible in the current position.
        ///  Refer to 'CheckKingSideCastle' in order to read more about what these methods do.
        /// </summary>
        /// <param name="startSquare"></param>
        /// <returns></returns>
        private static LegalMove? CheckQueenSideCastle(int startSquare)
        {
            // first checks if a rook piece is present in the corner and a king is on the e file
            // (this can be useful in non-standard FEN string positions)
            if ((Squares[startSquare].encodedPiece & PieceTypeMask) != Piece.King || ((Squares[startSquare - 4].encodedPiece & PieceTypeMask) != Piece.Rook))
            {
                return null;
            }
            // decodes piece move status; if king or rook on queenside has moved, castling not allowed
            if ((Squares[startSquare].encodedPiece & PieceMoveStatusFlag) == PieceMoveStatusFlag || ((Squares[startSquare - 4].encodedPiece & PieceMoveStatusFlag) == PieceMoveStatusFlag))
            {
                return null;
            }

            // check for empty squares between rook and king
            for (int i = startSquare - 1; i > (startSquare - 4); i--)
            {
                if (Squares[i].encodedPiece != Piece.Empty)
                {
                    return null;
                }

            }

            // this adds a legal move with the queenSideCastling flag set to true
            return AddLegalMove(startSquare, startSquare - 2, false, true, false);

        }
        private static int FindKingPosition(bool whiteToMove)
        {
            int color;
            if (whiteToMove)
            {
                color = Piece.White;
            }
            else
            {
                color = Piece.Black;
            }

            for (int i = 0; i < BoardSize; i++)
            {
                if ((Squares[i].encodedPiece & PieceTypeMask) == Piece.King)
                {
                    if ((Squares[i].encodedPiece & PieceColorMask) == color)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        public static void ClearListMoves()
        {
            legalMoves.Clear();
        }

        private static List<int> FindAllPieceSquares(bool whiteToMove)
        {
            List<int> squareList = new();
            int color;
            if (whiteToMove)
            {
                color = Piece.White;
            }
            else
            {
                color = Piece.Black;
            }

            for (int i = 0; i < BoardSize; i++)
            {
                if ((Squares[i].encodedPiece & PieceColorMask) == color)
                {
                    squareList.Add(i);
                }
            }
            return squareList;

        }


        public static List<LegalMove> AfterMove()
        {
            // calculates all legal moves in a given position
            legalMoves = GenerateLegalMoves();

            SwapTurn();

            return legalMoves;

        }

        private static void SwapTurn()
        {
            BoardManager.CurrentTurn = BoardManager.CurrentTurn == BoardManager.Sides.White
                                    ? BoardManager.Sides.Black
                                    : BoardManager.Sides.White;
        }

        private static bool IsKingSideCastleLegal(int startSquare, List<LegalMove> opponentMoves)
        {
            int kingFinalSquare = startSquare + 2; // For kingside castling, king ends two squares to the right
            int kingPathSquare = startSquare + 1; // The square king passes through

            return !opponentMoves.Any(move => move.endSquare == kingFinalSquare || move.endSquare == kingPathSquare || move.endSquare == startSquare);
        }

        private static bool IsQueenSideCastleLegal(int startSquare, List<LegalMove> opponentMoves)
        {
            int kingFinalSquare = startSquare - 3; // For kingside castling, king ends two squares to the right
            int kingPathSquare = startSquare - 2; // The square king passes through
            int kingPathSquare2 = startSquare - 1;

            return !opponentMoves.Any(move => move.endSquare == kingFinalSquare || move.endSquare == kingPathSquare || move.endSquare == kingPathSquare2 || move.endSquare == startSquare);
        }

        public static List<LegalMove> GenerateLegalMoves()
        {
            // calculate all pseudo legal moves for the friendly side (whoevers turn it is)
            List<LegalMove> pseudoLegalMoves = CalculateAllMoves(BoardManager.whiteToMove);

            List<LegalMove> legalMoves = new List<LegalMove>();

            int originalkingSquare = FindKingPosition(BoardManager.whiteToMove);

            foreach (LegalMove move in pseudoLegalMoves)
            {
                int rememberedPiece = ExecuteMove(move);

                // replace this with current king square
                int currentKingSquare = FindKingPosition(BoardManager.whiteToMove);

                List<LegalMove> opponentResponses = CalculateAllMoves(!BoardManager.whiteToMove);

                // Special handling for castling moves
                if (move.kingSideCastling == true)
                {
                    if (IsKingSideCastleLegal(originalkingSquare, opponentResponses))
                    {
                        legalMoves.Add(move);
                    }
                }
                else if (move.queenSideCastling == true)
                {
                    if (IsQueenSideCastleLegal(originalkingSquare, opponentResponses))
                    {
                        legalMoves.Add(move);
                    }
                }
                // handle other moves
                else if (!opponentResponses.Any(response => response.endSquare == currentKingSquare))
                {
                    // if the king is not under attack after the move, add it to legal moves
                    legalMoves.Add(move);
                }
                // Undo the move for the next iteration
                UndoMove(move, rememberedPiece);
            }
            return legalMoves;
        }

        private static int ExecuteMove(LegalMove move)
        {
            int startSquareIndex = move.startSquare;
            int endSquareIndex = move.endSquare;
            int movingPiece = Squares[startSquareIndex].encodedPiece;

            // remember potential captured piece (this is needed when the move is un-done and the position returns to its previous state)
            int rememberedPiece = Squares[endSquareIndex].encodedPiece;

            // Move the piece
            Squares[endSquareIndex].encodedPiece = movingPiece;
            Squares[startSquareIndex].encodedPiece = Piece.Empty;
            return rememberedPiece;
        }

        private static void UndoMove(LegalMove move, int rememberedPiece)
        {
            int startSquareIndex = move.startSquare;
            int endSquareIndex = move.endSquare;
            int movingPiece = Squares[endSquareIndex].encodedPiece;
            int capturedPiece = rememberedPiece;

            // Revert the move
            Squares[startSquareIndex].encodedPiece = movingPiece;
            Squares[endSquareIndex].encodedPiece = capturedPiece;
        }

        public static List<LegalMove> CalculateAllMoves(bool friendlyMove)
        {
            List<int> whichPieces = FindAllPieceSquares(friendlyMove);
            List<LegalMove> moveList = new List<LegalMove>();

            foreach (int square in whichPieces)
            {
                moveList.AddRange(CalculateLegalMoves(square, Squares[square].encodedPiece));
            }
            return moveList;
        }


        public static List<LegalMove> CalculateLegalMoves(int startSquare, int internalGamePiece)
        {
            int decodedPiece = internalGamePiece & PieceTypeMask;
            int decodedColor = internalGamePiece & PieceColorMask;

            // if = PieceMoveStatusFlag (32), piece has moved, if 0, piece has not moved
            int decodedPieceStatus = internalGamePiece & PieceMoveStatusFlag;

            List<LegalMove> pieceMoves = new List<LegalMove>();

            switch (decodedPiece)
            {
                case Piece.Pawn:
                    pieceMoves.AddRange(CalculatePawnMoves(startSquare, decodedColor, decodedPieceStatus));
                    break;
                case Piece.Knight:
                    pieceMoves.AddRange(CalculateKnightMoves(startSquare, decodedColor));
                    break;
                case Piece.Rook:
                    pieceMoves.AddRange(CalculateRookMoves(startSquare, decodedColor));
                    break;
                case Piece.Bishop:
                    pieceMoves.AddRange(CalculateBishopMoves(startSquare, decodedColor));
                    break;
                case Piece.Queen:
                    // queen contains both movesets of a bishop and a rook
                    pieceMoves.AddRange(CalculateRookMoves(startSquare, decodedColor));
                    pieceMoves.AddRange(CalculateBishopMoves(startSquare, decodedColor));
                    break;
                case Piece.King:
                    pieceMoves.AddRange(CalculateKingMoves(startSquare, decodedColor));

                    // check for castling ability
                    // makes sure king is on e file
                    if (startSquare == 4 || startSquare == 60)
                    {
                        var kingSideCastleMove = CheckKingSideCastle(startSquare);

                        if (kingSideCastleMove != null)
                        {

                            pieceMoves.Add(kingSideCastleMove.Value);
                        }

                        var queenSideCastleMove = CheckQueenSideCastle(startSquare);

                        if (queenSideCastleMove != null)
                        {
                            pieceMoves.Add(queenSideCastleMove.Value);
                        }
                    }
                    break;
                default:
                    return pieceMoves;
            }
            return pieceMoves;
        }
    }
}
