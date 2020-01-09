using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleServer
{
	enum PacketDataType : byte
	{
		// 콘텐츠 타입
		PlainText = 0x01,
		JSON = 0x02,
		// 암호화
		Encrypted = 0x20,
		// 압축
		Compressed = 0x40
	}

	class Packet
	{
		public static readonly int PACKET_LENGTH_TYPE_SIZE = 4;
		public static readonly int PACKET_TYPE_POSITION = 4;
		public static readonly int PACKET_VERSION_POSITION = 6;
		public static readonly int PACKET_DATA_TYPE_POSITION = 7;

		public int PacketLength { get; private set; }
		public short PacketType { get; private set; }
		public byte PacketVersion { get; private set; }
		public PacketDataType PacketDataType { get; private set; }
		public byte[] PacketData { get; private set; }

		static readonly int PACKET_HEADER_SIZE = 4 + 2 + 1 + 1;
		
		public Packet(short packetType, byte packetVersion, PacketDataType packetDataType, byte[] dataBody)
		{
			this.PacketLength = dataBody.Length + PACKET_HEADER_SIZE;
			this.PacketType = packetType;
			this.PacketVersion = packetVersion;
			this.PacketDataType = packetDataType;
			
			this.PacketData = new byte[this.PacketLength];
			int offset = 0;

			// 패킷 사이즈 (전체)
			{
				var v = BitConverter.GetBytes(this.PacketLength);
				Buffer.BlockCopy(v, 0, this.PacketData, offset, v.Length);
				offset += v.Length;
			}

			// 패킷 타입 (전문 종류)
			{
				var v = BitConverter.GetBytes(this.PacketType);
				Buffer.BlockCopy(v, 0, this.PacketData, offset, v.Length);
				offset += v.Length;
			}

			// 패킷 버전
			this.PacketData[offset] = this.PacketVersion;
			offset++;

			// 패킷 데이터 타입
			this.PacketData[offset] = (byte)this.PacketDataType;
			offset++;

			// 패킷 본문
			Buffer.BlockCopy(dataBody, 0, this.PacketData, offset, dataBody.Length);
		}

		/// <summary>
		/// 패킷 데이터 전체를 받아서 패킷 객체 생성
		/// </summary>
		/// <param name="data">패킷 헤더가 포함된 패킷 데이터 전체</param>
		public Packet(byte[] data)
		{
			this.PacketData = new byte[data.Length];
			Buffer.BlockCopy(data, 0, this.PacketData, 0, this.PacketData.Length);

			this.PacketLength = BitConverter.ToInt32(this.PacketData, 0);
			Debug.Assert(this.PacketLength == this.PacketData.Length);
			this.PacketType = BitConverter.ToInt16(this.PacketData, PACKET_TYPE_POSITION);
			this.PacketVersion = this.PacketData[PACKET_VERSION_POSITION];
			this.PacketDataType = (PacketDataType)this.PacketData[PACKET_DATA_TYPE_POSITION];
		}
	}
}
