﻿using SimpleSockets.Messaging.Metadata;
using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SimpleSockets.Messaging.MessageContracts;
using System.Collections.Generic;

namespace SimpleSockets.Messaging
{
	/// <summary>
	/// Message is:
	///  - Flags
	///  - Header
	///  - Message
	/// </summary>
	internal class SimpleMessage
	{

		/// <summary>
		/// Header string
		/// Used for IMessageContract and path for a file and folder.
		/// </summary>
		internal byte[] Header { get; set; }

		/// <summary>
		/// What type of message it is
		/// Takes 1 Byte
		/// </summary>
		internal MessageType MessageType { get; set; }

		/// <summary>
		/// MessageContract item
		/// </summary>
		internal IMessageContract MessageContract { get; set; }

		/// <summary>
		/// Message Data
		/// </summary>
		internal byte[] Data { get; set; }

		/// <summary>
		/// Compress Data before sending.
		/// </summary>
		internal bool Compressed { get; set; }

		/// <summary>
		/// Encrypt Data before sending.
		/// </summary>
		internal bool Encrypted { get; set; }

		/// <summary>
		/// Metadata that can be send with a message
		/// </summary>
		internal byte[] Metadata { get; set; }

		/// <summary>
		/// Constructed after 'Build', these are the bytes that will be sent to another SimpleSocket
		/// </summary>
		internal byte[] PayLoad { get; set; }

		/// <summary>
		/// Will display what the message contains
		/// HeaderFields[0] = MessageLength			  -> Always True    (Length of 4 bytes)
		/// HeaderFields[1] = MessageType			  -> Always True	(Length of 1 byte )
		/// HeaderFields[2] = Header                  -> True/False     (Length of 4 bytes) -> This contains the length of the header (First x bytes of data)
		/// HeaderFields[3] = File/Folder part number -> True/False     (Length of 4 bytes)
		/// HeaderFields[4] = Total Parts			  -> True/False		(Length of 4 bytes)
		/// HeaderFields[5] = Encrypted				  -> True/False
		/// HeaderFields[6] = Compressed			  -> True/False
		/// HeaderFields[7] = Metadata				  -> True/False		(Length of 4 bytes)
		/// </summary>
		internal BitArray HeaderFields { get; set; }

		private readonly bool _debug;
		private readonly SimpleSocket _socket;
		private ClientMetadata _state;
		private int _headerLength = 0;
		private int _metadaLength = 0;
		private int _partNumber = -1;
		private int _totalParts = -1;
		private int _receivingMessageLength = 0;
		private IClientInfo _sendClient = null;

		//Message processing helpers
		private byte[] _receivedBytes = new byte[0];
		private byte[] _receivedMetadataBytes = new byte[0];
		private byte[] _receivedHeaderBytes = new byte[0];
		private int _readHeaderBytes = 0;
		private int _readBytes = 0;
		private int _readMetadataBytes = 0;


		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="messageType"></param>
		/// <param name="debug"></param>
		/// <param name="socket"></param>
		internal SimpleMessage(MessageType messageType, SimpleSocket socket, bool debug = false)
		{
			Metadata = new byte[0];
			Header = new byte[0];
			Data = new byte[0];

			InitHeaderFields();

			_socket = socket;
			_debug = debug;
			MessageType = messageType;
		}

		/// <summary>
		/// Receiving constructor
		/// </summary>
		/// <param name="state"></param>
		/// <param name="socket"></param>
		/// <param name="debug"></param>
		internal SimpleMessage(ClientMetadata state, SimpleSocket socket, bool debug = false)
		{
			Metadata = new byte[0];
			Header = new byte[0];
			Data = new byte[0];

			_debug = debug;
			_state = state;
			_socket = socket;
		}

		#region Builders

		/// <summary>
		/// Sets the encryption status of the message
		/// </summary>
		/// <param name="encrypt"></param>
		/// <returns></returns>
		internal SimpleMessage EncryptMessage(bool encrypt)
		{
			Encrypted = encrypt;
			HeaderFields[5] = encrypt;
			return this;
		}

		/// <summary>
		/// Sets the compression status of the message
		/// </summary>
		/// <param name="compress"></param>
		/// <returns></returns>
		internal SimpleMessage CompressMessage(bool compress)
		{
			Compressed = compress;
			HeaderFields[6] = compress;
			return this;
		}

