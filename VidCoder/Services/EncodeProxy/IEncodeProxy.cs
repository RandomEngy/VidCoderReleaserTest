﻿using System;
using System.Xml.Serialization;
using HandBrake.ApplicationServices.Interop.EventArgs;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using VidCoder.Services;
using VidCoderCommon.Model;

namespace VidCoder
{
	/// <summary>
	/// Abstraction for dealing with either a worker process or a local encoder.
	/// </summary>
	public interface IEncodeProxy
	{
		event EventHandler EncodeStarted;

		/// <summary>
		/// Fires for progress updates when encoding.
		/// </summary>
		event EventHandler<EncodeProgressEventArgs> EncodeProgress;

		/// <summary>
		/// Fires when an encode has completed.
		/// </summary>
		event EventHandler<EncodeCompletedEventArgs> EncodeCompleted;

		[XmlIgnore]
		bool IsEncodeStarted { get; }

		void StartEncode(
			VCJob job,
			ILogger logger,
			bool preview, 
			int previewNumber, 
			int previewSeconds, 
			double overallSelectedLengthSeconds);

		void PauseEncode();

		void ResumeEncode();

		void StopEncode();

		void StopAndWait();
	}
}