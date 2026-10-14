using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// The six-shooter held in front of the camera: fires <see cref="Bullet"/>s down the crosshair line, and animates
    /// recoil, the muzzle flash, aiming down the sights and the reload.
    /// The ammo rules live in <see cref="Chamber"/>.
    /// </summary>
    public sealed class Revolver : MonoBehaviour
    {
        const float FlashTime = 0.05f;

        static readonly Vector3 HipPos = new Vector3(0.2f, -0.2f, 0.42f);
        static readonly Vector3 SightsPos = new Vector3(0f, -0.085f, 0.34f);

        public Chamber Chamber { get; private set; }

        /// <summary>Hold to aim down the sights (zoom).</summary>
        public bool Aiming { get; set; }

        /// <summary>0 at the hip .. 1 fully aimed. The game zooms the camera with it.</summary>
        public float AimBlend { get; private set; }

        /// <summary>1 right after a shot, settling back to 0. The crosshair widens with it.</summary>
        public float Kick => recoil;

        KnockdownGame game;
        Camera cam;
        Transform model, drum, muzzle;
        GameObject flash;
        Light flashLight;
        float recoil, flashLeft, drumAngle, drumShown;

        public static Revolver Build(KnockdownGame game, Camera cam)
        {
            var art = game.art;
            var root = Shapes.Group(cam.transform, "Revolver", Vector3.zero);
            var model = Shapes.Group(root, "Model", HipPos);

            // Barrel points along +Z. Cylinders lie along Z when rotated 90 degrees about X.
            var alongZ = Quaternion.Euler(90f, 0f, 0f);
            Shapes.Cylinder(model, "Barrel", new Vector3(0f, 0.03f, 0.15f), 0.032f, 0.22f, art.gunMetal, Hitbox.None, alongZ);
            Shapes.Cylinder(model, "Ejector", new Vector3(0f, 0.008f, 0.11f), 0.02f, 0.13f, art.gunMetal, Hitbox.None, alongZ);
            Shapes.Box(model, "Frame", new Vector3(0f, 0.02f, 0f), new Vector3(0.042f, 0.07f, 0.12f), art.gunMetal, Hitbox.None);
            Shapes.Box(model, "Hammer", new Vector3(0f, 0.062f, -0.062f), new Vector3(0.014f, 0.03f, 0.025f), art.gunMetal, Hitbox.None, Quaternion.Euler(-25f, 0f, 0f));
            Shapes.Box(model, "Sight", new Vector3(0f, 0.052f, 0.25f), new Vector3(0.006f, 0.014f, 0.01f), art.brass, Hitbox.None);
            Shapes.Box(model, "Guard", new Vector3(0f, -0.028f, -0.015f), new Vector3(0.012f, 0.03f, 0.045f), art.brass, Hitbox.None);
            Shapes.Box(model, "Grip", new Vector3(0f, -0.055f, -0.085f), new Vector3(0.038f, 0.12f, 0.05f), art.gunGrip, Hitbox.None, Quaternion.Euler(20f, 0f, 0f));
            Shapes.Box(model, "Hand", new Vector3(0.004f, -0.06f, -0.085f), new Vector3(0.05f, 0.085f, 0.065f), art.skin, Hitbox.None, Quaternion.Euler(20f, 0f, 0f));
            Shapes.Box(model, "Sleeve", new Vector3(0.03f, -0.12f, -0.3f), new Vector3(0.085f, 0.085f, 0.36f), art.banditCloth, Hitbox.None, Quaternion.Euler(12f, -6f, 0f));

            var drum = Shapes.Group(model, "Drum", new Vector3(0f, 0.025f, 0.005f));
            Shapes.Cylinder(drum, "Cylinder", Vector3.zero, 0.072f, 0.07f, art.gunMetal, Hitbox.None, alongZ);
            for (int i = 0; i < Chamber.Capacity; i++)
            {
                // Brass cartridge rims showing at the back of the cylinder.
                float a = i * Mathf.PI * 2f / Chamber.Capacity;
                var rim = new Vector3(Mathf.Cos(a) * 0.022f, Mathf.Sin(a) * 0.022f, -0.036f);
                Shapes.Cylinder(drum, "Round", rim, 0.014f, 0.004f, art.brass, Hitbox.None, alongZ);
            }

            var muzzle = Shapes.Group(model, "Muzzle", new Vector3(0f, 0.03f, 0.27f));
            var flash = Shapes.Sphere(muzzle, "Flash", new Vector3(0f, 0f, 0.05f), new Vector3(0.09f, 0.09f, 0.16f), art.flash, Hitbox.None);
            flash.SetActive(false);
            var light = new GameObject("FlashLight").AddComponent<Light>();
            light.transform.SetParent(muzzle, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.4f);
            light.range = 8f;
            light.intensity = 0f;

            Shapes.SetLayer(root.gameObject, Shapes.NoHitLayer);
            Shapes.NoShadows(root.gameObject);

            var gun = root.gameObject.AddComponent<Revolver>();
            gun.game = game;
            gun.cam = cam;
            gun.model = model;
            gun.drum = drum;
            gun.muzzle = muzzle;
            gun.flash = flash;
            gun.flashLight = light;
            gun.Chamber = new Chamber(game.reloadTime, game.fireInterval);
            return gun;
        }

        /// <summary>Pulls the trigger. Returns true if a bullet left the barrel.</summary>
        public bool Trigger()
        {
            if (!Chamber.TryFire()) return false;

            // The shot flies exactly down the crosshair; it only looks like it leaves the muzzle (see Bullet).
            var eye = cam.transform;
            Bullet.Fire(game, eye.position, eye.forward, muzzle.position, game.bulletSpeed, game.bulletImpulse);

            recoil = 1f;
            flashLeft = FlashTime;
            drumAngle += 360f / Chamber.Capacity;
            game.Sfx.Gunshot();
            if (Chamber.Reloading) game.Sfx.Reload(); // that was the last round
            return true;
        }

        /// <summary>Starts a reload if the cylinder isn't full.</summary>
        public void Reload()
        {
            if (Chamber.StartReload()) game.Sfx.Reload();
        }

        /// <summary>Back to a full cylinder, at the hip, no animation in progress.</summary>
        public void ResetGun()
        {
            Chamber = new Chamber(game.reloadTime, game.fireInterval);
            recoil = flashLeft = 0f;
            Aiming = false;
            AimBlend = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Chamber.Tick(dt)) game.Sfx.ReloadDone();

            AimBlend = Mathf.MoveTowards(AimBlend, Aiming && !Chamber.Reloading ? 1f : 0f, dt * 6f);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 7f);

            flashLeft -= dt;
            bool flashing = flashLeft > 0f;
            flash.SetActive(flashing);
            flash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            flashLight.intensity = flashing ? 6f : 0f;

            // Reload: tip the gun over to the left and spin the cylinder.
            float reload = Chamber.Reloading ? Mathf.Sin(Chamber.ReloadProgress * Mathf.PI) : 0f;
            if (Chamber.Reloading) drumAngle += dt * 900f;
            drumShown = Mathf.Lerp(drumShown, drumAngle, 1f - Mathf.Exp(-dt * 30f));
            drum.localRotation = Quaternion.Euler(0f, 0f, drumShown);

            float k = recoil * recoil;
            Vector3 pos = Vector3.Lerp(HipPos, SightsPos, Smooth(AimBlend));
            pos += new Vector3(0f, k * 0.02f, -k * 0.07f) + new Vector3(-0.05f, -0.06f, 0f) * reload;
            model.localPosition = pos;
            model.localRotation = Quaternion.Euler(-k * 16f + reload * 25f, 0f, reload * 50f);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
