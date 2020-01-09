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
		public event EventHandler<SocketAsyncEventArgs> ReceivedCallback;
		public event EventHandler<SocketAsyncEventArgs> SentCallback;
		public event EventHandler<SocketAsyncEventArgs> ClosedCallback;

		SocketAsyncEventArgs receiveArgs;
		SocketAsyncEventArgs sendArgs;
		ILogger logger;

		Queue<Packet> sendQueue = new Queue<Packet>();
		int sendPacketOffset = 0;
		int sendPacketLength = 0;
		int sendBufferOffset = 0;
		int sendBufferLength = 0;

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

			this.ReceivedCallback?.Invoke(this, args);

			// 패킷 표시용 스트링
			StringBuilder sb = new StringBuilder(args.BytesTransferred * 2);
			for (int i = 0; i < args.BytesTransferred; i++)
			{
				sb.AppendFormat($"{args.Buffer[i]:x2}");
			}
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, BytesTransferred={args.BytesTransferred}, Packet={sb.ToString()}");

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
						lock (this.sendQueue)
						{
							this.sendQueue.Dequeue();
						}
						this.sendPacketOffset = 0;
						this.sendPacketLength = 0;
						this.sendBufferOffset = 0;
						this.sendBufferLength = 0;

						// 남은 큐를 처리할 수 있도록 재귀호출
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
				this.SentCallback?.Invoke(this, args);
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
