using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimpleServer
{
	public partial class SimpleServerMainForm : Form
	{
		readonly int MAX_LOG_ROWS = 4096;

		Socket socket;
		Socket clientSocket;
		SocketAsyncEventArgs saea;
		AutoResetEvent listenEvent, listenEventStop;
		Thread listenThread;

		SocketAsyncEventArgs receiveArgs;
		SocketAsyncEventArgs sendArgs;

		public SimpleServerMainForm()
		{
			InitializeComponent();
		}

		void AddLog(string log)
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
			if (this.listenEventStop != null)
			{
				this.listenEventStop.Set();
			}
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
				this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				this.socket.Bind(new IPEndPoint(IPAddress.Parse(Properties.Settings.Default.ServerIp), Properties.Settings.Default.ServerPort));
				this.socket.Listen(Properties.Settings.Default.BacklogSize);
				this.saea = new SocketAsyncEventArgs();
				this.saea.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptCompleted);
				this.listenEvent = new AutoResetEvent(false);
				this.listenEventStop = new AutoResetEvent(false);
				this.listenThread = new Thread(StartAccept);
				this.listenThread.Start();
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

		/// <summary>
		/// AcceptAsync를 반복하기 위한 래퍼 메서드
		/// </summary>
		/// <remarks>
		/// - AcceptAsync는 비동기 또는 동기로 완료될 수 있음
		/// - 비동기로 완료되면, 콜백 메서드가 자동으로 호출되며, 이때 다시 AcceptAsync를 호출하면 무한 반복할 수 있으므로 문제 없음
		/// - 동기로 완료되면, 콜백 메서드가 호출되지 않으며, 수동으로 AcceptAsync를 호출해야 함. 따라서 콜백 메서드만으로 무한 반복할 수 없음
		/// - 동기 완료를 처리하기 위해 비동기/동기 완료 모두 처리할 수 있도록, 루프를 돌면서 AcceptAsync 호출해서, 비동기 완료는 콜백 메서드가, 동기 완료는 이 메서드에서 처리
		/// </remarks>
		void StartAccept()
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}");

			// 루프와 종료 이벤트
			int STOP_EVENT_INDEX = 0;
			WaitHandle[] waitHandles = new WaitHandle[2] { this.listenEventStop, this.listenEvent };

			do
			{
				bool pending;
				try
				{
					pending = this.socket.AcceptAsync(this.saea);
					AddLog($"{MethodBase.GetCurrentMethod().Name}, AcceptAsync()");
				}
				catch (Exception ex)
				{
					AddLog($"{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
					continue;
				}

				// 즉각 완료됐다면 콜백 메서드가 자동으로 호출되지 않으므로 직접 호출해야 함
				if (!pending)
				{
					AcceptCompleted(null, this.saea);
				}
			} while (WaitHandle.WaitAny(waitHandles) != STOP_EVENT_INDEX);
		}

		/// <summary>
		/// AcceptAsync 호출이 비동기 완료됐을 때 호출되는 콜백 메서드
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		void AcceptCompleted(object sender, SocketAsyncEventArgs args)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}, {args.SocketError.ToString()}");

			try
			{
				if (args.SocketError == SocketError.Success)
				{
					this.clientSocket = args.AcceptSocket;
					args.AcceptSocket = null;   // SocketAsyncEventArgs 재활용

					this.receiveArgs = new SocketAsyncEventArgs();
					this.receiveArgs.SetBuffer(new byte[Properties.Settings.Default.BufferSize], 0, Properties.Settings.Default.BufferSize);
					this.receiveArgs.AcceptSocket = this.clientSocket;
					this.receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(ReceiveCompleted);

					Task.Factory.StartNew(() => StartReceive(this.receiveArgs));
				}
				else
				{
					CloseClientSocket(args);
				}
			}
			finally
			{
				// 다음 Accept를 받을 수 있도록 이벤트 셋
				this.listenEvent.Set();
			}
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
