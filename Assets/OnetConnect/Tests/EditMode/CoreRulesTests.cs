using System.Collections.Generic;
using NUnit.Framework;
using Onet.Core;

namespace Onet.Tests
{
    /// <summary>Unity Test Runner (EditMode) versions of the core rule checks.</summary>
    public class CoreRulesTests
    {
        static BoardModel FromRows(params string[] rows)
        {
            var b = new BoardModel(rows[0].Length, rows.Length);
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char ch = rows[y][x];
                    if (ch == '#') { b.Set(x, y, BoardModel.Blocked); continue; }
                    if (ch == ' ') continue;
                    b.SetSlot(x, y, true);
                    if (ch != '.') b.Set(x, y, ch - 'A' + 1);
                }
            return b;
        }

        readonly PathFinder _pf = new PathFinder();

        [Test] public void Straight_Line_Connects()
        {
            var b = FromRows("A..A", "BBBB");
            var path = new List<Cell>();
            Assert.IsTrue(_pf.FindPath(b, new Cell(0, 0), new Cell(3, 0), path, 0));
            Assert.AreEqual(2, path.Count);
        }

        [Test] public void L_Shape_Needs_One_Turn()
        {
            var b = FromRows("A.", "BA");
            Assert.IsFalse(_pf.CanConnect(b, new Cell(0, 0), new Cell(1, 1), 0));
            Assert.IsTrue(_pf.CanConnect(b, new Cell(0, 0), new Cell(1, 1), 1));
        }

        [Test] public void Z_Shape_Needs_Two_Turns()
        {
            var b = FromRows("A#C", "...", "C#A");
            Assert.IsFalse(_pf.CanConnect(b, new Cell(0, 0), new Cell(2, 2), 1));
            Assert.IsTrue(_pf.CanConnect(b, new Cell(0, 0), new Cell(2, 2), 2));
        }

        [Test] public void U_Shape_Uses_Outer_Lane()
        {
            var b = FromRows("ABA", "BBB");
            var path = new List<Cell>();
            Assert.IsTrue(_pf.FindPath(b, new Cell(0, 0), new Cell(2, 0), path, 2));
            Assert.AreEqual(-1, path[1].Y);
        }

        [Test] public void Three_Turns_Is_Illegal()
        {
            var b = FromRows("A..B", "BBB.", "....", ".BBB", "B..A");
            Assert.IsFalse(_pf.CanConnect(b, new Cell(0, 0), new Cell(3, 4), 2));
        }

        [Test] public void Gravity_Down_Respects_Stones()
        {
            var b = FromRows("A", ".", "#", "B", ".");
            Gravity.Apply(b, GravityMode.Down);
            Assert.AreEqual(1, b.Get(0, 1));
            Assert.AreEqual(2, b.Get(0, 4));
        }

        [Test] public void Generator_Is_Deterministic_And_Solvable()
        {
            var spec = LevelSpec.Rect(8, 10, 14, GravityMode.None, 42);
            var a = BoardGenerator.Generate(spec);
            var b = BoardGenerator.Generate(spec);
            Assert.IsTrue(a.Verified);
            Assert.AreEqual(a.Board.ToString(), b.Board.ToString());
        }

        [Test] public void Shuffle_Guarantees_A_Move()
        {
            var board = BoardGenerator.Generate(LevelSpec.Rect(6, 6, 18, GravityMode.None, 5)).Board;
            var finder = new MoveFinder();
            Assert.IsTrue(Shuffler.Shuffle(board, new System.Random(1), finder));
            Assert.IsTrue(finder.HasAnyMove(board));
        }
    }
}
