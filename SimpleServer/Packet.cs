using System;
using System.Collections.Generic;
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
		// 해싱
		Hashed = 0x10,
		// 암호화
		Encrypted = 0x20,
		// 압축
		Compressed = 0x40
	}

	class Packet
	{
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
	}
}
