using UnityEngine;

// Settings for the saved Scene objects. The editor places these objects into the scene;
// the game manager connects the detection scripts at Play time.
public static class StealthV2Layout
{
    public enum PatrolMode { ForwardSweep, CeilingSweep, DiagonalSweep, CrossCorridor }
    public enum PartFinish { Armor, Hazard, Cyan, Wall, Roof, Deck, Sign, Pipe }

    public struct Guard
    {
        public string name;
        public Vector3 position;
        public StealthPatrolSensor.DetectionZone shape;
        public PatrolMode mode;
        public float range, cone, travel, speed;
        public Guard(string n, float x, float y, float z,
            StealthPatrolSensor.DetectionZone s, PatrolMode m, float r, float angle,
            float movement, float rate)
        {
            name = n; position = new Vector3(x, y, z); shape = s; mode = m;
            range = r; cone = angle; travel = movement; speed = rate;
        }
    }

    public struct Part
    {
        public string name;
        public Vector3 position, size, rotation;
        public PartFinish finish;
        public bool blocksSight;
        public Part(string n, float x, float y, float z, float w, float h, float d,
            PartFinish f, bool solid = true, float rx = 0, float ry = 0, float rz = 0)
        {
            name = n;
            position = new Vector3(x, y, z);
            size = new Vector3(w, h, d);
            rotation = new Vector3(rx, ry, rz);
            finish = f;
            blocksSight = solid;
        }
    }

    public struct Intel
    {
        public string name;
        public Vector3 position;
        public bool gantryKey;
        public Intel(string n, float x, float y, float z, bool key = false)
        {
            name = n; position = new Vector3(x, y, z); gantryKey = key;
        }
    }

