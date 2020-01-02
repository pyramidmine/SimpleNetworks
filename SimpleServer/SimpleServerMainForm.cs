using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace SimpleServer
{
	public partial class SimpleServerMainForm : Form
	{
		readonly int MAX_LOG_ROWS = 4096;

		Socket socket;
		Socket clientSocket;
		SocketAsyncEventArgs saea;
		CancellationTokenSource cts;
		AutoResetEvent listenEvent;
		Thread listenThread;

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
				this.ctrlLog.Items.Add(log);
			}
		}

		private void SimpleServerMainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			this.cts.Cancel();
			this.listenEvent.Set();
		}

		private void SimpleServerMainForm_FormClosed(object sender, FormClosedEventArgs e)
		{
			Properties.Settings.Default.Save();
		}

		private void buttonListen_Click(object sender, EventArgs e)
		{
			try
			{
				this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				this.socket.Bind(new IPEndPoint(IPAddress.Parse(Properties.Settings.Default.ServerIp), Properties.Settings.Default.ServerPort));
				this.socket.Listen(Properties.Settings.Default.BacklogSize);
				this.saea = new SocketAsyncEventArgs();
				this.saea.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptCompleted);
				this.listenEvent = new AutoResetEvent(false);
				this.cts = new CancellationTokenSource();
				this.listenThread = new Thread(() => RepeatAccept(this.socket, this.saea, this.cts.Token));
				this.listenThread.Start();
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
		/// <param name="socket"></param>
		/// <param name="saea"></param>
		/// <param name="ct"></param>
		void RepeatAccept(Socket socket, SocketAsyncEventArgs saea, CancellationToken ct)
		{
			AddLog($"{MethodBase.GetCurrentMethod().Name}");

			while (!ct.IsCancellationRequested)
			{
				// SocketAsyncEventArgs 재활용
				saea.AcceptSocket = null;

				bool pending = true;
				try
				{
					pending = socket.AcceptAsync(saea);
					AddLog($"{MethodBase.GetCurrentMethod().Name}, AcceptAsync()");
				}
				catch (Exception ex)
				{
					AddLog($"{MethodBase.GetCurrentMethod().Name}, {ex.GetType().Name}, {ex.Message}");
					continue;
				}

				// 동기 완료됐다면 콜백 메서드가 자동으로 호출되지 않으므로 직접 호출해야 함
				if (!pending)
				{
					AcceptCompleted(this, saea);
				}

				// AcceptAsync는 동기/비동기 상관없이 즉시 반환하므로 while 루프가 계속 실행되는 문제가 있음
				// 이런 상황을 방지하기 위해 오토리셋이벤트를 기다리는 코드를 추가
				// 오토리셋이벤트는 AcceptCompleted에서 셋
				this.listenEvent.WaitOne();
			}
		}

		void AcceptCompleted(object sender, SocketAsyncEventArgs e)
		{
			if (e.SocketError == SocketError.Success)
			{
				this.clientSocket = e.AcceptSocket;
				AddLog($"{MethodBase.GetCurrentMethod().Name}, ClientSocket={e.AcceptSocket.Handle}");
			}
			else
			{
				AddLog($"{MethodBase.GetCurrentMethod().Name}, {e.SocketError.ToString()}");
			}

			this.listenEvent.Set();
		}
	}
}
