using System.Collections.Generic;
using UnityEngine;

namespace C3TreasurePuzzle
{
    /// <summary>
    /// Scene-owned circulation, installed after the workshop and reference factory by
    /// C3FactoryPresentation. Retains its original component/GUID; no parallel generator.
    /// V11 uses Unity-space primitives for floors/treads and matching rail profiles,
    /// rather than overlapping V3-V9 OBJ visuals with independently authored colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class C3V3Circulation : MonoBehaviour
    {
        public const float GroundSurface = .56f;
        public const float WalkSurface = 4.50f;
        public const float WestStairX = -23.20f;
        public const float EastStairX = -1.55f;
        public const float MainWalkZ = 19.20f;
        public const float StairStartZ = 3.80f;
        public const float StairTopZ = 13.70f;
        public const float RouteWidth = 1.80f;
        public const float RearSouthZ = 18.10f;
        public const float RearNorthZ = 21.30f;
        public const int StairSteps = 22;
        public const float GuardHeight = 1.24f;
        private const float DeckThickness = .18f;
        private const string RootName = "C3_V3_VerifiedCirculation";
        private Transform buildRoot;
        private Material deckMat, frameMat, railMat, treadMat, mechanismMat;
        private readonly List<Mesh> railMeshes = new List<Mesh>();
        private readonly List<Material> signMaterials = new List<Material>();

        private void Awake()
        {
            // Scoped idempotence: disabling/re-enabling this component does not rebuild.
            if (transform.Find(RootName)) return;
            RetireLegacyStructure();
            buildRoot = new GameObject(RootName).transform;
            buildRoot.SetParent(transform, true);
            // All layout coordinates are world space, independent of the owner pose.
            buildRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Shader shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Diffuse");
            deckMat = NewMaterial(shader, "V11 steel walking surface", new Color(.31f, .38f, .40f));
            treadMat = NewMaterial(shader, "V11 stair treads", new Color(.40f, .48f, .52f));
            frameMat = NewMaterial(shader, "V11 galvanized steel", new Color(.57f, .63f, .66f));
            railMat = NewMaterial(shader, "V11 safety yellow", new Color(.98f, .61f, .065f));
            mechanismMat = NewMaterial(shader, "V10 retained conveyor frame", new Color(.21f, .26f, .29f));

            BuildStair(WestStairX, "A_WEST");
            BuildStair(EastStairX, "B_EAST");
            float routeLength = RearSouthZ - StairTopZ;
            Deck("2F_WEST_SERVICE_ROUTE", WestStairX, (StairTopZ + RearSouthZ) * .5f, RouteWidth, routeLength);
            Deck("2F_EAST_SERVICE_ROUTE", EastStairX, (StairTopZ + RearSouthZ) * .5f, RouteWidth, routeLength);
            float west = WestStairX - RouteWidth * .5f;
            float east = EastStairX + RouteWidth * .5f;
            Deck("2F_CONNECTING_REAR_ROUTE", (west + east) * .5f,
                (RearSouthZ + RearNorthZ) * .5f, east - west, RearNorthZ - RearSouthZ);
            BuildUpperGuards(west, east);
            BuildSupports(west, east);
            GameObject floorSign = C3BakedSignFactory.AtWorld("SecondFloor",
                new Vector3(-12.5f, 6.05f, 24.53f), Quaternion.Euler(0f, 180f, 0f), 3f, .6f, false);
            OwnSign(floorSign, buildRoot);

            // Phase 2 owns the complete replacement equipment; retain the old OBJ assets only.
        }

        private static void RetireLegacyStructure()
        {
            // Exact leaf allowlist from C_Main V10. Do NOT disable the root, the
            // Floor1_Additions/FutureGameplayMarkers groups, or arbitrary colliders.
            GameObject blockout = GameObject.Find("C_TwoFloor_Blockout");
            if (!blockout) return;
            Transform root = blockout.transform;
            foreach (string stair in new[] { "Stair_West_Up", "Stair_East_Down" })
                for (int i = 1; i <= 22; i++)
                {
                    RetireLeaf(root, "Stairs/" + stair + "/Tread_" + i.ToString("D2"));
                    foreach (string side in new[] { "Left", "Right" })
                        RetireLeaf(root, "Railings/R_" + stair + "/Guard" + side + "_" + i.ToString("D2"));
                }
            foreach (string item in new[] { "F2_MainWalkway", "F2_ControlChipBay", "F2_ObservationDeck" })
                RetireLeaf(root, "Floor2_Platforms/" + item);
            foreach (string item in new[] { "R_WestEnd", "R_EastEnd", "R_MainSouth", "R_NorthWest",
                "R_NorthBetweenBays", "R_ChipBayWest", "R_ChipBayEast", "R_ChipBayNorth",
                "R_ObservationWest", "R_ObservationNorth" })
                RetireLeaf(root, "Railings/" + item);
            foreach (string x in new[] { "m21p5", "m17p5", "m10p0", "m5p5" })
                foreach (string z in new[] { "14p5", "16p5" })
                    RetireLeaf(root, "Supports/S_Main_" + x + "_" + z);
            foreach (string x in new[] { "m13p5", "m11p5", "m8p5", "m5p5" })
                RetireLeaf(root, "Supports/S_Bay_" + x);
        }

        private static void RetireLeaf(Transform root, string path)
        {
            Transform item = root.Find(path);
            if (!item) return;
            // Refuse to retire a leaf if future revisions add gameplay to it.
            if (item.GetComponent<MonoBehaviour>() || item.childCount != 0) return;
            foreach (Collider collider in item.GetComponents<Collider>())
                if (collider.isTrigger) return;
            foreach (Collider collider in item.GetComponents<Collider>()) collider.enabled = false;
            foreach (Renderer renderer in item.GetComponents<Renderer>()) renderer.enabled = false;
        }

        private Material NewMaterial(Shader shader, string name, Color color)
        {
            Material mat = new Material(shader) { name = name, color = color };
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", .45f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", .38f);
            return mat;
        }

