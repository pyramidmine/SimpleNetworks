using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleServer
{
	public class Listener
	{
		public event EventHandler<SocketAsyncEventArgs> Completed;

		ILogger logger;
		Socket listenSocket;
		SocketAsyncEventArgs listenArgs;
		AutoResetEvent stopListeningEvent;
		AutoResetEvent acceptNextEvent;

		public Listener(ILogger logger)
		{
			this.logger = logger;

			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
		}

		public void Listen(string ip, int port, int backlogSize)
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, IP={ip}, Port={port}, BacklogSize={backlogSize}");

			try
			{
				this.listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				this.listenSocket.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
				this.listenSocket.Listen(backlogSize);

				this.listenArgs = new SocketAsyncEventArgs();
				this.listenArgs.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptCompleted);

				this.stopListeningEvent = new AutoResetEvent(false);
				this.acceptNextEvent = new AutoResetEvent(false);

				Thread listening = new Thread(StartAccept);
				listening.IsBackground = true;	// 메인쓰레드가 종료되면 Accept 쓰레드도 종료되도록 설정
				listening.Start();
			}
			catch (Exception ex)
			{
				this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		void StartAccept()
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			int STOP_EVENT_INDEX = 0;
			WaitHandle[] waitHandles = new WaitHandle[2] { this.stopListeningEvent, this.acceptNextEvent};

			do
			{
				bool pending;
				try
				{
					pending = this.listenSocket.AcceptAsync(this.listenArgs);
					this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, AcceptAsync()");
				}
				catch (Exception ex)
				{
					this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
					continue;
				}

				// 즉각 완료됐다면 콜백 메서드가 자동으로 호출되지 않으므로 직접 호출해야 함
				if (!pending)
				{
					AcceptCompleted(null, this.listenArgs);
				}
			} while (WaitHandle.WaitAny(waitHandles) != STOP_EVENT_INDEX);
		}

		void AcceptCompleted(object sender, SocketAsyncEventArgs args)
		{
			this.logger?.AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, {args.SocketError.ToString()}");

			try
			{
				if (args.SocketError == SocketError.Success)
				{
					Socket socket = args.AcceptSocket;
					args.AcceptSocket = null;

					SocketAsyncEventArgs newArgs = new SocketAsyncEventArgs();
					newArgs.AcceptSocket = socket;
					this.Completed?.Invoke(this, newArgs);
				}
				else
				{
					CloseClientSocket(args);
				}
			}
			finally
			{
				// 다음 Accept를 받을 수 있도록 이벤트 셋
				this.acceptNextEvent.Set();
			}
		}

		public void Stop()
		{
			this.stopListeningEvent?.Set();
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
