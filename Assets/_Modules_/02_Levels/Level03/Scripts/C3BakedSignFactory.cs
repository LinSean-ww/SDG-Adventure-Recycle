using UnityEngine;

namespace C3TreasurePuzzle
{
    // Fixed signs use baked, Traditional-Chinese images rather than TextMesh glyph atlases.
    // Their independent world-space roots prevent negative parent scale from mirroring letters.
    // Sign geometry is static, not an interactable collider or a camera-facing billboard.
    public static class C3BakedSignFactory
    {
        private const string ResourcePrefix = "C3Signage/";

        public static GameObject AtWorld(string textureName, Vector3 position,
            Quaternion rotation, float width, float height, bool bothSides = true)
        {
            Texture2D texture = Resources.Load<Texture2D>(ResourcePrefix + textureName);
            if (texture == null)
            {
                Debug.LogError("Missing C3 baked sign texture: " + textureName);
                return null;
            }
            GameObject root = new GameObject("C3_IndustrialSign_" + textureName);
            root.transform.SetPositionAndRotation(position, rotation);
            Shader plateShader = Shader.Find("Standard");
            Shader imageShader = Resources.Load<Shader>(ResourcePrefix + "C3BakedSign");
            if (plateShader == null || imageShader == null)
            {
                Debug.LogError("Required built-in sign shaders could not be loaded.");
                Object.Destroy(root);
                return null;
            }
            Material backing = new Material(plateShader);
            backing.color = new Color(0.11f, 0.17f, 0.17f);
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "FixedMetalBacking";
            panel.transform.SetParent(root.transform, false);
            panel.transform.localScale = new Vector3(width, height, 0.065f);
            panel.GetComponent<Renderer>().sharedMaterial = backing;
            Collider panelCollider = panel.GetComponent<Collider>();
            if (panelCollider != null) panelCollider.enabled = false;

            Material ink = new Material(imageShader);
            ink.mainTexture = texture;
            MakeFace(root.transform, ink, width * 0.96f, height * 0.92f, 0.038f, false);
            if (bothSides)
                MakeFace(root.transform, ink, width * 0.96f, height * 0.92f, -0.038f, true);
            return root;
        }

        public static GameObject AtAnchor(Transform anchor, string textureName, Vector3 localPosition,
            Quaternion localRotation, float width, float height, bool bothSides = true)
        {
            // Deliberately DO NOT parent the sign under an artist model with negative scale.
            return AtWorld(textureName, anchor.TransformPoint(localPosition),
                anchor.rotation * localRotation, width, height, bothSides);
        }

        private static void MakeFace(Transform root, Material ink,
            float width, float height, float z, bool rear)
        {
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = rear ? "ReadableRear" : "ReadableFront";
            face.transform.SetParent(root, false);
            face.transform.localPosition = new Vector3(0f, 0f, z);
            face.transform.localRotation = rear ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            face.transform.localScale = new Vector3(width, height, 1f);
            face.GetComponent<Renderer>().sharedMaterial = ink;
            Collider faceCollider = face.GetComponent<Collider>();
            if (faceCollider != null) faceCollider.enabled = false;
        }

        // There are three pre-existing author-authored TextMesh signs in C_Main.unity.
        // Retain their positions, replace their legibility with physical baked nameplates.
        public static void ReplaceLegacySigns()
        {
            foreach (TextMesh text in Object.FindObjectsOfType<TextMesh>(true))
            {
                string glyphs = text.text;
                string sign = glyphs.Contains("高壓危險") ? "Warning" :
                    glyphs.Contains("03 號淨化設備") ? "Purifier" :
                    glyphs.Contains("維修紀錄") ? "Repair" : null;
                if (sign == null) continue;
                Renderer oldRenderer = text.GetComponent<Renderer>();
                if (oldRenderer != null) oldRenderer.enabled = false;
                if (GameObject.Find("C3_IndustrialSign_" + sign) != null) continue;
                Transform t = text.transform;
                float width = sign == "Repair" ? 2.45f : sign == "Warning" ? 2.20f : 2.55f;
                AtWorld(sign, t.position, Quaternion.Euler(0f, t.eulerAngles.y, 0f),
                    width, sign == "Repair" ? 0.90f : 0.76f);
            }
        }
    }
}