        private Transform Group(string name)
        {
            Transform item = new GameObject(name).transform;
            item.SetParent(buildRoot, false);
            return item;
        }

        private GameObject Box(Transform parent, string name, Vector3 center, Vector3 size,
            Material material, bool solid = true, Quaternion? rotation = null)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.SetPositionAndRotation(center, rotation ?? Quaternion.identity);
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            // Cube collider and render mesh share the exact same transform and size.
            if (!solid)
            {
                Collider collider = item.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
            }
            return item;
        }

        private void Deck(string name, float x, float z, float width, float length)
        {
            Box(buildRoot, name, new Vector3(x, WalkSurface - DeckThickness * .5f, z),
                new Vector3(width, DeckThickness, length), deckMat);
        }

        private void BuildStair(float x, string side)
        {
            Transform stair = Group("STAIR_" + side);
            float rise = (WalkSurface - GroundSurface) / StairSteps; // .1791 < production stepOffset .30
            float run = (StairTopZ - StairStartZ) / StairSteps;      // .45; pitch 21.7 < slopeLimit 45
            Box(stair, "BOTTOM_LANDING", new Vector3(x, GroundSurface - .06f, StairStartZ - .60f),
                new Vector3(RouteWidth, .12f, 1.20f), deckMat);
            for (int i = 0; i < StairSteps; i++)
            {
                float top = GroundSurface + rise * (i + 1);
                float z = StairStartZ + run * (i + .5f);
                // Physical tread plates, not a hidden ramp. Keep the space BELOW
                // the upper flight open for the existing ground-level B/C doorway.
                Box(stair, "TREAD_" + (i + 1).ToString("D2"),
                    new Vector3(x, top - rise * .5f, z),
                    new Vector3(RouteWidth, rise, run), treadMat);
                Box(stair, "NOSING_" + (i + 1).ToString("D2"),
                    new Vector3(x, top + .004f, z - run * .5f + .025f),
                    new Vector3(RouteWidth, .008f, .05f), railMat, false);
            }
            for (int edge = -1; edge <= 1; edge += 2)
            {
                float railX = x + edge * RouteWidth * .5f;
                Vector3 stringerStart = new Vector3(railX, GroundSurface - .15f, StairStartZ);
                Vector3 stringerEnd = new Vector3(railX, WalkSurface - .15f, StairTopZ);
                Vector3 stringer = stringerEnd - stringerStart;
                Box(stair, "STRINGER_" + edge, (stringerStart + stringerEnd) * .5f,
                    new Vector3(.10f, .18f, stringer.magnitude), frameMat,
                    true, Quaternion.LookRotation(stringer));
                Box(stair, "ENTRY_POST_" + edge,
                    new Vector3(railX, GroundSurface + 1.25f, StairStartZ - 1.2f),
                    new Vector3(.08f, 2.5f, .08f), frameMat);
                Guard(stair, "GUARD_BOTTOM_" + edge,
                    new Vector3(railX, GroundSurface, StairStartZ - 1.2f),
                    new Vector3(railX, GroundSurface, StairStartZ));
                Guard(stair, "GUARD_FLIGHT_" + edge,
                    new Vector3(railX, GroundSurface, StairStartZ),
                    new Vector3(railX, WalkSurface, StairTopZ));
            }
            Box(stair, "ENTRY_HEADER", new Vector3(x, GroundSurface + 2.5f, StairStartZ - 1.2f),
                new Vector3(RouteWidth + .08f, .08f, .08f), railMat);
            GameObject sign = C3BakedSignFactory.AtWorld("StairEntry",
                new Vector3(x, GroundSurface + 2.5f, StairStartZ - 1.23f),
                Quaternion.identity, 1.55f, .4f, true);
            if (sign)
            {
                sign.name = "STAIR_" + side + "_ENTRY_SIGN";
                OwnSign(sign, stair);
            }
        }

        private void OwnSign(GameObject sign, Transform parent)
        {
            if (!sign) return;
            sign.transform.SetParent(parent, true);
            // BakedSignFactory creates per-sign backing/ink material instances.
            foreach (Renderer renderer in sign.GetComponentsInChildren<Renderer>())
                foreach (Material material in renderer.sharedMaterials)
                    if (material && !signMaterials.Contains(material)) signMaterials.Add(material);
        }

        private void BuildUpperGuards(float west, float east)
        {
            Transform rails = Group("V11_UPPER_GUARDS");
            Guard(rails, "GUARD_WEST_OUTER", new Vector3(west, WalkSurface, StairTopZ), new Vector3(west, WalkSurface, RearNorthZ));
            Guard(rails, "GUARD_WEST_INNER", new Vector3(west + RouteWidth, WalkSurface, StairTopZ), new Vector3(west + RouteWidth, WalkSurface, RearSouthZ));
            Guard(rails, "GUARD_REAR_NORTH", new Vector3(west, WalkSurface, RearNorthZ), new Vector3(east, WalkSurface, RearNorthZ));
            Guard(rails, "GUARD_REAR_SOUTH", new Vector3(west + RouteWidth, WalkSurface, RearSouthZ), new Vector3(east - RouteWidth, WalkSurface, RearSouthZ));
            Guard(rails, "GUARD_EAST_WALL_OUTER", new Vector3(east, WalkSurface, StairTopZ), new Vector3(east, WalkSurface, RearNorthZ));
            Guard(rails, "GUARD_EAST_WALL_INNER_AFTER_STAIR", new Vector3(east - RouteWidth, WalkSurface, StairTopZ), new Vector3(east - RouteWidth, WalkSurface, RearSouthZ));
        }

        private void Guard(Transform parent, string name, Vector3 a, Vector3 b)
        {
            Transform rail = new GameObject(name).transform;
            rail.SetParent(parent, false);
            Vector3 delta = b - a;
            Vector3 across = Vector3.Cross(Vector3.up, delta).normalized * .04f;
            // One closed convex safety profile spans toe to top, including for prone
            // players. It follows the visible rail envelope, never a route entrance.
            Vector3 up = Vector3.up * GuardHeight;
            Mesh mesh = new Mesh { name = name + "_SafetyProfile" };
            mesh.vertices = new[] { a - across, a + across, b + across, b - across,
                a - across + up, a + across + up, b + across + up, b - across + up };
            mesh.triangles = new[] { 0,1,2, 0,2,3, 4,6,5, 4,7,6,
                0,5,1, 0,4,5, 1,6,2, 1,5,6, 2,7,3, 2,6,7, 3,4,0, 3,7,4 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            railMeshes.Add(mesh);
            MeshCollider barrier = rail.gameObject.AddComponent<MeshCollider>();
            barrier.sharedMesh = mesh;
            barrier.convex = true;
            foreach (float height in new[] { .12f, .60f, 1.20f })
                Box(rail, height > 1f ? "TOP_RAIL" : height > .5f ? "MID_RAIL" : "TOE_RAIL",
                    (a + b) * .5f + Vector3.up * height, new Vector3(.08f, .08f, delta.magnitude),
                    railMat, false, Quaternion.LookRotation(delta));
            int spans = Mathf.CeilToInt(delta.magnitude / 1.2f);
            for (int i = 0; i <= spans; i++)
                Box(rail, "POST_" + i, Vector3.Lerp(a, b, i / (float)spans) + up * .5f,
                    new Vector3(.08f, GuardHeight, .08f), railMat, false);
        }

        private void BuildSupports(float west, float east)
        {
            Transform supports = Group("V11_EDGE_SUPPORTS");
            // Edge-mounted, entirely below deck; never in the upper walking envelope.
            foreach (float x in new[] { west, east })
                foreach (float z in new[] { 14.5f, 17.5f, 20.9f })
                    Box(supports, "COLUMN_" + x + "_" + z,
                        new Vector3(x, (GroundSurface + WalkSurface - DeckThickness) * .5f, z),
                        new Vector3(.14f, WalkSurface - DeckThickness - GroundSurface, .14f), frameMat);
        }

        private void OnDestroy()
        {
            if (buildRoot)
            {
                buildRoot.gameObject.SetActive(false);
                Destroy(buildRoot.gameObject);
            }
            foreach (Mesh mesh in railMeshes) if (mesh) Destroy(mesh);
            foreach (Material material in signMaterials) if (material) Destroy(material);
            foreach (Material mat in new[] { deckMat, frameMat, railMat, treadMat, mechanismMat })
                if (mat) Destroy(mat);
        }
    }
}
