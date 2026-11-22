using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SDGAdventure.ExperienceV2
{
    /// <summary>
    /// Visual-only dirt burst + scattering material pieces at the shovel impact moment.
    /// Never creates collectable items, grants rewards, or touches the existing digging rules.
    /// An active SDG_HandHeld_Shovel (provided by Experience v1) is required.
    /// </summary>
    public sealed class SDGLevel01ShovelVFX : MonoBehaviour
    {
        private sealed class FlyingPiece
        {
            public GameObject go;
            public Vector3 origin, velocity;
            public Vector3 spin;
            public float born, lifetime;
            public float originalScale;
        }

        private readonly List<FlyingPiece> flying = new List<FlyingPiece>(32);
        private Camera activeCamera;
        private Transform shovel;
        private float searchAt, lastStrike = -5f;
        private Material dirtMat, metalMat, plasticMat, boardMat;
        private bool missingToolLogged;
        private int maxLivePieces = 25;

        private void Start()
        {
            dirtMat = CreateMaterial(new Color(.48f, .37f, .24f));
            metalMat = CreateMaterial(new Color(.65f, .72f, .75f));
            plasticMat = CreateMaterial(new Color(.13f, .67f, .84f));
            boardMat = CreateMaterial(new Color(.08f, .59f, .37f));
        }

        private void Update()
        {
            AnimateFlyingPieces();
            if (Time.unscaledTime >= searchAt && (activeCamera == null || shovel == null))
            {
                searchAt = Time.unscaledTime + 0.75f;
                FindCameraAndShovel();
            }
            if (activeCamera == null || shovel == null || !shovel.gameObject.activeInHierarchy) return;
            if (Time.timeScale <= 0f || Cursor.lockState != CursorLockMode.Locked) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (Time.time - lastStrike < 0.55f) return;
            lastStrike = Time.time;
            StartCoroutine(ImpactAtShovelPeak());
        }

        private void FindCameraAndShovel()
        {
            // Prefer the active player camera, never a preview/menu camera.
            foreach (Camera camera in Camera.allCameras)
            {
                if (camera == null || !camera.enabled || camera.gameObject.scene != gameObject.scene) continue;
                if (!HasPlayerController(camera.transform)) continue;
                Transform held = camera.transform.Find("SDG_HandHeld_Shovel");
                if (held == null) continue;
                activeCamera = camera;
                shovel = held;
                return;
            }
            if (!missingToolLogged && Time.unscaledTime > 6f)
            {
                missingToolLogged = true;
                Debug.LogWarning("[SDG Experience V2] Level01 shovel effect requires the v1 handheld shovel on the player camera; no original gameplay has been changed.");
            }
        }

        private static bool HasPlayerController(Transform cam)
        {
            for (Transform parent = cam; parent != null; parent = parent.parent)
            {
                Component[] components = parent.GetComponents<Component>();
                foreach (Component comp in components)
                    if (comp != null && comp.GetType().Name == "SharedFirstPersonController") return true;
            }
            return false;
        }

        private IEnumerator ImpactAtShovelPeak()
        {
            // The v1 shovel swing is 0.32 seconds. Hit at approximately the middle of its arc.
            yield return new WaitForSeconds(0.15f);
            if (activeCamera == null || shovel == null || !shovel.gameObject.activeInHierarchy || Time.timeScale <= 0f) yield break;
            Vector3 impact = FindImpactPoint(activeCamera);
            DirtBurst(impact);
            ScatterRecycledFragments(impact);
        }

        private Vector3 FindImpactPoint(Camera camera)
        {
            Ray ray = new Ray(camera.transform.position, camera.transform.forward);
            // Ignore Raycast (layer 2) contains the visible handheld tool parts.
            if (Physics.Raycast(ray, out RaycastHit hit, 4.3f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                return hit.point + hit.normal * .04f;
            // If the shovel is aimed past the terrain, place the effect on nearby ground.
            Vector3 near = ray.origin + ray.direction * 1.85f;
            if (Physics.Raycast(near + Vector3.up * 1.3f, Vector3.down, out hit, 4.5f,
                                ~(1 << 2), QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * .04f;
            return near - Vector3.up * .55f;
        }

        private void DirtBurst(Vector3 origin)
        {
            GameObject obj = new GameObject("SDG_Shoveled_Dust_Transient");
            obj.layer = 2;
            obj.transform.position = origin;
            SceneManager.MoveGameObjectToScene(obj, gameObject.scene);
            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.32f, .75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 3.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(.055f, .16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.67f, .54f, .34f, .86f));
            main.gravityModifier = .95f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 34;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = .19f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    Material particleMaterial = new Material(shader);
                    particleMaterial.color = new Color(.7f, .59f, .43f, .8f);
                    renderer.material = particleMaterial;
                    // The material belongs only to this short-lived burst.
                    StartCoroutine(DestroyMaterialLater(particleMaterial, 1.7f));
                }
            }
            ps.Emit(24);
            Destroy(obj, 1.65f);
        }

        private IEnumerator DestroyMaterialLater(Material material, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (material != null) Destroy(material);
        }

        private void ScatterRecycledFragments(Vector3 origin)
        {
            // Six collectible-looking pieces fly apart as VFX only. Not physical inventory or reward objects.
            for (int i = 0; i < 7 && flying.Count < maxLivePieces; i++)
            {
                GameObject go = GameObject.CreatePrimitive(i % 3 == 1 ? PrimitiveType.Sphere : PrimitiveType.Cube);
                go.name = "SDG_Recycled_Fragment_VISUAL_ONLY";
                go.layer = 2;
                SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                var collider = go.GetComponent<Collider>();
                if (collider != null) { collider.enabled = false; Destroy(collider); }
                Renderer rend = go.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = i % 3 == 0 ? boardMat : i % 3 == 1 ? plasticMat : metalMat;
                float baseScale = Random.Range(.055f, .115f);
                go.transform.localScale = new Vector3(baseScale, baseScale * .25f, baseScale * 1.4f);
                Vector2 r = Random.insideUnitCircle.normalized;
                Vector3 velocity = new Vector3(r.x * Random.Range(1.3f, 3.5f), Random.Range(2.3f, 4.2f),
                                               r.y * Random.Range(1.3f, 3.5f));
                flying.Add(new FlyingPiece {
                    go = go, origin = origin, velocity = velocity,
                    spin = new Vector3(Random.Range(-500f, 500f), Random.Range(-500f, 500f), Random.Range(-500f, 500f)),
                    born = Time.time, lifetime = Random.Range(1f, 1.5f), originalScale = baseScale
                });
            }
        }

        private void AnimateFlyingPieces()
        {
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                FlyingPiece f = flying[i];
                if (f.go == null) { flying.RemoveAt(i); continue; }
                float age = Mathf.Max(0f, Time.time - f.born);
                if (age >= f.lifetime)
                {
                    Destroy(f.go);
                    flying.RemoveAt(i);
                    continue;
                }
                Vector3 p = f.origin + f.velocity * age + Vector3.down * (4.6f * age * age);
                // Visual bounce when a fragment reaches the impact ground plane.
                if (p.y < f.origin.y + .03f)
                {
                    p.y = f.origin.y + .03f;
                    p.x += .05f * age;
                }
                f.go.transform.position = p;
                f.go.transform.Rotate(f.spin * Time.deltaTime, Space.Self);
                float shrink = Mathf.Clamp01((f.lifetime - age) / .32f);
                f.go.transform.localScale = new Vector3(f.originalScale, f.originalScale * .25f, f.originalScale * 1.4f) * shrink;
            }
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return null;
            var m = new Material(shader) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return m;
        }

        private void OnDestroy()
        {
            foreach (var f in flying) if (f.go != null) Destroy(f.go);
            flying.Clear();
            if (dirtMat != null) Destroy(dirtMat);
            if (metalMat != null) Destroy(metalMat);
            if (plasticMat != null) Destroy(plasticMat);
            if (boardMat != null) Destroy(boardMat);
        }
    }
}
