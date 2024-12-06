using System;
using System.Linq;
using System.Collections.Generic;
using static Chess.Board;
using static Chess.PositionInformation;


namespace Chess
{

    public static class MoveGen
    {
        public enum SpecialMove
        {
            None = 0,
            KingSideCastleMove = 1,
            QueenSideCastleMove = 2,
            EnPassant = 3,
            TwoSquarePawnMove = 4,
        }

        public enum PromotionFlags
        {
            None = 0,
            PromoteToQueenFlag = 1,
            PromoteToRookFlag = 2,
            PromoteToBishopFlag = 3,
            PromoteToKnightFlag = 4
        }

        // this structure will hold a move that can be executed
        public struct Move
        {
            // 'startSquare' and 'endSquare' holds the internal board start square and end square 
            public ulong fromSquare;

            public ulong toSquare;

            public int movedPiece;

            // special move flags
            public SpecialMove specialMove;

            // captured piece type is ChessBoard.None (-1) if no piece was captured
            public int capturedPieceType;

            public bool IsPawnPromotion;

            public PromotionFlags? promotionFlag;

            public readonly bool IsDefault()
            {
                return fromSquare == 0 && movedPiece == 0 && toSquare == 0 && specialMove == SpecialMove.None;
            }
        }

        public static bool MatchingMove(Move move1, Move move2)
        {
            return move1.toSquare == move2.toSquare && move1.fromSquare == move2.fromSquare;
        }

        public static Move[] legalMoves = new Move[256];

        // this holds the current move index for the move list actively being computed.
        // Allows for more efficient move list computations using statically allocated memory on the stack
        public static int currentMoveIndex;

        // legalMoveCount is a cut down value of the currentMoveIndex representing the actual count of moves within the list
        public static int legalMoveCount;


        private static Move AddLegalMove(ulong startSquare, ulong endSquare, int movedPiece, int capturedPieceType = ChessBoard.None, SpecialMove specialMove = SpecialMove.None, bool isPawnPromotion = false, PromotionFlags promotionFlag = PromotionFlags.None)
        {
            return
                new Move
                {
                    fromSquare = startSquare,
                    toSquare = endSquare,
                    movedPiece = movedPiece,
                    specialMove = specialMove,
                    capturedPieceType=capturedPieceType,
                    IsPawnPromotion = isPawnPromotion,
                    promotionFlag = promotionFlag,
                };
        }

        public static bool SquareAttackedByPawn(int square)
        {
            if (whiteToMove)
            {
                if ((MoveTables.PrecomputedWhitePawnCaptures[square] & InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Pawn]) != 0) return true;
            } else
            {
                if ((MoveTables.PrecomputedBlackPawnCaptures[square] & InternalBoard.Pieces[ChessBoard.White, ChessBoard.Pawn]) != 0) return true;
            }
            return false;
        }

        // returns true if a piece is attacking the square
        public static bool SquareAttackedBy(int square)
        {

            /* What is going on here is confusing at first glance. For example, with pawn captures, if we want to find if a square is under
             * attack by a white pawn, we have to index the potential black pawn captures movetable to essentially
             * look from the perspective of that square if it had a black pawn on it and see if its potential capture squares intersect with
             * the white pawn we were originally addressing. Pawns capture opposite ways on opposite sides but in the same manner (diagonally).
             * */


            if (whiteToMove)
            {
                if ((MoveTables.PrecomputedWhitePawnCaptures[square] & InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Pawn]) != 0) return true;

                if ((MoveTables.PrecomputedKnightMoves[square] & InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Knight]) != 0) return true;

                if ((MoveTables.PrecomputedKingMoves[square] & InternalBoard.Pieces[ChessBoard.Black, ChessBoard.King]) != 0) return true;

