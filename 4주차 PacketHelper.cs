using System;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

namespace BattleArenaServer
{
    /// <summary>
    /// 패킷 송수신 도우미 — 동기 버전
    ///
    /// ═══════════════════════════════════════════════
    /// [4주차 실습 대상]
    ///
    /// 이 파일은 동기(Blocking) 방식으로 작성되어 있습니다.
    /// Send / Receive 를 호출하면 끝날 때까지 쓰레드가 멈춥니다.
    ///
    /// 여러분이 할 일:
    ///   Send    → SendAsync
    ///   Receive → ReceiveAsync
    /// 로 바꾸는 것입니다.
    /// ═══════════════════════════════════════════════
    /// </summary>
    public static class PacketHelper
    {
        // ═══════════════════════════════════════
        // 보내기 (동기)
        // ═══════════════════════════════════════

        public static async Task Send(Socket socket, object packet)
        {
            string json = JsonConvert.SerializeObject(packet);
            await SendAsync(socket, json);
        }

        public static async Task SendAsync(Socket socket, string json)
        {
            byte[] body   = Encoding.UTF8.GetBytes(json);
            byte[] header = BitConverter.GetBytes(body.Length);

            byte[] packet = new byte[header.Length + body.Length];
            Array.Copy(header, 0, packet, 0, header.Length);
            Array.Copy(body, 0, packet, header.Length, body.Length);

            // ★ 동기 전송 — 다 보낼 때까지 쓰레드가 멈춤
            await socket.SendAsync(packet, SocketFlags.None);
        }

        // ═══════════════════════════════════════
        // 받기 (동기)
        // ═══════════════════════════════════════

       public static async Task<string?> Receive(Socket socket) 
        {
            // 1. 길이 헤더 4바이트
            byte[]? headerBytes = ReceiveExact(socket, 4);
            if (headerBytes == null) return null;

            int bodyLength = BitConverter.ToInt32(headerBytes, 0);

            if (bodyLength <= 0 || bodyLength > 65535)
            {
                Console.WriteLine($"[경고] 비정상 패킷 길이: {bodyLength}");
                return null;
            }

            // 2. 본문
            byte[]? bodyBytes = ReceiveExact(socket, bodyLength);
            if (bodyBytes == null) return null;

            return Encoding.UTF8.GetString(bodyBytes);
        }

        /// <summary>
        /// 정확히 count 바이트를 읽을 때까지 반복 (동기)
        ///
        /// ★ 여기가 문제의 핵심입니다.
        ///   데이터가 안 오면 이 줄에서 쓰레드가 영원히 멈춥니다.
        ///   그동안 그 쓰레드는 아무 일도 못 합니다.
        /// </summary>
        private static async Task<byte[]?> ReceiveExact(Socket socket, int count)
        {
            byte[] buffer = new byte[count];
            int received = 0;

            while (received < count)
            {
                // ★ 동기 수신 — 데이터가 올 때까지 여기서 멈춤
                int n = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, received, count - received),
                    SocketFlags.None);

                if (n == 0) return null;
                received += n;
            }

            return buffer;
        }
    }
}
