using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// A painted plywood figure on a hinge, like a fairground knockdown plate: a bullet flips it over backwards.
    /// It carries both paint jobs (bandit and townsperson) and <see cref="StandUp"/> picks one, so the moving targets in
    /// Ambush can be reused.
    /// </summary>
    public sealed class CutoutTarget : KnockdownTarget
    {
        public const int BanditPoints = 150;
        public const int InnocentPenalty = 300;
        public const float FallTime = 0.28f;
        public const float FallAngle = 86f;

        /// <summary>Height of the chest aim point above the base.</summary>
        public const float ChestHeight = 1.05f;
        /// <summary>Overall height including the hat.</summary>
        public const float FigureHeight = 1.9f;

        Transform pivot, banditLook, townsLook;
        float fall;     // 0 upright .. 1 flat
        bool falling;

        /// <summary>True once the fall animation has finished.</summary>
        public bool Flat => fall >= 1f;

        public override Vector3 AimPoint => pivot.TransformPoint(new Vector3(0f, ChestHeight, 0f));

        public static CutoutTarget Create(Transform parent, Vector3 localPos, ArtSet art, bool innocent, Quaternion? localRot = null)
        {
            var root = Shapes.Group(parent, "Cutout", localPos, localRot);
            var pivot = Shapes.Group(root, "Hinge", Vector3.zero);
            Shapes.Box(pivot, "Post", new Vector3(0f, 0.15f, 0.04f), new Vector3(0.08f, 0.3f, 0.04f), art.darkWood);

            var cut = root.gameObject.AddComponent<CutoutTarget>();
            cut.pivot = pivot;
            cut.banditLook = BuildBandit(pivot, art);
            cut.townsLook = BuildTownsfolk(pivot, art);

            // A kinematic body makes the whole figure one moving physics object instead of loose static colliders.
            var rb = root.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;

            cut.StandUp(innocent);
            return cut;
        }

        static Transform BuildBandit(Transform pivot, ArtSet art)
        {
            var g = Shapes.Group(pivot, "Bandit", Vector3.zero);
            const float front = -0.035f;
            Shapes.Box(g, "Body", new Vector3(0f, 0.8f, 0f), new Vector3(0.62f, 1.0f, 0.05f), art.banditCloth);
            Shapes.Box(g, "ArmL", new Vector3(-0.38f, 0.82f, 0f), new Vector3(0.14f, 0.7f, 0.05f), art.banditCloth, Hitbox.Box, Quaternion.Euler(0f, 0f, -8f));
            Shapes.Box(g, "ArmR", new Vector3(0.38f, 0.82f, 0f), new Vector3(0.14f, 0.7f, 0.05f), art.banditCloth, Hitbox.Box, Quaternion.Euler(0f, 0f, 8f));
            Shapes.Box(g, "Belt", new Vector3(0f, 0.5f, front), new Vector3(0.63f, 0.08f, 0.01f), art.darkWood, Hitbox.None);
            Shapes.Box(g, "Buckle", new Vector3(0f, 0.5f, front - 0.006f), new Vector3(0.09f, 0.07f, 0.01f), art.brass, Hitbox.None);
            Shapes.Box(g, "Holster", new Vector3(0.22f, 0.4f, front), new Vector3(0.1f, 0.2f, 0.01f), art.darkWood, Hitbox.None);
            Head(g, art);
            Shapes.Box(g, "Bandana", new Vector3(0f, 1.38f, front - 0.004f), new Vector3(0.4f, 0.16f, 0.012f), art.bandana, Hitbox.None);
            Shapes.Box(g, "EyeL", new Vector3(-0.08f, 1.5f, front - 0.004f), new Vector3(0.05f, 0.05f, 0.01f), art.ink, Hitbox.None);
            Shapes.Box(g, "EyeR", new Vector3(0.08f, 1.5f, front - 0.004f), new Vector3(0.05f, 0.05f, 0.01f), art.ink, Hitbox.None);
            Shapes.Box(g, "Brow", new Vector3(0f, 1.56f, front - 0.004f), new Vector3(0.26f, 0.03f, 0.01f), art.ink, Hitbox.None);
            Shapes.Box(g, "Brim", new Vector3(0f, 1.66f, 0f), new Vector3(0.66f, 0.05f, 0.06f), art.banditHat);
            Shapes.Box(g, "Crown", new Vector3(0f, 1.78f, 0f), new Vector3(0.36f, 0.22f, 0.06f), art.banditHat);
            return g;
        }

        static Transform BuildTownsfolk(Transform pivot, ArtSet art)
        {
            var g = Shapes.Group(pivot, "Townsfolk", Vector3.zero);
            const float front = -0.035f;
            Shapes.Box(g, "Bodice", new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 0.6f, 0.05f), art.townsfolkDress);
            Shapes.Box(g, "Skirt", new Vector3(0f, 0.48f, 0f), new Vector3(0.74f, 0.42f, 0.05f), art.townsfolkDress);
            Shapes.Box(g, "Apron", new Vector3(0f, 0.55f, front), new Vector3(0.36f, 0.5f, 0.01f), art.townsfolkHat, Hitbox.None);
            Shapes.Box(g, "ArmL", new Vector3(-0.32f, 0.95f, 0f), new Vector3(0.12f, 0.55f, 0.05f), art.townsfolkDress, Hitbox.Box, Quaternion.Euler(0f, 0f, -10f));
            Shapes.Box(g, "ArmR", new Vector3(0.32f, 0.95f, 0f), new Vector3(0.12f, 0.55f, 0.05f), art.townsfolkDress, Hitbox.Box, Quaternion.Euler(0f, 0f, 10f));
            Head(g, art);
            Shapes.Box(g, "EyeL", new Vector3(-0.08f, 1.48f, front - 0.004f), new Vector3(0.04f, 0.06f, 0.01f), art.ink, Hitbox.None);
            Shapes.Box(g, "EyeR", new Vector3(0.08f, 1.48f, front - 0.004f), new Vector3(0.04f, 0.06f, 0.01f), art.ink, Hitbox.None);
            Shapes.Box(g, "Smile", new Vector3(0f, 1.36f, front - 0.004f), new Vector3(0.14f, 0.025f, 0.01f), art.bandana, Hitbox.None);
            Shapes.Box(g, "Bonnet", new Vector3(0f, 1.62f, 0f), new Vector3(0.52f, 0.2f, 0.06f), art.townsfolkHat);
            Shapes.Box(g, "BonnetSideL", new Vector3(-0.23f, 1.46f, 0f), new Vector3(0.08f, 0.3f, 0.06f), art.townsfolkHat);
            Shapes.Box(g, "BonnetSideR", new Vector3(0.23f, 1.46f, 0f), new Vector3(0.08f, 0.3f, 0.06f), art.townsfolkHat);
            return g;
        }

        static void Head(Transform g, ArtSet art) =>
            Shapes.Cylinder(g, "Head", new Vector3(0f, 1.45f, 0f), 0.42f, 0.05f, art.skin, Hitbox.Box, Quaternion.Euler(90f, 0f, 0f));

        public override void TakeHit(Vector3 point, Vector3 dir)
        {
            if (IsDown) return;
            falling = true;
            if (Game != null) Game.Sfx.Thunk();
            KnockDown();
        }

        /// <summary>Stands the figure back up as a bandit or a townsperson and makes it scorable again.</summary>
        public void StandUp(bool asInnocent)
        {
            innocent = asInnocent;
            points = asInnocent ? 0 : BanditPoints;
            banditLook.gameObject.SetActive(!asInnocent);
            townsLook.gameObject.SetActive(asInnocent);
            falling = false;
            fall = 0f;
            pivot.localRotation = Quaternion.identity;
            Revive();
        }

        protected override void Update()
        {
            base.Update();
            if (!falling || fall >= 1f) return;
            fall = Mathf.MoveTowards(fall, 1f, Time.deltaTime / FallTime);
            float eased = fall * fall; // accelerates like something toppling
            pivot.localRotation = Quaternion.Euler(eased * FallAngle, 0f, 0f);
        }
    }
}