    // 20 closely spaced, deliberately paired patrols (instead of V2's 8).
    // Each corridor cover cluster is defended; ceiling, diagonal and floor
    // scanners force players to use blockers, crouching and the catwalk.
    // All patrols require an overlap shape test, a field-of-view test,
    // AND an unobstructed Raycast to see the player.
    public static readonly Guard[] Guards =
    {
        // ENTRY: the first intel remains in a safe pocket before the first scanner.
        new Guard("Entry Sweeper", 0f, 2.5f, -124f, StealthPatrolSensor.DetectionZone.CheckSphere, PatrolMode.ForwardSweep, 10f, 47f, 2.15f, .75f),
        new Guard("Generator Sentinel", 1.9f, 2.55f, -111f, StealthPatrolSensor.DetectionZone.CheckBox, PatrolMode.ForwardSweep, 9.4f, 45f, 1.0f, .56f),

        // ROOFED MAINTENANCE: a descending eye and a low sweeper guard the same cover chain.
        new Guard("Maintenance Ceiling Watch", -.4f, 6.7f, -93f, StealthPatrolSensor.DetectionZone.CheckSphere, PatrolMode.CeilingSweep, 9.5f, 49f, 1.45f, .61f),
        new Guard("Angled Plate Sweeper", .75f, 2.6f, -80f, StealthPatrolSensor.DetectionZone.CheckBox, PatrolMode.ForwardSweep, 9.5f, 43f, 1.45f, .66f),
        new Guard("Cooling Ceiling Watch", -.8f, 6.8f, -68f, StealthPatrolSensor.DetectionZone.CheckCapsule, PatrolMode.CeilingSweep, 9.0f, 45f, 1.65f, .69f),

        // COOLING / BULKHEAD: separate diagonal and horizontal search lanes.
        new Guard("Cooling Diagonal Eye", 2.4f, 5.55f, -56f, StealthPatrolSensor.DetectionZone.CheckCapsule, PatrolMode.DiagonalSweep, 11.5f, 53f, 1.1f, .58f),
        new Guard("Split Cover Sweeper", .65f, 2.65f, -44f, StealthPatrolSensor.DetectionZone.CheckSphere, PatrolMode.ForwardSweep, 9.6f, 45f, 1.65f, .72f),
        new Guard("Narrow Gate Scanner", -.2f, 2.5f, -29f, StealthPatrolSensor.DetectionZone.CheckBox, PatrolMode.ForwardSweep, 10f, 46f, 2.0f, .67f),
        new Guard("Terminal Diagonal Watch", 2.3f, 5.4f, -14f, StealthPatrolSensor.DetectionZone.CheckCapsule, PatrolMode.DiagonalSweep, 10.3f, 51f, .95f, .64f),
        new Guard("Cable Bay Overwatch", .65f, 6.7f, 0f, StealthPatrolSensor.DetectionZone.CheckSphere, PatrolMode.CeilingSweep, 9.4f, 49f, 1.5f, .63f),

        // VERTICAL ROUTE: a ceiling watch at the ramp, ground pressure below the catwalk,
        // and an upper scanner whose raycast is stopped by the NEW overhead catwalk roof.
        new Guard("Ramp Overwatch", .2f, 6.65f, 17f, StealthPatrolSensor.DetectionZone.CheckCapsule, PatrolMode.CeilingSweep, 10.5f, 54f, 1.3f, .57f),
        new Guard("Ramp Junction Watch", -1.6f, 6.7f, 30f, StealthPatrolSensor.DetectionZone.CheckSphere, PatrolMode.CeilingSweep, 9.3f, 47f, .9f, .61f),
        new Guard("Lower Lane Sweeper", -2.15f, 2.6f, 42f, StealthPatrolSensor.DetectionZone.CheckBox, PatrolMode.ForwardSweep, 10.3f, 46f, 1.1f, .59f),
        new Guard("Floor Cross-Corridor Scanner", -4.2f, 1.85f, 54f, StealthPatrolSensor.DetectionZone.CheckBox, PatrolMode.CrossCorridor, 10.8f, 55f, 1.8f, .49f),
        new Guard("Catwalk Ceiling Camera", 3.0f, 7.5f, 68f, StealthPatrolSensor.DetectionZone.CheckSphere, PatrolMode.CeilingSweep, 9.7f, 48f, .85f, .67f),
        new Guard("Lower Ramp Scanner", -2.2f, 2.65f, 80f, StealthPatrolSensor.DetectionZone.CheckBox, PatrolMode.ForwardSweep, 9.6f, 43f, 1.25f, .58f),

        // VAULT: overlapping diagonal / downward / horizontal watches guard the last cover.
        new Guard("Vault Side Watch", 2.55f, 5.45f, 93f, StealthPatrolSensor.DetectionZone.CheckCapsule, PatrolMode.DiagonalSweep, 10.6f, 49f, 1.05f, .63f),
        new Guard("Vault Ceiling Watch", .4f, 6.7f, 105f, StealthPatrolSensor.DetectionZone.CheckSphere, PatrolMode.CeilingSweep, 10f, 49f, 1.65f, .65f),
        new Guard("Vault Console Sweeper", -.4f, 2.55f, 118f, StealthPatrolSensor.DetectionZone.CheckBox, PatrolMode.ForwardSweep, 9.5f, 45f, 1.6f, .69f),
        new Guard("Final Security Sweeper", -.35f, 2.6f, 131f, StealthPatrolSensor.DetectionZone.CheckCapsule, PatrolMode.ForwardSweep, 10.7f, 50f, 2.3f, .79f)
    };

