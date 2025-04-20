using System;
using System.Linq;
using System.Collections.Generic;
using static Chess.Board;
using static Chess.PositionInformation;
using System.Numerics;
using System.Formats.Tar;
using System.Net.NetworkInformation;


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

        public static void InitializeMoveData()
        {
            // reset the currentMoveIndex
            currentMoveIndex = 0;
            for (int i = 0; i < pinMasks.Length; i++)
            {
                pinMasks[i] = 0;
            }

            // calculate opponent move map data
            MoveTables.OpponentAttackMap = 0;
            InitializeAttackMaps();
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

            if ((MoveTables.PrecomputedWhitePawnCaptures[square] & InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Pawn]) != 0) return true;

            if ((MoveTables.PrecomputedKnightMoves[square] & InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Knight]) != 0) return true;

            if ((MoveTables.PrecomputedKingMoves[square] & InternalBoard.Pieces[OpponentColorIndex, ChessBoard.King]) != 0) return true;

            ulong bishopsAndQueens = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Queen] | InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Bishop];
            ulong rooksAndQueens = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Queen] | InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Rook];

            if ((GetBishopAttacks(InternalBoard.AllPieces, square) & bishopsAndQueens) != 0) return true;

            if ((GetRookAttacks(InternalBoard.AllPieces, square) & rooksAndQueens) != 0) return true;

            return false;
        }


        public static int CountAttackersToSquare(int kingIndex)
        {
            int count = 0;

            ulong pawnAttacks = OpponentColorIndex == ChessBoard.White ?
                MoveTables.PrecomputedBlackPawnCaptures[kingIndex] :
                MoveTables.PrecomputedWhitePawnCaptures[kingIndex];
            count += CountBits(pawnAttacks & InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Pawn]);

            count += CountBits(MoveTables.PrecomputedKnightMoves[kingIndex] &
                                           InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Knight]);

            count += CountBits(MoveTables.PrecomputedKingMoves[kingIndex] &
                                           InternalBoard.Pieces[OpponentColorIndex, ChessBoard.King]);

            ulong bishopsAndQueens = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Queen] |
                                     InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Bishop];
            count += CountBits(GetBishopAttacks(InternalBoard.AllPieces, kingIndex) & bishopsAndQueens);

            ulong rooksAndQueens = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Queen] |
                                   InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Rook];
            count += CountBits(GetRookAttacks(InternalBoard.AllPieces, kingIndex) & rooksAndQueens);

            return count;
        }

        public static (int checkingPieceType, ulong CheckingPieceBitboard, int CheckingPieceIndex) FindCheckingPiece(int kingIndex)
        {
            ulong king = InternalBoard.Pieces[MoveColorIndex, ChessBoard.King];

            ulong pawnAttacks = MoveColorIndex == ChessBoard.White ?
                MoveTables.PrecomputedWhitePawnCaptures[kingIndex] :
                MoveTables.PrecomputedBlackPawnCaptures[kingIndex];
            ulong checkingPawns = pawnAttacks & InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Pawn];
            if (checkingPawns != 0)
            {
                ulong checkingPawn = checkingPawns & (~checkingPawns + 1); // Isolate LSB
                int pawnIndex = BitBoardHelper.GetLSB(ref checkingPawn);
                return (ChessBoard.Pawn, checkingPawn, pawnIndex);
            }

            ulong knightAttacks = MoveTables.PrecomputedKnightMoves[kingIndex];
            ulong checkingKnights = knightAttacks & InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Knight];
            if (checkingKnights != 0)
            {
                ulong checkingKnight = checkingKnights & (~checkingKnights + 1);
                int knightIndex = BitBoardHelper.GetLSB(ref checkingKnight);
                return (ChessBoard.Knight, checkingKnight, knightIndex);
            }

            ulong bishops = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Bishop];
            while (bishops != 0)
            {
                ulong isolatedBishop = bishops & (~bishops + 1);
                int bishopIndex = BitBoardHelper.GetLSB(ref isolatedBishop);
                ulong bishopAttacks = GetBishopAttacks(InternalBoard.AllPieces, bishopIndex);
                if ((bishopAttacks & king) != 0)
                {
                    return (ChessBoard.Bishop, isolatedBishop, bishopIndex);
                }
                bishops &= bishops - 1;
            }

            ulong rooks = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Rook];
            while (rooks != 0)
            {
                ulong isolatedRook = rooks & (~rooks + 1);
                int rookIndex = BitBoardHelper.GetLSB(ref isolatedRook);
                ulong rookAttacks = GetRookAttacks(InternalBoard.AllPieces, rookIndex);
                if ((rookAttacks & king) != 0)
                {
                    return (ChessBoard.Rook, isolatedRook, rookIndex);
                }
                rooks &= rooks - 1;
            }

            // Check queens
            ulong queens = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Queen];
            while (queens != 0)
            {
                ulong isolatedQueen = queens & (~queens + 1);
                int queenIndex = BitBoardHelper.GetLSB(ref isolatedQueen);
                ulong queenAttacks = GetBishopAttacks(InternalBoard.AllPieces, queenIndex) |
                                     GetRookAttacks(InternalBoard.AllPieces, queenIndex);
                if ((queenAttacks & king) != 0)
                {
                    return (ChessBoard.Queen, isolatedQueen, queenIndex);
                }
                queens &= queens - 1;
            }

            // This should never happen if we correctly detected check beforehand
            return (ChessBoard.None, 0UL, -1);
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
                if (((1UL << i) & MoveTables.OpponentAttackMap) != 0) return false;

                //if (SquareAttackedBy(i)) return false;
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
                if (((1UL << i) & MoveTables.OpponentAttackMap) != 0) return false;
                //if (SquareAttackedBy(i)) return false;
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
                if (((1UL << i) & MoveTables.OpponentAttackMap) != 0) return false;
                //if (SquareAttackedBy(i)) return false;
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
                if (((1UL << i) & MoveTables.OpponentAttackMap) != 0) return false;
                //if (SquareAttackedBy(i)) return false;
            }

            return true;
        }

        public static void GenerateKingMoves(ref Span<Move> moves, ulong friendlyPieces, ulong oppPieces, ulong king, int kingIndex, bool onlyGenerateCaptures = false, ulong targetMask = 0xffffffffffffffff, bool kingInCheck = false)
        {
            // grabs the corresponding bitboard representing all legal moves from the given king index on the board
            ulong validKingMoves = MoveTables.PrecomputedKingMoves[kingIndex];

            if (onlyGenerateCaptures)
            {
                // only include captures in movelist if quiescence search
                validKingMoves &= oppPieces;
            }

            validKingMoves &= ~friendlyPieces;

            // ensures the king can only move to squares that are not under attack
            validKingMoves &= ~MoveTables.OpponentAttackMap;

            if (!kingInCheck)
            {
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

        public static void GenerateRookMoves(ref Span<Move> moves, ulong friendlyPieces, ulong oppPieces, ref ulong[] pinMasks, bool onlyGenerateCaptures = false, ulong targetMask = 0xffffffffffffffff)
        {
            ulong rooks = InternalBoard.Pieces[MoveColorIndex, ChessBoard.Rook];

            while (rooks != 0)
            {
                // and with twos complement to isolate each rook
                ulong isolatedRooklsb = rooks & (~rooks + 1);
                int rookIndex = BitBoardHelper.GetLSB(ref isolatedRooklsb);

                ulong validRookMoves = GetRookAttacks(InternalBoard.AllPieces, rookIndex);

                // remove friendly piece blockers from potential captures 
                validRookMoves &= onlyGenerateCaptures ? oppPieces : ~friendlyPieces;

                validRookMoves &= targetMask;

                validRookMoves &= pinMasks[rookIndex] != 0 ? pinMasks[rookIndex] : 0xffffffffffffffff;

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

        public static void GenerateBishopMoves(ref Span<Move> moves, ulong friendlyPieces, ulong oppPieces, ref ulong[] pinMasks, bool onlyGenerateCaptures = false, ulong checkBlocksMask = 0xffffffffffffffff)
        {
            ulong bishops = InternalBoard.Pieces[MoveColorIndex, ChessBoard.Bishop];

            while (bishops != 0)
            {
                // and with twos complement to isolate each bishop
                ulong isolatedBishoplsb = bishops & (~bishops + 1);

                int bishopIndex = BitBoardHelper.GetLSB(ref isolatedBishoplsb);

                ulong validBishopMoves = GetBishopAttacks(InternalBoard.AllPieces, bishopIndex);

                // remove friendly piece blockers from potential captures, if Quiescence search, only add capture moves
                validBishopMoves &= onlyGenerateCaptures ? oppPieces : ~friendlyPieces;

                validBishopMoves &= checkBlocksMask;

                validBishopMoves &= pinMasks[bishopIndex] != 0 ? pinMasks[bishopIndex] : 0xffffffffffffffff;

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

        public static void GenerateQueenMoves(ref Span<Move> moves, ulong friendlyPieces, ulong oppPieces, ref ulong[] pinMasks, bool onlyGenerateCaptures = false, ulong checkBlocksMask = 0xffffffffffffffff)
        {
            ulong queens = InternalBoard.Pieces[MoveColorIndex, ChessBoard.Queen];

            while (queens != 0)
            {
                // and with twos complement to isolate each queen
                ulong isolatedQueenlsb = queens & (~queens + 1);

                int queenIndex = BitBoardHelper.GetLSB(ref isolatedQueenlsb);
                ulong validQueenMoves = GetBishopAttacks(InternalBoard.AllPieces, queenIndex);
                validQueenMoves |= GetRookAttacks(InternalBoard.AllPieces, queenIndex);

                // remove friendly piece blockers from potential captures, if Quiescence search, only add capture moves
                validQueenMoves &= onlyGenerateCaptures ? oppPieces : ~friendlyPieces;

                validQueenMoves &= checkBlocksMask;

                validQueenMoves &= pinMasks[queenIndex] != 0 ? pinMasks[queenIndex] : 0xffffffffffffffff;

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

        public static void GenerateWhitePawnMoves(ref Span<Move> moves, ref ulong[] pinMasks, bool onlyGenerateCaptures = false, ulong checkBlockMask = 0xffffffffffffffff, int kingIndex=0)
        {
            ulong whitePawns = InternalBoard.Pieces[ChessBoard.White, ChessBoard.Pawn];

            while (whitePawns != 0)
            {
                // isolates each pawn one by one
                ulong isolatedPawnlsb = whitePawns & (~whitePawns + 1);
                // gets current pawn position to add to legal move list
                int pawnIndex = BitBoardHelper.GetLSB(ref isolatedPawnlsb);

                if(!onlyGenerateCaptures)
                {
                    ulong oneSquareMove = isolatedPawnlsb << 8;

                    if (pinMasks[pawnIndex] != 0)
                    {
                        ulong legalMoveMask = pinMasks[pawnIndex] & checkBlockMask;
                        oneSquareMove &= legalMoveMask;
                    }
                    else
                    {
                        oneSquareMove &= checkBlockMask;
                    }

                    if (WhitePawnsAbleToPushOneSquare(isolatedPawnlsb, ~InternalBoard.AllPieces) == isolatedPawnlsb)
                    {
                        if (isolatedPawnlsb << 16 == 0 && oneSquareMove != 0)
                        {
                            BranchForPromotion(ref moves, isolatedPawnlsb, oneSquareMove);
                        }
                        else
                        {
                            if(oneSquareMove != 0)
                            {
                                moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, oneSquareMove, ChessBoard.Pawn);
                                currentMoveIndex++;
                            }
                            if (WhitePawnsAbleToPushTwoSquares(isolatedPawnlsb, ~InternalBoard.AllPieces) == isolatedPawnlsb)
                            {
                                ulong twoSquareMove = isolatedPawnlsb << 16;

                                if (pinMasks[pawnIndex] != 0)
                                {
                                    ulong legalMoveMask = pinMasks[pawnIndex] & checkBlockMask;
                                    twoSquareMove &= legalMoveMask;
                                }
                                else
                                {
                                    twoSquareMove &= checkBlockMask;
                                }
                                if (twoSquareMove != 0)
                                {
                                    moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, isolatedPawnlsb << 16, ChessBoard.Pawn, ChessBoard.None, SpecialMove.TwoSquarePawnMove);
                                    currentMoveIndex++;
                                }
                            }
                        }
                    }
                }

                // handle a potential en passant capture
                if (CurrentGameState.enPassantFile != 0)
                {
                    ulong enPassantTargetSquare = 1UL << (CurrentGameState.enPassantFile - 1 + (5 * 8));
                    ulong enPassantCapture = MoveTables.PrecomputedWhitePawnCaptures[pawnIndex] & enPassantTargetSquare;

                    // keep the "pseudo-legal" legal move logic for en passant, the move is so infrequent this is unlikely to cause any significant slowdown
                    if (enPassantCapture != 0)
                    {
                        Move enPassantMove = new Move()
                        {
                            fromSquare = isolatedPawnlsb,
                            toSquare = enPassantCapture,
                            specialMove = SpecialMove.EnPassant,
                            capturedPieceType = ChessBoard.Pawn,
                            movedPiece = ChessBoard.Pawn,
                        };

                        ExecuteMove(ref enPassantMove);

                        if (!IsKingInCheck(kingIndex))
                        {
                            moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, enPassantTargetSquare, ChessBoard.Pawn, ChessBoard.Pawn, SpecialMove.EnPassant);
                            currentMoveIndex++;
                        }
                        UndoMove(ref enPassantMove);

                    }
                }

                // this is for normal piece captures
                ulong validPawnCaptures = MoveTables.PrecomputedWhitePawnCaptures[pawnIndex] & InternalBoard.AllBlackPieces;

                if (pinMasks[pawnIndex] != 0)
                {
                    ulong legalMoveMask = pinMasks[pawnIndex] & checkBlockMask;
                    validPawnCaptures &= legalMoveMask;
                } else
                {
                    validPawnCaptures &= checkBlockMask;
                }

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

        public static void GenerateBlackPawnMoves(ref Span<Move> moves, ref ulong[] pinMasks, bool onlyGenerateCaptures = false, ulong checkBlockMask = 0xffffffffffffffff, int kingIndex = 0)
        {

            ulong blackPawns = InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Pawn];

            while (blackPawns != 0)
            {
                ulong isolatedPawnlsb = blackPawns & (~blackPawns + 1);

                int pawnIndex = BitBoardHelper.GetLSB(ref isolatedPawnlsb);

                if (!onlyGenerateCaptures) {

                    // valid pawn moves include pushes, captures, and en passant
                    ulong oneSquareMove = isolatedPawnlsb >> 8;

                    if (pinMasks[pawnIndex] != 0)
                    {
                        ulong legalMoveMask = pinMasks[pawnIndex] & checkBlockMask;
                        oneSquareMove &= legalMoveMask;
                    }
                    else
                    {
                        oneSquareMove &= checkBlockMask;
                    }

                    if (BlackPawnsAbleToPushOneSquare(isolatedPawnlsb, ~InternalBoard.AllPieces) == isolatedPawnlsb)
                    {
                        // this shifts the bitboard out of the 64-bit range essentially determining if the next move is a promotion (it signals the end of the board)
                        if ((isolatedPawnlsb >> 16 == 0 && oneSquareMove != 0))
                        {
                            BranchForPromotion(ref moves, isolatedPawnlsb, oneSquareMove);
                        }
                        else
                        {
                            if(oneSquareMove != 0)
                            {
                                moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, oneSquareMove, ChessBoard.Pawn);
                                currentMoveIndex++;
                            }
               
                            if (BlackPawnsAbleToPushTwoSquares(isolatedPawnlsb, ~InternalBoard.AllPieces) == isolatedPawnlsb)
                            {
                                ulong twoSquareMove = isolatedPawnlsb >> 16;
                                if (pinMasks[pawnIndex] != 0)
                                {
                                    ulong legalMoveMask = pinMasks[pawnIndex] & checkBlockMask;
                                    twoSquareMove &= legalMoveMask;
                                }
                                else
                                {
                                    twoSquareMove &= checkBlockMask;
                                }
                                if (twoSquareMove != 0)
                                {
                                    moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, isolatedPawnlsb >> 16, ChessBoard.Pawn, ChessBoard.None, SpecialMove.TwoSquarePawnMove);
                                    currentMoveIndex++;
                                }
                            }
                        }
                    }
                }
                
                // handle a potential en passant capture
                if (CurrentGameState.enPassantFile != 0)
                {
                    ulong enPassantTargetSquare = 1UL << (CurrentGameState.enPassantFile - 1 + (2 * 8));
                    ulong enPassantCapture = MoveTables.PrecomputedBlackPawnCaptures[pawnIndex] & enPassantTargetSquare;

                    if (enPassantCapture != 0)
                    {
                        Move enPassantMove = new Move()
                        {
                            fromSquare = isolatedPawnlsb,
                            toSquare = enPassantCapture,
                            movedPiece = ChessBoard.Pawn,
                            specialMove = SpecialMove.EnPassant,
                            capturedPieceType = ChessBoard.Pawn
                        };

                        ExecuteMove(ref enPassantMove);
                        
                        if (!IsKingInCheck(kingIndex))
                        {
                            moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, enPassantTargetSquare, ChessBoard.Pawn, ChessBoard.Pawn, SpecialMove.EnPassant);
                            currentMoveIndex++;
                        }
                        UndoMove(ref enPassantMove);
                    }
                }

                // if a pawn can capture any black piece it is a pseudo-legal capture
                // this is for normal piece captures
                ulong validPawnCaptures = MoveTables.PrecomputedBlackPawnCaptures[pawnIndex] & InternalBoard.AllWhitePieces;

                if (pinMasks[pawnIndex] != 0)
                {
                    ulong legalMoveMask = pinMasks[pawnIndex] & checkBlockMask;
                    validPawnCaptures &= legalMoveMask;
                } else
                {
                    validPawnCaptures &= checkBlockMask;
                }

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

        public static void GenerateKnightMoves(ref Span<Move> moves, ulong friendlyPieces, ulong oppPieces, ref ulong[] pinMasks, bool onlyGenerateCaptures = false, ulong targetMask = 0xffffffffffffffff)
        {

            ulong knights = InternalBoard.Pieces[MoveColorIndex, ChessBoard.Knight];
            while (knights != 0)
            {
                // isolate each knight
                ulong isolatedKnightlsb = knights & (~knights + 1);
                int knightIndex = BitBoardHelper.GetLSB(ref isolatedKnightlsb);

                // valid knight moves only include either empty squares or squares the opponent pieces occupy
                ulong validKnightMoves = MoveTables.PrecomputedKnightMoves[knightIndex];
                validKnightMoves &= onlyGenerateCaptures ? oppPieces : ~friendlyPieces;

                validKnightMoves &= targetMask;

                validKnightMoves &= pinMasks[knightIndex] != 0 ? pinMasks[knightIndex] : 0xffffffffffffffff;

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
            if ((kingAttacks & InternalBoard.Pieces[OpponentColorIndex, ChessBoard.King]) != 0) return true;

            return false;
        }

        public static int GenerateAllLegalMoves(ref Span<Move> LegalMoves, bool onlyGenerateCaptures = false)
        {
            InitializeMoveData();
            
            GenerateLegalMovesBitboard(ref LegalMoves, onlyGenerateCaptures);
            
            return currentMoveIndex;
        }

        public static void FindPinningPieces(int kingIndex)
        {
            ulong king = InternalBoard.Pieces[MoveColorIndex, ChessBoard.King];

            ulong bishops = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Bishop];
            ulong rooks = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Rook];
            ulong queens = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Queen];

            while (bishops != 0)
            {
                ulong isolatedBishoplsb = bishops & (~bishops + 1);

                int bishopIndex = BitBoardHelper.GetLSB(ref isolatedBishoplsb);

                ulong twoSquareRay = MoveTables.BishopAttackRays[kingIndex, bishopIndex] & ~king & ~isolatedBishoplsb;

                ulong piecesOnRay = twoSquareRay & InternalBoard.AllPieces;

                int pinnedPieceCount = CountBits(piecesOnRay);

                // if there is only one piece between a slider and the king, that piece is pinned
                if (pinnedPieceCount == 1)
                {
                    int pinnedPieceIndex = BitBoardHelper.GetLSB(ref piecesOnRay);
                    pinMasks[pinnedPieceIndex] = twoSquareRay | isolatedBishoplsb;
                }

                bishops &= bishops - 1;
            }

            while (rooks != 0)
            {
                ulong isolatedRooklsb = rooks & (~rooks + 1);

                int rookIndex = BitBoardHelper.GetLSB(ref isolatedRooklsb);

                ulong twoSquareRay = MoveTables.RookAttackRays[kingIndex, rookIndex] & ~king & ~isolatedRooklsb;

                ulong piecesOnRay = twoSquareRay & InternalBoard.AllPieces;

                int pinnedPieceCount = CountBits(piecesOnRay);

                // if there is only one piece between a slider and the king, that piece is pinned
                if (pinnedPieceCount == 1)
                {
                    int pinnedPieceIndex = BitBoardHelper.GetLSB(ref piecesOnRay);
                    pinMasks[pinnedPieceIndex] = twoSquareRay | isolatedRooklsb;
                }

                rooks &= rooks - 1;
            }

            while (queens != 0)
            {
                ulong isolatedQueenlsb = queens & (~queens + 1);

                int queenIndex = BitBoardHelper.GetLSB(ref isolatedQueenlsb);

                ulong twoSquareRay = MoveTables.QueenAttackRays[kingIndex, queenIndex] & ~isolatedQueenlsb & ~king;

                ulong piecesOnRay = twoSquareRay & InternalBoard.AllPieces;

                int pinnedPieceCount = CountBits(piecesOnRay);

                // if there is only one piece between a slider and the king, that piece is pinned
                if (pinnedPieceCount == 1)
                {
                    int pinnedPieceIndex = BitBoardHelper.GetLSB(ref piecesOnRay);
                    pinMasks[pinnedPieceIndex] = twoSquareRay | isolatedQueenlsb;
                }

                queens &= queens - 1;
            }
        }

        public static void GenerateLegalMovesBitboard(ref Span<Move> moves, bool onlyGenerateCaptures = false)
        {

            ulong king = InternalBoard.Pieces[MoveColorIndex, ChessBoard.King];
 
            int kingIndex = BitBoardHelper.GetLSB(ref king);

            bool kingInCheck = (MoveTables.OpponentAttackMap & king) != 0;

            FindPinningPieces(kingIndex);

            if (kingInCheck)
            {
                // single check, king can move or another piece can capture/block the checking piece
                if(CountAttackersToSquare(kingIndex) == 1) {
                    (int pieceType, ulong checkingPieceBB, int checkingPieceIndex) = FindCheckingPiece(kingIndex);

                    // had to add an attack rays table because magic bitboards cannot provide me the bitboard of squares between two squares
                    ulong blockMask = MoveTables.AttackRays[kingIndex, checkingPieceIndex] & ~king;

                    ulong checkBlocksMask = blockMask | checkingPieceBB;

                    GenerateBishopMoves(ref moves, MoveColorPieces, OpponentColorPieces, ref pinMasks, onlyGenerateCaptures, checkBlocksMask);
                    GenerateRookMoves(ref moves, MoveColorPieces, OpponentColorPieces, ref pinMasks, onlyGenerateCaptures, checkBlocksMask);
                    GenerateQueenMoves(ref moves, MoveColorPieces, OpponentColorPieces, ref pinMasks, onlyGenerateCaptures, checkBlocksMask);
                    GenerateKnightMoves(ref moves, MoveColorPieces, OpponentColorPieces, ref pinMasks, onlyGenerateCaptures, checkBlocksMask);
                    GenerateKingMoves(ref moves, MoveColorPieces, OpponentColorPieces, king, kingIndex, onlyGenerateCaptures, checkBlocksMask, kingInCheck);

                    if (whiteToMove)
                    {
                        GenerateWhitePawnMoves(ref moves, ref pinMasks, onlyGenerateCaptures, checkBlocksMask, kingIndex);
                    }
                    else
                    {
                        GenerateBlackPawnMoves(ref moves, ref pinMasks, onlyGenerateCaptures, checkBlocksMask, kingIndex);
                    }

                } else // double check, only king moves are legal
                {
                    GenerateKingMoves(ref moves, InternalBoard.AllWhitePieces, InternalBoard.AllBlackPieces, king, kingIndex, onlyGenerateCaptures, kingInCheck: kingInCheck);
                }
              
            } else
            {
                // if king is not in check, just address potential pin move complications
                GenerateBishopMoves(ref moves, MoveColorPieces, OpponentColorPieces, ref pinMasks, onlyGenerateCaptures);
                GenerateRookMoves(ref moves, MoveColorPieces, OpponentColorPieces, ref pinMasks, onlyGenerateCaptures);
                GenerateQueenMoves(ref moves, MoveColorPieces, OpponentColorPieces, ref pinMasks, onlyGenerateCaptures);
                GenerateKnightMoves(ref moves, MoveColorPieces, OpponentColorPieces, ref pinMasks, onlyGenerateCaptures);
                GenerateKingMoves(ref moves, MoveColorPieces, OpponentColorPieces, king, kingIndex, onlyGenerateCaptures, kingInCheck: kingInCheck);
                if (whiteToMove)
                {
                    GenerateWhitePawnMoves(ref moves, ref pinMasks, onlyGenerateCaptures, kingIndex: kingIndex);
                }
                else
                {
                    GenerateBlackPawnMoves(ref moves, ref pinMasks, onlyGenerateCaptures, kingIndex: kingIndex);
                }
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

        public static void InitializeAttackMaps()
        {
            ulong pawns = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Pawn];
            ulong knights = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Knight];
            ulong bishops = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Bishop];
            ulong rooks = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Rook];
            ulong queens = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Queen];
            ulong king = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.King];

            // this is because I want the opponent attack map to show all squares under potential attack, not just attack
            ulong AllPiecesNoKing = InternalBoard.AllPieces & ~InternalBoard.Pieces[MoveColorIndex, ChessBoard.King];

            while (pawns != 0) {
                ulong isolatedPawnlsb = pawns & (~pawns + 1);
                int currentPawnPos = BitBoardHelper.GetLSB(ref isolatedPawnlsb);
                ulong pawnAttacks = whiteToMove ? MoveTables.PrecomputedBlackPawnCaptures[currentPawnPos] : MoveTables.PrecomputedWhitePawnCaptures[currentPawnPos];
                MoveTables.OpponentAttackMap |= pawnAttacks;
                pawns &= pawns - 1;
            }

            while(knights != 0)
            {
                ulong isolatedKnightlsb = knights & (~knights + 1);
                int currentKnightPos = BitBoardHelper.GetLSB(ref isolatedKnightlsb);
                ulong knightAttacks = MoveTables.PrecomputedKnightMoves[currentKnightPos];
                MoveTables.OpponentAttackMap |= knightAttacks;
                knights &= knights - 1;
            }

            while (bishops != 0)
            {
                ulong isolatedBishoplsb = bishops & (~bishops + 1);
                int currentBishopPos = BitBoardHelper.GetLSB(ref isolatedBishoplsb);

                ulong bishopAttacks = GetBishopAttacks(AllPiecesNoKing, currentBishopPos);

                MoveTables.OpponentAttackMap |= bishopAttacks;
                bishops &= bishops - 1;
            }

            while (rooks != 0)
            {
                ulong isolatedRooklsb = rooks & (~rooks + 1);
                int currentRookPos = BitBoardHelper.GetLSB(ref isolatedRooklsb);

                ulong rookAttacks = GetRookAttacks(AllPiecesNoKing, currentRookPos);

                MoveTables.OpponentAttackMap |= rookAttacks;
                rooks &= rooks - 1;
            }

            while (queens != 0)
            {
                ulong isolatedQueenlsb = queens & (~queens + 1);
                int currentQueenPos = BitBoardHelper.GetLSB(ref isolatedQueenlsb);

                ulong rookQueenAttacks = GetRookAttacks(AllPiecesNoKing, currentQueenPos);
                ulong bishopQueenAttacks = GetBishopAttacks(AllPiecesNoKing, currentQueenPos);
                ulong queenAttacks = rookQueenAttacks | bishopQueenAttacks;
                MoveTables.OpponentAttackMap |= queenAttacks;
                queens &= queens - 1;
            }

            int kingIndex = BitBoardHelper.GetLSB(ref king);
            ulong kingAttacks = MoveTables.PrecomputedKingMoves[kingIndex];
            MoveTables.OpponentAttackMap |= kingAttacks;
        }

        //public static bool IsKingInCheck(int currentKingSquare)
        //{
        //    // Check for pawn attacks
        //    ulong pawnAttacks = whiteToMove ?
        //        MoveTables.PrecomputedBlackPawnCaptures[currentKingSquare] :
        //        MoveTables.PrecomputedWhitePawnCaptures[currentKingSquare];
        //    if ((pawnAttacks & InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Pawn]) != 0) return true;

        //    // Check for knight attacks
        //    ulong knightAttacks = MoveTables.PrecomputedKnightMoves[currentKingSquare];
        //    if ((knightAttacks & InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Knight]) != 0) return true;

        //    // Check for sliding pieces (bishops, rooks, queens)
        //    ulong bishopQueenAttacks = GetBishopAttacks(InternalBoard.AllPieces, currentKingSquare);
        //    ulong rookQueenAttacks = GetRookAttacks(InternalBoard.AllPieces, currentKingSquare);

        //    ulong bishopsQueens = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Bishop] |
        //                          InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Queen];
        //    ulong rooksQueens = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Rook] |
        //                        InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Queen];

        //    if ((bishopQueenAttacks & bishopsQueens) != 0) return true;
        //    if ((rookQueenAttacks & rooksQueens) != 0) return true;

        //    // Check for king attacks (useful in edge cases and avoids self-check scenarios)
        //    ulong kingAttacks = MoveTables.PrecomputedKingMoves[currentKingSquare];
        //    if ((kingAttacks & InternalBoard.Pieces[MoveColorIndex, ChessBoard.King]) != 0) return true;

        //    return false;
        //}
    }
}