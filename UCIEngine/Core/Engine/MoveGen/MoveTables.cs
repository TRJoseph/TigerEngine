using System;
using System.Linq;
using System.Collections.Generic;
using static Chess.Board;
namespace Chess
{

    public static class BoardHelper
    {
        public const ulong a1 = 1UL << 0;
        public const ulong b1 = 1UL << 1;
        public const ulong c1 = 1UL << 2;
        public const ulong d1 = 1UL << 3;
        public const ulong e1 = 1UL << 4;
        public const ulong f1 = 1UL << 5;
        public const ulong g1 = 1UL << 6;
        public const ulong h1 = 1UL << 7;

        public const ulong a2 = 1UL << 8;
        public const ulong b2 = 1UL << 9;
        public const ulong c2 = 1UL << 10;
        public const ulong d2 = 1UL << 11;
        public const ulong e2 = 1UL << 12;
        public const ulong f2 = 1UL << 13;
        public const ulong g2 = 1UL << 14;
        public const ulong h2 = 1UL << 15;

        public const ulong a3 = 1UL << 16;
        public const ulong b3 = 1UL << 17;
        public const ulong c3 = 1UL << 18;
        public const ulong d3 = 1UL << 19;
        public const ulong e3 = 1UL << 20;
        public const ulong f3 = 1UL << 21;
        public const ulong g3 = 1UL << 22;
        public const ulong h3 = 1UL << 23;

        public const ulong a4 = 1UL << 24;
        public const ulong b4 = 1UL << 25;
        public const ulong c4 = 1UL << 26;
        public const ulong d4 = 1UL << 27;
        public const ulong e4 = 1UL << 28;
        public const ulong f4 = 1UL << 29;
        public const ulong g4 = 1UL << 30;
        public const ulong h4 = 1UL << 31;

        public const ulong a5 = 1UL << 32;
        public const ulong b5 = 1UL << 33;
        public const ulong c5 = 1UL << 34;
        public const ulong d5 = 1UL << 35;
        public const ulong e5 = 1UL << 36;
        public const ulong f5 = 1UL << 37;
        public const ulong g5 = 1UL << 38;
        public const ulong h5 = 1UL << 39;

        public const ulong a6 = 1UL << 40;
        public const ulong b6 = 1UL << 41;
        public const ulong c6 = 1UL << 42;
        public const ulong d6 = 1UL << 43;
        public const ulong e6 = 1UL << 44;
        public const ulong f6 = 1UL << 45;
        public const ulong g6 = 1UL << 46;
        public const ulong h6 = 1UL << 47;

        public const ulong a7 = 1UL << 48;
        public const ulong b7 = 1UL << 49;
        public const ulong c7 = 1UL << 50;
        public const ulong d7 = 1UL << 51;
        public const ulong e7 = 1UL << 52;
        public const ulong f7 = 1UL << 53;
        public const ulong g7 = 1UL << 54;
        public const ulong h7 = 1UL << 55;

        public const ulong a8 = 1UL << 56;
        public const ulong b8 = 1UL << 57;
        public const ulong c8 = 1UL << 58;
        public const ulong d8 = 1UL << 59;
        public const ulong e8 = 1UL << 60;
        public const ulong f8 = 1UL << 61;
        public const ulong g8 = 1UL << 62;
        public const ulong h8 = 1UL << 63;

        public static ulong GetSquareBitboard(string square)
        {
            return square switch
            {
                "a1" => a1,
                "b1" => b1,
                "c1" => c1,
                "d1" => d1,
                "e1" => e1,
                "f1" => f1,
                "g1" => g1,
                "h1" => h1,
                "a2" => a2,
                "b2" => b2,
                "c2" => c2,
                "d2" => d2,
                "e2" => e2,
                "f2" => f2,
                "g2" => g2,
                "h2" => h2,
                "a3" => a3,
                "b3" => b3,
                "c3" => c3,
                "d3" => d3,
                "e3" => e3,
                "f3" => f3,
                "g3" => g3,
                "h3" => h3,
                "a4" => a4,
                "b4" => b4,
                "c4" => c4,
                "d4" => d4,
                "e4" => e4,
                "f4" => f4,
                "g4" => g4,
                "h4" => h4,
                "a5" => a5,
                "b5" => b5,
                "c5" => c5,
                "d5" => d5,
                "e5" => e5,
                "f5" => f5,
                "g5" => g5,
                "h5" => h5,
                "a6" => a6,
                "b6" => b6,
                "c6" => c6,
                "d6" => d6,
                "e6" => e6,
                "f6" => f6,
                "g6" => g6,
                "h6" => h6,
                "a7" => a7,
                "b7" => b7,
                "c7" => c7,
                "d7" => d7,
                "e7" => e7,
                "f7" => f7,
                "g7" => g7,
                "h7" => h7,
                "a8" => a8,
                "b8" => b8,
                "c8" => c8,
                "d8" => d8,
                "e8" => e8,
                "f8" => f8,
                "g8" => g8,
                "h8" => h8,
                _ => throw new ArgumentException("Invalid board square"),
            };
        }

