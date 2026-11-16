using System;
using BaneMenu.modules;
using HarmonyLib;
using UnityEngine;

namespace BaneMenu.modules.troll
{
	/// <summary>
	/// Spams meetings/reports so the lobby can't progress normally.
	/// UI label: "Disable Meetings/Reports"
	/// Triggers after the Shhh start screen.
	/// </summary>
	internal class MeetingSpam : Module
	{
		public MeetingSpam() : base("MeetingSpam") { }

		private bool _hasTriggered;
		private float _delayTimer;
		private int _meetingCount;
		private const int MAX_MEETINGS = 4;

		protected override void OnEnable()
		{
			base.OnEnable();
			_hasTriggered = false;
			_delayTimer = 0f;
			_meetingCount = 0;
			Bane.notifications.Send("Disable Meetings/Reports", "Enabled. Will spam meetings/reports once a meeting starts.", 4);
		}

		protected override void OnDisable()
		{
			_hasTriggered = false;
			_meetingCount = 0;
		}

		public void Tick()
		{
			if (!Enabled || !_hasTriggered) return;
			if (_meetingCount >= MAX_MEETINGS)
			{
				Enabled = false;
				return;
			}

			_delayTimer += Time.deltaTime;
			if (_delayTimer >= 0.45f)
			{
				_delayTimer = 0f;
				CallMeeting();
			}
		}

		public void OnShhh()
		{
			if (!Enabled || _hasTriggered) return;
			_hasTriggered = true;
			_delayTimer = 0f;
			_meetingCount = 0;
			Bane.notifications.Send("Disable Meetings/Reports", "Spamming meetings/reports...", 3);
			CallMeeting();
		}

		private void CallMeeting()
		{
			try
			{
				if (PlayerControl.LocalPlayer == null || AmongUsClient.Instance == null) return;
				if (MeetingHud.Instance != null) return;

				if (Utilities.IsHostEffective())
				{
					MeetingRoomManager.Instance.AssignSelf(PlayerControl.LocalPlayer, null);
					PlayerControl.LocalPlayer.RpcStartMeeting(null);
					if (HudManager.Instance != null)
						HudManager.Instance.OpenMeetingRoom(PlayerControl.LocalPlayer);
				}
				else
				{
					PlayerControl.LocalPlayer.CmdReportDeadBody(null);
				}
				_meetingCount++;
			}
			catch (Exception e)
			{
				Bane.Log.LogWarning("MeetingSpam: " + e.Message);
			}
		}
	}

	[HarmonyPatch(typeof(ShhhBehaviour), nameof(ShhhBehaviour.OnEnable))]
	internal static class MeetingSpamShhhPatch
	{
		static void Postfix()
		{
			if (ModuleManager.meetingSpam != null && ModuleManager.meetingSpam.Enabled)
				ModuleManager.meetingSpam.OnShhh();
		}
	}
}
