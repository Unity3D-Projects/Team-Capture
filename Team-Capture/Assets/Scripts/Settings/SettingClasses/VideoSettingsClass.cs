﻿using Attributes;
using UnityEngine;

namespace Settings.SettingClasses
{
	public sealed class VideoSettingsClass : Setting
	{
		public Resolution Resolution = Screen.currentResolution;
		
		[SettingsMenuFormat("Screen Mode")] public FullScreenMode ScreenMode = FullScreenMode.FullScreenWindow;
	}
}