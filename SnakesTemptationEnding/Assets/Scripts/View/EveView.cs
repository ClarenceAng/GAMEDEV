using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// Eve: a stylised, procedurally built figure (dress, long hair, a flower crown) about two units tall, in the same
    /// chunky style as the snake. Arms are solved with a two-bone IK so her hand can reach the apple and then her mouth.
    /// Everything is a function of what <see cref="Pose"/> is told each frame.
    /// </summary>
    sealed class EveView
    {
        const float UpperLen = 0.40f, LowerLen = 0.38f;
        const float ShoulderX = 0.25f, ShoulderY = 1.50f;

        public readonly Transform Root;
        readonly Transform dress, head, jaw, footL, footR;
        readonly Transform[] upper = new Transform[2], lower = new Transform[2], hand = new Transform[2]; // 0 = left, 1 = right
        float yaw;

        /// <summary>Where her face is, in the garden's space (for the camera).</summary>
        public Vector3 FaceWorld => head.position + Root.up * 0.1f;
        public Vector3 MouthWorld => head.TransformPoint(new Vector3(0f, 0.05f, 0.2f));
        public Vector3 ShoulderWorld(bool right) => Root.TransformPoint(new Vector3(right ? ShoulderX : -ShoulderX, ShoulderY, 0f));
        public Vector3 RightHandWorld => hand[1].position;

        public EveView(Transform parent, GardenMaterials m)
        {
            Root = new GameObject("Eve").transform;
            Root.SetParent(parent, false);

            // dress: a long skirt that nips in at the waist, a fitted bodice, bare shoulders
            var dressMesh = ProcMesh.Lathe(
                new[] { 0.06f, 0.30f, 0.62f, 0.95f, 1.16f, 1.28f, 1.42f, 1.52f, 1.57f },
                new[] { 0.48f, 0.44f, 0.37f, 0.28f, 0.185f, 0.205f, 0.225f, 0.20f, 0.12f }, 32, "EveDress");
            dress = Prims.Make("Dress", dressMesh, m.dress, Root);
            var belt = Prims.Make("Belt", Prims.Cylinder, m.leafGreen, Root);
            belt.localPosition = new Vector3(0f, 1.17f, 0f);
            belt.localScale = new Vector3(0.40f, 0.025f, 0.40f);
            var neck = Prims.Make("Neck", Prims.Cylinder, m.skin, Root);
            neck.localPosition = new Vector3(0f, 1.62f, 0f);
            neck.localScale = new Vector3(0.12f, 0.1f, 0.12f);
            var chest = Prims.Make("Shoulders", Prims.Sphere, m.skin, Root);
            chest.localPosition = new Vector3(0f, 1.50f, 0f);
            chest.localScale = new Vector3(0.58f, 0.2f, 0.28f);

            // head (pivots at the neck)
            head = new GameObject("Head").transform;
            head.SetParent(Root, false);
            head.localPosition = new Vector3(0f, 1.70f, 0f);
            var skull = Prims.Make("Skull", Prims.Sphere, m.skin, head);
            skull.localPosition = new Vector3(0f, 0.15f, 0f);
            skull.localScale = new Vector3(0.37f, 0.42f, 0.39f);
            var nose = Prims.Make("Nose", Prims.Sphere, m.skin, head, false, false);
            nose.localPosition = new Vector3(0f, 0.12f, 0.2f);
            nose.localScale = new Vector3(0.05f, 0.06f, 0.06f);
            jaw = new GameObject("Jaw").transform;
            jaw.SetParent(head, false);
            jaw.localPosition = new Vector3(0f, 0.05f, 0.17f);
            var lip = Prims.Make("Mouth", Prims.Sphere, m.mouth, jaw, false, false);
            lip.localScale = new Vector3(0.085f, 0.028f, 0.03f);
            for (int side = -1; side <= 1; side += 2)
            {
                var eyeWhite = Prims.Make("EyeWhite", Prims.Sphere, m.dress, head, false, false);
                eyeWhite.localPosition = new Vector3(side * 0.075f, 0.19f, 0.165f);
                eyeWhite.localScale = new Vector3(0.062f, 0.05f, 0.04f);
                var iris = Prims.Make("Iris", Prims.Sphere, m.leafDark, head, false, false);
                iris.localPosition = new Vector3(side * 0.075f, 0.19f, 0.182f);
                iris.localScale = new Vector3(0.036f, 0.036f, 0.02f);
                var brow = Prims.Make("Brow", Prims.Cube, m.hair, head, false, false);
                brow.localPosition = new Vector3(side * 0.078f, 0.245f, 0.17f);
                brow.localScale = new Vector3(0.075f, 0.012f, 0.014f);
                brow.localRotation = Quaternion.Euler(0f, 0f, side * -8f);
                var cheek = Prims.Make("Cheek", Prims.Sphere, m.flowers[1], head, false, false);
                cheek.localPosition = new Vector3(side * 0.12f, 0.08f, 0.15f);
                cheek.localScale = new Vector3(0.06f, 0.04f, 0.02f);
            }

            // hair: a cap on top, a mass behind the head and a long fall down the back, so the face stays clear in profile
            var cap = Prims.Make("HairCap", Prims.Sphere, m.hair, head);
            cap.localPosition = new Vector3(0f, 0.285f, -0.035f);
            cap.localScale = new Vector3(0.41f, 0.25f, 0.43f);
            var bangs = Prims.Make("Bangs", Prims.Sphere, m.hair, head);
            bangs.localPosition = new Vector3(0f, 0.32f, 0.075f);
            bangs.localScale = new Vector3(0.34f, 0.11f, 0.2f);
            var back = Prims.Make("HairBack", Prims.Sphere, m.hair, head);
            back.localPosition = new Vector3(0f, 0.07f, -0.13f);
            back.localScale = new Vector3(0.38f, 0.46f, 0.26f);
            var fall = Prims.Make("HairFall", Prims.Sphere, m.hair, head);
            fall.localPosition = new Vector3(0f, -0.38f, -0.17f);
            fall.localScale = new Vector3(0.34f, 1.0f, 0.2f);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                var fl = Prims.Make("Flower", Prims.Sphere, m.flowers[i % 4 == 3 ? 2 : i % 2], head, false, false);
                fl.localPosition = new Vector3(Mathf.Sin(a) * 0.21f, 0.33f + 0.01f * Mathf.Cos(a * 2f), Mathf.Cos(a) * 0.21f - 0.02f);
                fl.localScale = Vector3.one * 0.07f;
            }

            // arms
            for (int s = 0; s < 2; s++)
            {
                upper[s] = new GameObject("UpperArm").transform;
                upper[s].SetParent(Root, false);
                var u = Prims.Make("Mesh", Prims.Cylinder, m.skin, upper[s]);
                u.localPosition = new Vector3(0f, -UpperLen * 0.5f, 0f);
                u.localScale = new Vector3(0.115f, UpperLen * 0.5f, 0.115f);
                lower[s] = new GameObject("LowerArm").transform;
                lower[s].SetParent(Root, false);
                var l = Prims.Make("Mesh", Prims.Cylinder, m.skin, lower[s]);
                l.localPosition = new Vector3(0f, -LowerLen * 0.5f, 0f);
                l.localScale = new Vector3(0.1f, LowerLen * 0.5f, 0.1f);
                var elbow = Prims.Make("Elbow", Prims.Sphere, m.skin, lower[s]);
                elbow.localScale = Vector3.one * 0.115f;
                var shoulder = Prims.Make("Shoulder", Prims.Sphere, m.skin, upper[s]);
                shoulder.localScale = Vector3.one * 0.14f;
                hand[s] = Prims.Make("Hand", Prims.Sphere, m.skin, lower[s]);
                hand[s].localPosition = new Vector3(0f, -LowerLen - 0.03f, 0f);
                hand[s].localScale = new Vector3(0.13f, 0.15f, 0.09f);
            }

            footL = Prims.Make("FootL", Prims.Sphere, m.skin, Root);
            footR = Prims.Make("FootR", Prims.Sphere, m.skin, Root);
            footL.localScale = footR.localScale = new Vector3(0.12f, 0.07f, 0.22f);
        }

        public void SetVisible(bool v) => Root.gameObject.SetActive(v);

        /// <param name="walk">The walk cycle (radians); <paramref name="walkAmount"/> how much she is walking (0 = standing).</param>
        /// <param name="mouthOpen">0..1, while she speaks.</param>
        /// <param name="lookAt">World point her head turns toward, with <paramref name="lookWeight"/> 0..1.</param>
        /// <param name="rightHand">If set, her right hand goes here (world) with <paramref name="rightWeight"/> 0..1 (the rest is her natural pose).</param>
        /// <param name="gesture">0..1 small speaking gesture of the left hand.</param>
        public void Pose(Vector3 position, float yawDegrees, float walk, float walkAmount, float time, float mouthOpen,
                         Vector3 lookAt, float lookWeight, Vector3 rightHand, float rightWeight, float gesture)
        {
            yaw = yawDegrees;
            float bob = Mathf.Abs(Mathf.Sin(walk)) * 0.045f * walkAmount + Mathf.Sin(time * 1.7f) * 0.006f;
            Root.localPosition = position + Vector3.up * bob;
            Root.localRotation = Quaternion.Euler(0f, yaw, Mathf.Sin(walk) * 1.6f * walkAmount);
            dress.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(walk) * -2.5f * walkAmount);

            // feet: alternate steps
            float step = Mathf.Sin(walk), lift = Mathf.Max(0f, Mathf.Cos(walk));
            footL.localPosition = new Vector3(-0.12f, 0.04f + (step > 0f ? 0f : 0.07f * Mathf.Abs(step)) * walkAmount, 0.06f + step * 0.20f * walkAmount);
            footR.localPosition = new Vector3(0.12f, 0.04f + (step < 0f ? 0f : 0.07f * Mathf.Abs(step)) * walkAmount, 0.06f - step * 0.20f * walkAmount);

            // head: turns toward what she looks at, within what a neck can do
            Quaternion baseRot = Root.rotation;
            if (lookWeight > 0.001f)
            {
                Vector3 to = lookAt - head.position;
                if (to.sqrMagnitude > 1e-4f)
                {
                    Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
                    head.rotation = Quaternion.Slerp(baseRot, Quaternion.RotateTowards(baseRot, want, 55f), Mathf.Clamp01(lookWeight));
                }
                else head.rotation = baseRot;
            }
            else head.rotation = baseRot;
            jaw.localScale = new Vector3(1f, 1f + Mathf.Clamp01(mouthOpen) * 2.4f, 1f);

            // arms
            for (int s = 0; s < 2; s++)
            {
                bool right = s == 1;
                float side = right ? 1f : -1f;
                // relaxed: hands by the hips, swinging as she walks (opposite to the feet)
                float swing = Mathf.Sin(walk + (right ? 0f : Mathf.PI)) * 0.24f * walkAmount;
                Vector3 relaxed = Root.TransformPoint(new Vector3(side * 0.36f, 0.86f + (right ? 0f : gesture * 0.34f), 0.05f + swing + (right ? 0f : gesture * 0.22f)));
                Vector3 target = right ? Vector3.Lerp(relaxed, rightHand, Mathf.Clamp01(rightWeight)) : relaxed;
                Vector3 pole = Root.TransformDirection(new Vector3(side * 0.8f, -0.5f, -0.45f));
                SolveArm(s, Root.TransformPoint(new Vector3(side * ShoulderX, ShoulderY, 0f)), target, pole);
            }
        }

        void SolveArm(int s, Vector3 shoulder, Vector3 target, Vector3 pole)
        {
            Vector3 d = target - shoulder;
            float dist = Mathf.Clamp(d.magnitude, 0.12f, UpperLen + LowerLen - 0.01f);
            Vector3 dir = d.sqrMagnitude < 1e-6f ? Vector3.down : d.normalized;
            float cosA = Mathf.Clamp((UpperLen * UpperLen + dist * dist - LowerLen * LowerLen) / (2f * UpperLen * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 perp = pole - dir * Vector3.Dot(pole, dir);
            perp = perp.sqrMagnitude < 1e-6f ? Vector3.Cross(dir, Vector3.up).normalized : perp.normalized;
            Vector3 elbow = shoulder + dir * (UpperLen * cosA) + perp * (UpperLen * sinA);
            Vector3 wrist = shoulder + dir * dist;

            upper[s].position = shoulder;
            upper[s].rotation = Quaternion.FromToRotation(Vector3.down, (elbow - shoulder).normalized);
            lower[s].position = elbow;
            lower[s].rotation = Quaternion.FromToRotation(Vector3.down, (wrist - elbow).normalized);
        }
    }
}
