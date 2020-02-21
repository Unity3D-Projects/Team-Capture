﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Logger;
using Core.Networking;
using Helper;
using Mirror;
using Player;
using UnityEngine;
using Logger = Core.Logger.Logger;

namespace Weapons
{
	public class WeaponManager : NetworkBehaviour
	{
		private readonly SyncListWeapons weapons = new SyncListWeapons();

		[field: SyncVar(hook = nameof(SelectWeapon))] 
		public int SelectedWeaponIndex { get; private set; }

		[SerializeField] private string weaponLayerName = "LocalWeapon";

		[SerializeField] private Transform weaponsHolderSpot;

		private PlayerManager playerManager;

		public int WeaponHolderSpotChildCount => weaponsHolderSpot.childCount;

		private void Start()
		{
			playerManager = GetComponent<PlayerManager>();

			//Create all existing weapons on start
			for (int i = 0; i < weapons.Count; i++)
			{
				GameObject newWeapon =
					Instantiate(WeaponsResourceManager.GetWeapon(weapons[i]).baseWeaponPrefab, weaponsHolderSpot);

				newWeapon.SetActive(SelectedWeaponIndex == i);
			}
		}

		public override void OnStartServer()
		{
			base.OnStartServer();

			//Setup our add weapon callback
			weapons.Callback += ServerWeaponCallback;

			//Add stock weapons to client
			AddStockWeapons();
		}

		public override void OnStartLocalPlayer()
		{
			base.OnStartLocalPlayer();

			weapons.Callback += ClientWeaponCallback;

			weaponsHolderSpot.gameObject.AddComponent<WeaponSway>();
		}

		[Server]
		private void ServerWeaponCallback(SyncList<string>.Operation op, int itemIndex, string item, string newItem)
		{
			switch (op)
			{
				case SyncList<string>.Operation.OP_ADD when newItem == null:
					Logger.Log("Passed in weapon to be added is null!", LogVerbosity.Error);
					weapons.Remove(item);
					return;
				case SyncList<string>.Operation.OP_ADD:
					RpcInstantiateWeaponOnClients(newItem);
					break;
				case SyncList<string>.Operation.OP_CLEAR:
					RpcRemoveAllActiveWeapons();
					break;
			}
		}

		[Client]
		private void ClientWeaponCallback(SyncList<string>.Operation op, int itemIndex, string item, string newItem)
		{
			if (op != SyncList<string>.Operation.OP_ADD) return;

			if (newItem == null)
			{
				Logger.Log("Passed in weapon to be added is null!", LogVerbosity.Error);
				return;
			}

			playerManager.clientUi.hud.UpdateAmmoUi(this);
		}

		[ClientRpc]
		private void RpcInstantiateWeaponOnClients(string weaponName)
		{
			if (weaponName == null) return;

			GameObject newWeapon = Instantiate(WeaponsResourceManager.GetWeapon(weaponName).baseWeaponPrefab,
				weaponsHolderSpot);
			if (isLocalPlayer)
				Layers.SetLayerRecursively(newWeapon, LayerMask.NameToLayer(weaponLayerName));
		}

		#region Weapon Reloading

		public IEnumerator ReloadCurrentWeapon()
		{
			TCWeapon weapon = GetActiveWeapon();

			if (weapon.isReloading)
				yield break;

			Logger.Log($"Reloading weapon `{weapon.weapon}`", LogVerbosity.Debug);

			weapon.currentBulletsAmount = 0;
			weapon.isReloading = true;

			GetComponent<PlayerManager>().clientUi.hud.UpdateAmmoUi(this);

			yield return new WaitForSeconds(weapon.reloadTime);

			weapon.Reload();
			weapon.isReloading = false;

			GetComponent<PlayerManager>().clientUi.hud.UpdateAmmoUi(this);
		}

		#endregion

		public TCWeapon GetActiveWeapon()
		{
			return weapons.Count == 0 ? null : WeaponsResourceManager.GetWeapon(weapons[SelectedWeaponIndex]);
		}

		public WeaponGraphics GetActiveWeaponGraphics()
		{
			return weaponsHolderSpot.GetChild(SelectedWeaponIndex).GetComponent<WeaponGraphics>();
		}

		public TCWeapon GetWeapon(string weapon)
		{
			IEnumerable<string> result = from a in weapons
				where a == weapon
				select a;

			return WeaponsResourceManager.GetWeapon(result.FirstOrDefault());
		}

		private class SyncListWeapons : SyncList<string>
		{
		}

		#region Add Weapons

		[Server]
		public void AddStockWeapons()
		{
			foreach (TCWeapon weapon in TCNetworkManager.Instance.stockWeapons)
				AddWeapon(weapon.weapon);
		}

		/// <summary>
		/// Direct server command.
		/// This function adds a weapon to a player
		/// </summary>
		/// <param name="weaponName"></param>
		[Server]
		public void ServerAddWeapon(string weaponName)
		{
			if (WeaponsResourceManager.GetWeapon(weaponName) == null) return;

			AddWeapon(weaponName);
		}

		[Server]
		private void AddWeapon(string weapon)
		{
			TCWeapon tcWeapon = WeaponsResourceManager.GetWeapon(weapon);

			if (tcWeapon == null)
				return;

			weapons.Add(tcWeapon.weapon);

			//Setup the new added weapon, and stop any reloading going on with the current weapon
			TargetSetupWeapon(weapon);

			if (weapons.Count > 1)
			{
				SelectedWeaponIndex += 1;
				RpcSelectWeapon(SelectedWeaponIndex);
			}
		}

		[TargetRpc]
		private void TargetSetupWeapon(string weapon)
		{
			Logger.Log($"Setting up weapon `{weapon}`", LogVerbosity.Debug);

			StopCoroutine(ReloadCurrentWeapon());
			WeaponsResourceManager.GetWeapon(weapon).Reload();
		}

		#endregion

		#region Weapon Removal

		[Server]
		public void RemoveAllWeapons()
		{
			SelectedWeaponIndex = 0;
			weapons.Clear();
		}

		[ClientRpc]
		private void RpcRemoveAllActiveWeapons()
		{
			for (int i = 0; i < weaponsHolderSpot.childCount; i++) Destroy(weaponsHolderSpot.GetChild(i).gameObject);
		}

		#endregion

		#region Weapon Selection

		public void SelectWeapon(int oldValue, int newValue)
		{
			if (!isLocalPlayer)
				return;

			playerManager.clientUi.hud.UpdateAmmoUi(this);
		}

		[Command]
		public void CmdSetWeapon(int index)
		{
			Logger.Log($"Player `{transform.name}` set their weapon index to `{index}`.", LogVerbosity.Debug);

			//Set the selected weapon index and update the visible gameobject
			SelectedWeaponIndex = index;
			RpcSelectWeapon(index);
		}

		[ClientRpc]
		private void RpcSelectWeapon(int index)
		{
			for (int i = 0; i < weaponsHolderSpot.childCount; i++)
				weaponsHolderSpot.GetChild(i).gameObject.SetActive(i == index);
		}

		#endregion
	}
}