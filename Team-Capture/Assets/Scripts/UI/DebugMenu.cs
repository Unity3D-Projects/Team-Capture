﻿// Team-Capture
// Copyright (C) 2019-2021 Voltstro-Studios
// 
// This project is governed by the AGPLv3 License.
// For more details see the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using Team_Capture.Console;
using Team_Capture.Helper;
using Team_Capture.Input;
using Team_Capture.Player.Movement;
using Unity.Profiling;
using UnityEngine;

namespace Team_Capture.UI
{
	/// <summary>
	///     A UI used for debugging purposes
	/// </summary>
	internal class DebugMenu : SingletonMonoBehaviour<DebugMenu>
	{
		/// <summary>
		///     Reads input
		/// </summary>
		public InputReader inputReader;

		/// <summary>
		///		How often to refresh the fps counter
		/// </summary>
		public float refreshRate = 1f;

		/// <summary>
		///     Is the debug menu open?
		/// </summary>
		[ConVar("cl_debugmenu", "Shows the debug menu", true)]
		public static bool DebugMenuOpen;

		private const string Spacer = "===================";

		private ProfilerRecorder mainThreadRecorder;
		private ProfilerRecorder totalMemoryUsedRecorder;
		private ProfilerRecorder gcReservedMemoryRecorder;
		private ProfilerRecorder totalDrawCallsRecorder;

		private float timer;

		private double frameTime;
		private int fps;
		private int totalMemoryUsed;
		private int gcReserved;
		private int drawCalls;
		
		private int inMessageCountFrame;
		private int outMessageCountFrame;
		private int inMessageBytesFrame;
		private int outMessageBytesFrame;
		
		private int inMessageCount;
		private int outMessageCount;
		private int inMessageBytes;
		private int outMessageBytes;
		
		private void OnGUI()
		{
			if (!DebugMenuOpen)
				return;

			//Setup GUI
			GUI.skin.label.fontSize = 20;

			//Setup our initial yOffset;
			float yOffset = 10;
			if (NetworkManager.singleton != null && NetworkManager.singleton.mode == NetworkManagerMode.ClientOnly)
				if (PlayerMovementManager.ShowPos)
					yOffset = 120;

			GUI.Box(new Rect(8, yOffset, 475, 420), "");
			GUI.Label(new Rect(10, yOffset, 1000, 40), version);
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), Spacer);

			GUI.Label(new Rect(10, yOffset += 30, 1000, 40), $"Frame Time: {frameTime:F1}ms");
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), $"FPS: {fps}");
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), $"Total Memory: {totalMemoryUsed} MB");
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), $"GC Reserved: {gcReserved} MB");
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), $"Draw Calls: {drawCalls}");

			GUI.Label(new Rect(10, yOffset += 30, 1000, 40), "Device Info");
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), Spacer);
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), cpu);
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), gpu);
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), ram);
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), renderingApi);
			GUI.Label(new Rect(10, yOffset += 30, 1000, 40), "Network");
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), Spacer);
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), ipAddress);
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), $"Status: {GetNetworkingStatus()}");
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), $"In Messages {inMessageCountFrame} ({inMessageBytesFrame / 1000} kb)");
			GUI.Label(new Rect(10, yOffset += 20, 1000, 40), $"Out Message {outMessageCountFrame} ({outMessageBytesFrame / 1000} kb)");
		}

		private void Update()
		{
			if (!(Time.unscaledTime > timer)) return;

			frameTime = GetRecorderFrameTimeAverage(mainThreadRecorder) * 1e-6f;
			fps = (int) (1f / Time.unscaledDeltaTime);

			totalMemoryUsed = (int) totalMemoryUsedRecorder.LastValue / (1024 * 1024);
			gcReserved = (int) gcReservedMemoryRecorder.LastValue / (1024 * 1024);
			drawCalls = (int) totalDrawCallsRecorder.LastValue;

			inMessageCountFrame = inMessageCount;
			outMessageCountFrame = outMessageCount;
			inMessageBytesFrame = inMessageBytes;
			outMessageBytesFrame = outMessageBytes;

			inMessageCount = 0;
			inMessageBytes = 0;
			outMessageCount = 0;
			outMessageBytes = 0;
			
			timer = Time.unscaledTime + refreshRate;
		}

		private void OnEnable()
		{
			timer = Time.unscaledTime;

			mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
			totalMemoryUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
			gcReservedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
			totalDrawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");

			NetworkDiagnostics.InMessageEvent += AddInMessage;
			NetworkDiagnostics.OutMessageEvent += AddOutMessage;
		}

		private void OnDisable()
		{
			mainThreadRecorder.Dispose();
			totalMemoryUsedRecorder.Dispose();
			gcReservedMemoryRecorder.Dispose();
			totalDrawCallsRecorder.Dispose();
			
			NetworkDiagnostics.InMessageEvent -= AddInMessage;
			NetworkDiagnostics.OutMessageEvent -= AddOutMessage;
		}

		protected override void SingletonAwakened()
		{
		}

		protected override void SingletonStarted()
		{
			version = $"Team-Capture {Application.version}";
			cpu = $"CPU: {SystemInfo.processorType}";
			gpu = $"GPU: {SystemInfo.graphicsDeviceName}";
			ram = $"RAM: {SystemInfo.systemMemorySize / 1000} GB";
			renderingApi = $"Rendering API: {SystemInfo.graphicsDeviceType}";
			ipAddress = $"IP: {NetHelper.LocalIpAddress()}";

			inputReader.DebugMenuToggle += () => DebugMenuOpen = !DebugMenuOpen;
			inputReader.EnableDebugMenuInput();
		}

		protected override void SingletonDestroyed()
		{
		}

		private string GetNetworkingStatus()
		{
			if (NetworkManager.singleton == null)
				return "Networking not active!";

			switch (NetworkManager.singleton.mode)
			{
				case NetworkManagerMode.Offline:
					return "Not Connected";
				case NetworkManagerMode.ServerOnly:
					return "Server active";
				case NetworkManagerMode.ClientOnly:
					return $"Connected ({NetworkManager.singleton.networkAddress})";
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private double GetRecorderFrameTimeAverage(ProfilerRecorder recorder)
		{
			int samplesCount = recorder.Capacity;
			if (samplesCount == 0)
				return 0;

			List<ProfilerRecorderSample> samples = new List<ProfilerRecorderSample>(samplesCount);
			recorder.CopyTo(samples);
			double r = samples.Aggregate<ProfilerRecorderSample, double>(0, (current, sample) => current + sample.Value);
			r /= samplesCount;

			return r;
		}

		private void AddInMessage(NetworkDiagnostics.MessageInfo info)
		{
			inMessageCount++;
			inMessageBytes += info.bytes;
		}

		private void AddOutMessage(NetworkDiagnostics.MessageInfo info)
		{
			outMessageCount++;
			outMessageBytes += info.bytes;
		}

		#region Info

		private string version;
		private string cpu;
		private string gpu;
		private string ram;
		private string renderingApi;
		private string ipAddress;

		#endregion
	}
}