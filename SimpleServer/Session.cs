﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SimpleServer
{
	class Session
	{
		SocketAsyncEventArgs receiveArgs;
		SocketAsyncEventArgs sendArgs;
		ILogger logger;

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
		}
	}
}