                ulong bishopsAndQueens = InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Queen] | InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Bishop];
                ulong rooksAndQueens = InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Queen] | InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Rook];

                if ((GetBishopAttacks(InternalBoard.AllPieces, square) & bishopsAndQueens) != 0) return true;

                if ((GetRookAttacks(InternalBoard.AllPieces, square) & rooksAndQueens) != 0) return true;

            }
            else
            {

                if ((MoveTables.PrecomputedBlackPawnCaptures[square] & InternalBoard.Pieces[ChessBoard.White, ChessBoard.Pawn]) != 0) return true;

                if ((MoveTables.PrecomputedKnightMoves[square] & InternalBoard.Pieces[ChessBoard.White, ChessBoard.Knight]) != 0) return true;

                if ((MoveTables.PrecomputedKingMoves[square] & InternalBoard.Pieces[ChessBoard.White, ChessBoard.King]) != 0) return true;

                ulong bishopsAndQueens = InternalBoard.Pieces[ChessBoard.White, ChessBoard.Queen] | InternalBoard.Pieces[ChessBoard.White, ChessBoard.Bishop];
                ulong rooksAndQueens = InternalBoard.Pieces[ChessBoard.White, ChessBoard.Queen] | InternalBoard.Pieces[ChessBoard.White, ChessBoard.Rook];

                if ((GetBishopAttacks(InternalBoard.AllPieces, square) & bishopsAndQueens) != 0) return true;

                if ((GetRookAttacks(InternalBoard.AllPieces, square) & rooksAndQueens) != 0) return true;
            }

            return false;
        }

        private static bool CanCastleKingsideWhite()
        {
            // check to make sure squares between king and kingside rook are empty
            if (((InternalBoard.AllPieces) & 0x60) != 0)
            {
                return false;
            }

            // checks to make sure neither the kingside rook or king has moved
            if ((CurrentGameState.castlingRights & 0x1) == 0)
            {
                return false;
            }

            // check if squares between king and kingside rook are underattack
            // from e1 to g1
            for (int i = 4; i < 7; i++)
            {
                if (SquareAttackedBy(i)) return false;
            }

            return true;
        }

        private static bool CanCastleQueensideWhite()
        {

            // check to make sure squares between king and queenside rook are empty
            if (((InternalBoard.AllPieces) & 0xE) != 0)
            {
                return false;
            }

            // checks to make sure neither the queenside rook or king has moved
            if ((CurrentGameState.castlingRights & 0x2) == 0)
            {
                return false;
            }

            // check if squares between king and kingside rook are underattack
            // from c1 to e1
            for (int i = 2; i < 5; i++)
            {
                if (SquareAttackedBy(i)) return false;
            }
            return true;
        }

        private static bool CanCastleKingsideBlack()
        {
            // check to make sure squares between king and kingside rook are empty
            if (((InternalBoard.AllPieces) & 0x6000000000000000) != 0)
            {
                return false;
            }

            if ((CurrentGameState.castlingRights & 0x4) == 0)
            {
                return false;
            }

            // from e8 to g8
            for (int i = 60; i < 63; i++)
            {
                if (SquareAttackedBy(i)) return false;
            }

            return true;
        }

        private static bool CanCastleQueensideBlack()
        {
            // check to make sure squares between king and queenside rook are empty
            if (((InternalBoard.AllPieces) & 0xE00000000000000) != 0)
            {
                return false;
            }

            if ((CurrentGameState.castlingRights & 0x8) == 0)
            {
                return false;
            }

            // from c8 to e8
            for (int i = 58; i < 61; i++)
            {
                if (SquareAttackedBy(i)) return false;
            }

            return true;
        }

        public static void GenerateKingMoves(ref Span<Move> moves, ulong friendlyPieces, ulong oppPieces, bool onlyGenerateCaptures = false)
        {
            ulong king = InternalBoard.Pieces[MoveColorIndex, ChessBoard.King];

            // king index converts the king bitboard 
            int kingIndex = BitBoardHelper.GetLSB(ref king);

            // grabs the corresponding bitboard representing all legal moves from the given king index on the board
            ulong validKingMoves = MoveTables.PrecomputedKingMoves[kingIndex];
            
            if(onlyGenerateCaptures)
            {
                // only include captures in movelist if quiescence search
                validKingMoves &= oppPieces;

            } else
            {
                // valid king moves only include either empty squares or squares the opponent pieces occupy (for now, this will change when check is implemented)
                validKingMoves &= ~friendlyPieces;

                // castling, castling moves are quiet moves 
                if (whiteToMove)
                {
                    if (CanCastleKingsideWhite())
                    {
                        moves[currentMoveIndex] = AddLegalMove(king, 1UL << 6, ChessBoard.King, ChessBoard.None, SpecialMove.KingSideCastleMove);
                        currentMoveIndex++;
                    }

                    if (CanCastleQueensideWhite())
                    {
                        moves[currentMoveIndex] = AddLegalMove(king, 1UL << 2, ChessBoard.King, ChessBoard.None, SpecialMove.QueenSideCastleMove);
                        currentMoveIndex++;
                    }
                }
                else
                {
                    if (CanCastleKingsideBlack())
                    {
                        moves[currentMoveIndex] = AddLegalMove(king, 1UL << 62, ChessBoard.King, ChessBoard.None, SpecialMove.KingSideCastleMove);
                        currentMoveIndex++;
                    }

                    if (CanCastleQueensideBlack())
                    {
                        moves[currentMoveIndex] = AddLegalMove(king, 1UL << 58, ChessBoard.King, ChessBoard.None, SpecialMove.QueenSideCastleMove);
                        currentMoveIndex++;
                    }
                }
            }

            while (validKingMoves != 0)
            {
                // gets the least significant bit while validmoves are being parsed in order to find new square position
                ulong movelsb = validKingMoves & (~validKingMoves + 1);

                validKingMoves &= validKingMoves - 1;

                moves[currentMoveIndex] = AddLegalMove(king, movelsb, ChessBoard.King, GetPieceAtSquare(OpponentColorIndex, movelsb));
                currentMoveIndex++;
            }
        }

        /* these functions came from "https://www.chessprogramming.org/Pawn_Pushes_(Bitboards)"
        * the pawn is able to push if no impeding piece (friendly or foe) is blocking the path, these functions traverse
        * the intersection of pawns with the shifted down empty squares in the opposite direction
        */
        static ulong WhitePawnsAbleToPushOneSquare(ulong wpawns, ulong emptySquares)
        {
            ulong result = ChessBoard.SouthOne(emptySquares) & wpawns;
            return result;
        }

        static ulong WhitePawnsAbleToPushTwoSquares(ulong wpawns, ulong emptySquares)
        {
            ulong rank4 = MoveTables.RankMasks[3];
            ulong emptyRank3 = ChessBoard.SouthOne(emptySquares & rank4) & emptySquares;
            return WhitePawnsAbleToPushOneSquare(wpawns, emptyRank3);
        }

        static ulong BlackPawnsAbleToPushOneSquare(ulong wpawns, ulong emptySquares)
        {
            ulong result = ChessBoard.NorthOne(emptySquares) & wpawns;
            return result;
        }

        static ulong BlackPawnsAbleToPushTwoSquares(ulong wpawns, ulong emptySquares)
        {
            ulong rank5 = MoveTables.RankMasks[4];
            ulong emptyRank6 = ChessBoard.NorthOne(emptySquares & rank5) & emptySquares;
            return BlackPawnsAbleToPushOneSquare(wpawns, emptyRank6);
        }

        //

        public static void GenerateRookMoves(ref Span<Move> moves, ulong friendlyPieces, ulong oppPieces, bool onlyGenerateCaptures = false)
        {
            ulong rooks = InternalBoard.Pieces[MoveColorIndex, ChessBoard.Rook];

            while (rooks != 0)
            {
                // and with twos complement to isolate each rook
                ulong isolatedRooklsb = rooks & (~rooks + 1);

                ulong validRookMoves = GetRookAttacks(InternalBoard.AllPieces, BitBoardHelper.GetLSB(ref isolatedRooklsb));

                // remove friendly piece blockers from potential captures 
                validRookMoves &= onlyGenerateCaptures ? oppPieces : ~friendlyPieces;

                while (validRookMoves != 0)
                {
                    ulong movelsb = validRookMoves & (~validRookMoves + 1);

                    validRookMoves &= validRookMoves - 1;
                    moves[currentMoveIndex] = AddLegalMove(isolatedRooklsb, movelsb, ChessBoard.Rook, GetPieceAtSquare(OpponentColorIndex, movelsb));
                    currentMoveIndex++;
                }
                rooks &= rooks - 1;
            }
        }

        public static void GenerateBishopMoves(ref Span<Move> moves, ulong friendlyPieces, ulong oppPieces, bool onlyGenerateCaptures = false)
        {
            ulong bishops = InternalBoard.Pieces[MoveColorIndex, ChessBoard.Bishop];

            while (bishops != 0)
            {
                // and with twos complement to isolate each bishop
                ulong isolatedBishoplsb = bishops & (~bishops + 1);

                ulong validBishopMoves = GetBishopAttacks(InternalBoard.AllPieces, BitBoardHelper.GetLSB(ref isolatedBishoplsb));

                // remove friendly piece blockers from potential captures, if Quiescence search, only add capture moves
                validBishopMoves &= onlyGenerateCaptures ? oppPieces : ~friendlyPieces;

                while (validBishopMoves != 0)
                {
                    ulong movelsb = validBishopMoves & (~validBishopMoves + 1);

                    validBishopMoves &= validBishopMoves - 1;

                    moves[currentMoveIndex] = AddLegalMove(isolatedBishoplsb, movelsb, ChessBoard.Bishop, GetPieceAtSquare(OpponentColorIndex, movelsb));
                    currentMoveIndex++;
                }
                bishops &= bishops - 1;
            }
        }

        public static void GenerateQueenMoves(ref Span<Move> moves, ulong friendlyPieces, ulong oppPieces, bool onlyGenerateCaptures = false)
        {
            ulong queens = InternalBoard.Pieces[MoveColorIndex, ChessBoard.Queen];

            while (queens != 0)
            {
                // and with twos complement to isolate each queen
                ulong isolatedQueenlsb = queens & (~queens + 1);

                int currentQueenPos = BitBoardHelper.GetLSB(ref isolatedQueenlsb);
                ulong validQueenMoves = GetBishopAttacks(InternalBoard.AllPieces, currentQueenPos);
                validQueenMoves |= GetRookAttacks(InternalBoard.AllPieces, currentQueenPos);

                // remove friendly piece blockers from potential captures, if Quiescence search, only add capture moves
                validQueenMoves &= onlyGenerateCaptures ? oppPieces : ~friendlyPieces;

                while (validQueenMoves != 0)
                {
                    ulong movelsb = validQueenMoves & (~validQueenMoves + 1);

                    validQueenMoves &= validQueenMoves - 1;
                    moves[currentMoveIndex] = AddLegalMove(isolatedQueenlsb, movelsb, ChessBoard.Queen, GetPieceAtSquare(OpponentColorIndex, movelsb));
                    currentMoveIndex++;
                }
                queens &= queens - 1;
            }
        }

        private static void BranchForPromotion(ref Span<Move> moves, ulong isolatedPawnlsb, ulong movelsb)
        {
            int potentialCapture = GetPieceAtSquare(OpponentColorIndex, movelsb);
            moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, potentialCapture, SpecialMove.None, true, PromotionFlags.PromoteToQueenFlag);
            currentMoveIndex++;
            moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, potentialCapture, SpecialMove.None, true, PromotionFlags.PromoteToRookFlag);
            currentMoveIndex++;
            moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, potentialCapture, SpecialMove.None, true, PromotionFlags.PromoteToBishopFlag);
            currentMoveIndex++;
            moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, potentialCapture, SpecialMove.None, true, PromotionFlags.PromoteToKnightFlag);
            currentMoveIndex++;
        }

        public static void GenerateWhitePawnMoves(ref Span<Move> moves, bool onlyGenerateCaptures = false)
        {
            ulong whitePawns = InternalBoard.Pieces[ChessBoard.White, ChessBoard.Pawn];

            while (whitePawns != 0)
            {
                // isolates each pawn one by one
                ulong isolatedPawnlsb = whitePawns & (~whitePawns + 1);
                // gets current pawn position to add to legal move list
                int currentPawnPos = BitBoardHelper.GetLSB(ref isolatedPawnlsb);

                if(!onlyGenerateCaptures)
                {
                    ulong oneSquareMove = isolatedPawnlsb << 8;
                    if (WhitePawnsAbleToPushOneSquare(isolatedPawnlsb, ~InternalBoard.AllPieces) == isolatedPawnlsb)
                    {
                        if ((oneSquareMove << 8) == 0)
                        {
                            BranchForPromotion(ref moves, isolatedPawnlsb, oneSquareMove);
                        }
                        else
                        {
                            moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, oneSquareMove, ChessBoard.Pawn);
                            currentMoveIndex++;
                            if (WhitePawnsAbleToPushTwoSquares(isolatedPawnlsb, ~InternalBoard.AllPieces) == isolatedPawnlsb)
                            {
                                moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, isolatedPawnlsb << 16, ChessBoard.Pawn, ChessBoard.None, SpecialMove.TwoSquarePawnMove);
                                currentMoveIndex++;
                            }
                        }
                    }
                }

                // handle a potential en passant capture
                if (CurrentGameState.enPassantFile != 0)
                {
                    ulong enPassantTargetSquare = 1UL << (CurrentGameState.enPassantFile - 1 + (5 * 8));
                    ulong enPassantCapture = MoveTables.PrecomputedWhitePawnCaptures[currentPawnPos] & enPassantTargetSquare;

                    // Handle en passant captures
                    if (enPassantCapture != 0)
                    {
                        moves[currentMoveIndex++] = AddLegalMove(isolatedPawnlsb, enPassantTargetSquare, ChessBoard.Pawn, ChessBoard.Pawn, SpecialMove.EnPassant);
                    }
                }

                // this is for normal piece captures
                ulong validPawnCaptures = MoveTables.PrecomputedWhitePawnCaptures[currentPawnPos] & InternalBoard.AllBlackPieces;

                while (validPawnCaptures != 0)
                {
                    // Isolate the least significant bit (LSB) of validPawnCaptures to get a single capture move
                    ulong pawnCapture = validPawnCaptures & (~validPawnCaptures + 1);

                    if ((pawnCapture << 8) == 0)
                    {
                        BranchForPromotion(ref moves, isolatedPawnlsb, pawnCapture);
                    }
                    else
                    {
                        moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, pawnCapture, ChessBoard.Pawn, GetPieceAtSquare(OpponentColorIndex, pawnCapture));
                        currentMoveIndex++;
                    }
                    validPawnCaptures &= validPawnCaptures - 1;
                }
                // move to the next pawn
                whitePawns &= whitePawns - 1;
            }
        }

        public static void GenerateBlackPawnMoves(ref Span<Move> moves, bool onlyGenerateCaptures = false)
        {

            ulong blackPawns = InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Pawn];

            while (blackPawns != 0)
            {
                ulong isolatedPawnlsb = blackPawns & (~blackPawns + 1);

                int currentPawnPos = BitBoardHelper.GetLSB(ref isolatedPawnlsb);


                if(!onlyGenerateCaptures) {
                    // valid pawn moves include pushes, captures, and en passant
                    ulong oneSquareMove = isolatedPawnlsb >> 8;
                    if (BlackPawnsAbleToPushOneSquare(isolatedPawnlsb, ~InternalBoard.AllPieces) == isolatedPawnlsb)
                    {
                        if ((oneSquareMove >> 8) == 0)
                        {
                            BranchForPromotion(ref moves, isolatedPawnlsb, oneSquareMove);
                        }
                        else
                        {
                            moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, oneSquareMove, ChessBoard.Pawn);
                            currentMoveIndex++;
                            if (BlackPawnsAbleToPushTwoSquares(isolatedPawnlsb, ~InternalBoard.AllPieces) == isolatedPawnlsb)
                            {
                                moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, isolatedPawnlsb >> 16, ChessBoard.Pawn, ChessBoard.None, SpecialMove.TwoSquarePawnMove);
                                currentMoveIndex++;
                            }
                        }
                    }
                }
                
                // handle a potential en passant capture
                if (CurrentGameState.enPassantFile != 0)
                {
                    ulong enPassantTargetSquare = 1UL << (CurrentGameState.enPassantFile - 1 + (2 * 8));
                    ulong enPassantCapture = MoveTables.PrecomputedBlackPawnCaptures[currentPawnPos] & enPassantTargetSquare;

                    // Handle en passant captures
                    if (enPassantCapture != 0)
                    {
                        moves[currentMoveIndex++] = AddLegalMove(isolatedPawnlsb, enPassantTargetSquare, ChessBoard.Pawn, ChessBoard.Pawn, SpecialMove.EnPassant);
                    }
                }

                // if a pawn can capture any black piece it is a pseudo-legal capture
                // this is for normal piece captures
                ulong validPawnCaptures = MoveTables.PrecomputedBlackPawnCaptures[currentPawnPos] & InternalBoard.AllWhitePieces;

                while (validPawnCaptures != 0)
                {
                    // Isolate the least significant bit (LSB) of validPawnCaptures to get a single capture move
                    ulong pawnCapture = validPawnCaptures & (~validPawnCaptures + 1);

                    if ((pawnCapture >> 8) == 0)
                    {
                        BranchForPromotion(ref moves, isolatedPawnlsb, pawnCapture);
                    }
                    else
                    {
                        moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, pawnCapture, ChessBoard.Pawn, GetPieceAtSquare(OpponentColorIndex, pawnCapture));
                        currentMoveIndex++;
                    }
                    validPawnCaptures &= validPawnCaptures - 1;
                }

                blackPawns &= blackPawns - 1;
            }
        }

        public static void GenerateKnightMoves(ref Span<Move> moves, ulong friendlyPieces, ulong oppPieces, bool onlyGenerateCaptures = false)
        {

            ulong knights = InternalBoard.Pieces[MoveColorIndex, ChessBoard.Knight];
            while (knights != 0)
            {
                // isolate each knight
                ulong isolatedKnightlsb = knights & (~knights + 1);
                int currentKnightPos = BitBoardHelper.GetLSB(ref isolatedKnightlsb);

                // valid knight moves only include either empty squares or squares the opponent pieces occupy
                ulong validKnightMoves = MoveTables.PrecomputedKnightMoves[currentKnightPos];
                validKnightMoves &= onlyGenerateCaptures ? oppPieces : ~friendlyPieces;

                while (validKnightMoves != 0)
                {
                    ulong movelsb = validKnightMoves & (~validKnightMoves + 1);

                    validKnightMoves &= validKnightMoves - 1;
                    moves[currentMoveIndex] = AddLegalMove(isolatedKnightlsb, movelsb, ChessBoard.Knight, GetPieceAtSquare(OpponentColorIndex, movelsb));
                    currentMoveIndex++;
                }

                // move to next knight
                knights &= knights - 1;
            }
        }

        public static Move[] GenerateMoves(bool onlyGenerateCaptures = false)
        {
            Span<Move> moves = stackalloc Move[256];

            int validMoveCount = GenerateAllLegalMoves(ref moves, onlyGenerateCaptures);

            Move[] movesArray = new Move[validMoveCount];
            for (int i = 0; i < validMoveCount; i++)
            {
                movesArray[i] = moves[i];
            }
            return movesArray;
        }

        public static bool IsKingInCheck(int currentKingSquare)
        {
            // Check for pawn attacks
            ulong pawnAttacks = whiteToMove ?
                MoveTables.PrecomputedBlackPawnCaptures[currentKingSquare] :
                MoveTables.PrecomputedWhitePawnCaptures[currentKingSquare];
            if ((pawnAttacks & InternalBoard.Pieces[MoveColorIndex, ChessBoard.Pawn]) != 0) return true;

            // Check for knight attacks
            ulong knightAttacks = MoveTables.PrecomputedKnightMoves[currentKingSquare];
            if ((knightAttacks & InternalBoard.Pieces[MoveColorIndex, ChessBoard.Knight]) != 0) return true;

            // Check for sliding pieces (bishops, rooks, queens)
            ulong bishopQueenAttacks = GetBishopAttacks(InternalBoard.AllPieces, currentKingSquare);
            ulong rookQueenAttacks = GetRookAttacks(InternalBoard.AllPieces, currentKingSquare);

            ulong bishopsQueens = InternalBoard.Pieces[MoveColorIndex, ChessBoard.Bishop] |
                                  InternalBoard.Pieces[MoveColorIndex, ChessBoard.Queen];
            ulong rooksQueens = InternalBoard.Pieces[MoveColorIndex, ChessBoard.Rook] |
                                InternalBoard.Pieces[MoveColorIndex, ChessBoard.Queen];

            if ((bishopQueenAttacks & bishopsQueens) != 0) return true;
            if ((rookQueenAttacks & rooksQueens) != 0) return true;

            // Check for king attacks (useful in edge cases and avoids self-check scenarios)
            ulong kingAttacks = MoveTables.PrecomputedKingMoves[currentKingSquare];
            if ((kingAttacks & InternalBoard.Pieces[MoveColorIndex, ChessBoard.King]) != 0) return true;

            return false;
        }

        public static void Initialize()
        {
            currentMoveIndex = 0;
        }

        public static int GenerateAllLegalMoves(ref Span<Move> pseudoLegalMoves, bool onlyGenerateCaptures = false)
        {
            Initialize();

            GenerateLegalMovesBitboard(ref pseudoLegalMoves, onlyGenerateCaptures);

            legalMoveCount = 0;

            kingInCheck = false;

            for (int i = 0; i < currentMoveIndex; i++)
            {
                ExecuteMove(pseudoLegalMoves[i], true);

                int currentKingSquare = whiteToMove ? BitBoardHelper.GetLSB(ref InternalBoard.Pieces[ChessBoard.Black, ChessBoard.King]) : BitBoardHelper.GetLSB(ref InternalBoard.Pieces[ChessBoard.White, ChessBoard.King]);

                if (!IsKingInCheck(currentKingSquare))
                {
                    pseudoLegalMoves[legalMoveCount++] = pseudoLegalMoves[i];
                }

                UndoMove(pseudoLegalMoves[i], true);
            }
            return legalMoveCount; // Returning the count of legal moves
        }

        public static void GenerateLegalMovesBitboard(ref Span<Move> moves, bool onlyGenerateCaptures = false)
        {
            // create list to store legal moves
            //List<Move> moves = new();

            // TODO edit genbishop, queen, and rook movesets to pass in reference to friendly piece bitboard

            if (whiteToMove)
            {
                GenerateBishopMoves(ref moves, InternalBoard.AllWhitePieces, InternalBoard.AllBlackPieces, onlyGenerateCaptures);
                GenerateRookMoves(ref moves, InternalBoard.AllWhitePieces, InternalBoard.AllBlackPieces, onlyGenerateCaptures);
                GenerateQueenMoves(ref moves, InternalBoard.AllWhitePieces, InternalBoard.AllBlackPieces, onlyGenerateCaptures);
                GenerateWhitePawnMoves(ref moves, onlyGenerateCaptures);
                GenerateKnightMoves(ref moves, InternalBoard.AllWhitePieces, InternalBoard.AllBlackPieces, onlyGenerateCaptures);
                GenerateKingMoves(ref moves, InternalBoard.AllWhitePieces, InternalBoard.AllBlackPieces, onlyGenerateCaptures);
            }
            else
            {
                GenerateBishopMoves(ref moves, InternalBoard.AllBlackPieces, InternalBoard.AllWhitePieces, onlyGenerateCaptures);
                GenerateRookMoves(ref moves, InternalBoard.AllBlackPieces, InternalBoard.AllWhitePieces, onlyGenerateCaptures);
                GenerateQueenMoves(ref moves, InternalBoard.AllBlackPieces, InternalBoard.AllWhitePieces, onlyGenerateCaptures);
                GenerateBlackPawnMoves(ref moves, onlyGenerateCaptures);
                GenerateKnightMoves(ref moves, InternalBoard.AllBlackPieces, InternalBoard.AllWhitePieces, onlyGenerateCaptures);
                GenerateKingMoves(ref moves, InternalBoard.AllBlackPieces, InternalBoard.AllWhitePieces, onlyGenerateCaptures);
            }
        }

        /* 
        *  The following functions were implemented through the help of these resources. Using magic bitboards is a complex process.
        *  Functions below "InitBishopLookup" and "InitRookLookup" are executed once to fill the lookup tables for magic bitboards. I chose to use
        *  the 'Plain Magic Bitboards' found on the chess programming wikipedia as it was the simplest for me to wrap my mind around.
        *  
        *  The magic numbers themselves I did not generate, as it was much easier to find a suitable precomputed table online.
        *  The Movetables C# class contains the array of magic bitboards
        *  
        *  https://www.chessprogramming.org/Magic_Bitboards#Plain
        *  https://stackoverflow.com/questions/67513005/how-to-generate-this-preinitialized-array-for-magic-bitboards
        */
        public static ulong GetBishopAttacks(ulong occupied, int square)
        {
            ulong blockers = occupied & MoveTables.BishopRelevantOccupancy[square];
            ulong index = (blockers * MoveTables.BishopMagics[square]) >> MoveTables.PrecomputedBishopShifts[square];
            return MoveTables.BishopAttackTable[square, index];
        }

        public static ulong GetRookAttacks(ulong occupied, int square)
        {
            ulong blockers = occupied & MoveTables.RookRelevantOccupancy[square];
            ulong index = (blockers * MoveTables.RookMagics[square]) >> MoveTables.PrecomputedRookShifts[square];
            return MoveTables.RookAttackTable[square, index];
        }


        public static int GetFile(int square)
        {
            return square % 8;
        }

        public static int GetRank(int square)
        {
            return square / 8;
        }

        private static int GetSquare(int rank, int file)
        {
            return rank * 8 + file;
        }

        // Generates the key, similar to the Java code snippet
        private static int Transform(ulong blockers, ulong magic, int shift)
        {
            return (int)((blockers * magic) >> shift);
        }

        private static int TrailingZeroCount(ulong value)
        {
            if (value == 0) return 64;
            int count = 0;

            while ((value & 1) == 0)
            {
                count++;
                value >>= 1;
            }

            return count;
        }

        private static int PopCount(ulong x)
        {
            int count;
            for (count = 0; x != 0; count++)
            {
                x &= x - 1; // Clear the least significant bit set
            }
            return count;
        }

        public static void InitBishopLookup()
        {
            for (int square = 0; square < 64; square++)
            {
                ulong mask = MoveTables.BishopRelevantOccupancy[square];
                int permutationCount = 1 << PopCount(mask);

                for (int i = 0; i < permutationCount; i++)
                {
                    ulong blockers = BlockersPermutation(i, mask);
                    ulong attacks = 0UL;
                    int rank = GetRank(square), r;
                    int file = GetFile(square), f;

                    for (r = rank + 1, f = file + 1; r <= 7 && f <= 7; r++, f++)
                    {
                        attacks |= 1UL << GetSquare(r, f);
                        if ((blockers & (1UL << GetSquare(r, f))) != 0)
                        {
                            break;
                        }
                    }

                    for (r = rank - 1, f = file + 1; r >= 0 && f <= 7; r--, f++)
                    {
                        attacks |= 1UL << GetSquare(r, f);
                        if ((blockers & (1UL << GetSquare(r, f))) != 0)
                        {
                            break;
                        }
                    }

                    for (r = rank - 1, f = file - 1; r >= 0 && f >= 0; r--, f--)
                    {
                        attacks |= 1UL << GetSquare(r, f);
                        if ((blockers & (1UL << GetSquare(r, f))) != 0)
                        {
                            break;
                        }
                    }

                    for (r = rank + 1, f = file - 1; r <= 7 && f >= 0; r++, f--)
                    {
                        attacks |= 1UL << GetSquare(r, f);
                        if ((blockers & (1UL << GetSquare(r, f))) != 0)
                        {
                            break;
                        }
                    }

                    int key = Transform(blockers, MoveTables.BishopMagics[square], MoveTables.PrecomputedBishopShifts[square]);

                    MoveTables.BishopAttackTable[square, key] = attacks;
                }
            }
        }

        public static void InitRookLookup()
        {
            for (int square = 0; square < 64; square++)
            {
                ulong mask = MoveTables.RookRelevantOccupancy[square];
                int permutationCount = 1 << PopCount(mask);

                for (int i = 0; i < permutationCount; i++)
                {
                    ulong blockers = BlockersPermutation(i, mask);
                    ulong attacks = 0UL;
                    int rank = GetRank(square), r;
                    int file = GetFile(square), f;

                    // Horizontal attacks to the right
                    for (f = file + 1; f <= 7; f++)
                    {
                        attacks |= 1UL << GetSquare(rank, f);
                        if ((blockers & (1UL << GetSquare(rank, f))) != 0)
                        {
                            break;
                        }
                    }

                    // Horizontal attacks to the left
                    for (f = file - 1; f >= 0; f--)
                    {
                        attacks |= 1UL << GetSquare(rank, f);
                        if ((blockers & (1UL << GetSquare(rank, f))) != 0)
                        {
                            break;
                        }
                    }

                    // Vertical attacks upwards
                    for (r = rank + 1; r <= 7; r++)
                    {
                        attacks |= 1UL << GetSquare(r, file);
                        if ((blockers & (1UL << GetSquare(r, file))) != 0)
                        {
                            break;
                        }
                    }

                    // Vertical attacks downwards
                    for (r = rank - 1; r >= 0; r--)
                    {
                        attacks |= 1UL << GetSquare(r, file);
                        if ((blockers & (1UL << GetSquare(r, file))) != 0)
                        {
                            break;
                        }
                    }

                    int key = Transform(blockers, MoveTables.RookMagics[square], MoveTables.PrecomputedRookShifts[square]);

                    MoveTables.RookAttackTable[square, key] = attacks;
                }
            }
        }


        private static ulong BlockersPermutation(int iteration, ulong mask)
        {
            ulong blockers = 0;

            while (iteration != 0)
            {
                if ((iteration & 1) != 0)
                {
                    int shift = TrailingZeroCount(mask);
                    blockers |= 1UL << shift;
                }

                iteration >>= 1;
                mask &= mask - 1; // Kernighan's bit count algorithm step
            }

            return blockers;
        }

        /*
         * 
         * 
         */
    }
}