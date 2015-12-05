﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using VidCoder.ViewModel;
using System.IO;
using System.Management;

namespace VidCoder.Services
{
	using System.Runtime.InteropServices;

	public class DriveService : IDriveService
	{
		private MainViewModel mainViewModel = Ioc.Get<MainViewModel>();
		private ManagementEventWatcher watcher;

		public DriveService()
		{
			// Bind to local machine
			var options = new ConnectionOptions { EnablePrivileges = true };
			var scope = new ManagementScope(@"root\CIMV2", options);

			try
			{
				var query = new WqlEventQuery
				{
					EventClassName = "__InstanceModificationEvent",
					WithinInterval = TimeSpan.FromSeconds(1),
					Condition = @"TargetInstance ISA 'Win32_LogicalDisk' and TargetInstance.DriveType = 5" // DriveType - 5: CDROM
				};

				this.watcher = new ManagementEventWatcher(scope, query);

				// register async. event handler
				this.watcher.EventArrived += this.HandleDiscEvent;
				this.watcher.Start();
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(e.Message);
			}
		}

		private void HandleDiscEvent(object sender, EventArrivedEventArgs e)
		{
			DispatchUtilities.BeginInvoke(() => this.mainViewModel.UpdateDriveCollection());
		}

		public IList<DriveInformation> GetDiscInformation()
		{
			DriveInfo[] driveCollection = DriveInfo.GetDrives();
			var driveList = new List<DriveInformation>();

			foreach (DriveInfo driveInfo in driveCollection)
			{
				if (driveInfo.DriveType == DriveType.CDRom && driveInfo.IsReady)
				{
					if (File.Exists(driveInfo.RootDirectory + @"VIDEO_TS\VIDEO_TS.IFO"))
					{
						driveList.Add(new DriveInformation
						{
							RootDirectory = driveInfo.RootDirectory.FullName,
							VolumeLabel = driveInfo.VolumeLabel,
							DiscType = DiscType.Dvd
						});
					}
					else if (Directory.Exists(driveInfo.RootDirectory + "BDMV"))
					{
						driveList.Add(new DriveInformation
						{
							RootDirectory = driveInfo.RootDirectory.FullName,
							VolumeLabel = driveInfo.VolumeLabel,
							DiscType = DiscType.BluRay
						});
					}
				}
			}

			return driveList;
		}

		public bool PathIsDrive(string sourcePath)
		{
			if (string.IsNullOrWhiteSpace(sourcePath))
			{
				return false;
			}

			string root = Path.GetPathRoot(sourcePath);
			if (string.Compare(sourcePath, root, StringComparison.OrdinalIgnoreCase) == 0)
			{
				foreach (DriveInformation drive in this.GetDiscInformation())
				{
					if (string.Compare(drive.RootDirectory, sourcePath, StringComparison.OrdinalIgnoreCase) == 0)
					{
						return true;
					}
				}
			}

			return false;
		}

		public DriveInformation GetDriveInformationFromPath(string sourcePath)
		{
			foreach (DriveInformation drive in this.GetDiscInformation())
			{
				if (string.Compare(drive.RootDirectory, sourcePath, StringComparison.OrdinalIgnoreCase) == 0)
				{
					return drive;
				}
			}

			return null;
		}

		public IList<DriveInfo> GetDriveInformation()
		{
			return new List<DriveInfo>(DriveInfo.GetDrives());
		}

		public void Close()
		{
			try
			{
				this.watcher.Stop();
			}
			catch (COMException)
			{
				// Can happen if the user has already disconnected. Ignore and continue shutting down.
			}
		}
	}
}