		/// <summary>
		/// Sets the message using bytes.
		/// </summary>
		/// <param name="message"></param>
		/// <returns></returns>
		internal SimpleMessage SetMessage(string message)
		{
			Data = Encoding.UTF8.GetBytes(message);
			return this;
		}

		/// <summary>
		/// Sets the message using a string
		/// </summary>
		/// <param name="bytes"></param>
		/// <returns></returns>
		internal SimpleMessage SetBytes(byte[] bytes)
		{
			Data = bytes;
			return this;
		}

		/// <summary>
		/// Sets the metadata using bytes.
		/// </summary>
		/// <param name="metadata"></param>
		/// <returns></returns>
		internal SimpleMessage SetMetadata(IDictionary<object, object> metadata) {
			Metadata = _socket.ObjectSerializer.SerializeObjectToBytes(metadata);
			HeaderFields[7] = true;
			return this;
		}

		/// <summary>
		/// Sets the header, only required for MessageContracts, Files or Folders.
		/// </summary>
		/// <param name="header"></param>
		/// <returns></returns>
		internal SimpleMessage SetHeaderString(string header)
		{
			Header = Encoding.UTF8.GetBytes(header);
			HeaderFields[2] = true;
			return this;
		}

		/// <summary>
		/// Sets the header only required for MessageContracts, Files or Folders
		/// </summary>
		/// <param name="header"></param>
		/// <returns></returns>
		internal SimpleMessage SetHeader(byte[] header)
		{
			Header = header;
			HeaderFields[2] = true;
			return this;
		}

		/// <summary>
		/// Set the File/Folder part number.
		/// </summary>
		/// <param name="part"></param>
		/// <returns></returns>
		internal SimpleMessage SetPartNumber(int part)
		{
			_partNumber = part;
			HeaderFields[3] = true;
			return this;
		}

		/// <summary>
		/// Set the total File/Folder parts.
		/// </summary>
		/// <param name="total"></param>
		/// <returns></returns>
		internal SimpleMessage SetTotalParts(int total)
		{
			_totalParts = total;
			HeaderFields[4] = true;
			return this;
		}

