STEALTH CORRIDOR V3 - DENSE PATROLS (SEPARATE UNITY PROJECT)
===========================================================
Unity editor: 6000.3.9f1. Open Assets/Scenes/LaserCorridor.unity and press Play.

WHAT CHANGED FROM V2
- 20 moving security scanner bots (V2 had 8) with typical z separations
  of 12-18 Unity units rather than large 30-50 unit empty gaps.
- Added staggered shields, machinery, overhead cover and catwalk roof,
  each near active scanners so hiding and line of sight matter.
- Preserved CheckSphere, CheckBox, CheckCapsule, FOV and occluding Raycasts.
- Removed the numbered sector name that used to remain on screen
  between important HUD alerts. Objective / detection HUD remains.
- Editor preview shares runtime Guard/Part layout. It is rebuilt on
  opening the scene if it is still the V2 preview. If needed manually:
  Tools > Laser Corridor > Rebuild Editable Scene Geometry.

CONTROL KEYS
WASD movement; mouse look; Shift sprint; Ctrl/C crouch; Space jump;
F flashlight; E collect item; R restart; Esc releases cursor.

MISSION
Collect 3 regular intel cores (5 scattered) plus the Gantry Access Key
on the raised right-side catwalk, then reach the far exit.

DEMO REQUIRED FOR SUBMISSION
Capture gameplay directly from Unity. Show two adjacent moving patrols,
use nearby cover to interrupt their raycasts, and show an overhead bot
blocked by suspended cover. Show flashlight toggling to reveal a hidden
intel core; show an inspector for CheckSphere/CheckBox/CheckCapsule.

TEST IN UNITY BEFORE SUBMITTING
[ ] No Console compiler errors.
[ ] 20 patrol scanners are moving and detecting in their respective zones.
[ ] Cover blocks detection when in front of scanner line of sight.
[ ] Can walk up both ramps; overhead canopy on catwalk does not block head.
[ ] Can collect the catwalk key and three intel, and exit.
[ ] The centered numbered sector labels are no longer displayed.
[ ] Record and commit demo on a dedicated branch (e.g., mariella-stealth-v3).

Note: The included code/ZIP is statically checked, but actual Unity
physics/navigation must be verified in the Unity editor by the user.
