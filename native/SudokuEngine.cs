using System;
using System.Collections.Generic;

namespace IceSudoku;

public sealed record LogicStep(int Level, string Technique, int Row, int Col, int Value, string Explain);

public sealed record Puzzle(int[,] Grid, int[,] Solution, string Difficulty, int GivenCount, int Grade);

public static class SudokuEngine
{
    private static readonly Random Rng = new();

    public static readonly IReadOnlyDictionary<string, string> DifficultyLabel =
        new Dictionary<string, string> { ["easy"] = "简单", ["medium"] = "中等", ["hard"] = "困难", ["expert"] = "专家" };

    private static readonly IReadOnlyDictionary<string, int> TargetRemovals =
        new Dictionary<string, int> { ["easy"] = 40, ["medium"] = 48, ["hard"] = 53, ["expert"] = 57 };

    public static int[,] Clone(int[,] g)
    {
        var n = new int[9, 9];
        Array.Copy(g, n, 81);
        return n;
    }

    private static T[] Shuffle<T>(T[] arr)
    {
        var a = (T[])arr.Clone();
        for (int i = a.Length - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
        return a;
    }

    public static bool CanPlace(int[,] g, int r, int c, int v)
    {
        for (int i = 0; i < 9; i++)
        {
            if (g[r, i] == v) return false;
            if (g[i, c] == v) return false;
        }
        int br = r / 3 * 3, bc = c / 3 * 3;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                if (g[br + i, bc + j] == v) return false;
        return true;
    }

    public static int[,] GenerateSolved()
    {
        var g = new int[9, 9];
        Fill(g);
        return g;
    }

    private static bool Fill(int[,] g)
    {
        int best = -1, bestR = -1, bestC = -1;
        int[]? bestCands = null;
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                if (g[r, c] != 0) continue;
                var cands = new List<int>();
                for (int v = 1; v <= 9; v++) if (CanPlace(g, r, c, v)) cands.Add(v);
                if (cands.Count == 0) return false;
                if (best == -1 || cands.Count < best)
                {
                    best = cands.Count; bestR = r; bestC = c; bestCands = cands.ToArray();
                    if (best == 1) break;
                }
            }
            if (best == 1) break;
        }
        if (best == -1) return true;

