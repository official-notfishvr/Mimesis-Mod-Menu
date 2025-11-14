using MelonLoader;
using Mimesis_Mod_Menu.Core;
using UnityEngine;

[assembly: MelonInfo(typeof(Loader), "ModMenu", "2.2.0", "notfishvr")]
[assembly: MelonGame("ReLUGames", "MIMESIS")]

namespace Mimesis_Mod_Menu.Core
{
    public class Loader : MelonMod
    {
        private GameObject gui;

        public override void OnInitializeMelon() { }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (gui == null)
            {
                gui = new GameObject(nameof(MainGUI));
                gui.AddComponent<MainGUI>();
                GameObject.DontDestroyOnLoad(gui);
            }
        }
    }
}
