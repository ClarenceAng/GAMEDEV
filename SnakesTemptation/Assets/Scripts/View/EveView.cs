using UnityEngine;

// eve, made out of spheres and cylinders like everything else. about 2 units tall.
// the arms are two cylinders each, from the shoulder to the elbow and from the elbow to the hand,
// so we can put her hand anywhere (on the apple, then at her mouth)
public class EveView
{
    public Transform Root;
    public Vector3 RightHandWorld;

    Transform head;
    Transform mouth;
    Transform footL;
    Transform footR;
    Transform[] upperArms = new Transform[2]; // 0 = left, 1 = right
    Transform[] lowerArms = new Transform[2];
    Transform[] hands = new Transform[2];

    public EveView(Transform parent, GardenMaterials m)
    {
        Root = new GameObject("Eve").transform;
        Root.SetParent(parent, false);

        // the dress is a big squished sphere so it looks like a bell
        Transform skirt = ShapeMaker.Make(PrimitiveType.Sphere, "Dress", m.dress, Root);
        skirt.localPosition = new Vector3(0f, 0.62f, 0f);
        skirt.localScale = new Vector3(0.85f, 1.3f, 0.85f);
        Transform top = ShapeMaker.Make(PrimitiveType.Capsule, "Top", m.dress, Root);
        top.localPosition = new Vector3(0f, 1.32f, 0f);
        top.localScale = new Vector3(0.4f, 0.22f, 0.3f);
        Transform belt = ShapeMaker.Make(PrimitiveType.Cylinder, "Belt", m.leafGreen, Root);
        belt.localPosition = new Vector3(0f, 1.17f, 0f);
        belt.localScale = new Vector3(0.42f, 0.025f, 0.42f);
        Transform shoulders = ShapeMaker.Make(PrimitiveType.Sphere, "Shoulders", m.skin, Root);
        shoulders.localPosition = new Vector3(0f, 1.50f, 0f);
        shoulders.localScale = new Vector3(0.58f, 0.2f, 0.28f);
        Transform neck = ShapeMaker.Make(PrimitiveType.Cylinder, "Neck", m.skin, Root);
        neck.localPosition = new Vector3(0f, 1.62f, 0f);
        neck.localScale = new Vector3(0.12f, 0.1f, 0.12f);

        // the head turns around the neck
        head = new GameObject("Head").transform;
        head.SetParent(Root, false);
        head.localPosition = new Vector3(0f, 1.70f, 0f);
        Transform face = ShapeMaker.Make(PrimitiveType.Sphere, "Face", m.skin, head);
        face.localPosition = new Vector3(0f, 0.15f, 0f);
        face.localScale = new Vector3(0.37f, 0.42f, 0.39f);
        Transform nose = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Nose", m.skin, head);
        nose.localPosition = new Vector3(0f, 0.12f, 0.2f);
        nose.localScale = new Vector3(0.05f, 0.06f, 0.06f);
        mouth = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Mouth", m.mouth, head);
        mouth.localPosition = new Vector3(0f, 0.05f, 0.17f);
        for (int side = -1; side <= 1; side += 2)
        {
            Transform eye = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Eye", m.dress, head);
            eye.localPosition = new Vector3(side * 0.075f, 0.19f, 0.165f);
            eye.localScale = new Vector3(0.062f, 0.05f, 0.04f);
            Transform iris = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Iris", m.leafDark, head);
            iris.localPosition = new Vector3(side * 0.075f, 0.19f, 0.182f);
            iris.localScale = new Vector3(0.036f, 0.036f, 0.02f);
        }

        // hair
        Transform hairTop = ShapeMaker.Make(PrimitiveType.Sphere, "Hair", m.hair, head);
        hairTop.localPosition = new Vector3(0f, 0.285f, -0.035f);
        hairTop.localScale = new Vector3(0.41f, 0.25f, 0.43f);
        Transform hairBack = ShapeMaker.Make(PrimitiveType.Sphere, "HairBack", m.hair, head);
        hairBack.localPosition = new Vector3(0f, 0.07f, -0.13f);
        hairBack.localScale = new Vector3(0.38f, 0.46f, 0.26f);
        Transform hairLong = ShapeMaker.Make(PrimitiveType.Sphere, "HairLong", m.hair, head);
        hairLong.localPosition = new Vector3(0f, -0.38f, -0.17f);
        hairLong.localScale = new Vector3(0.34f, 1.0f, 0.2f);
        // flower crown
        for (int i = 0; i < 9; i++)
        {
            float angle = i / 9f * Mathf.PI * 2f;
            Transform flower = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Flower", m.flowers[i % 3], head);
            flower.localPosition = new Vector3(Mathf.Sin(angle) * 0.21f, 0.33f, Mathf.Cos(angle) * 0.21f - 0.02f);
            flower.localScale = new Vector3(0.07f, 0.07f, 0.07f);
        }

        // arms
        for (int i = 0; i < 2; i++)
        {
            upperArms[i] = ShapeMaker.Make(PrimitiveType.Cylinder, "UpperArm", m.skin, Root);
            lowerArms[i] = ShapeMaker.Make(PrimitiveType.Cylinder, "LowerArm", m.skin, Root);
            hands[i] = ShapeMaker.Make(PrimitiveType.Sphere, "Hand", m.skin, Root);
            hands[i].localScale = new Vector3(0.13f, 0.15f, 0.09f);
        }

        footL = ShapeMaker.Make(PrimitiveType.Sphere, "FootL", m.skin, Root);
        footR = ShapeMaker.Make(PrimitiveType.Sphere, "FootR", m.skin, Root);
        footL.localScale = new Vector3(0.12f, 0.07f, 0.22f);
        footR.localScale = new Vector3(0.12f, 0.07f, 0.22f);
    }

