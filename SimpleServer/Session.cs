using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace SimpleServer
{
	class Session
	{
		// 패킷을 받았을 때 호출되는 콜백
		public event EventHandler<Message> ReceivedCallback;
		// 패킷을 보냈을 때 호출되는 콜백
		public event EventHandler<int> SentCallback;
		// 소켓이 닫힐 때 호출되는 콜백
		public event EventHandler<SocketAsyncEventArgs> ClosedCallback;
				
		SocketAsyncEventArgs receiveArgs;
		SocketAsyncEventArgs sendArgs;
		ILogger logger;

		Queue<Message> sendQueue = new Queue<Message>();
		int sndPacketOffset;
		int sndPacketLength;
		int sndBufferOffset;
		int sndBufferLength;

		byte[] rcvPacket;
		int rcvPacketOffset;
		int rcvPacketLength;
		byte[] rcvPacketLengthBuffer;
		
		public Session(Socket socket, int bufferSize, ILogger logger)
		{
			logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			this.logger = logger;

			this.receiveArgs = new SocketAsyncEventArgs();
			this.receiveArgs.AcceptSocket = socket;
			this.receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(ReceiveCompleted);
			this.receiveArgs.SetBuffer(new byte[bufferSize], 0, bufferSize);

			this.sendArgs = new SocketAsyncEventArgs();
			this.sendArgs.AcceptSocket = socket;
			this.sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(SendCompleted);
			this.sendArgs.SetBuffer(new byte[bufferSize], 0, bufferSize);
		}

		public void StartReceive()
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			try
			{
				bool pending = this.receiveArgs.AcceptSocket.ReceiveAsync(this.receiveArgs);
				if (!pending)
				{
					ReceiveCompleted(null, this.receiveArgs);
				}
			}
			catch (Exception ex)
			{
				this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		void ReceiveCompleted(object sender, SocketAsyncEventArgs args)
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			if (args.SocketError != SocketError.Success)
			{
				this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, Close socket: SocketError={args.SocketError}");
				CloseClientSocket(args);
				return;
			}

			if (args.BytesTransferred == 0)
			{
				this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, Close socket: BytesTransferred=0");
				CloseClientSocket(args);
				return;
			}

			int recvBufferOffset = 0;
			int recvBufferLength = args.BytesTransferred;

			while (0 < (recvBufferLength - recvBufferOffset))
			{
				// 수신 시작이면 패킷을 새로 받는 경우
				if (this.rcvPacketOffset == 0)
				{
					// 버퍼에 남은 부분이 패킷 길이 이상인가?
					if (Message.MESSAGE_LENGTH_TYPE_SIZE <= (recvBufferLength - recvBufferOffset))
					{
						// 받아야 하는 패킷 전체 길이만큼 버퍼 생성하고 복사
						this.rcvPacketLength = BitConverter.ToInt32(args.Buffer, args.Offset + recvBufferOffset) + Message.MESSAGE_LENGTH_TYPE_SIZE;
						this.rcvPacket = new byte[this.rcvPacketLength];
						int count = Math.Min(this.rcvPacketLength, (recvBufferLength - recvBufferOffset));
						Buffer.BlockCopy(args.Buffer, args.Offset + recvBufferOffset, this.rcvPacket, this.rcvPacketOffset, count);
						this.rcvPacketOffset += count;
						recvBufferOffset += count;
					}
					else
					{
						// 패킷 길이 정보를 저장하기 시작
						this.rcvPacketLengthBuffer = new byte[Message.MESSAGE_LENGTH_TYPE_SIZE];
						int count = Math.Min(this.rcvPacketLengthBuffer.Length, (recvBufferLength - recvBufferOffset));
						Buffer.BlockCopy(args.Buffer, args.Offset + recvBufferOffset, this.rcvPacketLengthBuffer, this.rcvPacketOffset, count);
						this.rcvPacketLength = Message.MESSAGE_LENGTH_TYPE_SIZE;
						this.rcvPacketOffset += count;
						recvBufferOffset += count;
					}
				}
				// 수신 데이터를 모두 소진하지 못한 경우 (패킷을 받는 중이거나 한 번에 여러 개의 패킷을 받은 경우)
				else if (this.rcvPacketOffset < this.rcvPacketLength)
				{
					// 패킷 길이를 받고 있던 경우 (패킷 길이를 알아야 버퍼를 만들든 할 수 있으므로 이거 중요함)
					if (this.rcvPacketLengthBuffer != null)
					{
						// 패킷 길이를 완성할 수 있는 경우
						if (Message.MESSAGE_LENGTH_TYPE_SIZE <= this.rcvPacketOffset + (recvBufferLength - recvBufferOffset))
						{
							// 패킷 길이 배열을 일단 완성
							int count = this.rcvPacketLength - this.rcvPacketOffset;
							Buffer.BlockCopy(args.Buffer, args.Offset + recvBufferOffset, this.rcvPacketLengthBuffer, this.rcvPacketOffset, count);
							recvBufferOffset += count;

							// 패킷 데이터 배열 새로 생성
							this.rcvPacketLength = BitConverter.ToInt32(this.rcvPacketLengthBuffer, 0) + Message.MESSAGE_LENGTH_TYPE_SIZE;
							this.rcvPacket = new byte[this.rcvPacketLength];
							Buffer.BlockCopy(this.rcvPacketLengthBuffer, 0, this.rcvPacket, 0, this.rcvPacketLengthBuffer.Length);
							this.rcvPacketOffset = this.rcvPacketLengthBuffer.Length;
							this.rcvPacketLengthBuffer = null;
						}
						// 여전히 패킷 길이를 완성하지 못한 경우 (계속 이렇게 보낼 거야?)
						else
						{
							// 패킷 길이 배열에 복사
							int count = recvBufferLength - recvBufferOffset;
							Buffer.BlockCopy(args.Buffer, args.Offset + recvBufferOffset, this.rcvPacketLengthBuffer, this.rcvPacketOffset, count);
							this.rcvPacketOffset += count;
							recvBufferOffset += count;
						}
					}
					// 패킷 본체를 받고 있는 경우
					else
					{
						int count = Math.Min(this.rcvPacketLength - this.rcvPacketOffset, recvBufferLength - recvBufferOffset);
						Buffer.BlockCopy(args.Buffer, args.Offset + recvBufferOffset, this.rcvPacket, this.rcvPacketOffset, count);
						this.rcvPacketOffset += count;
						recvBufferOffset += count;
					}
				}

				// 패킷이 완성됐으면 콜백 호출
				if (this.rcvPacketOffset == this.rcvPacketLength)
				{
					this.ReceivedCallback?.Invoke(this, new Message(this.rcvPacket, true));

					// 패킷을 새로 수신하기 위해 진행 정보 리셋
					this.rcvPacket = null;
					this.rcvPacketOffset = 0;
					this.rcvPacketLength = 0;
					this.rcvPacketLengthBuffer = null;
				}
			}

			// 다음 패킷 받기
			StartReceive();
		}

		public void SendData(byte[] data, bool dataIncludeHeader = false, bool reply = false)
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}(Length={data.Length}, dataIncludeHeader={dataIncludeHeader}, reply={reply})");

			try
			{
				Message message = new Message(data, dataIncludeHeader, reply);
				lock (this.sendQueue)
				{
					this.sendQueue.Enqueue(message);
				}
				StartSend();
			}
			catch (Exception ex)
			{
				this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		void StartSend()
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			Message message = null;

			lock (this.sendQueue)
			{
				// 큐에 보낼 패킷이 있으면 peek (꺼내지는 않고 참조만)
				if (0 < this.sendQueue.Count)
				{
					message = this.sendQueue.Peek();
					this.sndPacketLength = message.MessageLength;
				}
				// 큐에 보낼 패킷이 없으면 리턴
				else
				{
					return;
				}
			}

			// 전송 시작이면 버퍼에 패킷 복사 후 전송
			if (this.sndPacketOffset == 0 && this.sndBufferOffset == 0)
			{
				this.sndBufferLength = Math.Min(this.sndPacketLength - this.sndPacketOffset, this.sendArgs.Buffer.Length);
				Buffer.BlockCopy(message.MessageData, this.sndPacketOffset, this.sendArgs.Buffer, this.sndBufferOffset, this.sndBufferLength);
				this.sendArgs.SetBuffer(0, this.sndBufferLength);
			}
			// 전송 진행 중
			else if (0 < this.sndBufferOffset)
			{
				// 버퍼를 모두 소진했나?
				if (this.sndBufferOffset == this.sndBufferLength)
				{
					// 보낼 패킷 데이터가 남아있나?
					if (this.sndPacketOffset < this.sndPacketLength)
					{
						this.sndBufferOffset = 0;
						this.sndBufferLength = Math.Min(this.sndPacketLength - this.sndPacketOffset, this.sendArgs.Buffer.Length);
						Buffer.BlockCopy(message.MessageData, this.sndPacketOffset, this.sendArgs.Buffer, this.sndBufferOffset, this.sndBufferLength);
						this.sendArgs.SetBuffer(0, this.sndBufferLength);
					}
					// 패킷을 다 보냈으면 큐에서 제거
					else
					{
						this.sndPacketOffset = 0;
						this.sndPacketLength = 0;
						this.sndBufferOffset = 0;
						this.sndBufferLength = 0;

						lock (this.sendQueue)
						{
							this.sendQueue.Dequeue();
						}

						// 더 이상 보내지 않고 리턴
						// 보내기를 계속하는 건 ReceivedCompleted에서 처리
						return;
					}
				}
				// 보낼 버퍼가 아직 있나?
				else
				{
					// 남은 버퍼 부분을 전송
					this.sendArgs.SetBuffer(this.sndBufferOffset, this.sndBufferLength - this.sndBufferOffset);
				}
			}

			// 비동기 전송 시작
			try
			{
				bool pending = this.sendArgs.AcceptSocket.SendAsync(this.sendArgs);
				if (!pending)
				{
					SendCompleted(this, this.sendArgs);
				}
			}
			catch (Exception ex)
			{
				this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		void SendCompleted(object sender, SocketAsyncEventArgs args)
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			if (args.SocketError != SocketError.Success)
			{
				this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, Close socket: SocketError={args.SocketError}");
				CloseClientSocket(args);
				return;
			}

			if (args.BytesTransferred == 0)
			{
				this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, Close socket: BytesTransferred=0");
				CloseClientSocket(args);
				return;
			}

			// 보낸 데이터 크기만큼 오프셋 변경
			this.sndPacketOffset += args.BytesTransferred;
			this.sndBufferOffset += args.BytesTransferred;
			
			// 패킷 전송이 완료되면 콜백 호출
			if (this.sndPacketOffset == this.sndPacketLength)
			{
				this.SentCallback?.Invoke(this, this.sndPacketLength);
			}

			StartSend();
		}

		public void Disconnect()
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			try
			{
				this.receiveArgs.AcceptSocket?.Shutdown(SocketShutdown.Both);
				this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, Shutdown, Socket={this.receiveArgs.AcceptSocket?.GetHashCode()}");
			}
			catch
			{
			}
			this.receiveArgs.AcceptSocket?.Close();
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, Close, Socket={this.receiveArgs.AcceptSocket?.GetHashCode()}");
		}

		void CloseClientSocket(SocketAsyncEventArgs args)
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			try
			{
				args.AcceptSocket?.Shutdown(SocketShutdown.Both);
			}
			catch (Exception ex)
			{
				this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, Ignored, {ex.GetType().Name}, {ex.Message}");
			}

			args.AcceptSocket?.Close();

			this.ClosedCallback?.Invoke(this, args);
		}
	}
}
