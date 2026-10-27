using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BattleArena
{
    /// <summary>
    /// TCP 패킷 송수신 담당
    ///
    /// 패킷 형식 : [ 본문길이(4바이트) ][ JSON 본문 ]
    ///
    /// 길이를 앞에 붙이는 이유는 TCP가 경계를 보장하지 않기 때문입니다.
    /// </summary>
    public static class PacketHelper
    {
        /// <summary>한 패킷의 본문 최대 크기</summary>
        private const int MAX_BODY = 8192;

        // ═══════════════════════════════════════
        // 전송
        // ═══════════════════════════════════════
        public static async Task SendAsync(Socket socket, object packet)
        {
            string json = JsonConvert.SerializeObject(packet);
            byte[] body = Encoding.UTF8.GetBytes(json);

            byte[] full = new byte[4 + body.Length];
            BitConverter.GetBytes(body.Length).CopyTo(full, 0);
            body.CopyTo(full, 4);

            int sent = 0;
            while (sent < full.Length)
            {
                sent += await socket.SendAsync(
                    new ArraySegment<byte>(full, sent, full.Length - sent),
                    SocketFlags.None);
            }
        }

        // ═══════════════════════════════════════
        // 수신
        // ═══════════════════════════════════════
        public static async Task<string?> ReceiveAsync(Socket socket)
        {
            byte[] header = new byte[4];

            // 헤더 4바이트를 다 받을 때까지 반복해서 읽습니다.
            if (!await ReceiveExactAsync(socket, header, 4)) return null;   // 상대가 연결을 닫음

            int bodyLength = BitConverter.ToInt32(header, 0);

            // 말도 안 되는 길이가 오면 끊습니다 (악성 패킷 방어)
            if (bodyLength <= 0 || bodyLength > MAX_BODY) return null;

            byte[] body = new byte[bodyLength];

            // 알아낸 길이만큼 본문을 다 받을 때까지 반복해서 읽습니다.
            if (!await ReceiveExactAsync(socket, body, bodyLength)) return null;

            return Encoding.UTF8.GetString(body, 0, bodyLength);
        }

        /// <summary>
        /// size 바이트를 다 받을 때까지 반복해서 읽습니다.
        /// TCP는 보낸 단위 그대로 도착한다는 보장이 없어서,
        /// Receive 한 번에 일부만 올 수 있습니다.
        /// 상대가 연결을 닫으면 false를 돌려줍니다.
        ///
        /// socket.ReceiveAsync 는 데이터를 기다리는 동안 스레드를 쓰지 않습니다.
        /// (Task.Run 안에서 동기 Receive 를 부르면 접속자마다 스레드가 하나씩 묶입니다)
        /// </summary>
        private static async Task<bool> ReceiveExactAsync(Socket socket, byte[] buffer, int size)
        {
            int received = 0;
            while (received < size)
            {
                int n = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, received, size - received),
                    SocketFlags.None);

                if (n == 0) return false;
                received += n;
            }
            return true;
        }
    }
}