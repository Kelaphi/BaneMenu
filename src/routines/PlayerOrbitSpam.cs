using System.Collections.Generic;
using InnerNet;
using BaneMenu.network;
using UnityEngine;

namespace BaneMenu.routines
{
	/// <summary>
	/// Host: fake Players orbit target. Positions are network-only (no local transform
	/// updates) so the host sees roughly what remotes see. Colors via RpcSetColor.
	/// </summary>
	public class PlayerOrbitSpamRoutine : Routine
	{
		public PlayerOrbitSpamRoutine() : base("PlayerOrbitSpam") { }

		public readonly HashSet<int> targets = new HashSet<int>();

		private readonly List<InnerNetObject> ring = new List<InnerNetObject>();
		private float angle;
		private float radius = 1.4f;
		private int count = 4;
		private float speed = 1.4f;
		private float snapTimer;
		private const float SnapInterval = 0.18f;
		private int colorCursor;

		public override void Run()
		{
			if (!AmongUsClient.Instance || !Utilities.IsHostEffective())
				return;
			if (PlayerControl.LocalPlayer == null)
				return;
			if (targets.Count == 0)
			{
				ClearRing(true);
				return;
			}

			PlayerControl focus = null;
			foreach (PlayerControl p in PlayerControl.AllPlayerControls)
			{
				if (p == null || p.Data == null) continue;
				if (targets.Contains(p.GetHashCode()))
				{
					focus = p;
					break;
				}
			}

			if (focus == null || focus.Data.Disconnected)
			{
				ClearRing(true);
				return;
			}

			if (ring.Count == 0)
				SpawnRing(focus);

			angle += speed * Time.deltaTime;
			if (angle >= Mathf.PI * 2f)
			{
				angle -= Mathf.PI * 2f;
				ClearRing(true);
				SpawnRing(focus);
			}

			snapTimer -= Time.deltaTime;
			if (snapTimer > 0f) return;
			snapTimer = SnapInterval;

			Vector3 center = focus.transform.position;
			BatchedMessage batch = new BatchedMessage();

			for (int i = 0; i < ring.Count; i++)
			{
				var obj = ring[i];
				if (obj == null) continue;
				float a = angle + (i * (Mathf.PI * 2f / Mathf.Max(1, ring.Count)));
				Vector2 pos = new Vector2(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius);
				try
				{
					FakePlayerTracker.UpdatePin(obj, pos);
					var pc = obj.TryCast<PlayerControl>();
					if (pc != null && pc.NetTransform != null)
					{
						if (BaneMenu.ui.sections.HostSection.SpawnPlayerNetworkOnly)
							batch.QueueSnapToNetworkOnly(pc, pos);
						else
							batch.QueueSnapTo(pc, pos);
					}
				}
				catch { }
			}

			if (batch.msgCount > 0)
			{
				try { batch.FinishBatch(); } catch { }
			}
		}

		protected override void OnDisable()
		{
			ClearRing(true);
			targets.Clear();
		}

		private InnerNetObject FindPlayerPrefab()
		{
			if (AmongUsClient.Instance == null) return null;
			var list = AmongUsClient.Instance.NonAddressableSpawnableObjects;
			if (list == null) return null;
			for (int i = 0; i < list.Count; i++)
			{
				var obj = list[i];
				if (obj == null) continue;
				string n = obj.gameObject != null ? obj.gameObject.name : "";
				if (obj.SpawnId == 4 || n.Equals("Player", System.StringComparison.OrdinalIgnoreCase))
					return obj;
			}
			return null;
		}

		private void SpawnRing(PlayerControl focus)
		{
			InnerNetObject prefab = FindPlayerPrefab();
			if (prefab == null) return;

			Vector3 center = focus.transform.position;
			BatchedMessage spawnBatch = new BatchedMessage();
			List<PlayerControl> spawnedPcs = new List<PlayerControl>();
			List<Vector2> slots = new List<Vector2>();

			for (int i = 0; i < count; i++)
			{
				try
				{
					InnerNetObject spawned = Object.Instantiate(prefab).Cast<InnerNetObject>();
					float a = angle + (i * (Mathf.PI * 2f / count));
					Vector2 pos = new Vector2(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius);

					// Set transform for spawn serialization only; do not keep correcting locally after
					spawned.transform.position = new Vector3(pos.x, pos.y, center.z);

					PlayerControl pc = null;
					try { pc = spawned.TryCast<PlayerControl>(); } catch { }

					spawnBatch.QueueSpawn(spawned, -2, SpawnFlags.None);
					ring.Add(spawned);
					FakePlayerTracker.Register(spawned, pos);
					if (pc != null)
					{
						spawnedPcs.Add(pc);
						slots.Add(pos);
					}
				}
				catch (System.Exception e)
				{
					Bane.Log.LogWarning($"PlayerOrbitSpam spawn: {e.Message}");
				}
			}

			try { spawnBatch.FinishBatch(); } catch { }

			try
			{
				BatchedMessage snapBatch = new BatchedMessage();
				for (int i = 0; i < spawnedPcs.Count; i++)
				{
					var pc = spawnedPcs[i];
					if (pc == null || pc.NetTransform == null) continue;
					if (BaneMenu.ui.sections.HostSection.SpawnPlayerNetworkOnly)
						snapBatch.QueueSnapToNetworkOnly(pc, slots[i]);
					else
						snapBatch.QueueSnapTo(pc, slots[i]);
				}
				if (snapBatch.msgCount > 0)
					snapBatch.FinishBatch();
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning($"PlayerOrbitSpam post-spawn snap: {e.Message}");
			}

			for (int i = 0; i < spawnedPcs.Count; i++)
			{
				try
				{
					byte color = (byte)(colorCursor % 12);
					colorCursor++;
					spawnedPcs[i].RpcSetColor(color);
				}
				catch (System.Exception e)
				{
					Bane.Log.LogWarning($"PlayerOrbitSpam color: {e.Message}");
				}
			}
		}

		private void ClearRing(bool networkDespawn)
		{
			foreach (var obj in ring)
			{
				if (obj == null) continue;
				try
				{
					if (networkDespawn)
						obj.Despawn();
					else
						Object.Destroy(obj.gameObject);
				}
				catch
				{
					try { Object.Destroy(obj.gameObject); } catch { }
				}
			}
			ring.Clear();
		}
	}
}
