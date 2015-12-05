﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Model.WindowPlacer;
using VidCoder.Resources;
using VidCoder.ViewModel;

namespace VidCoder.Services.Windows
{
	public class WindowManager : ReactiveObject, IWindowManager
	{
		private static readonly Type MainViewModelType = typeof(MainViewModel);
		private const string WindowTypePrefix = "VidCoder.View.";
		private object mainViewModel;
		private Dictionary<object, Window> openWindows;

		static WindowManager()
		{
			Definitions = new List<WindowDefinition>
			{
				new WindowDefinition
				{
					ViewModelType = typeof(MainViewModel), 
					PlacementConfigKey = "MainWindowPlacement"
				},

				new WindowDefinition
				{
					ViewModelType = typeof(EncodingWindowViewModel), 
					InMenu = true,
					PlacementConfigKey = "EncodingDialogPlacement",
					IsOpenConfigKey = "EncodingWindowOpen", 
					InputGestureText = "Ctrl+N",
					MenuLabel = MainRes.EncodingSettingsMenuItem
				},

				new WindowDefinition
				{
					ViewModelType = typeof(PreviewWindowViewModel), 
					InMenu = true,
					PlacementConfigKey = "PreviewWindowPlacement",
					ManualPlacementRestore = true,
					IsOpenConfigKey = "PreviewWindowOpen", 
					InputGestureText = "Ctrl+P",
					MenuLabel = MainRes.PreviewMenuItem
				},

				new WindowDefinition
				{
					ViewModelType = typeof(PickerWindowViewModel), 
					InMenu = true,
					PlacementConfigKey = "PickerWindowPlacement",
					IsOpenConfigKey = "PickerWindowOpen", 
					InputGestureText = "Ctrl+I",
					MenuLabel = MainRes.PickerMenuItem
				},

				new WindowDefinition
				{
					ViewModelType = typeof(LogWindowViewModel), 
					InMenu = true,
					PlacementConfigKey = "LogWindowPlacement",
					IsOpenConfigKey = "LogWindowOpen", 
					InputGestureText = "Ctrl+L",
					MenuLabel = MainRes.LogMenuItem
				},

				new WindowDefinition
				{
					ViewModelType = typeof(EncodeDetailsWindowViewModel), 
					InMenu = true,
					PlacementConfigKey = "EncodeDetailsWindowPlacement",
					IsOpenConfigKey = "EncodeDetailsWindowOpen", 
					MenuLabel = MainRes.EncodeDetailsMenuItem,
					CanOpen = () => Ioc.Get<ProcessingService>().WhenAnyValue(x => x.Encoding)
				},

				new WindowDefinition
				{
					ViewModelType = typeof(SubtitleDialogViewModel),
					PlacementConfigKey = "SubtitlesDialogPlacement"
				},

				new WindowDefinition
				{
					ViewModelType = typeof(ChapterMarkersDialogViewModel),
					PlacementConfigKey = "ChapterMarkersDialogPlacement"
				},
				new WindowDefinition
				{
					ViewModelType = typeof(QueueTitlesWindowViewModel),
					PlacementConfigKey = "QueueTitlesDialogPlacement2"
				},
				new WindowDefinition
				{
					ViewModelType = typeof(AddAutoPauseProcessDialogViewModel),
					PlacementConfigKey = "AddAutoPauseProcessDialogPlacement"
				},

				new WindowDefinition
				{
					ViewModelType = typeof(OptionsDialogViewModel),
					PlacementConfigKey = "OptionsDialogPlacement"
				},
			};
		}

		public WindowManager()
		{
			this.openWindows = new Dictionary<object, Window>();
		}

		public static List<WindowDefinition> Definitions { get; private set; }

		/// <summary>
		/// Fires when a window opens.
		/// </summary>
		public event EventHandler<EventArgs<Type>> WindowOpened;

		/// <summary>
		/// Fires when a window closes.
		/// </summary>
		public event EventHandler<EventArgs<Type>> WindowClosed;

		/// <summary>
		/// Opens the viewmodel as a window.
		/// </summary>
		/// <param name="viewModel">The window's viewmodel.</param>
		/// <param name="ownerViewModel">The viewmodel of the owner window.</param>
		public void OpenWindow(object viewModel, object ownerViewModel = null)
		{
			if (viewModel.GetType() == MainViewModelType)
			{
				this.mainViewModel = viewModel;
			}
			else if (ownerViewModel == null)
			{
				ownerViewModel = this.mainViewModel;
			}

			Window windowToOpen = this.PrepareWindowForOpen(viewModel, ownerViewModel, userInitiated: true, isDialog: false);
			windowToOpen.Show();
		}

