using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Extensibility;
using LazyCat.VSTools.AttachToPlugin;
using Microsoft.VisualStudio.CommandBars;

using UserCommand = LazyCat.VSTools.AttachToPlugin.Command;
using VsCommand = EnvDTE.Command;

namespace LazyCat.VSTools
{
	public class AttachTo : IDTExtensibility2, IDTCommandTarget
	{
		private DTE2 _applicationObject;
		private AddIn _addInInstance;

		private readonly ISettingsProvider _settingsProvider;

		private readonly IDictionary<UserCommand, VsCommand> _commandMapping = new Dictionary<UserCommand, VsCommand>();

		public AttachTo()
		{
			_settingsProvider = new FileSettingsProvider("settings.xml");
		}

		public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
		{
			_applicationObject = (DTE2)application;
			_addInInstance = (AddIn)addInInst;

			var settings = _settingsProvider.Get();

			InitUserCommands(settings);

			RefreshVsCommands(settings.DisplayInToolsMenu);
		}

		private void RefreshVsCommands(bool displayInToolsMenu)
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

				var toDelete = new List<VsCommand>();

				foreach (VsCommand vsCommand in vsCommands)
				{
					if (!vsCommand.Name.StartsWith(_addInInstance.ProgID) || vsCommand.Name.Substring(_addInInstance.ProgID.Length + 1).Contains("."))
						continue;

					var userCommand = FindCommandByName(vsCommand.Name);

					if (userCommand != null)
						_commandMapping[userCommand] = vsCommand;
					else
						toDelete.Add(vsCommand);
				}

				foreach (var vsCommand in toDelete)
					vsCommand.Delete();
				
				var userCommands = new List<UserCommand>(_commandMapping.Keys);

				foreach (var command in userCommands)
				{
					if (_commandMapping[command] != null)
					{
						continue;
					}

					var vsCommand = vsCommands.AddNamedCommand2(_addInInstance, command.Name, command.DisplayText, command.Description, true, null,
					                                                                             ref contextGuids,
					                                                                             (int) vsCommandStatus.vsCommandStatusSupported +
					                                                                             (int) vsCommandStatus.vsCommandStatusEnabled,
					                                                                             (int) vsCommandStyle.vsCommandStylePictAndText,
					                                                                             vsCommandControlType.vsCommandControlTypeButton);

					_commandMapping[command] = vsCommand;

					if (displayInToolsMenu)
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
			if (disconnectMode != ext_DisconnectMode.ext_dm_UserClosed)
				return;
			
			foreach (var vsCommand in _commandMapping.Values)
				vsCommand.Delete();

			_commandMapping.Clear();
		}

		public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut,
		                 ref bool handled)
		{
			handled = false;

			if (executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
			{
				var command = _commandMapping.Keys.FirstOrDefault(c => string.Equals(c.FullName, commandName));

				if (command != null)
				{
					handled = true;
					command.Run();
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
			{
				if (FindCommandByName(commandName) != null)
					status = vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;}
		}

		private UserCommand FindCommandByName(string name)
		{
			return _commandMapping.Keys.FirstOrDefault(c => string.Equals(c.FullName, name));
		}

		private void InitUserCommands(Settings settings)
		{
			foreach (var process in settings.Processes)
			{
				_commandMapping.Add(GetCommand(process), null);
			}
		}

		private UserCommand GetCommand(ProcessData process)
		{
			if (process.AttachToIIS)
				return new IISCommand(process, _applicationObject, _addInInstance);

			return new UserCommand(process, _applicationObject, _addInInstance);
		}
	}
}