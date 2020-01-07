using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimpleServer
{
	public partial class SimpleServerMainForm : Form, ILogger
	{
		readonly int MAX_LOG_ROWS = 4096;

		// Listener (for Server)
		Listener listener;
		List<Session> sessions = new List<Session>();

		// Connector (for Client)
		Connector connector;
		Session session;

		public SimpleServerMainForm()
		{
			InitializeComponent();
		}

		public void AddLog(string log)
		{
			if (this.ctrlLog.InvokeRequired)
			{
				// UI 쓰레드가 아닌 쓰레드에서 호출했을 때: UI 쓰레드에게 처리해 달라고 요청
				this.ctrlLog.Invoke(new Action(() => AddLog(log)));
			}
			else
			{
				// UI 쓰레드라면: 최대 로그 갯수 유지하면서 로그 추가
				if (MAX_LOG_ROWS < this.ctrlLog.Items.Count)
				{
					this.ctrlLog.Items.RemoveAt(0);
				}
				this.ctrlLog.Items.Add(string.Format($"{DateTime.Now:HH:mm:ss} {log}"));
				this.ctrlLog.TopIndex = this.ctrlLog.Items.Count - 1;
			}
		}

		private void SimpleServerMainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			this.listener?.Stop();
		}

		private void SimpleServerMainForm_FormClosed(object sender, FormClosedEventArgs e)
		{
			Properties.Settings.Default.Save();
		}

		private void buttonListen_Click(object sender, EventArgs e)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}");

			try
			{
				this.listener = new Listener(this);
				this.listener.Completed += new EventHandler<SocketAsyncEventArgs>(ListenCompleted);
				this.listener.Listen(Properties.Settings.Default.ServerIp, Properties.Settings.Default.ServerPort, Properties.Settings.Default.BacklogSize);
			}
			catch (Exception ex)
			{
				AddLog($"{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		private void buttonConnect_Click(object sender, EventArgs e)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}");

			try
			{
				this.connector = new Connector(this);
				this.connector.Completed += new EventHandler<SocketAsyncEventArgs>(ConnectCompleted);
				this.connector.Connect(Properties.Settings.Default.ServerIp, Properties.Settings.Default.ServerPort);
			}
			catch (Exception ex)
			{
				AddLog($"{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		private void buttonSend_Click(object sender, EventArgs e)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}");

			try
			{
				if (this.session != null)
				{
					byte[] sourceBuffer = new byte[Properties.Settings.Default.DataSize];
					for (int i = 0; i < sourceBuffer.Length; i++)
					{
						sourceBuffer[i] = (byte)((i + 1) % 10);
					}
					this.session.SendData(sourceBuffer);
				}
				else
				{
					AddLog($"{MethodBase.GetCurrentMethod().Name}, Session is null.");
				}
			}
			catch (Exception ex)
			{
				AddLog($"{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		void ListenCompleted(object sender, SocketAsyncEventArgs args)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}");

			Session newSession = new Session(args.AcceptSocket, Properties.Settings.Default.BufferSize, this);
			newSession.ClosedCallback += new EventHandler<SocketAsyncEventArgs>(ClosedCallback);
			this.sessions.Add(newSession);
			AddLog($"{MethodBase.GetCurrentMethod().Name}, Session={newSession.GetHashCode()}, Accepted.");
			Task.Factory.StartNew(newSession.StartReceive);
		}

		void ConnectCompleted(object sender, SocketAsyncEventArgs args)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}, SocketError={args.SocketError}");

			if (args.SocketError == SocketError.Success)
			{
				this.session = new Session(args.ConnectSocket, Properties.Settings.Default.BufferSize, this);
				Task.Factory.StartNew(this.session.StartReceive);
			}
			else
			{
				try
				{
					args.ConnectSocket?.Shutdown(SocketShutdown.Both);
				}
				catch
				{
				}
				args.ConnectSocket?.Close();
			}
		}

		void StartReceive(SocketAsyncEventArgs args)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}");

			try
			{
				bool pending = args.AcceptSocket.ReceiveAsync(args);
				if (!pending)
				{
					ReceiveCompleted(null, args);
				}
			}
			catch (Exception ex)
			{
				AddLog($"{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		void ReceiveCompleted(object sender, SocketAsyncEventArgs args)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}");

			if (args.SocketError != SocketError.Success)
			{
				AddLog($"{MethodBase.GetCurrentMethod().Name}, Close socket: SocketError={args.SocketError}");
				CloseClientSocket(args);
				return;
			}

			if (args.BytesTransferred == 0)
			{
				AddLog($"{MethodBase.GetCurrentMethod().Name}, Close socket: BytesTransferred=0");
				CloseClientSocket(args);
				return;
			}

			// 패킷 표시용 스트링
			StringBuilder sb = new StringBuilder(args.BytesTransferred * 2);
			for (int i = 0; i < args.BytesTransferred; i++)
			{
				sb.AppendFormat($"{args.Buffer[i]:x2}");
			}

			AddLog($"{MethodBase.GetCurrentMethod().Name}, BytesTransferred={args.BytesTransferred}, Packet={sb.ToString()}");
			StartReceive(args);
		}

		void StartSend(SocketAsyncEventArgs args)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}");

			try
			{
				bool pending = args.AcceptSocket.SendAsync(args);
				if (!pending)
				{
					SendCompleted(null, args);
				}
			}
			catch (Exception ex)
			{
				AddLog($"{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
				CloseClientSocket(args);
			}
		}

		void SendCompleted(object sender, SocketAsyncEventArgs args)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}, SocketError={args.SocketError}, BytesTransferred={args.BytesTransferred}");
		}

		void ClosedCallback(object sender, SocketAsyncEventArgs args)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}");

			Session closedSession = (Session)sender;
			if (this.sessions.Remove(closedSession))
			{
				AddLog($"{MethodBase.GetCurrentMethod().Name}, Session={closedSession.GetHashCode()}, Closed.");
			}
		}

		/// <summary>
		/// 클라이언트 소켓을 닫고 관련된 자원을 해제
		/// </summary>
		/// <param name="args"></param>
		void CloseClientSocket(SocketAsyncEventArgs args)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}");

			try
			{
				args.AcceptSocket.Shutdown(SocketShutdown.Both);
			}
			catch (Exception ex)
			{
				AddLog($"{MethodBase.GetCurrentMethod().Name}, Ignored, {ex.GetType().Name}, {ex.Message}");
			}

			if (args.AcceptSocket != null)
			{
				args.AcceptSocket.Close();
			}
		}
	}
}
