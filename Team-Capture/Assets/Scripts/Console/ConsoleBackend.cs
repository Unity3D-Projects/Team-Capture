﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Console.TypeReader;
using Core;
using Delegates;
using UnityEngine;
using Logger = Core.Logging.Logger;

namespace Console
{
	public static class ConsoleBackend
	{
		private static readonly Dictionary<string, ConsoleCommand> Commands = new Dictionary<string, ConsoleCommand>();

		private const int HistoryCount = 50;
		private static readonly string[] History = new string[HistoryCount];
		private static int historyNextIndex;
		private static int historyIndex;

		private const BindingFlags BindingFlags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

		private static Dictionary<Type, ITypeReader> typeReaders;

		public static void InitConsoleBackend()
		{
			configFilesLocation = Game.GetGameExecutePath() + "/Cfg/";
			typeReaders = new Dictionary<Type, ITypeReader>
			{
				[typeof(string)] = new TypeReader.StringReader(),
				[typeof(bool)] = new BoolReader(),
				[typeof(int)] = new IntReader(),
				[typeof(float)] = new FloatReader()
			};

			RegisterCommands();
			RegisterConVars();
		}

		/// <summary>
		/// Finds all static methods with the <see cref="ConCommand"/> attribute attached to it
		/// and adds it to the list of commands
		/// </summary>
		private static void RegisterCommands()
		{
			foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
			foreach (MethodInfo method in type.GetMethods(BindingFlags))
			{
				if (!(Attribute.GetCustomAttribute(method, typeof(ConCommand)) is ConCommand attribute))
					continue;

				if(attribute.GraphicsModeOnly && Game.IsHeadless)
					continue;

				MethodDelegate methodDelegate =
					(MethodDelegate) Delegate.CreateDelegate(typeof(MethodDelegate), method);

				AddCommand(new ConsoleCommand
				{
					CommandSummary = attribute.Summary,
					RunPermission = attribute.RunPermission,
					MinArgs = attribute.MinArguments,
					MaxArgs = attribute.MaxArguments,
					CommandMethod = methodDelegate
				}, attribute.Name);
			}
		}

		private static void RegisterConVars()
		{
			foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
			{
				foreach (FieldInfo fieldInfo in type.GetFields(BindingFlags))
				{
					if(!(Attribute.GetCustomAttribute(fieldInfo, typeof(ConVar)) is ConVar attribute))
						continue;

					AddCommand(new ConsoleCommand
					{
						CommandSummary = attribute.Summary,
						RunPermission = CommandRunPermission.Both,
						MinArgs = 1,
						MaxArgs = 1,
						CommandMethod = args =>
						{
							if (typeReaders.TryGetValue(fieldInfo.FieldType, out ITypeReader reader))
							{
								fieldInfo.SetValue(fieldInfo, reader.ReadType(args[0]));
								Logger.Info("'{@Attribute}' was set to '{@Value}'", attribute.Name, reader.ReadType(args[0]));
							}
						}
					}, attribute.Name);
				}
			}
		}

		/// <summary>
		/// Adds a command to the list of commands
		/// </summary>
		/// <param name="conCommand"></param>
		/// <param name="commandName"></param>
		private static void AddCommand(ConsoleCommand conCommand, string commandName)
		{
			//Make sure the command doesn't already exist
			if (Commands.ContainsKey(commandName))
			{
				Logger.Error("The command {@CommandName} already exists in the command list!", commandName);
				return;
			}

			Logger.Debug("Added command {@CommandName}", commandName);

			//Add the command
			Commands.Add(commandName, conCommand);
		}

		/// <summary>
		/// Get a dictionary containing the command name, and it associated <see cref="ConsoleCommand"/>
		/// </summary>
		/// <returns></returns>
		public static Dictionary<string, ConsoleCommand> GetAllCommands()
		{
			return Commands;
		}

		/// <summary>
		/// Executes a command
		/// </summary>
		/// <param name="command"></param>
		public static void ExecuteCommand(string command)
		{
			List<string> tokens = Tokenize(command);
			if (tokens.Count < 1)
				return;

			if (Commands.TryGetValue(tokens[0].ToLower(), out ConsoleCommand conCommand))
			{
				string[] arguments = tokens.GetRange(1, tokens.Count - 1).ToArray();

				if (conCommand.RunPermission == CommandRunPermission.ServerOnly)
				{
					if (Mirror.NetworkManager.singleton == null ||
					    Mirror.NetworkManager.singleton.mode != Mirror.NetworkManagerMode.ServerOnly)
					{
						Logger.Error("The command {@Command} can only be run in server mode!", tokens[0].ToLower());
						return;
					}
				}
				else if (conCommand.RunPermission == CommandRunPermission.ClientOnly)
				{
					if (Mirror.NetworkManager.singleton != null)
					{
						if (Mirror.NetworkManager.singleton.mode == Mirror.NetworkManagerMode.ServerOnly)
						{
							Logger.Error("The command {@Command} can only be run in client/offline mode!", tokens[0].ToLower());
							return;
						}
					}
				}

				//Command min arguments
				if (conCommand.MinArgs != 0)
					if (arguments.Length <= conCommand.MinArgs - 1)
					{
						Logger.Error("Invalid arguments: More arguments are required!");
						return;
					}

				//Command max arguments
				if (conCommand.MaxArgs != 0)
					if (arguments.Length > conCommand.MaxArgs)
					{
						Logger.Error("Invalid arguments: Less arguments are required!");
						return;
					}

				//Invoke the method
				try
				{
					conCommand.CommandMethod.Invoke(arguments);
					History[historyNextIndex % HistoryCount] = command;
					historyNextIndex++;
					historyIndex = historyNextIndex;
				}
				catch (Exception ex)
				{
					Logger.Error("An error occurred! {@Exception}", ex);
				}

				return;
			}

			Logger.Error($"Unknown command: `{tokens[0]}`.");
		}

