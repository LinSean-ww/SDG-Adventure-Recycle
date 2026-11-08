using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SDGAdventure.CompletePlayer;
using SDGAdventure.SharedPlayer;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace SDGAdventure.PlayerControlTests
{
    public sealed class CompletePlayerWorkerParityTests
    {
        [UnityTest]
        public IEnumerator WorkerMatchesC3GeometryMaterialsAndStancePoses()
        {
            const string viewKey = PlayerViewPreference.Key;
            bool hadView = PlayerPrefs.HasKey(viewKey);
            int oldView = PlayerPrefs.GetInt(viewKey, 0);
            GameObject c3 = null, shared = null, floor = null;
            try
            {
                PlayerViewPreference.Mode = PlayerViewMode.FirstPerson;
                var c3Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Modules_/02_Levels/Level03/Prefabs/M2_Player.prefab");
                var sharedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Modules_/01_Shared/CompletePlayer/Prefabs/CompletePlayer.prefab");
                Assert.NotNull(c3Prefab); Assert.NotNull(sharedPrefab);
                c3 = UnityEngine.Object.Instantiate(c3Prefab, new Vector3(120, 30, 120), Quaternion.identity);
                shared = UnityEngine.Object.Instantiate(sharedPrefab, new Vector3(130, 30, 120), Quaternion.identity);
                var c3Controller = c3.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "C3FirstPersonController");
                c3Controller.enabled = false;
                shared.GetComponent<SharedFirstPersonController>().enabled = false;
                yield return null;
                var first = c3.transform.Find("V11_RecyclingEngineer_Visual");
                var second = shared.transform.Find("V11_RecyclingEngineer_Visual");
                Assert.NotNull(first); Assert.NotNull(second);
                Assert.NotNull(shared.GetComponent<CompletePlayerWorkerAppearance>());
                Assert.IsNull(shared.GetComponentInChildren<CompletePlayerAnimation>(true));
                var a = first.GetComponentsInChildren<Transform>(true);
                var b = second.GetComponentsInChildren<Transform>(true);
                Assert.AreEqual(a.Length, b.Length);
                for (int i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i].name, b[i].name, "hierarchy " + i);
                    Assert.That(Vector3.Distance(a[i].localScale, b[i].localScale), Is.LessThan(.0001f), a[i].name + " scale");
                    var ar = a[i].GetComponent<Renderer>(); var br = b[i].GetComponent<Renderer>();
                    Assert.AreEqual(ar != null, br != null, a[i].name + " renderer");
                    if (ar && br)
                    {
                        Assert.AreEqual(ar.sharedMaterial.color, br.sharedMaterial.color, a[i].name + " material");
                        Assert.AreEqual(ar.sharedMaterial.mainTexture != null, br.sharedMaterial.mainTexture != null, a[i].name + " texture");
                    }
                }
                var c3Appearance = c3.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "C3WorkerAppearance");
                var sharedAppearance = shared.GetComponent<CompletePlayerWorkerAppearance>();
                var c3Late = c3Appearance.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                var sharedLate = typeof(CompletePlayerWorkerAppearance).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (float height in new[] { 1.8f, 1.1f, .75f })
                {
                    c3.GetComponent<CharacterController>().height = height;
                    shared.GetComponent<CharacterController>().height = height;
                    c3Late.Invoke(c3Appearance, null); sharedLate.Invoke(sharedAppearance, null);
                    for (int i = 0; i < a.Length; i++)
                    {
                        Assert.That(Vector3.Distance(a[i].localPosition, b[i].localPosition), Is.LessThan(.0001f), a[i].name + " position at " + height);
                        Assert.That(Quaternion.Angle(a[i].localRotation, b[i].localRotation), Is.LessThan(.01f), a[i].name + " pose at " + height);
                    }
                }
                // Compare the same C3 formulas while grounded at walking/running speeds,
                // then while airborne. Both motors receive identical collision moves.
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = new Vector3(125, 29, 120);
                floor.transform.localScale = new Vector3(30, 2, 30);
                c3.transform.position = new Vector3(120, 30.02f, 120);
                shared.transform.position = new Vector3(130, 30.02f, 120);
                var motorA = c3.GetComponent<CharacterController>();
                var motorB = shared.GetComponent<CharacterController>();
                motorA.height = motorB.height = 1.8f;
                Physics.SyncTransforms();
                motorA.Move(Vector3.down * .1f); motorB.Move(Vector3.down * .1f);
                Assert.IsTrue(motorA.isGrounded, "C3 must be grounded before walking comparisons");
                Assert.IsTrue(motorB.isGrounded, "Shared worker must be grounded before walking comparisons");
                yield return null;
                foreach (float speed in new[] { 2f, 6f, -1f })
                {
                    Vector3 movement = speed > 0f ? Vector3.forward * speed * Time.deltaTime + Vector3.down * .02f : Vector3.up * .15f;
                    motorA.Move(movement); motorB.Move(movement);
                    Assert.AreEqual(motorA.isGrounded, motorB.isGrounded);
                    Assert.AreEqual(speed > 0f, motorA.isGrounded, "walking/running grounded; jump airborne");
                    c3Late.Invoke(c3Appearance, null); sharedLate.Invoke(sharedAppearance, null);
                    for (int i = 0; i < a.Length; i++)
                    {
                        Assert.That(Vector3.Distance(a[i].localPosition, b[i].localPosition), Is.LessThan(.001f), a[i].name + " movement pose " + speed);
                        Assert.That(Quaternion.Angle(a[i].localRotation, b[i].localRotation), Is.LessThan(.05f), a[i].name + " movement pose " + speed);
                    }
                    yield return null;
                }
                var camera = shared.GetComponentInChildren<Camera>();
                var rig = shared.GetComponent<PlayerPerspectiveRig>();
                foreach (var renderer in second.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.IsTrue(renderer.enabled);
                    Assert.AreEqual(ShadowCastingMode.ShadowsOnly, renderer.shadowCastingMode);
                }
                PlayerViewPreference.Mode = PlayerViewMode.ThirdPerson;
                rig.ApplyMode(); yield return null;
                foreach (var renderer in second.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.IsTrue(renderer.enabled);
                    Assert.AreNotEqual(ShadowCastingMode.ShadowsOnly, renderer.shadowCastingMode);
                    Assert.AreNotEqual(0, camera.cullingMask & (1 << renderer.gameObject.layer));
                }
            }
            finally
            {
                if (floor) UnityEngine.Object.DestroyImmediate(floor);
                if (c3) UnityEngine.Object.DestroyImmediate(c3);
                if (shared) UnityEngine.Object.DestroyImmediate(shared);
                if (hadView) PlayerPrefs.SetInt(viewKey, oldView); else PlayerPrefs.DeleteKey(viewKey);
                PlayerPrefs.Save();
            }
        }
    }
}
