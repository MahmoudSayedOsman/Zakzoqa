using UnityEngine;
using UnityEngine.UIElements;

namespace Zakzoqa
{
    // The main menu. For now it shows the menu and switches language; Play, Store and Settings are
    // wired as their screens arrive.
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuScreen : MonoBehaviour
    {
        void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            Language.ApplyTo(root);

            Button language = root.Q<Button>("language");
            if (language != null) language.clicked += SwitchLanguage;
        }

        static void SwitchLanguage() =>
            Language.Set(Language.IsRightToLeft ? Language.English : Language.Arabic);
    }
}
