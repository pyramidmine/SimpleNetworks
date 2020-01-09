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
			AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			try
			{
				this.listener = new Listener(this);
				this.listener.AcceptedCallback += new EventHandler<SocketAsyncEventArgs>(AcceptedCallback);
				this.listener.Listen(Properties.Settings.Default.ServerIp, Properties.Settings.Default.ServerPort, Properties.Settings.Default.BacklogSize);
			}
			catch (Exception ex)
			{
				AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		private void buttonConnect_Click(object sender, EventArgs e)
		{
			AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			try
			{
				this.connector = new Connector(this);
				this.connector.ConnectedCallback += new EventHandler<SocketAsyncEventArgs>(ConnectedCallback);
				this.connector.Connect(Properties.Settings.Default.ServerIp, Properties.Settings.Default.ServerPort);
			}
			catch (Exception ex)
			{
				AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		private void buttonSend_Click(object sender, EventArgs e)
		{
			AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			try
			{
				byte[] sourceBuffer = new byte[Properties.Settings.Default.DataSize];
				for (int i = 0; i < sourceBuffer.Length; i++)
				{
					sourceBuffer[i] = (byte)((i + 1) % 10);
				}

				if (this.session != null)
				{
					this.session.SendData(10, 1, PacketDataType.PlainText, sourceBuffer);
				}
				else if (0 < this.sessions.Count)
				{
					this.sessions[0].SendData(11, 1, PacketDataType.PlainText, sourceBuffer);
				}
				else
				{
					AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, Session or Sessions are null.");
				}
			}
			catch (Exception ex)
			{
				AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		private void buttonDisconnect_Click(object sender, EventArgs e)
		{
			AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			if (this.session != null)
			{
				this.session.Disconnect();
			}
			else
			{
				lock (this.sessions)
				{
					foreach (var s in this.sessions)
					{
						s.Disconnect();
					}
				}
			}
		}

		void AcceptedCallback(object sender, SocketAsyncEventArgs args)
		{
			AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			Session newSession = new Session(args.AcceptSocket, Properties.Settings.Default.BufferSize, this);
			newSession.ClosedCallback += new EventHandler<SocketAsyncEventArgs>(ClosedCallback);
			this.sessions.Add(newSession);
			AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, Session={newSession.GetHashCode()}, Accepted.");
			Task.Factory.StartNew(newSession.StartReceive);
		}

		void ConnectedCallback(object sender, SocketAsyncEventArgs args)
		{
			AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, SocketError={args.SocketError}");

			if (args.SocketError == SocketError.Success)
			{
				this.session = new Session(args.ConnectSocket, Properties.Settings.Default.BufferSize, this);
				this.session.ReceivedCallback += new EventHandler<SocketAsyncEventArgs>(ReceivedCallback);
				this.session.SentCallback += new Session.SentCallbackHandler(SentCallback);
				this.session.ClosedCallback += new EventHandler<SocketAsyncEventArgs>(ClosedCallback);
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

		void ReceivedCallback(object sender, SocketAsyncEventArgs args)
		{
			AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, SocketError={args.SocketError}, BytesTransferred={args.BytesTransferred}");
		}

		void SentCallback(object sender, int bytesTransferred)
		{
			AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, BytesTransferred={bytesTransferred}");
		}

		void ClosedCallback(object sender, SocketAsyncEventArgs args)
		{
			AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			Session closedSession = (Session)sender;
			if (this.sessions.Remove(closedSession))
			{
				AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}, Session={closedSession.GetHashCode()}, Closed.");
			}
		}

	}
}
