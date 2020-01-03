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

		Listener listener;
		List<Session> sessions = new List<Session>();

		Socket socket;
		Socket clientSocket;
		SocketAsyncEventArgs saea;

		SocketAsyncEventArgs receiveArgs;
		SocketAsyncEventArgs sendArgs;

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

			//
			// Listen directly
			//
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
				this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				this.saea = new SocketAsyncEventArgs();
				this.saea.AcceptSocket = this.socket;
				this.saea.Completed += new EventHandler<SocketAsyncEventArgs>(ConnectCompleted);
				this.saea.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(Properties.Settings.Default.ServerIp), Properties.Settings.Default.ServerPort);
				StartConnect(this.saea);
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
				if (this.sendArgs.AcceptSocket != null)
				{
					byte[] sourceBuffer = new byte[Properties.Settings.Default.DataSize];
					for (int i = 0; i < sourceBuffer.Length; i++)
					{
						sourceBuffer[i] = (byte)((i + 1) % 10);
					}
					Buffer.BlockCopy(sourceBuffer, 0, this.sendArgs.Buffer, 0, sourceBuffer.Length);
					this.sendArgs.SetBuffer(0, sourceBuffer.Length);
					StartSend(this.sendArgs);
				}
				else
				{
					AddLog($"{MethodBase.GetCurrentMethod().Name}, Socket=null");
				}
			}
			catch (Exception ex)
			{
				AddLog($"{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
			}
		}

		void ListenCompleted(object sender, SocketAsyncEventArgs args)
		{
			AddLog($"{this.GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

			Session session = new Session(args.AcceptSocket, Properties.Settings.Default.BufferSize, this);
			this.sessions.Add(session);
			Task.Factory.StartNew(session.StartReceive);
		}

		void StartConnect(SocketAsyncEventArgs args)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}");

			bool pending = args.AcceptSocket.ConnectAsync(args);
			if (!pending)
			{
				ConnectCompleted(null, args);
			}
		}

		void ConnectCompleted(object sender, SocketAsyncEventArgs args)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}, SocketError={args.SocketError}");

			if (args.SocketError == SocketError.Success)
			{
				this.receiveArgs = new SocketAsyncEventArgs();
				this.receiveArgs.AcceptSocket = args.AcceptSocket;
				this.receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(ReceiveCompleted);
				this.receiveArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(Properties.Settings.Default.ServerIp), Properties.Settings.Default.ServerPort);
				this.receiveArgs.SetBuffer(new byte[Properties.Settings.Default.BufferSize], 0, Properties.Settings.Default.BufferSize);
				this.sendArgs = new SocketAsyncEventArgs();
				this.sendArgs.AcceptSocket = args.AcceptSocket;
				this.sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(SendCompleted);
				this.sendArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(Properties.Settings.Default.ServerIp), Properties.Settings.Default.ServerPort);
				this.sendArgs.SetBuffer(new byte[Properties.Settings.Default.BufferSize], 0, Properties.Settings.Default.BufferSize);
				StartReceive(this.receiveArgs);
			}
			else
			{
				// ConnectionRefused: 리스닝 하고 있지 않은 포트로 커넥트 시도
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
