﻿using System.Collections.Generic;
using System.Linq;
using Settings;
using TMPro;
using UI.Elements.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Panels
{
	public class OptionsPanel : MainMenuPanelBase
	{
		private readonly List<GameObject> settingPanels = new List<GameObject>();

		[SerializeField] private Transform panelsLocation;
		[SerializeField] private Transform buttonLocation;

		[SerializeField] private GameObject panelPrefab;
		[SerializeField] private GameObject settingsTitlePrefab;
		[SerializeField] private GameObject settingsButtonPrefab;

		public void OpenPanel(string panelName)
		{
			foreach (GameObject panel in settingPanels)
			{
				panel.SetActive(false);
			}

			GetMenuPanel(panelName).SetActive(true);
		}

		private GameObject GetMenuPanel(string panelName)
		{
			IEnumerable<GameObject> result = from a in settingPanels
				where a.name == panelName
				select a;

			return result.FirstOrDefault();
		}

		public GameObject AddPanel(Menu menu)
		{
			//The panel it self
			GameObject panel = Instantiate(panelPrefab, panelsLocation, false);
			panel.name = menu.Name;
			AddTitleToPanel(panel, menu.Name);

			//Button
			Button button = Instantiate(settingsButtonPrefab, buttonLocation, false).GetComponent<Button>();
			button.onClick.AddListener((delegate { OpenPanel(menu.Name); }));

			settingPanels.Add(panel);

			return panel;
		}

		public GameObject AddTitleToPanel(GameObject panel, string title)
		{
			GameObject titleObject = Instantiate(settingsTitlePrefab, panel.transform, false);
			titleObject.GetComponent<TextMeshProUGUI>().text = title;
			return titleObject;
		}

		public void SaveSettings()
		{
			GameSettings.Save();
		}
	}
}