        foreach (var v in Shuffle(bestCands!))
        {
            g[bestR, bestC] = v;
            if (Fill(g)) return true;
            g[bestR, bestC] = 0;
        }
        return false;
    }

    public static int CountSolutions(int[,] grid, int limit = 2)
    {
        var g = Clone(grid);
        return SolveCount(g, limit);
    }

    private static int SolveCount(int[,] g, int limit)
    {
        int best = -1, br = -1, bc = -1;
        for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
            {
                if (g[r, c] != 0) continue;
                int cnt = 0;
                for (int v = 1; v <= 9; v++) if (CanPlace(g, r, c, v)) cnt++;
                if (cnt == 0) return 0;
                if (best == -1 || cnt < best) { best = cnt; br = r; bc = c; }
            }
        if (best == -1) return 1;

        int total = 0;
        for (int v = 1; v <= 9; v++)
        {
            if (!CanPlace(g, br, bc, v)) continue;
            g[br, bc] = v;
            total += SolveCount(g, limit);
            g[br, bc] = 0;
            if (total >= limit) return total;
        }
        return total;
    }

    // 候选位掩码：bit(v-1) 表示数字 v 可放（bits 0-8）
    public static int[,] GetCandidates(int[,] g)
    {
        var cands = new int[9, 9];
        for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
            {
                if (g[r, c] != 0) continue;
                int mask = 0;
                for (int v = 1; v <= 9; v++) if (CanPlace(g, r, c, v)) mask |= 1 << (v - 1);
                cands[r, c] = mask;
            }
        return cands;
    }

    private static int BitCount(int mask)
    {
        int n = 0;
        while (mask != 0) { mask &= mask - 1; n++; }
        return n;
    }

    private static int MaskToDigit(int mask) => System.Numerics.BitOperations.TrailingZeroCount((uint)mask) + 1;

    private sealed record Unit(string Type, (int R, int C)[] Cells);

    private static Unit[] BuildUnits()
    {
        var units = new List<Unit>();
        for (int u = 0; u < 9; u++)
        {
            var row = new (int, int)[9];
            var col = new (int, int)[9];
            for (int i = 0; i < 9; i++) { row[i] = (u, i); col[i] = (i, u); }
            units.Add(new Unit("row", row));
            units.Add(new Unit("col", col));
            int br = u / 3 * 3, bc = u % 3 * 3;
            var box = new (int, int)[9];
            int k = 0;
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) box[k++] = (br + i, bc + j);
            units.Add(new Unit("box", box));
        }
        return units.ToArray();
    }

    private static string UnitLabel(string t) => t switch { "row" => "行", "col" => "列", _ => "宫" };

    public static LogicStep? ScanUnits(int[,] g, int[,] cands, int levelFilter = 4)
    {
        var units = BuildUnits();
        foreach (var unit in units)
        {
            if (levelFilter >= 1)
            {
                foreach (var (r, c) in unit.Cells)
                    if (g[r, c] == 0 && BitCount(cands[r, c]) == 1)
                    {
                        int v = MaskToDigit(cands[r, c]);
                        return new LogicStep(1, "裸单元 (Naked Single)", r, c, v,
                            $"({r + 1},{c + 1}) 这一格只剩一个数字 {v} 可以填。");
                    }
            }
            if (levelFilter >= 2)
            {
                for (int v = 1; v <= 9; v++)
                {
                    int bit = 1 << (v - 1);
                    (int R, int C) pos = default; int count = 0;
                    foreach (var (r, c) in unit.Cells)
                        if (g[r, c] == 0 && (cands[r, c] & bit) != 0) { pos = (r, c); count++; }
                    if (count == 1)
                        return new LogicStep(2, "隐单元 (Hidden Single)", pos.R, pos.C, v,
                            $"在{UnitLabel(unit.Type)}内，数字 {v} 只有 ({pos.R + 1},{pos.C + 1}) 这一个位置可放。");
                }
            }
        }

        if (levelFilter >= 3)
        {
            for (int b = 0; b < 9; b++)
            {
                int br = b / 3 * 3, bc = b % 3 * 3;
                for (int v = 1; v <= 9; v++)
                {
                    int bit = 1 << (v - 1);
                    var rows = new HashSet<int>(); var cols = new HashSet<int>();
                    for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
                    {
                        int r = br + i, c = bc + j;
                        if (g[r, c] == 0 && (cands[r, c] & bit) != 0) { rows.Add(r); cols.Add(c); }
                    }
                    if (rows.Count == 0) continue;
                    if (rows.Count == 1)
                    {
                        int r = System.Linq.Enumerable.First(rows);
                        for (int c = 0; c < 9; c++)
                            if (c / 3 != bc / 3 && g[r, c] == 0 && (cands[r, c] & bit) != 0)
                                return new LogicStep(3, "区块排除 (Pointing)", r, c, v,
                                    $"宫内数字 {v} 的候选都在第 {r + 1} 行，因此可排除该行其他宫的 {v}。");
                    }
                    if (cols.Count == 1)
                    {
                        int c = System.Linq.Enumerable.First(cols);
                        for (int r = 0; r < 9; r++)
                            if (r / 3 != br / 3 && g[r, c] == 0 && (cands[r, c] & bit) != 0)
                                return new LogicStep(3, "区块排除 (Claiming)", r, c, v,
                                    $"宫内数字 {v} 的候选都在第 {c + 1} 列，因此可排除该列其他宫的 {v}。");
                    }
                }
            }
        }

        if (levelFilter >= 4)
        {
            foreach (var unit in units)
                for (int a = 0; a < unit.Cells.Length; a++)
                    for (int b = a + 1; b < unit.Cells.Length; b++)
                    {
                        var (r1, c1) = unit.Cells[a]; var (r2, c2) = unit.Cells[b];
                        if (g[r1, c1] != 0 || g[r2, c2] != 0) continue;
                        int m1 = cands[r1, c1], m2 = cands[r2, c2];
                        if (m1 == m2 && BitCount(m1) == 2)
                        {
                            foreach (var (r, c) in unit.Cells)
                            {
                                if ((r == r1 && c == c1) || (r == r2 && c == c2)) continue;
                                if (g[r, c] == 0 && (cands[r, c] & m1) != 0)
                                {
                                    int v = MaskToDigit(m1 & -m1);
                                    return new LogicStep(4, "裸对 (Naked Pair)", r, c, v,
                                        $"({r1 + 1},{c1 + 1}) 与 ({r2 + 1},{c2 + 1}) 构成裸对，可排除同{UnitLabel(unit.Type)}内其他格的候选。");
                                }
                            }
                        }
                    }
        }
        return null;
    }

    public static int Grade(int[,] grid)
    {
        var g = Clone(grid);
        var cands = GetCandidates(g);
        int maxLevel = 0;
        bool progress = true;
        while (progress)
        {
            progress = false;
            var step = ScanUnits(g, cands, 4);
            if (step != null)
            {
                g[step.Row, step.Col] = step.Value;
                maxLevel = Math.Max(maxLevel, step.Level);
                cands = GetCandidates(g);
                progress = true;
            }
        }
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++) if (g[r, c] == 0) return 5;
        return maxLevel;
    }

    public static Puzzle MakePuzzle(string difficulty)
    {
        if (!TargetRemovals.ContainsKey(difficulty)) difficulty = "medium";
        var solution = GenerateSolved();
        var puzzle = Clone(solution);

        var positions = new (int R, int C, int MR, int MC)[81];
        for (int i = 0; i < 81; i++)
        {
            int r = i / 9, c = i % 9;
            positions[i] = (r, c, 8 - r, 8 - c);
        }

        int target = TargetRemovals[difficulty];
        int removed = 0;
        foreach (var (r, c, mr, mc) in Shuffle(positions))
        {
            if (removed >= target) break;
            if (puzzle[r, c] == 0) continue;
            int backup1 = puzzle[r, c], backup2 = puzzle[mr, mc];
            bool pair = mr != r || mc != c;
            puzzle[r, c] = 0;
            if (pair) puzzle[mr, mc] = 0;
            if (CountSolutions(puzzle, 2) != 1)
            {
                puzzle[r, c] = backup1;
                if (pair) puzzle[mr, mc] = backup2;
            }
            else removed += pair ? 2 : 1;
        }

        return new Puzzle(puzzle, solution, difficulty, 81 - removed, Grade(puzzle));
    }

    public static int[,] Solve(int[,] grid)
    {
        var g = Clone(grid);
        SolveRec(g);
        return g;
    }

    private static bool SolveRec(int[,] g)
    {
        int br = -1, bc = -1, best = -1;
        for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
            {
                if (g[r, c] != 0) continue;
                int cnt = 0;
                for (int v = 1; v <= 9; v++) if (CanPlace(g, r, c, v)) cnt++;
                if (cnt == 0) return false;
                if (best == -1 || cnt < best) { best = cnt; br = r; bc = c; }
            }
        if (best == -1) return true;
        foreach (var v in Shuffle(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }))
        {
            if (!CanPlace(g, br, bc, v)) continue;
            g[br, bc] = v;
            if (SolveRec(g)) return true;
            g[br, bc] = 0;
        }
        return false;
    }
}