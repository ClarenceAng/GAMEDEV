using System.Collections.Generic;
using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// One version of the range. The game builds a fresh one for every round; it lays out the scenery and targets
    /// under <see cref="Root"/> and runs anything that moves in <see cref="Tick"/>.
    /// </summary>
    public abstract class Level
    {
        protected readonly KnockdownGame game;
        protected readonly ArtSet art;
        protected readonly System.Random rng;

        public Transform Root { get; }

        /// <summary>Every target currently in play (tossed cans come and go).</summary>
        public readonly List<KnockdownTarget> Targets = new List<KnockdownTarget>();

        protected Level(KnockdownGame game, Transform root, int seed)
        {
            this.game = game;
            art = game.art;
            Root = root;
            rng = new System.Random(seed);
        }

        /// <summary>True when targets keep coming (the round only ends on time). False when the range can be cleared.</summary>
        public abstract bool Endless { get; }

        public int DownCount
        {
            get
            {
                int n = 0;
                foreach (var t in Targets) if (t != null && t.IsDown) n++;
                return n;
            }
        }

        public bool AllDown => !Endless && Targets.Count > 0 && DownCount == Targets.Count;

        /// <summary>Runs the level for one frame. <paramref name="playing"/> is false on the title and results screens.</summary>
        public virtual void Tick(float dt, bool playing) => Targets.RemoveAll(t => t == null);

        protected T Add<T>(T target) where T : KnockdownTarget
        {
            target.Game = game;
            Targets.Add(target);
            return target;
        }

        protected float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        protected bool Chance(float p) => rng.NextDouble() < p;
    }
}
