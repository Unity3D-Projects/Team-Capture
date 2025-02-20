﻿// Team-Capture
// Copyright (C) 2019-2021 Voltstro-Studios
// 
// This project is governed by the AGPLv3 License.
// For more details see the LICENSE file.

using Team_Capture.Core.Networking;
using Team_Capture.UI;
using UnityEngine;

namespace Team_Capture.Settings.SettingClasses
{
	internal class MultiplayerSettingsClass : Setting
	{
		[SettingsPropertyDisplayText("Settings_MultiplayerMuzzleFlashLighting")]
		public bool WeaponMuzzleFlashLighting = true;

		[SettingsPropertyDisplayText("Settings_MultiplayerWeaponSway")]
		public bool WeaponSwayEnabled = true;

		[Range(0, 15)]
		[SettingsPropertyDisplayText("Settings_MultiplayerWeaponSwayAmount")]
		public float WeaponSwayAmount = 0.1f;

		[SettingsPropertyDisplayText("Settings_MultiplayerMOTDMode")]
		public Client.ClientMOTDMode MOTDMode = Client.ClientMOTDMode.WebSupport;
	}
}