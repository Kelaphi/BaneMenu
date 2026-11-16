using BaneMenu.ui;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BaneMenu.modules.visuals
{
	/// <summary>
	/// SickoMenu VersionShower_Start watermark:
	/// appends " • BaneMenu vX" to the title-screen version text.
	/// </summary>
	internal static class VersionWatermark
	{
		[HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
		static class VersionShowerStart
		{
			static void Postfix(VersionShower __instance)
			{
				try
				{
					if (__instance == null || __instance.text == null) return;

					string original = __instance.text.text ?? "";
					// Avoid double-append if Start runs twice
					if (original.Contains("BaneMenu")) return;

					// Sicko: <size=N%>color + defaultText + " • SickoMenu version by author"
					string menuName = MainUI.DisplayMenuName; // BaneMenu / BonerMenu on April 1
					string ver = MyPluginInfo.PLUGIN_VERSION ?? "1.0";
					// Cardinal-ish accent for menu name
					string watermark =
						$" • <color=#c41e3a>{menuName}</color> <color=#ffbb00>v{ver}</color>";

					int size = 100;
					try
					{
						string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
						if (scene == "FindAGame") size = 60;
						else if (scene == "MainMenu") size = 75;
					}
					catch { }

					__instance.text.richText = true;
					__instance.text.text = $"<size={size}%>{original}{watermark}</size>";
				}
				catch { }
			}
		}
	}
}
