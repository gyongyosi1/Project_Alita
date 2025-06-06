﻿using System.Net.Security;
using System.Net.Sockets;
using System.Threading;

namespace SimpleSockets.Messaging.Metadata
{
	/// <summary>
	/// Interface for SocketState
	/// </summary>
	public interface IClientMetadata: IClientInfo
	{
		/// <summary>
		/// Get the buffersize
		/// </summary>
		int BufferSize { get; }

		/// <summary>
		/// Bytes that need to be processed
		/// </summary>
		byte[] UnhandledBytes { get; set; }

		/// <summary>
		/// Get or set the SslStream
		/// </summary>
		SslStream SslStream { get; set; }

		ManualResetEvent MreRead { get; }

		ManualResetEvent MreReceiving { get; }

		ManualResetEvent MreTimeout { get; }

		/// <summary>
		/// The flag of the stateObject, used to check  in which state the object is.
		/// </summary>
		MessageFlag Flag { get; set; }
		
		/// <summary>
		/// If the state should be closed after this message
		/// </summary>
		bool Close { get; set; }

		/// <summary>
		/// Gets the buffer
		/// </summary>
		byte[] Buffer { get; }

		/// <summary>
		/// The listener socket
		/// </summary>
		Socket Listener { get; set;  }

		/// <summary>
		/// Change the value of the buffer
		/// </summary>
		/// <param name="newBuffer"></param>
		void ChangeBuffer(byte[] newBuffer);

		/// <summary>
		/// Reset the current state object.
		/// </summary>
		void Reset();

	}
}
