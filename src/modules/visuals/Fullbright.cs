using HarmonyLib;
using UnityEngine;

namespace BaneMenu.modules.visuals
{
	/// <summary>
	/// One-to-one port of SickoMenu MaxVision:
	/// - ShipStatus.CalculateLightRadius → 420f
	/// - AirshipStatus.CalculateLightRadius → 420f (if present)
	/// - PlayerControl.IsFlashlightEnabled → false
	/// - HudManager ShadowQuad hidden while enabled (lobby + game)
	/// </summary>
	internal class Fullbright : Module
	{
		public Fullbright() : base("Fullbright") { }

		static Fullbright Instance => ModuleManager.fullbright;

		// Sicko: dShipStatus_CalculateLightRadius
		[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
		static class ShipLightRadius
		{
			static bool Prefix(ref float __result)
			{
				if (!Instance.Enabled) return true;
				__result = 420f;
				return false;
			}
		}

		// Sicko: dPlayerControl_IsFlashlightEnabled
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.IsFlashlightEnabled))]
		static class NoFlashlight
		{
			static bool Prefix(ref bool __result)
			{
				if (!Instance.Enabled) return true;
				__result = false;
				return false;
			}
		}

		// Keep ShadowQuad off each Hud update while MaxVision is on
		[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
		static class HideShadowQuad
		{
			static void Postfix(HudManager __instance)
			{
				if (!Instance.Enabled) return;
				if (__instance == null || __instance.ShadowQuad == null) return;
				try
				{
					// Sicko shouldShowShadowQuad is false when MaxVision (esp. in lobby)
					__instance.ShadowQuad.gameObject.SetActive(false);
				}
				catch { }
			}
		}

		private void OnGameLoad()
		{
			try
			{
				if (HudManager.Instance != null && HudManager.Instance.ShadowQuad != null)
					HudManager.Instance.ShadowQuad.gameObject.SetActive(false);
			}
			catch { }
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			try
			{
				if (HudManager.Instance != null && HudManager.Instance.ShadowQuad != null)
					HudManager.Instance.ShadowQuad.gameObject.SetActive(false);
			}
			catch { }
			EventCoordinator.OnGameLoad += OnGameLoad;
		}

		protected override void OnDisable()
		{
			try
			{
				if (HudManager.Instance != null && HudManager.Instance.ShadowQuad != null)
				{
					bool shouldBeEnabled = true;
					try
					{
						if (ModuleManager.spectatePlayer != null && ModuleManager.spectatePlayer.Enabled)
							shouldBeEnabled = false;
						if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.Data != null
							&& RoleManager.IsGhostRole(PlayerControl.LocalPlayer.Data.RoleType))
							shouldBeEnabled = false;
						// Zoom may be hiding shadows intentionally
						if (ModuleManager.zoom != null && ModuleManager.zoom.Enabled && !ModuleManager.zoom.ShowShadows)
							shouldBeEnabled = false;
					}
					catch { }
					HudManager.Instance.ShadowQuad.gameObject.SetActive(shouldBeEnabled);
				}
			}
			catch { }

			EventCoordinator.OnGameLoad -= OnGameLoad;
		}
	}
}
