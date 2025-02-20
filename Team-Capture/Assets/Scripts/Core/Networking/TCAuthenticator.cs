// Team-Capture
// Copyright (C) 2019-2021 Voltstro-Studios
// 
// This project is governed by the AGPLv3 License.
// For more details see the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Mirror;
using Team_Capture.Console;
using Team_Capture.UserManagement;
using UnityCommandLineParser;
using UnityEngine;
using Logger = Team_Capture.Logging.Logger;

namespace Team_Capture.Core.Networking
{
	/// <summary>
	///		<see cref="NetworkAuthenticator"/> for Team-Capture
	/// </summary>
	internal class TCAuthenticator : NetworkAuthenticator
	{
		[ConVar("sv_auth_method", "What account system to use to check clients")]
		[CommandLineArgument("auth-method", "What account system to use to check clients")]
		public static UserProvider ServerAuthMethod = UserProvider.Steam;

		[ConVar("sv_auth_clean_names", "Will trim whitespace at the start and end of account names")]
		public static bool CleanAccountNames = true;
		
		#region Server

		private Dictionary<int, IUser> authAccounts;

		/// <summary>
		///		Gets an account from their connection ID
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public IUser GetAccount(int id)
		{
			IUser account = authAccounts[id];
			if (account == null)
				throw new ArgumentException();

			return account;
		}

		public override void OnStartServer()
		{
			ServerAuthMethod = UserProvider.Offline;
			authAccounts = new Dictionary<int, IUser>();
			NetworkServer.RegisterHandler<JoinRequestMessage>(OnRequestJoin, false);
		}

		public override void OnStopServer()
		{
			NetworkServer.UnregisterHandler<JoinRequestMessage>();
		}

		public override void OnServerAuthenticate(NetworkConnection conn)
		{
		}

		private void OnRequestJoin(NetworkConnection conn, JoinRequestMessage msg)
		{
			//Check versions
			if (msg.ApplicationVersion != Application.version)
			{
				SendRequestResponseMessage(conn, HttpCode.PreconditionFailed, "Server and client versions mismatch!");
				Logger.Warn("Client {Id} had mismatched versions with the server! Rejecting connection.", conn.connectionId);

				RefuseClientConnection(conn);
				return;
			}

			//Make sure they at least provided an account
			if (msg.UserAccounts.Length == 0)
			{
				SendRequestResponseMessage(conn, HttpCode.Unauthorized, "No accounts provided!");
				Logger.Warn("Client {Id} sent no user accounts. Rejecting connection.", conn.connectionId);

				RefuseClientConnection(conn);
				return;
			}
			
			Logger.Debug("Got {UserAccountsNum} user accounts from {UserId}", msg.UserAccounts.Length, conn.connectionId);

			//Get the user account the server wants
			IUser user = msg.UserAccounts.FirstOrDefault(x => x.UserProvider == ServerAuthMethod);
			if (user == null)
			{
				SendRequestResponseMessage(conn, HttpCode.Unauthorized, "No valid user accounts sent!");
				Logger.Warn("Client {Id} sent no valid user accounts!. Rejecting connection.", conn.connectionId);

				RefuseClientConnection(conn);
				return;
			}

			try
			{
				if (!user.ServerIsClientAuthenticated())
				{
					SendRequestResponseMessage(conn, HttpCode.Unauthorized, "Failed authorization!");
					Logger.Warn("Client {Id} failed to authorize!. Rejecting connection.", conn.connectionId);

					RefuseClientConnection(conn);
					return;
				}
			}
			catch (Exception ex)
			{
				SendRequestResponseMessage(conn, HttpCode.InternalServerError, "An error occured with the server authorization!");
				Logger.Error(ex, "An error occured on the server side with authorization");

				RefuseClientConnection(conn);
				return;
			}

			authAccounts.Add(conn.connectionId, user);

			SendRequestResponseMessage(conn, HttpCode.Ok, "Ok");
			ServerAccept(conn);
			Logger.Debug("Accepted client {Id}", conn.connectionId);
		}

		private void RefuseClientConnection(NetworkConnection conn)
		{
			conn.isAuthenticated = false;
			DisconnectClientDelayed(conn).Forget();
		}

		private async UniTask DisconnectClientDelayed(NetworkConnection conn)
		{
			await Integrations.UniTask.UniTask.Delay(1000);

			ServerReject(conn);
		}

		private void SendRequestResponseMessage(NetworkConnection conn, HttpCode code, string message)
		{
			conn.Send(new JoinRequestResponseMessage
			{
				Code = code,
				Message = message
			});
		}

		#endregion

		#region Client

		public override void OnStartClient()
		{
			NetworkClient.RegisterHandler<JoinRequestResponseMessage>(OnReceivedJoinRequestResponse, false);
		}

		public override void OnStopClient()
		{
			NetworkClient.UnregisterHandler<JoinRequestResponseMessage>();
		}

		public override void OnClientAuthenticate()
		{
			IUser[] users = User.GetUsers();
			foreach (IUser user in users)
			{
				try
				{
					user.ClientStartAuthentication();
				}
				catch (Exception ex)
				{
					Logger.Error(ex, "An error occured while trying to authenticate on the client end!");
					
					ClientReject();
					return;
				}
			}
			
			NetworkClient.connection.Send(new JoinRequestMessage
			{
				ApplicationVersion = Application.version,
				UserAccounts = users
			});
		}

		public void OnClientDisconnect()
		{
			foreach (IUser user in User.GetUsers())
			{
				user.ClientStopAuthentication();
			}
		}
		
		private void OnReceivedJoinRequestResponse(JoinRequestResponseMessage msg)
		{
			//We good to connect
			if (msg.Code == HttpCode.Ok)
			{
				Logger.Info("Join request was accepted! {Message} ({Code})", msg.Message, (int)msg.Code);

				ClientAccept();
			}
			//Something fucked up
			else
			{
				Logger.Error("Failed to connect! Error: {Message} ({Code})", msg.Message, (int)msg.Code);

				ClientReject();
			}
		}

		#endregion

		#region Messages

		private struct JoinRequestMessage : NetworkMessage
		{
			public string ApplicationVersion;

			internal IUser[] UserAccounts;
		}

		private struct JoinRequestResponseMessage : NetworkMessage
		{
			public HttpCode Code;
			public string Message;
		}

		#endregion
	}
}