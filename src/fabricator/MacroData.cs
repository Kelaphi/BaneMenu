using System.Collections.Generic;

namespace BaneMenu.fabricator
{
	internal enum MacroEventType : byte
	{
		Kill = 1,
		TaskComplete = 2,
		StartMeeting = 3,
		Chat = 4,
		ReportBody = 5,
		EnterVent = 6,
		ExitVent = 7,
	}

	internal struct MacroSample
	{
		public float t;
		public float x;
		public float y;
		public byte facing;
		public bool inVent;
	}

	internal struct MacroEvent
	{
		public float t;
		public MacroEventType type;
		public float x;
		public float y;
		public byte targetPlayerId;
		public string text;
	}

	/// <summary>One recorded player performance inside a macro.</summary>
	internal class PlayerTrack
	{
		public string recordedName = "Player";
		public string hatId = "";
		public string skinId = "";
		public string visorId = "";
		public string namePlateId = "";
		public string petId = "";
		public byte colorId = 0;
		public float duration;
		public List<MacroSample> samples = new List<MacroSample>();
		public List<MacroEvent> events = new List<MacroEvent>();
	}

	/// <summary>A named macro = collection of player tracks played together.</summary>
	internal class Macro
	{
		public string name = "Macro";
		public List<PlayerTrack> players = new List<PlayerTrack>();

		public float Duration
		{
			get
			{
				float d = 0f;
				foreach (var p in players)
					if (p != null && p.duration > d) d = p.duration;
				return d;
			}
		}
	}
}