		/// <summary>
		/// Opens the viewmodel as a dialog.
		/// </summary>
		/// <param name="viewModel">The dialog's viewmodel.</param>
		/// <param name="ownerViewModel">The viewmodel of the owner window.</param>
		public void OpenDialog(object viewModel, object ownerViewModel = null)
		{
			if (ownerViewModel == null)
			{
				ownerViewModel = this.mainViewModel;
			}

			Window windowToOpen = this.PrepareWindowForOpen(viewModel, ownerViewModel, userInitiated: true, isDialog: true);
			windowToOpen.ShowDialog();
		}

		/// <summary>
		/// Opens the viewmodel type as a dialog.
		/// </summary>
		/// <typeparam name="T">The type of the viewmodel.</typeparam>
		/// <param name="ownerViewModel">The viewmodel of the owner window.</param>
		public void OpenDialog<T>(object ownerViewModel = null)
		{
			this.OpenDialog(Ioc.Get<T>(), ownerViewModel);
		}

		/// <summary>
		/// Opens all tracked windows that are open according to config.
		/// </summary>
		/// <remarks>Call at app startup.</remarks>
		public void OpenTrackedWindows()
		{
			bool windowOpened = false;

			foreach (var definition in Definitions.Where(d => d.IsOpenConfigKey != null))
			{
				bool canOpen = true;
				if (definition.CanOpen != null)
				{
					IDisposable disposable = definition.CanOpen().Subscribe(value =>
					{
						canOpen = value;
					});

					disposable.Dispose();
				}

				if (canOpen && Config.Get<bool>(definition.IsOpenConfigKey))
				{
					this.OpenWindow(Ioc.Get(definition.ViewModelType));
					windowOpened = true;
				}
			}

			if (windowOpened)
			{
				this.Focus(this.mainViewModel);
			}
		}

		/// <summary>
		/// Closes all tracked windows.
		/// </summary>
		/// <remarks>Call on app exit.</remarks>
		public void CloseTrackedWindows()
		{
			using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
			{
				foreach (var definition in Definitions.Where(d => d.IsOpenConfigKey != null))
				{
					object viewModel = this.FindOpenWindowViewModel(definition.ViewModelType);
					if (viewModel != null)
					{
						this.CloseInternal(viewModel, userInitiated: false);
					}
				}

				transaction.Commit();
			}
		}

		/// <summary>
		/// Opens or focuses the viewmodel type's window.
		/// </summary>
		/// <param name="viewModelType">The type of the window viewmodel.</param>
		/// <param name="ownerViewModel">The owner view model (main view model).</param>
		public void OpenOrFocusWindow(Type viewModelType, object ownerViewModel = null)
		{
			object viewModel = this.FindOpenWindowViewModel(viewModelType);

			if (viewModel == null)
			{
				viewModel = Ioc.Get(viewModelType);
				if (ownerViewModel == null)
				{
					ownerViewModel = this.mainViewModel;
				}

				Window window = this.PrepareWindowForOpen(viewModel, ownerViewModel, userInitiated: true, isDialog: false);
				window.Show();
			}
			else
			{
				this.Focus(viewModel);
			}
		}

		/// <summary>
		/// Finds an open window with the given viewmodel type.
		/// </summary>
		/// <typeparam name="T">The viewmodel type.</typeparam>
		/// <returns>The open window viewmodel, or null if is not open.</returns>
		public T Find<T>() where T : class
		{
			return this.FindOpenWindowViewModel(typeof(T)) as T;
		}

		/// <summary>
		/// Gets the view for the given viewmodel.
		/// </summary>
		/// <param name="viewModel">The viewmodel.</param>
		/// <returns>The view for the given viewmodel.</returns>
		public Window GetView(object viewModel)
		{
			return this.openWindows[viewModel];
		}

		/// <summary>
		/// Creates a command to open a window.
		/// </summary>
		/// <param name="viewModelType">The type of window viewmodel to open.</param>
		/// <param name="openAsDialog">True to open as a dialog, false to open as a window.</param>
		/// <returns>The command.</returns>
		public ICommand CreateOpenCommand(Type viewModelType, bool openAsDialog = false)
		{
			var command = ReactiveCommand.Create();
			command.Subscribe(_ =>
			{
				if (openAsDialog)
				{
					this.OpenDialog(Ioc.Get(viewModelType));
				}
				else
				{
					this.OpenOrFocusWindow(viewModelType);
				}
			});

			return command;
		}

