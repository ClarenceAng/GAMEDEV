using System.Collections.Generic;
using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// Version 2, moving targets. Nothing holds still:
    ///  - figures pop up from behind four barrels (10.5 m) for a moment and duck back down,
    ///  - three bottles swing on ropes from a gallows beam (13.5 m),
    ///  - a mine cart rolls across on rails carrying a figure (15.5 m),
    ///  - two figures pace back and forth inside the saloon, only visible through the door and windows (19 m),
    ///  - cans get tossed up from behind the fence for trick shots.
    /// Some figures are townsfolk: shooting one costs points and your streak. The round only ends on time.
    /// </summary>
    public sealed class AmbushLevel : Level
    {
        // Share of figures that turn out to be townsfolk.
        public const float InnocentChance = 0.25f;

        const float PopZ = 11.1f;
        const float PopHidden = -1.8f;
        const float PopRiseTime = 0.18f;
        const float PopHold = 1.7f;
        const float CartZ = 15.5f, CartSpan = 15f, CartSpeed = 3.6f;
        const float SaloonZ = 19.5f;
        const float BottleRespawn = 2.5f;
        const float FigureRespawn = 1.2f;

        enum PopPhase { Hidden, Rising, Up, Lowering }

        sealed class PopUp
        {
            public CutoutTarget fig;
            public PopPhase phase;
            public float timer, height;
        }

        sealed class Pendulum
        {
            public Transform pivot;
            public BottleTarget bottle;
            public float amplitude, speed, phase, brokenFor;
        }

        sealed class Pacer
        {
            public CutoutTarget fig;
            public float x, dir, speed, downFor;
        }

        readonly List<PopUp> popUps = new List<PopUp>();
        readonly List<Pendulum> pendulums = new List<Pendulum>();
        readonly List<Pacer> pacers = new List<Pacer>();
        Transform cart;
        CutoutTarget cartFig;
        float cartX, cartDir, cartWait;
        float tossWait;
        float clock;

        public override bool Endless => true;

        public AmbushLevel(KnockdownGame game, Transform root, int seed) : base(game, root, seed)
        {
            Frontier.Desert(root, art);
            Frontier.Booth(root, art);
            Frontier.Fence(root, art, 7f, 5f);

            // Pop-up figures hide behind barrels.
            foreach (float x in new[] { -5f, -1.7f, 1.7f, 5f })
            {
                Frontier.Barrel(root, art, new Vector3(x, 0f, 10.5f));
                var fig = Add(CutoutTarget.Create(root, new Vector3(x, PopHidden, PopZ), art, Chance(InnocentChance)));
                popUps.Add(new PopUp { fig = fig, phase = PopPhase.Hidden, timer = Range(0.4f, 2.5f), height = PopHidden });
            }

            // Swinging bottles under a gallows beam.
            var frame = Shapes.Group(root, "Gallows", new Vector3(0f, 0f, 13.5f));
            foreach (float x in new[] { -3f, 3f }) Shapes.Box(frame, "Post", new Vector3(x, 1.75f, 0f), new Vector3(0.2f, 3.5f, 0.2f), art.darkWood);
            Shapes.Box(frame, "Beam", new Vector3(0f, 3.45f, 0f), new Vector3(6.4f, 0.2f, 0.2f), art.darkWood);
            float[] swingX = { -1.6f, 0f, 1.6f };
            for (int i = 0; i < swingX.Length; i++)
            {
                var pivot = Shapes.Group(frame, "Swing", new Vector3(swingX[i], 3.35f, 0f));
                const float rope = 1.1f;
                Shapes.Box(pivot, "Rope", new Vector3(0f, -rope * 0.5f, 0f), new Vector3(0.02f, rope, 0.02f), art.rope, Hitbox.None);
                var bottle = Add(BottleTarget.Create(pivot, Vector3.zero, art, amber: i == 1, hanging: true));
                bottle.transform.localPosition = new Vector3(0f, -rope - BottleTarget.Height, 0f);
                bottle.transform.localRotation = Quaternion.identity;
                pendulums.Add(new Pendulum { pivot = pivot, bottle = bottle, amplitude = Range(22f, 32f), speed = Range(1.6f, 2.4f), phase = Range(0f, 6.3f) });
            }

            // Mine cart track across the range.
            var track = Shapes.Group(root, "Track", new Vector3(0f, 0f, CartZ));
            foreach (float dz in new[] { -0.45f, 0.45f }) Shapes.Box(track, "Rail", new Vector3(0f, 0.06f, dz), new Vector3(CartSpan * 2f + 6f, 0.08f, 0.07f), art.iron);
            for (float x = -CartSpan - 3f; x <= CartSpan + 3f; x += 1.2f) Shapes.Box(track, "Sleeper", new Vector3(x, 0.02f, 0f), new Vector3(0.25f, 0.06f, 1.3f), art.darkWood);
            cart = Shapes.Group(root, "Cart", new Vector3(-CartSpan - 2f, 0f, CartZ));
            Shapes.Box(cart, "Tub", new Vector3(0f, 0.5f, 0f), new Vector3(1.5f, 0.6f, 1.0f), art.iron);
            Shapes.Box(cart, "Trim", new Vector3(0f, 0.82f, 0f), new Vector3(1.6f, 0.06f, 1.1f), art.darkWood, Hitbox.None);
            foreach (float wx in new[] { -0.5f, 0.5f })
                foreach (float wz in new[] { -0.45f, 0.45f })
                    Shapes.Cylinder(cart, "Wheel", new Vector3(wx, 0.2f, wz), 0.36f, 0.06f, art.darkWood, Hitbox.None, Quaternion.Euler(90f, 0f, 0f));
            cartFig = Add(CutoutTarget.Create(cart, new Vector3(0f, 0.85f, 0f), art, Chance(InnocentChance)));
            cartDir = 1f;
            cartX = -CartSpan - 2f;
            cartWait = Range(0.5f, 2f);

            // The saloon: figures pace behind the facade and show through the openings.
            var openings = new[]
            {
                new Frontier.Opening(-4.5f, 1.6f, 1.4f, 2.8f),
                new Frontier.Opening(-1.5f, 1.2f, 1.4f, 2.8f),
                new Frontier.Opening(1.5f, 1.4f, Frontier.PorchHeight, 3f),
                new Frontier.Opening(4.5f, 1.6f, 1.4f, 2.8f),
            };
            var saloon = Frontier.Saloon(root, art, new Vector3(0f, 0f, SaloonZ), 14f, openings);
            Frontier.Poster(saloon, art, new Vector3(-3f, 2.1f, -0.01f));
            Frontier.Poster(saloon, art, new Vector3(3f, 2.1f, -0.01f));
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -4f : 3f;
                var fig = Add(CutoutTarget.Create(saloon, new Vector3(x, Frontier.PorchHeight, 1.1f + i * 0.9f), art, Chance(InnocentChance)));
                pacers.Add(new Pacer { fig = fig, x = x, dir = i == 0 ? 1f : -1f, speed = i == 0 ? 2.2f : 3.1f });
            }

            Frontier.Crates(root, art, new Vector3(-7.5f, 0f, 9f), 20f);
            Frontier.Crates(root, art, new Vector3(7.8f, 0f, 12f), -35f);
            tossWait = Range(1f, 2f);
        }

        public override void Tick(float dt, bool playing)
        {
            base.Tick(dt, playing);
            clock += dt;
            SwingBottles(dt);
            PaceSaloon(dt);
            if (!playing) return; // pop-ups, the cart and tossed cans only come out once the round is on
            RunPopUps(dt);
            RunCart(dt);
            RunToss(dt);
        }

        void SwingBottles(float dt)
        {
            foreach (var p in pendulums)
            {
                p.pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(clock * p.speed + p.phase) * p.amplitude);
                if (!p.bottle.IsDown) continue;
                p.brokenFor += dt;
                if (p.brokenFor < BottleRespawn) continue;
                p.brokenFor = 0f;
                p.bottle.Restore();
            }
        }

        void PaceSaloon(float dt)
        {
            const float minX = -6.2f, maxX = 6.2f;
            foreach (var p in pacers)
            {
                if (p.fig.IsDown)
                {
                    p.downFor += dt;
                    if (p.downFor < FigureRespawn) continue;
                    p.downFor = 0f;
                    p.fig.StandUp(Chance(InnocentChance));
                }
                p.x += p.dir * p.speed * dt;
                if (p.x > maxX) { p.x = maxX; p.dir = -1f; }
                if (p.x < minX) { p.x = minX; p.dir = 1f; }
                var lp = p.fig.transform.localPosition;
                p.fig.transform.localPosition = new Vector3(p.x, lp.y, lp.z);
            }
        }

        void RunPopUps(float dt)
        {
            foreach (var p in popUps)
            {
                p.timer -= dt;
                switch (p.phase)
                {
                    case PopPhase.Hidden:
                        if (p.timer > 0f) break;
                        p.fig.StandUp(Chance(InnocentChance));
                        p.phase = PopPhase.Rising;
                        p.timer = PopRiseTime;
                        break;
                    case PopPhase.Rising:
                        p.height = Mathf.Lerp(0f, PopHidden, Mathf.Max(0f, p.timer) / PopRiseTime);
                        if (p.timer <= 0f) { p.phase = PopPhase.Up; p.timer = PopHold; p.height = 0f; }
                        break;
                    case PopPhase.Up:
                        // A shot figure lies flat for a moment before it drops away.
                        if (p.fig.IsDown && p.timer > 0.45f) p.timer = 0.45f;
                        if (p.timer <= 0f) { p.phase = PopPhase.Lowering; p.timer = PopRiseTime; }
                        break;
                    case PopPhase.Lowering:
                        p.height = Mathf.Lerp(PopHidden, 0f, Mathf.Max(0f, p.timer) / PopRiseTime);
                        if (p.timer <= 0f) { p.phase = PopPhase.Hidden; p.timer = Range(0.8f, 3f); p.height = PopHidden; }
                        break;
                }
                var lp = p.fig.transform.localPosition;
                p.fig.transform.localPosition = new Vector3(lp.x, p.height, lp.z);
            }
        }

        void RunCart(float dt)
        {
            if (cartWait > 0f)
            {
                cartWait -= dt;
                if (cartWait <= 0f) cartFig.StandUp(Chance(InnocentChance));
                return;
            }
            cartX += cartDir * CartSpeed * dt;
            cart.localPosition = new Vector3(cartX, 0f, CartZ);
            if (Mathf.Abs(cartX) < CartSpan + 2f) return;
            // Off the far side: wait, then come back the other way.
            cartX = Mathf.Sign(cartX) * (CartSpan + 2f);
            cartDir = -cartDir;
            cartWait = Range(1f, 2.5f);
        }

        void RunToss(float dt)
        {
            tossWait -= dt;
            if (tossWait > 0f) return;
            tossWait = Range(2.2f, 3.8f);
            TossCan();
        }

        /// <summary>Lobs a can up from behind the fence. Public so tests can make one on demand.</summary>
        public CanTarget TossCan()
        {
            var can = Add(CanTarget.Create(Root, new Vector3(Range(-3.5f, 3.5f), 0.05f, 8.6f), art));
            can.Toss(new Vector3(Range(-1.2f, 1.2f), Range(7.5f, 8.5f), Range(-0.7f, -0.3f)), Random.insideUnitSphere * 8f);
            return can;
        }

        /// <summary>Figures currently in view and scorable (for tests).</summary>
        public IEnumerable<CutoutTarget> VisibleFigures()
        {
            foreach (var p in popUps) if (p.phase == PopPhase.Up && p.fig.IsLive) yield return p.fig;
            if (cartWait <= 0f && cartFig.IsLive) yield return cartFig;
        }

        /// <summary>The figure riding the mine cart.</summary>
        public CutoutTarget CartFigure => cartFig;

        /// <summary>Starts the cart's next run right away (for tests).</summary>
        public void SendCartNow(bool innocent)
        {
            cartWait = 0f;
            cartFig.StandUp(innocent);
        }
    }
}