        public static string GetStringFromSquareBitboard(ulong bitboard)
        {
            return bitboard switch
            {
                a1 => "a1",
                b1 => "b1",
                c1 => "c1",
                d1 => "d1",
                e1 => "e1",
                f1 => "f1",
                g1 => "g1",
                h1 => "h1",
                a2 => "a2",
                b2 => "b2",
                c2 => "c2",
                d2 => "d2",
                e2 => "e2",
                f2 => "f2",
                g2 => "g2",
                h2 => "h2",
                a3 => "a3",
                b3 => "b3",
                c3 => "c3",
                d3 => "d3",
                e3 => "e3",
                f3 => "f3",
                g3 => "g3",
                h3 => "h3",
                a4 => "a4",
                b4 => "b4",
                c4 => "c4",
                d4 => "d4",
                e4 => "e4",
                f4 => "f4",
                g4 => "g4",
                h4 => "h4",
                a5 => "a5",
                b5 => "b5",
                c5 => "c5",
                d5 => "d5",
                e5 => "e5",
                f5 => "f5",
                g5 => "g5",
                h5 => "h5",
                a6 => "a6",
                b6 => "b6",
                c6 => "c6",
                d6 => "d6",
                e6 => "e6",
                f6 => "f6",
                g6 => "g6",
                h6 => "h6",
                a7 => "a7",
                b7 => "b7",
                c7 => "c7",
                d7 => "d7",
                e7 => "e7",
                f7 => "f7",
                g7 => "g7",
                h7 => "h7",
                a8 => "a8",
                b8 => "b8",
                c8 => "c8",
                d8 => "d8",
                e8 => "e8",
                f8 => "f8",
                g8 => "g8",
                h8 => "h8",
                _ => throw new ArgumentException("Invalid board square"),
            };
        }
    }



    public static class MoveTables
    {
        // from index 0 to 63 starting from bottom left to top right of chess board
        public static ulong[] PrecomputedKingMoves = {
            0x302,
            0x705,
            0xe0a,
            0x1c14,
            0x3828,
            0x7050,
            0xe0a0,
            0xc040,
            0x30203,
            0x70507,
            0xe0a0e,
            0x1c141c,
            0x382838,
            0x705070,
            0xe0a0e0,
            0xc040c0,
            0x3020300,
            0x7050700,
            0xe0a0e00,
            0x1c141c00,
            0x38283800,
            0x70507000,
            0xe0a0e000,
            0xc040c000,
            0x302030000,
            0x705070000,
            0xe0a0e0000,
            0x1c141c0000,
            0x3828380000,
            0x7050700000,
            0xe0a0e00000,
            0xc040c00000,
            0x30203000000,
            0x70507000000,
            0xe0a0e000000,
            0x1c141c000000,
            0x382838000000,
            0x705070000000,
            0xe0a0e0000000,
            0xc040c0000000,
            0x3020300000000,
            0x7050700000000,
            0xe0a0e00000000,
            0x1c141c00000000,
            0x38283800000000,
            0x70507000000000,
            0xe0a0e000000000,
            0xc040c000000000,
            0x302030000000000,
            0x705070000000000,
            0xe0a0e0000000000,
            0x1c141c0000000000,
            0x3828380000000000,
            0x7050700000000000,
            0xe0a0e00000000000,
            0xc040c00000000000,
            0x203000000000000,
            0x507000000000000,
            0xa0e000000000000,
            0x141c000000000000,
            0x2838000000000000,
            0x5070000000000000,
            0xa0e0000000000000,
            0x40c0000000000000
        };

