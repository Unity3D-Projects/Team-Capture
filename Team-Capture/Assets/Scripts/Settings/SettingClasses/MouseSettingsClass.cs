﻿// Team-Capture
// Copyright (C) 2019-2021 Voltstro-Studios
// 
// This project is governed by the AGPLv3 License.
// For more details see the LICENSE file.

using Team_Capture.UI;
using UnityEngine;

namespace Team_Capture.Settings.SettingClasses
{
	internal class MouseSettingsClass : Setting
	{
		[Range(50, 200)] [SettingsPropertyDisplayText("Settings_MouseSensitivity")]
		public int MouseSensitivity = 100;

		[SettingsPropertyDisplayText("Settings_MouseReverse")]
		public bool ReverseMouse;
	}
}