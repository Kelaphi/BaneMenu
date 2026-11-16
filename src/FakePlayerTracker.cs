using System.Collections.Generic;
using BaneMenu.network;
using InnerNet;
using UnityEngine;

namespace BaneMenu
{
	/// <summary>
	/// Tracks host-spawned Player shells: despawn after TTL, and keep force-SnapTo'ing
	/// their intended world position so remotes don't settle at origin.
	/// </summary>
	internal static class FakePlayerTracker
	{
		public const float DefaultTtlSeconds = 20f;
		private const float PinInterval = 0.12f;

		private class Entry
		{
			public InnerNetObject Obj;
			public PlayerControl Pc;
			public Vector2 PinPos;
			public float SpawnedAt;
			public float NextPinAt;
		}

		private static readonly List<Entry> entries = new List<Entry>();

		public static void Register(InnerNetObject obj, Vector2 pinPos)
		{
			if (obj == null) return;
			PlayerControl pc = null;
			try { pc = obj.TryCast<PlayerControl>(); } catch { }

			entries.Add(new Entry
			{
				Obj = obj,
				Pc = pc,
				PinPos = pinPos,
				SpawnedAt = Time.realtimeSinceStartup,
				NextPinAt = Time.realtimeSinceStartup
			});
		}

		/// <summary>Update pin target (orbit uses this every move).</summary>
		public static void UpdatePin(InnerNetObject obj, Vector2 pinPos)
		{
			if (obj == null) return;
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i].Obj != obj) continue;
				entries[i].PinPos = pinPos;
				return;
			}
		}

		public static void Tick(float ttl = DefaultTtlSeconds)
		{
			float now = Time.realtimeSinceStartup;
			BatchedMessage batch = null;

			for (int i = entries.Count - 1; i >= 0; i--)
			{
				var e = entries[i];
				if (e.Obj == null)
				{
					entries.RemoveAt(i);
					continue;
				}

				if (now - e.SpawnedAt >= ttl)
				{
					try { e.Obj.Despawn(); }
					catch
					{
						try { Object.Destroy(e.Obj.gameObject); } catch { }
					}
					entries.RemoveAt(i);
					Bane.Log.LogInfo("FakePlayerTracker: despawned shell past TTL");
					continue;
				}

				// Keep pinning for the whole lifetime - single post-spawn snaps die to 0,0
				if (now < e.NextPinAt) continue;
				e.NextPinAt = now + PinInterval;

				if (e.Pc == null || e.Pc.NetTransform == null) continue;

				try
				{
					bool networkOnly = BaneMenu.ui.sections.HostSection.SpawnPlayerNetworkOnly;
					if (!networkOnly)
					{
						e.Pc.transform.position = new Vector3(e.PinPos.x, e.PinPos.y, 0f);
						e.Pc.NetTransform.SnapTo(e.PinPos);
					}

					if (batch == null) batch = new BatchedMessage();
					if (networkOnly)
						batch.QueueSnapToNetworkOnly(e.Pc, e.PinPos);
					else
						batch.QueueSnapTo(e.Pc, e.PinPos);
				}
				catch { }
			}

			if (batch != null && batch.msgCount > 0)
			{
				try { batch.FinishBatch(); } catch { }
			}
		}

		public static void ClearAll()
		{
			for (int i = entries.Count - 1; i >= 0; i--)
			{
				var e = entries[i];
				if (e.Obj != null)
				{
					try { e.Obj.Despawn(); }
					catch
					{
						try { Object.Destroy(e.Obj.gameObject); } catch { }
					}
				}
			}
			entries.Clear();
		}
	}
}
