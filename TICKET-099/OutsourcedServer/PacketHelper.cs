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

            // 먼저 헤더 4바이트를 읽어 본문 길이를 알아냅니다.
            // Receive는 블로킹 함수라서 Task.Run으로 감싸 비동기화했습니다.
            int n = await Task.Run(() => socket.Receive(header, 0, 4, SocketFlags.None));

            if (n == 0) return null;          // 상대가 연결을 닫음

            int bodyLength = BitConverter.ToInt32(header, 0);

            // 말도 안 되는 길이가 오면 끊습니다 (악성 패킷 방어)
            if (bodyLength <= 0 || bodyLength > MAX_BODY) return null;

            byte[] body = new byte[bodyLength];

            // 알아낸 길이만큼 본문을 읽습니다.
            await Task.Run(() => socket.Receive(body, 0, bodyLength, SocketFlags.None));

            return Encoding.UTF8.GetString(body, 0, bodyLength);
        }
    }
}
