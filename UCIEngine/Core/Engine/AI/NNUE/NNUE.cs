using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.ConstrainedExecution;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace Chess
{

    public class NNUE
    {
        Network network = new();
        AccumulatorPair accumulatorPair = new();

        #region File Handling
        public void ExtractNormalWeightsAndBiases()
        {
            /// This extracts the non-quantized weights from the network 
            /// TODO: change this file path variable to adhere to standards
            string filePath = "C:\\Users\\trjos\\Projects\\TigerEngine\\UCIEngine\\Core\\Engine\\AI\\NNUE\\nnue_weightsNormal.bin";

            int ftWeightRows = 768, ftWeightCols = 1024;
            int l1WeightRows = 2048, l1WeightCols = 8;
            int l2WeightRows = 8, l2WeightCols = 32;
            int l3WeightRows = 32, l3WeightCols = 1;

            byte[] bytes = File.ReadAllBytes(filePath);
            int byteIndex = 0;

            // Extract weights as floats
            network.accumulator_weights = ExtractMatrixFloat(bytes, ref byteIndex, ftWeightRows, ftWeightCols);
            network.hiddenLayer1_weights = ExtractMatrixFloat(bytes, ref byteIndex, l1WeightRows, l1WeightCols);
            network.hiddenLayer2_weights = ExtractMatrixFloat(bytes, ref byteIndex, l2WeightRows, l2WeightCols);
            network.output_weights = ExtractMatrixFloat(bytes, ref byteIndex, l3WeightRows, l3WeightCols);

            // Extract biases as floats
            network.accumulator_biases = ExtractArrayFloat(bytes, ref byteIndex, ftWeightCols);
            network.hiddenLayer1_biases = ExtractArrayFloat(bytes, ref byteIndex, l1WeightCols);
            network.hiddenLayer2_biases = ExtractArrayFloat(bytes, ref byteIndex, l2WeightCols);
            network.output_bias = ExtractArrayFloat(bytes, ref byteIndex, 1);

            //// Debug info
            //Console.WriteLine("C# Loaded Weights (Sample):");
            //Console.WriteLine($"FT Weight[0,0]: {network.accumulator_weights[0, 0]}");
            //Console.WriteLine($"L1 Bias[0]: {network.hiddenLayer1_biases[0]}");
            //Console.WriteLine($"Output Weight[0,0]: {network.output_weights[0, 0]}");
            //Console.WriteLine($"Output Bias: {network.output_bias}");

            //Console.WriteLine($"Successfully loaded quantized weights! Final byte index: {byteIndex}, File size: {bytes.Length}");
        }

        public void ExtractQuantizedWeightsAndBiases()
        {
            string filePath = "C:\\Users\\trjos\\Projects\\TigerEngine\\UCIEngine\\Core\\Engine\\AI\\NNUE\\nnue_weightsQuantized.bin";

            int ftWeightRows = 768, ftWeightCols = 1024;
            int l1WeightRows = 2048, l1WeightCols = 8;
            int l2WeightRows = 8, l2WeightCols = 32;
            int l3WeightRows = 32, l3WeightCols = 1;

            int[] weightShapes = {
            ftWeightRows * ftWeightCols,
            l1WeightRows * l1WeightCols,
            l2WeightRows * l2WeightCols,
            l3WeightRows * l3WeightCols
            };

            int[] biasShapes = { ftWeightCols, l1WeightCols, l2WeightCols, l3WeightCols };

            // Read binary file
            byte[] bytes = File.ReadAllBytes(filePath);
            int byteIndex = 0;

            // weights
            // Extract weights
            network.accumulator_weights_quantized = ExtractMatrixInt16(bytes, ref byteIndex, ftWeightRows, ftWeightCols);
            network.hiddenLayer1_weights_quantized = ExtractMatrixInt8(bytes, ref byteIndex, l1WeightRows, l1WeightCols);
            network.hiddenLayer2_weights_quantized = ExtractMatrixInt8(bytes, ref byteIndex, l2WeightRows, l2WeightCols);
            network.output_weights_quantized = ExtractMatrixInt8(bytes, ref byteIndex, l3WeightRows, l3WeightCols);

            // Extract biases
            network.accumulator_biases_quantized = ExtractArrayInt16(bytes, ref byteIndex, ftWeightCols);
            network.hiddenLayer1_biases_quantized = ExtractArrayInt32(bytes, ref byteIndex, l1WeightCols);
            network.hiddenLayer2_biases_quantized = ExtractArrayInt32(bytes, ref byteIndex, l2WeightCols);
            network.output_bias_quantized = ExtractArrayInt32(bytes, ref byteIndex, l3WeightCols);
            byteIndex += 4;

            // After loading weights/biases:
            Console.WriteLine("C# Loaded Weights (Sample):");
            Console.WriteLine($"FT Weight[0,0]: {network.accumulator_weights_quantized[0, 0]}");
            Console.WriteLine($"L1 Bias[0]: {network.hiddenLayer1_biases_quantized[0]}");
            Console.WriteLine($"Output Weight[0,0]: {network.output_weights_quantized[0, 0]}");
            Console.WriteLine($"Output Bias: {network.output_bias_quantized}");

            Console.WriteLine($"Successfully loaded quantized weights! Final byte index: {byteIndex}, File size: {bytes.Length}");
        }

        private short[,] ExtractMatrixInt16(byte[] data, ref int byteIndex, int rows, int cols)
        {
            short[,] matrix = new short[rows, cols];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    matrix[i, j] = BitConverter.ToInt16(data, byteIndex);
                    byteIndex += 2;
                }
            }
            return matrix;
        }

        // Extract int8 matrix
        private sbyte[,] ExtractMatrixInt8(byte[] data, ref int byteIndex, int rows, int cols)
        {
            sbyte[,] matrix = new sbyte[rows, cols];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    matrix[i, j] = (sbyte)data[byteIndex];
                    byteIndex += 1;
                }
            }
            return matrix;
        }

        // Extract int16 array
        private short[] ExtractArrayInt16(byte[] data, ref int byteIndex, int size)
        {
            short[] array = new short[size];
            for (int i = 0; i < size; i++)
            {
                array[i] = BitConverter.ToInt16(data, byteIndex);
                byteIndex += 2;
            }
            return array;
        }

        // Extract int32 array
        private int[] ExtractArrayInt32(byte[] data, ref int byteIndex, int size)
        {
            int[] array = new int[size];
            for (int i = 0; i < size; i++)
            {
                array[i] = BitConverter.ToInt32(data, byteIndex);
                byteIndex += 4;
            }
            return array;
        }
        // Function to read the binary file
        static short[] ReadBinaryFile(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            short[] shorts = new short[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, shorts, 0, bytes.Length);
            return shorts;
        }

        private float[,] ExtractMatrixFloat(byte[] data, ref int byteIndex, int rows, int cols)
        {
            float[,] matrix = new float[rows, cols];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    matrix[i, j] = BitConverter.ToSingle(data, byteIndex);
                    byteIndex += 4; // float is 32 bits
                }
            }
            return matrix;
        }

        private float[] ExtractArrayFloat(byte[] data, ref int byteIndex, int size)
        {
            float[] array = new float[size];
            for (int i = 0; i < size; i++)
            {
                array[i] = BitConverter.ToSingle(data, byteIndex);
                byteIndex += 4; // float is 32 bits
            }
            return array;
        }
        #endregion

        #region NNUE Core
        public struct Accumulator
        {
            public float[] values;
            public Accumulator()
            {
                this.values = new float[Network.hlSize];
            }
        }
        public struct QuantizedAccumulator
        {
            public short[] values;
            public QuantizedAccumulator()
            {
                this.values = new short[Network.hlSize];
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

        public struct LinearLayer
        {
            public int NumInputs;
            public int NumOutputs;
            public float[,] Weights;
            public float[] Biases;
        }

        public static float[] Linear(
            LinearLayer layer,
            float[] input,
            float[] output)
        {
            // Initialize output with biases
            Array.Copy(layer.Biases, output, layer.NumOutputs);

            // Perform matrix multiplication
            for (int i = 0; i < layer.NumInputs; i++)
            {
                for (int j = 0; j < layer.NumOutputs; j++)
                {
                    output[j] += input[i] * layer.Weights[i, j];
                }
            }

            return output;
        }

        public struct QuantizedLinearLayer
        {
            public int NumInputs;
            public int NumOutputs;
            public sbyte[,] Weights;
            public int[] Biases;
        }

        public static int[] QuantizedLinear(
            QuantizedLinearLayer layer,
            short[] input,
            int[] output)
        {
            // Initialize output with biases
            Array.Copy(layer.Biases, output, layer.NumOutputs);

            // Perform matrix multiplication
            for (int i = 0; i < layer.NumInputs; i++)
            {
                for (int j = 0; j < layer.NumOutputs; j++)
                {
                    output[j] += (int)input[i] * (int)layer.Weights[i, j];
                }
            }

            for (int i = 0; i < layer.NumOutputs; i++)
                output[i] /= 64;

            return output;
        }

        public struct Network
        {
            public short[] whiteFeatures;
            public short[] blackFeatures;

            public const int scale = 400;
            public const int QA = 127;
            public const int QB = 64;

            public const int inputSize = 768;
            public const short hlSize = 1024;
            public const short hl2Size = 8;
            public const short hl3Size = 32;
            public const short outSize = 1;

            public float[,] accumulator_weights;  // float [768, 1024]
            public float[,] hiddenLayer1_weights; // float [2048, 8]
            public float[,] hiddenLayer2_weights; // float [8, 32]
            public float[,] output_weights;       // float [32, 1]
            public float[] accumulator_biases;    // float [1024]
            public float[] hiddenLayer1_biases;   // float [8]
            public float[] hiddenLayer2_biases;   // float [32]
            public float[] output_bias;           // float


            // These will be correct quantized weights using stockfish quantization schema (hopefully)
            public short[,] accumulator_weights_quantized;  // int16 [768, 1024]
            public sbyte[,] hiddenLayer1_weights_quantized; // int8 [2048, 8]
            public sbyte[,] hiddenLayer2_weights_quantized; // int8 [8, 32]
            public sbyte[,] output_weights_quantized;       // int8 [32, 1]
            public short[] accumulator_biases_quantized;    // int16 [1024]
            public int[] hiddenLayer1_biases_quantized;     // int32 [8]
            public int[] hiddenLayer2_biases_quantized;     // int32 [32]
            public int[] output_bias_quantized;               // int32 [1]

            public Network()
            {
                // features are 768 bit sparse input features
                this.whiteFeatures = new short[inputSize];
                this.blackFeatures = new short[inputSize];

                // 768->1024x2->8->32->1
                this.accumulator_weights = new float[inputSize, hlSize];
                this.hiddenLayer1_weights = new float[2 * hlSize, 8];
                this.hiddenLayer2_weights = new float[8, 32];
                this.output_weights = new float[32, 1];
                this.accumulator_biases = new float[hlSize];
                this.hiddenLayer1_biases = new float[8];
                this.hiddenLayer2_biases = new float[32];
                this.output_bias = new float[1];


                this.accumulator_weights_quantized = new short[inputSize,hlSize];
                this.hiddenLayer1_weights_quantized = new sbyte[2 * hlSize, 8];
                this.hiddenLayer2_weights_quantized = new sbyte[8, 32];
                this.output_weights_quantized = new sbyte[32, 1];
                this.accumulator_biases_quantized = new short[hlSize];
                this.hiddenLayer1_biases_quantized = new int[8];
                this.hiddenLayer2_biases_quantized = new int[32];
                this.output_bias_quantized = new int[1];
            }
        }
        
        public void AccumulatorAddNormal(ref Network network, ref Accumulator accumulator, int index)
        {
            for(int i = 0; i < Network.hlSize; i++)
            {
                accumulator.values[i] += network.accumulator_weights[index, i];
            }
        }

        public void AccumulatorAddQuantized(ref Network network, ref QuantizedAccumulator accumulator, int index)
        {
            for (int i = 0; i < Network.hlSize; i++)
            {
                accumulator.values[i] += network.accumulator_weights_quantized[index, i];
            }
        }


        public void AccumulatorSub(ref Network network, ref Accumulator accumulator, int index)
        {
            for (int i = 0; i < Network.hlSize; i++)
            {
                accumulator.values[i] -= network.accumulator_weights[index, i];
            }
        }

        // QA is the quantization factor, QA = 127 for floating point inference for now
        public static float[] CReLU(int size, ref float[] output, ref float[] input, float QA = 127.0f)
        {
            for(int i = 0; i < size ; i++)
            {
                output[i] = Math.Min(Math.Max(input[i], 0), QA);
            }

            return output;
        }

        // QA is the quantization factor, clips to 255 for now, honestly have no idea why this fixes the quantization, I need to be clipping to 127 without losing massive amounts of precision
        public static short[] QuantizedCReLU(int size, ref short[] output, ref int[] input, int QA = 255)
        {
            for (int i = 0; i < size; i++)
            {
                if (input[i] <= 0)
                {
                    output[i] = 0;
                }
                else
                {
                    output[i] = (short)Math.Min(input[i], QA);
                }
            }
            return output;
        }


        //public static short[] QuantizedCReLU(int size, ref short[] output, ref int[] input, int scale_prev,sbyte QA = 127)
        //{
        //    for (int i = 0; i < size; i++)
        //    {
        //        int scaled_value = input[i] / scale_prev; // Apply previous layer's scaling
        //        if (scaled_value <= 0)
        //            output[i] = 0;
        //        else if (scaled_value >= QA)
        //            output[i] = QA;
        //        else
        //            output[i] = (sbyte)scaled_value;
        //    }
        //    return output;
        //}
        public static float NNUE_Evaluate(ref Network network, ref Accumulator sideToMoveAcc, ref Accumulator notSideToMoveAcc)
        {
            float[] l1_x = new float[2 * Network.hlSize];
            float[] l1_x_clamped = new float[2 * Network.hlSize];

            float[] l2_x = new float[Network.hl2Size];
            float[] l2_x_clamped = new float[Network.hl2Size];

            float[] l3_x = new float[Network.hl3Size];
            float[] l3_x_clamped = new float[Network.hl3Size];

            float[] eval = new float[1];

            // feature transformer
            for (int i = 0; i < Network.hlSize; i++)
            {
                l1_x[i] = sideToMoveAcc.values[i];
                l1_x[i + Network.hlSize] = notSideToMoveAcc.values[i];
            }

            CReLU(2 * Network.hlSize, ref l1_x_clamped, ref l1_x);

            // hidden layer 1
            var HiddenLayer1 = new LinearLayer
            {
                NumInputs = 2 * Network.hlSize,
                NumOutputs = Network.hl2Size,
                Weights = network.hiddenLayer1_weights,
                Biases = network.hiddenLayer1_biases,
            };
            l2_x = Linear(HiddenLayer1, l1_x_clamped, l2_x);
            CReLU(Network.hl2Size, ref l2_x_clamped, ref l2_x);

            // hidden layer 2
            var HiddenLayer2 = new LinearLayer
            {
                NumInputs = Network.hl2Size,
                NumOutputs = Network.hl3Size,
                Weights = network.hiddenLayer2_weights,
                Biases = network.hiddenLayer2_biases,
            };
            l3_x = Linear(HiddenLayer2, l2_x_clamped, l3_x);
            CReLU(Network.hl3Size, ref l3_x_clamped, ref l3_x);

            // output layer
            var OutputLayer = new LinearLayer
            {
                NumInputs = Network.hl3Size,
                NumOutputs = Network.outSize,
                Weights = network.output_weights,
                Biases = network.output_bias,
            };

            // non scaled output
            eval = Linear(OutputLayer, l3_x_clamped, eval);

            // scale with stockfish scaling factor (~400)
            float scaled_output = Network.scale * eval[0];
           
            return scaled_output;
        }

        public void InitializeNormalAccumulators(ref Network network, ref Accumulator sideToMoveAcc, ref Accumulator notSideToMoveAcc)
        {
            // Initialize with biases
            for (int i = 0; i < Network.hlSize; i++)
            {
                sideToMoveAcc.values[i] = network.accumulator_biases[i];
                notSideToMoveAcc.values[i] = network.accumulator_biases[i];
            }

            //// Debug information
            //// Print bias value for neuron 0
            //Console.WriteLine($"Initial accumulator[0] (bias): {sideToMoveAcc.values[0]}");

            //// Count active features
            //int activeWhiteFeatures = network.whiteFeatures.Count(f => f == 1);
            //int activeBlackFeatures = network.blackFeatures.Count(f => f == 1);
            //Console.WriteLine($"Active white features: {activeWhiteFeatures}, Active black features: {activeBlackFeatures}");

            //// Debug first few active features
            //for (int i = 0; i < Math.Min(5, Network.inputSize); i++)
            //{
            //    if (network.whiteFeatures[i] == 1)
            //    {
            //        Console.WriteLine($"Active white feature {i}, weight[{i},0]: {network.accumulator_weights[i, 0]}");
            //    }
            //}


            for (int i = 0; i < Network.inputSize; i++)
            {
                if (network.whiteFeatures[i] == 1)
                {
                    AccumulatorAddNormal(ref network, ref sideToMoveAcc, i);
                }
                if (network.blackFeatures[i] == 1)
                {
                    AccumulatorAddNormal(ref network, ref notSideToMoveAcc, i);
                }
            }
        }

        public static int Forward(ref Network network, ref QuantizedAccumulator sideToMoveAcc, ref QuantizedAccumulator notSideToMoveAcc)
        {
            int[] l1_x = new int[2 * Network.hlSize];
            short[] l1_x_clamped = new short[2 * Network.hlSize];

            int[] l2_x = new int[Network.hl2Size];
            short[] l2_x_clamped = new short[Network.hl2Size];

            int[] l3_x = new int[Network.hl3Size];
            short[] l3_x_clamped = new short[Network.hl3Size];

            int[] eval = new int[1];

            // feature transformer
            for (int i = 0; i < Network.hlSize; i++)
            {
                l1_x[i] = sideToMoveAcc.values[i];
                l1_x[i + Network.hlSize] = notSideToMoveAcc.values[i];
            }

            QuantizedCReLU(2 * Network.hlSize, ref l1_x_clamped, ref l1_x);

            // hidden layer 1
            var HiddenLayer1 = new QuantizedLinearLayer
            {
                NumInputs = 2 * Network.hlSize,
                NumOutputs = Network.hl2Size,
                Weights = network.hiddenLayer1_weights_quantized,
                Biases = network.hiddenLayer1_biases_quantized,
            };
            l2_x = QuantizedLinear(HiddenLayer1, l1_x_clamped, l2_x);
            QuantizedCReLU(Network.hl2Size, ref l2_x_clamped, ref l2_x);

            // hidden layer 2
            var HiddenLayer2 = new QuantizedLinearLayer
            {
                NumInputs = Network.hl2Size,
                NumOutputs = Network.hl3Size,
                Weights = network.hiddenLayer2_weights_quantized,
                Biases = network.hiddenLayer2_biases_quantized,
            };
            l3_x = QuantizedLinear(HiddenLayer2, l2_x_clamped, l3_x);
            QuantizedCReLU(Network.hl3Size, ref l3_x_clamped, ref l3_x);

            // output layer
            var OutputLayer = new QuantizedLinearLayer
            {
                NumInputs = Network.hl3Size,
                NumOutputs = Network.outSize,
                Weights = network.output_weights_quantized,
                Biases = network.output_bias_quantized,
            };

            // non scaled output
            eval = QuantizedLinear(OutputLayer, l3_x_clamped, eval);

            // scale with stockfish scaling factor (~400)
            int scaled_output = eval[0];

            return scaled_output;
        }
        public void InitializeQuantizedAccumulators(ref Network network, ref QuantizedAccumulator sideToMoveAcc, ref QuantizedAccumulator notSideToMoveAcc)
        {
            // Initialize with biases
            for (int i = 0; i < Network.hlSize; i++)
            {
                sideToMoveAcc.values[i] = network.accumulator_biases_quantized[i];
                notSideToMoveAcc.values[i] = network.accumulator_biases_quantized[i];
            }

            // Debug information
            // Print bias value for neuron 0
            Console.WriteLine($"Initial accumulator[0] (bias): {sideToMoveAcc.values[0]}");

            // Count active features
            int activeWhiteFeatures = network.whiteFeatures.Count(f => f == 1);
            int activeBlackFeatures = network.blackFeatures.Count(f => f == 1);
            Console.WriteLine($"Active white features: {activeWhiteFeatures}, Active black features: {activeBlackFeatures}");

            // Debug first few active features
            for (int i = 0; i < Math.Min(5, Network.inputSize); i++)
            {
                if (network.whiteFeatures[i] == 1)
                {
                    Console.WriteLine($"Active white feature {i}, weight[{i},0]: {network.accumulator_weights_quantized[i, 0]}");
                }
            }


            for (int i = 0; i < Network.inputSize; i++)
            {
                if (network.whiteFeatures[i] == 1)
                {
                    AccumulatorAddQuantized(ref network, ref sideToMoveAcc, i);
                }
                if (network.blackFeatures[i] == 1)
                {
                    AccumulatorAddQuantized(ref network, ref notSideToMoveAcc, i);
                }
            }
        }

        #endregion


        #region Initialization section
        public int MirrorSquare(int square)
        {
            int file = square % 8;
            int rank = square / 8;
            int flippedRank = 7 - rank;
            return flippedRank * 8 + file;
        }

        public bool ParseFEN(string fen, ref short[] whiteFeatures, ref short[] blackFeatures)
        {
            Array.Clear(whiteFeatures, 0, whiteFeatures.Length);
            Array.Clear(blackFeatures, 0, blackFeatures.Length);

            string[] parts = fen.Split(' ');
            string boardPart = parts[0];
            string[] rows = boardPart.Split('/');

            for (int r = 0; r < 8; r++)
            {
                int rank = 7 - r;
                string rowStr = rows[r];
                int file = 0;

                foreach (char c in rowStr)
                {
                    if (char.IsDigit(c))
                    {
                        file += c - '0';
                    }
                    else
                    {
                        int pieceIdx = GetPieceIndex(char.ToLower(c));
                        bool isWhite = char.IsUpper(c);
                        int square = rank * 8 + file;
                        int whiteIndex = square * 12 + pieceIdx + (isWhite ? 0 : 6);
                        whiteFeatures[whiteIndex] = 1;
                        int flippedSquare = MirrorSquare(square);
                        int blackIndex = flippedSquare * 12 + pieceIdx + (isWhite ? 6 : 0);
                        blackFeatures[blackIndex] = 1;
                        file++;
                    }
                }
            }

            return parts[1] == "w" ? true : false;
        }

        private int GetPieceIndex(char piece)
        {
            return piece switch
            {
                'p' => 0,
                'n' => 1,
                'b' => 2,
                'r' => 3,
                'q' => 4,
                'k' => 5,
                _ => throw new ArgumentException($"Invalid piece character: {piece}")
            };
        }


        public void EvaluateFromFENQuantized(string fen)
        {
            bool whiteToMove = ParseFEN(fen, ref network.whiteFeatures, ref network.blackFeatures);
            ExtractQuantizedWeightsAndBiases();
            QuantizedAccumulator whiteAcc = new QuantizedAccumulator();
            QuantizedAccumulator blackAcc = new QuantizedAccumulator();
            InitializeQuantizedAccumulators(ref network, ref whiteAcc, ref blackAcc);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            int eval = Forward(ref network, ref whiteToMove ? ref whiteAcc : ref blackAcc, ref whiteToMove ? ref blackAcc : ref whiteAcc);
            watch.Stop();
            Console.WriteLine($"Time elapsed in ms: {watch.Elapsed.TotalMilliseconds}");

            // print evaluation for now
            Console.WriteLine($"QUANTIZED Evaluation: {eval}");

        }

        public void EvaluateFromFEN(string fen)
        {
            // I guess eventually when implementing this into my chess engine itself, I will get the move from the position information instead of the fen string
            bool whiteToMove = ParseFEN(fen, ref network.whiteFeatures, ref network.blackFeatures);

            //ExtractQuantizedWeightsAndBiases();

            ExtractNormalWeightsAndBiases();

            // initialize accumulators, will likely just initialize an accumulator pair instead and pass that struct around
            Accumulator whiteAcc = new Accumulator();
            Accumulator blackAcc = new Accumulator();
            InitializeNormalAccumulators(ref network, ref whiteAcc, ref blackAcc);

            // Run forward pass
            //int eval = Forward(
            //    ref network,
            //    ref whiteToMove ? ref whiteAcc : ref blackAcc, // Side to move accumulator
            //    ref whiteToMove ? ref blackAcc : ref whiteAcc, // Opponent accumulator
            //    whiteToMove
            //);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            float eval = NNUE_Evaluate(ref network, ref whiteToMove ? ref whiteAcc : ref blackAcc, ref whiteToMove ? ref blackAcc : ref whiteAcc);
            watch.Stop();
            Console.WriteLine($"Time elapsed in ms: {watch.Elapsed.TotalMilliseconds}");

            // print evaluation for now
            Console.WriteLine($"Evaluation: {eval}");
        }
        #endregion
    }
}
