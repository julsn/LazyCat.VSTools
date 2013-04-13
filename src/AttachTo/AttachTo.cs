using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using EnvDTE;
using EnvDTE80;
using Extensibility;
using LazyCat.VSTools.AttachToPlugin;
using Microsoft.VisualStudio.CommandBars;

using Command = LazyCat.VSTools.AttachToPlugin.Command;
using VsCommand = EnvDTE.Command;

namespace LazyCat.VSTools
{
	public class AttachTo : IDTExtensibility2, IDTCommandTarget
	{
		private DTE2 _applicationObject;
		private AddIn _addInInstance;
		private readonly IList<VsCommand> _vsCommands = new List<VsCommand>();

		private readonly ISettingsProvider _settingsProvider;
		private readonly IDictionary<string, Command> _commands = new Dictionary<string, Command>();

		public AttachTo()
		{
			_settingsProvider = new FileSettingsProvider("settings.xml");
		}

		public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
		{
			_applicationObject = (DTE2)application;
			_addInInstance = (AddIn)addInInst;

			var settings = _settingsProvider.Get();

			CreateCommands(settings);

			CreateVsCommands(settings);
		}

		private void CreateVsCommands(Settings settings)
		{
			const string menubar = "MenuBar";
			const string toolsMenuName = "Tools";

			var menuBarCommandBar = ((CommandBars) _applicationObject.CommandBars)[menubar];
			var toolsControl = menuBarCommandBar.Controls[toolsMenuName];

			var toolsPopup = (CommandBarPopup) toolsControl;

			var vsCommands = (Commands2) _applicationObject.Commands;
			var contextGuids = new object[] {};

			try
			{
				var position = 1;

				foreach (var command in _commands.Values)
				{
					var vsCommand = vsCommands.AddNamedCommand2(_addInInstance, command.Name, command.DisplayText, command.Description, true, 59,
					                                            ref contextGuids,
					                                            (int) vsCommandStatus.vsCommandStatusSupported +
					                                            (int) vsCommandStatus.vsCommandStatusEnabled,
					                                            (int) vsCommandStyle.vsCommandStylePictAndText,
					                                            vsCommandControlType.vsCommandControlTypeButton);

					_vsCommands.Add(vsCommand);

					if (settings.DisplayInToolsMenu)
						vsCommand.AddControl(toolsPopup.CommandBar, position++);
				}
			}
			catch (ArgumentException)
			{
				//If we are here, then the exception is probably because a command with that name
				//  already exists. If so there is no need to recreate the command and we can 
				//  safely ignore the exception.
			}
		}

		public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
		{
			foreach (var vsCommand in _vsCommands)
				vsCommand.Delete();

			_vsCommands.Clear();
		}

		public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut,
		                 ref bool handled)
		{
			handled = false;

			if (executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
			{
				if (_commands.ContainsKey(commandName))
				{
					handled = true;

					_commands[commandName].Run();
				}
			}
		}

		public void OnAddInsUpdate(ref Array custom)
		{
		}

		public void OnStartupComplete(ref Array custom)
		{
		}

		public void OnBeginShutdown(ref Array custom)
		{
		}

		public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status,
		                        ref object commandText)
		{
			if (neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
				if (_commands.ContainsKey(commandName))
					status = vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
		}

		private void CreateCommands(Settings settings)
		{
			foreach (var process in settings.Processes)
			{
				var command = GetCommand(process);
				var keyName = GetKeyName(command);

				if (!_commands.ContainsKey(keyName))
					_commands.Add(keyName, command);
			}
		}

		private string GetKeyName(Command command)
		{
			return string.Format("{0}.{1}", GetType().FullName, command.Name);
		}

		private Command GetCommand(ProcessData process)
		{
			if (process.AttachToIIS)
				return new IISCommand(process, _applicationObject);

			return new Command(process, _applicationObject);
		}
	}
}