        // from index 0 to 63 starting from bottom left to top right of chess board
        public static ulong[] PrecomputedKnightMoves = {
            0x20400,
            0x50800,
            0xa1100,
            0x142200,
            0x284400,
            0x508800,
            0xa01000,
            0x402000,
            0x2040004,
            0x5080008,
            0xa110011,
            0x14220022,
            0x28440044,
            0x50880088,
            0xa0100010,
            0x40200020,
            0x204000402,
            0x508000805,
            0xa1100110a,
            0x1422002214,
            0x2844004428,
            0x5088008850,
            0xa0100010a0,
            0x4020002040,
            0x20400040200,
            0x50800080500,
            0xa1100110a00,
            0x142200221400,
            0x284400442800,
            0x508800885000,
            0xa0100010a000,
            0x402000204000,
            0x2040004020000,
            0x5080008050000,
            0xa1100110a0000,
            0x14220022140000,
            0x28440044280000,
            0x50880088500000,
            0xa0100010a00000,
            0x40200020400000,
            0x204000402000000,
            0x508000805000000,
            0xa1100110a000000,
            0x1422002214000000,
            0x2844004428000000,
            0x5088008850000000,
            0xa0100010a0000000,
            0x4020002040000000,
            0x400040200000000,
            0x800080500000000,
            0x1100110a00000000,
            0x2200221400000000,
            0x4400442800000000,
            0x8800885000000000,
            0x100010a000000000,
            0x2000204000000000,
            0x4020000000000,
            0x8050000000000,
            0x110a0000000000,
            0x22140000000000,
            0x44280000000000,
            0x88500000000000,
            0x10a00000000000,
            0x20400000000000,
        };

        public static ulong[] PrecomputedWhitePawnCaptures =
        {
            0x200,
            0x500,
            0xa00,
            0x1400,
            0x2800,
            0x5000,
            0xa000,
            0x4000,
            0x20000,
            0x50000,
            0xa0000,
            0x140000,
            0x280000,
            0x500000,
            0xa00000,
            0x400000,
            0x2000000,
            0x5000000,
            0xa000000,
            0x14000000,
            0x28000000,
            0x50000000,
            0xa0000000,
            0x40000000,
            0x200000000,
            0x500000000,
            0xa00000000,
            0x1400000000,
            0x2800000000,
            0x5000000000,
            0xa000000000,
            0x4000000000,
            0x20000000000,
            0x50000000000,
            0xa0000000000,
            0x140000000000,
            0x280000000000,
            0x500000000000,
            0xa00000000000,
            0x400000000000,
            0x2000000000000,
            0x5000000000000,
            0xa000000000000,
            0x14000000000000,
            0x28000000000000,
            0x50000000000000,
            0xa0000000000000,
            0x40000000000000,
            0x200000000000000,
            0x500000000000000,
            0xa00000000000000,
            0x1400000000000000,
            0x2800000000000000,
            0x5000000000000000,
            0xa000000000000000,
            0x4000000000000000,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
        };

        public static ulong[] PrecomputedBlackPawnCaptures = {
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x0,
            0x2,
            0x5,
            0xa,
            0x14,
            0x28,
            0x50,
            0xa0,
            0x40,
            0x200,
            0x500,
            0xa00,
            0x1400,
            0x2800,
            0x5000,
            0xa000,
            0x4000,
            0x20000,
            0x50000,
            0xa0000,
            0x140000,
            0x280000,
            0x500000,
            0xa00000,
            0x400000,
            0x2000000,
            0x5000000,
            0xa000000,
            0x14000000,
            0x28000000,
            0x50000000,
            0xa0000000,
            0x40000000,
            0x200000000,
            0x500000000,
            0xa00000000,
            0x1400000000,
            0x2800000000,
            0x5000000000,
            0xa000000000,
            0x4000000000,
            0x20000000000,
            0x50000000000,
            0xa0000000000,
            0x140000000000,
            0x280000000000,
            0x500000000000,
            0xa00000000000,
            0x400000000000,
            0x2000000000000,
            0x5000000000000,
            0xa000000000000,
            0x14000000000000,
            0x28000000000000,
            0x50000000000000,
            0xa0000000000000,
            0x40000000000000,
        };

        // starting from the 1st rank (index 0) to the 8th rank (index 7)
        public static ulong[] RankMasks =
        {
            0xff,
            0xff00,
            0xff0000,
            0xff000000,
            0xff00000000,
            0xff0000000000,
            0xff000000000000,
            0xff00000000000000
        };

