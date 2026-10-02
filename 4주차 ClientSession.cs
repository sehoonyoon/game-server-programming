using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BattleArenaServer
{
    /// <summary>
    /// 클라이언트 한 명과의 연결 — 동기 버전
    ///
    /// ═══════════════════════════════════════════════
    /// [4주차 실습 대상]
    ///
    /// Run() 안의 while 루프가 통째로 동기입니다.
    /// Receive 에서 데이터를 기다리는 동안
    /// 이 쓰레드는 아무것도 못 합니다.
    ///
    /// 여러분이 할 일:
    ///   Run()  → RunAsync()
    ///   Send() → await SendAsync()
    ///   Receive() → await ReceiveAsync()
    /// ═══════════════════════════════════════════════
    /// </summary>
    public class ClientSession
    {
        public int    PlayerId { get; private set; }
        public string Nickname { get; private set; } = "Unknown";

        private readonly Socket _socket;
        private volatile bool _isConnected = true;
        private int _disconnectStarted = 0;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public ClientSession(Socket socket, int playerId)
        {
            _socket  = socket;
            PlayerId = playerId;
        }

        // ═══════════════════════════════════════
        // 수신 루프 (동기)
        // ═══════════════════════════════════════
        public async Task RunAsync()
        {
            try
            {
                Console.WriteLine($"[접속] ID:{PlayerId}  {_socket.RemoteEndPoint}");

                while (_isConnected)
                {
                    // ★ 여기서 쓰레드가 멈춥니다
                    //   클라이언트가 뭔가 보낼 때까지 영원히 대기
                    string? json = await PacketHelper.ReceiveAsync(_socket);

                    if (json == null) break;

                    Console.WriteLine($"[수신] ID:{PlayerId}  {json}");
                    await HandlePacketAsync(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[에러] ID:{PlayerId}  {ex.Message}");
            }
            finally
            {
                await DisconnectAsync();
            }
        }

        // ═══════════════════════════════════════
        // 패킷 처리
        // ═══════════════════════════════════════
        private async Task HandlePacketAsync(string json)
        {
            dynamic? packet = JsonConvert.DeserializeObject(json);
            if (packet == null) return;

            int type = (int)packet.Type;

            switch (type)
            {
                case PacketType.LOGIN:
                    await HandleLoginAsync(json);
                    break;

                default:
                    Console.WriteLine($"[경고] 모르는 패킷 타입: {type}");
                    break;
            }
        }

        private async Task HandleLoginAsync(string json)
        {
            LoginPacket? login = JsonConvert.DeserializeObject<LoginPacket>(json);
            if (login == null) return;

            Nickname = string.IsNullOrWhiteSpace(login.Nickname)
                ? $"Player{PlayerId}"
                : login.Nickname;

            Console.WriteLine($"[로그인] {Nickname} (ID:{PlayerId})");

            await SendAsync(new WelcomePacket
            {
                PlayerId = PlayerId,
                Message  = $"{Nickname}님, 배틀아레나에 오신 것을 환영합니다!"
            });

            if (!_isConnected) return;

            await GameServer.Instance.BroadcastAsync(new NoticePacket
            {
                Type    = PacketType.PLAYER_IN,
                Message = $"{Nickname}님이 입장했습니다."
            }, exceptPlayerId: PlayerId);
        }

        // ═══════════════════════════════════════
        // 전송 (동기)
        // ═══════════════════════════════════════
        public async Task SendAsync(object packet)
        {
            if (!_isConnected) return;

            try
            {
                await _sendLock.WaitAsync();

                try
                {
                    if (!_isConnected) return;

                    await PacketHelper.SendAsync(_socket, packet);
                }
                finally
                {
                    _sendLock.Release();
                }
            }
            catch
            {
                await DisconnectAsync();
            }
        }

        // ═══════════════════════════════════════
        // 연결 종료
        // ═══════════════════════════════════════
        public async Task DisconnectAsync()
        {
            if (Interlocked.Exchange(ref _disconnectStarted, 1) != 0)
                return;

            _isConnected = false;

            Console.WriteLine($"[퇴장] {Nickname} (ID:{PlayerId})");

            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch { }

            try
            {
                _socket.Close();
            }
            catch { }

            GameServer.Instance.RemoveSession(PlayerId);

            await GameServer.Instance.BroadcastAsync(new NoticePacket
            {
                Type    = PacketType.PLAYER_OUT,
                Message = $"{Nickname}님이 나갔습니다."
            });
        }
    }
}
