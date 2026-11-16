using BaneMenu.network;
using UnityEngine;

namespace BaneMenu.modules.self
{
	internal class NanPosition : Module
	{
		public NanPosition() : base("NanPosition") { }

		private Vector3 lockedCameraPosition;
		private Vector2 returnPosition;
		private bool hasLockedCamera = false;
		private FollowerCamera followerCamera;
		private float nextNetSnap;

		private static NanPosition Instance => ModuleManager.nanPosition;

		public void Tick()
		{
			if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return;
			if (Camera.main == null) return;

			Vector2 nan = new Vector2(float.NaN, float.NaN);

			// Local keep-alive
			PlayerControl.LocalPlayer.NetTransform.SnapTo(nan);

			if (PlayerControl.LocalPlayer.NetTransform.body != null)
				PlayerControl.LocalPlayer.NetTransform.body.velocity = Vector2.zero;

			// Network SnapTo (throttled - every-frame RPC is free ban)
			if (Time.unscaledTime >= nextNetSnap)
			{
				nextNetSnap = Time.unscaledTime + 0.25f;
				try
				{
					BatchedMessage batch = new BatchedMessage();
					batch.QueueSnapTo(PlayerControl.LocalPlayer, nan);
					batch.FinishBatch();
				}
				catch { }
			}

			if (hasLockedCamera && followerCamera != null)
			{
				Camera.main.transform.position = lockedCameraPosition;
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			if (PlayerControl.LocalPlayer == null || Camera.main == null)
			{
				Enabled = false;
				return;
			}

			returnPosition = PlayerControl.LocalPlayer.transform.position;
			lockedCameraPosition = Camera.main.transform.position;
			hasLockedCamera = true;
			nextNetSnap = 0f;

			followerCamera = Camera.main.GetComponent<FollowerCamera>();
			if (followerCamera != null)
				followerCamera.enabled = false;

			Vector2 nan = new Vector2(float.NaN, float.NaN);
			PlayerControl.LocalPlayer.NetTransform.SnapTo(nan);
			try
			{
				BatchedMessage batch = new BatchedMessage();
				batch.QueueSnapTo(PlayerControl.LocalPlayer, nan);
				batch.FinishBatch();
			}
			catch { }

			Bane.Log.LogInfo($"NaN Position enabled (local + SnapTo RPC). Camera locked at {lockedCameraPosition}");
			Bane.notifications.Send("NaN Position", "NaN via SnapTo RPC + local. Camera frozen.", 5);
		}

		protected override void OnDisable()
		{
			hasLockedCamera = false;

			if (followerCamera != null)
			{
				followerCamera.enabled = true;
				if (PlayerControl.LocalPlayer != null)
					followerCamera.SetTarget(PlayerControl.LocalPlayer);
				followerCamera = null;
			}

			if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.Data != null)
			{
				PlayerControl.LocalPlayer.NetTransform.SnapTo(returnPosition);
				try
				{
					BatchedMessage batch = new BatchedMessage();
					batch.QueueSnapTo(PlayerControl.LocalPlayer, returnPosition);
					batch.FinishBatch();
				}
				catch { }
			}

			Bane.Log.LogInfo("NaN Position disabled.");
		}
	}
}
