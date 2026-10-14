using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zakzoqa.EditorTools
{
    // PROTOTYPE (Design.md §14): "Zakzoqa controls the air in her body". A def with air control on,
    // and a dev scene (not in the build) that asks each question once, left to right:
    //
    //   1. A wall with a low gap at the bottom: let the air out (small) to get under it.
    //   2. A pit with wind blowing up: full of air (big), the wind carries her to the high ledge.
    //   3. A weak floor: small and heavy, land on it hard and it breaks into a secret room.
    //   4. Pins on the ground: every hit, she shrinks — her health, seen.
    //   5. A step too high to jump: jump, then jet upward.
    //
    // Created only if missing; deleted with the rest of the prototype if it is not kept.
    public static class AirProtoSetup
    {
        public const string DefPath = "Assets/Data/Transformations/Balloon_AirProto.asset";
        public const string ScenePath = "Assets/Scenes/Dev/AirProto.unity";

        [MenuItem("Zakzoqa/Set Up Air Control Prototype")]
        public static void CreateIfMissing()
        {
            PlaygroundSetup.Png("wind", 200, 200, (x, y, w, h) =>
                (y / 25 + x / 50) % 2 == 0 ? new Color32(255, 255, 255, 70) : new Color32(255, 255, 255, 25));
            PlaygroundSetup.Png("weak_floor", 200, 200, (x, y, w, h) =>
                (x < 6 || y < 6 || x >= w - 6 || y >= h - 6 || Mathf.Abs(x - y) < 4 || Mathf.Abs(x + y - w) < 4)
                    ? new Color32(140, 100, 60, 255) : new Color32(205, 160, 105, 255));
            AssetDatabase.Refresh();

            TransformationDef def = AssetDatabase.LoadAssetAtPath<TransformationDef>(DefPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<TransformationDef>();
                def.kind = FormKind.Balloon;
                def.displayNameKey = "form.balloon";
                def.airControl = true;
                AssetDatabase.CreateAsset(def, DefPath);
                AssetDatabase.SaveAssets();
            }

            if (!File.Exists(ScenePath)) CreateScene();
            AssetDatabase.SaveAssets();
        }

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Loaded AFTER the new scene: opening a scene unloads every asset nothing uses, and a def
            // loaded before it was a dead object by the time the scene pointed at it, saved as empty.
            TransformationDef def = AssetDatabase.LoadAssetAtPath<TransformationDef>(DefPath);
            if (def == null) throw new System.InvalidOperationException("AirProto: " + DefPath + " is missing.");

            GameObject camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = WorldScale.OrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(186, 228, 255, 255);
            camGo.transform.position = new Vector3(0, 0, -10);
            CameraFollow follow = camGo.AddComponent<CameraFollow>();
            follow.min = new Vector2(-4f, -6f);
            follow.max = new Vector2(46f, 9f);

            GameObject lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;

            GameObject z = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlaygroundSetup.PrefabPath));
            z.transform.position = new Vector3(-6f, -3.8f, 0);
            ZakzoqaBody body = z.GetComponent<ZakzoqaBody>();
            SerializedObject so = new SerializedObject(body);
            so.FindProperty("startingForm").objectReferenceValue = def;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            // Compared by asset path: a dead object compares equal to null, and fooled this check once.
            if (AssetDatabase.GetAssetPath(new SerializedObject(body).FindProperty("startingForm").objectReferenceValue) != DefPath)
                throw new System.InvalidOperationException("AirProto: Zakzoqa's starting form did not stick.");
            follow.target = body;

            // Ground: top at y = -4.5 from x -10 to 16.
            PlaygroundSetup.Block("Floor", new Vector2(3f, -5f), new Vector2(26f, 1f));
            PlaygroundSetup.Block("Wall L", new Vector2(-10.5f, 3f), new Vector2(1f, 16f));

            // 1. A wall with a gap at the bottom: 0.85 high, room for her small (0.6) and not big (1.2).
            const float gap = 0.85f;
            float wallBottom = -4.5f + gap, wallTop = 8f;
            PlaygroundSetup.Block("Low wall", new Vector2(8f, (wallBottom + wallTop) / 2f), new Vector2(2f, wallTop - wallBottom));

            // 2. A pit (x 16..20, floor at -9) with wind up it, to the high ledge (top y = 0, x 20..26).
            PlaygroundSetup.Block("Pit floor", new Vector2(18f, -9.5f), new Vector2(4f, 1f));
            PlaygroundSetup.Block("Pit wall", new Vector2(15.5f, -7.5f), new Vector2(1f, 4f));
            Wind("Wind", new Vector2(18f, -3.5f), new Vector2(4f, 11f));
            PlaygroundSetup.Block("Ledge 1", new Vector2(23f, -4.5f), new Vector2(6f, 9f));
            PlaygroundSetup.HeliumAt(new Vector2(14f, -3.5f));

            // 3. A weak floor (x 26..28) over a secret room; wind inside to get back out.
            GameObject weak = PlaygroundSetup.Block("Weak floor", new Vector2(27f, -0.25f), new Vector2(2f, 0.5f));
            weak.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaygroundSetup.PlaceholderDir}/weak_floor.png");
            weak.AddComponent<WeakFloor>();
            PlaygroundSetup.Block("Secret floor", new Vector2(27f, -9.5f), new Vector2(2f, 1f));
            Wind("Secret wind", new Vector2(27f, -4.5f), new Vector2(2f, 9f));
            PlaygroundSetup.TapeAt(new Vector2(27f, -8.3f));
            PlaygroundSetup.HeliumAt(new Vector2(27f, -7.2f));

            // 4. Pins on the ground (top y = 0, x 28..44): every hit, she shrinks.
            PlaygroundSetup.Block("Ledge 2", new Vector2(36f, -4.5f), new Vector2(16f, 9f));
            float onGround = WorldScale.PinSoldierHeight * 0.5f;
            foreach (float x in new[] { 33f, 33.5f }) PlaygroundSetup.PinAt(new Vector2(x, onGround), 0f);
            PlaygroundSetup.TapeAt(new Vector2(38f, 0.8f));

            // 5. A step 4.5 high (x 44..50): higher than a jump, reachable with a jet upward.
            PlaygroundSetup.Block("High step", new Vector2(47f, -2.25f), new Vector2(6f, 13.5f));
            PlaygroundSetup.HeliumAt(new Vector2(41f, 0.8f));
            PlaygroundSetup.Block("Wall R", new Vector2(50.5f, 6f), new Vector2(1f, 12f));

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void Wind(string name, Vector2 at, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.position = at;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaygroundSetup.PlaceholderDir}/wind.png");
            r.sortingOrder = -1;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            go.AddComponent<WindColumn>();
        }
    }
}
