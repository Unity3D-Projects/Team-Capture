﻿using System.Collections.Generic;
using Core.Networking.Messages;
using Mirror;
using SceneManagement;
using UnityEngine;
using Logger = Core.Logging.Logger;

namespace Pickups
{
	/// <summary>
	/// A static class for the server to manage pickups
	/// </summary>
	public static class ServerPickupManager
	{
		/// <summary>
		/// The tag for pickups
		/// </summary>
		private const string PickupTagName = "Pickup";

		private static List<string> unActivePickups = new List<string>();
		private const int ChannelToSendOnId = 1;

		public static void SetupServerPickupManager()
		{
			unActivePickups = new List<string>();

			SceneManager.OnBeginSceneLoading += OnBeginSceneLoading;
			TCScenesManager.OnSceneLoadedEvent += OnSceneLoaded;
		}

		public static void ShutdownServer()
		{
			SceneManager.OnBeginSceneLoading -= OnBeginSceneLoading;
			TCScenesManager.OnSceneLoadedEvent -= OnSceneLoaded;
		}

		private static void OnBeginSceneLoading(AsyncOperation loadOperation)
		{
			unActivePickups.Clear();
		}

		private static void OnSceneLoaded(TCScene scene)
		{
			//If the scene isn't an online scene then there will be no pickups
			if(!scene.isOnlineScene) return;

			//Setup pickups
			//TODO: We should save all references to pickups to the associated scene file
			GameObject[] pickups = GameObject.FindGameObjectsWithTag(PickupTagName);
			foreach (GameObject pickup in pickups)
			{
				//Make sure it has the Pickup script on it
				Pickup pickupLogic = pickup.GetComponent<Pickup>();
				if(pickupLogic == null)
				{
					Logger.Error("The pickup with the name of `{@PickupName}` @ {@PickupTransform} doesn't have the {@Pickup} behaviour on it!", pickup.name, pickup.transform, typeof(Pickup));
					continue;
				}

				//Setup the trigger
				pickupLogic.SetupTrigger();
			}
		}

		/// <summary>
		/// Call this when a client joins the game
		/// </summary>
		/// <param name="conn"></param>
		public static void OnClientJoined(NetworkConnection conn)
		{
			conn.Send(new PickupStatusMessage
			{
				DisabledPickups = unActivePickups.ToArray()
			});
		}

		/// <summary>
		/// Deactivate a pickup
		/// </summary>
		/// <param name="pickup"></param>
		public static void DeactivatePickup(Pickup pickup)
		{
			unActivePickups.Add(pickup.name);

			NetworkServer.SendToAll(new SetPickupStatus
			{
				PickupName = pickup.name,
				IsActive = false
			}, ChannelToSendOnId);
		}

		/// <summary>
		/// Activate a pickup
		/// </summary>
		/// <param name="pickup"></param>
		public static void ActivatePickup(Pickup pickup)
		{
			unActivePickups.Remove(pickup.name);

			NetworkServer.SendToAll(new SetPickupStatus
			{
				PickupName = pickup.name,
				IsActive = true
			}, ChannelToSendOnId);
		}
	}
}