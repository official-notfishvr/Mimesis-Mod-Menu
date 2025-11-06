using MIMESIS_Mod_Menu;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(MIMESIS_Mod_Menu.Loader), "ModMenu", "1.0.0", "notfishvr")]
[assembly: MelonGame("ReLUGames", "MIMESIS")]

namespace MIMESIS_Mod_Menu
{
    public class Loader : MelonMod
    {
        private GameObject gui;

        public override void OnInitializeMelon()
        {
        }

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
