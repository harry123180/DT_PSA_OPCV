using System.Globalization;
using UnityEngine;

namespace OC.UI.Console.Commands
{
	public static class PlayerPrefsCommands
	{
		[ConsoleMethod( "prefs.int", "Returns the value of an Integer PlayerPrefs field" ), UnityEngine.Scripting.Preserve]
		public static string PlayerPrefsGetInt( string key )
		{
			return !PlayerPrefs.HasKey( key ) ? "Key Not Found" : PlayerPrefs.GetInt( key ).ToString();
		}

		[ConsoleMethod( "prefs.int", "Sets the value of an Integer PlayerPrefs field" ), UnityEngine.Scripting.Preserve]
		public static void PlayerPrefsSetInt( string key, int value )
		{
			PlayerPrefs.SetInt( key, value );
		}

		[ConsoleMethod( "prefs.float", "Returns the value of a Float PlayerPrefs field" ), UnityEngine.Scripting.Preserve]
		public static string PlayerPrefsGetFloat( string key )
		{
			return !PlayerPrefs.HasKey( key ) ? "Key Not Found" : PlayerPrefs.GetFloat( key ).ToString(CultureInfo.InvariantCulture);
		}

		[ConsoleMethod( "prefs.float", "Sets the value of a Float PlayerPrefs field" ), UnityEngine.Scripting.Preserve]
		public static void PlayerPrefsSetFloat( string key, float value )
		{
			PlayerPrefs.SetFloat( key, value );
		}

		[ConsoleMethod( "prefs.string", "Returns the value of a String PlayerPrefs field" ), UnityEngine.Scripting.Preserve]
		public static string PlayerPrefsGetString( string key )
		{
			return !PlayerPrefs.HasKey( key ) ? "Key Not Found" : PlayerPrefs.GetString( key );
		}

		[ConsoleMethod( "prefs.string", "Sets the value of a String PlayerPrefs field" ), UnityEngine.Scripting.Preserve]
		public static void PlayerPrefsSetString( string key, string value )
		{
			PlayerPrefs.SetString( key, value );
		}

		[ConsoleMethod( "prefs.delete", "Deletes a PlayerPrefs field" ), UnityEngine.Scripting.Preserve]
		public static void PlayerPrefsDelete( string key )
		{
			PlayerPrefs.DeleteKey( key );
		}

		[ConsoleMethod( "prefs.clear", "Deletes all PlayerPrefs fields" ), UnityEngine.Scripting.Preserve]
		public static void PlayerPrefsClear()
		{
			PlayerPrefs.DeleteAll();
		}
	}
}