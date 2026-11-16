using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace BaneMenu.modules.self
{
	/// <summary>
	/// SickoMenu Shift/Ctrl + Right-click teleport (InnerNetClient.cpp).
	/// Shift+RMB: single RpcSnapTo to cursor world position.
	/// Ctrl+RMB held: repeated SnapTo with short delay.
	/// </summary>
	internal class ClickTeleport : Module
	{
		public ClickTeleport() : base("Click Teleport")
		{
			// Always available; toggle in Self tab
			/* default off until config */ base.Enabled = false;
		}

		static ClickTeleport Instance => ModuleManager.clickTeleport;

		static float _ctrlDelay;

		static bool InGameOrLobby()
		{
			try
			{
				if (AmongUsClient.Instance == null || PlayerControl.LocalPlayer == null) return false;
				if (MeetingHud.Instance != null) return false;
				var st = AmongUsClient.Instance.GameState;
				return st == InnerNetClient.GameStates.Joined
					|| st == InnerNetClient.GameStates.Started;
			}
			catch { return false; }
		}

		static Vector2 ScreenToWorld(Vector3 screen)
		{
			Camera cam = Camera.main;
			if (cam == null) return Vector2.zero;
			// Sicko ScreenToWorld uses game camera
			Vector3 w = cam.ScreenToWorldPoint(screen);
			return new Vector2(w.x, w.y);
		}

		static void SnapTo(Vector2 pos)
		{
			if (PlayerControl.LocalPlayer == null) return;
			try
			{
				if (Teleporter.UseSnapToRPC)
					PlayerControl.LocalPlayer.NetTransform.RpcSnapTo(pos);
				else
					PlayerControl.LocalPlayer.NetTransform.SnapTo(pos);
			}
			catch
			{
				try { PlayerControl.LocalPlayer.transform.position = pos; } catch { }
			}
		}

		[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Update))]
		static class Tick
		{
			static void Postfix()
			{
				if (Instance == null || !Instance.Enabled) return;
				if (!InGameOrLobby()) return;

				// Don't teleport through the menu
				try { if (Bane.mainUI != null && Bane.mainUI.visible) return; } catch { }

				bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
				bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

				Vector3 mouse = Input.mousePosition;
				Vector2 target = ScreenToWorld(mouse);
				// Sicko rejects 0,0
				if (target.x == 0f && target.y == 0f) return;

				// Shift + right click (single)
				if (shift && Input.GetMouseButtonDown(1))
				{
					SnapTo(target);
					return;
				}

				// Ctrl + right held (repeat)
				if (ctrl && Input.GetMouseButton(1))
				{
					if (_ctrlDelay <= 0f)
					{
						SnapTo(target);
						_ctrlDelay = 0.05f; // ~Sicko fps-scaled delay
					}
					else
					{
						_ctrlDelay -= Time.deltaTime;
					}
				}
				else
				{
					_ctrlDelay = 0f;
				}
			}
		}
	}
}
