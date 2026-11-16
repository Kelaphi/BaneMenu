using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace BaneMenu.modules.visuals
{
	/// <summary>
	/// SickoMenu Zoom (InnerNetClient.cpp camHeight block) - faithful port.
	/// State.CameraHeight → Scale; ortho = Scale * 3 when on, else 3.
	/// Smooth: step = abs(delta) * deltaTime * 12 toward target.
	/// </summary>
	internal class Zoom : Module
	{
		public Zoom() : base("Zoom") { }

		/// <summary>Sicko State.CameraHeight (default 3).</summary>
		public float Scale { get; set; } = 3f;
		public bool SmoothZoom { get; set; } = true;
		public bool ShowShadows { get; set; } = false;
		public bool ScrollZoom { get; set; } = true;

		static Zoom Instance => ModuleManager.zoom;
		const float BaseOrtho = 3f;

		static bool GameOrLobby()
		{
			try
			{
				if (AmongUsClient.Instance == null) return false;
				if (PlayerControl.LocalPlayer == null) return false;
				var st = AmongUsClient.Instance.GameState;
				return st == InnerNetClient.GameStates.Joined
					|| st == InnerNetClient.GameStates.Started;
			}
			catch { return false; }
		}

		static bool ShouldZoom(HudManager hud)
		{
			if (!Instance.Enabled) return false;
			if (!GameOrLobby()) return false;
			if (MeetingHud.Instance != null) return false;
			try { if (ExileController.Instance != null) return false; } catch { }
			if (hud == null) return true;
			try { if (hud.Chat != null && hud.Chat.IsOpenOrOpening) return false; } catch { }
			try { if (hud.KillOverlay != null && hud.KillOverlay.IsOpen) return false; } catch { }
			return true;
		}

		static Camera GetFollowerCam()
		{
			try
			{
				if (Camera.main != null && Camera.main.orthographic)
					return Camera.main;
			}
			catch { }
			return null;
		}

		[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Update))]
		static class ZoomTick
		{
			static void Postfix()
			{
				if (!GameOrLobby()) return;

				Camera cam = GetFollowerCam();
				if (cam == null) return;

				HudManager hud = null;
				try { hud = HudManager.Instance; } catch { }

				bool should = ShouldZoom(hud);

				if (should && Instance.ScrollZoom)
				{
					bool menuOpen = false;
					try { menuOpen = Bane.mainUI != null && Bane.mainUI.visible; } catch { }
					if (!menuOpen)
					{
						float w = Input.mouseScrollDelta.y;
						if (w < -0.01f) Instance.Scale += 0.5f;
						else if (w > 0.01f)
						{
							Instance.Scale -= 0.5f;
							if (Instance.Scale < 1f) Instance.Scale = 1f;
						}
						Instance.Scale = Mathf.Clamp(Instance.Scale, 0.5f, 10f);
					}
				}

				float camHeight = should ? (Instance.Scale * BaseOrtho) : BaseOrtho;
				float oldCamHeight = cam.orthographicSize;
				float del = camHeight - oldCamHeight;
				float step = Mathf.Abs(del) * Time.deltaTime * 12f;

				float newCamHeight;
				if (!Instance.SmoothZoom || !should)
					newCamHeight = camHeight;
				else if (del < 0f)
					newCamHeight = Mathf.Max(camHeight, oldCamHeight - step);
				else if (del > 0f)
					newCamHeight = Mathf.Min(camHeight, oldCamHeight + step);
				else
					newCamHeight = camHeight;

				const float precision = 1e-6f;
				if (Mathf.Abs(del) > precision)
				{
					cam.orthographicSize = newCamHeight;

					if (hud != null)
					{
						try
						{
							if (hud.UICamera != null && hud.UICamera.orthographic)
								hud.UICamera.orthographicSize = newCamHeight;
						}
						catch { }

						try
						{
							if (hud.ShadowQuad != null)
							{
								float aspect = cam.aspect > 0.01f ? cam.aspect : (16f / 9f);
								hud.ShadowQuad.transform.localScale =
									new Vector3(newCamHeight * aspect * 2f, newCamHeight * 2f, 0f);
							}
						}
						catch { }
					}
				}

				if (hud != null && hud.ShadowQuad != null)
				{
					try
					{
						bool hide = should && !Instance.ShowShadows;
						bool maxVision = ModuleManager.fullbright != null && ModuleManager.fullbright.Enabled;
						if (hide || maxVision)
							hud.ShadowQuad.gameObject.SetActive(false);
						else if (!should || Instance.ShowShadows)
						{
							if (!maxVision)
								hud.ShadowQuad.gameObject.SetActive(true);
						}
					}
					catch { }
				}
			}
		}

		[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
		static class MeetingReset
		{
			static void Postfix()
			{
				try
				{
					Camera cam = GetFollowerCam();
					if (cam != null) cam.orthographicSize = BaseOrtho;
					if (HudManager.Instance != null && HudManager.Instance.UICamera != null)
						HudManager.Instance.UICamera.orthographicSize = BaseOrtho;
				}
				catch { }
			}
		}

		protected override void OnDisable()
		{
			// Sicko: when zoom off, camHeight snaps to 3 and ShadowQuad scale follows
			const float BaseOrtho = 3f;
			try
			{
				Camera cam = null;
				try
				{
					if (Camera.main != null && Camera.main.orthographic)
						cam = Camera.main;
				}
				catch { }

				if (cam != null)
					cam.orthographicSize = BaseOrtho;

				if (HudManager.Instance != null)
				{
					if (HudManager.Instance.UICamera != null && HudManager.Instance.UICamera.orthographic)
						HudManager.Instance.UICamera.orthographicSize = BaseOrtho;

					if (HudManager.Instance.ShadowQuad != null)
					{
						float aspect = cam != null && cam.aspect > 0.01f ? cam.aspect : (16f / 9f);
						// Reset scale for ortho=3 (same formula as zoom path)
						HudManager.Instance.ShadowQuad.transform.localScale =
							new Vector3(BaseOrtho * aspect * 2f, BaseOrtho * 2f, 0f);

						bool maxVision = ModuleManager.fullbright != null && ModuleManager.fullbright.Enabled;
						if (!maxVision)
							HudManager.Instance.ShadowQuad.gameObject.SetActive(true);
					}
				}
			}
			catch { }
		}
	}
}