    // World-space data, including rotated shields and a walkable elevated route.
    // All ramp/deck pieces are collidable, to make height a real stealth option.
    public static readonly Part[] Parts =
    {
        // BAY 1: staggered, differently sized ground cover around the first guard.
        new Part("01 LARGE POWER CRATE", -2.6f, 1.54f, -132f, 2.5f, 2.05f, 2.2f, PartFinish.Armor),
        new Part("01 SHORT SHIELD", 2.35f, 1.13f, -121f, 3.0f, 1.25f, 1.15f, PartFinish.Hazard, true, 0, -26),
        new Part("01 SIDE GENERATOR", -3.1f, 2.0f, -113f, 2.1f, 2.95f, 1.65f, PartFinish.Armor),
        new Part("01 GENERATOR GLOW", -3.1f, 2.02f, -111.96f, 1.15f, .18f, .09f, PartFinish.Cyan, false),

        // BAY 2: a real horizontal roof interrupts the top-down sensor raycast.
        new Part("02 FLOATING SHADOW ROOF", -1.75f, 3.70f, -94f, 4.0f, .42f, 6.2f, PartFinish.Roof),
        new Part("02 ROOF STRIP", -1.75f, 3.47f, -94f, 3.7f, .055f, 5.8f, PartFinish.Cyan, false),
        new Part("02 REACTOR PEDESTAL", 2.85f, 1.58f, -89f, 2.45f, 2.15f, 2.65f, PartFinish.Armor),
        new Part("02 ANGLED PLATE", -2.15f, 1.43f, -80f, 3.45f, 1.85f, .65f, PartFinish.Hazard, true, 0, 26),
        new Part("02 TERMINAL GLASS", 3.0f, 1.65f, -74f, 2.4f, 1.8f, .5f, PartFinish.Cyan),

        // BAY 3: diagonal sightlines, vertical blockers and two ceiling shelters.
        new Part("03 ROTATED BULKHEAD", -.55f, 1.72f, -67f, 3.35f, 2.4f, .68f, PartFinish.Armor, true, 0, -28),
        new Part("03 SHADOW CANOPY", 1.65f, 3.75f, -55f, 3.65f, .42f, 5.5f, PartFinish.Roof),
        new Part("03 CANOPY STRIP", 1.65f, 3.50f, -55f, 3.3f, .05f, 5.0f, PartFinish.Cyan, false),
        new Part("03 TALL MACHINE", -2.85f, 2.08f, -51f, 2.3f, 3.15f, 2.1f, PartFinish.Armor),
        new Part("03 OFFSET BARRICADE", 2.4f, 1.41f, -38f, 3.4f, 1.8f, .76f, PartFinish.Hazard, true, 0, 30),
        new Part("03 HALF HEIGHT CRATE", -2.55f, 1.15f, -24f, 2.45f, 1.3f, 2.2f, PartFinish.Armor),
        new Part("03 TERMINAL SLAB", 3.05f, 1.7f, -16f, 2.3f, 2.35f, 1.5f, PartFinish.Armor),

        new Part("03 CABLE COLUMN", -2.95f, 1.48f, -4f, 1.45f, 1.95f, 1.5f, PartFinish.Armor),
        new Part("03 MAINTENANCE SHIELD", 2.6f, 1.2f, 7f, 2.2f, 1.4f, 1.8f, PartFinish.Hazard, true, 0, -20),
        new Part("03 PRE-RAMP CRATE", -2.6f, 1.35f, 15f, 2.35f, 1.7f, 2.25f, PartFinish.Armor),

        // BAY 4: alternate route. The lateral scanner protects the FLOOR below.
        // Walkable angled ramps join the elevated right-side catwalk (top y=4).
        new Part("04 RAMP ENTRY LIGHT", 3.05f, .54f, 23.0f, 2.6f, .04f, 1.0f, PartFinish.Cyan, false),
        new Part("04 ARROW STEM", 3.05f, .544f, 20.4f, .16f, .05f, 1.65f, PartFinish.Cyan, false),
        new Part("04 ARROW TIP LEFT", 2.73f, .544f, 21.40f, .14f, .05f, 1.02f, PartFinish.Cyan, false, 0, -34),
        new Part("04 ARROW TIP RIGHT", 3.37f, .544f, 21.40f, .14f, .05f, 1.02f, PartFinish.Cyan, false, 0, 34),
        new Part("04 ASCENDING WALKABLE RAMP", 3.05f, 2.05f, 30.5f, 2.65f, .38f, 15.45f, PartFinish.Deck, true, -13.1f),
        new Part("04 RAISED CATWALK", 3.05f, 3.78f, 56f, 2.65f, .44f, 37.1f, PartFinish.Deck),
        new Part("04 DESCENDING WALKABLE RAMP", 3.05f, 2.05f, 81.5f, 2.65f, .38f, 15.45f, PartFinish.Deck, true, 13.1f),
        new Part("04 OUTER GUARD RAIL", 4.34f, 4.62f, 56f, .10f, 1.16f, 34f, PartFinish.Armor),
        new Part("04 GANTRY STRIPE", 3.05f, 4.016f, 56f, .10f, .032f, 33f, PartFinish.Cyan, false),
        new Part("04 GANTRY KEY SCAN RING", 3.05f, 4.023f, 62f, 1.4f, .04f, 1.4f, PartFinish.Hazard, false),
        new Part("04 CATWALK SUPPORT A", 4.0f, 2.12f, 43.0f, .38f, 3.25f, .42f, PartFinish.Armor),
        new Part("04 CATWALK SUPPORT B", 4.0f, 2.12f, 68.0f, .38f, 3.25f, .42f, PartFinish.Armor),
        new Part("04 LEFT FLOOR SHIELD", -1.85f, 1.48f, 44f, 3.15f, 1.95f, 2.0f, PartFinish.Hazard, true, 0, -18),
        new Part("04 RIGHT FLOOR MACHINE", .15f, 1.79f, 62f, 2.55f, 2.55f, 2.4f, PartFinish.Armor),
        new Part("04 LOWER COVER", -2.65f, 1.32f, 73f, 2.65f, 1.6f, 2.5f, PartFinish.Armor),

        // BAY 5: a roofed hideout, asymmetric shields, final mixed patrols.
        new Part("05 SIDE SPINE", -3.2f, 2.3f, 92f, 1.85f, 3.6f, 2.25f, PartFinish.Armor),
        new Part("05 FLOATING BLIND", -1.7f, 3.66f, 105f, 4.4f, .42f, 6.3f, PartFinish.Roof),
        new Part("05 FLOATING BLIND LIGHT", -1.7f, 3.42f, 105f, 4.05f, .055f, 5.8f, PartFinish.Cyan, false),
        new Part("05 RIGHT DEFLECTOR", 2.5f, 1.66f, 111f, 3.05f, 2.3f, .75f, PartFinish.Hazard, true, 0, 25),
        new Part("05 GATEWAY CONSOLE", -2.55f, 1.70f, 122f, 2.8f, 2.35f, 2.0f, PartFinish.Armor),
        new Part("05 FINAL OFFSET COVER", 2.5f, 1.4f, 135f, 2.85f, 1.8f, 1.4f, PartFinish.Hazard, true, 0, -32),

        // V3 TIGHT STEALTH POCKETS: every additional bot has nearby useful cover.
        // Keep a traversable left/right opening through the 10m-wide corridor.
        new Part("Generator Side Baffle", 2.15f, 1.60f, -105f, 2.65f, 2.1f, 1.05f, PartFinish.Hazard, true, 0, -23),
        new Part("Cooling Suspended Roof", -.6f, 3.76f, -68f, 4.25f, .40f, 5.4f, PartFinish.Roof),
        new Part("Cooling Roof Underglow", -.6f, 3.53f, -68f, 3.9f, .055f, 5.05f, PartFinish.Cyan, false),
        new Part("Cooling Upright Server", 2.95f, 2.02f, -62.8f, 1.85f, 2.96f, 1.6f, PartFinish.Armor),
        new Part("Split Lane Transformer", .65f, 1.70f, -46f, 1.85f, 2.42f, 1.55f, PartFinish.Armor, true, 0, 21),
        new Part("Terminal Left Shutter", -2.75f, 1.43f, -13f, 2.2f, 1.82f, 1.05f, PartFinish.Hazard, true, 0, -18),
        new Part("Cable Bay Suspended Baffle", .35f, 3.75f, -1f, 3.85f, .40f, 5.2f, PartFinish.Roof),
        new Part("Cable Bay Roof Underglow", .35f, 3.52f, -1f, 3.45f, .05f, 4.9f, PartFinish.Cyan, false),
        new Part("Cable Bay Right Shield", 2.95f, 1.45f, 1.5f, 2.3f, 1.95f, 1.1f, PartFinish.Hazard, true, 0, 27),
        new Part("Ramp Junction Suspended Shield", -1.5f, 3.65f, 29.3f, 3.1f, .42f, 4.9f, PartFinish.Roof),
        new Part("Lower Route Left Barricade", -2.75f, 1.48f, 34f, 2.45f, 1.95f, 1.3f, PartFinish.Hazard, true, 0, 23),
        new Part("Crossfire Electronics Cabinet", -2.0f, 1.94f, 51f, 2.0f, 2.87f, 1.25f, PartFinish.Armor),
        // This canopy is above player head-height on the gantry, not at floor height.
        // Ceiling bot at z=68 cannot see a player underneath its solid roof.
        new Part("Catwalk Overhead Raycast Shield", 3.05f, 6.72f, 68f, 2.65f, .30f, 7.0f, PartFinish.Roof),
        new Part("Catwalk Ceiling Shield Underglow", 3.05f, 6.54f, 68f, 2.35f, .045f, 6.7f, PartFinish.Cyan, false),
        new Part("Lower Ramp Cover Block", -2.6f, 1.62f, 82.5f, 2.2f, 2.2f, 1.25f, PartFinish.Armor),
        new Part("Vault Scanner Hideout Roof", 1.95f, 3.72f, 91f, 3.3f, .38f, 4.5f, PartFinish.Roof),
        new Part("Vault Right Side Rack", 2.85f, 1.85f, 99.2f, 1.95f, 2.65f, 1.65f, PartFinish.Armor),
        new Part("Vault Center Knee Wall", -.25f, 1.24f, 114.5f, 2.3f, 1.5f, 1.05f, PartFinish.Hazard, true, 0, -20),
        new Part("Exit Approach Short Barricade", .25f, 1.28f, 126f, 2.2f, 1.55f, 1.25f, PartFinish.Armor),

        // Wall-to-wall visual rhythm breaks: local fins, vertical bands, wall panels.
        new Part("MAINTENANCE BAY L", -4.78f, 3.0f, -96f, .32f, 4.1f, 14f, PartFinish.Wall, false),
        new Part("MAINTENANCE BAY R", 4.78f, 3.0f, -96f, .32f, 4.1f, 14f, PartFinish.Wall, false),
        new Part("MAINTENANCE WARNING", -4.59f, 4.8f, -96f, .12f, .15f, 12f, PartFinish.Hazard, false),
        new Part("COOLING BAY L", -4.79f, 3.1f, -52f, .32f, 4.3f, 18f, PartFinish.Wall, false),
        new Part("COOLING BAY VENT", -4.61f, 3.2f, -52f, .14f, 2.65f, 7f, PartFinish.Pipe, false),
        new Part("COOLING BAY R", 4.79f, 3.3f, -52f, .32f, 3.7f, 18f, PartFinish.Wall, false),
        new Part("GANTRY WALL STRIP R", 4.70f, 5.95f, 56f, .12f, .2f, 64f, PartFinish.Cyan, false),
        new Part("GANTRY WALL STRIP L", -4.71f, 5.95f, 56f, .12f, .2f, 64f, PartFinish.Hazard, false),
        new Part("VAULT SIDE PANEL L", -4.76f, 3.1f, 108f, .32f, 4.0f, 27f, PartFinish.Wall, false),
        new Part("VAULT SIDE PANEL R", 4.76f, 3.1f, 108f, .32f, 4.0f, 27f, PartFinish.Wall, false),
        new Part("VAULT RED STATUS", 4.58f, 5f, 108f, .12f, .14f, 23f, PartFinish.Hazard, false),
        new Part("EXIT SIDE MARKER", -4.63f, 2.15f, 137f, .1f, 2.8f, .38f, PartFinish.Cyan, false)
    };

