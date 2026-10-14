using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Zakzoqa.Tests
{
    // THE WHOLE PATH, AS THE PLAYER HAS IT: a real scene, a keyboard, Update and FixedUpdate. Every
    // other movement test feeds frames straight into ZakzoqaBody, so none of them would notice the
    // keyboard never reaching her — which is what the owner reported the first time they pressed Play.
    //
    // A virtual keyboard is added and its keys pressed through the Input System, exactly as a real one
    // reports them.
    public class RealSceneControlsTests
    {
        Keyboard keyboard;
        InputSettings.BackgroundBehavior savedBackground;
        InputSettings.EditorInputBehaviorInPlayMode savedEditorBehaviour;

        [SetUp]
        public void SetUp()
        {
            // A test run has no focused game window, and the Input System drops keyboard input to an
            // unfocused one (the same rule that needs a click into the Game view in the editor).
            savedBackground = InputSystem.settings.backgroundBehavior;
            savedEditorBehaviour = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public void TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = savedBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehaviour;
        }

        static IEnumerator Load(string path)
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        void Hold(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        static IEnumerator Seconds(float s)
        {
            for (float t = 0; t < s; t += Time.deltaTime) yield return null;
        }

        static ZakzoqaBody Her()
        {
            ZakzoqaBody z = Object.FindAnyObjectByType<ZakzoqaBody>();
            Assert.IsNotNull(z, "no Zakzoqa in the scene.");
            Assert.IsNotNull(z.Def, "Zakzoqa has no shape: her starting form is not set.");
            return z;
        }

        [UnityTest]
        [TestCase("Assets/Scenes/Dev/Playground.unity", ExpectedResult = null)]
        [TestCase("Assets/Scenes/Dev/AirProto.unity", ExpectedResult = null)]
        public IEnumerator TheKeyboard_RunsAndJumpsHer(string scene)
        {
            yield return Load(scene);
            ZakzoqaBody z = Her();
            yield return Seconds(0.5f);                    // land
            Assert.IsTrue(z.IsGrounded, "she never landed on the floor.");

            float x = z.Position.x;
            Hold(Key.D);
            yield return Seconds(0.5f);
            string why = $"current keyboard is ours: {Keyboard.current == keyboard}, D pressed: {keyboard.dKey.isPressed}, "
                       + $"velocity {z.Velocity}, run speed {z.RunSpeed}, grounded {z.IsGrounded}, stunned {z.IsStunned}, "
                       + $"timeScale {Time.timeScale}, keyboards: {InputSystem.devices.Count}";
            Hold();
            Assert.Greater(z.Position.x, x + 1f, "holding D did not run her right. " + why);

            yield return Seconds(0.3f);
            float y = z.Position.y;
            float top = y;
            Hold(Key.Space);
            for (float t = 0; t < 0.4f; t += Time.deltaTime) { yield return null; top = Mathf.Max(top, z.Position.y); }
            Hold();
            Assert.Greater(top, y + 1f, "pressing Space did not jump her.");
        }
    }
}
