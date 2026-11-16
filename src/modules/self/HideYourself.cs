using BaneMenu.network;
using HarmonyLib;
using UnityEngine;

namespace BaneMenu.modules.self
{
	/// <summary>
	/// Soft invis: network position/SnapTo always reported as (30000, 30000).
	/// Local movement stays real; local sprite alpha = 50% so you can tell it's on.
	/// </summary>
	internal class HideYourself : Module
	{
		public HideYourself() : base("Hide Yourself") { }

		public static readonly Vector2 FarPos = new Vector2(30000f, 30000f);

		float _nextNetSnap;
		Vector2 _returnPos;

		static HideYourself Instance => ModuleManager.hideYourself;

		public static bool IsActive =>
			Instance != null && Instance.Enabled &&
			PlayerControl.LocalPlayer != null;

		protected override void OnEnable()
		{
			if (PlayerControl.LocalPlayer == null)
			{
				Enabled = false;
				Bane.notifications.Send("Hide Yourself", "Not in a game.", 3);
				return;
			}

			_returnPos = PlayerControl.LocalPlayer.transform.position;
			_nextNetSnap = 0f;
			ApplyLocalOpacity(0.5f);
			PushFarNetwork();
			Bane.notifications.Send("Hide Yourself", "On - network pos 30000,30000, local 50% opacity.", 4);
		}

		protected override void OnDisable()
		{
			ApplyLocalOpacity(1f);

			if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.NetTransform != null)
			{
				try
				{
					Vector2 back = _returnPos;
					try { back = PlayerControl.LocalPlayer.transform.position; } catch { }
					PlayerControl.LocalPlayer.NetTransform.SnapTo(back);
					BatchedMessage batch = new BatchedMessage();
					batch.QueueSnapTo(PlayerControl.LocalPlayer, back);
					batch.FinishBatch();
				}
				catch { }
			}

			Bane.notifications.Send("Hide Yourself", "Off.", 3);
		}

		public void Tick()
		{
			if (!Enabled) return;
			if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return;

			// Keep local alpha correct (meetings / skin changes can reset it)
			ApplyLocalOpacity(0.5f);

			if (Time.unscaledTime < _nextNetSnap) return;
			_nextNetSnap = Time.unscaledTime + 0.2f;
			PushFarNetwork();
		}

		void PushFarNetwork()
		{
			try
			{
				if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.NetTransform == null) return;
				// Network only - do not SnapTo locally or you teleport yourself to 30k
				BatchedMessage batch = new BatchedMessage();
				batch.QueueSnapToNetworkOnly(PlayerControl.LocalPlayer, FarPos);
				batch.FinishBatch();
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning("HideYourself net snap: " + e.Message);
			}
		}

		static void ApplyLocalOpacity(float alpha)
		{
			try
			{
				PlayerControl me = PlayerControl.LocalPlayer;
				if (me == null) return;

				var renderers = me.GetComponentsInChildren<SpriteRenderer>(true);
				if (renderers != null)
				{
					for (int i = 0; i < renderers.Length; i++)
					{
						var r = renderers[i];
						if (r == null) continue;
						Color c = r.color;
						c.a = alpha;
						r.color = c;
					}
				}

			}
			catch { }
		}

		/// <summary>Rewrite outgoing RpcSnapTo so others always get far coords.</summary>
		[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.RpcSnapTo))]
		static class PatchRpcSnapTo
		{
			static void Prefix(CustomNetworkTransform __instance, ref Vector2 position)
			{
				if (!IsActive) return;
				if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.NetTransform != __instance) return;
				position = FarPos;
			}
		}

		/// <summary>If something calls SnapTo for network side-effects, still keep local real pos - 
		/// only rewrite when this is clearly an outgoing-style call is hard; net-only tick handles it.</summary>
		[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.SnapTo), typeof(Vector2))]
		static class PatchSnapToVec2
		{
			static void Prefix(CustomNetworkTransform __instance, ref Vector2 position)
			{
				// Do not rewrite local SnapTo - that would yank you to 30k.
				// Outgoing is handled by RpcSnapTo + network-only batch.
			}
		}
	}
}
