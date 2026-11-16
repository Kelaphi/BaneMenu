using BaneMenu.network;
using UnityEngine;

namespace BaneMenu.modules.misc
{
	/// <summary>
	/// Polls AC posture every 0.5s. Host does NOT mean "no anticheat" - 
	/// host is still AC, just more lenient. Only freeplay/local/modded = no AC.
	/// </summary>
	internal class AnticheatWatch : Module
	{
		public AnticheatWatch() : base("Anticheat Watch") { }

		public bool LogEveryPoll { get; set; } = false;
		public float Interval { get; set; } = 0.5f;

		float nextPoll;
		string lastState = null;

		public enum AcPosture
		{
			Unknown,
			None,           // freeplay / local / modded / no session
			HostLenient,    // you are host OR PlayerInfo host-owned - AC still exists, laxer
			ServerStrict    // server-auth PlayerInfo - full public AC
		}

		public static AcPosture Evaluate(out string detail)
		{
			detail = "";
			if (AmongUsClient.Instance == null)
			{
				detail = "no client";
				return AcPosture.Unknown;
			}

			try
			{
				if (AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
				{
					detail = "FreePlay";
					return AcPosture.None;
				}
				if (AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame)
				{
					detail = "LocalGame";
					return AcPosture.None;
				}
			}
			catch { }

			try
			{
				if (Constants.IsVersionModded())
				{
					detail = "modded version";
					return AcPosture.None;
				}
			}
			catch { }

			if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null)
			{
				detail = "no LocalPlayer.Data";
				return AcPosture.Unknown;
			}

			int owner = PlayerControl.LocalPlayer.Data.OwnerId;
			bool amHost = AmongUsClient.Instance.AmHost;
			var state = AmongUsClient.Instance.GameState;
			detail = $"OwnerId={owner} AmHost={amHost} state={state}";

			// Server-owned PlayerInfo → strict public AC
			if (owner != (int)OwnerIds.Host)
			{
				detail += " | server-auth PlayerInfo";
				return AcPosture.ServerStrict;
			}

			// Host-owned PlayerInfo: still anticheat, but host-authoritative / lenient
			detail += " | host-auth PlayerInfo (lenient, not AC-off)";
			return AcPosture.HostLenient;
		}

		public static string PostureLabel(AcPosture p)
		{
			return p switch
			{
				AcPosture.None => "NO anticheat (freeplay/local/modded)",
				AcPosture.HostLenient => "AC PRESENT (host-lenient)",
				AcPosture.ServerStrict => "AC PRESENT (server-strict)",
				_ => "Unknown (not in a readable session)"
			};
		}

		public void Tick()
		{
			if (!Enabled) return;
			if (Time.unscaledTime < nextPoll) return;
			nextPoll = Time.unscaledTime + Mathf.Max(0.1f, Interval);

			AcPosture posture = Evaluate(out string detail);
			string key = posture.ToString();

			if (LogEveryPoll)
				Bane.Log.LogInfo($"[ACWatch] {PostureLabel(posture)} | {detail}");

			if (lastState == null)
			{
				lastState = key;
				Bane.Log.LogInfo($"[ACWatch] initial: {PostureLabel(posture)} | {detail}");
				return;
			}

			if (key != lastState)
			{
				string prev = lastState;
				lastState = key;
				Bane.notifications.Send("AC Watch", PostureLabel(posture), 6);
				Bane.Log.LogWarning($"[ACWatch] CHANGE {prev} → {key} | {detail}");
			}
		}
	}
}