		/// <summary>
		/// Focuses the window.
		/// </summary>
		/// <param name="viewModel">The viewmodel of the window to focus.</param>
		public void Focus(object viewModel)
		{
			this.openWindows[viewModel].Focus();
		}

		/// <summary>
		/// Activates the window.
		/// </summary>
		/// <param name="viewModel">The viewmodel of the window to activate.</param>
		public void Activate(object viewModel)
		{
			this.openWindows[viewModel].Activate();
		}

		/// <summary>
		/// Closes the window.
		/// </summary>
		/// <param name="viewModel">The viewmodel of the window to close.</param>
		public void Close(object viewModel)
		{
			this.CloseInternal(viewModel, userInitiated: true);
		}

		/// <summary>
		/// Closes the window of the given type.
		/// </summary>
		/// <typeparam name="T">The viewmodel type of the window to close.</typeparam>
		/// <param name="userInitiated">True if the user specifically asked this window to close.</param>
		public void Close<T>(bool userInitiated) where T : class
		{
			object viewModel = this.FindOpenWindowViewModel(typeof (T));
			if (viewModel != null)
			{
				this.CloseInternal(viewModel, userInitiated);
			}
		}

		/// <summary>
		/// Gets a list of window positions.
		/// </summary>
		/// <param name="excludeWindow">The window to exclude.</param>
		/// <returns>A list of open window positions.</returns>
		public List<WindowPosition> GetOpenedWindowPositions(Window excludeWindow = null)
		{
			var result = new List<WindowPosition>();
			foreach (var definition in Definitions.Where(d => d.PlacementConfigKey != null))
			{
				object windowVM = this.FindOpenWindowViewModel(definition.ViewModelType);
				if (windowVM != null)
				{
					Window window = this.GetView(windowVM);
					if (window != null && window != excludeWindow)
					{
						result.Add(new WindowPosition
						{
							Position = new Rect(
								(int)window.Left,
								(int)window.Top,
								(int)window.ActualWidth,
								(int)window.ActualHeight),
							ViewModelType = definition.ViewModelType
						});
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Prepares a window for opening.
		/// </summary>
		/// <param name="viewModel">The window viewmodel to use.</param>
		/// <param name="ownerViewModel">The owner viewmodel.</param>
		/// <param name="userInitiated">True if the user specifically asked this window to open,
		/// false if it being re-opened automatically on app start.</param>
		/// <param name="isDialog">True if the window is being opened as a dialog, false if it's being opened
		/// as a window.</param>
		/// <returns>The prepared window.</returns>
		private Window PrepareWindowForOpen(object viewModel, object ownerViewModel, bool userInitiated, bool isDialog)
		{
			Window windowToOpen = CreateWindow(viewModel.GetType());
			if (ownerViewModel != null)
			{
				windowToOpen.Owner = this.openWindows[ownerViewModel];
			}

			windowToOpen.DataContext = viewModel;
			windowToOpen.Closing += this.OnClosingHandler;

			WindowDefinition windowDefinition = GetWindowDefinition(viewModel);
			if (windowDefinition != null && !windowDefinition.ManualPlacementRestore && windowDefinition.PlacementConfigKey != null)
			{
				windowToOpen.SourceInitialized += (o, e) =>
				{
					string placementJson = Config.Get<string>(windowDefinition.PlacementConfigKey);
					if (isDialog)
					{
						windowToOpen.SetPlacementJson(placementJson);
					}
					else
					{
						windowToOpen.PlaceDynamic(placementJson);
					}
				};
			}

			this.openWindows.Add(viewModel, windowToOpen);

			if (userInitiated)
			{
				if (windowDefinition != null)
				{
					if (windowDefinition.IsOpenConfigKey != null)
					{
						Config.Set(windowDefinition.IsOpenConfigKey, true);
					}
				}
			}

			var localWindowOpened = this.WindowOpened;
			if (localWindowOpened != null)
			{
				localWindowOpened(this, new EventArgs<Type>(viewModel.GetType()));
			}

			if (!isDialog)
			{
				windowToOpen.RegisterGlobalHotkeys();
			}

			return windowToOpen;
		}

		/// <summary>
		/// Closes the window.
		/// </summary>
		/// <param name="viewModel">The viewmodel of the window to close.</param>
		/// <param name="userInitiated">True if the user specifically asked this window to close.</param>
		private void CloseInternal(object viewModel, bool userInitiated)
		{
			if (!this.openWindows.ContainsKey(viewModel))
			{
				return;
			}

			Window window = this.openWindows[viewModel];

			if (!userInitiated)
			{
				window.Closing -= this.OnClosingHandler;
				this.OnClosing(window, userInitiated: false);
			}

			window.Close();
		}

		/// <summary>
		/// Fires when a window is closing.
		/// </summary>
		/// <param name="sender">The sending window.</param>
		/// <param name="e">The cancellation event args.</param>
		/// <remarks>This should only fire when the user has specifically asked for the window to
		/// close.</remarks>
		private void OnClosingHandler(object sender, CancelEventArgs e)
		{
			var closingWindow = (Window)sender;
			this.OnClosing(closingWindow, userInitiated: true);
		}

		/// <summary>
		/// Fires when a window is closing.
		/// </summary>
		/// <param name="window">The window.</param>
		/// <param name="userInitiated">True if the close was initated by the user, false if this
		/// was initiated by the system as part of app shutdown.</param>
		private void OnClosing(Window window, bool userInitiated)
		{
			object viewModel = window.DataContext;
			var closableWindow = viewModel as IClosableWindow;
			if (closableWindow != null)
			{
				var dialogVM = closableWindow;
				dialogVM.OnClosing();
			}

			WindowDefinition windowDefinition = GetWindowDefinition(viewModel);
			if (windowDefinition != null)
			{
				if (windowDefinition.PlacementConfigKey != null)
				{
					Config.Set(windowDefinition.PlacementConfigKey, window.GetPlacementJson());
				}

				if (userInitiated && windowDefinition.IsOpenConfigKey != null)
				{
					Config.Set(windowDefinition.IsOpenConfigKey, false);
				}
			}

			this.openWindows.Remove(viewModel);

			if (userInitiated && window.Owner != null)
			{
				window.Owner.Activate();
			}

			var localWindowClosed = this.WindowClosed;
			if (localWindowClosed != null)
			{
				localWindowClosed(this, new EventArgs<Type>(viewModel.GetType()));
			}
		}

		/// <summary>
		/// Finds the viewmodel for an open window.
		/// </summary>
		/// <param name="viewModelType">The viewmodel's type.</param>
		/// <returns>The viewmodel, or null if it was not open.</returns>
		private object FindOpenWindowViewModel(Type viewModelType)
		{
			return this.openWindows.Keys.FirstOrDefault(k => k.GetType() == viewModelType);
		}

		/// <summary>
		/// Gets a tracked window definition from the given viewmodel.
		/// </summary>
		/// <param name="viewModel">The window viewmodel.</param>
		/// <returns>The tracked window definition for the viewmodel.</returns>
		private static WindowDefinition GetWindowDefinition(object viewModel)
		{
			Type viewModelType = viewModel.GetType();
			WindowDefinition definition = Definitions.FirstOrDefault(d => d.ViewModelType == viewModelType);
			if (definition != null)
			{
				return definition;
			}

			return null;
		}

		/// <summary>
		/// Creates a Window for the given viewmodel type.
		/// </summary>
		/// <param name="viewModelType">The type of viewmodel.</param>
		/// <returns>The created window.</returns>
		private static Window CreateWindow(Type viewModelType)
		{
			string typeName = viewModelType.Name;
			string baseName;
			string suffix;

			if (typeName.EndsWith("DialogViewModel", StringComparison.Ordinal))
			{
				baseName = typeName.Substring(0, typeName.Length - "DialogViewModel".Length);
				suffix = "Dialog";
			}
			else if (typeName.EndsWith("WindowViewModel", StringComparison.Ordinal))
			{
				baseName = typeName.Substring(0, typeName.Length - "WindowViewModel".Length);
				suffix = "Window";
			}
			else if (typeName.EndsWith("ViewModel", StringComparison.Ordinal))
			{
				baseName = typeName.Substring(0, typeName.Length - "ViewModel".Length);
				suffix = string.Empty;
			}
			else
			{
				throw new ArgumentException("Window viewmodel type's name must end in 'ViewModel'");
			}

			Type windowType = Type.GetType(WindowTypePrefix + baseName + suffix);
			if (windowType == null)
			{
				windowType = Type.GetType(WindowTypePrefix + baseName);
			}

			if (windowType == null)
			{
				throw new ArgumentException("Could not find Window for " + typeName);
			}

			return (Window)Activator.CreateInstance(windowType);
		}
	}
}