    // V5 moving canopy configuration. Every name corresponds to a real solid
    // PartFinish.Roof entry, and each glowName is visually attached to the roof.
    // Travel stays inside the corridor and above the player's standing head.
    // X axes create lateral opening/closing cover; Z axes make advancing cover.
    public struct RoofMotion
    {
        public string roofName, glowName;
        public Vector3 axis;
        public float halfTravel, cyclesPerSecond;
        public bool reverse;
        public RoofMotion(string roof, string glow, Vector3 direction,
            float travel, float frequency, bool reverseMotion = false)
        {
            roofName = roof; glowName = glow; axis = direction;
            halfTravel = travel; cyclesPerSecond = frequency; reverse = reverseMotion;
        }
    }

    public static readonly RoofMotion[] MovingRoofs =
    {
        new RoofMotion("02 FLOATING SHADOW ROOF", "02 ROOF STRIP", Vector3.right, .72f, .16f),
        new RoofMotion("03 SHADOW CANOPY", "03 CANOPY STRIP", Vector3.forward, .95f, .18f, true),
        new RoofMotion("Cooling Suspended Roof", "Cooling Roof Underglow", Vector3.right, .82f, .21f, true),
        new RoofMotion("Cable Bay Suspended Baffle", "Cable Bay Roof Underglow", Vector3.right, .9f, .19f),
        new RoofMotion("Ramp Junction Suspended Shield", "", Vector3.right, .82f, .16f, true),
        new RoofMotion("Catwalk Overhead Raycast Shield", "Catwalk Ceiling Shield Underglow", Vector3.forward, 1.05f, .17f),
        new RoofMotion("05 FLOATING BLIND", "05 FLOATING BLIND LIGHT", Vector3.right, .66f, .19f, true),
        new RoofMotion("Vault Scanner Hideout Roof", "", Vector3.right, .70f, .23f)
    };

    public static readonly Intel[] Intels =
    {
        new Intel("ENTRY FILE", 2.8f, 1.85f, -139f),
        new Intel("MAINTENANCE FILE", -3.25f, 1.85f, -108f),
        new Intel("COOLING FILE", 3.15f, 1.85f, -75f),
        new Intel("SECURITY FILE", -3.0f, 1.85f, -43f),
        new Intel("GANTRY ACCESS KEY", 3.0f, 5.35f, 62f, true),
        new Intel("VAULT FILE", 2.95f, 1.85f, 119f)
    };

}
