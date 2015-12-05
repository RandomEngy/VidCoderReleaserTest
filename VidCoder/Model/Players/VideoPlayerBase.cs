﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using VidCoder.Resources;
using VidCoder.Services;

namespace VidCoder.Model
{
	public abstract class VideoPlayerBase : IVideoPlayer
	{
		public bool Installed
		{
			get
			{
				string executable = this.PlayerExecutable;
				if (string.IsNullOrEmpty(executable))
				{
					return false;
				}

				return File.Exists(executable);
			}
		}

		public void PlayTitle(string discPath, int title)
		{
			string executablePath = this.PlayerExecutable;
			if (!File.Exists(executablePath))
			{
				string message = string.Format(
					MiscRes.CouldNotFindVideoPlayerError,
					this.Display);
				Ioc.Get<IMessageBoxService>().Show(message);
				return;
			}

			if (!discPath.EndsWith(@"\", StringComparison.Ordinal))
			{
				discPath += @"\";
			}

			try
			{
				this.PlayTitleInternal(executablePath, discPath, title);
			}
			catch (Exception exception)
			{
				string message = 
					MiscRes.ErrorPlayingSource + Environment.NewLine + Environment.NewLine + exception.Message;
				Ioc.Get<IMessageBoxService>().Show(message);
			}
		}

		public abstract string PlayerExecutable { get; }

		public abstract void PlayTitleInternal(string executablePath, string discPath, int title);

		public abstract string Id { get; }

		public abstract string Display { get; }
	}
}
