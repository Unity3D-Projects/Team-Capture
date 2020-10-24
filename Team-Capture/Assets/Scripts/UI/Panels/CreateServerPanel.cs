﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Core.Networking;
using SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Logger = Core.Logging.Logger;

namespace UI.Panels
{
	/// <summary>
	/// The code driving the create server panel
	/// </summary>
	internal class CreateServerPanel : MainMenuPanelBase
	{
		private List<TCScene> onlineTCScenes;

		private Image gameNameImage;
		private Image maxPlayersImage;

		private Color gameNameImageColor;
		private Color maxPlayersImageColor;

		private int maxPlayers = 16;

		private TCNetworkManager netManager;

		/// <summary>
		/// Dropdown for the maps
		/// </summary>
		[Tooltip("Dropdown for the maps")]
		public TMP_Dropdown mapsDropdown;

		/// <summary>
		/// Input for the game name
		/// </summary>
		[Tooltip("Input for the game name")]
		public TMP_InputField gameNameText;

		/// <summary>
		/// Input for the max amount of players
		/// </summary>
		[Tooltip("Input for the max amount of players")]
		public TMP_InputField maxPlayersText;

		/// <summary>
		/// The color to use when there is an error
		/// </summary>
		[Tooltip("The color to use when there is an error")]
		public Color errorColor = Color.red;

		private void Start()
		{
			//First, clear the maps dropdown
			mapsDropdown.ClearOptions();

			//Then get all online scenes
			onlineTCScenes = TCScenesManager.GetAllEnabledOnlineScenesInfo().ToList();

			//And all the scenes to the map dropdown
			List<string> scenes = onlineTCScenes.Select(scene => scene.displayName).ToList();
			mapsDropdown.AddOptions(scenes);
			mapsDropdown.RefreshShownValue();

			//Get active network manager
			netManager = TCNetworkManager.Instance;

			//Get the images that are in the input fields
			gameNameImage = gameNameText.GetComponent<Image>();
			maxPlayersImage = maxPlayersText.GetComponent<Image>();

			//Get the existing colors of the input fields
			gameNameImageColor = gameNameImage.color;
			maxPlayersImageColor = maxPlayersImage.color;
		}

		/// <summary>
		/// Starts the server and connects a player
		/// </summary>
		public void CreateGame()
		{
#if UNITY_EDITOR
			//If we are running as the editor, then we to check to see if an existing build already exists and use that instead
			if (!System.IO.Directory.Exists($"{Voltstro.UnityBuilder.Build.GameBuilder.GetBuildDirectory()}Team-Capture-Quick/"))
			{
				Debug.LogError("There is no pre-existing build of Team-Capture! Build the game using VoltBuild.");
				return;
			}
#endif

			//Make sure the game name isn't white space or null
			if (string.IsNullOrWhiteSpace(gameNameText.text))
			{
				Logger.Error("Game name input is white space or null!");
				gameNameImage.color = errorColor;
				return;
			}

			//Make sure the max players input is actually a number
			if (int.TryParse(maxPlayersText.text, out int result))
			{
				//Make sure max players is greater then 1
				if (result <= 1)
				{
					Logger.Error("Max players must be greater then one!");
					maxPlayersImage.color = errorColor;
					return;
				}

				maxPlayers = result;
			}
			else //Display an error if is not a number
			{
				Logger.Error("Max players input isn't just an int!");
				maxPlayersImage.color = errorColor;
				return;
			}

			if (netManager.isNetworkActive)
			{
				StartCoroutine(QuitExistingGame(CreateServerProcess));
				return;
			}

			CreateServerProcess();
		}

		private void CreateServerProcess()
		{
			//Now start the server
			Process newTcServer = new Process
			{
				StartInfo = new ProcessStartInfo
				{
#if UNITY_EDITOR
					FileName = $"{Voltstro.UnityBuilder.Build.GameBuilder.GetBuildDirectory()}Team-Capture-Quick/Team-Capture.exe",
#elif UNITY_STANDALONE_WIN
					FileName = "Team-Capture.exe",
#else
					FileName = "Team-Capture",
#endif
					Arguments = $"-batchmode -nographics -gamename \"{gameNameText.text}\" -scene {onlineTCScenes[mapsDropdown.value].SceneFileName} -maxplayers {maxPlayers}"
				}
			};
			newTcServer.Start();

			StartCoroutine(WaitAndConnectToGame());
		}

		private IEnumerator QuitExistingGame(Action doLast)
		{
			netManager.StopHost();

			yield return new WaitForSeconds(0.1f);

			doLast();
		}

		private IEnumerator WaitAndConnectToGame()
		{
			yield return new WaitForSeconds(1.0f);

			netManager.StartClient();
		}

		public void ResetGameNameTextColor()
		{
			if (gameNameImage.color == errorColor)
				gameNameImage.color = gameNameImageColor;
		}

		public void ResetMaxPlayersTextColor()
		{
			if (maxPlayersImage.color == errorColor)
				maxPlayersImage.color = maxPlayersImageColor;
		}
	}
}