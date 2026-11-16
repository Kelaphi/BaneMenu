using BaneMenu.modules;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	internal class GeneralSection : Section
	{
		public GeneralSection() : base("About") { }

		public override void Render()
		{
			GUILayout.Label("Welcome to BaneMenu!");
			GUILayout.Label("The most powerful mod menu for Among Us ever, made with love <3 from the Bane team.");
			GUILayout.Space(8);
			GUILayout.Label($"Version: {MyPluginInfo.PLUGIN_VERSION}");
			GUILayout.Space(10);
			GUILayout.Label("Credits");
			GUILayout.Label("Jelly");
			GUILayout.Label("Kelaphi");
			GUILayout.Label("hpwd");
			GUILayout.Label("g0aty");
			GUILayout.Label("TyGamer4");
			GUILayout.Label("alexor_27");
			GUILayout.Space(8);
			GUILayout.Label("GitHub: https://github.com/Kelaphi/BaneMenu");
		}
	}
}
