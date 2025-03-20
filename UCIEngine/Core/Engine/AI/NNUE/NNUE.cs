using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess
{
    public class NNUE
    {
        const int inputSize = 768;
        const int scale = 400;
        const int QA = 255;
        const int QB = 64;
        const short hlSize = 1024;

        public struct Accumulator
        {
            public short[] values;
            public Accumulator()
            {
                this.values = new short[hlSize];
            }
        }

        public struct AccumulatorPair
        {
            public Accumulator white;
            public Accumulator black;

            public AccumulatorPair()
            {
                this.white = new Accumulator(); 
                this.black = new Accumulator(); 
            }
        }

        public struct Network
        {
            public short[,] accumulator_weights;
            public short[] accumulator_biases;
            public short[] output_weights;
            public short output_bias;

            public Network()
            {
                this.accumulator_weights = new short[inputSize,hlSize];
                this.accumulator_biases = new short[hlSize];
                this.output_weights = new short[2 * hlSize];
                this.output_bias = 0;
            }
        }

        Network network = new();
        AccumulatorPair accumulatorPair = new();
        
        private static int CalculateIndex(int square, int pieceType, bool whiteToMove)
        {
            int side = 0;
            if (!whiteToMove)
            {
                side = 1 - side;          // flip side
                square ^= 0b111000; // mirror
            }

            return side * 64 * 6 + pieceType * 64 + square;
        }

        public void AccumulatorAdd(ref Network network, ref Accumulator accumulator, int index)
        {
            for(int i = 0; i < hlSize; i++)
            {
                accumulator.values[i] += network.accumulator_weights[index, i];
            }
        }

        public void AccumulatorSub(ref Network network, ref Accumulator accumulator, int index)
        {
            for (int i = 0; i < hlSize; i++)
            {
                accumulator.values[i] -= network.accumulator_weights[index, i];
            }
        }

        // Clipped Rectified Linear Unit Activation Function
        private short CReLU(short value, short min, short max)
        {
            if (value <= min)
                return min;
            if (value >= max)
                return max;

            return value;
        }

        // CReLU activation
        public int activation(short value)
        {
            return CReLU(value, 0, QA);
        }

        // When forwarding the accumulator values, the network does not consider the color of the perspectives.
        // Rather, we are more interested in whether the accumulator is from the perspective of the side-to-move.
        public int forward(ref Network network, ref Accumulator sideToMoveAcc, ref Accumulator notSideToMoveAcc)
        {
            int eval = 0;
            for(int i = 0; i < hlSize; i++)
            {
                eval += activation(sideToMoveAcc.values[i]) * network.output_weights[i];
                eval += activation(notSideToMoveAcc.values[i]) * network.output_weights[i + hlSize];
            }

            eval += network.output_bias;
            eval *= scale;
            eval /= QA * QB;

            return eval;
        }

    }
}