        /* MAGIC BITBOARD ARRAYS */

        public static ulong[] BishopMagics =
{
            0x0002020202020200, 0x0002020202020000, 0x0004010202000000, 0x0004040080000000,
            0x0001104000000000, 0x0000821040000000, 0x0000410410400000, 0x0000104104104000,
            0x0000040404040400, 0x0000020202020200, 0x0000040102020000, 0x0000040400800000,
            0x0000011040000000, 0x0000008210400000, 0x0000004104104000, 0x0000002082082000,
            0x0004000808080800, 0x0002000404040400, 0x0001000202020200, 0x0000800802004000,
            0x0000800400A00000, 0x0000200100884000, 0x0000400082082000, 0x0000200041041000,
            0x0002080010101000, 0x0001040008080800, 0x0000208004010400, 0x0000404004010200,
            0x0000840000802000, 0x0000404002011000, 0x0000808001041000, 0x0000404000820800,
            0x0001041000202000, 0x0000820800101000, 0x0000104400080800, 0x0000020080080080,
            0x0000404040040100, 0x0000808100020100, 0x0001010100020800, 0x0000808080010400,
            0x0000820820004000, 0x0000410410002000, 0x0000082088001000, 0x0000002011000800,
            0x0000080100400400, 0x0001010101000200, 0x0002020202000400, 0x0001010101000200,
            0x0000410410400000, 0x0000208208200000, 0x0000002084100000, 0x0000000020880000,
            0x0000001002020000, 0x0000040408020000, 0x0004040404040000, 0x0002020202020000,
            0x0000104104104000, 0x0000002082082000, 0x0000000020841000, 0x0000000000208800,
            0x0000000010020200, 0x0000000404080200, 0x0000040404040400, 0x0002020202020200
        };

        public static ulong[] RookMagics = {
            0x0080001020400080, 0x0040001000200040, 0x0080081000200080, 0x0080040800100080,
            0x0080020400080080, 0x0080010200040080, 0x0080008001000200, 0x0080002040800100,
            0x0000800020400080, 0x0000400020005000, 0x0000801000200080, 0x0000800800100080,
            0x0000800400080080, 0x0000800200040080, 0x0000800100020080, 0x0000800040800100,
            0x0000208000400080, 0x0000404000201000, 0x0000808010002000, 0x0000808008001000,
            0x0000808004000800, 0x0000808002000400, 0x0000010100020004, 0x0000020000408104,
            0x0000208080004000, 0x0000200040005000, 0x0000100080200080, 0x0000080080100080,
            0x0000040080080080, 0x0000020080040080, 0x0000010080800200, 0x0000800080004100,
            0x0000204000800080, 0x0000200040401000, 0x0000100080802000, 0x0000080080801000,
            0x0000040080800800, 0x0000020080800400, 0x0000020001010004, 0x0000800040800100,
            0x0000204000808000, 0x0000200040008080, 0x0000100020008080, 0x0000080010008080,
            0x0000040008008080, 0x0000020004008080, 0x0000010002008080, 0x0000004081020004,
            0x0000204000800080, 0x0000200040008080, 0x0000100020008080, 0x0000080010008080,
            0x0000040008008080, 0x0000020004008080, 0x0000800100020080, 0x0000800041000080,
            0x00FFFCDDFCED714A, 0x007FFCDDFCED714A, 0x003FFFCDFFD88096, 0x0000040810002101,
            0x0001000204080011, 0x0001000204000801, 0x0001000082000401, 0x0001FFFAABFAD1A2
        };

        public static int[] PrecomputedBishopShifts =
        {
            58, 59, 59, 59, 59, 59, 59, 58,
            59, 59, 59, 59, 59, 59, 59, 59,
            59, 59, 57, 57, 57, 57, 59, 59,
            59, 59, 57, 55, 55, 57, 59, 59,
            59, 59, 57, 55, 55, 57, 59, 59,
            59, 59, 57, 57, 57, 57, 59, 59,
            59, 59, 59, 59, 59, 59, 59, 59,
            58, 59, 59, 59, 59, 59, 59, 58
        };