		/// <summary>
		/// Tries to auto complete a prefix
		/// </summary>
		/// <param name="prefix"></param>
		/// <returns></returns>
		public static string AutoComplete(string prefix)
		{
			List<string> possibleMatches = new List<string>();

			foreach (KeyValuePair<string, ConsoleCommand> command in Commands)
			{
				string name = command.Key;
				if(!name.StartsWith(prefix, true, null))
					continue;
				possibleMatches.Add(name);
			}

			if (possibleMatches.Count == 0)
				return prefix;

			// Look for longest common prefix
			int lcp = possibleMatches[0].Length;
			for (int i = 0; i < possibleMatches.Count - 1; i++)
			{
				lcp = Mathf.Min(lcp, CommonPrefix(possibleMatches[i], possibleMatches[i + 1]));
			}

			prefix += possibleMatches[0].Substring(prefix.Length, lcp - prefix.Length);
			if (possibleMatches.Count > 1)
			{
				// write list of possible completions
				foreach (string t in possibleMatches)
					Logger.Info(t);
			}
			else
			{
				prefix += " ";
			}

			return prefix;
		}

		public static string HistoryUp(string current)
		{
			if (historyIndex == 0 || historyNextIndex - historyIndex >= HistoryCount - 1)
				return "";

			if (historyIndex == historyNextIndex)
			{
				History[historyIndex % HistoryCount] = current;
			}

			historyIndex--;

			return History[historyIndex % HistoryCount];
		}

		public static string HistoryDown()
		{
			if (historyIndex == historyNextIndex)
				return "";

			historyIndex++;

			return History[historyIndex % HistoryCount];
		}

		/// <summary>
		/// Returns length of largest common prefix of two strings
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		private static int CommonPrefix(string a, string b)
		{
			int minl = Mathf.Min(a.Length, b.Length);
			for (int i = 1; i <= minl; i++)
			{
				if (!a.StartsWith(b.Substring(0, i), true, null))
					return i - 1;
			}
			return minl;
		}

		#region Argument Parsing

		private static List<string> Tokenize(string input)
		{
			int pos = 0;
			List<string> res = new List<string>();
			int c = 0;
			while (pos < input.Length && c++ < 10000)
			{
				SkipWhite(input, ref pos);
				if (pos == input.Length)
					break;

				if (input[pos] == '"' && (pos == 0 || input[pos - 1] != '\\'))
					res.Add(ParseQuoted(input, ref pos));
				else
					res.Add(Parse(input, ref pos));
			}

			return res;
		}

		private static void SkipWhite(string input, ref int pos)
		{
			while (pos < input.Length && " \t".IndexOf(input[pos]) > -1)
			{
				pos++;
			}
		}

		private static string ParseQuoted(string input, ref int pos)
		{
			pos++;
			int startPos = pos;
			while (pos < input.Length)
			{
				if (input[pos] == '"' && input[pos - 1] != '\\')
				{
					pos++;
					return input.Substring(startPos, pos - startPos - 1);
				}

				pos++;
			}

			return input.Substring(startPos);
		}

		private static string Parse(string input, ref int pos)
		{
			int startPos = pos;
			while (pos < input.Length)
			{
				if (" \t".IndexOf(input[pos]) > -1) return input.Substring(startPos, pos - startPos);
				pos++;
			}

			return input.Substring(startPos);
		}

		#endregion

		#region File Executuion

		private static string configFilesLocation;

		[ConCommand("exec", "Executes a file", CommandRunPermission.Both, 1, 1)]
		public static void ExecuteFileCommand(string[] args)
		{
			if (args.Length != 1)
			{
				Logger.Error("Invalid arguments!");
				return;
			}

			string fileName = args[0] + ".cfg";
			if (!File.Exists(configFilesLocation + fileName))
			{
				Logger.Error($"`{fileName}` doesn't exist! Not executing.");
				return;
			}

			string[] lines = File.ReadAllLines(configFilesLocation + fileName);
			foreach (string line in lines)
			{
				if (line.StartsWith("//")) continue;

				ExecuteCommand(line);
			}
		}

		#endregion
	}
}