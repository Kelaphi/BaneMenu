using BaneMenu.modules;
using BaneMenu.network;
using UnityEngine;

namespace BaneMenu.routines
{
	internal class SpamInfluencerCues : Routine
	{
		// Host-authoritative lobbies only (vanilla AC requires per-player InfluencerMessage)
		public SpamInfluencerCues() : base("SpamInfluencerCues") { }

		private readonly int MAX_SPRITE_COUNT = 3;
		private readonly float SEND_DELAY = 4.0f;
		private float timeElapsed = 0.0f;
		private System.Random rnd = new System.Random();

		public override void Run()
		{
			timeElapsed += Time.deltaTime;
			if(timeElapsed < SEND_DELAY) return;
			timeElapsed = 0.0f;

			if(ShipStatus.Instance == null || ShipStatus.Instance.socialMediumFeedSystem == null)
				return;

			byte[] sprites = new byte[MAX_SPRITE_COUNT];
			int possibleSpriteCount = ShipStatus.Instance.socialMediumFeedSystem.PossibleSpriteList.Count;
			if(possibleSpriteCount <= 0) return;

			for(int i = 0; i < sprites.Length; i++)
			{
				sprites[i] = (byte)rnd.Next(0, possibleSpriteCount);
			}

			BatchedMessage batch = new BatchedMessage();
			batch.QueueInfluencerMessage(sprites);
			batch.FinishBatch();
		}

		private void OnDisconnect()
		{
			Bane.notifications.Send("Spam Influencer Cues", "Disabled as you left the game.", 10);
			Enabled = false;
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			if(PlayerControl.LocalPlayer == null || ShipStatus.Instance == null)
			{
				Bane.notifications.Send("Spam Influencer Cues", "Can only be used once the game has started.", 10);
				Enabled = false;
				return;
			}

			EventCoordinator.OnDisconnect += OnDisconnect;
		}

		protected override void OnDisable()
		{
			EventCoordinator.OnDisconnect -= OnDisconnect;
		}
	}
}