        public static int[] PrecomputedRookShifts =
        {
            52, 53, 53, 53, 53, 53, 53, 52,
            53, 54, 54, 54, 54, 54, 54, 53,
            53, 54, 54, 54, 54, 54, 54, 53,
            53, 54, 54, 54, 54, 54, 54, 53,
            53, 54, 54, 54, 54, 54, 54, 53,
            53, 54, 54, 54, 54, 54, 54, 53,
            53, 54, 54, 54, 54, 54, 54, 53,
            53, 54, 54, 53, 53, 53, 53, 53
        };


        /* Relevant occupancy tables include all bishop attack and rook attack maps excluding pieces on the edge of the board.
         * Ex: Squares such as A8 and H1 (with a rook on A1) have no squares behind them to block
         */
        public static ulong[] BishopRelevantOccupancy =
        {
            0x40201008040200,
            0x402010080400,
            0x4020100A00,
            0x40221400,
            0x2442800,
            0x204085000,
            0x20408102000,
            0x2040810204000,
            0x20100804020000,
            0x40201008040000,
            0x4020100A0000,
            0x4022140000,
            0x244280000,
            0x20408500000,
            0x2040810200000,
            0x4081020400000,
            0x10080402000200,
            0x20100804000400,
            0x4020100A000A00,
            0x402214001400,
            0x24428002800,
            0x2040850005000,
            0x4081020002000,
            0x8102040004000,
            0x8040200020400,
            0x10080400040800,
            0x20100A000A1000,
            0x40221400142200,
            0x2442800284400,
            0x4085000500800,
            0x8102000201000,
            0x10204000402000,
            0x4020002040800,
            0x8040004081000,
            0x100A000A102000,
            0x22140014224000,
            0x44280028440200,
            0x8500050080400,
            0x10200020100800,
            0x20400040201000,
            0x2000204081000,
            0x4000408102000,
            0xA000A10204000,
            0x14001422400000,
            0x28002844020000,
            0x50005008040200,
            0x20002010080400,
            0x40004020100800,
            0x20408102000,
            0x40810204000,
            0xA1020400000,
            0x142240000000,
            0x284402000000,
            0x500804020000,
            0x201008040200,
            0x402010080400,
            0x2040810204000,
            0x4081020400000,
            0xA102040000000,
            0x14224000000000,
            0x28440200000000,
            0x50080402000000,
            0x20100804020000,
            0x40201008040200
        };

        public static ulong[] RookRelevantOccupancy =
        {
            0x101010101017E,
            0x202020202027C,
            0x404040404047A,
            0x8080808080876,
            0x1010101010106E,
            0x2020202020205E,
            0x4040404040403E,
            0x8080808080807E,
            0x1010101017E00,
            0x2020202027C00,
            0x4040404047A00,
            0x8080808087600,
            0x10101010106E00,
            0x20202020205E00,
            0x40404040403E00,
            0x80808080807E00,
            0x10101017E0100,
            0x20202027C0200,
            0x40404047A0400,
            0x8080808760800,
            0x101010106E1000,
            0x202020205E2000,
            0x404040403E4000,
            0x808080807E8000,
            0x101017E010100,
            0x202027C020200,
            0x404047A040400,
            0x8080876080800,
            0x1010106E101000,
            0x2020205E202000,
            0x4040403E404000,
            0x8080807E808000,
            0x1017E01010100,
            0x2027C02020200,
            0x4047A04040400,
            0x8087608080800,
            0x10106E10101000,
            0x20205E20202000,
            0x40403E40404000,
            0x80807E80808000,
            0x17E0101010100,
            0x27C0202020200,
            0x47A0404040400,
            0x8760808080800,
            0x106E1010101000,
            0x205E2020202000,
            0x403E4040404000,
            0x807E8080808000,
            0x7E010101010100,
            0x7C020202020200,
            0x7A040404040400,
            0x76080808080800,
            0x6E101010101000,
            0x5E202020202000,
            0x3E404040404000,
            0x7E808080808000,
            0x7E01010101010100,
            0x7C02020202020200,
            0x7A04040404040400,
            0x7608080808080800,
            0x6E10101010101000,
            0x5E20202020202000,
            0x3E40404040404000,
            0x7E80808080808000
        };

        public static ulong[,] BishopAttackTable = new ulong[64, 512];

        public static ulong[,] RookAttackTable = new ulong[64, 4096];


        public static ulong lightSquares = 0x55aa55aa55aa55aa;
        public static ulong darkSquares = 0xaa55aa55aa55aa55;
    }
}