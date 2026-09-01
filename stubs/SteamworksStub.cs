using System;
using System.Reflection;

[assembly: AssemblyVersion("7.0.0.0")]

namespace Steamworks
{
	public class SteamAPI
	{
		public static bool Init() { return false; }
		public static void RunCallbacks() { }
		public static void Shutdown() { }
	}

	public class SteamUserStats
	{
		public static bool RequestCurrentStats() { return false; }
		public static bool GetAchievement(string pchName, out bool pbAchieved)
		{
			pbAchieved = false;
			return false;
		}
		public static bool SetAchievement(string pchName) { return false; }
		public static bool StoreStats() { return false; }
		public static bool ResetAllStats(bool bAchievementsToo) { return false; }
		public static bool SetStat(string pchName, int nData) { return false; }
		public static bool GetStat(string pchName, out int pData)
		{
			pData = 0;
			return false;
		}
	}

	public class SteamRemoteStorage
	{
		public static bool FileWrite(string pchFile, byte[] pvData, int cubData) { return false; }
		public static int FileRead(string pchFile, byte[] pvData, int cubDataToRead) { return 0; }
		public static bool FileExists(string pchFile) { return false; }
		public static bool FileDelete(string pchFile) { return false; }
		public static int GetFileSize(string pchFile) { return 0; }
		public static bool IsCloudEnabledForAccount() { return false; }
		public static bool IsCloudEnabledForApp() { return false; }
	}

	public class Packsize
	{
		public static bool Test() { return true; }
	}

	public class DllCheck
	{
		public static bool Test() { return true; }
	}

	public class Callback<T>
	{
		public delegate void DispatchDelegate(T param);
		public static Callback<T> Create(DispatchDelegate func) { return new Callback<T>(); }
	}

	public struct GameOverlayActivated_t
	{
		public byte m_bActive;
	}
}
