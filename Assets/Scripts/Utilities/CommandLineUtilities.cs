using System;
using System.Collections.Generic;

public class CommandLineUtilities
{
	/// <summary>
	/// Signal if the executable was started in Headless mode by using the "-batchmode -nographics" command-line arguments
	/// <see cref="https://docs.unity3d.com/Manual/PlayerCommandLineArguments.html"/>
	/// </summary>
	/// <returns>True if in "Headless Mode", false otherwise</returns>
	public static bool IsHeadlessMode()
	{
		return Environment.CommandLine.Contains("-batchmode") && Environment.CommandLine.Contains("-nographics");
	}

	/// <summary>
	/// Get a tuple list of arguments starting with a specific prefix.
	/// </summary>
	/// <param name="prefix">Prefix tested on each argument</param>
	/// <returns>List of tuples with argument name and argument value</returns>
	public static List<(string, string)> GetArgumentList(string prefix)
	{
		List<(string, string)> output = new();

		string[] args = Environment.GetCommandLineArgs();

		for (int i = 0; i < args.Length; i++)
		{
			if (args[i].Trim().StartsWith(prefix) && args.Length > i + 1)
			{
				string key = args[i].Trim().Replace(prefix, "");
				string value = args[i + 1];

				output.Add((key, value));
			}
		}

		return output;
	}

	/// <summary>
	/// Get the value of a specific command-line argument passed when starting the executable
	/// </summary>
	/// <example>
	/// Starting the binary with: "./my-game.exe -map street -type hide-and-seek"
	/// and calling TryGetArg() with the argNames being {"-map", "-m"} will return the string "street"
	/// </example>
	/// <param name="keys">List of possible keys for the argument</param>
	/// <returns>The string value of the argument if the at least 1 key was found, null otherwise</returns>
	public static bool TryGetArg(out string argValue, params string[] argNames)
	{
		string[] args = Environment.GetCommandLineArgs();
		argValue = null;

		for (int i = 0; i < args.Length; i++)
		{
			foreach (string argName in argNames)
			{
				if (args[i].Equals(argName) && args.Length > i + 1)
				{
					argValue = args[i + 1];
					return true;
				}
			}
		}

		return false;
	}
}