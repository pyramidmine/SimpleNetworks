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
		public event EventHandler<Packet> ReceivedCallback;
		// 패킷을 보냈을 때 호출되는 콜백
		public event EventHandler<int> SentCallback;
		// 소켓이 닫힐 때 호출되는 콜백
		public event EventHandler<SocketAsyncEventArgs> ClosedCallback;
				
		SocketAsyncEventArgs receiveArgs;
		SocketAsyncEventArgs sendArgs;
		ILogger logger;

		Queue<Packet> sendQueue = new Queue<Packet>();
		int sendPacketOffset;
		int sendPacketLength;
		int sendBufferOffset;
		int sendBufferLength;

		byte[] recvPacket;
		int recvPacketOffset;
		int recvPacketLength;
		byte[] recvPacketLengthBuffer;
		
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
				if (this.recvPacketOffset == 0)
				{
					// 버퍼에 남은 부분이 패킷 길이 이상인가?
					if (Packet.PACKET_LENGTH_TYPE_SIZE <= (recvBufferLength - recvBufferOffset))
					{
						// 받아야 하는 패킷 전체 길이만큼 버퍼 생성하고 복사
						this.recvPacketLength = BitConverter.ToInt32(args.Buffer, args.Offset + recvBufferOffset);
						this.recvPacket = new byte[this.recvPacketLength];
						int count = Math.Min(this.recvPacketLength, (recvBufferLength - recvBufferOffset));
						Buffer.BlockCopy(args.Buffer, args.Offset + recvBufferOffset, this.recvPacket, this.recvPacketOffset, count);
						this.recvPacketOffset += count;
						recvBufferOffset += count;
					}
					else
					{
						// 패킷 길이 정보를 저장하기 시작
						this.recvPacketLengthBuffer = new byte[Packet.PACKET_LENGTH_TYPE_SIZE];
						int count = Math.Min(this.recvPacketLengthBuffer.Length, (recvBufferLength - recvBufferOffset));
						Buffer.BlockCopy(args.Buffer, args.Offset + recvBufferOffset, this.recvPacketLengthBuffer, this.recvPacketOffset, count);
						this.recvPacketLength = Packet.PACKET_LENGTH_TYPE_SIZE;
						this.recvPacketOffset += count;
						recvBufferOffset += count;
					}
				}
				// 수신 데이터를 모두 소진하지 못한 경우 (패킷을 받는 중이거나 한 번에 여러 개의 패킷을 받은 경우)
				else if (this.recvPacketOffset < this.recvPacketLength)
				{
					// 패킷 길이를 받고 있던 경우 (패킷 길이를 알아야 버퍼를 만들든 할 수 있으므로 이거 중요함)
					if (this.recvPacketLengthBuffer != null)
					{
						// 패킷 길이를 완성할 수 있는 경우
						if (Packet.PACKET_LENGTH_TYPE_SIZE <= this.recvPacketOffset + (recvBufferLength - recvBufferOffset))
						{
							// 패킷 길이 배열을 일단 완성
							int count = this.recvPacketLength - this.recvPacketOffset;
							Buffer.BlockCopy(args.Buffer, args.Offset + recvBufferOffset, this.recvPacketLengthBuffer, this.recvPacketOffset, count);
							recvBufferOffset += count;

							// 패킷 데이터 배열 새로 생성
							this.recvPacketLength = BitConverter.ToInt32(this.recvPacketLengthBuffer, 0);
							this.recvPacket = new byte[this.recvPacketLength];
							Buffer.BlockCopy(this.recvPacketLengthBuffer, 0, this.recvPacket, 0, this.recvPacketLengthBuffer.Length);
							this.recvPacketOffset = this.recvPacketLengthBuffer.Length;
							this.recvPacketLengthBuffer = null;
						}
						// 여전히 패킷 길이를 완성하지 못한 경우 (계속 이렇게 보낼 거야?)
						else
						{
							// 패킷 길이 배열에 복사
							int count = recvBufferLength - recvBufferOffset;
							Buffer.BlockCopy(args.Buffer, args.Offset + recvBufferOffset, this.recvPacketLengthBuffer, this.recvPacketOffset, count);
							this.recvPacketOffset += count;
							recvBufferOffset += count;
						}
					}
					// 패킷 본체를 받고 있는 경우
					else
					{
						int count = Math.Min(this.recvPacketLength - this.recvPacketOffset, recvBufferLength - recvBufferOffset);
						Buffer.BlockCopy(args.Buffer, args.Offset + recvBufferOffset, this.recvPacket, this.recvPacketOffset, count);
						this.recvPacketOffset += count;
						recvBufferOffset += count;
					}
				}

				// 패킷이 완성됐으면 콜백 호출
				if (this.recvPacketOffset == this.recvPacketLength)
				{
					this.ReceivedCallback?.Invoke(this, new Packet(this.recvPacket));

					// 패킷을 새로 수신하기 위해 진행 정보 리셋
					this.recvPacket = null;
					this.recvPacketOffset = 0;
					this.recvPacketLength = 0;
					this.recvPacketLengthBuffer = null;
				}
			}

			// 다음 패킷 받기
			StartReceive();
		}

		public void SendData(short packetType, byte packetVersion, PacketDataType packetDataType, byte[] buffer)
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}({packetType}, {packetVersion}, {packetDataType})");

			try
			{
				Packet packet = new Packet(packetType, packetVersion, packetDataType, buffer);
				lock (this.sendQueue)
				{
					this.sendQueue.Enqueue(packet);
					if (this.sendQueue.Count == 1)
					{
						StartSend();
					}
				}
			}
			catch (Exception ex)
			{
				this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		void StartSend()
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			Packet packet = null;

			lock (this.sendQueue)
			{
				// 큐에 보낼 패킷이 있으면 peek (꺼내지는 않고 참조만)
				if (0 < this.sendQueue.Count)
				{
					packet = this.sendQueue.Peek();
					this.sendPacketLength = packet.PacketLength;
				}
				// 큐에 보낼 패킷이 없으면 리턴
				else
				{
					return;
				}
			}

			// 전송 시작이면 버퍼에 패킷 복사 후 전송
			if (this.sendPacketOffset == 0 && this.sendBufferOffset == 0)
			{
				this.sendBufferLength = Math.Min(this.sendPacketLength - this.sendPacketOffset, this.sendArgs.Buffer.Length);
				Buffer.BlockCopy(packet.PacketData, this.sendPacketOffset, this.sendArgs.Buffer, this.sendBufferOffset, this.sendBufferLength);
				this.sendArgs.SetBuffer(0, this.sendBufferLength);
			}
			// 전송 진행 중
			else if (0 < this.sendBufferOffset)
			{
				// 버퍼를 모두 소진했나?
				if (this.sendBufferOffset == this.sendBufferLength)
				{
					// 보낼 패킷 데이터가 남아있나?
					if (this.sendPacketOffset < this.sendPacketLength)
					{
						this.sendBufferOffset = 0;
						this.sendBufferLength = Math.Min(this.sendPacketLength - this.sendPacketOffset, this.sendArgs.Buffer.Length);
						Buffer.BlockCopy(packet.PacketData, this.sendPacketOffset, this.sendArgs.Buffer, this.sendBufferOffset, this.sendBufferLength);
						this.sendArgs.SetBuffer(0, this.sendBufferLength);
					}
					// 패킷을 다 보냈으면 큐에서 제거
					else
					{
						this.sendPacketOffset = 0;
						this.sendPacketLength = 0;
						this.sendBufferOffset = 0;
						this.sendBufferLength = 0;

						lock (this.sendQueue)
						{
							this.sendQueue.Dequeue();
						}

						StartSend();
						return;
					}
				}
				// 보낼 버퍼가 아직 있나?
				else
				{
					// 남은 버퍼 부분을 전송
					this.sendArgs.SetBuffer(this.sendBufferOffset, this.sendBufferLength - this.sendBufferOffset);
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
			this.sendPacketOffset += args.BytesTransferred;
			this.sendBufferOffset += args.BytesTransferred;
			
			// 패킷 전송이 완료되면 콜백 호출
			if (this.sendPacketOffset == this.sendPacketLength)
			{
				this.SentCallback?.Invoke(this, this.sendPacketLength);
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
