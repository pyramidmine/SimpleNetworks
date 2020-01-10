using System;
using System.Text;

namespace SimpleServer
{
	/// <summary>
	/// 기존 전문
	/// </summary>
	public class Message
	{
		// 패킷 길이를 나타내는 데이터의 사이즈
		public static readonly int MESSAGE_LENGTH_TYPE_SIZE = 4;
		// 메시지 길이 (전체 길이)
		public int MessageLength { get; private set; }
		public bool MessageReply { get; private set; }
		// 메시지 데이터 (패킷 헤더는 포함하지 않음)
		public byte[] MessageData { get; private set; }

		// 회신 데이터 (현재는 '10000' 고정)
		private static readonly string FixedReplyData = "10000";
		private static readonly byte[] FixedReplyDataBytes = Encoding.UTF8.GetBytes("10000");

		public Message(byte[] data, bool dataIncludeHeader = false, bool reply = false)
		{
			//
			// 본문 길이 계산
			//
			int bodyLength = data.Length;
			{
				// 헤더가 포함되어 있으면 헤더 길이 제외
				bodyLength -= (dataIncludeHeader ? MESSAGE_LENGTH_TYPE_SIZE : 0);

				// 회신 전문은 회신 데이터 포함
				bodyLength += (reply ? FixedReplyDataBytes.Length : 0);
			}

			this.MessageLength = bodyLength + MESSAGE_LENGTH_TYPE_SIZE;
			this.MessageData = new byte[this.MessageLength];
			int srcDataOffset = 0;
			int tgtDataOffset = 0;

			//
			// 헤더 쓰기
			//
			if (!dataIncludeHeader || reply)
			{
				// 기존 데이터에 헤더가 없거나, 회신 데이터가 추가되는 경우
				var v = BitConverter.GetBytes(bodyLength);
				Buffer.BlockCopy(v, 0, this.MessageData, tgtDataOffset, v.Length);
				tgtDataOffset += v.Length;

				// 기존 데이터의 오프셋만 이동
				if (dataIncludeHeader)
				{
					srcDataOffset += MESSAGE_LENGTH_TYPE_SIZE;
				}
			}
			else
			{
				// 기존 데이터의 헤더를 복사
				Buffer.BlockCopy(data, srcDataOffset, this.MessageData, tgtDataOffset, MESSAGE_LENGTH_TYPE_SIZE);
				srcDataOffset += MESSAGE_LENGTH_TYPE_SIZE;
				tgtDataOffset += MESSAGE_LENGTH_TYPE_SIZE;
			}

			// 회신 데이터가 있는지 확인
			if (FixedReplyDataBytes.Length <= (data.Length - srcDataOffset))
			{
				string replyData = Encoding.UTF8.GetString(data, srcDataOffset, FixedReplyDataBytes.Length);
				if (string.Compare(FixedReplyData, replyData) == 0)
				{
					this.MessageReply = true;
				}
			}
			
			// 회신 데이터 쓰기
			if (reply && !this.MessageReply)
			{
				Buffer.BlockCopy(FixedReplyDataBytes, 0, this.MessageData, tgtDataOffset, FixedReplyDataBytes.Length);
				tgtDataOffset += FixedReplyDataBytes.Length;
				this.MessageReply = true;
			}

			// 메시지 본체 쓰기
			Buffer.BlockCopy(data, srcDataOffset, this.MessageData, tgtDataOffset, data.Length - srcDataOffset);

			// 메시지 정합성 검증
			{
				// 메시지 길이
				int messageLength = BitConverter.ToInt32(this.MessageData, 0);
				if (messageLength != (this.MessageLength - MESSAGE_LENGTH_TYPE_SIZE))
				{
					throw new InvalidOperationException($"Message length mismatch, Member:{this.MessageLength}, Data:{messageLength}");
				}

				// 회신 데이터 여부
				bool isReply = false;
				if (MESSAGE_LENGTH_TYPE_SIZE + FixedReplyDataBytes.Length <= messageLength)
				{
					string replyData = Encoding.UTF8.GetString(this.MessageData, MESSAGE_LENGTH_TYPE_SIZE, FixedReplyDataBytes.Length);
					isReply = (string.Compare(FixedReplyData, replyData) == 0);
					if (this.MessageReply != isReply)
					{
						throw new InvalidOperationException($"Message reply mismatch, Member:{this.MessageReply}, Data:{isReply}");
					}
				}
				else
				{
					if (this.MessageReply != isReply)
					{
						throw new InvalidOperationException($"Message reply mismatch, Member:{this.MessageReply}, Data:{isReply}(DEFAULT)");
					}
				}
			}
		}
	}
}
