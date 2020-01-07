using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace SimpleServer
{
	public class Connector
	{
		public event EventHandler<SocketAsyncEventArgs> ConnectedCallback;

		Socket socket;
		ILogger logger;
		
		public Connector(ILogger logger)
		{
			this.logger = logger;

			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
		}

		/// <summary>
		/// <para>서버에 접속</para>
		/// <para>접속 성공/실패 여부는 Completed에 등록한 콜백함수로 확인</para>
		/// </summary>
		/// <param name="ip"></param>
		/// <param name="port"></param>
		public void Connect(string ip, int port)
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
			saea.Completed += ConnectCompleted;
			saea.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

			bool pending = this.socket.ConnectAsync(saea);
			if (!pending)
			{
				this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, ConnectAsync completed synchronously");
				ConnectCompleted(null, saea);
			}
		}

		void ConnectCompleted(object sender, SocketAsyncEventArgs args)
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, SocketError={args.SocketError}, this.socket.GetHashCode()={this.socket.GetHashCode()}, args.ConnectSocket.GetHashCode()={args.ConnectSocket.GetHashCode()}");
			this.ConnectedCallback?.Invoke(this, args);
		}
	}
}
