using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace CatDetective.Systems
{
    /// <summary>
    /// Tap-to-walk navigation for one room, built at LoadRoom from the Tiled
    /// Collisions rects.
    ///
    /// A cell is walkable when the cat's feet box, centred on the cell centre
    /// (bottom-center foot point), fits without touching any solid rect and stays
    /// on the canvas. The box uses the cat's WIDEST facing (+1 px), so any path
    /// the grid accepts is walkable in every facing.
    ///
    /// Costs are travel TIME, not distance: the cat walks vertically at half its
    /// horizontal speed (isometric foreshortening), so a vertical step costs 2
    /// and a diagonal sqrt(5). Paths are smoothed by line-of-sight string
    /// pulling, so the cat walks a few straight legs instead of cell zigzags.
    ///
    /// `--screenshot ui navtest [case]` walks the real cat with this grid to
    /// every target from every spawn (Game1.TapControls.cs). Stuck spots are
    /// fixed in the room's Tiled Collisions layer, never with per-room code here.
    /// </summary>
    public sealed class NavGrid
    {
        public const int CELL = 8;

        private readonly List<Rectangle> _solids;
        private readonly int    _w, _h, _halfW, _boxH, _cols, _rows;
        private readonly bool[] _walk;
        private readonly int[]  _comp;   // connected-component label, -1 = blocked

        public NavGrid(List<Rectangle> solids, int canvasW, int canvasH, int halfW, int boxH)
        {
            _solids = solids;
            _w = canvasW; _h = canvasH;
            _halfW = halfW; _boxH = boxH;
            _cols = Math.Max(1, canvasW / CELL);
            _rows = Math.Max(1, canvasH / CELL);
            _walk = new bool[_cols * _rows];
            _comp = new int[_cols * _rows];

            for (int i = 0; i < _walk.Length; i++)
                _walk[i] = Fits(CellCenter(i));
            LabelComponents();
        }

        // ── Geometry ──────────────────────────────────────────────────────────

        private Vector2 CellCenter(int i) =>
            new((i % _cols) * CELL + CELL * 0.5f, (i / _cols) * CELL + CELL * 0.5f);

        private int CellAt(Vector2 p)
        {
            int cx = Math.Clamp((int)(p.X / CELL), 0, _cols - 1);
            int cy = Math.Clamp((int)(p.Y / CELL), 0, _rows - 1);
            return cy * _cols + cx;
        }

        private Rectangle FootBox(Vector2 foot, int halfW) =>
            new((int)(foot.X - halfW), (int)(foot.Y - _boxH), halfW * 2, _boxH);

        /// <summary>True when the (widest) cat feet box fits at this foot point.</summary>
        public bool Fits(Vector2 foot)
        {
            if (foot.X - _halfW < 0 || foot.X + _halfW > _w ||
                foot.Y - _boxH < 0 || foot.Y > _h)
                return false;
            // +1 px above and below too: the cat's float Y truncates to an int
            // box, so a path hugging a wall must not touch it after rounding.
            var box = FootBox(foot, _halfW);
            box.Inflate(0, 1);
            foreach (var s in _solids)
                if (box.Intersects(s)) return false;
            return true;
        }

        private void LabelComponents()
        {
            Array.Fill(_comp, -1);
            int label = 0;
            var queue = new Queue<int>();
            for (int i = 0; i < _walk.Length; i++)
            {
                if (!_walk[i] || _comp[i] >= 0) continue;
                _comp[i] = label;
                queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    int c = queue.Dequeue();
                    foreach (var (n, _) in Neighbors(c))
                    {
                        if (_comp[n] >= 0) continue;
                        _comp[n] = label;
                        queue.Enqueue(n);
                    }
                }
                label++;
            }
        }

        private static readonly float DIAG = MathF.Sqrt(5f);

        /// <summary>8-connected walkable neighbours; diagonals never cut a corner.</summary>
        private IEnumerable<(int Cell, float Cost)> Neighbors(int c)
        {
            int cx = c % _cols, cy = c / _cols;
            bool W(int x, int y) => x >= 0 && y >= 0 && x < _cols && y < _rows && _walk[y * _cols + x];

            bool l = W(cx - 1, cy), r = W(cx + 1, cy), u = W(cx, cy - 1), d = W(cx, cy + 1);
            if (l) yield return (c - 1, 1f);
            if (r) yield return (c + 1, 1f);
            if (u) yield return (c - _cols, 2f);
            if (d) yield return (c + _cols, 2f);
            if (l && u && W(cx - 1, cy - 1)) yield return (c - _cols - 1, DIAG);
            if (r && u && W(cx + 1, cy - 1)) yield return (c - _cols + 1, DIAG);
            if (l && d && W(cx - 1, cy + 1)) yield return (c + _cols - 1, DIAG);
            if (r && d && W(cx + 1, cy + 1)) yield return (c + _cols + 1, DIAG);
        }

        /// <summary>Travel-time metric (vertical counts double); admissible A* heuristic.</summary>
        private static float TimeDist(Vector2 a, Vector2 b)
        {
            float dx = (b.X - a.X) / CELL, dy = 2f * (b.Y - a.Y) / CELL;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Nearest walkable cell to p (optionally within one component), or -1.</summary>
        private int NearestWalkable(Vector2 p, int comp = -1)
        {
            int c = CellAt(p);
            if (_walk[c] && (comp < 0 || _comp[c] == comp)) return c;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < _walk.Length; i++)
            {
                if (!_walk[i] || (comp >= 0 && _comp[i] != comp)) continue;
                float d = Vector2.DistanceSquared(CellCenter(i), p);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private int StartCell(Vector2 from) => NearestWalkable(from);

        // ── Queries ───────────────────────────────────────────────────────────

        /// <summary>
        /// Path from the cat's foot point to a tapped point. If the point is
        /// blocked (inside furniture, off the floor) or on a cut-off patch of
        /// floor, the path ends at the nearest walkable spot the cat can actually
        /// REACH. Null only if the cat is somewhere with no walkable floor at all.
        /// </summary>
        public List<Vector2>? PathTo(Vector2 from, Vector2 to)
        {
            int start = StartCell(from);
            if (start < 0) return null;
            int goal = NearestWalkable(to, _comp[start]);
            if (goal < 0) return null;

            var cells = Search(start, c => c == goal, CellCenter(goal));
            if (cells == null) return null;

            // End on the exact tapped point when the cat fits there.
            Vector2 end = CellCenter(goal);
            if (_walk[CellAt(to)] && _comp[CellAt(to)] == _comp[start] &&
                Fits(to) && LineClear(end, to))
                end = to;
            return Smooth(from, cells, end);
        }

        /// <summary>
        /// Path to the walk-nearest reachable spot where a cat feet box of
        /// <paramref name="probeHalfW"/> overlaps <paramref name="zone"/> (an
        /// interactable's InteractZone or a doorway's trigger rect). Null when no
        /// reachable spot touches the zone.
        /// </summary>
        public List<Vector2>? PathToZone(Vector2 from, Rectangle zone, int probeHalfW)
        {
            int start = StartCell(from);
            if (start < 0) return null;
            // Overlap by a few px, not edge-touching: the real box is int-truncated
            // and Rectangle.Intersects is strict, so a cell that merely touches
            // the zone can leave the cat "next to" it but out of reach.
            var inner = zone;
            if (inner.Width > 10 && inner.Height > 10) inner.Inflate(-3, -3);
            var cells = Search(start,
                c => FootBox(CellCenter(c), probeHalfW).Intersects(inner), null);
            if (cells == null) return null;
            return Smooth(from, cells, CellCenter(cells[^1]));
        }

        /// <summary>A* (Dijkstra when target is null) over walkable cells.</summary>
        private List<int>? Search(int start, Func<int, bool> isGoal, Vector2? target)
        {
            var g      = new Dictionary<int, float> { [start] = 0f };
            var parent = new Dictionary<int, int>();
            var open   = new PriorityQueue<int, float>();
            open.Enqueue(start, 0f);
            var closed = new HashSet<int>();

            while (open.Count > 0)
            {
                int c = open.Dequeue();
                if (!closed.Add(c)) continue;
                if (isGoal(c))
                {
                    var path = new List<int> { c };
                    while (parent.TryGetValue(c, out int p)) { c = p; path.Add(c); }
                    path.Reverse();
                    return path;
                }
                foreach (var (n, cost) in Neighbors(c))
                {
                    if (closed.Contains(n)) continue;
                    float ng = g[c] + cost;
                    if (g.TryGetValue(n, out float old) && old <= ng) continue;
                    g[n] = ng;
                    parent[n] = c;
                    float h = target.HasValue ? TimeDist(CellCenter(n), target.Value) : 0f;
                    open.Enqueue(n, ng + h);
                }
            }
            return null;
        }

        /// <summary>
        /// Every 4 px along a→b the widest cat box fits. Samples at the start
        /// that do not fit are skipped: the cat may stand where only its
        /// narrower facing fits (spawn points hugging a wall).
        /// </summary>
        public bool LineClear(Vector2 a, Vector2 b)
        {
            float len = Vector2.Distance(a, b);
            int steps = Math.Max(1, (int)(len / 4f));
            bool leftStart = false;
            for (int i = 0; i <= steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)steps);
                bool fits = Fits(p);
                if (!fits && leftStart) return false;
                if (fits) leftStart = true;
            }
            return leftStart;
        }

        /// <summary>Greedy string pull: keep only the corners line of sight needs.</summary>
        private List<Vector2> Smooth(Vector2 from, List<int> cells, Vector2 end)
        {
            var pts = new List<Vector2>(cells.Count + 1);
            foreach (int c in cells) pts.Add(CellCenter(c));
            pts[^1] = end;

            var result = new List<Vector2>();
            Vector2 anchor = from;
            int i = 0;
            while (i < pts.Count)
            {
                int j = i;
                while (j + 1 < pts.Count && LineClear(anchor, pts[j + 1])) j++;
                result.Add(pts[j]);
                anchor = pts[j];
                i = j + 1;
            }
            return result;
        }
    }
}