    public Vector3 FaceWorld
    {
        get { return head.position + Root.up * 0.1f; }
    }

    public Vector3 MouthWorld
    {
        get { return mouth.position; }
    }

    public void SetVisible(bool visible)
    {
        Root.gameObject.SetActive(visible);
    }

    // walk = the walk cycle, walking = true while she walks, talk = 0 to 1 how open her mouth is,
    // she turns her head to lookAt, and her right hand goes to rightHand (rightWeight = 0 is her normal pose)
    public void Pose(Vector3 position, float yaw, float walk, bool walking, float time, float talk,
                     Vector3 lookAt, float lookWeight, Vector3 rightHand, float rightWeight)
    {
        float walkAmount = 0f;
        if (walking) walkAmount = 1f;

        float bob = Mathf.Abs(Mathf.Sin(walk)) * 0.045f * walkAmount;
        Root.localPosition = position + Vector3.up * bob;
        Root.localRotation = Quaternion.Euler(0f, yaw, Mathf.Sin(walk) * 1.6f * walkAmount);

        // feet take turns stepping forward
        float step = Mathf.Sin(walk) * walkAmount;
        footL.localPosition = new Vector3(-0.12f, 0.04f, 0.06f + step * 0.2f);
        footR.localPosition = new Vector3(0.12f, 0.04f, 0.06f - step * 0.2f);

        // turn the head
        if (lookWeight > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(lookAt - head.position);
            head.rotation = Quaternion.Slerp(Root.rotation, lookRotation, lookWeight);
        }
        else
        {
            head.rotation = Root.rotation;
        }

        mouth.localScale = new Vector3(0.085f, 0.028f + 0.06f * talk, 0.03f);

        for (int i = 0; i < 2; i++)
        {
            float side = -1f;
            if (i == 1) side = 1f;
            Vector3 shoulder = Root.TransformPoint(new Vector3(side * 0.25f, 1.5f, 0f));

            // arms hang by the hips and swing when she walks
            float swing = Mathf.Sin(walk + i * Mathf.PI) * 0.24f * walkAmount;
            Vector3 hand = Root.TransformPoint(new Vector3(side * 0.36f, 0.86f, 0.05f + swing));
            if (i == 1) hand = Vector3.Lerp(hand, rightHand, rightWeight);

            // put the elbow in the middle and push it out and down a bit
            Vector3 elbow = (shoulder + hand) / 2f + Root.TransformDirection(new Vector3(side * 0.12f, -0.1f, -0.08f));
            PlaceBetween(upperArms[i], shoulder, elbow, 0.115f);
            PlaceBetween(lowerArms[i], elbow, hand, 0.1f);
            hands[i].position = hand;
            if (i == 1) RightHandWorld = hand;
        }
    }

    // stretches a cylinder from a to b
    void PlaceBetween(Transform cylinder, Vector3 a, Vector3 b, float thickness)
    {
        cylinder.position = (a + b) / 2f;
        cylinder.up = b - a;
        cylinder.localScale = new Vector3(thickness, Vector3.Distance(a, b) / 2f, thickness);
    }
}
