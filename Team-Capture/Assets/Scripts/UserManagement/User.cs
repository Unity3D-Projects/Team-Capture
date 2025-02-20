﻿// Team-Capture
// Copyright (C) 2019-2021 Voltstro-Studios
// 
// This project is governed by the AGPLv3 License.
// For more details see the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Team_Capture.UserManagement
{
    public static class User
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Init()
        {
            users = new SortedList<UserProvider, IUser>(new UserProviderComparer());
            AddUser(new OfflineUser());
        }
        
        private static SortedList<UserProvider, IUser> users;

        public static void AddUser(IUser user)
        {
            users.Add(user.UserProvider, user);
        }
        
        public static IUser GetActiveUser()
        {
            return users.ElementAt(users.Count).Value;
        }

        internal static IUser[] GetUsers()
        {
            return users.Select(x => x.Value).ToArray();
        }
        
        private class UserProviderComparer : IComparer<UserProvider>
        {
            public int Compare(UserProvider x, UserProvider y)
            {
                return x.CompareTo(y);
            }
        }
    }
}