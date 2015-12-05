﻿using System;
using System.Globalization;
using System.Windows.Threading;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class ShutdownWarningWindowViewModel : OkCancelDialogViewModel
	{
		private EncodeCompleteActionType actionType;

		private ISystemOperations systemOperations = Ioc.Get<ISystemOperations>();
		private int secondsRemaining = 30;
		private DispatcherTimer timer;

		public ShutdownWarningWindowViewModel(EncodeCompleteActionType actionType)
		{
			this.actionType = actionType;

			this.CancelOperation = ReactiveCommand.Create();
			this.CancelOperation.Subscribe(_ => this.CancelOperationImpl());

			this.timer = new DispatcherTimer();
			this.timer.Interval = TimeSpan.FromSeconds(1);
			this.timer.Tick += (o, e) =>
			{
				secondsRemaining--;
				this.RaisePropertyChanged(nameof(this.Message));

				if (secondsRemaining == 0)
				{
					this.timer.Stop();
					this.Cancel.Execute(null);
					this.ExecuteAction();
				}
			};

			this.timer.Start();
		}

		public string Title
		{
			get
			{
				switch (this.actionType)
				{
					case EncodeCompleteActionType.Sleep:
						return MiscRes.EncodeCompleteWarning_SleepTitle;
					case EncodeCompleteActionType.LogOff:
						return MiscRes.EncodeCompleteWarning_LogOffTitle;
					case EncodeCompleteActionType.Shutdown:
						return MiscRes.EncodeCompleteWarning_ShutdownTitle;
					case EncodeCompleteActionType.Hibernate:
						return MiscRes.EncodeCompleteWarning_HibernateTitle;
					default:
						return string.Empty;
				}
			}
		}

		public string Message
		{
			get
			{
				string messageFormat = string.Empty;
				switch (this.actionType)
				{
					case EncodeCompleteActionType.Sleep:
						messageFormat = MiscRes.EncodeCompleteWarning_SleepMessage;
						break;
					case EncodeCompleteActionType.LogOff:
						messageFormat = MiscRes.EncodeCompleteWarning_LogOffMessage;
						break;
					case EncodeCompleteActionType.Shutdown:
						messageFormat = MiscRes.EncodeCompleteWarning_ShutdownMessage;
						break;
					case EncodeCompleteActionType.Hibernate:
						messageFormat = MiscRes.EncodeCompleteWarning_HibernateMessage;
						break;
				}

				return string.Format(CultureInfo.CurrentCulture, messageFormat, this.secondsRemaining);
			}
		}

		private void ExecuteAction()
		{
			switch (actionType)
			{
				case EncodeCompleteActionType.Sleep:
					this.systemOperations.Sleep();
					break;
				case EncodeCompleteActionType.LogOff:
					this.systemOperations.LogOff();
					break;
				case EncodeCompleteActionType.Shutdown:
					this.systemOperations.ShutDown();
					break;
				case EncodeCompleteActionType.Hibernate:
					this.systemOperations.Hibernate();
					break;
			}
		}

		public ReactiveCommand<object> CancelOperation { get; }
		private void CancelOperationImpl()
		{
			this.timer.Stop();
			this.Cancel.Execute(null);
		}
	}
}
