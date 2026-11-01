using System;
using System.Collections.Generic;
using UnityEngine;

namespace C3TreasurePuzzle
{
    // Imported, shared OBJ meshes + Standard materials. No per-frame/per-session
    // Mesh, Texture or Material allocations. Models contain NO gameplay identity.
    [DisallowMultipleComponent]
    public sealed class C3RecyclingVisuals : MonoBehaviour
    {
        public const int VariantsPerMaterial=5;
        public const string RawRootName="RAW_MATERIAL_VISUAL_ROOT";
        private const string ResourceRoot="RecyclingArt/";
        private static readonly string[][] RawNames={
            new[] {"Paper_Newspaper","Paper_CardboardBox","Paper_FoldedCarton","Paper_PaperBag","Paper_EggCarton"},
            new[] {"Metal_AluminumCan","Metal_CrushedCan","Metal_TinCan","Metal_CrownCap","Metal_FoodTin"},
            new[] {"Plastic_PETBottle","Plastic_DrinkCup","Plastic_DetergentBottle","Plastic_Clamshell","Plastic_CrushedBottle"}
        };
        private readonly Dictionary<string,GameObject> models=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        public int LoadedModelCount => models.Count;
        public int LoadedMaterialCount => materials.Count;
        private GameObject Model(string name)
        {
            if (!models.TryGetValue(name,out GameObject asset))
            {
                asset=Resources.Load<GameObject>(ResourceRoot+"Meshes/"+name);
                if (!asset) throw new InvalidOperationException("Missing recycling model: "+name);
                models.Add(name,asset);
            }
            return asset;
        }
        private Material Material(string name)
        {
            if (!materials.TryGetValue(name,out Material asset))
            {
                asset=Resources.Load<Material>(ResourceRoot+"Materials/"+name);
                if (!asset || !asset.shader) throw new InvalidOperationException("Missing recycling material: "+name);
                materials.Add(name,asset);
            }
            return asset;
        }
        private static Transform Part(GameObject model,Material material,Transform parent,string name)
        {
            GameObject item=Instantiate(model,parent,false); item.name=name;
            item.transform.localPosition=Vector3.zero; item.transform.localRotation=Quaternion.identity; item.transform.localScale=Vector3.one;
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial=material;
            return item.transform;
        }
        public Transform CreateRaw(EcoRecycleCargo.Kind kind,int variant,Transform parent)
        {
            int lane=C3RecyclingLine.MachineIndex(kind);
            if (lane<0) throw new ArgumentOutOfRangeException(nameof(kind));
            variant=Mathf.Abs(variant%VariantsPerMaterial); string name=RawNames[lane][variant];
            GameObject body=Model(name+"_Body"),detail=Model(name+"_Details");
            Material material=Material("Raw_"+kind);
            Transform root=new GameObject(RawRootName).transform; root.SetParent(parent,false);
            try
            {
                Part(body,material,root,name+"_Body"); Part(detail,material,root,name+"_Details");
                return root;
            }
            catch { root.gameObject.SetActive(false); Destroy(root.gameObject); throw; }
        }
        public Transform CreateBale(EcoRecycleCargo.Kind kind,Transform parent)
        {
            if (C3RecyclingLine.MachineIndex(kind)<0) throw new ArgumentOutOfRangeException(nameof(kind));
            GameObject shell=Model("Bale_"+kind+"_Shell"),front=Model("Bale_"+kind+"_Front"),straps=Model("Bale_Straps");
            Material sideMaterial=Material("BaleSide_"+kind),frontMaterial=Material("BaleFront_"+kind),binding=Material("Binding_Steel");
            Transform root=new GameObject(kind.ToString().ToUpperInvariant()+"_COMPRESSED_BALE").transform; root.SetParent(parent,false);
            try
            {
                Part(shell,sideMaterial,root,"VOLUME_SHELL"); Part(front,frontMaterial,root,"RELIEF_FRONT"); Part(straps,binding,root,"SOLID_BINDINGS");
                return root;
            }
            catch { root.gameObject.SetActive(false); Destroy(root.gameObject); throw; }
        }
        public Transform CreateStorage(EcoRecycleCargo.Kind kind,Transform parent,Vector3 groundPosition)
        {
            GameObject pallet=Model("Wood_Pallet"),stock=Model("Stock_"+kind),straps=Model("Bale_Straps");
            Material wood=Material("Pallet_Wood"),bale=Material("Stock_"+kind),binding=Material("Binding_Steel");
            Transform root=new GameObject("STORAGE_DECOR_"+kind).transform; root.SetParent(parent,false); root.position=groundPosition;
            Part(pallet,wood,root,"WOOD_PALLET");
            BoxCollider palletCollision=root.gameObject.AddComponent<BoxCollider>();
            palletCollision.center=Vector3.up*.07f; palletCollision.size=new Vector3(.96f,.14f,.84f);
            Transform load=new GameObject("DECORATIVE_STOCK_BALE").transform; load.SetParent(root,false);
            load.localPosition=Vector3.up*(.14f+.43f*1.35f*.5f); load.localScale=Vector3.one*1.35f;
            Part(stock,bale,load,"STOCK_VOLUME"); Part(straps,binding,load,"STOCK_BINDINGS");
            BoxCollider collider=load.gameObject.AddComponent<BoxCollider>(); collider.size=C3RecyclingLine.ProductSize;
            return root;
        }
        public static bool FitsRawEnvelope(Transform cargo)
        {
            // Keep the existing per-renderer AABB size check at ingress too.
            // Check the UNION of real mesh vertices, including offsets; a set of
            // tiny renderers spread over a large area must not bypass the proxy.
            Renderer[] renderers=cargo.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length==0) return false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer.transform==cargo) return false; // cannot safely squash a gameplay root renderer
                MeshFilter mesh=renderer.GetComponent<MeshFilter>();
                if (!mesh || !mesh.sharedMesh || !mesh.sharedMesh.isReadable) return false;
                foreach (Vector3 vertex in mesh.sharedMesh.vertices)
                {
                    Vector3 p=cargo.InverseTransformPoint(mesh.transform.TransformPoint(vertex));
                    if (float.IsNaN(p.sqrMagnitude) || float.IsInfinity(p.sqrMagnitude) ||
                        p.sqrMagnitude>(C3RecyclingLineGeometry.RawRadius+.0001f)*(C3RecyclingLineGeometry.RawRadius+.0001f)) return false;
                }
            }
            return true;
        }
        public static Transform UnifyRawVisualRoot(Transform cargo)
        {
            Transform root=cargo.Find(RawRootName);
            if (!root) { root=new GameObject(RawRootName).transform; root.SetParent(cargo,false); }
            // Reparent only top-level renderer branches, never the cargo itself,
            // its gameplay identity or unrelated children such as an ingress API.
            HashSet<Transform> branches=new HashSet<Transform>();
            foreach (Renderer renderer in cargo.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(root)) continue;
                Transform branch=renderer.transform;
                while (branch.parent && branch.parent!=cargo) branch=branch.parent;
                if (branch!=cargo && branch!=root) branches.Add(branch);
            }
            foreach (Transform branch in branches) branch.SetParent(root,true);
            return root;
        }
    }
}
