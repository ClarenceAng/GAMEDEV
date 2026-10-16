using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// The apple monster. The prefab (built by the apple-monster generator) is static, so movement life comes from here:
    /// it hops cell to cell, turns toward where it is heading, sways, and bursts out of the ground when it spawns.
    /// </summary>
    sealed class MonsterView
    {
        const float ModelScale = 1.1f; // the prefab is ~1.9 m tall; this makes the apple body ~1.2 cells wide and the whole thing ~2 m tall

        readonly GridMap grid;
        readonly Transform root;
        readonly Transform model;
        readonly Light aura;
        const float FaceHeight = 1.3f;   // height of the eyes above the monster's feet, in its own scale
        bool lunging;
        Vector3 lungeFrom;
        Vector2Int from, to;
        Quaternion rot = Quaternion.identity;

        public MonsterView(Transform parent, GridMap grid, ArtSet art, Vector2Int cell)
        {
            this.grid = grid;
            from = to = cell;
            root = new GameObject("Monster").transform;
            root.SetParent(parent, false);
            model = Object.Instantiate(art.monsterPrefab, root).transform;
            model.localScale = Vector3.one * ModelScale;

            aura = new GameObject("Aura").AddComponent<Light>();
            aura.transform.SetParent(root, false);
            aura.transform.localPosition = new Vector3(0f, 1.2f, 0.4f);
            aura.type = LightType.Point;
            aura.color = new Color(1f, 0.85f, 0.2f);
            aura.range = 9f;
            aura.intensity = 7f;
            aura.shadows = LightShadows.None;
        }

        /// <summary>Put the monster on a cell without it walking there.</summary>
        public void Teleport(Vector2Int cell)
        {
            from = to = cell;
        }

        public void OnStep(Vector2Int newCell)
        {
            from = to;
            to = newCell;
        }

        /// <summary>Where the monster's eyes are in the world right now.</summary>
        public Vector3 FaceWorld => root.position + root.rotation * (Vector3.up * (FaceHeight * root.localScale.y));

        /// <summary>The jumpscare begins from wherever the monster is standing.</summary>
        public void BeginLunge()
        {
            lunging = true;
            lungeFrom = FaceWorld;
        }

        /// <summary>
        /// The monster flings itself into the camera: its face races from where it was to a metre or two in front of the lens,
        /// turned to look straight at the player, growing as it comes and trembling once it arrives, its light blazing.
        /// </summary>
        /// <param name="t">0..1 over the length of the lunge (it stays at 1 afterwards).</param>
        public void Lunge(Transform camera, float t, float time)
        {
            if (!lunging) return;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);              // fast out, then it just hangs there
            Vector3 camPos = camera.position;
            Vector3 arrive = camPos + camera.forward * 2.5f + camera.up * 0.12f;   // eyes just above centre so the fangs fit in frame
            Vector3 face = Vector3.Lerp(lungeFrom, arrive, e);
            face += Random.insideUnitSphere * (0.02f + 0.05f * e);            // it shakes
            float s = Mathf.Lerp(1f, 1.9f, e) * (1f + 0.03f * Mathf.Sin(time * 47f));
            Quaternion look = Quaternion.LookRotation(camPos - face, Vector3.up);
            root.rotation = look;                                              // the face looks into the lens
            root.localScale = Vector3.one * s;
            root.position = face - look * (Vector3.up * (FaceHeight * s));      // pivot on the face, not the feet
            // The monster's own light sits inside its body at this size, so hang a hot orange one in front of the face.
            aura.transform.position = face + (camPos - face).normalized * (1.3f * s) + Vector3.up * 0.4f;
            aura.color = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.55f, 0.18f), e);
            aura.intensity = Mathf.Lerp(7f, 55f, e) * (0.8f + 0.2f * Mathf.Sin(time * 53f));
            aura.range = Mathf.Lerp(9f, 16f, e);
        }

        public void EndLunge()
        {
            lunging = false;
            aura.transform.localPosition = new Vector3(0f, 1.2f, 0.4f);
            aura.color = new Color(1f, 0.85f, 0.2f);
            aura.intensity = 7f;
            aura.range = 9f;
        }

        public void Refresh(float progress, float emerge, float time, float dt)
        {
            if (lunging) return;
            Vector3 a = grid.ToWorld(from), b = grid.ToWorld(to);
            Vector3 pos = Vector3.Lerp(a, b, progress);

            // It runs at the snake, so it faces the way it is going.
            Vector3 dir = b - a;
            if (dir.sqrMagnitude > 0.01f)
                rot = Quaternion.Slerp(rot, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-10f * dt));

            float hop = Mathf.Sin(progress * Mathf.PI) * 0.14f;                       // a stomp per cell
            float sink = (1f - Mathf.Clamp01(emerge)) * 1.8f;                          // rises out of the ground
            root.position = pos + new Vector3(0f, hop - sink, 0f);
            root.rotation = rot * Quaternion.Euler(8f, 0f, Mathf.Sin(time * 5f) * 4f); // leaning into the chase, with a menacing sway
            root.localScale = Vector3.one * Mathf.Clamp01(emerge * 1.5f);
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root.gameObject);
        }
    }
}
