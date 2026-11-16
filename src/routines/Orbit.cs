using BaneMenu.modules;
using InnerNet;
using UnityEngine;

namespace BaneMenu.routines
{
	public class OrbitRoutine : Routine
	{
		public OrbitRoutine() : base("Orbit") { }

		private float angle = 0f;
		private float lastNetworkSnap = 0f;

		private const float RADIUS = 1.35f;
		private const float SPEED = 3.2f; // radians per second
		// ~8 network snaps/sec max - frame-rate SnapTo is what got you banned
		private const float NETWORK_INTERVAL = 0.12f;

		public override void Run()
		{
			if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return;
			if (PlayerControl.LocalPlayer.Data.IsDead || PlayerControl.LocalPlayer.Data.Disconnected) return;
			if (ShipStatus.Instance == null) return;
			if (MeetingHud.Instance != null) return;

			PlayerControl nearest = null;
			float best = float.MaxValue;

			foreach (PlayerControl p in PlayerControl.AllPlayerControls)
			{
				if (p == null || p == PlayerControl.LocalPlayer) continue;
				if (p.Data == null || p.Data.IsDead || p.Data.Disconnected) continue;

				float d = Vector2.Distance(PlayerControl.LocalPlayer.GetTruePosition(), p.GetTruePosition());
				if (d < best)
				{
					best = d;
					nearest = p;
				}
			}

			if (nearest == null) return;

			angle += SPEED * Time.deltaTime;
			if (angle > Mathf.PI * 2f) angle -= Mathf.PI * 2f;

			Vector2 center = nearest.GetTruePosition();
			Vector2 pos = center + new Vector2(Mathf.Cos(angle) * RADIUS, Mathf.Sin(angle) * RADIUS);

			// Local move every frame so it looks smooth on your screen
			PlayerControl.LocalPlayer.NetTransform.SnapTo(pos);

			// Network only on a throttle so the server doesn't rate-limit / ban you
			if (Time.time - lastNetworkSnap >= NETWORK_INTERVAL)
			{
				lastNetworkSnap = Time.time;
				PlayerControl.LocalPlayer.NetTransform.RpcSnapTo(pos);
			}
		}

		private void OnDisconnect()
		{
			Enabled = false;
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			angle = 0f;
			lastNetworkSnap = 0f;
			EventCoordinator.OnDisconnect += OnDisconnect;
		}

		protected override void OnDisable()
		{
			EventCoordinator.OnDisconnect -= OnDisconnect;
		}
	}
}
