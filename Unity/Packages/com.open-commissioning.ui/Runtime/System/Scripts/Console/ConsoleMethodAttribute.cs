using System;

namespace OC.UI.Console
{
	[AttributeUsage( AttributeTargets.Method, Inherited = false, AllowMultiple = true )]
	public class ConsoleMethodAttribute : Attribute
	{
		public string Command => _command;
		public string Description => _description;
		public string[] ParameterNames => _parameterNames;

		private readonly string _command;
		private readonly string _description;
		private readonly string[] _parameterNames;

		public ConsoleMethodAttribute( string command, string description, params string[] parameterNames)
		{
			_command = command;
			_description = description;
			this._parameterNames = parameterNames;
		}
	}
}