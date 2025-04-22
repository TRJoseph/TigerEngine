using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess
{
    public class TigerEngine
    {
        public Arbiter arbiter;
        public Board board;
        public Engine engine;
        public MoveGen moveGenerator;
        public PerformanceTester performanceTester;

        public TigerEngine()
        {
            board = new Board();
            moveGenerator = new MoveGen(board);
            engine = new Engine(moveGenerator, board);
            arbiter = new Arbiter(moveGenerator, board);
            performanceTester = new PerformanceTester(moveGenerator, board, arbiter);
        }
    }
}
