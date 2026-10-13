using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    public enum DeathCause { None, Wall, Self }

    public struct StepResult
    {
        public bool Moved;
        public bool Ate;
        public bool Died;
        public DeathCause Cause;
    }

    /// <summary>
    /// The rules of the game with no GameObjects and no clock: grid, snake and apple. The controller calls
    /// <see cref="Step"/> on its own timer, which keeps this class fully unit-testable. Cells are (x, y); y maps to world +Z.
    /// Snake[0] is the head. Leaving the board is a <see cref="DeathCause.Wall"/> death.
    /// </summary>
    public sealed class SnakeSim
    {
        public readonly int Width;
        public readonly int Height;
        public readonly List<Vector2Int> Snake = new List<Vector2Int>();

        public Vector2Int Heading { get; private set; }
        public Vector2Int Apple { get; private set; }
        public bool HasApple { get; private set; }
        public int ApplesEaten { get; private set; }
        public bool Dead { get; private set; }
        public DeathCause Cause { get; private set; }

        public Vector2Int Head => Snake[0];

        readonly List<Vector2Int> turns = new List<Vector2Int>();
        readonly System.Random rng;

        public SnakeSim(int width, int height, int seed)
        {
            Width = width;
            Height = height;
            rng = new System.Random(seed);
            Reset();
        }

        public void Reset()
        {
            Snake.Clear();
            turns.Clear();
            ApplesEaten = 0;
            Dead = false;
            Cause = DeathCause.None;
            HasApple = false;
            Heading = Vector2Int.right;
            var c = new Vector2Int(Width / 2, Height / 2);
            for (int i = 0; i < 3; i++) Snake.Add(c - Vector2Int.right * i);
            SpawnApple();
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        public static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        // ------------------------------------------------------------------ snake

        /// <summary>Buffers a turn (max two ahead). Reversing and repeating the current direction are ignored.</summary>
        public void QueueTurn(Vector2Int dir)
        {
            if (Dead || dir == Vector2Int.zero) return;
            Vector2Int last = turns.Count > 0 ? turns[turns.Count - 1] : Heading;
            if (dir == last) return;
            if (dir.x + last.x == 0 && dir.y + last.y == 0) return;
            if (turns.Count < 2) turns.Add(dir);
        }

        public StepResult Step()
        {
            var r = new StepResult();
            if (Dead) return r;

            if (turns.Count > 0)
            {
                Heading = turns[0];
                turns.RemoveAt(0);
            }

            Vector2Int head = Snake[0] + Heading;
            if (!InBounds(head)) return Die(DeathCause.Wall, r);

            bool eats = HasApple && head == Apple;

            // The tail cell is vacated this step unless the snake is growing, so chasing your own tail is legal.
            int solid = eats ? Snake.Count : Snake.Count - 1;
            for (int i = 0; i < solid; i++)
                if (Snake[i] == head) return Die(DeathCause.Self, r);

            Snake.Insert(0, head);
            if (eats)
            {
                ApplesEaten++;
                HasApple = false;
                SpawnApple();
                r.Ate = true;
            }
            else Snake.RemoveAt(Snake.Count - 1);

            r.Moved = true;
            return r;
        }

        StepResult Die(DeathCause cause, StepResult r)
        {
            Dead = true;
            Cause = cause;
            r.Died = true;
            r.Cause = cause;
            return r;
        }

        // ------------------------------------------------------------------ apple

        bool IsFree(Vector2Int c) => InBounds(c) && !Snake.Contains(c);

        public bool SpawnApple()
        {
            var free = new List<Vector2Int>();
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (IsFree(c)) free.Add(c);
                }
            if (free.Count == 0) return false;
            Apple = free[rng.Next(free.Count)];
            HasApple = true;
            return true;
        }

        public bool PlaceApple(Vector2Int c)
        {
            if (!IsFree(c)) return false;
            Apple = c;
            HasApple = true;
            return true;
        }

        // ------------------------------------------------------------------ test hooks

        public void DebugSetSnake(IList<Vector2Int> cells, Vector2Int heading)
        {
            Snake.Clear();
            Snake.AddRange(cells);
            Heading = heading;
            turns.Clear();
        }
    }
}
