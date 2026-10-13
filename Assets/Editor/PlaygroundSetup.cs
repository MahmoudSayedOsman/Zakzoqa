using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zakzoqa.EditorTools
{
    // The pieces to play the balloon with before any real art exists (Decisions §2, rule 5):
    // placeholder shapes AT THE FINAL SIZES, so when the drawn art arrives it replaces a file and
    // nothing else moves. Plus the balloon's TransformationDef, the Zakzoqa prefab, and a dev scene
    // (not in the build) to try them in.
    //
    // Each piece is created only if missing. The art is drawn by code here only because there is no
    // art yet; the drawn files replace these PNGs under the same names.
    public static class PlaygroundSetup
    {
        public const string PlaceholderDir = "Assets/Art/Placeholder";
        public const string BalloonDefPath = "Assets/Data/Transformations/Balloon.asset";
        public const string PrefabPath = "Assets/Prefabs/Zakzoqa.prefab";
        public const string PlaygroundPath = "Assets/Scenes/Dev/Playground.unity";

        const int Ppu = WorldScale.PixelsPerUnit;

        [MenuItem("Zakzoqa/Set Up Playground")]
        public static void CreateIfMissing()
        {
            Directory.CreateDirectory(PlaceholderDir);
            Directory.CreateDirectory(Path.GetDirectoryName(BalloonDefPath));
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            Directory.CreateDirectory(Path.GetDirectoryName(PlaygroundPath));

            int body = Mathf.RoundToInt(WorldScale.ZakzoqaHeight * Ppu);      // 240
            int pinH = Mathf.RoundToInt(WorldScale.PinSoldierHeight * Ppu);   // 180
            Png("zakzoqa_balloon", body, body, Balloon);
            // The face is its own layer, shared by every transformation (Decisions §2, rule 1).
            Png("zakzoqa_face", body, body, Face);
            Png("pin", pinH / 2, pinH, Pin);
            Png("tape", 120, 60, (x, y, w, h) => new Color32(240, 205, 120, 255));
            Png("block", Ppu, Ppu, (x, y, w, h) => (x < 6 || y < 6 || x >= w - 6 || y >= h - 6) ? new Color32(120, 150, 200, 255) : new Color32(170, 200, 240, 255));
            AssetDatabase.Refresh();

            TransformationDef balloon = AssetDatabase.LoadAssetAtPath<TransformationDef>(BalloonDefPath);
            if (balloon == null)
            {
                balloon = ScriptableObject.CreateInstance<TransformationDef>();
                balloon.kind = FormKind.Balloon;
                balloon.displayNameKey = "form.balloon";
                AssetDatabase.CreateAsset(balloon, BalloonDefPath);
            }

            if (!File.Exists(PrefabPath)) CreatePrefab(balloon);
            if (!File.Exists(PlaygroundPath)) CreatePlayground();
            AssetDatabase.SaveAssets();
        }

        static void CreatePrefab(TransformationDef balloon)
        {
            GameObject root = new GameObject("Zakzoqa");
            root.AddComponent<Rigidbody2D>();
            root.AddComponent<CircleCollider2D>();
            ZakzoqaBody bodyComp = root.AddComponent<ZakzoqaBody>();
            SerializedObject so = new SerializedObject(bodyComp);
            so.FindProperty("startingForm").objectReferenceValue = balloon;
            so.ApplyModifiedPropertiesWithoutUndo();

            Layer(root, "Body", "zakzoqa_balloon", 10);
            Layer(root, "Face", "zakzoqa_face", 11);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        static void Layer(GameObject parent, string name, string sprite, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaceholderDir}/{sprite}.png");
            r.sortingOrder = order;
        }

        // A shaft to climb: walls both sides, pins on them and in the middle, tape on the way up.
        static void CreatePlayground()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = WorldScale.OrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(186, 228, 255, 255);
            camGo.transform.position = new Vector3(0, 0, -10);
            CameraFollow follow = camGo.AddComponent<CameraFollow>();
            follow.minY = 0f;

            // The 2D renderer lights every sprite; with no light at all they come out as dim
            // silhouettes. The template's scenes carry this light, an empty scene does not.
            GameObject lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            light.intensity = 1f;

            GameObject z = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            z.transform.position = new Vector3(0, -3.5f, 0);
            follow.target = z.transform;

            const float halfWidth = 4.5f, height = 60f;
            Block("Floor", new Vector2(0, -5f), new Vector2(halfWidth * 2 + 2, 1));
            Block("Wall L", new Vector2(-halfWidth - 0.5f, height / 2 - 5), new Vector2(1, height));
            Block("Wall R", new Vector2(halfWidth + 0.5f, height / 2 - 5), new Vector2(1, height));

            for (int i = 0; i < 8; i++)
            {
                float y = 2f + i * 6f;
                bool left = i % 2 == 0;
                PinAt(new Vector2(left ? -halfWidth + 0.25f : halfWidth - 0.25f, y), left ? -90f : 90f);
                if (i % 3 == 1) PinAt(new Vector2(0, y + 3f), 180f);
                if (i % 2 == 1) TapeAt(new Vector2(left ? 2f : -2f, y + 1.5f));
            }

            EditorSceneManager.SaveScene(scene, PlaygroundPath);
        }

        static void Block(string name, Vector2 at, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.position = at;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            go.AddComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaceholderDir}/block.png");
            go.AddComponent<BoxCollider2D>();
        }

        static void PinAt(Vector2 at, float angle)
        {
            GameObject go = new GameObject("Pin");
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, 0, angle));
            go.AddComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaceholderDir}/pin.png");
            PolygonCollider2D col = go.AddComponent<PolygonCollider2D>();
            col.isTrigger = true;
            go.AddComponent<Pin>();
        }

        static void TapeAt(Vector2 at)
        {
            GameObject go = new GameObject("Tape");
            go.transform.position = at;
            go.AddComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaceholderDir}/tape.png");
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            go.AddComponent<TapePickup>();
        }

        // --- placeholder drawing -------------------------------------------------------------------

        delegate Color32 Paint(int x, int y, int w, int h);

        static void Png(string name, int w, int h, Paint paint)
        {
            string path = $"{PlaceholderDir}/{name}.png";
            if (File.Exists(path)) return;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = paint(x, y, w, h);
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        static bool InCircle(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

        static Color32 Balloon(int x, int y, int w, int h)
        {
            float r = w / 2f - 2;
            if (!InCircle(x + .5f, y + .5f, w / 2f, h / 2f, r)) return Clear;
            if (!InCircle(x + .5f, y + .5f, w / 2f, h / 2f, r - 8)) return new Color32(170, 30, 50, 255);
            // A highlight, so it reads as round.
            if (InCircle(x, y, w * 0.32f, h * 0.70f, w * 0.10f)) return new Color32(255, 170, 180, 255);
            return new Color32(230, 60, 80, 255);
        }

        static Color32 Face(int x, int y, int w, int h)
        {
            Color32 white = new Color32(255, 255, 255, 255), ink = new Color32(40, 30, 50, 255);
            foreach (float ex in new[] { w * 0.38f, w * 0.62f })
            {
                if (InCircle(x, y, ex + 4, h * 0.56f, w * 0.045f)) return ink;
                if (InCircle(x, y, ex, h * 0.56f, w * 0.085f)) return white;
            }
            // A smile: a ring segment under the eyes.
            float dx = x - w * 0.5f, dy = y - h * 0.50f, d = Mathf.Sqrt(dx * dx + dy * dy);
            if (dy < -w * 0.06f && d > w * 0.15f && d < w * 0.19f) return ink;
            return Clear;
        }

        static Color32 Pin(int x, int y, int w, int h)
        {
            // Point at the top (local up), head at the bottom.
            float head = h * 0.28f;
            if (y < head) return InCircle(x, y, w / 2f, head / 2f, w / 2f - 1) ? new Color32(220, 60, 60, 255) : Clear;
            float half = (w / 2f) * 0.35f * (1f - (y - head) / (h - head));
            return Mathf.Abs(x + .5f - w / 2f) <= half + 0.5f ? new Color32(150, 155, 170, 255) : Clear;
        }
    }
}
