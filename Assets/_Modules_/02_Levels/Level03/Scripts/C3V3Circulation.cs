using System;
using UnityEngine;

namespace C3TreasurePuzzle
{
    /// <summary>
    /// V10 axis-corrected assembly (retains component name for scene/test compatibility).
    /// Unity's OBJ importer mirrors OBJ X when converting handedness. The V3-V9 OBJ
    /// source files incorrectly used Unity world X unconverted, so stair and B-feed
    /// meshes rendered ACROSS the east wall instead of atop their C-hall colliders.
    /// All C3V3Models OBJ vertices now pre-negate X; their faces and normals are
    /// mirrored as well. This component checks the actual imported renderer.bounds
    /// rather than accepting a nonzero size as evidence of correct placement.
    /// The walkable deck,
    /// stair treads, frames and yellow railings are imported *authored OBJ meshes*
    /// (Resources/C3V3Models). Only invisible collision planes are constructed here.
    /// The pedestrian bridge runs BEHIND the sorter and outside the horizontal
    /// extent of the live high-level belt. Therefore it does not intersect the belt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class C3V3Circulation : MonoBehaviour
    {
        public const float WalkSurface = 4.50f;
        public const float WestStairX = -23.20f;
        public const float EastStairX = -1.55f;
        public const float MainWalkZ = 19.20f;
        public const float StairStartZ = 3.80f; // west entrance; east entrance is z=6.00
        public const float StairTopZ = 13.70f; // west staircase; east top=10.10
        private Transform buildRoot;
        private Material deckMat, frameMat, railMat, treadMat, mechanismMat;
        private static readonly string[] Legacy =
        {
            "Stairs", "Floor2_Platforms", "Railings", "Supports"
        };

        private void Awake()
        {
            // Deactivate *both mesh and collider* of the old floating stairs,
            // obstructing south safety rail and mezzanine collision proxies.
            GameObject blockout = GameObject.Find("C_TwoFloor_Blockout");
            if (blockout)
            {
                foreach (string name in Legacy)
                {
                    Transform t = blockout.transform.Find(name);
                    if (t) t.gameObject.SetActive(false);
                    else Debug.LogWarning("V3 expected old blockout child missing: " + name);
                }
            }
            // Deactivate all old structural blockout collision, including nested old
            // pillars outside the four legacy groups. Never touch gameplay triggers.
            if (blockout)
                foreach (Collider oldCollider in blockout.GetComponentsInChildren<Collider>(true))
                    if (oldCollider && !oldCollider.isTrigger) oldCollider.enabled = false;
            // The original 2F chip and observation controls still belong to gameplay;
            // their small upper-deck cabinets must remain VISIBLE on the new bridge.
            foreach (string liveStation in new[] { "UpperChipMaintenanceBench", "UpperDistributionBox" })
            {
                GameObject station = GameObject.Find(liveStation);
                if (!station) continue;
                foreach (Renderer renderer in station.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = true;
            }
            if (GameObject.Find("C3_V3_VerifiedCirculation")) return;
            buildRoot = new GameObject("C3_V3_VerifiedCirculation").transform;
            buildRoot.SetParent(transform, false);
            Shader shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Diffuse");
            deckMat = NewMaterial(shader, "V3 diamond-plate walking surface", new Color(.31f,.38f,.40f));
            treadMat = NewMaterial(shader, "V3 high-contrast stair tread", new Color(.40f,.48f,.52f));
            frameMat = NewMaterial(shader, "V3 galvanized structural steel", new Color(.57f,.63f,.66f));
            railMat = NewMaterial(shader, "V3 safety yellow continuous rail", new Color(.98f,.61f,.065f));
            mechanismMat = NewMaterial(shader, "V3 bounded conveyor frame", new Color(.21f,.26f,.29f));

            // V8 primary stair: visible from the C floor, safely offset from the sorting machines.
            // V9 moves the sole primary entrance to the visible right-hand C hall aisle.
            // Each piece has outward-facing OBJ normals and proper frame/rail geometry.
            Visual("V9_RightVisibleStairTreads", treadMat);
            Visual("V9_RightVisibleStairFrame", frameMat);
            Visual("V9_RightVisibleStairGuardrails", railMat);
            Visual("V9_StairGatewayArch", railMat);
            Visual("V3_WalkwayDeck", deckMat);
            Visual("V3_WalkwayUnderframe", frameMat);
            Visual("V8_2F_RailNetwork", railMat);
            Visual("V3_WalkwaySupportPosts", frameMat);
            Visual("V7_EastWallSkywalkDeck", deckMat);
            Visual("V7_EastWallSkywalkFrame", frameMat);
            Visual("V9_EastGalleryRailingWithStairEntrance", railMat);
            Visual("V7_EastGalleryRailWithConnectorGap", railMat);
            Visual("V3_HopperShields", frameMat);
            Visual("V3_InfeedCarrierFrame", mechanismMat);
            Visual("V3_InfeedDrums", frameMat);
            Visual("V3_OutfeedCarriers", mechanismMat);
            // V8 B doorway -> C main sorter: a SINGLE visible, cleated, inclined feed.
            // Its portal endpoint stops outside the B wall; B interior stays opaque.
            Visual("V9_BDoorToSorterVisibleBelt", mechanismMat);
            Visual("V9_BDoorToSorterSteelFrame", frameMat);
            Visual("V9_BDoorToSorterYellowGuards", railMat);
            Visual("V9_BDoorToSorterRollers", frameMat);

            // Continuous second-floor service circulation: all walkable tops at y=4.50.
            WalkCollider("2F_WEST_SERVICE_ROUTE", WestStairX, 16.00f, 1.80f, 5.50f);
            WalkCollider("2F_EAST_SERVICE_ROUTE", EastStairX, 16.00f, 1.74f, 5.50f);
            WalkCollider("2F_CONNECTING_REAR_ROUTE", -13.925f, MainWalkZ, 20.29f, 2.16f);
            // East-side overhead inspection branch joins the rear gallery;
            // the old east ground-level stair is not a phantom physical blocker.
            WalkCollider("2F_EAST_WALL_CONNECTING_ROUTE",EastStairX,13.65f,1.72f,7.30f);
            WalkCollider("2F_EAST_REAR_TRANSFER_ROUTE",-3.09f,17.20f,3.12f,1.80f);
            BuildV9VisibleRightStairCollision();
            // Old, off-screen east stair was retired, including its invisible collision ramp.
            // The east gallery is reachable via the rear connecting bridge.
            BuildRailCollision();
            BuildSupportCollision();
            // Small entrance signs are fixed to the physical yellow stair arches.
            C3BakedSignFactory.AtWorld("StairEntry",
                new Vector3(-2.95f,3.06f,2.49f),Quaternion.identity,2.10f,.52f,false);
            // SecondFloor image is bolted to rear wall rather than floating in space.
            C3BakedSignFactory.AtWorld("SecondFloor", new Vector3(-12.5f, 6.05f, 24.53f),
                Quaternion.Euler(0f,180f,0f), 3.0f, .60f, false);
            Debug.Log("C3 V10: corrected Unity OBJ handedness for stair, catwalk and B receiving belt; auditing imported WORLD bounds in Start(). PlayMode walking unverified.");
        }

        // Do not advertise "ACTIVE" merely because a mesh exists. The imported
        // renderer must occupy its intended C-hall world-space X / Y / Z bounds.
        private bool v10BoundsPassed;
        private string v10Status = "V10 CHECKING MESH POSITIONS";
        private void OnGUI()
        {
            GUI.Label(new Rect(Mathf.Max(0, Screen.width - 365), 10, 360, 30), v10Status);
        }

        private bool CheckWorldBounds(string assetName, Vector3 min, Vector3 max, float tolerance)
        {
            Transform item = buildRoot ? buildRoot.Find(assetName) : null;
            Renderer renderer = item ? item.GetComponentInChildren<Renderer>(true) : null;
            if (!renderer || !renderer.enabled)
            {
                Debug.LogError("V10 MISSING MESH/RENDERER: " + assetName);
                return false;
            }
            Bounds actual = renderer.bounds;
            bool ok =
                Vector3.Distance(actual.min, min) < tolerance &&
                Vector3.Distance(actual.max, max) < tolerance;
            if (!ok)
                Debug.LogError("V10 WORLD MESH BOUNDS WRONG: " + assetName +
                    " actual min=" + actual.min.ToString("F2") +
                    " max=" + actual.max.ToString("F2") +
                    "; EXPECTED min=" + min.ToString("F2") +
                    " max=" + max.ToString("F2") +
                    ". Do not move colliders to the mirrored render mesh.");
            else
                Debug.Log("V10 WORLD MESH BOUNDS OK: " + assetName +
                    " min=" + actual.min.ToString("F2") +
                    " max=" + actual.max.ToString("F2"));
            return ok;
        }

        private void AuditV10RuntimeAssets()
        {
            bool stair = CheckWorldBounds("V9_RightVisibleStairTreads",
                new Vector3(-4.01f,.56f,2.42f), new Vector3(-1.11f,4.50f,11.74f),.24f);
            bool feed = CheckWorldBounds("V9_BDoorToSorterVisibleBelt",
                new Vector3(-7.49f,1.13f,11.26f), new Vector3(-.64f,6.71f,17.37f),.24f);
            bool upper = CheckWorldBounds("V3_WalkwayDeck",
                new Vector3(-24.10f,4.32f,13.25f), new Vector3(-3.75f,4.50f,20.28f),.24f);
            bool branch = CheckWorldBounds("V7_EastWallSkywalkDeck",
                new Vector3(-4.65f,4.32f,10.0f), new Vector3(-.69f,4.50f,18.10f),.24f);
            v10BoundsPassed = stair && feed && upper && branch;
            v10Status = v10BoundsPassed
                ? "V10 MESH POSITIONS OK | WALK TEST PENDING"
                : "V10 MESH MISPLACED - CHECK CONSOLE";
        }

        private void Start()
        {
            AuditV10RuntimeAssets();
            // M4 real maintenance bench was hidden together with the old art pass.
            // Re-enable this REAL upper workbench after all factory Awake calls.
            foreach(string name in new[] {"UpperChipMaintenanceBench", "UpperDistributionBox"})
            {
                GameObject go=GameObject.Find(name);
                if (!go) { Debug.LogWarning("V8 missing original second-floor target: "+name); continue; }
                foreach(Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.enabled=true;
            }
        }

        private Material NewMaterial(Shader shader, string name, Color color)
        {
            Material mat = new Material(shader) { name = name, color = color };
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", .45f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", .38f);
            return mat;
        }
        private void Visual(string filename, Material mat)
        {
            GameObject asset = Resources.Load<GameObject>("C3V3Models/" + filename);
            if (!asset)
            {
                Debug.LogError("V10 authored factory mesh is missing: " + filename);
                return;
            }
            // The OBJ geometry deliberately stores world-space vertices. Unity
            // originally focused an empty imported parent at (0,0,0), misleading
            // users trying to locate the stair in Scene with F. Create an anchor
            // at its actual rendered centre, while leaving its WORLD geometry fixed.
            GameObject anchor = new GameObject(filename);
            anchor.transform.SetParent(buildRoot, false);
            GameObject piece = Instantiate(asset, Vector3.zero, Quaternion.identity, anchor.transform);
            piece.name = filename + "_Geometry";
            foreach (Renderer rend in piece.GetComponentsInChildren<Renderer>(true))
                rend.sharedMaterial = mat;
            foreach (Collider collider in piece.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            Renderer first = piece.GetComponentInChildren<Renderer>(true);
            if (first)
            {
                Vector3 actualCentre = first.bounds.center;
                // Use world-space centring and preserve world-space mesh bounds.
                anchor.transform.position = actualCentre;
                piece.transform.position = Vector3.zero;
            }
        }
        private GameObject ColliderObject(string name, Vector3 position, Vector3 size,
            Quaternion rotation)
        {
            GameObject holder = new GameObject(name);
            holder.transform.SetParent(buildRoot, true);
            holder.transform.SetPositionAndRotation(position, rotation);
            BoxCollider c = holder.AddComponent<BoxCollider>();
            c.size = size;
            return holder;
        }
        private void WalkCollider(string name, float x, float z, float width, float length)
        {
            // Models have top=4.50m, thickness=.18, centre=4.41m.
            ColliderObject(name, new Vector3(x,4.41f,z),new Vector3(width,.18f,length),
                Quaternion.identity);
        }
        private void BuildStairCollision(float x, string side)
        {
            // Measured from the visible stair tread OBJ: ground y=.56 -> deck y=4.50.
            // The WEST staircase starts near the entrance (z=3.8); the EAST
            // staircase begins behind the live quantum workbench (z=6.0).
            float start = x < -10f ? 3.80f : 2.70f;
            float top = x < -10f ? 13.70f : 10.10f;
            float rise = 3.94f;
            float run = top - start;
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float rampLength = Mathf.Sqrt(rise * rise + run * run);
            ColliderObject("STAIR_"+side+"_CONTINUOUS_WALKING_RAMP",
                new Vector3(x,2.48f,(start+top)*.5f),
                new Vector3(1.74f,.12f,rampLength), Quaternion.Euler(-angle,0f,0f));
            ColliderObject("STAIR_"+side+"_BOTTOM_LANDING",
                new Vector3(x,.56f,start-.46f),
                new Vector3(1.80f,.065f,.90f),Quaternion.identity);
        }
        // V9 stairs: actual tread mesh and collider share start, top and x coordinate.
        // Front-right entrance is in the sight line of the C-hall player.
        private void BuildV9VisibleRightStairCollision()
        {
            const float x=-2.95f, start=3.20f, top=10.30f, rise=3.94f;
            float angle=Mathf.Atan2(rise,top-start)*Mathf.Rad2Deg;
            float length=Mathf.Sqrt(rise*rise+(top-start)*(top-start));
            ColliderObject("V9_RIGHT_STAIR_WALKABLE_RAMP",new Vector3(x,2.53f,(start+top)*.5f),
                new Vector3(1.72f,.12f,length),Quaternion.Euler(-angle,0f,0f));
            ColliderObject("V9_RIGHT_STAIR_START",new Vector3(x,.59f,2.80f),
                new Vector3(2.05f,.07f,.76f),Quaternion.identity);
            ColliderObject("V9_RIGHT_STAIR_TOP",new Vector3(x,4.41f,10.76f),
                new Vector3(2.12f,.18f,1.32f),Quaternion.identity);
            ColliderObject("V9_RIGHT_STAIR_EAST_GALLERY_JOIN",new Vector3(-2.15f,4.41f,10.92f),
                new Vector3(2.08f,.18f,1.65f),Quaternion.identity);
            for(int side=-1;side<=1;side+=2)
                ColliderObject("V9_RIGHT_STAIR_GUARD_"+side,
                    new Vector3(x+side*.92f,3.03f,(start+top)*.5f),
                    new Vector3(.07f,1.10f,length),Quaternion.Euler(-angle,0f,0f));
        }
        private void SafetyStrip(string name, float x, float y, float z,
            float width, float depth)
        {
            ColliderObject("GUARD_"+name,new Vector3(x,y,z),
                new Vector3(width,1.07f,depth),Quaternion.identity);
        }
        private void BuildRailCollision()
        {
            // Authored V8 yellow network has a deliberate WEST-gallery doorway at z=15.3.
            // Collision follows the same opening, leaving x=-22.3 unobstructed.
            SafetyStrip("WEST_OUTER",-24.07f,5.03f,15.70f,.09f,4.75f);
            SafetyStrip("WEST_INNER",-22.30f,5.03f,15.66f,.09f,4.62f);
            SafetyStrip("REAR_NORTH",-13.925f,5.03f,20.28f,20.25f,.09f);
            SafetyStrip("REAR_SOUTH",-13.875f,5.03f,18.12f,16.65f,.09f);
            SafetyStrip("REAR_WEST_END",-24.07f,5.03f,19.20f,.09f,2.16f);
            SafetyStrip("REAR_EAST_END",-3.78f,5.03f,19.20f,.09f,2.16f);
            SafetyStrip("EAST_WALL_OUTER",-.67f,5.03f,13.65f,.09f,7.20f);
            // At z=10.0 to 11.8: no guard, the physical stair landing enters here.
            SafetyStrip("EAST_WALL_INNER_AFTER_STAIR",-2.43f,5.03f,13.95f,.09f,4.30f);
            // Leave a traversable transfer channel x=-4.6..-3.7 between the east
            // skywalk connector and the back 2F maintenance gallery.
            SafetyStrip("EAST_CONNECTOR_NORTH_INNER",-2.43f,5.03f,18.11f,.28f,.09f);
            SafetyStrip("EAST_CONNECTOR_SOUTH_OUTER",-4.08f,5.03f,16.28f,1.05f,.09f);
        }
        private void BuildSupportCollision()
        {
            foreach (float x in new[] { WestStairX })
                foreach (float z in new[] {14.55f,17.70f})
                    ColliderObject("BRIDGE_GROUNDED_COLUMN_"+x+"_"+z,
                        new Vector3(x + (x < -10f ? -.74f : .74f),2.40f,z),
                        new Vector3(.17f,3.83f,.17f),
                        Quaternion.identity);
            foreach (float z in new[] {12.00f,15.60f})
                ColliderObject("EAST_WALL_GALLERY_EDGE_SUPPORT_"+z,
                    new Vector3(-.88f,2.40f,z),new Vector3(.15f,3.80f,.15f),Quaternion.identity);
        }
        private void OnDestroy()
        {
            if (deckMat) Destroy(deckMat);
            if (frameMat) Destroy(frameMat);
            if (railMat) Destroy(railMat);
            if (treadMat) Destroy(treadMat);
            if (mechanismMat) Destroy(mechanismMat);
        }
    }
}
