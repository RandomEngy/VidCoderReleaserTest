﻿using System;
using System.Reactive.Linq;
using System.Windows.Shell;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.ViewModel;

namespace VidCoder.Services
{
	public class TaskBarProgressTracker : ReactiveObject
	{
		public TaskBarProgressTracker()
		{
			ProcessingService processingService = Ioc.Get<ProcessingService>();
			MainViewModel mainViewModel = Ioc.Get<MainViewModel>();

			// Set up some observables for properties we care about.
			var isEncodingObservable = processingService
				.WhenAnyValue(x => x.EncodeProgress)
				.Select(encodeProgress => encodeProgress != null && encodeProgress.Encoding);

			var encodeProgressFractionObservable = processingService
				.WhenAnyValue(x => x.EncodeProgress)
				.Select(encodeProgress => encodeProgress == null ? 0 : encodeProgress.OverallProgressFraction);

			var isEncodePausedObservable = processingService.WhenAnyValue(x => x.Paused);
			var videoSourceStateObservable = mainViewModel.WhenAnyValue(x => x.VideoSourceState);
			var scanProgressFractionObservable = mainViewModel.WhenAnyValue(x => x.ScanProgressFraction);

			// Set up output properties
			Observable.CombineLatest(
				isEncodingObservable,
				encodeProgressFractionObservable,
				videoSourceStateObservable,
				scanProgressFractionObservable,
				(isEncoding, encodeProgressFraction, videoSourceState, scanProgressFraction) =>
				{
					if (isEncoding)
					{
						return encodeProgressFraction;
					}
					else if (videoSourceState == VideoSourceState.Scanning)
					{
						return scanProgressFraction;
					}
					else
					{
						return 0;
					}
				}).ToProperty(this, x => x.ProgressFraction, out this.progressFraction);

			Observable.CombineLatest(
				isEncodingObservable,
				isEncodePausedObservable,
				videoSourceStateObservable,
				(isEncoding, isEncodePaused, videoSourceState) =>
				{
					if (isEncoding)
					{
						if (isEncodePaused)
						{
							return TaskbarItemProgressState.Paused;
						}
						else
						{
							return TaskbarItemProgressState.Normal;
						}
					}
					else if (videoSourceState == VideoSourceState.Scanning)
					{
						return TaskbarItemProgressState.Normal;
					}
					else
					{
						return TaskbarItemProgressState.None;
					}
				}).ToProperty(this, x => x.ProgressState, out this.progressState);
		}

		private ObservableAsPropertyHelper<double> progressFraction;
		public double ProgressFraction => this.progressFraction.Value;

		private ObservableAsPropertyHelper<TaskbarItemProgressState> progressState;
		public TaskbarItemProgressState ProgressState => this.progressState.Value;
	}
}
