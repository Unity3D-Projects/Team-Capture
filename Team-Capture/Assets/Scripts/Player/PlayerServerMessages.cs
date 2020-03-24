﻿using Core;
using Core.Logger;
using Core.Networking.Messages;
using Mirror;
using Pickups;
using UI;
using UnityEngine;
using Logger = Core.Logger.Logger;

namespace Player
{
	public class PlayerServerMessages : MonoBehaviour
	{
		/// <summary>
		/// The <see cref="ClientUI"/> that is associated with this client
		/// </summary>
		private ClientUI clientUi;

		private void Awake()
		{
			clientUi =  GetComponent<PlayerManager>().clientUi;

			//Register all our custom messages
			NetworkClient.RegisterHandler<SetPickupStatus>(PickupMessage);
			NetworkClient.RegisterHandler<PlayerDiedMessage>(PlayerDiedMessage);
			NetworkClient.RegisterHandler<WeaponSyncMessage>(SyncWeapon);
		}

		private void OnDestroy()
		{
			//Unregister our custom messages on destroy
			NetworkClient.UnregisterHandler<SetPickupStatus>();
			NetworkClient.UnregisterHandler<PlayerDiedMessage>();
			NetworkClient.UnregisterHandler<WeaponSyncMessage>();
		}

		/// <summary>
		/// Player died message, for killfeed
		/// </summary>
		/// <param name="conn"></param>
		/// <param name="message"></param>
		private void PlayerDiedMessage(NetworkConnection conn, PlayerDiedMessage message)
		{
			clientUi.killFeed.AddFeedBackItem(message);
		}

		/// <summary>
		/// When a pickup changes status
		/// </summary>
		/// <param name="conn"></param>
		/// <param name="status"></param>
		private static void PickupMessage(NetworkConnection conn, SetPickupStatus status)
		{
			string pickupDirectory = GameManager.GetActiveScene().pickupsParent + status.PickupName;
			GameObject pickup = GameObject.Find(pickupDirectory);
			if (pickup == null)
			{
				Logger.Log($"Was told to change status of a pickup at `{pickupDirectory}` that doesn't exist!", LogVerbosity.Error);
				return;
			}

			Pickup pickupLogic = pickup.GetComponent<Pickup>();

			foreach (PickupMaterials pickupMaterial in pickupLogic.pickupMaterials)
			{
				pickupMaterial.meshToChange.material = status.IsActive ? pickupMaterial.pickupMaterial : pickupMaterial.pickupPickedUpMaterial;
			}
		}

		/// <summary>
		/// Syncs weapons stuff, for UI
		/// </summary>
		/// <param name="conn"></param>
		/// <param name="weaponSync"></param>
		private void SyncWeapon(NetworkConnection conn, WeaponSyncMessage weaponSync)
		{
			clientUi.hud.UpdateAmmoUI(weaponSync);
		}
	}
}