		/// <summary>
		/// Set the Id of the client to which the message will be sent.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		internal SimpleMessage SetSendClient(IClientInfo clientInfo)
		{
			_sendClient = clientInfo;
			return this;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Init
		/// </summary>
		/// <returns></returns>
		private void InitHeaderFields()
		{
			HeaderFields = new BitArray(8, false) {[0] = true, [1] = true};
		}

		//Convert the HeaderFields to a single byte.
		private byte[] HeaderFieldsToByte()
		{
			if (HeaderFields.Count != 8)
				throw new Exception("Invalid headerFieldsLength");
			var headerFieldsByte = new byte[1];
			HeaderFields.CopyTo(headerFieldsByte, 0);
			return headerFieldsByte;
		}

		//Convert a byte to a BitArray
		private BitArray ConvertByteToBitArray(byte headerByte)
		{
			byte[] bytes = new byte[1];
			bytes[0] = headerByte;
			return new BitArray(bytes);
		}

		/// <summary>
		/// The length of the headerFields
		/// </summary>
		/// <returns></returns>
		private int CalculateHeaderFieldsLength()
		{
			var length = 1;

			if (HeaderFields[0]) //MessageLength    - 4  Bytes
				length += 4;
			if (HeaderFields[1]) //MessageType		- 1  Byte
				length += 1;
			if (HeaderFields[2]) //Header		    - 4  Bytes
				length += 4;
			if (HeaderFields[3]) //Part n°		    - 4  Bytes
				length += 4;
			if (HeaderFields[4]) //Total Parts		- 4  Bytes
				length += 4;
			if (HeaderFields[7]) //Metadata			- 4  Bytes
				length += 4;

			return length;
		}

		#endregion
		
		#region Message-Builder

		/// <summary>
		/// Build the header of the message.
		/// </summary>
		/// <returns></returns>
		private byte[] BuildHeader()
		{
			Log("Building the messageHeader");
			int currIndex = 0;

			var length = CalculateHeaderFieldsLength();
			byte[] headerFieldBytes = new byte[length];

			// HeaderFields -- 1 byte (8 boolean values)
			HeaderFieldsToByte().CopyTo(headerFieldBytes, currIndex);
			currIndex += 1;

			// MessageLength -- 4 bytes //
			BitConverter.GetBytes(Data.Length).CopyTo(headerFieldBytes, currIndex);
			currIndex += 4;

			// MessageType -- 1 byte (0-255)
			headerFieldBytes[currIndex] = (byte)MessageType;
			currIndex += 1;

			// HeaderLength -- 4 bytes
			if (HeaderFields[2])
			{
				byte[] headerString = Header;
				BitConverter.GetBytes(headerString.Length).CopyTo(headerFieldBytes, currIndex);
				currIndex += 4;
			}

			// Part -- 4 Bytes
			if (HeaderFields[3])
			{
				BitConverter.GetBytes(_partNumber).CopyTo(headerFieldBytes, currIndex);
				currIndex += 4;
			}

			// Total Parts -- 4 Bytes
			if (HeaderFields[4])
			{
				BitConverter.GetBytes(_totalParts).CopyTo(headerFieldBytes, currIndex);
				currIndex += 4;
			}

			// Metadata -- 4 Bytes
			if (HeaderFields[7]) {
				byte[] metadata = Metadata;
				BitConverter.GetBytes(metadata.Length).CopyTo(headerFieldBytes, currIndex);
				currIndex += 4;
			}

			Log("Header build completed.");

			return headerFieldBytes;
		}
		
		//Main method used to build a message
		private bool BuildMsg()
		{
			try
			{
				//Header
				var headerBytes = BuildHeader();
				byte[] payLoadBytes = new byte[headerBytes.Length + Data.Length + Header.Length + Metadata.Length];

				headerBytes.CopyTo(payLoadBytes, 0);

				if (HeaderFields[2])
					Header.CopyTo(payLoadBytes, headerBytes.Length);

				if (HeaderFields[7])
					Metadata.CopyTo(payLoadBytes, headerBytes.Length + Header.Length);

				Data.CopyTo(payLoadBytes, headerBytes.Length + Header.Length + Metadata.Length);

				PayLoad = payLoadBytes;


				Log("Message has been built and has a length of " + PayLoad.Length);
				Log("===============================================");

				return true;
			}
			catch (Exception ex)
			{
				if (_debug)
					Log(ex);

				throw;
			}

		}

		#region Build Synchronously

		public void CompressEncryptMessage()
		{
			if (Compressed)
			{
				if (MessageType != MessageType.File && MessageType != MessageType.Folder)
				{
					_socket.RaiseMessageUpdate(_sendClient, Encoding.UTF8.GetString(Data),
						HeaderFields[2] ? Encoding.UTF8.GetString(Header) : "", MessageType,
						MessageState.Compressing);
					Log("Compressing message...");
				}

				if (HeaderFields[2])
					Header = _socket.CompressBytes(Header);
				if (MessageType != MessageType.File && MessageType != MessageType.Folder)
					Data = _socket.CompressBytes(Data);
			}

			if (Encrypted)
			{
				if (MessageType != MessageType.File && MessageType != MessageType.Folder)
				{
					_socket.RaiseMessageUpdate(_sendClient, Encoding.UTF8.GetString(Data),HeaderFields[2] ? Encoding.UTF8.GetString(Header) : "", MessageType,
						MessageState.Encrypting);
					Log("Encrypting message...");
				}

				if (HeaderFields[2])
					Header = _socket.EncryptBytes(Header);
				if (MessageType != MessageType.File && MessageType != MessageType.Folder)
					Data = _socket.EncryptBytes(Data);
			}
		}

		/// <summary>
		/// Builds the message with the given params
		/// </summary>
		/// <returns></returns>
		internal bool Build()
		{
			Log("===============================================");
			Log("Building Message");
			CompressEncryptMessage();
			return BuildMsg();
		}

		#endregion

		#region Build Async

		/// <summary>
		/// Builds the message with the given parameters.
		/// </summary>
		internal async Task<bool> BuildAsync()
		{
			Log("===============================================");
			Log("Building Message");

			await CompressEncryptMessageAsync();
			return BuildMsg();
		}

		internal async Task CompressEncryptMessageAsync()
		{
			if (Compressed)
			{
				if (MessageType != MessageType.File && MessageType != MessageType.Folder)
				{
					if (HeaderFields[7] == false) {
						_socket.RaiseMessageUpdate(_sendClient, Encoding.UTF8.GetString(Data),
												HeaderFields[2] ? Encoding.UTF8.GetString(Header) : "", MessageType,
												MessageState.Compressing);
					}

					Log("Compressing message...");
				}

				if (HeaderFields[2])
					Header = await _socket.CompressBytesAsync(Header);
				if (HeaderFields[7])
					Metadata = await _socket.CompressBytesAsync(Metadata);
				if (MessageType != MessageType.File && MessageType != MessageType.Folder)
					Data = await _socket.CompressBytesAsync(Data);
			}

			if (Encrypted)
			{
				if (MessageType != MessageType.File && MessageType != MessageType.Folder)
				{
					if (HeaderFields[7] == false) {
						_socket.RaiseMessageUpdate(_sendClient, Encoding.UTF8.GetString(Data),
									HeaderFields[2] ? Encoding.UTF8.GetString(Header) : "", MessageType,
									MessageState.Encrypting);
					}
					Log("Encrypting message...");
				}

				if (HeaderFields[2])
					Header = await _socket.EncryptBytesAsync(Header);
				if (HeaderFields[7])
					Metadata = await _socket.EncryptBytesAsync(Metadata);
				if (MessageType != MessageType.File && MessageType != MessageType.Folder)
					Data = await _socket.EncryptBytesAsync(Data);
			}
		}

		#endregion

		#endregion

		#region Message-Receivers

		/// <summary>
		/// Call from socket receiver and create a message from this.
		/// </summary>
		/// <param name="receive"></param>
		internal bool ReadBytesAndBuildMessage(int receive) {

			var processing = true;

			while (processing)
			{
				if (_state.Flag == MessageFlag.Idle)
				{
					DeconstructHeaderField(_state.Buffer[0]);
					var length = CalculateHeaderFieldsLength();
					if (_state.Buffer.Length < length)
					{
						return true;
					}

					byte headerFieldByte = _state.Buffer[0];
					DeconstructHeaderFields(headerFieldByte, _state.Buffer);
					byte[] remainingBytes = new byte[_state.Buffer.Length - length];
					receive -= length;

					Array.Copy(_state.Buffer, length, remainingBytes, 0, remainingBytes.Length);
					_state.ChangeBuffer(remainingBytes);
					_state.Flag = MessageFlag.ProcessingHeader;

				}

				if (_state.Buffer.Length < 2)
				{
					processing = false;
				}


				if (_state.Flag == MessageFlag.ProcessingHeader)
				{
					var enoughBytes = DeconstructHeader();
					if (!enoughBytes || _state.Flag != MessageFlag.ProcessingData || _state.Flag != MessageFlag.ProcessingMetadata)
						processing = false;
					receive -= _headerLength;
				}

				if (_state.Flag == MessageFlag.ProcessingMetadata) {
					processing = ReadMetaData(receive);
					receive = _state.Buffer.Length;
				}

				if (_state.Flag == MessageFlag.ProcessingData) {
					processing = ReadData(receive);
					receive = _state.Buffer.Length;
				}
			}

			return true;
		}


		#region Header-Deconstructors

		//Gets the header if there is one.
		private bool DeconstructHeader()
		{
			if (HeaderFields[2])
			{
				if (_state.Buffer.Length >= _headerLength)
				{
					byte[] headerBytes = new byte[_headerLength];
					byte[] remainingBytes = new byte[_state.Buffer.Length - _headerLength];
					Array.Copy(_state.Buffer, 0, headerBytes, 0, headerBytes.Length);

					//Decrypt then decompress
					if (Encrypted)
						headerBytes = _socket.DecryptBytes(headerBytes);
					if (Compressed)
						headerBytes = _socket.DecompressBytes(headerBytes);

					if (_state.Buffer.Length > _headerLength)
					{
						Array.Copy(_state.Buffer, _headerLength, remainingBytes, 0, remainingBytes.Length);
						_state.ChangeBuffer(remainingBytes);
					}

					//Set header.
					Header = headerBytes;

					if (HeaderFields[7])
						_state.Flag = MessageFlag.ProcessingMetadata;
					else
						_state.Flag = MessageFlag.ProcessingData;

					return true;
				}

				return false;
			}

			if (HeaderFields[7])
				_state.Flag = MessageFlag.ProcessingMetadata;
			else
				_state.Flag = MessageFlag.ProcessingData;
			
			return true;
		}

		private void DeconstructHeaderField(byte headerFieldByte)
		{
			HeaderFields = new BitArray(8, false);
			HeaderFields = ConvertByteToBitArray(headerFieldByte);
		}

		private void DeconstructHeaderFields(byte headerFieldByte, byte[] buffer)
		{
			int index = 6;

			_receivingMessageLength = BitConverter.ToInt32(_state.Buffer,1);
			MessageType = (MessageType)_state.Buffer[5];

			if (HeaderFields[2]) {
				_headerLength = BitConverter.ToInt32(_state.Buffer, index);
				index += 4;
			}
			else
				_headerLength = 0;

			if (HeaderFields[3]) {
				_partNumber = BitConverter.ToInt32(_state.Buffer, index);
				index += 4;
			}
			else
				_partNumber = 0;

			if (HeaderFields[4]) {
				_totalParts = BitConverter.ToInt32(_state.Buffer, index);
				index += 4;
			}
			else
				_totalParts = 0;

			if (HeaderFields[7]) {
				_metadaLength = BitConverter.ToInt32(_state.Buffer, index);
				index += 4;
			}
			else
				_metadaLength = 0;

			Encrypted = HeaderFields[5];
			Compressed = HeaderFields[6];

		}

		#endregion

		#region Read message-data

		private bool ReadMetaData(int receive) {

			if (receive < 0)
				return false;

			byte[] bytes = new byte[receive];
			_readMetadataBytes += receive;

			if (_readMetadataBytes > _metadaLength)
			{
				int extraRead = _readMetadataBytes - _metadaLength;

				var bytes2 = new byte[extraRead];
				Array.Copy(_state.Buffer, receive - extraRead, bytes2, 0, extraRead);

				bytes = new byte[receive - extraRead];
				Array.Copy(_state.Buffer, 0, bytes, 0, receive - extraRead);

				_state.Flag = MessageFlag.MetaDataReceivedExtraData;
				_state.ChangeBuffer(bytes2);
				_readMetadataBytes -= extraRead;
			}
			else if (_readMetadataBytes == _metadaLength)
			{
				_state.Flag = MessageFlag.MetaDataReceivedNoExtraData;
				bytes = new byte[receive];
				Array.Copy(_state.Buffer, 0, bytes, 0, bytes.Length);
				_state.ChangeBuffer(new byte[0]);
			}
			else
			{
				bytes = new byte[receive];
				Array.Copy(_state.Buffer, 0, bytes, 0, bytes.Length);
				_state.ChangeBuffer(new byte[0]);
			}

			// Add received metadata bytes to buffer
			var temp = new byte[_receivedMetadataBytes.Length + bytes.Length];
			_receivedMetadataBytes.CopyTo(temp, 0);
			bytes.CopyTo(temp, _receivedMetadataBytes.Length);
			_receivedMetadataBytes = temp;


			if (_state.Flag == MessageFlag.MetaDataReceivedNoExtraData || _state.Flag == MessageFlag.MetaDataReceivedExtraData) 
			{
				var returnValue = false;

				if (_state.Flag == MessageFlag.MetaDataReceivedExtraData)
					returnValue = true;

				_state.Flag = MessageFlag.ProcessingData;

				return returnValue;
			}

			return false;
		}

		//Returns true if there is more data to read.
		private bool ReadData(int receive)
		{
			if (receive < 0)
				return false;

			byte[] bytes = new byte[receive];
			_readBytes += receive;

			if (_readBytes > _receivingMessageLength)
			{
				int extraRead = _readBytes - _receivingMessageLength;

				var bytes2 = new byte[extraRead];
				Array.Copy(_state.Buffer, receive - extraRead, bytes2, 0, extraRead);

				bytes = new byte[receive - extraRead];
				Array.Copy(_state.Buffer, 0, bytes, 0, receive - extraRead);

				_readBytes -= extraRead;
				// _state.SubtractRead(extraRead);

				_state.ChangeBuffer(bytes2);
				_state.Flag = MessageFlag.MessageReceivedExtraData;
			}
			else if (_readBytes == _receivingMessageLength)
			{
				_state.Flag = MessageFlag.MessageReceivedNoExtraData;
				bytes = new byte[receive];
				Array.Copy(_state.Buffer, 0, bytes, 0, bytes.Length);
				_state.ChangeBuffer(new byte[0]);
			}
			else
			{
				bytes = new byte[receive];
				Array.Copy(_state.Buffer, 0, bytes, 0, bytes.Length);
				_state.ChangeBuffer(new byte[0]);
			}

			if (MessageType == MessageType.File || MessageType == MessageType.Folder)
			{
				if (_socket.AllowReceivingFiles)
				{
					var file = Encoding.UTF8.GetString(Header);
					file = GetTempPath(file);
					CreateDeleteFile(file);

					if (_partNumber == 0)
					{
						_socket.RaiseFileReceiver(_state, _partNumber, _totalParts, file, MessageState.Beginning);
					}

					using (BinaryWriter writer = new BinaryWriter(File.Open(file, FileMode.Append)))
					{
						writer.Write(bytes, 0, bytes.Length);
						writer.Close();
					}
				}
			}
			else
			{
				var temp = new byte[_receivedBytes.Length + bytes.Length];
				_receivedBytes.CopyTo(temp, 0);
				bytes.CopyTo(temp, _receivedBytes.Length);
				_receivedBytes = temp;
				// _state.AppendBytes(bytes);
			}
			
			if (_state.Flag == MessageFlag.MessageReceivedNoExtraData)
			{
				bytes = null;
				MessageHasBeenReceived();
				_state.Reset();
			}

			if (_state.Flag == MessageFlag.MessageReceivedExtraData)
			{
				bytes = null;
				MessageHasBeenReceived();
				_state.Reset();
				return true;
			}

			return false;
		}

		private string GetTempPath(string path)
		{
			if (Compressed || Encrypted)
			{
				var info = new FileInfo(path);
				var temp = _socket.TempPath + info.Name;
				if (Compressed)
				{
					if (MessageType == MessageType.File)
						temp += _socket.FileCompressor.Extension;
					if (MessageType == MessageType.Folder)
						temp += _socket.FolderCompressor.Extension;
				}

				if (Encrypted)
					temp += _socket.MessageEncryption.Extension;

				return temp;
			}

			return path;
		}

		private void CreateDeleteFile(string path)
		{
			if (_partNumber == 0)
			{
				FileInfo file = new FileInfo(path);
				file.Directory?.Create();
				//if (file.Exists)
				//{
				//	file.Delete();
				//}
			}
		}

		#endregion

		#region CompletedMessageHandlers

		private void MessageHasBeenReceived()
		{

			if (MessageType != MessageType.Folder && MessageType != MessageType.File)
			{
				//Header has already been decrypted and decompressed.

				// Data
				if (Encrypted)
					_receivedBytes = _socket.DecryptBytes(_receivedBytes);

				if (Compressed)
					_receivedBytes = _socket.DecompressBytes(_receivedBytes);

				// Metadata
				if (HeaderFields[7]) {
					if (Encrypted)
						_receivedMetadataBytes = _socket.DecryptBytes(_receivedMetadataBytes);

					if (Compressed)
						_receivedMetadataBytes = _socket.DecompressBytes(_receivedMetadataBytes);
				}

			}

			//Invoke correct receiver.
			switch (MessageType)
			{

				case MessageType.Auth:
				{
						var message = Encoding.UTF8.GetString(_receivedBytes);
						var arr = message.Split('|');
						_state.ClientName = arr[0];
						_state.Guid = arr[1];
						_state.UserDomainName = arr[2];
						_state.OsVersion = arr[3];
						break;
				}
				case MessageType.BasicAuth:
				{
						var message = Encoding.UTF8.GetString(_receivedBytes);
						var arr = message.Split('|');
						_state.Guid = arr[0];
						_state.OsVersion = arr[1];
						break;
				}
				case MessageType.Object:
				{
						var type = Type.GetType(Encoding.UTF8.GetString(Header));
						var obj = _socket.ObjectSerializer.DeserializeBytesToObject(_receivedBytes, type);
						_socket.RaiseObjectReceived(_state, obj, obj.GetType());
						break;
				}
				case MessageType.Message:
						_socket.RaiseMessageReceived(_state, Encoding.UTF8.GetString(_receivedBytes));
						break;
				case MessageType.Bytes:
						_socket.RaiseBytesReceived(_state, _receivedBytes);
						break;
				case MessageType.File:
				{
						var file = Encoding.UTF8.GetString(Header);
						var output = file;

						_socket.RaiseFileReceiver(_state, _partNumber, _totalParts, output, MessageState.ReceivingData);

						if (_totalParts == _partNumber)
						{
							file = GetTempPath(file);

							if (Encrypted)
							{
								_socket.RaiseFileReceiver(_state, _partNumber, _totalParts, output, MessageState.Decrypting);
								Log("Decrypting file from path : " + file);
								var tmp = file;
								file = _socket.DecryptFile(file, _socket.TempPath + Path.GetRandomFileName()).FullName;
								File.Delete(tmp);
								Log("File has been decrypted from path: " + file);
								_socket.RaiseFileReceiver(_state, _partNumber, _totalParts, output,MessageState.DecryptingDone);
							}

							if (Compressed)
							{
								_socket.RaiseFileReceiver(_state, _partNumber, _totalParts, output,MessageState.Decompressing);
								Log("Decompressing file from path : " + file);
								var tmp = file;
								file = _socket.DecompressFile(new FileInfo(file), _socket.TempPath + Path.GetRandomFileName()).FullName;
								if (Encrypted)
									File.Delete(tmp);
								Log("File Decompressed from path : " + file);
								_socket.RaiseFileReceiver(_state, _partNumber, _totalParts, output,MessageState.DecompressingDone);
							}


							if (Encrypted || Compressed)
							{
								if (File.Exists(output))
									File.Delete(output);
								Log("Deleting file: " + output);
								File.Move(file, output);
								Log("Moving " + file + " to " + output);
								File.Delete(file); // Delete encrypted/compressed file.
								Log("Deleting temp file : " + file);
							}

							_socket.RaiseFileReceiver(_state, _partNumber, _totalParts, output, MessageState.Completed);
							Log("File has been received and saved to path : " + output);
						}
						
						break;
				}
				case MessageType.MessageWithMetadata:
				{
						var header = Encoding.UTF8.GetString(Header);
						object obj = null;
						Type objType = null;

						if (header != "ByteArray")
						{
							objType = Type.GetType(header);
							obj = _socket.ObjectSerializer.DeserializeBytesToObject(_receivedBytes, objType);
						}
						else {
							obj = _receivedBytes;
							objType = obj.GetType();
						}

						var metadata = _socket.ObjectSerializer.DeserializeJson<IDictionary<object, object>>(_receivedMetadataBytes);
						_socket.RaiseMessageWithMetaDataReceived(_state, obj, metadata, objType);
						break;
				}
				case MessageType.MessageContract:
				{
						var contractHeader = Encoding.UTF8.GetString(Header);
						var contract = GetCorrespondingMessageContract(contractHeader);
						if (contract != null)
							_socket.RaiseMessageContractReceived(_state, contract, _receivedBytes);
						else if (_debug)
							Log("MessageContract with Header '" + contractHeader + "' does not exist.");
						break;
				}
				case MessageType.Folder:
				{
						var folder = Encoding.UTF8.GetString(Header);
						var output = folder;

						_socket.RaiseFolderReceiver(_state, _partNumber, _totalParts, output, MessageState.ReceivingData);

						if (_totalParts == _partNumber)
						{
							folder = GetTempPath(folder);

							if (Encrypted)
							{
								var tmp = _socket.DecryptFile(folder, _socket.TempPath + Path.GetRandomFileName()).FullName;
								File.Delete(folder);
								folder = tmp;
							}


							_socket.ExtractToFolder(folder, output);
							File.Delete(folder); //Delete extracted folder.

							_socket.RaiseFolderReceiver(_state, _partNumber, _totalParts, output, MessageState.Completed);
						}
						break;
				}
				default:
						throw new ArgumentOutOfRangeException();
			}

			ResetReadData();

		}

		private IMessageContract GetCorrespondingMessageContract(string header)
		{
			var contract = _socket.MessageContracts[header];
			return contract;
		}

		#endregion

		#endregion

		#region Debugging

		private void Log(string log)
		{
			_socket.Log(log);
		}

		private void Log(Exception ex)
		{
			_socket.Log(ex);
		}

		#endregion

		public void ResetReadData() {
			// Resets
			Metadata = new byte[0];
			Header = new byte[0];
			Data = new byte[0];
			_receivedBytes = new byte[0];
			_receivedMetadataBytes = new byte[0];
			_receivedHeaderBytes = new byte[0];
			_readBytes = 0;
			_readMetadataBytes = 0;
			_readHeaderBytes = 0;
		}

	}

}
